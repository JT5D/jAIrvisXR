/**
 * Portals V4 Integration Example
 *
 * Shows how to replace the current 6-stage VoiceIntelligence pipeline
 * with Jarvis using just 1 provider and 1 hook.
 *
 * BEFORE (current Portals V4 — 6 stages, 7+ files):
 *   VoiceIntelligence.startRecording()
 *   → DeviceNativeSTT (on-device)
 *   → localIntentParser (1,011-line regex, ~1ms)
 *   → AdaptiveEngine.boost()
 *   → CloudIntentParser (Gemini fallback if confidence < 0.5)
 *   → semanticToSceneActions()
 *   → sendToUnity(JSON)
 *
 * AFTER (with Jarvis — 1 provider, 1 hook):
 *   <JarvisProvider> → useJarvis() → sendCommand(text)
 *   Jarvis daemon handles: STT, LLM (Groq/Claude/Gemini), TTS, tool execution
 */
import React, { useState, useRef, useCallback } from "react";
import { View, Text, TouchableOpacity, StyleSheet, ScrollView } from "react-native";
import { JarvisProvider, useJarvis, useVoice, useBridge } from "@jairvisxr/react-native";

// ─── Main App Wrapper ───────────────────────────────────────────────

export default function PortalsWithJarvis() {
  return (
    <JarvisProvider
      endpoint="http://localhost:7437"
      onConnectionChange={(connected) => {
        console.log(`Jarvis ${connected ? "connected" : "disconnected"}`);
      }}
    >
      <JarvisVoiceUI />
    </JarvisProvider>
  );
}

// ─── Voice UI Component ─────────────────────────────────────────────

function JarvisVoiceUI() {
  const { connected, sendCommand, status } = useJarvis();
  const { speak, ask, lastResponse } = useVoice();
  const [log, setLog] = useState<string[]>([]);

  // Unity bridge ref (in real Portals V4, this comes from useComposerBridge)
  const sendToUnity = useCallback((type: string, payload: Record<string, unknown>) => {
    const msg = JSON.stringify({ type, ...payload });
    setLog(prev => [...prev, `→ Unity: ${msg}`]);
    // In Portals V4: unityRef.current?.sendMessage(msg)
  }, []);

  const { executeVoiceCommand } = useBridge(sendToUnity);

  const handleVoiceCommand = async (text: string) => {
    setLog(prev => [...prev, `You: ${text}`]);
    try {
      const result = await executeVoiceCommand(text);
      setLog(prev => [...prev, `Jarvis: ${result.response}`]);
      if (result.actions.length > 0) {
        setLog(prev => [...prev, `Actions: ${result.actions.map(a => a.type).join(", ")}`]);
      }
    } catch (err) {
      setLog(prev => [...prev, `Error: ${(err as Error).message}`]);
    }
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Jarvis + Portals V4</Text>
      <Text style={styles.status}>
        {connected ? "● Connected" : "○ Offline"}
        {status ? ` (v${status.version}, up ${Math.round(status.uptime)}s)` : ""}
      </Text>

      {/* Quick action buttons */}
      <View style={styles.buttons}>
        <TouchableOpacity
          style={styles.btn}
          onPress={() => handleVoiceCommand("add a red cube to the scene")}
        >
          <Text style={styles.btnText}>Add Red Cube</Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={styles.btn}
          onPress={() => handleVoiceCommand("create fireworks")}
        >
          <Text style={styles.btnText}>Fireworks</Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={styles.btn}
          onPress={() => speak("Hello, I am Jarvis, your spatial intelligence agent.")}
        >
          <Text style={styles.btnText}>Speak</Text>
        </TouchableOpacity>
      </View>

      {/* Log output */}
      <ScrollView style={styles.log}>
        {log.map((entry, i) => (
          <Text key={i} style={styles.logEntry}>{entry}</Text>
        ))}
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, padding: 20, backgroundColor: "#0a0a0a" },
  title: { fontSize: 24, fontWeight: "bold", color: "#00d4ff", marginBottom: 8 },
  status: { fontSize: 14, color: "#888", marginBottom: 20 },
  buttons: { flexDirection: "row", gap: 10, marginBottom: 20, flexWrap: "wrap" },
  btn: { backgroundColor: "#1a1a2e", padding: 12, borderRadius: 8, borderWidth: 1, borderColor: "#333" },
  btnText: { color: "#00d4ff", fontSize: 14, fontWeight: "600" },
  log: { flex: 1, backgroundColor: "#111", borderRadius: 8, padding: 12 },
  logEntry: { color: "#ccc", fontSize: 12, fontFamily: "monospace", marginBottom: 4 },
});
