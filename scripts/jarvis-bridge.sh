#!/bin/bash
# jarvis-bridge.sh — CLI to interact with the Jarvis daemon.
# In v3 mode, commands go via HTTP API. Falls back to direct file access.
#
# Usage:
#   jarvis share <url|file|text>   Share context with Jarvis
#   jarvis ask "<question>"        Ask Jarvis a question
#   jarvis task <description>      Queue a task for Claude Code
#   jarvis status                  Show daemon status
#   jarvis logs [--tail]           Show activity log
#   jarvis context [clear]         Show/clear context store
#   jarvis permit <perm>           Grant a session permission (e.g., push)
#   jarvis stop                    Stop the daemon
#   jarvis restart                 Restart the daemon

JARVIS_PORT="${JARVIS_PORT:-7437}"
JARVIS_URL="http://127.0.0.1:${JARVIS_PORT}"
MEM="/tmp/jarvis-daemon/shared-memory.json"
LOG="/tmp/jarvis-daemon/activity-log.jsonl"

# Check if v3 HTTP API is running
api_up() {
  curl -sf "${JARVIS_URL}/api/health" > /dev/null 2>&1
}

case "${1}" in
  share)
    shift
    INPUT="$*"
    if [[ -f "$INPUT" ]]; then
      TYPE="file"
    elif [[ "$INPUT" =~ ^https?:// ]]; then
      TYPE="url"
    else
      TYPE="text"
    fi
    if api_up; then
      curl -s -X POST "${JARVIS_URL}/api/share" \
        -H "Content-Type: application/json" \
        -d "{\"type\":\"$TYPE\",\"content\":\"$INPUT\"}" | python3 -m json.tool 2>/dev/null
    else
      # Fallback: direct shared memory write
      python3 -c "
import json, os, time
with open('${MEM}') as f: mem = json.load(f)
mem.setdefault('_context', []).append({
  'type': '${TYPE}', 'content': '''${INPUT}''',
  'from': 'user-cli',
  'timestamp': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())
})
mem['_lastUpdated'] = time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())
tmp = '${MEM}.tmp'
with open(tmp, 'w') as f: json.dump(mem, f, indent=2)
os.rename(tmp, '${MEM}')
print('Shared with agents (direct).')
"
    fi
    ;;
  ask)
    shift
    INPUT="$*"
    if api_up; then
      curl -s -X POST "${JARVIS_URL}/api/command" \
        -H "Content-Type: application/json" \
        -d "{\"command\":\"$INPUT\"}" | python3 -m json.tool 2>/dev/null
    else
      echo "Daemon API not running. Start with: node jAIrvisXR/Daemon/jarvis-daemon.mjs"
    fi
    ;;
  task)
    shift
    INPUT="$*"
    python3 << PYEOF
import json, os, time
with open('${MEM}') as f: mem = json.load(f)
mem.setdefault('_taskQueue', []).append({
  'id': str(int(time.time()*1000)),
  'description': """${INPUT}""",
  'status': 'pending',
  'assignedTo': 'claude-code',
  'createdAt': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()),
  'createdBy': 'user-cli'
})
mem['_lastUpdated'] = time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())
tmp = '${MEM}.tmp'
with open(tmp, 'w') as f: json.dump(mem, f, indent=2)
os.rename(tmp, '${MEM}')
print('Task queued for Claude Code.')
PYEOF
    ;;
  status)
    if api_up; then
      echo "=== Jarvis v3 (HTTP API) ==="
      curl -s "${JARVIS_URL}/api/status" | python3 -c "
import sys, json
sm = json.load(sys.stdin)
agents = sm.get('_agents', {})
for name, info in agents.items():
    print(f'  {name}: {info.get(\"status\",\"?\")} ({info.get(\"type\",\"?\")}) PID={info.get(\"pid\",\"?\")}')
tasks = sm.get('_taskQueue', [])
pending = [t for t in tasks if t.get('status') == 'pending']
print(f'\nTasks: {len(tasks)} total, {len(pending)} pending')
" 2>/dev/null
    else
      echo "=== Jarvis (direct memory) ==="
      python3 -c "
import json
with open('${MEM}') as f: sm = json.load(f)
agents = sm.get('_agents', {})
for name, info in agents.items():
    print(f'  {name}: {info.get(\"status\",\"?\")} ({info.get(\"type\",\"?\")}) PID={info.get(\"pid\",\"?\")}')
" 2>/dev/null
    fi
    ;;
  logs)
    if [[ "$2" == "--tail" ]]; then
      tail -f "${LOG}"
    else
      if api_up; then
        curl -s "${JARVIS_URL}/api/logs?n=20" | python3 -c "
import sys, json
for e in json.load(sys.stdin):
    ts = e.get('ts','')[-12:]
    agent = e.get('agent','')
    action = e.get('action','')
    meta = e.get('meta',{})
    detail = meta.get('userText','') or meta.get('reason','') or ''
    print(f'{ts} [{agent}] {action} {detail[:80]}')
" 2>/dev/null
      else
        tail -20 "${LOG}" | python3 -c "
import sys, json
for line in sys.stdin:
    try:
        e = json.loads(line.strip())
        ts = e.get('ts','')[-12:]
        print(f'{ts} [{e.get(\"agent\",\"\")}] {e.get(\"action\",\"\")}')
    except: pass
"
      fi
    fi
    ;;
  context)
    if [[ "$2" == "clear" ]]; then
      if api_up; then
        curl -s -X DELETE "${JARVIS_URL}/api/context" | python3 -m json.tool 2>/dev/null
      else
        echo "Daemon API not running."
      fi
    else
      if api_up; then
        curl -s "${JARVIS_URL}/api/context" | python3 -m json.tool 2>/dev/null
      else
        echo "Daemon API not running."
      fi
    fi
    ;;
  permit)
    if api_up; then
      curl -s -X POST "${JARVIS_URL}/api/command" \
        -H "Content-Type: application/json" \
        -d "{\"command\":\"grant-permission:$2\"}" | python3 -m json.tool 2>/dev/null
    else
      echo "Daemon API not running."
    fi
    ;;
  stop)
    if api_up; then
      curl -s -X POST "${JARVIS_URL}/api/command" \
        -H "Content-Type: application/json" \
        -d '{"command":"shutdown"}' | python3 -m json.tool 2>/dev/null
    else
      echo "Daemon not running."
    fi
    ;;
  restart)
    if api_up; then
      curl -s -X POST "${JARVIS_URL}/api/command" \
        -H "Content-Type: application/json" \
        -d '{"command":"restart"}' | python3 -m json.tool 2>/dev/null
    else
      echo "Daemon not running."
    fi
    ;;
  *)
    echo "Jarvis CLI — talk to the daemon"
    echo ""
    echo "Usage:"
    echo "  jarvis share <url|file|text>   Share context"
    echo "  jarvis ask \"<question>\"        Ask Jarvis"
    echo "  jarvis task <description>      Queue task for Claude Code"
    echo "  jarvis status                  Show agent status"
    echo "  jarvis logs [--tail]           Show activity log"
    echo "  jarvis context [clear]         Context store"
    echo "  jarvis permit push             Grant git push permission"
    echo "  jarvis stop                    Stop daemon"
    echo "  jarvis restart                 Restart daemon"
    ;;
esac
