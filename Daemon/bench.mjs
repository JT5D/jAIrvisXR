#!/usr/bin/env node
/**
 * Jarvis Benchmark Suite — 20 graded prompts across categories.
 * Measures latency, cost, and provider performance.
 *
 * Usage: node bench.mjs [--port 7437] [--runs 1]
 */

const PORT = process.argv.includes("--port")
  ? parseInt(process.argv[process.argv.indexOf("--port") + 1])
  : 7437;
const RUNS = process.argv.includes("--runs")
  ? parseInt(process.argv[process.argv.indexOf("--runs") + 1])
  : 1;
const BASE = `http://127.0.0.1:${PORT}`;

const PROMPTS = [
  // Simple math (expect <500ms)
  { category: "math", prompt: "What is 2 + 2?", maxMs: 500 },
  { category: "math", prompt: "What is 15 times 7?", maxMs: 500 },
  { category: "math", prompt: "What is the square root of 144?", maxMs: 500 },

  // Knowledge (expect <800ms)
  { category: "knowledge", prompt: "Who wrote Hamlet?", maxMs: 800 },
  { category: "knowledge", prompt: "What is the capital of France?", maxMs: 800 },
  { category: "knowledge", prompt: "What year did the Moon landing happen?", maxMs: 800 },

  // Scene commands — fast-path (expect <50ms)
  { category: "scene-fast", prompt: "add a red cube", maxMs: 50 },
  { category: "scene-fast", prompt: "create a blue sphere", maxMs: 50 },
  { category: "scene-fast", prompt: "clear scene", maxMs: 50 },
  { category: "scene-fast", prompt: "remove the cube", maxMs: 50 },
  { category: "scene-fast", prompt: "undo", maxMs: 50 },

  // Complex scene (LLM needed, expect <2000ms)
  { category: "scene-llm", prompt: "create a formation of 5 spheres arranged in a circle", maxMs: 2000 },
  { category: "scene-llm", prompt: "build a house with walls, a roof, and a door", maxMs: 2000 },

  // Tool use (expect <3000ms)
  { category: "tool", prompt: "what files are in /tmp?", maxMs: 3000 },
  { category: "tool", prompt: "what is the current date and time?", maxMs: 3000 },

  // Conversation (expect <1000ms)
  { category: "conversation", prompt: "Hello, how are you?", maxMs: 1000 },
  { category: "conversation", prompt: "Tell me a one-sentence joke.", maxMs: 1000 },
  { category: "conversation", prompt: "What can you help me with?", maxMs: 1000 },

  // Short responses (expect <600ms)
  { category: "short", prompt: "Say hello in Japanese.", maxMs: 600 },
  { category: "short", prompt: "What color is the sky?", maxMs: 600 },
];

async function runBenchmark(prompt) {
  const start = Date.now();
  try {
    const res = await fetch(`${BASE}/api/command`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ command: prompt }),
    });
    const elapsed = Date.now() - start;
    if (!res.ok) return { elapsed, error: `HTTP ${res.status}`, provider: null, timing: null };
    const data = await res.json();
    return {
      elapsed,
      response: (data.response || data.result || "").slice(0, 80),
      provider: data.provider || "unknown",
      timing: data.timing || null,
      error: null,
    };
  } catch (err) {
    return { elapsed: Date.now() - start, error: err.message, provider: null, timing: null };
  }
}

async function main() {
  // Check daemon is running
  try {
    const health = await fetch(`${BASE}/api/health`);
    if (!health.ok) throw new Error(`Health check failed: ${health.status}`);
  } catch {
    console.error(`\x1b[31mCannot connect to Jarvis at ${BASE}. Is the daemon running?\x1b[0m`);
    process.exit(1);
  }

  console.log(`\n\x1b[36m  Jarvis Benchmark Suite\x1b[0m`);
  console.log(`  Endpoint: ${BASE}`);
  console.log(`  Prompts:  ${PROMPTS.length}`);
  console.log(`  Runs:     ${RUNS}\n`);

  const allResults = [];

  for (let run = 0; run < RUNS; run++) {
    if (RUNS > 1) console.log(`\x1b[33m  ── Run ${run + 1}/${RUNS} ──\x1b[0m`);

    for (const p of PROMPTS) {
      const result = await runBenchmark(p.prompt);
      const passed = !result.error && result.elapsed <= p.maxMs;
      const icon = result.error ? "\x1b[31m✗\x1b[0m" : passed ? "\x1b[32m✓\x1b[0m" : "\x1b[33m⚠\x1b[0m";
      const ms = `${result.elapsed}ms`.padStart(7);
      const budget = `≤${p.maxMs}ms`.padStart(8);
      const provider = (result.provider || "err").padEnd(12);

      console.log(`  ${icon} ${ms} ${budget} [${provider}] ${p.category.padEnd(12)} "${p.prompt.slice(0, 50)}"`);
      if (result.error) console.log(`    \x1b[31m${result.error}\x1b[0m`);

      allResults.push({ ...p, ...result, passed });
    }
  }

  // ─── Scorecard ───
  const latencies = allResults.filter(r => !r.error).map(r => r.elapsed).sort((a, b) => a - b);
  const p50 = latencies[Math.floor(latencies.length * 0.5)] || 0;
  const p95 = latencies[Math.floor(latencies.length * 0.95)] || 0;
  const avg = latencies.length ? Math.round(latencies.reduce((a, b) => a + b, 0) / latencies.length) : 0;
  const passCount = allResults.filter(r => r.passed).length;
  const failCount = allResults.filter(r => r.error).length;
  const slowCount = allResults.filter(r => !r.error && !r.passed).length;

  // Category breakdown
  const categories = [...new Set(PROMPTS.map(p => p.category))];
  const catStats = {};
  for (const cat of categories) {
    const catResults = allResults.filter(r => r.category === cat && !r.error);
    const catLatencies = catResults.map(r => r.elapsed).sort((a, b) => a - b);
    catStats[cat] = {
      avg: catLatencies.length ? Math.round(catLatencies.reduce((a, b) => a + b, 0) / catLatencies.length) : 0,
      p50: catLatencies[Math.floor(catLatencies.length * 0.5)] || 0,
      pass: catResults.filter(r => r.passed).length,
      total: allResults.filter(r => r.category === cat).length,
    };
  }

  // Provider breakdown
  const providers = [...new Set(allResults.filter(r => r.provider).map(r => r.provider))];

  console.log(`\n\x1b[36m  ── Scorecard ──\x1b[0m`);
  console.log(`  Total:    ${allResults.length} prompts`);
  console.log(`  Passed:   \x1b[32m${passCount}\x1b[0m  Slow: \x1b[33m${slowCount}\x1b[0m  Failed: \x1b[31m${failCount}\x1b[0m`);
  console.log(`  Latency:  avg=${avg}ms  p50=${p50}ms  p95=${p95}ms`);
  console.log(`  Provider: ${providers.join(", ") || "none"}`);

  console.log(`\n\x1b[36m  ── By Category ──\x1b[0m`);
  for (const cat of categories) {
    const s = catStats[cat];
    console.log(`  ${cat.padEnd(14)} avg=${String(s.avg).padStart(5)}ms  p50=${String(s.p50).padStart(5)}ms  ${s.pass}/${s.total} pass`);
  }

  console.log();
  process.exit(failCount > 0 ? 1 : 0);
}

main();
