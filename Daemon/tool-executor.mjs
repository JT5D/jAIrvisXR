/**
 * Tool Executor — gives the daemon hands.
 * Ported from v2 jarvis-tools.mjs with safety integration.
 */
import { execSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { memoryRead, memoryWrite, memoryKeys } from "./shared-memory.mjs";
import { logActivity, readLog, getPerformanceSummary } from "./activity-log.mjs";
import { checkCommandSafety } from "./safety-guard.mjs";
import { CONFIG } from "./config.mjs";

export const TOOL_SCHEMAS = [
  {
    name: "open_browser",
    description: "Open a URL in the user's default browser.",
    input_schema: {
      type: "object",
      properties: { url: { type: "string", description: "URL to open" } },
      required: ["url"],
    },
  },
  {
    name: "run_shell",
    description: "Execute a shell command on macOS and return output. Do NOT use for destructive operations.",
    input_schema: {
      type: "object",
      properties: { command: { type: "string", description: "Shell command" } },
      required: ["command"],
    },
  },
  {
    name: "read_file",
    description: "Read file contents from disk.",
    input_schema: {
      type: "object",
      properties: {
        path: { type: "string", description: "Absolute path" },
        max_lines: { type: "number", description: "Max lines (default: 100)" },
      },
      required: ["path"],
    },
  },
  {
    name: "write_file",
    description: "Write content to a file. Creates parent dirs if needed.",
    input_schema: {
      type: "object",
      properties: {
        path: { type: "string", description: "Absolute path" },
        content: { type: "string", description: "Content to write" },
      },
      required: ["path", "content"],
    },
  },
  {
    name: "list_directory",
    description: "List files in a directory.",
    input_schema: {
      type: "object",
      properties: { path: { type: "string", description: "Absolute path" } },
      required: ["path"],
    },
  },
  {
    name: "read_memory",
    description: "Read from shared agent memory. Use key='all' to see everything.",
    input_schema: {
      type: "object",
      properties: { key: { type: "string", description: "Memory key or 'all'" } },
      required: ["key"],
    },
  },
  {
    name: "write_memory",
    description: "Write to shared agent memory (shared with Claude Code).",
    input_schema: {
      type: "object",
      properties: {
        key: { type: "string", description: "Memory key" },
        value: { type: "string", description: "Value (JSON string for objects)" },
      },
      required: ["key", "value"],
    },
  },
  {
    name: "read_activity_log",
    description: "Read recent activity log entries.",
    input_schema: {
      type: "object",
      properties: {
        count: { type: "number", description: "Number of entries (default: 10)" },
        agent: { type: "string", description: "Filter by agent name" },
      },
    },
  },
  {
    name: "search_project",
    description: "Search for text across project files using grep.",
    input_schema: {
      type: "object",
      properties: {
        query: { type: "string", description: "Search text or regex" },
        project: { type: "string", description: "Project name or path" },
      },
      required: ["query"],
    },
  },
  {
    name: "record_lesson",
    description: "Record a learning for agent memory (bug-fix, pattern, optimization).",
    input_schema: {
      type: "object",
      properties: {
        category: { type: "string", description: "bug-fix|pattern|optimization|tool-usage|architecture" },
        lesson: { type: "string", description: "What was learned" },
      },
      required: ["category", "lesson"],
    },
  },
];

export function executeTool(name, input) {
  const start = Date.now();
  let result;
  let success = true;
  let error = null;

  try {
    switch (name) {
      case "open_browser":
        execSync(`open "${input.url}"`, { stdio: "pipe" });
        result = `Opened: ${input.url}`;
        break;

      case "run_shell": {
        const safety = checkCommandSafety(input.command);
        if (!safety.safe) {
          result = `BLOCKED: ${safety.reason}`;
          success = false;
          break;
        }
        try {
          result = execSync(input.command, {
            encoding: "utf-8",
            timeout: 15000,
            stdio: "pipe",
            cwd: process.env.HOME,
          }).slice(0, 2000);
        } catch (e) {
          result = `Error: ${e.message?.slice(0, 500)}`;
          success = false;
        }
        break;
      }

      case "read_file": {
        const filePath = input.path;
        if (!fs.existsSync(filePath)) {
          result = `File not found: ${filePath}`;
          success = false;
          break;
        }
        const content = fs.readFileSync(filePath, "utf-8");
        const lines = content.split("\n");
        const maxLines = input.max_lines || 100;
        result = lines.slice(0, maxLines).join("\n");
        if (lines.length > maxLines) result += `\n... (${lines.length - maxLines} more lines)`;
        break;
      }

      case "write_file": {
        const dir = path.dirname(input.path);
        if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
        fs.writeFileSync(input.path, input.content);
        result = `Written: ${input.path} (${input.content.length} chars)`;
        break;
      }

      case "list_directory": {
        const dirPath = input.path || process.env.HOME;
        if (!fs.existsSync(dirPath)) {
          result = `Directory not found: ${dirPath}`;
          success = false;
          break;
        }
        const items = fs.readdirSync(dirPath, { withFileTypes: true });
        result = items.map(i => `${i.isDirectory() ? "d" : "f"} ${i.name}`).join("\n");
        break;
      }

      case "read_memory": {
        const val = memoryRead(input.key);
        result = JSON.stringify(val, null, 2)?.slice(0, 2000) || "null";
        break;
      }

      case "write_memory": {
        let val = input.value;
        try { val = JSON.parse(val); } catch {}
        memoryWrite(input.key, val);
        result = `Written: ${input.key}`;
        break;
      }

      case "read_activity_log": {
        const entries = readLog(input.count || 10, input.agent || null);
        result = entries.map(e =>
          `[${e.ts}] ${e.agent}: ${e.action}${e.meta?.userText ? ` "${e.meta.userText.slice(0, 60)}"` : ""}`
        ).join("\n");
        break;
      }

      case "search_project": {
        const searchPath = input.project
          ? (CONFIG?.projects?.[input.project] || input.project)
          : process.env.HOME;
        try {
          result = execSync(
            `grep -rn --include="*.{js,mjs,ts,tsx,cs,json,md,py,sh}" "${input.query}" "${searchPath}" | head -30`,
            { encoding: "utf-8", timeout: 10000, stdio: "pipe" }
          ).slice(0, 2000);
        } catch (e) {
          result = e.stdout?.slice(0, 500) || "No matches found";
        }
        break;
      }

      case "record_lesson": {
        const lessons = memoryRead("agent-lessons") || [];
        lessons.push({
          category: input.category,
          lesson: input.lesson,
          confidence: 0.8,
          recordedAt: new Date().toISOString(),
          recordedBy: "jarvis-daemon",
        });
        // Keep last 50 lessons
        while (lessons.length > 50) lessons.shift();
        memoryWrite("agent-lessons", lessons);
        result = `Lesson recorded: [${input.category}] ${input.lesson.slice(0, 60)}`;
        break;
      }

      default:
        result = `Unknown tool: ${name}`;
        success = false;
    }
  } catch (e) {
    result = `Tool error: ${e.message?.slice(0, 200)}`;
    success = false;
    error = e.message?.slice(0, 100);
  }

  logActivity({
    agent: "jarvis-daemon",
    action: name,
    tool: name,
    durationMs: Date.now() - start,
    success,
    error,
    meta: { input: JSON.stringify(input).slice(0, 200) },
  });

  return result;
}

