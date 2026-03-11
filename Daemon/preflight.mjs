#!/usr/bin/env node
/**
 * Preflight checks for Jarvis Daemon v3.
 * Validates all dependencies before startup.
 * Run standalone: node preflight.mjs
 * Or import: import { runPreflight } from "./preflight.mjs"
 */
import fs from "node:fs";
import net from "node:net";
import { execSync } from "node:child_process";
import { CONFIG } from "./config.mjs";

const PASS = "\x1b[32m✓\x1b[0m";
const FAIL = "\x1b[31m✗\x1b[0m";
const WARN = "\x1b[33m⚠\x1b[0m";

function check(label, ok, detail) {
  const icon = ok === true ? PASS : ok === "warn" ? WARN : FAIL;
  console.log(`  ${icon} ${label}${detail ? ` — ${detail}` : ""}`);
  return ok === true;
}

function binExists(name) {
  try {
    return execSync(`which ${name}`, { encoding: "utf-8", stdio: "pipe" }).trim();
  } catch {
    return null;
  }
}

async function checkPort(port, host = "127.0.0.1") {
  return new Promise((resolve) => {
    const server = net.createServer();
    server.once("error", () => resolve(false));
    server.once("listening", () => {
      server.close();
      resolve(true);
    });
    server.listen(port, host);
  });
}

export async function runPreflight() {
  console.log("\n\x1b[36m  Jarvis Daemon — Preflight Check\x1b[0m\n");

  let criticalFail = false;
  let warnings = 0;

  // Node.js version
  const nodeVersion = process.version;
  const major = parseInt(nodeVersion.slice(1));
  if (!check("Node.js", major >= 18, nodeVersion)) criticalFail = true;

  // sox
  const soxPath = binExists("sox") || (fs.existsSync("/opt/homebrew/bin/sox") ? "/opt/homebrew/bin/sox" : null);
  if (!check("sox (audio recording)", !!soxPath, soxPath || "install: brew install sox")) criticalFail = true;

  // edge-tts (non-critical — falls back to macOS say)
  const edgeTtsCandidates = [
    "/Library/Frameworks/Python.framework/Versions/3.14/bin/edge-tts",
    "/Library/Frameworks/Python.framework/Versions/3.13/bin/edge-tts",
    "/opt/homebrew/bin/edge-tts",
    "/usr/local/bin/edge-tts",
  ];
  let edgeTtsPath = binExists("edge-tts");
  if (!edgeTtsPath) {
    for (const p of edgeTtsCandidates) {
      if (fs.existsSync(p)) { edgeTtsPath = p; break; }
    }
  }
  if (!edgeTtsPath) {
    check("edge-tts (neural TTS)", "warn", "not found — will use macOS say. install: pip install edge-tts");
    warnings++;
  } else {
    check("edge-tts (neural TTS)", true, edgeTtsPath);
  }

  // .env file
  const envPath = `${CONFIG.tmpDir}/../.env`;
  const daemonEnv = "/Users/jamestunick/Applications/jAIrvisXR/Daemon/.env";
  const envExists = fs.existsSync(daemonEnv);
  if (!envExists) {
    check(".env file", "warn", "not found — API keys may be missing");
    warnings++;
  } else {
    const stat = fs.lstatSync(daemonEnv);
    if (stat.isSymbolicLink()) {
      const target = fs.readlinkSync(daemonEnv);
      const resolved = fs.existsSync(target);
      if (!check(".env file (symlink)", resolved, resolved ? target : `broken symlink → ${target}`)) criticalFail = true;
    } else {
      check(".env file", true, "present");
    }
  }

  // API keys
  const hasGroq = !!CONFIG.groqApiKey;
  const hasAnthropic = !!process.env.ANTHROPIC_API_KEY;
  const hasGemini = !!CONFIG.geminiApiKey;
  const hasAnyLlm = hasGroq || hasAnthropic || hasGemini;
  if (!check("LLM API key (at least one)", hasAnyLlm,
    [hasGroq && "Groq", hasAnthropic && "Anthropic", hasGemini && "Gemini"].filter(Boolean).join(", ") || "none set")) {
    criticalFail = true;
  }

  const hasFishAudio = !!process.env.FISH_API_KEY;
  if (!hasFishAudio) {
    check("Fish Audio API key", "warn", "not set — will fall back to ElevenLabs/edge-tts/say");
    warnings++;
  } else {
    check("Fish Audio API key", true, "set");
  }

  const hasElevenLabs = !!process.env.ELEVENLABS_API_KEY;
  if (!hasElevenLabs) {
    check("ElevenLabs API key", "warn", "not set — will use edge-tts/say");
    warnings++;
  } else {
    check("ElevenLabs API key", true, "set");
  }

  // Tmp directory
  try {
    if (!fs.existsSync(CONFIG.tmpDir)) fs.mkdirSync(CONFIG.tmpDir, { recursive: true });
    const testFile = `${CONFIG.tmpDir}/.preflight-test`;
    fs.writeFileSync(testFile, "ok");
    fs.unlinkSync(testFile);
    check("Temp directory", true, CONFIG.tmpDir);
  } catch {
    if (!check("Temp directory", false, `cannot write to ${CONFIG.tmpDir}`)) criticalFail = true;
  }

  // Port
  const portFree = await checkPort(CONFIG.httpPort, CONFIG.httpHost);
  if (!portFree) {
    check(`Port ${CONFIG.httpPort}`, "warn", "already in use — HTTP API may conflict");
    warnings++;
  } else {
    check(`Port ${CONFIG.httpPort}`, true, "available");
  }

  // Microphone (quick test — skip if sox missing)
  if (soxPath) {
    try {
      execSync(`"${soxPath}" -d -r 16000 -c 1 -b 16 /dev/null trim 0 0.1 2>/dev/null`, {
        stdio: "pipe",
        timeout: 5000,
      });
      check("Microphone access", true, "working");
    } catch {
      check("Microphone access", "warn", "sox could not record — check System Settings > Privacy > Microphone");
      warnings++;
    }
  }

  // Summary
  console.log();
  if (criticalFail) {
    console.log("  \x1b[31mPreflight FAILED — fix critical issues above before starting.\x1b[0m\n");
    return false;
  }
  if (warnings > 0) {
    console.log(`  \x1b[33mPreflight passed with ${warnings} warning(s).\x1b[0m\n`);
  } else {
    console.log("  \x1b[32mAll checks passed.\x1b[0m\n");
  }
  return true;
}

// Run standalone
if (import.meta.url === `file://${process.argv[1]}`) {
  const ok = await runPreflight();
  process.exit(ok ? 0 : 1);
}
