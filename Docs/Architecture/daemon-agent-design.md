# jAIrvisXR Daemon Agent - Architecture Design

**Author:** Claude Code + James Tunick
**Date:** 2026-03-10
**Status:** Design Phase (no implementation code yet)
**Previous Work:** `web-scraper/src/daemon/jarvis-listen.mjs` (v2 daemon)

---

## 1. Overview

The Jarvis Daemon Agent is an always-on macOS background process that serves as the central orchestrator for the jAIrvisXR ecosystem. It listens for voice commands via the Mac microphone, accepts shared context from CLI and browser extensions, and spawns sub-agents to execute complex tasks.

### 1.1 Goals

- **Always-on voice interface** — "Hey Jarvis" wake word activates the agent
- **Orchestrator role** — delegates to sub-agents for specialized tasks (code, research, XR, etc.)
- **Shared context ingestion** — URLs, files, images, code references via CLI and local HTTP API
- **Safe by design** — never pushes to repos without explicit permission, never interferes with other processes
- **Fault-tolerant** — auto-restarts, survives sleep/wake cycles, graceful degradation

### 1.2 What Changed from v2 (web-scraper daemon)

| Aspect | v2 (web-scraper) | v3 (jAIrvisXR) |
|--------|-------------------|-----------------|
| Location | `web-scraper/src/daemon/` | `jAIrvisXR/Daemon/` |
| Coupling | Tightly coupled to xrai-spatial-web server | Standalone; project-agnostic |
| Context input | Voice only | Voice + CLI + HTTP API + file watch |
| Agent model | Single LLM + tools | Orchestrator + sub-agent spawning |
| Provider chain | Groq -> Gemini -> Ollama | Configurable; same chain but pluggable |
| TTS | Depends on web-scraper server for Edge TTS | Self-contained TTS (say fallback, optional Edge TTS) |
| Process model | keepalive -> supervisor -> daemon | launchd -> daemon (self-supervising) |
| Shared memory | JSON file at `/tmp/jarvis-daemon/` | Same location, structured schema |

---

## 2. System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        macOS launchd                            │
│                  com.jairvisxr.daemon.plist                      │
└──────────────────────────┬──────────────────────────────────────┘
                           │ RunAtLoad, KeepAlive
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│                     jarvis-daemon.mjs                           │
│                   (Main Orchestrator Process)                   │
│                                                                 │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌───────────────┐  │
│  │  Voice   │  │   CLI    │  │  HTTP    │  │  Sub-Agent    │  │
│  │ Listener │  │ Handler  │  │  API     │  │  Spawner      │  │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └───────┬───────┘  │
│       │              │             │                │           │
│       ▼              ▼             ▼                ▼           │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                  Command Router                         │   │
│  │         (parses intent, selects handler)                 │   │
│  └────────────────────────┬────────────────────────────────┘   │
│                           │                                     │
│       ┌───────────────────┼───────────────────┐                │
│       ▼                   ▼                   ▼                │
│  ┌─────────┐       ┌───────────┐       ┌───────────┐          │
│  │ Direct  │       │ Sub-Agent │       │  Memory   │          │
│  │ Tools   │       │  Manager  │       │  & Log    │          │
│  └─────────┘       └───────────┘       └───────────┘          │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
         │                    │                    │
         ▼                    ▼                    ▼
  ┌─────────────┐   ┌──────────────┐   ┌──────────────────┐
  │  sox (mic)  │   │  Claude Code │   │ /tmp/jarvis-daemon│
  │  Groq STT   │   │  (sub-agent) │   │  shared-memory    │
  │  macOS say  │   │  Other agents│   │  activity-log     │
  │  afplay     │   │              │   │  context-store    │
  └─────────────┘   └──────────────┘   └──────────────────┘
```

---

## 3. Module Design

### 3.1 File Structure

```
jAIrvisXR/
├── Daemon/
│   ├── jarvis-daemon.mjs          # Main entry point (orchestrator)
│   ├── voice-listener.mjs         # sox recording + Groq Whisper STT
│   ├── command-router.mjs         # Intent parsing, handler dispatch
│   ├── tool-executor.mjs          # Direct tool execution (shell, files, browser)
│   ├── sub-agent-manager.mjs      # Spawn and manage sub-agents
│   ├── http-api.mjs               # Local HTTP API (localhost:7437)
│   ├── cli-handler.mjs            # CLI command processing
│   ├── context-store.mjs          # Shared context from CLI/API/voice
│   ├── shared-memory.mjs          # Persistent KV store (JSON file)
│   ├── activity-log.mjs           # Structured JSONL activity log
│   ├── agent-learning.mjs         # Auto-learning from activity patterns
│   ├── tts-speaker.mjs            # TTS output (macOS say + optional Edge TTS)
│   ├── safety-guard.mjs           # Command safety checks, permission gates
│   ├── config.mjs                 # Centralized configuration
│   └── package.json               # Dependencies (minimal: dotenv only)
├── Scripts/
│   ├── install-daemon.sh          # Install launchd plist + verify deps
│   ├── uninstall-daemon.sh        # Remove launchd plist
│   ├── jarvis                     # CLI symlink script (→ /usr/local/bin/jarvis)
│   └── jarvis-completion.zsh      # Shell tab-completion
└── Docs/
    └── Architecture/
        └── daemon-agent-design.md  # This document
