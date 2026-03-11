/**
 * useBridge — Unity bridge adapter for Portals V4 integration.
 *
 * Bridges Jarvis LLM responses into the existing SemanticAction format
 * that the Unity composer understands.
 *
 * Two modes:
 *   executeVoiceCommand(text)  — single request/response (fast-path <2ms)
 *   streamVoiceCommand(text)   — SSE streaming, dispatches actions mid-stream
 */
import { useCallback, useRef, useContext, useMemo } from "react";
import { useJarvis } from "./useJarvis";
import { JarvisContext } from "./JarvisProvider";
import { JarvisClient } from "./client";
import type { TimingMetrics, StreamEvent } from "./types";

/** Portals V4 SemanticAction types (subset for bridge compatibility) */
export type SemanticActionType =
  | "ADD_OBJECT"
  | "REMOVE_OBJECT"
  | "MODIFY_OBJECTS"
  | "SET_ANIMATION"
  | "ADD_EMITTER"
  | "ADD_COMPONENT"
  | "SET_PROPERTY"
  | "TRANSFORM_OBJECT"
  | "CLEAR_SCENE"
  | "ARRANGE_FORMATION"
  | "SET_VIBE"
  | "CHANGE_LIGHTING"
  | "ADD_WIRE"
  | "UNDO"
  | "REDO";

export interface SemanticAction {
  type: SemanticActionType;
  params: Record<string, unknown>;
  confidence: number;
}

export interface BridgeResult {
  transcript: string;
  response: string;
  actions: SemanticAction[];
  provider: string;
  requestId: string;
  timing?: TimingMetrics;
  /** Whether the result came from streaming */
  streamed?: boolean;
}

export interface StreamBridgeCallbacks {
  /** Called when the stream starts with the active provider */
  onStreamStart?: (provider: string) => void;
  /** Called for each text chunk as tokens arrive */
  onChunk?: (chunk: string, accumulated: string) => void;
  /** Called when scene actions are detected and dispatched to Unity */
  onActions?: (actions: SemanticAction[], requestId: string) => void;
  /** Called when the stream completes */
  onComplete?: (result: BridgeResult) => void;
  /** Called on error */
  onError?: (error: string) => void;
}

/**
 * Parse a Jarvis LLM response into SemanticActions.
 * The daemon's LLM can return structured JSON actions or natural language.
 * This parser handles both cases.
 */
function parseResponseToActions(response: string): SemanticAction[] {
  // Try parsing as JSON (daemon may return structured actions)
  try {
    const parsed = JSON.parse(response);
    if (Array.isArray(parsed)) {
      return parsed.filter(a => a.type && a.params);
    }
    if (parsed.actions && Array.isArray(parsed.actions)) {
      return parsed.actions;
    }
  } catch {
    // Not JSON — natural language response, no scene actions
  }
  return [];
}

/**
 * Try to extract complete JSON from an accumulating stream buffer.
 * Returns parsed actions if the buffer contains a complete JSON object/array,
 * or null if the JSON is still incomplete.
 */
function tryParsePartialActions(buffer: string): SemanticAction[] | null {
  const trimmed = buffer.trim();
  // Only attempt if it looks like JSON
  if (!trimmed.startsWith("{") && !trimmed.startsWith("[")) return null;

  try {
    const parsed = JSON.parse(trimmed);
    if (Array.isArray(parsed)) {
      const valid = parsed.filter(a => a.type && a.params);
      return valid.length > 0 ? valid : null;
    }
    if (parsed.actions && Array.isArray(parsed.actions)) {
      const valid = parsed.actions.filter((a: any) => a.type && a.params);
      return valid.length > 0 ? valid : null;
    }
  } catch {
    // JSON not yet complete — expected during streaming
  }
  return null;
}

function makeRequestId(): string {
  return `req-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`;
}

