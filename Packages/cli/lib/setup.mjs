/**
 * First-run setup wizard for Jarvis.
 * Checks dependencies, creates config, tests the pipeline.
 */
import { execSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { createInterface } from "node:readline";

const JARVIS_DIR = path.join(process.env.HOME || "~", ".jarvis");
const ENV_FILE = path.join(JARVIS_DIR, ".env");

const PASS = "\x1b[32m✓\x1b[0m";
const FAIL = "\x1b[31m✗\x1b[0m";
const WARN = "\x1b[33m⚠\x1b[0m";

function which(bin) {
  try {
    return execSync(`which ${bin}`, { encoding: "utf-8", stdio: "pipe" }).trim();
  } catch {
    return null;
  }
}

function ask(question) {
  const rl = createInterface({ input: process.stdin, output: process.stdout });
  return new Promise(resolve => {
    rl.question(question, answer => {
      rl.close();
      resolve(answer.trim());
    });
  });
}

export async function runSetup() {
  console.log("\n\x1b[36m  Jarvis — First-Run Setup\x1b[0m\n");

  // 1. Check Node version
  const major = parseInt(process.version.slice(1));
  console.log(`  ${major >= 18 ? PASS : FAIL} Node.js ${process.version}`);
  if (major < 18) {
    console.log("    Node.js 18+ required. Upgrade: https://nodejs.org");
    process.exit(1);
  }

  // 2. Check sox
  const sox = which("sox");
  if (sox) {
    console.log(`  ${PASS} sox (audio recording) — ${sox}`);
  } else {
    console.log(`  ${FAIL} sox not found`);
    console.log("    Install: brew install sox (macOS) / apt install sox (Linux)");
  }

  // 3. Check edge-tts
  const edgeTts = which("edge-tts");
  if (edgeTts) {
    console.log(`  ${PASS} edge-tts (neural TTS) — ${edgeTts}`);
  } else {
    console.log(`  ${WARN} edge-tts not found (will use macOS say as fallback)`);
    console.log("    Install: pip install edge-tts");
  }

  // 4. Setup .jarvis directory
  if (!fs.existsSync(JARVIS_DIR)) {
    fs.mkdirSync(JARVIS_DIR, { recursive: true });
    console.log(`  ${PASS} Created ${JARVIS_DIR}`);
  } else {
    console.log(`  ${PASS} Config directory exists — ${JARVIS_DIR}`);
  }

  // 5. API keys
  if (fs.existsSync(ENV_FILE)) {
    console.log(`  ${PASS} .env file exists — ${ENV_FILE}`);
  } else {
    console.log(`\n  ${WARN} No .env file found. Let's set up API keys.\n`);
    console.log("  API keys enable AI providers. All are optional (at least one LLM key needed).\n");

    const groqKey = await ask("  Groq API key (free, fastest — https://console.groq.com): ");
    const anthropicKey = await ask("  Anthropic API key (Claude — https://console.anthropic.com): ");
    const fishKey = await ask("  Fish Audio API key (TTS — https://fish.audio): ");

    const envContent = [
      "# Jarvis Daemon — API Keys",
      `GROQ_API_KEY=${groqKey}`,
      `ANTHROPIC_API_KEY=${anthropicKey}`,
      `FISH_API_KEY=${fishKey}`,
      "# Optional:",
      "# GEMINI_API_KEY=",
      "# ELEVENLABS_API_KEY=",
      "# FISH_AUDIO_VOICE_ID=  (custom voice clone ID)",
      "",
    ].join("\n");

    fs.writeFileSync(ENV_FILE, envContent);
    console.log(`\n  ${PASS} Created ${ENV_FILE}`);
  }

  console.log("\n  \x1b[32mSetup complete.\x1b[0m Run: jarvis start\n");
}