```

### 3.2 Module Descriptions

#### 3.2.1 `jarvis-daemon.mjs` — Main Orchestrator

The single entry point. Responsibilities:
- Initialize all subsystems (voice, HTTP API, shared memory, learning)
- Run the main event loop
- Self-supervise (catch crashes, auto-restart internal components)
- Write heartbeat to shared memory every 30s
- Graceful shutdown on SIGINT/SIGTERM

**Key change from v2:** Instead of a separate supervisor process wrapping the daemon, the daemon itself handles crash recovery internally. The keepalive + supervisor + daemon three-process model from v2 is collapsed into a single process that launchd manages directly. This is simpler and more reliable because launchd is already a process supervisor.

#### 3.2.2 `voice-listener.mjs` — Audio Capture + STT

Extracted from the monolithic v2 `jarvis-listen.mjs`. Encapsulates:
- Audio recording via `sox` (16kHz, mono, 16-bit WAV)
- Silence detection (configurable threshold)
- Passive mode: short recordings (5s), checking for wake word
- Active mode: longer recordings (15s), processing all speech
- STT via Groq Whisper API (free tier)
- Wake word matching: "Hey Jarvis", "Jarvis", "OK Jarvis", "Yo Jarvis"

```
Interface:
  createVoiceListener(config) → {
    start()                    // Begin recording loop
    stop()                     // Stop recording
    onWakeWord(callback)       // Fires when wake word detected
    onSpeech(callback)         // Fires with transcribed text in active mode
    onError(callback)          // Fires on errors
    getMode() → "passive" | "active" | "processing"
  }
```

**sox recording args (unchanged from v2, proven stable):**
```
sox -d -r 16000 -c 1 -b 16 <outfile>.wav
    trim 0 <seconds>
    silence 1 0.1 1.5%
    1 <silence_duration> 1.5%
```

#### 3.2.3 `command-router.mjs` — Intent Parsing + Dispatch

Routes incoming commands from any source (voice, CLI, HTTP) to the appropriate handler.

```
Interface:
  createRouter(config) → {
    route(command) → Promise<Response>
    registerHandler(pattern, handler)
  }

Command Sources:
  - Voice: transcribed text after wake word
  - CLI:   `jarvis share <url|file|text>`, `jarvis ask <question>`, etc.
  - HTTP:  POST /api/command { type, payload }

Internal routing logic:
  1. Normalize input (trim, lowercase for matching)
  2. Check for CLI-specific commands (share, status, logs)
  3. Check for direct tool requests ("open browser", "run command")
  4. Fall through to LLM for natural language understanding
```

#### 3.2.4 `tool-executor.mjs` — Direct Tool Execution

Ported from v2 `jarvis-tools.mjs` with improvements:

**Retained tools:**
- `open_browser` — Open URL in default browser
- `run_shell` — Execute shell command (with safety checks)
- `read_file` — Read file contents
- `write_file` — Write/create file
- `list_directory` — List directory contents
- `read_memory` / `write_memory` — Shared memory access
- `read_activity_log` — Read recent activity
- `search_project` — Grep across project directories
- `record_lesson` — Persist learning

**New tools:**
- `read_context` — Read from the context store (URLs, files shared via CLI/API)
- `spawn_sub_agent` — Launch a sub-agent for a specific task
- `query_sub_agent` — Check on a running sub-agent's progress
- `set_reminder` — Schedule a future notification/action
- `screenshot` — Capture current screen (via `screencapture` on macOS)

**Safety (from v2, retained):**
```javascript
const BLOCKED_PATTERNS = [
  /rm\s+(-rf?|--recursive)/i,
  /rmdir/i, /mkfs/i, /dd\s+if=/i,
  />\s*\/dev\//i, /chmod\s+777/i,
  /curl.*\|\s*(bash|sh|zsh)/i,
  /git\s+push/i,          // ALL git push blocked by default
  /git\s+reset\s+--hard/i,
  /drop\s+(table|database)/i,
  /sudo/i,
];
```

**New safety rule:** `git push` is blocked entirely (not just `--force`). The user must explicitly grant push permission per-session via voice or CLI.

#### 3.2.5 `sub-agent-manager.mjs` — Sub-Agent Spawning

The orchestrator can delegate complex tasks to sub-agents. A sub-agent is a separate process (typically another LLM invocation with a specific system prompt and tool set).

```
Interface:
  createSubAgentManager(config) → {
    spawn(task) → { agentId, status }
    query(agentId) → { status, output }
    kill(agentId)
    listActive() → Agent[]
  }

Sub-Agent Types (initial):
  - "code-review"    — Reviews a file/diff, returns findings
  - "research"       — Searches web/docs for a topic, returns summary
  - "file-edit"      — Makes specific edits to files (with safety guards)
  - "custom"         — User-defined system prompt + tools