export function useBridge(
  sendToUnity?: (type: string, payload: Record<string, unknown>) => void
) {
  const { sendCommand } = useJarvis();
  const ctx = useContext(JarvisContext);
  const client = useMemo(
    () => (ctx ? new JarvisClient(ctx.endpoint) : null),
    [ctx?.endpoint]
  );
  const abortRef = useRef<AbortController | null>(null);

  /**
   * Non-streaming command — fast-path scene commands return in <2ms.
   * Use this for simple scene commands where latency is already minimal.
   */
  const executeVoiceCommand = useCallback(
    async (transcript: string): Promise<BridgeResult> => {
      const requestId = makeRequestId();
      const result = await sendCommand(transcript);
      const response = result.response || result.result || "";
      const actions = parseResponseToActions(response);

      // If we have scene actions and a Unity bridge, execute them with correlation ID
      if (sendToUnity && actions.length > 0) {
        for (const action of actions) {
          sendToUnity(action.type, { ...action.params, _requestId: requestId });
        }
      }

      return {
        transcript,
        response,
        actions,
        provider: result.provider || "jarvis-daemon",
        requestId,
        timing: result.timing,
      };
    },
    [sendCommand, sendToUnity]
  );

  /**
   * Streaming command via SSE — dispatches scene actions to Unity as soon
   * as the JSON is complete, while text continues streaming for TTS overlap.
   *
   * Flow:
   *   1. SSE stream opens → onStreamStart(provider)
   *   2. Chunks arrive → onChunk(token, accumulated)
   *   3. JSON detected → parse actions → sendToUnity → onActions(actions)
   *   4. Stream ends → onComplete(fullResult)
   */
  const streamVoiceCommand = useCallback(
    async (
      transcript: string,
      callbacks?: StreamBridgeCallbacks
    ): Promise<BridgeResult> => {
      if (!client) {
        throw new Error("useBridge: JarvisProvider not found. Wrap your app in <JarvisProvider>.");
      }

      const requestId = makeRequestId();
      let accumulated = "";
      let provider = "jarvis-daemon";
      let timing: TimingMetrics | undefined;
      let actionsDispatched = false;
      let actions: SemanticAction[] = [];

      // Cancel any in-flight stream
      abortRef.current?.abort();
      abortRef.current = new AbortController();

      try {
        await client.streamCommand(
          transcript,
          (event: StreamEvent) => {
            switch (event.type) {
              case "start":
                provider = event.provider || "jarvis-daemon";
                callbacks?.onStreamStart?.(provider);
                break;

              case "chunk":
                if (event.text) {
                  accumulated += event.text;
                  callbacks?.onChunk?.(event.text, accumulated);

                  // Try to parse actions from accumulated text mid-stream.
                  // Once successfully parsed, dispatch to Unity immediately.
                  if (!actionsDispatched) {
                    const parsed = tryParsePartialActions(accumulated);
                    if (parsed && parsed.length > 0) {
                      actions = parsed;
                      actionsDispatched = true;

                      if (sendToUnity) {
                        for (const action of actions) {
                          sendToUnity(action.type, {
                            ...action.params,
                            _requestId: requestId,
                          });
                        }
                      }

                      callbacks?.onActions?.(actions, requestId);
                    }
                  }
                }
                break;

              case "done":
                timing = event.timing;
                break;

              case "error":
                callbacks?.onError?.(event.error || "Stream error");
                break;
            }
          },
          abortRef.current.signal
        );

        // Final parse attempt if actions weren't caught mid-stream
        if (!actionsDispatched) {
          actions = parseResponseToActions(accumulated);
          if (sendToUnity && actions.length > 0) {
            for (const action of actions) {
              sendToUnity(action.type, {
                ...action.params,
                _requestId: requestId,
              });
            }
          }
          if (actions.length > 0) {
            callbacks?.onActions?.(actions, requestId);
          }
        }

        const result: BridgeResult = {
          transcript,
          response: accumulated,
          actions,
          provider,
          requestId,
          timing,
          streamed: true,
        };

        callbacks?.onComplete?.(result);
        return result;
      } catch (err) {
        if ((err as Error).name === "AbortError") {
          return {
            transcript,
            response: accumulated,
            actions,
            provider,
            requestId,
            timing,
            streamed: true,
          };
        }
        const msg = (err as Error).message;
        callbacks?.onError?.(msg);
        throw err;
      }
    },
    [client, sendToUnity]
  );

  /** Cancel an in-flight streaming command. */
  const cancelStream = useCallback(() => {
    abortRef.current?.abort();
  }, []);

  return {
    executeVoiceCommand,
    streamVoiceCommand,
    cancelStream,
  };
}
