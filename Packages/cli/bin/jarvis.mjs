#!/usr/bin/env node
/**
 * Jarvis CLI — voice AI agent control interface.
 *
 * Usage:
 *   jarvis start              Start daemon
 *   jarvis stop               Stop daemon
 *   jarvis status             Check daemon health
 *   jarvis ask "question"     Ask Jarvis (LLM response)
 *   jarvis say "text"         Make Jarvis speak (TTS)
 *   jarvis logs               View recent activity
 *   jarvis setup              First-run setup wizard
 */
import { parseArgs } from "node:util";
import { startDaemon, stopDaemon, statusDaemon } from "../lib/daemon.mjs";
import { sendCommand, getLogs, isRunning } from "../lib/client.mjs";
import { runSetup } from "../lib/setup.mjs";

const args = parseArgs({
  options: {
    port: { type: "string", short: "p", default: "7437" },
    verbose: { type: "boolean", short: "v", default: false },
    help: { type: "boolean", short: "h", default: false },
  },
  allowPositionals: true,
  strict: false,
});

const port = parseInt(args.values.port);
const [command, ...rest] = args.positionals;
const text = rest.join(" ");

if (args.values.help || !command) {
  console.log(`
\x1b[36mJarvis\x1b[0m — always-on voice AI agent

\x1b[33mUsage:\x1b[0m
  jarvis start              Start the daemon
  jarvis stop               Stop the daemon
  jarvis status             Check health & uptime
  jarvis ask "question"     Send a text command
  jarvis say "text"         Speak text via TTS
  jarvis logs               View recent activity
  jarvis bench              Run benchmark suite (20 prompts)
  jarvis setup              First-run setup wizard

\x1b[33mOptions:\x1b[0m
  -p, --port <port>         Daemon port (default: 7437)
  -v, --verbose             Verbose output
  -h, --help                Show this help

\x1b[33mExamples:\x1b[0m
  jarvis start
  jarvis ask "What time is it?"
  jarvis say "Hello, I am Jarvis"
`);
  process.exit(0);
}

try {
  switch (command) {
    case "start":
      await startDaemon({ port, verbose: args.values.verbose });
      break;

    case "stop":
      await stopDaemon({ port });
      break;

    case "status":
      await statusDaemon({ port });
      break;

    case "ask": {
      if (!text) {
        console.error("Usage: jarvis ask \"your question\"");
        process.exit(1);
      }
      if (!(await isRunning(port))) {
        console.error("Jarvis is not running. Start with: jarvis start");
        process.exit(1);
      }
      const result = await sendCommand(text, port);
      console.log(result.response || result.result || JSON.stringify(result));
      if (args.values.verbose && result.timing) {
        const t = result.timing;
        console.log(`\x1b[90m  timing: llm=${t.llmMs}ms tool=${t.toolMs}ms total=${t.totalMs}ms provider=${result.provider || "unknown"}\x1b[0m`);
      }
      break;
    }

    case "say": {
      if (!text) {
        console.error("Usage: jarvis say \"text to speak\"");
        process.exit(1);
      }
      if (!(await isRunning(port))) {
        console.error("Jarvis is not running. Start with: jarvis start");
        process.exit(1);
      }
      await sendCommand(`say: ${text}`, port);
      console.log("(spoken)");
      break;
    }

    case "logs": {
      if (!(await isRunning(port))) {
        console.error("Jarvis is not running.");
        process.exit(1);
      }
      const logs = await getLogs(20, null, port);
      for (const entry of logs) {
        const ts = new Date(entry.ts).toLocaleTimeString();
        const status = entry.success ? "\x1b[32m✓\x1b[0m" : "\x1b[31m✗\x1b[0m";
        console.log(`  ${status} [${ts}] ${entry.action} ${entry.meta?.userText || ""}`);
      }
      break;
    }

    case "bench": {
      if (!(await isRunning(port))) {
        console.error("Jarvis is not running. Start with: jarvis start");
        process.exit(1);
      }
      const { execSync } = await import("node:child_process");
      const { fileURLToPath } = await import("node:url");
      const { dirname, resolve } = await import("node:path");
      const __dirname = dirname(fileURLToPath(import.meta.url));
      const benchPath = resolve(__dirname, "../../../Daemon/bench.mjs");
      execSync(`node "${benchPath}" --port ${port}`, { stdio: "inherit" });
      break;
    }

    case "setup":
      await runSetup();
      break;

    default:
      console.error(`Unknown command: ${command}. Run: jarvis --help`);
      process.exit(1);
  }
} catch (err) {
  if (err.cause?.code === "ECONNREFUSED") {
    console.error("Cannot connect to Jarvis. Start with: jarvis start");
  } else {
    console.error(`Error: ${err.message}`);
  }
  process.exit(1);
}
