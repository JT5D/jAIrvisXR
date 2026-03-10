# jAIrvisXR Technical Roadmap

> Last updated: 2026-03-10

## Current State (MVP)

### What Works
- **29 C# scripts** compiling clean in Unity 6 (6000.3.10f1)
- **Voice pipeline**: Mic → STT → Claude Agent → TTS → AudioSource playback
- **Real providers**: Groq Whisper STT, ElevenLabs TTS (PCM s16le direct)
- **Mock providers**: Testable pipeline without API keys
- **Auto-wiring**: VoicePipeline.FindProvider<T>() prefers real over mock
- **XR scene setup**: Editor script generates full OpenXR rig (XR Origin, controllers, hand tracking, locomotion)
- **Daemon v3**: Always-on macOS service — voice listener, CLI, HTTP API (localhost:7437), multi-agent tool calling
- **LLM failover**: Groq → Gemini → Ollama (configurable model)
- **TTS chain**: edge-tts (neural) → macOS say (fallback)
- **Wake word**: Fuzzy matching (15 variants: jarvis, jervis, jarves, etc.)
- **Dashboard**: Live web UI at localhost:7438

### Git History
```
3744b7e  Fix Unity 6 compile and add project metadata
373ba87  Wire real providers into Unity pipeline and fix ElevenLabs endpoint
1042d2d  Add real STT/TTS providers and upgrade daemon voice output
564d918  Add daemon v3, XR scene setup, and live dashboard
3b846cc  Add Git LFS tracking for large binary assets
43ec24e  Add architecture design docs for daemon agent and XR scene
```

### File Count
- **Unity C#**: 29 scripts (~2,400 LOC)
- **Daemon JS**: 11 modules (~1,300 LOC)
- **Total**: ~3,700 LOC

---

## Fork Strategy

After MVP verification, development branches into parallel forks that merge back to master once stable.

```
master (voice pipeline MVP)
  ├── fork/livekit-multiplayer   ← multiplayer audio/video rooms
  ├── fork/vrm-avatars           ← VRM 1.0 avatar system
  └── fork/webrtc-p2p            ← lightweight 2-person P2P fallback
```

---

## Fork 1: LiveKit Multiplayer

### Why LiveKit over raw WebRTC
- SFU architecture scales to 3,000 per room (vs 2-4 P2P)
- Built-in AI Agents framework (v1.0, production-ready)
- 10,000 free participant-minutes/month on cloud tier
- Self-hostable (open source, Docker/K8s)
- $45M Series B — not going anywhere

### Quest Compatibility
- Android SDK exists and works; Quest-specific validation needed
- Unity SDK v1.2.6 (native), v2.0.0 (WebGL)
- Install via UPM git URL (requires Git LFS)

### Implementation Plan
1. **Room service**: Create `LiveKitRoomManager.cs` — connect/disconnect, participant tracking
2. **Audio integration**: Route VoicePipeline audio through LiveKit tracks instead of local-only AudioSource
3. **Video (optional)**: Camera texture publishing for avatar-less video chat
4. **AI agent backend**: Python/Node server running LiveKit Agent that joins rooms as a participant
5. **Server**: Docker Compose self-hosted for dev, LiveKit Cloud for production

### Key Files to Create
```
Assets/jAIrvisXR/Scripts/Networking/
  ├── IMultiplayerProvider.cs        (interface)
  ├── LiveKitRoomManager.cs          (room lifecycle)
  ├── LiveKitAudioBridge.cs          (pipeline ↔ LiveKit audio)
  └── LiveKitParticipantView.cs      (UI for remote participants)
Server/
  ├── docker-compose.yml             (LiveKit SFU)
  └── agent/                         (AI agent backend)
```

### Risk
- Quest support is unproven — test on hardware early
- If Quest fails: fall back to WebRTC P2P fork for 2-person demo

---

## Fork 2: VRM Avatars

### Why VRM 1.0
- Khronos Group collaboration → ISO standardization path
- glTF 2.0 based — universal, not proprietary
- UniVRM v0.131+ production-ready
- VRoid Hub API for user avatar marketplace
- Standard blendshapes (Joy, Sorrow, Anger, A/I/U/E/O lip sync)

### Quest Performance Budget
| Parameter | Target |
|-----------|--------|
| Triangles | < 15,000 |
| Textures | 1024×1024 max |
| Materials | 1 (max 2) |
| Shader | Mobile-optimized |
| Spring bones | Use Vrm10FastSpringboneRuntime (Job System + Burst) |

