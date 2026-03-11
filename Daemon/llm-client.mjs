/**
 * LLM Client — multi-provider AI client with auto-failover.
 * Supports: Groq (free, fastest) → Gemini (free, higher limit) → Ollama (local).
 * Ported from v2 provider chain with improvements.
 */
import { CONFIG } from "./config.mjs";
import { logActivity } from "./activity-log.mjs";
import { memoryWrite } from "./shared-memory.mjs";

let activeProvider = "groq";
let failedProviders = new Set();
let allExhaustedUntil = 0;

function log(msg) {
  const ts = new Date().toLocaleTimeString();
  console.log(`\x1b[90m[${ts}]\x1b[0m ${msg}`);
}

function isProviderReady(name) {
  switch (name) {
    case "groq": return !!CONFIG.groqApiKey;
    case "claude": return !!CONFIG.anthropicApiKey;
    case "gemini": return !!CONFIG.geminiApiKey;
    case "ollama": return true; // assume available, will fail fast if not
    default: return false;
  }
}

export function getActiveProvider() { return activeProvider; }

export function initProviders() {
  if (isProviderReady("groq")) activeProvider = "groq";
  else if (isProviderReady("claude")) activeProvider = "claude";
  else if (isProviderReady("gemini")) activeProvider = "gemini";
  else activeProvider = "ollama";
  return activeProvider;
}

function switchProvider(reason) {
  failedProviders.add(activeProvider);
  const available = ["groq", "claude", "gemini", "ollama"].filter(p => !failedProviders.has(p) && isProviderReady(p));

  if (available.length === 0) {
    allExhaustedUntil = Date.now() + 60_000;
    log("\x1b[31mAll AI providers rate-limited. Cooldown 60s.\x1b[0m");
    logActivity({ agent: "jarvis-daemon", action: "all-providers-exhausted", success: false, meta: { reason } });
    memoryWrite("jarvis-status", "all-providers-exhausted");
    setTimeout(() => {
      failedProviders.clear();
      allExhaustedUntil = 0;
      memoryWrite("jarvis-status", "online");
    }, 60_000);
    return false;
  }

  const old = activeProvider;
  activeProvider = available[0];
  log(`\x1b[33mSwitching ${old} → ${activeProvider} (${reason.slice(0, 60)})\x1b[0m`);
  logActivity({ agent: "jarvis-daemon", action: "provider-switch", success: true, meta: { from: old, to: activeProvider, reason: reason.slice(0, 100) } });
  return true;
}

/**
 * Call the active LLM provider with tool support.
 * Returns { text, toolCalls } or throws on error.
 */
async function callProvider(systemPrompt, messages, tools) {
  switch (activeProvider) {
    case "groq": return await callGroq(systemPrompt, messages, tools);
    case "claude": return await callAnthropic(systemPrompt, messages, tools);
    case "gemini": return await callGemini(systemPrompt, messages, tools);
    case "ollama": return await callOllama(systemPrompt, messages, tools);
    default: throw new Error(`Unknown provider: ${activeProvider}`);
  }
}

async function callGroq(systemPrompt, messages, tools) {
  const groqMessages = [
    { role: "system", content: systemPrompt },
    ...messages,
  ];

  const body = {
    model: "llama-3.3-70b-versatile",
    messages: groqMessages,
    temperature: 0.7,
    max_tokens: 1024,
  };

  if (tools?.length) {
    body.tools = tools.map(t => ({
      type: "function",
      function: { name: t.name, description: t.description, parameters: t.input_schema },
    }));
  }

  const res = await fetch("https://api.groq.com/openai/v1/chat/completions", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "Authorization": `Bearer ${CONFIG.groqApiKey}`,
    },
    body: JSON.stringify(body),
  });

  if (!res.ok) {
    const text = await res.text();
    throw new Error(`Groq ${res.status}: ${text.slice(0, 200)}`);
  }

  const data = await res.json();
  const choice = data.choices?.[0];
  const msg = choice?.message;

  const toolCalls = (msg?.tool_calls || []).map(tc => ({
    id: tc.id,
    name: tc.function?.name,
    input: JSON.parse(tc.function?.arguments || "{}"),
  }));

  return { text: msg?.content || "", toolCalls };
}

async function callAnthropic(systemPrompt, messages, tools) {
  const anthropicMessages = messages.map(m => ({
    role: m.role,
    content: m.content,
  }));

  const body = {
    model: CONFIG.anthropicModel,
    max_tokens: 1024,
    system: systemPrompt,
    messages: anthropicMessages,
  };

  if (tools?.length) {
    body.tools = tools.map(t => ({
      name: t.name,
      description: t.description,
      input_schema: t.input_schema,
    }));
  }

  const res = await fetch("https://api.anthropic.com/v1/messages", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "x-api-key": CONFIG.anthropicApiKey,
      "anthropic-version": "2023-06-01",
    },
    body: JSON.stringify(body),
  });

  if (!res.ok) {
    const text = await res.text();
    throw new Error(`Anthropic ${res.status}: ${text.slice(0, 200)}`);
  }

  const data = await res.json();
  let text = "";
  const toolCalls = [];

  for (const block of data.content || []) {
    if (block.type === "text") text += block.text;
    if (block.type === "tool_use") {
      toolCalls.push({
        id: block.id,
        name: block.name,
        input: block.input || {},
      });
    }
  }

  return { text, toolCalls };
}

