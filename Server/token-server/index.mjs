#!/usr/bin/env node
/**
 * LiveKit Token Server for jAIrvisXR
 *
 * Generates JWT room tokens for Unity clients connecting to LiveKit.
 * Runs alongside the Jarvis daemon on a separate port.
 *
 * Usage:
 *   npm install livekit-server-sdk
 *   node Server/token-server/index.mjs
 *
 * Environment:
 *   LIVEKIT_API_KEY    - LiveKit API key (default: devkey)
 *   LIVEKIT_API_SECRET - LiveKit API secret (default: secret)
 *   TOKEN_SERVER_PORT  - Port to listen on (default: 7439)
 */

import http from "node:http";
import { URL } from "node:url";

// Try to load livekit-server-sdk, fall back to mock tokens
let AccessToken;
try {
  const lk = await import("livekit-server-sdk");
  AccessToken = lk.AccessToken;
} catch {
  AccessToken = null;
  console.warn("[token-server] livekit-server-sdk not installed — using mock tokens.");
  console.warn("[token-server] Install with: npm install livekit-server-sdk");
}

const API_KEY = process.env.LIVEKIT_API_KEY || "devkey";
const API_SECRET = process.env.LIVEKIT_API_SECRET || "secret";
const PORT = parseInt(process.env.TOKEN_SERVER_PORT || "7439", 10);

function generateToken(room, identity) {
  if (!AccessToken) {
    // Mock token for development without LiveKit SDK
    const payload = JSON.stringify({ room, identity, iat: Date.now() });
    const mock = Buffer.from(payload).toString("base64url");
    return `mock.${mock}.dev`;
  }

  const token = new AccessToken(API_KEY, API_SECRET, { identity });
  token.addGrant({
    room,
    roomJoin: true,
    canPublish: true,
    canSubscribe: true,
    canPublishData: true,
  });
  return token.toJwt();
}

const server = http.createServer(async (req, res) => {
  // CORS headers
  res.setHeader("Access-Control-Allow-Origin", "*");
  res.setHeader("Access-Control-Allow-Methods", "GET, OPTIONS");
  res.setHeader("Access-Control-Allow-Headers", "Content-Type");

  if (req.method === "OPTIONS") {
    res.writeHead(204);
    res.end();
    return;
  }

  const url = new URL(req.url, `http://localhost:${PORT}`);

  if (url.pathname === "/token" && req.method === "GET") {
    const room = url.searchParams.get("room");
    const identity = url.searchParams.get("identity");

    if (!room || !identity) {
      res.writeHead(400, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ error: "Missing 'room' or 'identity' query param" }));
      return;
    }

    try {
      const token = await generateToken(room, identity);
      console.log(`[token-server] Token issued: room=${room} identity=${identity}`);
      res.writeHead(200, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ token }));
    } catch (err) {
      console.error(`[token-server] Token generation failed: ${err.message}`);
      res.writeHead(500, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ error: err.message }));
    }
    return;
  }

  if (url.pathname === "/health") {
    res.writeHead(200, { "Content-Type": "application/json" });
    res.end(JSON.stringify({
      status: "ok",
      sdk: AccessToken ? "livekit-server-sdk" : "mock",
      port: PORT,
    }));
    return;
  }

  res.writeHead(404, { "Content-Type": "application/json" });
  res.end(JSON.stringify({ error: "Not found. Use GET /token?room=X&identity=Y" }));
});

server.listen(PORT, () => {
  console.log(`
┌─────────────────────────────────────────────┐
│  jAIrvisXR LiveKit Token Server             │
│  Port: ${String(PORT).padEnd(37)}│
│  SDK:  ${(AccessToken ? "livekit-server-sdk" : "mock (dev mode)").padEnd(37)}│
│  GET /token?room=X&identity=Y               │
│  GET /health                                │
└─────────────────────────────────────────────┘
`);
});
