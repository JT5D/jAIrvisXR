/**
 * TTS Speaker — text-to-speech output.
 * Priority: edge-tts CLI (free, high quality) → macOS say (fallback).
 */
import { execSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { CONFIG } from "./config.mjs";
import { logActivity } from "./activity-log.mjs";

export const EDGE_TTS_BIN = findEdgeTts();

function findEdgeTts() {
  // Try which first (works in interactive shells)
  try {
    return execSync("which edge-tts", { encoding: "utf-8", stdio: "pipe" }).trim();
  } catch { /* not in PATH */ }
  // Check common install locations (launchd has minimal PATH)
  const candidates = [
    "/Library/Frameworks/Python.framework/Versions/3.14/bin/edge-tts",
    "/Library/Frameworks/Python.framework/Versions/3.13/bin/edge-tts",
    "/Library/Frameworks/Python.framework/Versions/3.12/bin/edge-tts",
    "/opt/homebrew/bin/edge-tts",
    "/usr/local/bin/edge-tts",
  ];
  for (const p of candidates) {
    if (fs.existsSync(p)) return p;
  }
  return null;
}

export async function speak(text) {
  if (!text) return;
  const start = Date.now();

  // Try edge-tts CLI first (free, high-quality neural voices)
  if (EDGE_TTS_BIN) {
    try {
      const outFile = path.join(CONFIG.tmpDir, `speak-${Date.now()}.mp3`);
      const escaped = text.replace(/"/g, '\\"').replace(/`/g, "").replace(/\$/g, "");
      execSync(
        `"${EDGE_TTS_BIN}" --voice "${CONFIG.ttsVoice}" --text "${escaped}" --write-media "${outFile}"`,
        { stdio: "pipe", timeout: 15000 }
      );
      if (fs.existsSync(outFile) && fs.statSync(outFile).size > 100) {
        execSync(`afplay "${outFile}"`, { stdio: "pipe", timeout: 30000 });
        try { fs.unlinkSync(outFile); } catch {}
        logActivity({ agent: "jarvis-daemon", action: "speak", success: true, durationMs: Date.now() - start, meta: { provider: "edge-tts", voice: CONFIG.ttsVoice } });
        return;
      }
    } catch (err) {
      const msg = err.message?.slice(0, 80) || "unknown";
      console.log(`\x1b[33mEdge TTS failed: ${msg}, falling back to macOS say\x1b[0m`);
    }
  }

  // Fallback: macOS say
  try {
    const escaped = text.replace(/"/g, '\\"').replace(/`/g, "");
    execSync(`say -v "${CONFIG.macVoice}" "${escaped}"`, { stdio: "pipe", timeout: 30000 });
    logActivity({ agent: "jarvis-daemon", action: "speak", success: true, durationMs: Date.now() - start, meta: { provider: "macos-say" } });
  } catch (err) {
    logActivity({ agent: "jarvis-daemon", action: "speak", success: false, error: err.message?.slice(0, 100) });
  }
}