Sub-Agent Lifecycle:
  1. Orchestrator spawns sub-agent with: { task, systemPrompt, tools, timeout }
  2. Sub-agent runs in a child process (or async task)
  3. Sub-agent writes results to context store
  4. Orchestrator checks status, retrieves result
  5. Sub-agent auto-terminates after timeout (default: 60s)
  6. Max concurrent sub-agents: 3 (prevents resource exhaustion)
```

#### 3.2.6 `http-api.mjs` — Local HTTP API

A lightweight HTTP server on `localhost:7437` (port = "J-A-R-V" on a phone keypad, 7=J, 2=A, 7=R, 8=V... simplified to 7437).

```
Endpoints:
  POST /api/share
    Body: { type: "url"|"file"|"text"|"image"|"code", content: string, meta?: object }
    → Adds to context store, returns { id, status }

  POST /api/command
    Body: { command: string }
    → Routes through command-router, returns { response: string }

  GET /api/status
    → Returns daemon status, active sub-agents, recent context

  GET /api/context
    → Returns current context store contents

  DELETE /api/context/:id
    → Remove a context item

  GET /api/health
    → Returns { status: "ok", uptime, provider, mode }

  WebSocket /ws
    → Real-time event stream (voice transcriptions, agent responses, status changes)
    → Browser extensions can subscribe for live updates

Security:
  - Binds to 127.0.0.1 ONLY (not 0.0.0.0)
  - No authentication needed (localhost only)
  - Rate limiting: 60 requests/minute per endpoint
  - Request body size limit: 10MB (for images/files)
```

**Why port 7437?** It is unlikely to conflict with common development ports (3000, 3210, 5173, 8080, etc.) and is memorable.

#### 3.2.7 `cli-handler.mjs` — CLI Interface

The `jarvis` CLI command is a thin client that sends requests to the HTTP API.

```
Usage:
  jarvis share <url>              # Share a URL for context
  jarvis share <file>             # Share a file for context
  jarvis share "<text>"           # Share text for context
  jarvis ask "<question>"         # Ask Jarvis a question (text mode)
  jarvis status                   # Show daemon status
  jarvis logs [--tail]            # Show activity log
  jarvis context                  # Show current context store
  jarvis context clear            # Clear context store
  jarvis permit push              # Grant git push permission for this session
  jarvis stop                     # Graceful shutdown
  jarvis restart                  # Restart daemon

Implementation:
  - Thin shell script or Node.js script
  - All commands are HTTP requests to localhost:7437
  - If daemon is not running, prints error + suggests starting
  - Symlinked to /usr/local/bin/jarvis for global access
```

**CLI script (`Scripts/jarvis`):**
```bash
#!/bin/bash
# Jarvis CLI — thin client for the daemon's HTTP API
JARVIS_PORT="${JARVIS_PORT:-7437}"
JARVIS_URL="http://127.0.0.1:${JARVIS_PORT}"

case "$1" in
  share)
    shift
    INPUT="$*"
    # Detect if it's a file path, URL, or text
    if [[ -f "$INPUT" ]]; then
      TYPE="file"
      CONTENT=$(base64 < "$INPUT")
    elif [[ "$INPUT" =~ ^https?:// ]]; then
      TYPE="url"
      CONTENT="$INPUT"
    else
      TYPE="text"
      CONTENT="$INPUT"
    fi
    curl -s -X POST "$JARVIS_URL/api/share" \
      -H "Content-Type: application/json" \
      -d "{\"type\":\"$TYPE\",\"content\":\"$CONTENT\"}"
    ;;
  ask)
    shift
    curl -s -X POST "$JARVIS_URL/api/command" \
      -H "Content-Type: application/json" \
      -d "{\"command\":\"$*\"}"
    ;;
  status)
    curl -s "$JARVIS_URL/api/status" | python3 -m json.tool
    ;;
  logs)
    if [[ "$2" == "--tail" ]]; then
      tail -f /tmp/jarvis-daemon/activity-log.jsonl
    else
      tail -20 /tmp/jarvis-daemon/activity-log.jsonl | python3 -c "
import sys, json
for line in sys.stdin:
    e = json.loads(line)
    print(f\"[{e.get('ts','')}] {e.get('agent','')}: {e.get('action','')}\")" 2>/dev/null
    fi
    ;;
  context)
    if [[ "$2" == "clear" ]]; then
      curl -s -X DELETE "$JARVIS_URL/api/context"
    else
      curl -s "$JARVIS_URL/api/context" | python3 -m json.tool
    fi
    ;;
  permit)
    curl -s -X POST "$JARVIS_URL/api/command" \
      -H "Content-Type: application/json" \
      -d "{\"command\":\"grant-permission:$2\"}"
    ;;
  stop)
    curl -s -X POST "$JARVIS_URL/api/command" \
      -H "Content-Type: application/json" \
      -d "{\"command\":\"shutdown\"}"
    ;;
  restart)
    curl -s -X POST "$JARVIS_URL/api/command" \
      -H "Content-Type: application/json" \
      -d "{\"command\":\"restart\"}"
    ;;
  *)
    echo "Jarvis CLI - talk to the daemon"
    echo ""
    echo "Usage:"
    echo "  jarvis share <url|file|text>   Share context with Jarvis"
    echo "  jarvis ask \"<question>\"        Ask Jarvis a question"
    echo "  jarvis status                  Show daemon status"
    echo "  jarvis logs [--tail]           Show activity log"
    echo "  jarvis context [clear]         Show/clear context store"
    echo "  jarvis permit push             Grant git push permission"
    echo "  jarvis stop                    Stop the daemon"
    echo "  jarvis restart                 Restart the daemon"
    ;;
