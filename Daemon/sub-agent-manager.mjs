/**
 * Sub-Agent Manager — spawn and manage sub-agents for delegated tasks.
 * Sub-agents are lightweight async tasks with timeout + result reporting.
 */
import { execSync } from "node:child_process";
import { CONFIG } from "./config.mjs";
import { logActivity } from "./activity-log.mjs";
import { memoryRead, memoryWrite } from "./shared-memory.mjs";

const activeAgents = new Map();
let nextId = 1;

export function spawnSubAgent({ task, type = "custom", timeout = CONFIG.subAgentTimeout }) {
  if (activeAgents.size >= CONFIG.maxSubAgents) {
    return { error: `Max sub-agents reached (${CONFIG.maxSubAgents}). Wait for one to finish.` };
  }

  const agentId = `sub-${nextId++}`;
  const agent = {
    id: agentId,
    type,
    task,
    status: "running",
    startedAt: new Date().toISOString(),
    output: null,
    error: null,
  };

  activeAgents.set(agentId, agent);

  // Auto-timeout
  const timer = setTimeout(() => {
    if (agent.status === "running") {
      agent.status = "timeout";
      agent.error = "Task timed out";
      logActivity({ agent: "sub-agent-manager", action: "timeout", meta: { agentId, task: task.slice(0, 60) } });
    }
  }, timeout);

  // Execute based on type
  (async () => {
    try {
      let result;
      switch (type) {
        case "shell":
          result = execSync(task, { encoding: "utf-8", timeout: timeout - 1000, stdio: "pipe" }).slice(0, 5000);
          break;
        case "research":
        case "code-review":
        case "custom":
        default:
          // For now, shell-based execution. LLM sub-agents can be added later.
          result = `Sub-agent ${agentId} completed task: ${task.slice(0, 100)}`;
          break;
      }
      agent.output = result;
      agent.status = "completed";
      agent.completedAt = new Date().toISOString();
    } catch (err) {
      agent.error = err.message?.slice(0, 200);
      agent.status = "failed";
    }
    clearTimeout(timer);
    logActivity({
      agent: "sub-agent-manager",
      action: agent.status,
      success: agent.status === "completed",
      error: agent.error,
      meta: { agentId, type, task: task.slice(0, 60) },
    });
  })();

  return { agentId, status: "running" };
}

export function querySubAgent(agentId) {
  return activeAgents.get(agentId) || { error: "Agent not found" };
}

export function killSubAgent(agentId) {
  const agent = activeAgents.get(agentId);
  if (agent) {
    agent.status = "killed";
    activeAgents.delete(agentId);
    return true;
  }
  return false;
}

export function listActiveAgents() {
  return [...activeAgents.values()].filter(a => a.status === "running");
}

export function cleanupCompleted() {
  for (const [id, agent] of activeAgents) {
    if (agent.status !== "running") activeAgents.delete(id);
  }
}
