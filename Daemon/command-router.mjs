/**
 * Command Router — intent parsing + dispatch for voice, CLI, and HTTP commands.
 * Routes to tool executor or LLM for natural language understanding.
 */
import { executeTool, TOOL_SCHEMAS } from "./tool-executor.mjs";
import { grantPermission, revokePermission, listPermissions } from "./safety-guard.mjs";
import { contextAdd, contextGetAll, contextClear, contextToPrompt } from "./context-store.mjs";
import { memoryWrite } from "./shared-memory.mjs";

/**
 * Route a command from any source. Returns response text.
 * @param {string} command - Raw command text
 * @param {string} source - "voice" | "cli" | "api"
 * @param {Function} getLlmResponse - async fn(text) => string, for natural language fallback
 */
export async function routeCommand(command, source, getLlmResponse) {
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

  // Fall through to LLM for natural language
  if (getLlmResponse) {
    const response = await getLlmResponse(cmd);
    return { action: "conversation", response };
  }

  return { action: "unknown", response: "I'm not sure what to do with that." };
}
