/**
 * Activity Log — structured append-only JSONL log for all agent actions.
 */
import fs from "node:fs";
import { CONFIG } from "./config.mjs";

const LOG_FILE = CONFIG.logFile;
const MAX_LOG_SIZE = 5 * 1024 * 1024; // 5MB rotate

function ensureDir() {
  if (!fs.existsSync(CONFIG.tmpDir)) fs.mkdirSync(CONFIG.tmpDir, { recursive: true });
}

export function logActivity(entry) {
  ensureDir();
  const record = { ts: new Date().toISOString(), ...entry };

  try {
    if (fs.existsSync(LOG_FILE) && fs.statSync(LOG_FILE).size > MAX_LOG_SIZE) {
      const rotated = LOG_FILE.replace(".jsonl", `-${Date.now()}.jsonl`);
      fs.renameSync(LOG_FILE, rotated);
    }
  } catch {}

  fs.appendFileSync(LOG_FILE, JSON.stringify(record) + "\n");
  return record;
}

export function readLog(count = 20, agentFilter = null) {
  ensureDir();
  if (!fs.existsSync(LOG_FILE)) return [];
  const lines = fs.readFileSync(LOG_FILE, "utf-8").trim().split("\n");
  let entries = [];
  for (const line of lines) {
    try { entries.push(JSON.parse(line)); } catch {}
  }
  if (agentFilter) entries = entries.filter(e => e.agent === agentFilter);
  return entries.slice(-count);
}

export function getPerformanceSummary() {
  const entries = readLog(500);
  const stats = {};
  for (const e of entries) {
    if (!e.agent || !e.action || !e.durationMs) continue;
    const key = `${e.agent}:${e.action}`;
    if (!stats[key]) stats[key] = { count: 0, totalMs: 0, successes: 0, failures: 0 };
    stats[key].count++;
    stats[key].totalMs += e.durationMs;
    if (e.success) stats[key].successes++;
    else stats[key].failures++;
  }
  for (const key of Object.keys(stats)) {
    stats[key].avgMs = Math.round(stats[key].totalMs / stats[key].count);
    stats[key].successRate = Math.round((stats[key].successes / stats[key].count) * 100);
  }
  return stats;
}