### Implementation Plan
1. **Avatar loader**: Async runtime VRM loading from file/URL via UniVRM
2. **Humanoid mapping**: Map VRM skeleton to XR Hands + IK system
3. **Lip sync**: Drive VRM blendshapes (A/I/U/E/O) from TTS phoneme data
4. **Expressions**: Map pipeline state to facial expressions (Idle→neutral, Listening→attentive, Speaking→Joy)
5. **VRoid Hub**: OAuth 2.0 integration for avatar selection UI
6. **Music reactivity**: Beat detection → body motion anchors (Vmotionize or DIY)

### Key Files to Create
```
Assets/jAIrvisXR/Scripts/AI/Avatar/
  ├── IAvatarProvider.cs             (interface)
  ├── VrmAvatarLoader.cs             (UniVRM async loading)
  ├── VrmLipSync.cs                  (phoneme → blendshape)
  ├── VrmExpressionDriver.cs         (pipeline state → expressions)
  └── VrmHandTrackingMapper.cs       (XR Hands → VRM finger bones)
Assets/jAIrvisXR/Scripts/AI/Avatar/Music/
  ├── BeatDetector.cs                (audio frequency analysis)
  └── MusicMotionDriver.cs           (beat → skeletal animation)
```

### Dependencies
- UniVRM v0.131+ (UPM git or .unitypackage)
- Unity XR Hands v1.5+
- VRoid Hub developer credentials (OAuth)

---

## Fork 3: WebRTC P2P

### Purpose
Lightweight 2-person fallback if LiveKit Quest support fails, or for minimal-latency direct calls.

### Constraints
- P2P only — 2-4 participants max
- Quest requires OpenGLES3 (Vulkan breaks video streaming)
- Must disable: Low Overhead Mode, Meta Quest Occlusion, Subsampled Layout

### Implementation Plan
1. **Signaling server**: Minimal Node.js WebSocket server for offer/answer exchange
2. **Peer connection**: `WebRTCPeerManager.cs` using `com.unity.webrtc` v3.0
3. **Audio bridge**: Route pipeline audio through RTCPeerConnection tracks
4. **Video (optional)**: WebCamTexture → video track at VP8 30fps

### Key Files to Create
```
Assets/jAIrvisXR/Scripts/Networking/
  ├── WebRTCPeerManager.cs           (P2P connection lifecycle)
  └── WebRTCSignalingClient.cs       (WebSocket signaling)
Server/
  └── signaling-server.mjs           (minimal WebSocket relay)
```

---

## Priority Order

| Priority | Fork | Rationale |
|----------|------|-----------|
| **P0** | LiveKit Multiplayer | Core differentiator; enables AI agent rooms, scales to events |
| **P1** | VRM Avatars | Visual identity; enables performer tournaments, VRoid Hub marketplace |
| **P2** | WebRTC P2P | Fallback only; start if LiveKit Quest validation fails |

### Parallel Tracks (Not Forks)

| Track | Description | When |
|-------|-------------|------|
| Vision Pipeline | Camera passthrough → Claude vision API → spatial understanding | After P0 prototype |
| Persistent Memory | Cross-session context store, user preference learning | After P1 prototype |
| Spatial AI | Anchor-aware responses, room scanning, object recognition | After Vision Pipeline |

---

## Platform Targets

| Platform | Status | Notes |
|----------|--------|-------|
| Meta Quest 3 | Primary | OpenXR, hand tracking, passthrough |
| Meta Quest 2 | Secondary | OpenXR, controllers only (no IOBT) |
| SteamVR (Vive/Index) | Secondary | OpenXR, full body tracking via external |
| Desktop (editor) | Dev/test | Keyboard/mouse fallback |
| Apple Vision Pro | Deferred | Not OpenXR; requires separate build pipeline |

---

## API Keys Required

| Service | Env Var | Purpose | Free Tier |
|---------|---------|---------|-----------|
| Groq | `GROQ_API_KEY` | Whisper STT + LLM failover | Generous |
| ElevenLabs | `ELEVENLABS_API_KEY` | Neural TTS | 10K chars/month |
| Anthropic | `ANTHROPIC_API_KEY` | Claude agent (Unity) | Pay-per-use |
| Gemini | `GEMINI_API_KEY` | LLM failover (daemon) | 1M tokens/day |
| LiveKit | `LIVEKIT_API_KEY` + `LIVEKIT_API_SECRET` | Multiplayer rooms | 10K mins/month |
| VRoid Hub | OAuth credentials | Avatar marketplace | Free |

---

## Success Metrics (Demo-Ready)

- [ ] Voice loop works on Quest 3: speak → hear Jarvis respond
- [ ] 2+ people in same LiveKit room hearing each other + AI
- [ ] VRM avatar visible, lip-syncing to TTS output
- [ ] < 2s end-to-end latency (speak to hear response)
- [ ] Clean Quest 3 APK build via IL2CPP + ARM64
