#!/usr/bin/env node
/**
 * Jarvis Voice Command Test Harness
 *
 * 20 graded prompts of increasing difficulty — from simple scene commands
 * to complex multi-step spatial intelligence tasks.
 *
 * Usage:
 *   node test-harness.mjs                    # Run all 20 prompts
 *   node test-harness.mjs --single 3         # Run prompt #3 only
 *   node test-harness.mjs --range 1-5        # Run prompts 1-5
 *   node test-harness.mjs --port 7437        # Custom daemon port
 *   node test-harness.mjs --speak            # Daemon speaks responses via TTS
 *   node test-harness.mjs --json             # Output results as JSON
 */

const args = process.argv.slice(2);
const PORT = args.includes("--port")
  ? parseInt(args[args.indexOf("--port") + 1])
  : 7437;
const SPEAK = args.includes("--speak");
const JSON_OUT = args.includes("--json");
const SINGLE = args.includes("--single")
  ? parseInt(args[args.indexOf("--single") + 1])
  : null;
const RANGE = args.includes("--range")
  ? args[args.indexOf("--range") + 1].split("-").map(Number)
  : null;

const BASE = `http://127.0.0.1:${PORT}`;

// ─── 20 Graded Voice Prompts ────────────────────────────────────────

const PROMPTS = [
  // Level 1: Basic scene commands (1-4)
  {
    id: 1,
    level: "Basic",
    prompt: "Add a red sphere to the scene",
    expect: "Should acknowledge adding a sphere with red color",
  },
  {
    id: 2,
    level: "Basic",
    prompt: "Put a blue cube on the table",
    expect: "Should create a cube with blue material, positioned on a surface",
  },
  {
    id: 3,
    level: "Basic",
    prompt: "Place three red spheres next to each other between the windows",
    expect: "Should create multiple objects with spatial positioning",
  },
  {
    id: 4,
    level: "Basic",
    prompt: "Add a dinosaur. Make it young.",
    expect: "Should create a dinosaur object, potentially scaled smaller",
  },

  // Level 2: Effects & animation (5-8)
  {
    id: 5,
    level: "Effects",
    prompt: "Create fireworks",
    expect: "Should trigger a particle emitter or VFX with fireworks preset",
  },
  {
    id: 6,
    level: "Effects",
    prompt: "Put me on a boat in the middle of the ocean",
    expect: "Should compose a scene with water environment and boat object",
  },
  {
    id: 7,
    level: "Effects",
    prompt: "Make everything glow and pulse to the music",
    expect: "Should add audio-reactive wire bindings to scene objects",
  },
  {
    id: 8,
    level: "Effects",
    prompt: "Create a sunset with warm lighting and long shadows",
    expect: "Should adjust lighting parameters for golden hour atmosphere",
  },

  // Level 3: Data & visualization (9-12)
  {
    id: 9,
    level: "Data",
    prompt: "Provide me with a dynamic 3D data visualization of top-performing stocks",
    expect: "Should create a data visualization scene with real-time stock data",
  },
  {
    id: 10,
    level: "Data",
    prompt: "Show me the architecture of this codebase as a 3D knowledge graph",
    expect: "Should map code structure to spatial nodes and edges",
  },
  {
    id: 11,
    level: "Data",
    prompt: "Create a time-lapse story of human evolution for me and my children",
    expect: "Should compose a sequential educational scene with timeline progression",
  },
  {
    id: 12,
    level: "Data",
    prompt: "Visualize the solar system with accurate relative sizes and orbits",
    expect: "Should create planetary objects with correct proportions and animations",
  },

  // Level 4: Collaboration & telepresence (13-16)
  {
    id: 13,
    level: "Collab",
    prompt: "Create a holographic telepresence party for me and my coworkers",
    expect: "Should set up a multiplayer room with avatar/hologram support",
  },
  {
    id: 14,
    level: "Collab",
    prompt: "Set up a shared whiteboard that everyone in the room can draw on",
    expect: "Should create a collaborative surface with multi-user input",
  },
  {
    id: 15,
    level: "Collab",
    prompt: "Create a virtual classroom where students can see and interact with 3D models",
    expect: "Should compose an educational space with interactive object manipulation",
  },
  {
    id: 16,
    level: "Collab",
    prompt: "Build a spatial war room where we can plan our product launch together",
    expect: "Should create a structured planning space with data displays and zones",
  },

  // Level 5: Meta / agent-level commands (17-20)
  {
    id: 17,
    level: "Agent",
    prompt: "Create an XRAI Jarvis and JT and Ryan team lead graduate-level series of classes and talks where people can join physically in VR/AR participating spatially with app data visualization and AI content creation in real time",
    expect: "Should outline a multi-session spatial education program with mixed-reality participation",
  },
  {
    id: 18,
    level: "Agent",
    prompt: "Take me home. Give me my own tools and my own free Jarvis spatial intelligence agent swarm.",
    expect: "Should compose a personal workspace environment with agent orchestration capabilities",
  },
  {
    id: 19,
    level: "Agent",
    prompt: "Research the top 5 AR frameworks, compare their performance, and create an interactive 3D comparison chart I can walk through",
    expect: "Should perform research, synthesize data, and create a spatial visualization",
  },
  {
    id: 20,
    level: "Agent",
    prompt: "Design and build me a complete spatial computing app prototype. It should have voice control, hand tracking, and AI-powered content generation. Show me the architecture as I walk through it.",
    expect: "Should decompose a complex multi-step task into actionable sub-tasks and begin execution",
  },
];

