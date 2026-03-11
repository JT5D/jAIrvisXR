/**
 * Jarvis React Native SDK — type definitions.
 */

export interface JarvisConfig {
  /** Daemon API endpoint (default: http://127.0.0.1:7437) */
  endpoint: string;
  /** Health poll interval in ms (default: 5000) */
  pollInterval?: number;
  /** Auto-connect on mount (default: true) */
  autoConnect?: boolean;
}

export interface DaemonStatus {
  status: "ok" | "error";
  version: string;
  uptime: number;
}

export interface TimingMetrics {
  routeMs?: number;
  llmMs: number;
  toolMs: number;
  totalMs: number;
}

export interface CommandResult {
  action?: string;
  response?: string;
  result?: string;
  toolsUsed?: string[];
  timing?: TimingMetrics;
  provider?: string;
}

export interface ContextItem {
  id: string;
  type: "text" | "url" | "file" | "code";
  content: string;
  source: string;
  ts: string;
  meta?: Record<string, unknown>;
}

export interface LogEntry {
  ts: string;
  agent: string;
  action: string;
  success: boolean;
  durationMs?: number;
  error?: string;
  meta?: Record<string, unknown>;
}

export interface StreamEvent {
  type: "start" | "chunk" | "done" | "error";
  text?: string;
  provider?: string;
  timing?: TimingMetrics;
  error?: string;
}

export interface JarvisContextValue {
  connected: boolean;
  status: DaemonStatus | null;
  endpoint: string;
}

export interface JarvisProviderProps {
  children: React.ReactNode;
  /** Daemon API endpoint (default: http://127.0.0.1:7437) */
  endpoint?: string;
  /** Health poll interval in ms (default: 5000) */
  pollInterval?: number;
  /** Auto-connect on mount (default: true) */
  autoConnect?: boolean;
  /** Called when connection state changes */
  onConnectionChange?: (connected: boolean) => void;
}
