#!/usr/bin/env node
/**
 * Jarvis Daemon v3 — always-on voice + orchestrator.
 * Single entry point. Self-supervising. Modular architecture.
 *
 * Pipeline: mic → sox → Groq Whisper (STT) → wake word →
 *           LLM (Groq/Gemini/Ollama) + tools → macOS say (TTS)
 *
 * Usage: node jarvis-daemon.mjs
 * Requires: sox (brew install sox)
 */
import fs from "node:fs";
import { CONFIG } from "./config.mjs";
import { memoryInit, memoryWrite } from "./shared-memory.mjs";
import { logActivity } from "./activity-log.mjs";
import { contextInit, contextToPrompt } from "./context-store.mjs";
import { recordAudio, transcribe, matchWakeWord } from "./voice-listener.mjs";
import { speak } from "./tts-speaker.mjs";
import { TOOL_SCHEMAS } from "./tool-executor.mjs";
import { startHttpApi, stopHttpApi } from "./http-api.mjs";
import { initProviders, getActiveProvider, getResponse } from "./llm-client.mjs";

// State
let mode = "passive";
let silentRounds = 0;
let conversationHistory = [];

function log(msg) {
  const ts = new Date().toLocaleTimeString();
  console.log(`\x1b[90m[${ts}]\x1b[0m ${msg}`);
}

function logJarvis(msg) {
  console.log(`\x1b[36m  Jarvis:\x1b[0m ${msg}`);
}

function logUser(msg) {
  console.log(`\x1b[33m  You:\x1b[0m ${msg}`);
}

// Ensure tmp dir
if (!fs.existsSync(CONFIG.tmpDir)) fs.mkdirSync(CONFIG.tmpDir, { recursive: true });

/**
 * Get LLM response with context injection.
 */
async function getLlmResponse(text) {
  const contextBlock = contextToPrompt();
  const fullSystemPrompt = CONFIG.systemPrompt + contextBlock;
  return await getResponse(text, conversationHistory, fullSystemPrompt, TOOL_SCHEMAS);
}

/**
 * Main loop — always listening.
 */
async function main() {
  // Initialize all subsystems
  const provider = initProviders();
  memoryInit();
  contextInit();

  memoryWrite("jarvis-status", "online");
  memoryWrite("jarvis-capabilities", [
    "voice-listen", "voice-speak", "open-browser", "run-shell",
    "read-file", "write-file", "search-project", "shared-memory",
    "http-api", "sub-agents", "context-store",
  ]);

  logActivity({
    agent: "jarvis-daemon",
    action: "startup",
    success: true,
    meta: { version: "3.0", tools: TOOL_SCHEMAS.length, provider },
  });

  // Banner
  const providers = ["groq", "gemini", "ollama"]
    .filter(p => p === "groq" ? CONFIG.groqApiKey : p === "gemini" ? CONFIG.geminiApiKey : true)
    .map(p => p === "groq" ? "Groq" : p === "gemini" ? "Gemini" : "Ollama (local)")
    .join(" → ");

  console.log("\n\x1b[36m━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\x1b[0m");
  console.log("\x1b[36m  Jarvis Daemon v3 — jAIrvisXR\x1b[0m");
  console.log("\x1b[36m━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\x1b[0m");
  console.log(`  Brain:    ${providers} (auto-failover)`);
  console.log(`  Voice:    macOS say (${CONFIG.macVoice})`);
  console.log(`  STT:      Groq Whisper → local Whisper (${CONFIG.whisperModel})`);
  console.log(`  Wake:     "Hey Jarvis" / "Jarvis"`);
  console.log(`  Tools:    ${TOOL_SCHEMAS.length}`);
  console.log(`  Memory:   ${CONFIG.memFile}`);
  console.log(`  Log:      ${CONFIG.logFile}`);

  // Start HTTP API
  startHttpApi(getLlmResponse);

  console.log(`  Partner:  Claude Code (shared memory)`);
  console.log(`\x1b[90m  Press Ctrl+C to stop\x1b[0m\n`);

  // Heartbeat
  const heartbeatTimer = setInterval(() => {
    memoryWrite("jarvis-heartbeat", Date.now());
  }, 30_000);
  heartbeatTimer.unref();

  // Initial heartbeat
  memoryWrite("jarvis-heartbeat", Date.now());

  const providerNames = { groq: "Groq", gemini: "Gemini", ollama: "local Ollama" };
  await speak(`Jarvis v3 online with ${providerNames[provider] || provider}.`);

  // Main voice loop
  while (true) {
    try {
      const isActive = mode === "active";
      const seconds = isActive ? CONFIG.activeRecordSeconds : CONFIG.recordSeconds;
      const silDur = isActive ? CONFIG.activeSilenceSecs : CONFIG.passiveSilenceSecs;
      const audioPath = await recordAudio(seconds, silDur);

      if (!audioPath) {
        if (mode === "active") {
          silentRounds++;
          if (silentRounds >= CONFIG.silenceRoundsBeforePassive) {
            mode = "passive";
            silentRounds = 0;
            log("Returning to passive (extended silence)");
          } else {
            log(`Still listening... (${silentRounds}/${CONFIG.silenceRoundsBeforePassive})`);
          }
        }
        continue;
      }
      silentRounds = 0;

      const text = await transcribe(audioPath);
      try { fs.unlinkSync(audioPath); } catch {}

      if (!text || text.length < 2) continue;

      if (mode === "passive") {
        const afterWake = matchWakeWord(text);
        if (afterWake !== null) {
          log("\x1b[32m★ Wake word detected!\x1b[0m");
          mode = "active";

          if (afterWake.length > 2) {
            logUser(afterWake);
            mode = "processing";
            const response = await getLlmResponse(afterWake);
            logJarvis(response);
            await speak(response);
            mode = "active";
          } else {
            await speak("Yes?");
          }
        }
      } else if (mode === "active") {
        logUser(text);
        mode = "processing";
        const response = await getLlmResponse(text);
        logJarvis(response);
        await speak(response);
        mode = "active";
      }
    } catch (err) {
      log(`\x1b[31mError: ${err.message}\x1b[0m`);
      logActivity({
        agent: "jarvis-daemon",
        action: "error",
        success: false,
        error: err.message,
      });
      mode = "passive";
      await new Promise(r => setTimeout(r, 2000));
    }
  }
}

// Graceful shutdown
process.on("SIGINT", () => {
  console.log("\n\x1b[36mJarvis v3 going offline.\x1b[0m");
  memoryWrite("jarvis-status", "offline");
  logActivity({ agent: "jarvis-daemon", action: "shutdown", success: true });
  stopHttpApi();
  // Clean up temp audio files
  try {
    for (const f of fs.readdirSync(CONFIG.tmpDir)) {
      if (f.startsWith("chunk-") && f.endsWith(".wav")) {
        fs.unlinkSync(`${CONFIG.tmpDir}/${f}`);
      }
    }
  } catch {}
  process.exit(0);
});

process.on("SIGTERM", () => process.emit("SIGINT"));

main();
