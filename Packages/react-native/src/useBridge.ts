/**
 * useBridge — Unity bridge adapter for Portals V4 integration.
 *
 * Bridges Jarvis LLM responses into the existing SemanticAction format
 * that the Unity composer understands.
 *
 * Replaces the 6-stage VoiceIntelligence pipeline with a single hook:
 *   const { executeVoiceCommand } = useBridge(sendToUnity);
 */
import { useCallback } from "react";
import { useJarvis } from "./useJarvis";
import type { TimingMetrics } from "./types";

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

export function useBridge(
  sendToUnity?: (type: string, payload: Record<string, unknown>) => void
) {
  const { sendCommand } = useJarvis();

  const executeVoiceCommand = useCallback(
    async (transcript: string): Promise<BridgeResult> => {
      const requestId = `req-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`;
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

  return { executeVoiceCommand };
}
