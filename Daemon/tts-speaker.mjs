/**
 * TTS Speaker — text-to-speech output.
 * Self-contained: macOS `say` as primary, optional Edge TTS via external server.
 */
import { execSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { CONFIG } from "./config.mjs";
import { logActivity } from "./activity-log.mjs";

let edgeTtsUrl = null; // set if external TTS server is available

export function setEdgeTtsUrl(url) {
  edgeTtsUrl = url;
}

export async function speak(text) {
  if (!text) return;
  const start = Date.now();

  // Try Edge TTS first if configured
  if (edgeTtsUrl) {
    try {
      const res = await fetch(`${edgeTtsUrl}/agent/tts`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ text, voice: CONFIG.ttsVoice }),
      });
      if (res.ok) {
        const outFile = path.join(CONFIG.tmpDir, `speak-${Date.now()}.mp3`);
        const buffer = Buffer.from(await res.arrayBuffer());
        fs.writeFileSync(outFile, buffer);
        execSync(`afplay "${outFile}"`, { stdio: "pipe", timeout: 30000 });
        try { fs.unlinkSync(outFile); } catch {}
        logActivity({ agent: "jarvis-daemon", action: "speak", success: true, durationMs: Date.now() - start, meta: { provider: "edge-tts" } });
        return;
      }
    } catch {}
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
