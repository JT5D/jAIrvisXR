/**
 * Daemon process management — start, stop, and monitor the Jarvis daemon.
 */
import { spawn } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { isRunning, health } from "./client.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PID_FILE = "/tmp/jarvis-daemon/jarvis.pid";
const TMP_DIR = "/tmp/jarvis-daemon";
const LOG_FILE = "/tmp/jarvis-daemon/jarvis-v3.log";

function getDaemonPath() {
  // Check if daemon is bundled with CLI package
  const bundled = path.resolve(__dirname, "..", "daemon", "jarvis-daemon.mjs");
  if (fs.existsSync(bundled)) return bundled;
  // Fallback: development path
  const dev = path.resolve(__dirname, "..", "..", "..", "Daemon", "jarvis-daemon.mjs");
  if (fs.existsSync(dev)) return dev;
  throw new Error("Daemon not found. Reinstall @jairvisxr/cli.");
}

function readPid() {
  try {
    return parseInt(fs.readFileSync(PID_FILE, "utf-8").trim());
  } catch {
    return null;
  }
}

function isProcessAlive(pid) {
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
}

export async function startDaemon({ port = 7437, verbose = false } = {}) {
  // Check if already running
  if (await isRunning(port)) {
    const data = await health(port);
    console.log(`Jarvis is already running (uptime: ${Math.round(data.uptime)}s)`);
    return;
  }

  // Check stale PID
  const existingPid = readPid();
  if (existingPid && isProcessAlive(existingPid)) {
    console.log(`Jarvis process exists (PID ${existingPid}) but API not responding. Killing...`);
    try { process.kill(existingPid, "SIGTERM"); } catch {}
    await new Promise(r => setTimeout(r, 1000));
  }

  // Ensure tmp dir
  if (!fs.existsSync(TMP_DIR)) fs.mkdirSync(TMP_DIR, { recursive: true });

  const daemonPath = getDaemonPath();
  const daemonDir = path.dirname(daemonPath);
  const env = { ...process.env, JARVIS_PORT: String(port) };

  console.log("Starting Jarvis daemon...");

  const out = fs.openSync(LOG_FILE, "a");
  const err = fs.openSync(LOG_FILE, "a");

  const child = spawn("node", [daemonPath], {
    cwd: daemonDir,
    env,
    detached: true,
    stdio: ["ignore", out, err],
  });

  fs.writeFileSync(PID_FILE, String(child.pid));
  child.unref();

  // Wait for daemon to be ready
  for (let i = 0; i < 15; i++) {
    await new Promise(r => setTimeout(r, 1000));
    if (await isRunning(port)) {
      const data = await health(port);
      console.log(`Jarvis online (PID ${child.pid}, v${data.version})`);
      if (verbose) console.log(`  Log: ${LOG_FILE}`);
      return;
    }
  }

  console.error("Jarvis failed to start. Check log:", LOG_FILE);
  process.exit(1);
}

export async function stopDaemon({ port = 7437 } = {}) {
  const pid = readPid();

  if (pid && isProcessAlive(pid)) {
    console.log(`Stopping Jarvis (PID ${pid})...`);
    process.kill(pid, "SIGTERM");
    try { fs.unlinkSync(PID_FILE); } catch {}

    // Wait for shutdown
    for (let i = 0; i < 10; i++) {
      await new Promise(r => setTimeout(r, 500));
      if (!isProcessAlive(pid)) {
        console.log("Jarvis offline.");
        return;
      }
    }
    console.log("Force killing...");
    try { process.kill(pid, "SIGKILL"); } catch {}
    console.log("Jarvis offline.");
  } else if (await isRunning(port)) {
    console.log("Jarvis running but no PID file. Send stop via API...");
    // Can't stop cleanly without PID — inform user
    console.log("Use: launchctl unload ~/Library/LaunchAgents/com.jairvisxr.daemon.plist");
  } else {
    console.log("Jarvis is not running.");
  }
}

export async function statusDaemon({ port = 7437 } = {}) {
  if (!(await isRunning(port))) {
    console.log("Jarvis is offline.");
    const pid = readPid();
    if (pid) console.log(`  Stale PID file: ${pid} (process dead)`);
    return;
  }

  const data = await health(port);
  const pid = readPid();

  console.log("\x1b[36mJarvis Status\x1b[0m");
  console.log(`  Status:   \x1b[32monline\x1b[0m`);
  console.log(`  Version:  ${data.version}`);
  console.log(`  Uptime:   ${Math.round(data.uptime)}s`);
  if (pid) console.log(`  PID:      ${pid}`);
  console.log(`  API:      http://127.0.0.1:${port}`);
  console.log(`  Log:      ${LOG_FILE}`);
}
