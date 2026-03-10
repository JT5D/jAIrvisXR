/**
 * Context Store — structured store for shared context from voice, CLI, API.
 * Auto-evicts old items. Formats context for LLM prompt injection.
 */
import fs from "node:fs";
import { CONFIG } from "./config.mjs";

const STORE_FILE = CONFIG.tmpDir + "/context-store.json";
let store = new Map();

export function contextInit() {
  try {
    if (fs.existsSync(STORE_FILE)) {
      const data = JSON.parse(fs.readFileSync(STORE_FILE, "utf-8"));
      store = new Map(Object.entries(data));
    }
  } catch {}
  evictOld();
}

function persist() {
  const obj = Object.fromEntries(store);
  const tmp = STORE_FILE + ".tmp";
  fs.writeFileSync(tmp, JSON.stringify(obj, null, 2));
  fs.renameSync(tmp, STORE_FILE);
}

function evictOld() {
  const now = Date.now();
  for (const [id, item] of store) {
    if (now - new Date(item.timestamp).getTime() > CONFIG.contextMaxAge) {
      store.delete(id);
    }
  }
  // Cap at max items
  while (store.size > CONFIG.contextMaxItems) {
    const oldest = store.keys().next().value;
    store.delete(oldest);
  }
}

export function contextAdd(item) {
  const id = `ctx-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`;
  const entry = {
    id,
    type: item.type || "text",
    content: item.content,
    source: item.source || "api",
    timestamp: new Date().toISOString(),
    meta: item.meta || {},
  };
  store.set(id, entry);
  evictOld();
  persist();
  return { id, timestamp: entry.timestamp };
}

export function contextGet(id) {
  return store.get(id) || null;
}

export function contextGetAll() {
  return [...store.values()];
}

export function contextGetRecent(n = 5) {
  return [...store.values()].slice(-n);
}

export function contextRemove(id) {
  store.delete(id);
  persist();
}

export function contextClear() {
  store.clear();
  persist();
}

/**
 * Format context for LLM system prompt injection.
 */
export function contextToPrompt() {
  const items = contextGetRecent(10);
  if (items.length === 0) return "";

  let prompt = "\n\nCurrent shared context:\n";
  items.forEach((item, i) => {
    const age = Math.floor((Date.now() - new Date(item.timestamp).getTime()) / 60000);
    const ageStr = age < 1 ? "just now" : `${age}m ago`;
    const preview = (item.content || "").slice(0, 120);

    switch (item.type) {
      case "url":
        prompt += `  [${i + 1}] URL: ${preview} (via ${item.source}, ${ageStr})\n`;
        break;
      case "file":
        prompt += `  [${i + 1}] File: ${item.meta?.filename || "unknown"} (via ${item.source}, ${ageStr})\n`;
        break;
      case "code":
        prompt += `  [${i + 1}] Code (${item.meta?.language || "?"}): ${preview}... (via ${item.source}, ${ageStr})\n`;
        break;
      default:
        prompt += `  [${i + 1}] ${item.type}: "${preview}" (via ${item.source}, ${ageStr})\n`;
    }
  });
  return prompt;
}
