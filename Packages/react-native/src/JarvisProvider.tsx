/**
 * JarvisProvider — React Context provider for daemon connection.
 *
 * Wrap your app to give all children access to useJarvis() and useVoice().
 *
 * <JarvisProvider endpoint="http://localhost:7437">
 *   <App />
 * </JarvisProvider>
 */
import React, { createContext, useState, useEffect, useRef } from "react";
import type { JarvisContextValue, JarvisProviderProps, DaemonStatus } from "./types";
import { JarvisClient } from "./client";

export const JarvisContext = createContext<JarvisContextValue | null>(null);

export function JarvisProvider({
  children,
  endpoint = "http://127.0.0.1:7437",
  pollInterval = 5000,
  autoConnect = true,
  onConnectionChange,
}: JarvisProviderProps) {
  const [connected, setConnected] = useState(false);
  const [status, setStatus] = useState<DaemonStatus | null>(null);
  const clientRef = useRef(new JarvisClient(endpoint));
  const prevConnected = useRef(false);

  useEffect(() => {
    clientRef.current = new JarvisClient(endpoint);
  }, [endpoint]);

  useEffect(() => {
    if (!autoConnect) return;

    const checkHealth = async () => {
      try {
        const data = await clientRef.current.health();
        setConnected(true);
        setStatus(data);
      } catch {
        setConnected(false);
        setStatus(null);
      }
    };

    checkHealth();
    const interval = setInterval(checkHealth, pollInterval);
    return () => clearInterval(interval);
  }, [autoConnect, pollInterval, endpoint]);

  useEffect(() => {
    if (prevConnected.current !== connected) {
      prevConnected.current = connected;
      onConnectionChange?.(connected);
    }
  }, [connected, onConnectionChange]);

  return (
    <JarvisContext.Provider value={{ connected, status, endpoint }}>
      {children}
    </JarvisContext.Provider>
  );
}