// ─── Test Runner ────────────────────────────────────────────────────

async function sendCommand(command) {
  const res = await fetch(`${BASE}/api/command`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ command }),
  });
  if (!res.ok) throw new Error(`Command failed: ${res.status}`);
  return res.json();
}

async function runPrompt(prompt) {
  const start = Date.now();
  try {
    const result = await sendCommand(prompt.prompt);
    const elapsed = Date.now() - start;
    const response = result.response || result.result || JSON.stringify(result);

    return {
      id: prompt.id,
      level: prompt.level,
      prompt: prompt.prompt,
      response: response.slice(0, 500),
      elapsed,
      success: true,
      hasContent: response.length > 10,
    };
  } catch (err) {
    return {
      id: prompt.id,
      level: prompt.level,
      prompt: prompt.prompt,
      response: err.message,
      elapsed: Date.now() - start,
      success: false,
      hasContent: false,
    };
  }
}

// ─── Main ───────────────────────────────────────────────────────────

async function main() {
  // Check daemon is running
  try {
    const health = await fetch(`${BASE}/api/health`);
    if (!health.ok) throw new Error("not ok");
  } catch {
    console.error("Jarvis daemon not running. Start with: jarvis start");
    process.exit(1);
  }

  // Select prompts
  let selected = PROMPTS;
  if (SINGLE !== null) {
    selected = PROMPTS.filter(p => p.id === SINGLE);
    if (selected.length === 0) {
      console.error(`Prompt #${SINGLE} not found (range: 1-20)`);
      process.exit(1);
    }
  } else if (RANGE) {
    selected = PROMPTS.filter(p => p.id >= RANGE[0] && p.id <= RANGE[1]);
  }

  console.log(`\n\x1b[36m  Jarvis Voice Command Test Harness\x1b[0m`);
  console.log(`  Running ${selected.length} prompt(s) against ${BASE}\n`);

  const results = [];

  for (const prompt of selected) {
    const levelColors = {
      Basic: "\x1b[32m",
      Effects: "\x1b[33m",
      Data: "\x1b[34m",
      Collab: "\x1b[35m",
      Agent: "\x1b[36m",
    };
    const color = levelColors[prompt.level] || "\x1b[0m";

    process.stdout.write(`  ${color}[${prompt.level}]\x1b[0m #${prompt.id}: ${prompt.prompt.slice(0, 60)}... `);

    const result = await runPrompt(prompt);
    results.push(result);

    if (result.success) {
      console.log(`\x1b[32m✓\x1b[0m ${result.elapsed}ms`);
    } else {
      console.log(`\x1b[31m✗\x1b[0m ${result.elapsed}ms — ${result.response.slice(0, 50)}`);
    }

    if (!JSON_OUT && result.success) {
      // Print first 2 lines of response
      const lines = result.response.split("\n").slice(0, 2);
      for (const line of lines) {
        console.log(`    \x1b[90m${line.slice(0, 100)}\x1b[0m`);
      }
    }

    // Brief pause between prompts to avoid rate limiting
    if (selected.length > 1) {
      await new Promise(r => setTimeout(r, 1500));
    }
  }

  // Summary
  const passed = results.filter(r => r.success).length;
  const totalTime = results.reduce((sum, r) => sum + r.elapsed, 0);
  const avgTime = Math.round(totalTime / results.length);

  console.log(`\n  ─────────────────────────────────────`);
  console.log(`  ${passed}/${results.length} passed | avg ${avgTime}ms | total ${Math.round(totalTime / 1000)}s`);

  // Level breakdown
  const levels = ["Basic", "Effects", "Data", "Collab", "Agent"];
  for (const level of levels) {
    const levelResults = results.filter(r => r.level === level);
    if (levelResults.length === 0) continue;
    const levelPassed = levelResults.filter(r => r.success).length;
    const levelAvg = Math.round(levelResults.reduce((s, r) => s + r.elapsed, 0) / levelResults.length);
    console.log(`  ${level}: ${levelPassed}/${levelResults.length} (avg ${levelAvg}ms)`);
  }
  console.log();

  if (JSON_OUT) {
    console.log(JSON.stringify(results, null, 2));
  }

  process.exit(passed === results.length ? 0 : 1);
}

main().catch(err => {
  console.error(`Fatal: ${err.message}`);
  process.exit(1);
});
