/**
 * Centralized configuration for Jarvis Daemon v3.
 * Environment variables override defaults.
 */
import "dotenv/config";

export const CONFIG = {
  // Directories
  tmpDir: "/tmp/jarvis-daemon",
  memFile: "/tmp/jarvis-daemon/shared-memory.json",
  logFile: "/tmp/jarvis-daemon/activity-log.jsonl",

  // Voice
  wakeWords: [
    "jarvis", "hey jarvis", "ok jarvis", "yo jarvis",
    "jarves", "jarvus", "jarvas", "jervis", "jarv", "javis", "jarfis",
    "hey jarves", "hey jarvus", "hey jervis", "hey javis",
  ],
  recordSeconds: 5,
  activeRecordSeconds: 15,
  silenceThreshold: "1.5%",
  activeSilenceSecs: "3.0",
  passiveSilenceSecs: "1.5",
  silenceRoundsBeforePassive: 3,

  // AI providers
  groqApiKey: process.env.GROQ_API_KEY || "",
  geminiApiKey: process.env.GEMINI_API_KEY || "",
  ollamaUrl: process.env.OLLAMA_URL || "http://localhost:11434",

  // STT
  whisperModel: "base",
  groqWhisperModel: "whisper-large-v3",

  // TTS
  ttsVoice: "en-US-GuyNeural",
  macVoice: "Samantha",

  // HTTP API
  httpPort: parseInt(process.env.JARVIS_PORT || "7437"),
  httpHost: "127.0.0.1",

  // Limits
  maxToolRounds: 5,
  maxConversationMessages: 10,
  maxConversationChars: 8000,
  maxSubAgents: 3,
  subAgentTimeout: 60_000,
  contextMaxItems: 50,
  contextMaxAge: 24 * 60 * 60 * 1000, // 24h

  // System prompt
  systemPrompt: `You are Jarvis, an intelligent spatial navigation assistant running as a native macOS daemon.
You are always listening. You have a warm, intelligent personality — helpful but not servile.

YOU HAVE TOOLS. You are NOT just a chatbot. You can:
- Open browser windows (open_browser)
- Run shell commands (run_shell) — git, npm, ls, grep, etc.
- Read and write files (read_file, write_file)
- List directories (list_directory)
- Search codebases (search_project)
- Read/write shared memory (read_memory, write_memory)
- Read activity logs (read_activity_log)
- Record learnings/patterns (record_lesson)

When the user asks you to DO something, USE YOUR TOOLS. Don't just say you'll do it.

AGENT COORDINATION:
- You work alongside "Claude Code" (another AI agent the user runs in their terminal).
- You share memory via read_memory/write_memory. Check it regularly.
- Log important findings to memory so Claude Code can use them.
- Be TOKEN EFFICIENT — keep responses short.

CRITICAL RULES:
- NEVER respond to incomplete thoughts. Say "Go on." if cut off.
- Keep responses SHORT. 1-2 sentences unless asked for detail.
- When using tools, briefly say what you're doing, then report concisely.
- NEVER run git push without explicit user permission.`,

  // Known projects
  projects: {
    "jAIrvisXR": "/Users/jamestunick/Applications/jAIrvisXR",
    "xrai-spatial-web": "/Users/jamestunick/Applications/web-scraper",
    "portals-v4": "/Users/jamestunick/dev/portals_v4_fresh",
  },
};
