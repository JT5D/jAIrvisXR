/**
 * TTS Speaker — text-to-speech output.
 * Priority: Fish Audio (highest quality) → ElevenLabs → edge-tts CLI → macOS say.
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

async function fishAudioSpeak(text) {
  if (!CONFIG.fishAudioApiKey) return false;

  const outFile = path.join(CONFIG.tmpDir, `speak-fish-${Date.now()}.mp3`);
  const body = {
    text,
    format: "mp3",
  };
  if (CONFIG.fishAudioReferenceId) {
    body.reference_id = CONFIG.fishAudioReferenceId;
  }

  const res = await fetch("https://api.fish.audio/v1/tts", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "Authorization": `Bearer ${CONFIG.fishAudioApiKey}`,
      "model": CONFIG.fishAudioModel,
    },
    body: JSON.stringify(body),
  });

  if (!res.ok) throw new Error(`Fish Audio ${res.status}: ${(await res.text()).slice(0, 100)}`);

  const buffer = Buffer.from(await res.arrayBuffer());
  fs.writeFileSync(outFile, buffer);
  execSync(`afplay "${outFile}"`, { stdio: "pipe", timeout: 30000 });
  try { fs.unlinkSync(outFile); } catch {}
  return true;
}

async function elevenlabsSpeak(text) {
  if (!CONFIG.elevenlabsApiKey) return false;

  const outFile = path.join(CONFIG.tmpDir, `speak-11labs-${Date.now()}.mp3`);
  const res = await fetch(`https://api.elevenlabs.io/v1/text-to-speech/${CONFIG.elevenlabsVoiceId}`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "xi-api-key": CONFIG.elevenlabsApiKey,
    },
    body: JSON.stringify({
      text,
      model_id: "eleven_turbo_v2_5",
      voice_settings: { stability: 0.5, similarity_boost: 0.75 },
    }),
  });

  if (!res.ok) throw new Error(`ElevenLabs ${res.status}`);

  const buffer = Buffer.from(await res.arrayBuffer());
  fs.writeFileSync(outFile, buffer);
  execSync(`afplay "${outFile}"`, { stdio: "pipe", timeout: 30000 });
  try { fs.unlinkSync(outFile); } catch {}
  return true;
}

export async function speak(text) {
  if (!text) return;
  const start = Date.now();

  // Try Fish Audio first (highest quality, voice cloning capable)
  try {
    if (await fishAudioSpeak(text)) {
      logActivity({ agent: "jarvis-daemon", action: "speak", success: true, durationMs: Date.now() - start, meta: { provider: "fish-audio", model: CONFIG.fishAudioModel } });
      return;
    }
  } catch (err) {
    console.log(`\x1b[33mFish Audio failed: ${err.message?.slice(0, 80)}, trying ElevenLabs\x1b[0m`);
  }

  // Try ElevenLabs (high quality neural voices)
  try {
    if (await elevenlabsSpeak(text)) {
      logActivity({ agent: "jarvis-daemon", action: "speak", success: true, durationMs: Date.now() - start, meta: { provider: "elevenlabs", voice: CONFIG.elevenlabsVoiceId } });
      return;
    }
  } catch (err) {
    console.log(`\x1b[33mElevenLabs failed: ${err.message?.slice(0, 80)}, trying edge-tts\x1b[0m`);
  }

  // Try edge-tts CLI (free, high-quality neural voices)
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
