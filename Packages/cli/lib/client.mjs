/**
 * HTTP client for Jarvis Daemon API.
 * All communication with the daemon goes through localhost:7437.
 */

const DEFAULT_PORT = 7437;
const DEFAULT_HOST = "127.0.0.1";

function url(path, port = DEFAULT_PORT) {
  return `http://${DEFAULT_HOST}:${port}${path}`;
}

export async function health(port) {
  const res = await fetch(url("/api/health", port));
  if (!res.ok) throw new Error(`Health check failed: ${res.status}`);
  return res.json();
}

export async function status(port) {
  const res = await fetch(url("/api/status", port));
  if (!res.ok) throw new Error(`Status failed: ${res.status}`);
  return res.json();
}

export async function sendCommand(command, port) {
  const res = await fetch(url("/api/command", port), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ command }),
  });
  if (!res.ok) throw new Error(`Command failed: ${res.status}`);
  return res.json();
}

export async function shareContext(type, content, meta, port) {
  const res = await fetch(url("/api/share", port), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ type, content, meta }),
  });
  if (!res.ok) throw new Error(`Share failed: ${res.status}`);
  return res.json();
}

export async function getLogs(count = 20, agent = null, port) {
  const params = new URLSearchParams({ n: String(count) });
  if (agent) params.set("agent", agent);
  const res = await fetch(url(`/api/logs?${params}`, port));
  if (!res.ok) throw new Error(`Logs failed: ${res.status}`);
  return res.json();
}

export async function getPerformance(port) {
  const res = await fetch(url("/api/performance", port));
  if (!res.ok) throw new Error(`Performance failed: ${res.status}`);
  return res.json();
}

export async function isRunning(port) {
  try {
    await health(port);
    return true;
  } catch {
    return false;
  }
}