async function callGemini(systemPrompt, messages, tools) {
  const geminiMessages = messages.map(m => ({
    role: m.role === "assistant" ? "model" : "user",
    parts: [{ text: m.content }],
  }));

  const body = {
    system_instruction: { parts: [{ text: systemPrompt }] },
    contents: geminiMessages,
    generationConfig: { temperature: 0.7, maxOutputTokens: 1024 },
  };

  if (tools?.length) {
    body.tools = [{
      function_declarations: tools.map(t => ({
        name: t.name,
        description: t.description,
        parameters: t.input_schema,
      })),
    }];
  }

  const model = "gemini-2.0-flash";
  const res = await fetch(
    `https://generativelanguage.googleapis.com/v1beta/models/${model}:generateContent?key=${CONFIG.geminiApiKey}`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    }
  );

  if (!res.ok) {
    const text = await res.text();
    throw new Error(`Gemini ${res.status}: ${text.slice(0, 200)}`);
  }

  const data = await res.json();
  const parts = data.candidates?.[0]?.content?.parts || [];
  let text = "";
  const toolCalls = [];

  for (const part of parts) {
    if (part.text) text += part.text;
    if (part.functionCall) {
      toolCalls.push({
        id: `gem-${Date.now()}`,
        name: part.functionCall.name,
        input: part.functionCall.args || {},
      });
    }
  }

  return { text, toolCalls };
}

async function callOllama(systemPrompt, messages, _tools) {
  const ollamaMessages = [
    { role: "system", content: systemPrompt },
    ...messages,
  ];

  const res = await fetch(`${CONFIG.ollamaUrl}/api/chat`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      model: CONFIG.ollamaModel,
      messages: ollamaMessages,
      stream: false,
    }),
  });

  if (!res.ok) {
    const text = await res.text();
    throw new Error(`Ollama ${res.status}: ${text.slice(0, 200)}`);
  }

  const data = await res.json();
  return { text: data.message?.content || "", toolCalls: [] };
}

/**
 * Get a response with tool-calling loop and provider failover.
 * Main entry point for conversation.
 */
export async function getResponse(text, conversationHistory, systemPrompt, tools) {
  const start = Date.now();
  conversationHistory.push({ role: "user", content: text });

  // Trim history
  if (conversationHistory.length > CONFIG.maxConversationMessages) {
    conversationHistory.splice(0, conversationHistory.length - CONFIG.maxConversationMessages);
  }

  const totalChars = conversationHistory.reduce((sum, m) => sum + (m.content?.length || 0), 0);
  if (totalChars > CONFIG.maxConversationChars) {
    conversationHistory.splice(0, conversationHistory.length - 6);
  }

  if (Date.now() < allExhaustedUntil) {
    const waitSec = Math.ceil((allExhaustedUntil - Date.now()) / 1000);
    return `All providers cooling down. Try in ${waitSec}s.`;
  }

  let finalResponse = "";
  let toolRounds = 0;
  let retries = 0;

  while (toolRounds < CONFIG.maxToolRounds) {
    let result;
    try {
      result = await callProvider(systemPrompt, conversationHistory, tools);
    } catch (err) {
      const isRateLimit = /429|rate.?limit|quota/i.test(err.message);
      if (isRateLimit) {
        const switched = switchProvider(err.message.slice(0, 80));
        if (switched) continue;
        if (retries < 2) {
          await new Promise(r => setTimeout(r, 5000 * (retries + 1)));
          retries++;
          continue;
        }
      }
      log(`\x1b[31mAI Error (${activeProvider}): ${err.message.slice(0, 100)}\x1b[0m`);
      return "I'm having trouble thinking right now.";
    }

    if (!result.toolCalls?.length) {
      finalResponse = result.text;
      break;
    }

    // Tool calls
    conversationHistory.push({
      role: "assistant",
      content: result.text || `Using: ${result.toolCalls.map(t => t.name).join(", ")}`,
    });

    const { executeTool } = await import("./tool-executor.mjs");
    for (const tc of result.toolCalls) {
      log(`\x1b[35m  Tool: ${tc.name}(${JSON.stringify(tc.input).slice(0, 60)})\x1b[0m`);
      const toolResult = executeTool(tc.name, tc.input);
      console.log(`\x1b[35m  Result:\x1b[0m ${String(toolResult).slice(0, 80).replace(/\n/g, " ")}`);
      conversationHistory.push({
        role: "user",
        content: `[Tool: ${tc.name}]: ${String(toolResult).slice(0, 500)}`,
      });
    }

    toolRounds++;
    if (result.text) finalResponse = result.text;
  }

  logActivity({
    agent: "jarvis-daemon",
    action: "conversation",
    durationMs: Date.now() - start,
    success: true,
    meta: { userText: text.slice(0, 100), toolRounds, provider: activeProvider },
  });

  conversationHistory.push({ role: "assistant", content: finalResponse });
  return finalResponse;
}
