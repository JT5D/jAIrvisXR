#!/usr/bin/env node
/**
 * Standalone test client for Jarvis Daemon API.
 * Tests all endpoints without needing React Native.
 *
 * Usage: node test-client.mjs [--port 7437]
 */

const PORT = process.argv.includes("--port")
  ? parseInt(process.argv[process.argv.indexOf("--port") + 1])
  : 7437;
const BASE = `http://127.0.0.1:${PORT}`;

const PASS = "\x1b[32m✓\x1b[0m";
const FAIL = "\x1b[31m✗\x1b[0m";
let passed = 0;
let failed = 0;

async function test(name, fn) {
  try {
    await fn();
    console.log(`  ${PASS} ${name}`);
    passed++;
  } catch (err) {
    console.log(`  ${FAIL} ${name} — ${err.message}`);
    failed++;
  }
}

function assert(condition, msg) {
  if (!condition) throw new Error(msg || "Assertion failed");
}

console.log(`\n\x1b[36m  Jarvis API Test Suite\x1b[0m`);
console.log(`  Endpoint: ${BASE}\n`);

// ─── Health ─────────────────────────────────────────────────────────

await test("GET /api/health returns ok", async () => {
  const res = await fetch(`${BASE}/api/health`);
  assert(res.ok, `Status ${res.status}`);
  const data = await res.json();
  assert(data.status === "ok", `Expected ok, got ${data.status}`);
  assert(typeof data.uptime === "number", "Missing uptime");
  assert(data.version, "Missing version");
});

// ─── Status ─────────────────────────────────────────────────────────

await test("GET /api/status returns shared memory", async () => {
  const res = await fetch(`${BASE}/api/status`);
  assert(res.ok, `Status ${res.status}`);
  const data = await res.json();
  assert(typeof data === "object", "Expected object");
});

// ─── Command ────────────────────────────────────────────────────────

await test("POST /api/command processes text", async () => {
  const res = await fetch(`${BASE}/api/command`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ command: "What is 2 + 2?" }),
  });
  assert(res.ok, `Status ${res.status}`);
  const data = await res.json();
  assert(data.response || data.result, "No response in result");
});

// ─── Context Share ──────────────────────────────────────────────────

await test("POST /api/share accepts context", async () => {
  const res = await fetch(`${BASE}/api/share`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      type: "text",
      content: "Test context from API test suite",
      meta: { source: "test-client" },
    }),
  });
  assert(res.ok, `Status ${res.status}`);
  const data = await res.json();
  assert(data.status === "shared", `Expected shared, got ${data.status}`);
});

// ─── Context Get ────────────────────────────────────────────────────

await test("GET /api/context returns items", async () => {
  const res = await fetch(`${BASE}/api/context`);
  assert(res.ok, `Status ${res.status}`);
  const data = await res.json();
  assert(Array.isArray(data), "Expected array");
});

// ─── Logs ───────────────────────────────────────────────────────────

await test("GET /api/logs returns entries", async () => {
  const res = await fetch(`${BASE}/api/logs?n=5`);
  assert(res.ok, `Status ${res.status}`);
  const data = await res.json();
  assert(Array.isArray(data), "Expected array");
});

// ─── Performance ────────────────────────────────────────────────────

await test("GET /api/performance returns stats", async () => {
  const res = await fetch(`${BASE}/api/performance`);
  assert(res.ok, `Status ${res.status}`);
});

// ─── Context Cleanup ────────────────────────────────────────────────

await test("DELETE /api/context clears all", async () => {
  const res = await fetch(`${BASE}/api/context`, { method: "DELETE" });
  assert(res.ok, `Status ${res.status}`);
  const data = await res.json();
  assert(data.status === "cleared", `Expected cleared, got ${data.status}`);
});

// ─── 404 ────────────────────────────────────────────────────────────

await test("GET /api/nonexistent returns 404", async () => {
  const res = await fetch(`${BASE}/api/nonexistent`);
  assert(res.status === 404, `Expected 404, got ${res.status}`);
});

// ─── Summary ────────────────────────────────────────────────────────

console.log(`\n  ${passed} passed, ${failed} failed\n`);
process.exit(failed > 0 ? 1 : 0);
