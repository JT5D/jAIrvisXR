/**
 * Shared Memory — persistent key-value store accessible by all agents.
 * Both Jarvis (daemon) and Claude Code can read/write here.
 * Storage: JSON file with atomic rename.
 */
import fs from "node:fs";
import os from "node:os";
import { CONFIG } from "./config.mjs";

const MEM_FILE = CONFIG.memFile;
const MEM_DIR = CONFIG.tmpDir;

function ensureDir() {
  if (!fs.existsSync(MEM_DIR)) fs.mkdirSync(MEM_DIR, { recursive: true });
}

function load() {
  ensureDir();
  if (!fs.existsSync(MEM_FILE)) return {};
  try {
    return JSON.parse(fs.readFileSync(MEM_FILE, "utf-8"));
  } catch {
    return {};
  }
}

function save(data) {
  ensureDir();
  const tmp = MEM_FILE + ".tmp";
  fs.writeFileSync(tmp, JSON.stringify(data, null, 2));
  fs.renameSync(tmp, MEM_FILE);
}

export function memoryRead(key) {
  const data = load();
  if (key === "*" || key === "all") return data;
  return data[key] ?? null;
}

export function memoryWrite(key, value) {
  const data = load();
  data[key] = value;
  data._lastUpdated = new Date().toISOString();
  data._lastUpdatedBy = "jarvis-daemon-v3";
  save(data);
  return true;
}

export function memoryDelete(key) {
  const data = load();
  delete data[key];
  save(data);
  return true;
}

export function memoryKeys() {
  return Object.keys(load()).filter(k => !k.startsWith("_"));
}

export function memoryInit() {
  const data = load();
  if (!data._agents) data._agents = {};
  data._agents["jarvis-daemon"] = {
    type: "voice-agent",
    status: "online",
    startedAt: new Date().toISOString(),
    capabilities: ["voice", "shell", "browser", "files", "memory"],
    pid: process.pid,
  };
  data._systemInfo = {
    hostname: os.hostname(),
    platform: os.platform(),
    arch: os.arch(),
    homeDir: os.homedir(),
    nodeVersion: process.version,
  };
  if (!data.projects) data.projects = CONFIG.projects;
  save(data);
}
