/**
 * useVoice — voice-specific hook for TTS and status.
 *
 * const { speak, lastResponse } = useVoice();
 * await speak("Hello from Jarvis");
 */
import { useState, useCallback } from "react";
import { useJarvis } from "./useJarvis";

export function useVoice() {
  const { sendCommand, connected } = useJarvis();
  const [speaking, setSpeaking] = useState(false);
  const [lastResponse, setLastResponse] = useState<string | null>(null);

  const speak = useCallback(
    async (text: string): Promise<void> => {
      setSpeaking(true);
      try {
        const result = await sendCommand(`say: ${text}`);
        setLastResponse(result.response || text);
      } finally {
        setSpeaking(false);
      }
    },
    [sendCommand]
  );

  const ask = useCallback(
    async (question: string): Promise<string> => {
      const result = await sendCommand(question);
      const response = result.response || result.result || "";
      setLastResponse(response);
      return response;
    },
    [sendCommand]
  );

  return {
    speak,
    ask,
    speaking,
    connected,
    lastResponse,
  };
}
