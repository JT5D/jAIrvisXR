#!/usr/bin/env node
/**
 * Jarvis Live Dashboard Server
 * Serves a real-time dashboard showing agent status, transcripts, and task queue.
 * Polls /tmp/jarvis-daemon/shared-memory.json and activity-log.jsonl
 */

import { createServer } from "http";
import { readFile, stat } from "fs/promises";
import { existsSync } from "fs";

const PORT = 7438;
const DAEMON_DIR = "/tmp/jarvis-daemon";
const SM_PATH = `${DAEMON_DIR}/shared-memory.json`;
const LOG_PATH = `${DAEMON_DIR}/activity-log.jsonl`;
const V2_LOG = `${DAEMON_DIR}/jarvis-v2.log`;

async function readJson(path) {
  try {
    return JSON.parse(await readFile(path, "utf-8"));
  } catch {
    return null;
  }
}

async function tailLines(path, n = 50) {
  try {
    const text = await readFile(path, "utf-8");
    const lines = text.trim().split("\n");
    return lines.slice(-n);
  } catch {
    return [];
  }
}

const server = createServer(async (req, res) => {
  const url = new URL(req.url, `http://localhost:${PORT}`);

  if (url.pathname === "/api/status") {
    const sm = await readJson(SM_PATH);
    res.writeHead(200, { "Content-Type": "application/json", "Access-Control-Allow-Origin": "*" });
    res.end(JSON.stringify(sm || { error: "No shared memory" }));
    return;
  }

  if (url.pathname === "/api/logs") {
    const count = parseInt(url.searchParams.get("n") || "30");
    const lines = await tailLines(LOG_PATH, count);
    const entries = lines.map(l => { try { return JSON.parse(l); } catch { return null; } }).filter(Boolean);
    res.writeHead(200, { "Content-Type": "application/json", "Access-Control-Allow-Origin": "*" });
    res.end(JSON.stringify(entries));
    return;
  }

  if (url.pathname === "/api/v2log") {
    const lines = await tailLines(V2_LOG, 40);
    res.writeHead(200, { "Content-Type": "application/json", "Access-Control-Allow-Origin": "*" });
    res.end(JSON.stringify(lines));
    return;
  }

  // Serve dashboard HTML
  res.writeHead(200, { "Content-Type": "text/html" });
  res.end(DASHBOARD_HTML);
});

server.listen(PORT, () => {
  console.log(`Jarvis Dashboard → http://localhost:${PORT}`);
});