esac
```

#### 3.2.8 `context-store.mjs` — Shared Context Ingestion

A structured store for context that has been shared with the daemon from any source.

```
Interface:
  createContextStore(config) → {
    add(item) → { id, timestamp }
    get(id) → ContextItem | null
    getAll() → ContextItem[]
    getRecent(n) → ContextItem[]
    remove(id)
    clear()
    toPromptContext() → string   // Formats all context for LLM prompt injection
  }

ContextItem:
  {
    id: string,             // UUID
    type: "url" | "file" | "text" | "image" | "code",
    content: string,        // Raw content or base64 for binary
    source: "cli" | "api" | "voice" | "browser-extension",
    timestamp: string,      // ISO 8601
    meta: {
      filename?: string,
      mimeType?: string,
      url?: string,
      language?: string,    // For code
      summary?: string,     // AI-generated summary after ingestion
    }
  }

Storage:
  - In-memory Map for fast access
  - Persisted to /tmp/jarvis-daemon/context-store.json on changes
  - Auto-evicts items older than 24 hours
  - Max 50 items (oldest evicted first)
  - Total size cap: 50MB

LLM Context Injection:
  When the daemon sends a request to the LLM, the context store contents
  are injected into the system prompt as a "Current Context" section:

  "You currently have the following shared context:
   [1] URL: https://example.com — (shared via CLI, 5 min ago)
   [2] File: /path/to/code.ts — 142 lines of TypeScript
   [3] Text: 'Deploy the staging build' — (shared via voice, 2 min ago)"
```

#### 3.2.9 `shared-memory.mjs` — Persistent Key-Value Store

Carried forward from v2 with minimal changes. Same location (`/tmp/jarvis-daemon/shared-memory.json`), same atomic write pattern.

**Changes from v2:**
- Add schema versioning (`_schemaVersion: 3`)
- Add TTL support for ephemeral keys
- Add namespacing: `jarvis.*`, `claude-code.*`, `sub-agent.*`

#### 3.2.10 `tts-speaker.mjs` — Text-to-Speech Output

Self-contained TTS, no dependency on the web-scraper server.

```
Interface:
  createSpeaker(config) → {
    speak(text) → Promise<void>
    setVoice(voice)
    setEnabled(boolean)
    isSpeaking() → boolean
  }

TTS Priority Chain:
  1. Edge TTS (if edge-tts Python package installed) — higher quality
  2. macOS `say` command — always available, instant, no network
  3. Silent mode (log only) — if TTS disabled via config

macOS say voices (good options):
  - "Samantha" — default, female, clear
  - "Alex" — male, detailed (but slow to load first time)
  - "Daniel" — British male (good Jarvis feel)

afplay for Edge TTS output (MP3 playback).
```

#### 3.2.11 `safety-guard.mjs` — Safety + Permission System

Centralized safety module. All tool executions pass through this.

```
Interface:
  createSafetyGuard(config) → {
    checkCommand(cmd) → { allowed: boolean, reason?: string }
    grantPermission(scope, ttlSeconds)
    revokePermission(scope)
    getPermissions() → Permission[]
  }

Permission Scopes:
  - "git-push"         — Allow git push (default: denied)
  - "file-write"       — Allow writing files outside project dirs (default: denied)
  - "process-control"  — Allow killing/starting processes (default: denied)
  - "network-external" — Allow outbound HTTP to non-API hosts (default: allowed)

Permission Model:
  - Permissions are session-scoped (reset on daemon restart)
  - Permissions have TTL (default: 1 hour)
  - Granting requires explicit user action (voice confirmation or CLI `jarvis permit`)
  - All permission grants are logged to activity log

Safety Rules (always enforced, cannot be overridden):
  - Never run rm -rf, sudo, or destructive disk operations
  - Never modify files in /System, /Library, /usr (system directories)
  - Never send data to non-API external endpoints without explicit context
  - Max process spawn rate: 5 per minute
  - Max file write size: 10MB per operation
