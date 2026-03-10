/**
 * Voice Listener — sox mic capture + STT (Groq Whisper with local fallback).
 * Extracted from v2 monolith. Handles passive/active modes.
 */
import { spawn, execSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { CONFIG } from "./config.mjs";
import { logActivity } from "./activity-log.mjs";

/**
 * Record audio from mic via sox. Returns path to WAV or null if silence.
 */
export function recordAudio(seconds, silenceDuration) {
  return new Promise((resolve, reject) => {
    const outFile = path.join(CONFIG.tmpDir, `chunk-${Date.now()}.wav`);
    const silDur = silenceDuration || CONFIG.passiveSilenceSecs;
    const args = [
      "-d", "-r", "16000", "-c", "1", "-b", "16",
      outFile,
      "trim", "0", String(seconds),
      "silence", "1", "0.1", CONFIG.silenceThreshold,
      "1", silDur, CONFIG.silenceThreshold,
    ];

    const proc = spawn("sox", args, { stdio: ["pipe", "pipe", "pipe"] });
    let stderr = "";
    proc.stderr.on("data", (d) => stderr += d.toString());

    const timeout = setTimeout(() => proc.kill("SIGTERM"), (seconds + 2) * 1000);

    proc.on("close", () => {
      clearTimeout(timeout);
      if (fs.existsSync(outFile) && fs.statSync(outFile).size > 1000) {
        resolve(outFile);
      } else {
        if (fs.existsSync(outFile)) try { fs.unlinkSync(outFile); } catch {}
        resolve(null);
      }
    });

    proc.on("error", (err) => {
      clearTimeout(timeout);
      reject(err);
    });
  });
}

/**
 * Transcribe audio — tries Groq Whisper, falls back to local Whisper.
 */
export async function transcribe(audioPath) {
  try {
    return await transcribeGroq(audioPath);
  } catch (err) {
    if (err.message?.includes("429") || err.message?.includes("rate limit") || err.message?.includes("Rate limit")) {
      log("\x1b[33mGroq Whisper rate-limited, falling back to local Whisper...\x1b[0m");
      return await transcribeLocal(audioPath);
    }
    throw err;
  }
}

async function transcribeGroq(audioPath) {
  const formData = new FormData();
  formData.append("file", new Blob([fs.readFileSync(audioPath)]), "audio.wav");
  formData.append("model", CONFIG.groqWhisperModel);
  formData.append("language", "en");
  formData.append("response_format", "json");

  const res = await fetch("https://api.groq.com/openai/v1/audio/transcriptions", {
    method: "POST",
    headers: { Authorization: `Bearer ${CONFIG.groqApiKey}` },
    body: formData,
  });

  if (!res.ok) {
    const text = await res.text();
    throw new Error(`Whisper ${res.status}: ${text.slice(0, 200)}`);
  }

  const data = await res.json();
  return data.text?.trim() || "";
}

async function transcribeLocal(audioPath) {
  try {
    const outDir = path.join(CONFIG.tmpDir, "whisper-out");
    fs.mkdirSync(outDir, { recursive: true });
    execSync(
      `whisper "${audioPath}" --model ${CONFIG.whisperModel} --language en --output_format json --output_dir "${outDir}"`,
      { timeout: 30000, encoding: "utf-8", stdio: "pipe" }
    );
    const baseName = path.basename(audioPath, ".wav");
    const jsonPath = `${outDir}/${baseName}.json`;
    if (fs.existsSync(jsonPath)) {
      const data = JSON.parse(fs.readFileSync(jsonPath, "utf-8"));
      try { fs.unlinkSync(jsonPath); } catch {}
      const text = data.text?.trim() || "";
      if (text) log(`\x1b[32m[Local Whisper] "${text}"\x1b[0m`);
      return text;
    }
    return "";
  } catch (e) {
    log(`\x1b[31mLocal Whisper failed: ${e.message?.slice(0, 100)}\x1b[0m`);
    return "";
  }
}

/**
 * Check if text contains a wake word. Returns text after wake word, or null.
 */
export function matchWakeWord(text) {
  const lower = text.toLowerCase().trim();
  for (const ww of CONFIG.wakeWords) {
    const idx = lower.indexOf(ww);
    if (idx !== -1) return lower.slice(idx + ww.length).trim();
  }
  return null;
}

function log(msg) {
  const ts = new Date().toLocaleTimeString();
  console.log(`\x1b[90m[${ts}]\x1b[0m ${msg}`);
}
