/**
 * HTTP API — local REST server on localhost:7437.
 * Accepts commands, context shares, and status queries.
 * Binds to 127.0.0.1 only.
 */
import { createServer } from "node:http";
import { CONFIG } from "./config.mjs";
import { memoryRead } from "./shared-memory.mjs";
import { readLog, getPerformanceSummary } from "./activity-log.mjs";
import { logActivity } from "./activity-log.mjs";
import { contextAdd, contextGetAll, contextClear, contextRemove } from "./context-store.mjs";
import { routeCommand } from "./command-router.mjs";

let server = null;
let getLlmResponse = null;

function readBody(req) {
  return new Promise((resolve) => {
    let data = "";
    req.on("data", (chunk) => data += chunk);
    req.on("end", () => resolve(data));
  });
}

function json(res, statusCode, body) {
  res.writeHead(statusCode, {
    "Content-Type": "application/json",
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Methods": "GET, POST, DELETE, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type",
  });
  res.end(JSON.stringify(body));
}

export function startHttpApi(llmResponseFn) {
  getLlmResponse = llmResponseFn;

  server = createServer(async (req, res) => {
    // CORS preflight
    if (req.method === "OPTIONS") {
      json(res, 200, {});
      return;
    }

    const url = new URL(req.url, `http://localhost:${CONFIG.httpPort}`);

    try {
      // Health check
      if (url.pathname === "/api/health") {
        json(res, 200, {
          status: "ok",
          version: "3.0",
          uptime: process.uptime(),
        });
        return;
      }

      // Status
      if (url.pathname === "/api/status" && req.method === "GET") {
        const sm = memoryRead("*");
        json(res, 200, sm);
        return;
      }

      // Share context
      if (url.pathname === "/api/share" && req.method === "POST") {
        const body = JSON.parse(await readBody(req));
        const result = contextAdd({
          type: body.type || "text",
          content: body.content,
          source: "api",
          meta: body.meta,
        });
        logActivity({ agent: "http-api", action: "share", success: true, meta: { type: body.type } });
        json(res, 200, { status: "shared", ...result });
        return;
      }

      // Send command
      if (url.pathname === "/api/command" && req.method === "POST") {
        const body = JSON.parse(await readBody(req));
        const result = await routeCommand(body.command, "api", getLlmResponse);
        logActivity({ agent: "http-api", action: "command", success: true, meta: { command: body.command?.slice(0, 60) } });
        json(res, 200, result);
        return;
      }

      // Get context
      if (url.pathname === "/api/context" && req.method === "GET") {
        json(res, 200, contextGetAll());
        return;
      }

      // Clear context
      if (url.pathname === "/api/context" && req.method === "DELETE") {
        contextClear();
        json(res, 200, { status: "cleared" });
        return;
      }

      // Delete specific context item
      if (url.pathname.startsWith("/api/context/") && req.method === "DELETE") {
        const id = url.pathname.split("/").pop();
        contextRemove(id);
        json(res, 200, { status: "removed", id });
        return;
      }

      // Activity logs
      if (url.pathname === "/api/logs" && req.method === "GET") {
        const count = parseInt(url.searchParams.get("n") || "30");
        const agent = url.searchParams.get("agent") || null;
        json(res, 200, readLog(count, agent));
        return;
      }

      // Performance stats
      if (url.pathname === "/api/performance" && req.method === "GET") {
        json(res, 200, getPerformanceSummary());
        return;
      }

      json(res, 404, { error: "Not found" });
    } catch (err) {
      json(res, 500, { error: err.message?.slice(0, 200) });
    }
  });

  server.listen(CONFIG.httpPort, CONFIG.httpHost, () => {
    console.log(`  API:      http://${CONFIG.httpHost}:${CONFIG.httpPort}`);
  });

  return server;
}

export function stopHttpApi() {
  if (server) {
    server.close();
    server = null;
  }
}