```

#### 3.2.12 `config.mjs` — Centralized Configuration

```javascript
export const CONFIG = {
  // Voice
  wakeWords: ["jarvis", "hey jarvis", "ok jarvis", "yo jarvis"],
  passiveRecordSeconds: 5,
  activeRecordSeconds: 15,
  silenceThreshold: "1.5%",
  passiveSilenceDuration: "1.5",
  activeSilenceDuration: "3.0",
  silentRoundsBeforePassive: 3,

  // AI Providers (order = priority)
  providers: ["groq", "gemini", "ollama"],
  maxToolRounds: 5,
  maxConversationHistory: 10,
  maxContextChars: 8000,

  // HTTP API
  apiPort: 7437,
  apiHost: "127.0.0.1",
  apiRateLimit: 60,           // requests per minute
  apiMaxBodySize: 10485760,   // 10MB

  // Sub-Agents
  maxConcurrentSubAgents: 3,
  subAgentDefaultTimeout: 60000, // 60s

  // Storage
  tmpDir: "/tmp/jarvis-daemon",
  memoryFile: "/tmp/jarvis-daemon/shared-memory.json",
  activityLogFile: "/tmp/jarvis-daemon/activity-log.jsonl",
  contextStoreFile: "/tmp/jarvis-daemon/context-store.json",

  // Safety
  blockedShellPatterns: [/* ... */],
  maxFileWriteSize: 10485760,
  maxProcessSpawnRate: 5,

  // TTS
  ttsVoice: "Samantha",
  ttsEnabled: true,

  // Projects (known project paths for search)
  projects: {
    "jairvisxr": "/Users/jamestunick/Applications/jAIrvisXR",
    "xrai-spatial-web": "/Users/jamestunick/Applications/web-scraper",
    "portals-v4": "/Users/jamestunick/dev/portals_v4_fresh",
  },
};
```

---

## 4. Process Lifecycle

### 4.1 Startup Sequence

```
1. launchd starts jarvis-daemon.mjs (RunAtLoad + KeepAlive)
2. Load config.mjs, read .env for API keys
3. Initialize shared memory (write agent metadata)
4. Initialize activity log
5. Initialize context store (load persisted items)
6. Initialize AI provider chain (Groq -> Gemini -> Ollama)
7. Start HTTP API server on localhost:7437
8. Start voice listener (sox recording loop)
9. Initialize agent learning (periodic pattern extraction)
10. Write heartbeat to shared memory
11. Speak "Jarvis online" via TTS
12. Enter main event loop
```

### 4.2 Main Event Loop

```
while (running) {
  // Voice listener runs in parallel, emits events:
  //   onWakeWord → switch to active mode
  //   onSpeech → route through command-router

  // HTTP API runs in parallel, handles:
  //   /api/share → add to context store
  //   /api/command → route through command-router

  // Periodic tasks (via setInterval):
  //   Every 30s: write heartbeat to shared memory
  //   Every 5m: run learning cycle (extract patterns from activity log)
  //   Every 1h: clean up old context items, rotate logs
}
```

### 4.3 Shutdown Sequence

```
1. Receive SIGINT or SIGTERM (from launchd or CLI)
2. Stop voice listener
3. Kill all active sub-agents (with 5s grace period)
4. Close HTTP API server
5. Write "offline" status to shared memory
6. Log shutdown to activity log
7. Clean up temp WAV/MP3 files
8. Exit process (launchd will NOT restart due to clean exit)
```

### 4.4 Crash Recovery

```
launchd KeepAlive=true handles automatic restart.
ThrottleInterval=10 prevents restart storms.

