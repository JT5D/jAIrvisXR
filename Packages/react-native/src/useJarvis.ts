/**
 * useJarvis — primary hook for interacting with the Jarvis daemon.
 *
 * const { connected, sendCommand, shareContext, status } = useJarvis();
 */
import { useContext, useCallback, useMemo } from "react";
import { JarvisContext } from "./JarvisProvider";
import { JarvisClient } from "./client";
import type { CommandResult, ContextItem, LogEntry } from "./types";

export function useJarvis() {
  const ctx = useContext(JarvisContext);
  if (!ctx) {
    throw new Error("useJarvis must be used inside <JarvisProvider>");
  }

  const client = useMemo(() => new JarvisClient(ctx.endpoint), [ctx.endpoint]);

  const sendCommand = useCallback(
    async (command: string): Promise<CommandResult> => {
      return client.sendCommand(command);
    },
    [client]
  );

  const shareContext = useCallback(
    async (type: string, content: string, meta?: Record<string, unknown>): Promise<void> => {
      await client.shareContext(type, content, meta);
    },
    [client]
  );

  const getContext = useCallback(
    async (): Promise<ContextItem[]> => {
      return client.getContext();
    },
    [client]
  );

  const clearContext = useCallback(
    async (): Promise<void> => {
      await client.clearContext();
    },
    [client]
  );

  const getLogs = useCallback(
    async (count = 20): Promise<LogEntry[]> => {
      return client.getLogs(count);
    },
    [client]
  );

  return {
    connected: ctx.connected,
    status: ctx.status,
    sendCommand,
    shareContext,
    getContext,
    clearContext,
    getLogs,
  };
}
