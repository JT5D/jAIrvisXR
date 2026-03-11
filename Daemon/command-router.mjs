/**
 * Command Router — intent parsing + dispatch for voice, CLI, and HTTP commands.
 * Routes to tool executor or LLM for natural language understanding.
 * Includes local fast-path for common scene commands (<1ms, $0).
 */
import { executeTool, TOOL_SCHEMAS } from "./tool-executor.mjs";
import { grantPermission, revokePermission, listPermissions } from "./safety-guard.mjs";
import { contextAdd, contextGetAll, contextClear, contextToPrompt } from "./context-store.mjs";
import { memoryWrite } from "./shared-memory.mjs";

// ─── Local Fast-Path: Scene Command Parser ───────────────────────────

const COLOR_MAP = {
  red: "#FF0000", blue: "#0000FF", green: "#00FF00", yellow: "#FFFF00",
  orange: "#FFA500", purple: "#800080", pink: "#FFC0CB", white: "#FFFFFF",
  black: "#000000", cyan: "#00FFFF", magenta: "#FF00FF", gray: "#808080",
  grey: "#808080", gold: "#FFD700", silver: "#C0C0C0",
};
const COLORS = Object.keys(COLOR_MAP);

const SHAPES = ["cube", "sphere", "cylinder", "plane", "capsule", "cone", "torus", "pyramid", "quad"];

/**
 * Try to parse a command as a scene action locally (<1ms).
 * Returns SemanticAction[] or null if not a recognized pattern.
 */
function tryFastPath(cmd) {
  const lower = cmd.toLowerCase().trim();

  // Clear scene
  if (/^(clear|reset|empty)\s+(the\s+)?scene$/i.test(lower)) {
    return [{ type: "CLEAR_SCENE", params: {}, confidence: 1.0 }];
  }

  // Undo / Redo
  if (lower === "undo") return [{ type: "UNDO", params: {}, confidence: 1.0 }];
  if (lower === "redo") return [{ type: "REDO", params: {}, confidence: 1.0 }];

  // Add/create object: "add a red cube", "create 3 blue spheres"
  const addMatch = lower.match(/^(?:add|create|make|spawn|place)\s+(?:a\s+|an\s+|(\d+)\s+)?(?:([\w]+)\s+)?(cube|sphere|cylinder|plane|capsule|cone|torus|pyramid|quad)s?$/);
  if (addMatch) {
    const count = addMatch[1] ? parseInt(addMatch[1]) : 1;
    const colorWord = addMatch[2] && COLORS.includes(addMatch[2]) ? addMatch[2] : null;
    const shape = addMatch[3];
    const params = { shape, count };
    if (colorWord) params.color = COLOR_MAP[colorWord];
    return [{ type: "ADD_OBJECT", params, confidence: 0.95 }];
  }

  // Remove/delete object: "remove the cube", "delete sphere"
  const removeMatch = lower.match(/^(?:remove|delete|destroy|kill)\s+(?:the\s+|all\s+)?(?:([\w]+)\s+)?(cube|sphere|cylinder|plane|capsule|cone|torus|pyramid|quad)s?$/);
  if (removeMatch) {
    const shape = removeMatch[2] || removeMatch[1];
    if (SHAPES.includes(shape)) {
      return [{ type: "REMOVE_OBJECT", params: { shape }, confidence: 0.9 }];
    }
  }

  // Change color: "make the cube red", "change color to blue"
  const colorMatch = lower.match(/^(?:make|change|set|color)\s+(?:the\s+)?(cube|sphere|cylinder|plane|capsule|cone|torus|pyramid|quad)?\s*(?:to\s+|color\s+(?:to\s+)?)?(\w+)$/);
  if (colorMatch && COLORS.includes(colorMatch[2])) {
    const params = { color: COLOR_MAP[colorMatch[2]] };
    if (colorMatch[1]) params.target = colorMatch[1];
    return [{ type: "MODIFY_OBJECTS", params, confidence: 0.85 }];
  }

  return null;
}

/**
 * Route a command from any source. Returns response text.
 * @param {string} command - Raw command text
 * @param {string} source - "voice" | "cli" | "api"
 * @param {Function} getLlmResponse - async fn(text) => string, for natural language fallback
 */
export async function routeCommand(command, source, getLlmResponse) {
  const routeStart = Date.now();
  const cmd = command.trim();
  const lower = cmd.toLowerCase();

  // CLI/API specific commands
  if (lower === "shutdown" || lower === "stop") {
    return { action: "shutdown", response: "Jarvis shutting down." };
  }

  if (lower === "restart") {
    return { action: "restart", response: "Jarvis restarting." };
  }

  if (lower.startsWith("grant-permission:")) {
    const perm = lower.split(":")[1];
    grantPermission(perm);
    return { action: "permission", response: `Permission granted: ${perm}` };
  }

  if (lower.startsWith("revoke-permission:")) {
    const perm = lower.split(":")[1];
    revokePermission(perm);
    return { action: "permission", response: `Permission revoked: ${perm}` };
  }

  // Direct tool invocations (voice shortcuts)
  if (lower.startsWith("open ") && (lower.includes("http") || lower.includes("www") || lower.includes(".com"))) {
    const url = cmd.split(" ").slice(1).join(" ").trim();
    const result = executeTool("open_browser", { url });
    return { action: "tool", response: result };
  }

  if (lower === "status") {
    const perms = listPermissions();
    return {
      action: "status",
      response: `Online. Permissions: ${perms.length ? perms.join(", ") : "none"}`,
    };
  }

  if (lower.startsWith("share ")) {
    const content = cmd.slice(6).trim();
    const type = content.startsWith("http") ? "url" : "text";
    contextAdd({ type, content, source });
    return { action: "share", response: `Shared ${type}: ${content.slice(0, 60)}` };
  }

  // Fast-path: scene commands that don't need LLM (<1ms, $0)
  const fastActions = tryFastPath(cmd);
  if (fastActions) {
    const routeMs = Date.now() - routeStart;
    const response = JSON.stringify({ actions: fastActions });
    return { action: "scene", response, timing: { routeMs, llmMs: 0, toolMs: 0, totalMs: routeMs }, provider: "local" };
  }

  // Fall through to LLM for natural language
  if (getLlmResponse) {
    const result = await getLlmResponse(cmd);
    // getResponse now returns { text, timing, provider, toolRounds }
    if (result && typeof result === "object" && result.text !== undefined) {
      const timing = { ...result.timing, routeMs: Date.now() - routeStart };
      timing.totalMs = Date.now() - routeStart;
      return { action: "conversation", response: result.text, timing, provider: result.provider };
    }
    // Backward compat: if result is a plain string
    return { action: "conversation", response: result };
  }

  return { action: "unknown", response: "I'm not sure what to do with that." };
}