const DASHBOARD_HTML = `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>jAIrvisXR — Live Dashboard</title>
<style>
  :root {
    --bg: #0a0e17;
    --surface: #111827;
    --surface2: #1a2332;
    --border: #1e2d3d;
    --text: #e2e8f0;
    --dim: #64748b;
    --accent: #38bdf8;
    --green: #22c55e;
    --red: #ef4444;
    --amber: #f59e0b;
    --purple: #a78bfa;
  }
  * { margin: 0; padding: 0; box-sizing: border-box; }
  body {
    font-family: 'SF Mono', 'Fira Code', 'Cascadia Code', monospace;
    background: var(--bg);
    color: var(--text);
    min-height: 100vh;
    overflow-x: hidden;
  }
  .header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 16px 24px;
    border-bottom: 1px solid var(--border);
    background: var(--surface);
  }
  .header h1 {
    font-size: 18px;
    font-weight: 600;
    color: var(--accent);
    letter-spacing: 1px;
  }
  .header h1 span { color: var(--dim); font-weight: 400; }
  .header-meta {
    display: flex;
    gap: 16px;
    align-items: center;
    font-size: 12px;
    color: var(--dim);
  }
  .pulse {
    width: 8px; height: 8px;
    border-radius: 50%;
    background: var(--green);
    animation: pulse 2s ease-in-out infinite;
    display: inline-block;
  }
  .pulse.offline { background: var(--red); animation: none; }
  @keyframes pulse {
    0%, 100% { opacity: 1; box-shadow: 0 0 0 0 rgba(34,197,94,0.4); }
    50% { opacity: 0.7; box-shadow: 0 0 0 6px rgba(34,197,94,0); }
  }
  .grid {
    display: grid;
    grid-template-columns: 1fr 1fr 1fr;
    grid-template-rows: auto 1fr;
    gap: 1px;
    background: var(--border);
    min-height: calc(100vh - 57px);
  }
  .panel {
    background: var(--surface);
    padding: 16px;
    overflow-y: auto;
  }
  .panel-title {
    font-size: 11px;
    text-transform: uppercase;
    letter-spacing: 2px;
    color: var(--dim);
    margin-bottom: 12px;
    display: flex;
    justify-content: space-between;
    align-items: center;
  }
  .panel-title .badge {
    font-size: 10px;
    padding: 2px 6px;
    border-radius: 3px;
    background: var(--surface2);
    color: var(--accent);
  }

  /* Agent Cards */
  .agent-card {
    background: var(--surface2);
    border: 1px solid var(--border);
    border-radius: 6px;
    padding: 12px;
    margin-bottom: 8px;
  }
  .agent-card .agent-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 8px;
  }
  .agent-card .agent-name {
    font-weight: 600;
    font-size: 13px;
    color: var(--accent);
  }
  .agent-card .agent-type {
    font-size: 10px;
    color: var(--dim);
    background: var(--bg);
    padding: 2px 6px;
    border-radius: 3px;
  }
  .agent-card .caps {
    display: flex;
    flex-wrap: wrap;
    gap: 4px;
    margin-top: 6px;
  }
  .agent-card .cap {
    font-size: 9px;
    padding: 1px 5px;
    border-radius: 2px;
    background: var(--bg);
    color: var(--dim);
    border: 1px solid var(--border);
  }
  .status-dot {
    width: 6px; height: 6px;
    border-radius: 50%;
    display: inline-block;
    margin-right: 4px;
  }
  .status-dot.online { background: var(--green); }
  .status-dot.offline { background: var(--red); }

  /* Heartbeat */
  .heartbeat-bar {
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: 12px;
    color: var(--dim);
    margin-top: 4px;
  }
  .heartbeat-bar .ago { color: var(--green); }
  .heartbeat-bar .ago.stale { color: var(--amber); }
  .heartbeat-bar .ago.dead { color: var(--red); }

  /* Log entries */
  .log-entry {
    font-size: 11px;
    padding: 4px 8px;
    border-left: 2px solid var(--border);
    margin-bottom: 4px;
    line-height: 1.5;
  }
  .log-entry .ts {
    color: var(--dim);
    margin-right: 6px;
  }
  .log-entry.conversation { border-left-color: var(--accent); }
  .log-entry.startup { border-left-color: var(--green); }
  .log-entry.error { border-left-color: var(--red); }
  .log-entry.tool { border-left-color: var(--purple); }
  .log-entry .action { color: var(--accent); }
  .log-entry .meta { color: var(--dim); }

  /* Console log */
  .console-line {
    font-size: 11px;
    padding: 2px 0;
    line-height: 1.4;
    white-space: pre-wrap;
    word-break: break-all;
  }

  /* Stats */
  .stat-row {
    display: flex;
    justify-content: space-between;
    padding: 6px 0;
    border-bottom: 1px solid var(--border);
    font-size: 12px;
  }
  .stat-row:last-child { border-bottom: none; }
  .stat-label { color: var(--dim); }
  .stat-value { color: var(--text); font-weight: 500; }

  /* Transcript highlight */
  .transcript {
    color: var(--green);
    font-style: italic;
  }

  .full-width { grid-column: 1 / -1; }

  /* Task items */
  .task-item {
    background: var(--surface2);
    border: 1px solid var(--border);
    border-radius: 4px;
    padding: 8px 10px;
    margin-bottom: 6px;
    font-size: 11px;
  }
  .task-item .task-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 4px;
  }
  .task-item .task-id { color: var(--dim); font-size: 10px; }
  .task-status {
    font-size: 9px;
    padding: 1px 6px;
    border-radius: 3px;
    font-weight: 600;
    text-transform: uppercase;
  }
  .task-status.pending { background: var(--amber); color: var(--bg); }
  .task-status.completed { background: var(--green); color: var(--bg); }
  .task-status.in-progress { background: var(--accent); color: var(--bg); }
  .task-item .task-desc { color: var(--text); margin-bottom: 4px; }
  .task-item .task-result { color: var(--green); font-size: 10px; }
  .task-item .task-meta { color: var(--dim); font-size: 10px; }

  /* Messages */
  .msg-item {
    font-size: 11px;
    padding: 6px 8px;
    border-left: 2px solid var(--accent);
    margin-bottom: 4px;
    background: var(--surface2);
    border-radius: 0 4px 4px 0;
  }
  .msg-item .msg-from { color: var(--accent); font-weight: 600; }
  .msg-item .msg-to { color: var(--dim); }
  .msg-item .msg-text { color: var(--text); margin-top: 2px; }

  /* Scrollbar */
  ::-webkit-scrollbar { width: 4px; }
  ::-webkit-scrollbar-track { background: var(--bg); }
  ::-webkit-scrollbar-thumb { background: var(--border); border-radius: 2px; }
</style>
</head>
<body>

<div class="header">
  <h1>jAIrvisXR <span>Live Dashboard</span></h1>
  <div class="header-meta">
    <span id="conn-status"><span class="pulse"></span> Connected</span>
    <span id="last-update">—</span>
  </div>
</div>

<div class="grid">
  <!-- Agents Panel -->
  <div class="panel" id="agents-panel">
    <div class="panel-title">Agents <span class="badge" id="agent-count">0</span></div>
    <div id="agents-list"></div>
    <div style="margin-top: 16px">
      <div class="panel-title">System</div>
      <div id="system-info"></div>
    </div>
  </div>

  <!-- Stats Panel -->
  <div class="panel" id="stats-panel">
    <div class="panel-title">Status</div>
    <div id="status-stats"></div>
    <div style="margin-top: 16px">
      <div class="panel-title">Capabilities</div>
      <div id="capabilities"></div>
    </div>
    <div style="margin-top: 16px">
      <div class="panel-title">Lessons Learned <span class="badge" id="lesson-count">0</span></div>
      <div id="lessons" style="max-height: 200px; overflow-y: auto"></div>
    </div>
  </div>

  <!-- Task Queue -->
  <div class="panel" id="task-panel">
    <div class="panel-title">Task Queue <span class="badge" id="task-count">0</span></div>
    <div id="task-list"></div>
    <div style="margin-top: 16px">
      <div class="panel-title">Messages</div>
      <div id="message-list"></div>
    </div>
  </div>

  <!-- Activity Log -->
  <div class="panel" id="log-panel">
    <div class="panel-title">Activity Log <span class="badge" id="log-count">0</span></div>
    <div id="activity-log"></div>
  </div>

  <!-- Console -->
  <div class="panel full-width" id="console-panel">
    <div class="panel-title">Console (jarvis-v2.log)</div>
    <div id="console-output"></div>
  </div>
</div>

<script>
const API = '';
let pollInterval = 2000;

function ago(ts) {
  if (!ts) return '—';
  const ms = Date.now() - (typeof ts === 'number' ? ts : new Date(ts).getTime());
  if (ms < 1000) return 'just now';
  if (ms < 60000) return Math.floor(ms/1000) + 's ago';
  if (ms < 3600000) return Math.floor(ms/60000) + 'm ago';
  return Math.floor(ms/3600000) + 'h ago';
}

function agoClass(ts) {
  if (!ts) return 'dead';
  const ms = Date.now() - (typeof ts === 'number' ? ts : new Date(ts).getTime());
  if (ms < 30000) return '';
  if (ms < 120000) return 'stale';
  return 'dead';
}

function timeStr(ts) {
  if (!ts) return '';
  const d = new Date(typeof ts === 'number' ? ts : ts);
  return d.toLocaleTimeString('en-US', { hour12: true, hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

function escHtml(s) {
  return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

function stripAnsi(s) {
  return s.replace(/\\x1b\\[[0-9;]*m/g, '').replace(/\x1b\\[[0-9;]*m/g, '').replace(/\\u001b\\[[0-9;]*m/g, '');
}

async function fetchStatus() {
  try {
    const res = await fetch(API + '/api/status');
    return await res.json();
  } catch { return null; }
}

async function fetchLogs() {
  try {
    const res = await fetch(API + '/api/logs?n=40');
    return await res.json();
  } catch { return []; }
}

async function fetchConsole() {
  try {
    const res = await fetch(API + '/api/v2log');
    return await res.json();
  } catch { return []; }
}

function renderAgents(sm) {
  const agents = sm?._agents || {};
  const names = Object.keys(agents);
  document.getElementById('agent-count').textContent = names.length;

  document.getElementById('agents-list').innerHTML = names.map(name => {
    const a = agents[name];
    const isOnline = a.status === 'online';
    const caps = (a.capabilities || []).map(c => '<span class="cap">' + escHtml(c) + '</span>').join('');
    return '<div class="agent-card">' +
      '<div class="agent-header">' +
        '<span class="agent-name"><span class="status-dot ' + (isOnline ? 'online' : 'offline') + '"></span>' + escHtml(name) + '</span>' +
        '<span class="agent-type">' + escHtml(a.type || '?') + '</span>' +
      '</div>' +
      (a.pid ? '<div style="font-size:11px;color:var(--dim)">PID ' + a.pid + (a.project ? ' · ' + escHtml(a.project) : '') + '</div>' : '') +
      '<div class="heartbeat-bar">Started: ' + timeStr(a.startedAt) + '</div>' +
      '<div class="caps">' + caps + '</div>' +
    '</div>';
  }).join('');
}

function renderStats(sm) {
  const hb = sm?.['jarvis-heartbeat'];
  const status = sm?.['jarvis-status'] || 'unknown';
  const sup = sm?.['jarvis-supervisor'] || {};
  const keepalive = sm?.['keepalive-status'] || {};

  const hbAgo = ago(hb);
  const hbClass = agoClass(hb);

  document.getElementById('status-stats').innerHTML =
    '<div class="stat-row"><span class="stat-label">Daemon Status</span><span class="stat-value">' +
      '<span class="status-dot ' + (status === 'online' ? 'online' : 'offline') + '"></span>' + escHtml(status) + '</span></div>' +
    '<div class="stat-row"><span class="stat-label">Heartbeat</span><span class="stat-value"><span class="ago ' + hbClass + '">' + hbAgo + '</span></span></div>' +
    '<div class="stat-row"><span class="stat-label">Supervisor PID</span><span class="stat-value">' + (sup.pid || '—') + '</span></div>' +
    '<div class="stat-row"><span class="stat-label">Restarts</span><span class="stat-value">' + (sup.restartCount ?? '—') + '</span></div>' +
    '<div class="stat-row"><span class="stat-label">Last Restart</span><span class="stat-value">' + timeStr(sup.lastRestart) + '</span></div>' +
    '<div class="stat-row"><span class="stat-label">Keepalive Uptime</span><span class="stat-value">' + (keepalive.uptimeMin ? Math.floor(keepalive.uptimeMin) + ' min' : '—') + '</span></div>';

  // Capabilities
  const caps = sm?.['jarvis-capabilities'] || [];
  document.getElementById('capabilities').innerHTML = '<div class="caps" style="display:flex;flex-wrap:wrap;gap:4px">' +
    caps.map(c => '<span class="cap">' + escHtml(c) + '</span>').join('') + '</div>';

  // Lessons
  const lessons = sm?.['agent-lessons'] || [];
  document.getElementById('lesson-count').textContent = lessons.length;
  document.getElementById('lessons').innerHTML = lessons.slice(-8).map(l =>
    '<div style="font-size:11px;padding:4px 0;border-bottom:1px solid var(--border)">' +
      '<span style="color:var(--amber)">[' + escHtml(l.category || '?') + ']</span> ' +
      escHtml((l.lesson || '').slice(0, 120)) +
    '</div>'
  ).join('');

  // Connection
  const connEl = document.getElementById('conn-status');
  const pulseEl = connEl.querySelector('.pulse');
  if (hbClass === 'dead') {
    pulseEl.classList.add('offline');
    connEl.innerHTML = '<span class="pulse offline"></span> Stale';
  } else {
    pulseEl?.classList.remove('offline');
  }

  document.getElementById('last-update').textContent = 'Updated ' + new Date().toLocaleTimeString();
}

function renderLogs(entries) {
  document.getElementById('log-count').textContent = entries.length;
  const container = document.getElementById('activity-log');
  const wasAtBottom = container.scrollHeight - container.scrollTop - container.clientHeight < 50;

  container.innerHTML = entries.map(e => {
    let cls = '';
    if (e.action === 'conversation') cls = 'conversation';
    else if (e.action === 'startup') cls = 'startup';
    else if (!e.success) cls = 'error';
    else if (e.tool) cls = 'tool';

    let detail = '';
    if (e.meta?.userText) detail = ' <span class="transcript">"' + escHtml(e.meta.userText.slice(0, 80)) + '"</span>';
    else if (e.meta?.reason) detail = ' <span class="meta">' + escHtml(e.meta.reason.slice(0, 60)) + '</span>';
    else if (e.tool) detail = ' <span class="meta">' + escHtml(e.tool) + '</span>';

    return '<div class="log-entry ' + cls + '">' +
      '<span class="ts">' + timeStr(e.ts) + '</span>' +
      '<span class="action">' + escHtml(e.action || '?') + '</span>' +
      (e.meta?.provider ? ' <span class="meta">(' + escHtml(e.meta.provider) + ')</span>' : '') +
      detail +
    '</div>';
  }).join('');

  if (wasAtBottom) container.scrollTop = container.scrollHeight;
}

function renderConsole(lines) {
  const container = document.getElementById('console-output');
  const wasAtBottom = container.scrollHeight - container.scrollTop - container.clientHeight < 50;

  container.innerHTML = lines.map(l => {
    // Strip ANSI escape codes for display
    const clean = l.replace(/\x1b\[[0-9;]*m/g, '');
    let color = 'var(--text)';
    if (clean.includes('[Local Whisper]') || clean.includes('Whisper]')) color = 'var(--green)';
    else if (clean.includes('rate-limited') || clean.includes('fallback')) color = 'var(--amber)';
    else if (clean.includes('error') || clean.includes('Error')) color = 'var(--red)';
    else if (clean.includes('━') || clean.includes('Brain:')) color = 'var(--accent)';
    return '<div class="console-line" style="color:' + color + '">' + escHtml(clean) + '</div>';
  }).join('');

  if (wasAtBottom) container.scrollTop = container.scrollHeight;
}

function renderSystem(sm) {
  const sys = sm?._systemInfo || {};
  document.getElementById('system-info').innerHTML =
    '<div class="stat-row"><span class="stat-label">Host</span><span class="stat-value">' + escHtml(sys.hostname || '—') + '</span></div>' +
    '<div class="stat-row"><span class="stat-label">Platform</span><span class="stat-value">' + escHtml(sys.platform || '—') + ' ' + escHtml(sys.arch || '') + '</span></div>' +
    '<div class="stat-row"><span class="stat-label">Node</span><span class="stat-value">' + escHtml(sys.nodeVersion || '—') + '</span></div>';
}

function renderTasks(sm) {
  const tasks = sm?._taskQueue || [];
  document.getElementById('task-count').textContent = tasks.length;
  document.getElementById('task-list').innerHTML = tasks.map(t => {
    const statusCls = (t.status || 'pending').replace('_', '-');
    return '<div class="task-item">' +
      '<div class="task-header">' +
        '<span class="task-id">' + escHtml(t.id || '?') + '</span>' +
        '<span class="task-status ' + statusCls + '">' + escHtml(t.status || '?') + '</span>' +
      '</div>' +
      '<div class="task-desc">' + escHtml(t.description || '') + '</div>' +
      (t.result ? '<div class="task-result">' + escHtml(t.result) + '</div>' : '') +
      '<div class="task-meta">' + escHtml(t.assignedTo || '') + (t.completedAt ? ' · ' + timeStr(t.completedAt) : '') + '</div>' +
    '</div>';
  }).join('') || '<div style="font-size:11px;color:var(--dim)">No tasks</div>';

  // Messages
  const msgs = sm?._messages || [];
  document.getElementById('message-list').innerHTML = msgs.slice(-10).map(m =>
    '<div class="msg-item">' +
      '<span class="msg-from">' + escHtml(m.from || '?') + '</span>' +
      ' <span class="msg-to">→ ' + escHtml(m.to || '?') + '</span>' +
      ' <span style="color:var(--dim);font-size:10px">' + timeStr(m.timestamp) + '</span>' +
      '<div class="msg-text">' + escHtml(m.text || '') + '</div>' +
    '</div>'
  ).join('') || '<div style="font-size:11px;color:var(--dim)">No messages</div>';
}

async function poll() {
  const [sm, logs, console_] = await Promise.all([fetchStatus(), fetchLogs(), fetchConsole()]);
  if (sm && !sm.error) {
    renderAgents(sm);
    renderStats(sm);
    renderSystem(sm);
    renderTasks(sm);
  }
  renderLogs(logs);
  renderConsole(console_);
}

poll();
setInterval(poll, pollInterval);
</script>
</body>
</html>`;