On restart:
  - Shared memory survives (it's on disk)
  - Context store survives (it's on disk)
  - Activity log survives (append-only)
  - Conversation history is lost (acceptable; fresh start)
  - Sub-agents are lost (will be re-detected as orphaned and cleaned up)
```

---

## 5. AI Provider Architecture

### 5.1 Provider Chain (from v2, proven)

```
Primary:    Groq (Llama, free tier, fastest, 12K TPM limit)
Fallback 1: Gemini (free tier, higher daily limit)
Fallback 2: Ollama (local, no limits, slowest)
```

Auto-failover logic (retained from v2):
- On rate limit (429): switch to next provider, schedule retry in 10 min
- On stream error: switch provider immediately
- On all-exhausted: 60s cooldown, then retry
- All provider switching is logged and written to shared memory

### 5.2 System Prompt Structure

```
[Base Jarvis Personality]
  - Warm, intelligent, helpful but not servile
  - Token-efficient responses (1-2 sentences unless asked for detail)

[Tool Definitions]
  - All available tools in OpenAI function-calling format

[Current Context]
  - Injected from context-store (URLs, files, text shared via CLI/API)
  - Max 2000 chars of context summary

[Active Sub-Agents]
  - List of running sub-agents and their tasks

[Recent Lessons]
  - Top 3 most relevant lessons from agent-learning

[Safety Rules]
  - Explicit rules about what is blocked
  - Current permission grants
```

### 5.3 Tool-Calling Loop (from v2, proven)

```
1. Send user message + system prompt + tool schemas to LLM
2. If LLM returns tool calls:
   a. Execute each tool via safety guard
   b. Truncate results to 500 chars (free-tier TPM limits)
   c. Feed results back as user messages
   d. Repeat (max 5 rounds)
3. If LLM returns text: speak it, log it, done
```

---

## 6. CLI and HTTP API Integration

### 6.1 Context Flow

```
                                  ┌──────────────┐
  Terminal:                       │              │
  $ jarvis share https://...  ──→ │  HTTP API    │ ──→ Context Store
  $ jarvis share ./file.ts    ──→ │  :7437       │ ──→ (persisted)
  $ jarvis ask "what is..."   ──→ │              │ ──→ Command Router
                                  └──────────────┘
                                         ↑
  Browser Extension:                     │
  "Share this page with Jarvis"  ────────┘
                                         ↑
  Voice:                                 │
  "Hey Jarvis, check this out"   ────────┘
  (voice carries the command,
   but URLs/files come via CLI/API)
```

### 6.2 Browser Extension Protocol

The HTTP API at `localhost:7437` serves as the integration point for browser extensions.

A companion Chrome/Firefox extension would:
1. Detect user clicking "Share with Jarvis" button
2. POST the current page URL + selected text to `localhost:7437/api/share`
3. Optionally subscribe to WebSocket at `ws://localhost:7437/ws` for live responses

Extension design is out of scope for this document but the API is designed to support it.

---

## 7. launchd Configuration

### 7.1 Plist: `com.jairvisxr.daemon.plist`

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.jairvisxr.daemon</string>

    <key>ProgramArguments</key>
    <array>
        <string>/opt/homebrew/bin/node</string>
        <string>/Users/jamestunick/Applications/jAIrvisXR/Daemon/jarvis-daemon.mjs</string>
    </array>

    <key>WorkingDirectory</key>
    <string>/Users/jamestunick/Applications/jAIrvisXR</string>

    <key>RunAtLoad</key>
    <true/>

    <key>KeepAlive</key>
    <true/>

    <key>StandardOutPath</key>
    <string>/tmp/jarvis-daemon/daemon-stdout.log</string>

    <key>StandardErrorPath</key>
    <string>/tmp/jarvis-daemon/daemon-stderr.log</string>

    <key>EnvironmentVariables</key>
    <dict>
        <key>PATH</key>
        <string>/usr/local/bin:/usr/bin:/bin:/opt/homebrew/bin</string>
        <key>HOME</key>
        <string>/Users/jamestunick</string>
        <key>NODE_ENV</key>
        <string>production</string>
    </dict>

    <key>ThrottleInterval</key>
    <integer>10</integer>

    <key>ProcessType</key>
    <string>Interactive</string>

    <key>Nice</key>
    <integer>5</integer>
</dict>
</plist>
```

### 7.2 Key launchd Settings

| Setting | Value | Why |
|---------|-------|-----|
| `RunAtLoad` | `true` | Start on login |
| `KeepAlive` | `true` | Auto-restart on crash |
| `ThrottleInterval` | `10` | Min 10s between restarts (prevents storm) |
| `ProcessType` | `Interactive` | Higher scheduling priority for voice responsiveness |
| `Nice` | `5` | Slightly lower CPU priority than foreground apps |

### 7.3 Install Script

```bash
#!/bin/bash
# Scripts/install-daemon.sh
PLIST_NAME="com.jairvisxr.daemon"
PLIST_PATH="$HOME/Library/LaunchAgents/${PLIST_NAME}.plist"
DAEMON_DIR="$(cd "$(dirname "$0")/.." && pwd)/Daemon"
NODE_PATH="$(which node)"

echo "Installing jAIrvisXR Daemon..."
echo "  Node:   $NODE_PATH"
echo "  Daemon: $DAEMON_DIR/jarvis-daemon.mjs"

# Verify dependencies
command -v sox >/dev/null || { echo "ERROR: sox not found. Run: brew install sox"; exit 1; }
command -v node >/dev/null || { echo "ERROR: node not found."; exit 1; }

# Create tmp dir
mkdir -p /tmp/jarvis-daemon

# Symlink CLI
ln -sf "$DAEMON_DIR/../Scripts/jarvis" /usr/local/bin/jarvis
chmod +x "$DAEMON_DIR/../Scripts/jarvis"

# Generate and install plist
cat > "$PLIST_PATH" << PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!-- Generated by install-daemon.sh -->
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
  "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>${PLIST_NAME}</string>
    <key>ProgramArguments</key>
    <array>
        <string>${NODE_PATH}</string>
        <string>${DAEMON_DIR}/jarvis-daemon.mjs</string>
    </array>
    <key>WorkingDirectory</key>
    <string>$(dirname "$DAEMON_DIR")</string>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>StandardOutPath</key>
    <string>/tmp/jarvis-daemon/daemon-stdout.log</string>
    <key>StandardErrorPath</key>
    <string>/tmp/jarvis-daemon/daemon-stderr.log</string>
    <key>EnvironmentVariables</key>
    <dict>
        <key>PATH</key>
        <string>/usr/local/bin:/usr/bin:/bin:/opt/homebrew/bin</string>
        <key>HOME</key>
        <string>${HOME}</string>
    </dict>
    <key>ThrottleInterval</key>
    <integer>10</integer>
    <key>ProcessType</key>
    <string>Interactive</string>
</dict>
</plist>
PLIST

launchctl unload "$PLIST_PATH" 2>/dev/null
launchctl load "$PLIST_PATH"

echo ""
echo "Installed: $PLIST_NAME"
echo "  CLI:    /usr/local/bin/jarvis"
echo "  Logs:   /tmp/jarvis-daemon/daemon-*.log"
echo "  Status: launchctl list | grep jairvisxr"
echo "  Stop:   jarvis stop"
```

---

## 8. Data Flow Diagrams

### 8.1 Voice Command Flow

```
User speaks "Hey Jarvis, what's in my context?"
      │
      ▼
[sox records 5s audio chunk]
      │
      ▼
[Groq Whisper STT] → "Hey Jarvis what's in my context"
      │
      ▼
[Wake word detected] → strip "Hey Jarvis" → "what's in my context"
      │
      ▼
[Switch to active mode, record 15s for follow-up]
      │
      ▼
[Command Router] → detects context query
      │
      ▼
[LLM with tools] → calls read_context tool
      │
      ▼
[Context Store] → returns 3 items
      │
      ▼
[LLM generates response] → "You have 3 items: a GitHub URL, a TypeScript file, and a text note."
      │
      ▼
[TTS Speaker] → macOS `say` speaks the response
      │
      ▼
[Activity Log] → logs the interaction
```

### 8.2 CLI Share Flow

```
$ jarvis share https://github.com/user/repo/pull/42
      │
      ▼
[CLI script detects URL type]
      │
      ▼
[HTTP POST localhost:7437/api/share]
  { type: "url", content: "https://...", source: "cli" }
      │
      ▼
[HTTP API receives, validates]
      │
      ▼
[Context Store] → stores with UUID, timestamp
      │
      ▼
[Response] → { id: "abc-123", status: "stored" }
      │
      ▼
[CLI prints] "Shared with Jarvis: https://github.com/user/repo/pull/42"

Later, when user asks Jarvis a question, the URL is available
in the context store and injected into the LLM prompt.
```

---

## 9. Migration from v2

### 9.1 What to Port (copy + refactor)

| v2 Module | v3 Module | Changes |
|-----------|-----------|---------|
| `jarvis-listen.mjs` | `voice-listener.mjs` | Extract voice-only logic, remove LLM/tool code |
| `jarvis-tools.mjs` | `tool-executor.mjs` | Add new tools, improve safety |
| `shared-memory.mjs` | `shared-memory.mjs` | Add schema version, TTL, namespaces |
| `activity-log.mjs` | `activity-log.mjs` | Minimal changes |
| `agent-learning.mjs` | `agent-learning.mjs` | Minimal changes |

### 9.2 What to Drop

- `jarvis-supervisor.mjs` — replaced by launchd KeepAlive
- `jarvis-keepalive.mjs` — replaced by launchd KeepAlive
- `kb-writer.mjs` — keep as optional tool, not core
- Dependency on `../server/agent/groq-client.mjs` — inline or use native fetch

### 9.3 What is New

- `http-api.mjs` — local API server
- `cli-handler.mjs` — CLI client
- `context-store.mjs` — shared context ingestion
- `sub-agent-manager.mjs` — orchestrator delegation
- `safety-guard.mjs` — centralized safety
- `config.mjs` — centralized config
- `Scripts/jarvis` — CLI symlink
- `Scripts/install-daemon.sh` — launchd installer
- launchd plist — replaces keepalive + supervisor

---

## 10. Dependencies

### 10.1 System Dependencies (already installed)

| Tool | Version | Purpose |
|------|---------|---------|
| `node` | v24.10.0 | Runtime |
| `sox` | SoX v14.4.2+ | Audio recording from microphone |
| `ffmpeg` | installed | Audio format conversion (if needed) |
| `say` | macOS built-in | TTS fallback |
| `afplay` | macOS built-in | Audio playback |
| `python3` | installed | Whisper CLI (optional), Edge TTS |
| `curl` | macOS built-in | CLI HTTP client |

### 10.2 npm Dependencies (minimal)

```json
{
  "name": "jairvisxr-daemon",
  "version": "3.0.0",
  "type": "module",
  "dependencies": {
    "dotenv": "^16.0.0"
  }
}
```

Everything else uses Node.js built-in modules:
- `node:http` — HTTP API server
- `node:child_process` — sox, say, afplay, sub-agents
- `node:fs` — file operations, shared memory
- `node:path`, `node:url`, `node:os` — utilities
- `node:crypto` — UUID generation for context items
- Native `fetch` — Groq/Gemini API calls (Node 18+)
- Native `FormData` — multipart uploads to Whisper API
- Native `WebSocket` — (Node 22+ has built-in WebSocket, or use `node:net` for raw WS)

### 10.3 API Keys (environment variables)

```
GROQ_API_KEY=gsk_...         # Required: STT (Whisper) + LLM (Llama)
GEMINI_API_KEY=...           # Optional: fallback LLM
ANTHROPIC_API_KEY=sk-ant-... # Optional: for sub-agent spawning via Claude
OLLAMA_HOST=http://localhost:11434  # Optional: local LLM fallback
```

---

## 11. Security Considerations

### 11.1 Network Security

- HTTP API binds to `127.0.0.1` only — no external access
- No TLS needed (localhost-only)
- No authentication needed (trusted local environment)
- WebSocket connections accepted only from localhost

### 11.2 Process Safety

- All shell commands pass through safety guard (blocked patterns)
- Git push is blocked by default; requires explicit per-session permission
- No `sudo`, no destructive file operations
- Sub-agents inherit safety restrictions
- Max 3 concurrent sub-agents (resource protection)

### 11.3 Data Safety

- API keys loaded from `.env` file, never logged or written to shared memory
- Shared memory and context store contain no secrets
- Activity log does not record full command outputs (truncated)
- Temp audio files cleaned up after processing

### 11.4 Graceful Coexistence

- Does not kill or interfere with any existing processes
- Does not modify files outside of:
  - `/tmp/jarvis-daemon/` (runtime data)
  - User-specified project directories (only when explicitly requested)
- Does not auto-push to any git repository
- Does not open network ports accessible from outside the machine

---

## 12. Open Questions

1. **WebSocket implementation** — Node.js 22+ has experimental WebSocket support. Should we use the built-in or add `ws` as a dependency? Using the built-in keeps the zero-dependency goal but may have stability concerns. **Recommendation:** Use built-in `node:http` for the upgrade handshake and raw TCP for WebSocket frames, or accept `ws` as a single dependency.

2. **Edge TTS** — The v2 daemon depended on the web-scraper server for Edge TTS. Should v3 bundle its own Edge TTS (via `edge-tts` Python package) or rely solely on macOS `say`? **Recommendation:** Start with `say` only; add Edge TTS as an optional enhancement later.

3. **Sub-agent LLM** — Should sub-agents use the same provider chain as the main daemon, or should they default to a different provider (e.g., Claude for code tasks)? **Recommendation:** Same provider chain by default, with per-agent override capability.

4. **Context Store size limits** — Is 50 items / 50MB sufficient? For image-heavy workflows this could fill up. **Recommendation:** Start with these limits, monitor usage, adjust.

5. **Conflict with v2 daemon** — The old daemon uses the same `/tmp/jarvis-daemon/` directory. Should v3 use a different directory, or should installation explicitly uninstall v2 first? **Recommendation:** The install script should check for and offer to uninstall the old `com.xrai.jarvis-keepalive` plist. Shared memory format is backward-compatible so both daemons could theoretically coexist during migration, but only one should run at a time.

---

## 13. Implementation Plan (Phases)

### Phase 1: Core Daemon (MVP)
- [ ] `jarvis-daemon.mjs` — main entry, event loop, graceful shutdown
- [ ] `voice-listener.mjs` — sox + Whisper STT + wake word
- [ ] `config.mjs` — centralized config
- [ ] `shared-memory.mjs` — port from v2
- [ ] `activity-log.mjs` — port from v2
- [ ] `tts-speaker.mjs` — macOS `say` only
- [ ] `safety-guard.mjs` — command safety checks
- [ ] `tool-executor.mjs` — core tools (shell, files, browser, memory)
- [ ] launchd plist + install script
- [ ] Verify: daemon starts, listens, responds to "Hey Jarvis"

### Phase 2: CLI + HTTP API
- [ ] `http-api.mjs` — localhost:7437 API server
- [ ] `context-store.mjs` — context ingestion and persistence
- [ ] `cli-handler.mjs` — `jarvis` CLI commands
- [ ] `Scripts/jarvis` — CLI shell script
- [ ] Verify: `jarvis share <url>`, `jarvis ask "question"`, `jarvis status`

### Phase 3: Sub-Agents + Learning
- [ ] `sub-agent-manager.mjs` — spawn and manage sub-agents
- [ ] `command-router.mjs` — intent-based routing
- [ ] `agent-learning.mjs` — port from v2
- [ ] WebSocket support for real-time events
- [ ] Verify: sub-agent spawning, learning cycle, WebSocket events

### Phase 4: Polish + Migration
- [ ] Edge TTS support (optional)
- [ ] Browser extension documentation
- [ ] Shell tab-completion for `jarvis` CLI
- [ ] Migration script from v2 → v3
- [ ] Uninstall old `com.xrai.jarvis-keepalive` plist
- [ ] Stress testing (sustained listening, rapid CLI commands, concurrent sub-agents)

---

## 14. Summary

The jAIrvisXR Daemon Agent v3 takes the proven voice-listening core from v2 and elevates it into a proper orchestrator. The key improvements are:

1. **Standalone** — no dependency on the web-scraper server
2. **Multi-input** — voice + CLI + HTTP API + browser extensions
3. **Sub-agents** — delegates complex tasks instead of doing everything in one process
4. **Simpler process model** — launchd handles supervision directly (no keepalive/supervisor layers)
5. **Explicit safety** — centralized safety guard with permission system
6. **Zero external npm dependencies** — just dotenv, everything else is Node.js built-in

The architecture is designed to be incrementally buildable: Phase 1 (core daemon with voice) is a direct port from v2 and can be running within a day. Each subsequent phase adds capabilities without breaking what came before.
