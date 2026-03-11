/**
 * useStream — streaming hook for real-time LLM responses via SSE.
 *
 * const { streamCommand, streaming, chunks, fullText } = useStream();
 * await streamCommand("Tell me about spatial computing");
 * // chunks update in real-time as tokens arrive
 */
import { useState, useCallback, useRef, useContext, useMemo } from "react";
import { JarvisContext } from "./JarvisProvider";
import { JarvisClient } from "./client";
import type { StreamEvent, TimingMetrics } from "./types";

export interface StreamState {
  /** Whether a stream is currently active */
  streaming: boolean;
  /** Accumulated text chunks (grows as tokens arrive) */
  fullText: string;
  /** The active provider for this stream */
  provider: string | null;
  /** Timing metrics (available after stream completes) */
  timing: TimingMetrics | null;
  /** Error message if stream failed */
  error: string | null;
}

export function useStream() {
  const ctx = useContext(JarvisContext);
  if (!ctx) {
    throw new Error("useStream must be used inside <JarvisProvider>");
  }

  const client = useMemo(() => new JarvisClient(ctx.endpoint), [ctx.endpoint]);

  const [streaming, setStreaming] = useState(false);
  const [fullText, setFullText] = useState("");
  const [provider, setProvider] = useState<string | null>(null);
  const [timing, setTiming] = useState<TimingMetrics | null>(null);
  const [error, setError] = useState<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const streamCommand = useCallback(
    async (
      command: string,
      onChunk?: (chunk: string, accumulated: string) => void
    ): Promise<string> => {
      // Reset state
      setStreaming(true);
      setFullText("");
      setProvider(null);
      setTiming(null);
      setError(null);

      let accumulated = "";
      abortRef.current = new AbortController();

      try {
        const result = await client.streamCommand(
          command,
          (event: StreamEvent) => {
            switch (event.type) {
              case "start":
                setProvider(event.provider || null);
                break;
              case "chunk":
                if (event.text) {
                  accumulated += event.text;
                  setFullText(accumulated);
                  onChunk?.(event.text, accumulated);
                }
                break;
              case "done":
                setTiming(event.timing || null);
                break;
              case "error":
                setError(event.error || "Stream error");
                break;
            }
          },
          abortRef.current.signal
        );

        setStreaming(false);
        return result;
      } catch (err) {
        const msg = (err as Error).message;
        setError(msg);
        setStreaming(false);
        throw err;
      }
    },
    [client]
  );

  const cancel = useCallback(() => {
    abortRef.current?.abort();
    setStreaming(false);
  }, []);

  return {
    streamCommand,
    cancel,
    streaming,
    fullText,
    provider,
    timing,
    error,
    connected: ctx.connected,
  };
}
