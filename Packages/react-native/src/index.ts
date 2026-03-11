/**
 * @jairvisxr/react-native — Jarvis voice AI agent SDK for React Native.
 *
 * Usage:
 *   import { JarvisProvider, useJarvis, useVoice } from '@jairvisxr/react-native';
 *
 *   <JarvisProvider endpoint="http://localhost:7437">
 *     <App />
 *   </JarvisProvider>
 */

// Provider
export { JarvisProvider } from "./JarvisProvider";
export { JarvisContext } from "./JarvisProvider";

// Hooks
export { useJarvis } from "./useJarvis";
export { useVoice } from "./useVoice";
export { useBridge } from "./useBridge";
export { useStream } from "./useStream";

// Client (for direct API access without React)
export { JarvisClient } from "./client";

// Types
export type {
  JarvisConfig,
  JarvisProviderProps,
  JarvisContextValue,
  DaemonStatus,
  CommandResult,
  ContextItem,
  LogEntry,
  TimingMetrics,
  StreamEvent,
} from "./types";

export type { StreamState } from "./useStream";

export type {
  SemanticAction,
  SemanticActionType,
  BridgeResult,
} from "./useBridge";
