# XR Scene Design: OpenXR + XR Interaction Toolkit + Hand Tracking

**Project:** jAIrvisXR
**Date:** 2026-03-10
**Status:** Design / Pre-Implementation

---

## Table of Contents

1. [Required Unity Packages](#1-required-unity-packages)
2. [Package Manifest Entries](#2-package-manifest-entries)
3. [Scene Hierarchy](#3-scene-hierarchy)
4. [Voice Pipeline Integration](#4-voice-pipeline-integration)
5. [Input Action Bindings for Push-to-Talk](#5-input-action-bindings-for-push-to-talk)
6. [Project Settings Configuration](#6-project-settings-configuration)
7. [Platform-Specific Notes](#7-platform-specific-notes)
8. [Step-by-Step Setup Guide](#8-step-by-step-setup-guide)

---

## 1. Required Unity Packages

The following packages are required to build a basic XR scene with OpenXR support, XR Interaction Toolkit interactions, and hand tracking. Version numbers represent the recommended stable releases for Unity 2022.3 LTS (or Unity 6).

| Package | Package ID | Recommended Version | Purpose |
|---------|-----------|---------------------|---------|
| **XR Plugin Management** | `com.unity.xr.management` | 4.5.0+ | Manages XR loader lifecycle, build settings, and runtime initialization |
| **OpenXR Plugin** | `com.unity.xr.openxr` | 1.13.0+ (latest stable) | OpenXR runtime integration; provides cross-platform XR loader |
| **XR Interaction Toolkit** | `com.unity.xr.interaction.toolkit` | 3.0.7+ | High-level interaction system (interactors, interactables, locomotion) |
| **XR Hands** | `com.unity.xr.hands` | 1.5.0+ | Hand tracking subsystem; joint data, gestures, hand mesh |
| **Input System** | `com.unity.inputsystem` | 1.11.0+ | New Input System for action-based controller/hand input |
| **XR Core Utilities** | `com.unity.xr.core-utils` | 2.3.0+ | XR Origin component, math utilities (auto-dependency of XRI) |
| **Unity OpenXR: Meta** | `com.unity.xr.meta-openxr` | 2.1.0+ | Meta Quest-specific OpenXR extensions (hand tracking, passthrough, etc.) |
| **Shader Graph** | `com.unity.shadergraph` | (match your render pipeline) | Required for hand visualization materials in XR Hands samples |

### Optional but Recommended

| Package | Package ID | Version | Purpose |
|---------|-----------|---------|---------|
| **AR Foundation** | `com.unity.xr.arfoundation` | 6.0+ | Required if using `com.unity.xr.meta-openxr`; provides plane detection, anchors |
| **TextMeshPro** | `com.unity.textmeshpro` | 3.0+ | World-space UI text rendering (auto-dependency of many packages) |

### Version Compatibility Matrix

| Unity Editor | XRI | OpenXR Plugin | XR Hands | XR Mgmt |
|-------------|-----|---------------|----------|---------|
| 2022.3 LTS | 3.0.x | 1.13.x | 1.5.x | 4.5.x |
| Unity 6 (6000.0) | 3.0.x - 3.1.x | 1.13.x - 1.15.x | 1.5.x - 1.7.x | 4.5.x |
| Unity 6.2+ | 3.1.x - 3.3.x | 1.15.x - 1.16.x | 1.7.x | 4.5.x |

> **Guidance:** Use the latest patch release within the recommended major.minor for your Unity editor version. Check the Package Manager's "Verified" tab for your editor's validated versions.

---

## 2. Package Manifest Entries

The project currently has no `Packages/manifest.json`. When the Unity project is opened for the first time, Unity auto-generates this file. Below is the manifest content required for our XR stack.

### Packages/manifest.json

```json
{
  "dependencies": {
    "com.unity.inputsystem": "1.11.2",
    "com.unity.xr.core-utils": "2.3.0",
    "com.unity.xr.hands": "1.5.0",
    "com.unity.xr.interaction.toolkit": "3.0.7",
    "com.unity.xr.management": "4.5.0",
    "com.unity.xr.meta-openxr": "2.1.0",
    "com.unity.xr.openxr": "1.13.1",
    "com.unity.textmeshpro": "3.0.9",
    "com.unity.ugui": "1.0.0"
  },
  "scopedRegistries": []
}
```

> **Note:** Unity will auto-resolve transitive dependencies. For example, `com.unity.xr.interaction.toolkit` depends on `com.unity.inputsystem` and `com.unity.xr.core-utils`, and `com.unity.xr.meta-openxr` depends on `com.unity.xr.openxr` and `com.unity.xr.arfoundation`. Listing them explicitly ensures version pinning. After adding the manifest, open Unity and let it resolve/download all packages. If prompted to enable the new Input System backend, click **Yes** and allow the editor to restart.

---

## 3. Scene Hierarchy

Below is the complete GameObject hierarchy for the main XR scene, named `XRMainScene`. This scene combines the XR rig, interaction system, hand tracking, and the existing voice pipeline.

### 3.1 Full Hierarchy Tree

```
XRMainScene
│
├── [Managers]
│   ├── XR Interaction Manager          (1)
│   ├── EventSystem                     (2)
│   └── VoicePipelineManager            (3)
│
├── XR Origin (XR Rig)                  (4)
│   ├── Camera Offset                   (5)
│   │   ├── Main Camera                 (6)
│   │   ├── Gaze Interactor             (7)
│   │   │   ├── Gaze Stabilized
│   │   │   └── Gaze Stabilized Attach
│   │   │
│   │   ├── Left Controller             (8)
│   │   │   ├── Poke Interactor
│   │   │   │   └── Poke Point
│   │   │   ├── Direct Interactor
│   │   │   ├── Ray Interactor
│   │   │   │   ├── Stabilized
│   │   │   │   └── Stabilized Attach
│   │   │   └── Teleport Interactor
│   │   │
│   │   ├── Right Controller            (9)
│   │   │   ├── Poke Interactor
│   │   │   │   └── Poke Point
│   │   │   ├── Direct Interactor
│   │   │   ├── Ray Interactor
│   │   │   │   ├── Stabilized
│   │   │   │   └── Stabilized Attach
│   │   │   └── Teleport Interactor
│   │   │
│   │   ├── Left Hand                   (10)
│   │   │   ├── Hand Visualizer
│   │   │   ├── Poke Interactor
│   │   │   └── Direct Interactor
│   │   │
│   │   └── Right Hand                  (11)
│   │       ├── Hand Visualizer
│   │       ├── Poke Interactor
│   │       └── Direct Interactor
│   │
│   └── Locomotion                      (12)
│       ├── Turn
│       ├── Move
│       ├── Teleportation
│       └── Climb
│
├── [Environment]
│   ├── Directional Light
│   ├── Floor (with Teleport Area)
│   └── (scene content)
│
└── [UI]
    └── World Space Canvas
        └── (HUD panels, status indicators)
```

### 3.2 Component Breakdown by GameObject

**(1) XR Interaction Manager**

| Component | Configuration |
|-----------|--------------|
| `XRInteractionManager` | Default; acts as intermediary between all interactors and interactables |

**(2) EventSystem**

| Component | Configuration |
|-----------|--------------|
| `EventSystem` | Standard Unity EventSystem |
| `XRUIInputModule` | Replaces `StandaloneInputModule`; enables XR ray/poke interaction with world-space UI |

**(3) VoicePipelineManager** -- See [Section 4](#4-voice-pipeline-integration) for details.

**(4) XR Origin (XR Rig)**

| Component | Configuration |
|-----------|--------------|
| `XROrigin` | Tracking Origin Mode = **Not Specified** (runtime default); Camera Floor Offset Object = Camera Offset; Camera = Main Camera |
| `CharacterController` | Height = 1.8m; for locomotion collision |
| `InputActionManager` | Action Assets = `XRI Default Input Actions` (from Starter Assets sample) |
| `XRInputModalityManager` | Left Controller = Left Controller GO; Right Controller = Right Controller GO; Left Hand = Left Hand GO; Right Hand = Right Hand GO |
| `XRGazeAssistance` | Optional; assists gaze-based interactions |

> **XR Input Modality Manager** is critical: it automatically switches between controller and hand tracking GameObjects at runtime. When hands are detected, it deactivates controller GOs and activates hand GOs, and vice versa.

**(5) Camera Offset**

| Component | Configuration |
|-----------|--------------|
| `Transform` | Position auto-managed by XROrigin based on tracking origin mode |

**(6) Main Camera**

| Component | Configuration |
|-----------|--------------|
| `Camera` | Clear Flags = Solid Color (black for VR) or Skybox; Near clip = 0.01; Tag = MainCamera |
| `AudioListener` | For 3D spatial audio and TTS playback |
| `TrackedPoseDriver` | Position Input = `<XRHMD>/centerEyePosition`; Rotation Input = `<XRHMD>/centerEyeRotation`; Tracking State Input = `<XRHMD>/trackingState` |

**(8-9) Left/Right Controller**

| Component | Configuration |
|-----------|--------------|
| `TrackedPoseDriver` | Position/Rotation bound to `<XRController>{LeftHand}` or `{RightHand}` device position/rotation |
| `XRInteractionGroup` | Groups all child interactors for priority-based selection |
| `HapticImpulsePlayer` | Enables haptic feedback on interactions |
| `ControllerInputActionManager` | Manages enable/disable of input action maps based on interaction state |

Each controller has these child interactors:

- **Poke Interactor**: `XRPokeInteractor` + `SimpleHapticFeedback` -- for poking UI buttons
- **Direct Interactor**: `XRDirectInteractor` + `SphereCollider` + `SimpleHapticFeedback` -- for grabbing nearby objects
- **Ray Interactor**: `XRRayInteractor` + `LineRenderer` + `XRInteractorLineVisual` + `SimpleHapticFeedback` -- for distant selection
- **Teleport Interactor**: `XRRayInteractor` (teleport config) + `LineRenderer` + `XRInteractorLineVisual` -- for locomotion

**(10-11) Left/Right Hand (Hand Tracking)**

| Component | Configuration |
|-----------|--------------|
| `TrackedPoseDriver` | Bound to `<XRHand>{LeftHand}` or `{RightHand}` |
| `XRHandTrackingEvents` | Raises events when hand tracking state changes |
| `XRHandSkeletonDriver` | Drives hand skeleton joint transforms |
| `XRHandMeshController` | Optional; renders runtime hand mesh if available |

Each hand has:

- **Hand Visualizer**: `XRHandVisualizer` from XR Hands sample -- renders joint spheres or skinned mesh
- **Poke Interactor**: `XRPokeInteractor` -- index finger poke for UI
- **Direct Interactor**: `XRDirectInteractor` + `SphereCollider` -- pinch grab

> **Note:** Hand tracking interactors use the `XR Hand Interaction Profile` in OpenXR. The poke interactor tracks the index fingertip position. The direct interactor uses pinch detection (thumb-to-index proximity) for grab.

**(12) Locomotion**

| Component (on parent) | Configuration |
|-----------|--------------|
| `LocomotionMediator` | Coordinates between locomotion providers |
| `XRBodyTransformer` | Applies locomotion transforms to the XR Origin |

Child providers:
- **Turn**: `SnapTurnProvider` (45-degree increments) + `ContinuousTurnProvider` (smooth turn, disabled by default)
- **Move**: `DynamicMoveProvider` (thumbstick-based movement with head-direction)
- **Teleportation**: `TeleportationProvider`
- **Climb**: `ClimbProvider` (disabled by default)

---

## 4. Voice Pipeline Integration

The existing voice pipeline components (`VoicePipeline`, `VoiceActivation`, `MicrophoneCapture`, `WakeWordDetector`, `AudioPlaybackHandler`) are designed as a self-contained MonoBehaviour graph. They integrate into the XR scene as follows:

### 4.1 VoicePipelineManager GameObject

```
VoicePipelineManager
├── VoicePipeline             (component)
├── VoiceActivation           (component)
├── MicrophoneCapture         (component)
├── WakeWordDetector          (component)
├── AudioPlaybackHandler      (component + AudioSource)
├── MockSTTProvider           (component, swap with real provider)
├── MockTTSProvider           (component, swap with real provider)
└── ClaudeAgentService        (component)
```

### 4.2 Placement in XR Scene

The `VoicePipelineManager` sits at the **root level of the scene**, separate from the XR Origin hierarchy. It should NOT be a child of XR Origin because:

1. The voice pipeline has no spatial tracking requirements -- it processes audio from the device microphone, not from a tracked position.
2. The `AudioPlaybackHandler`'s `AudioSource` should be on the Main Camera (or a child of it) for proper spatialization. Two options:
   - **Option A (Recommended):** Keep `AudioPlaybackHandler` on VoicePipelineManager with a **non-spatial** AudioSource (2D sound). TTS responses play equally in both ears. This is simpler and appropriate for an assistant voice.
   - **Option B:** Place a separate `AudioSource` on Main Camera as a child, and reference it from `AudioPlaybackHandler`. Provides head-locked audio.

### 4.3 Connection Points Between XR and Voice Pipeline

| Connection | Mechanism | Details |
|------------|-----------|---------|
| **Push-to-Talk Input** | `InputActionReference` on `VoiceActivation` | The `_pushToTalkAction` field references an Input Action from the XRI Default Input Actions asset (or a custom asset). See [Section 5](#5-input-action-bindings-for-push-to-talk). |
| **Pipeline State to UI** | `VoicePipelineEvent` ScriptableObject | The XR world-space UI listens for pipeline state changes (Idle, Listening, Processing, Speaking) to show visual feedback. |
| **Transcript/Response to UI** | `StringEvent` ScriptableObjects | `_transcriptReadyEvent` and `_agentResponseReadyEvent` raise events that world-space UI panels can display. |
| **Microphone Access** | Unity `Microphone` API | Works on Quest via Android microphone permissions. No XR-specific integration needed. |

### 4.4 Architecture Diagram

```
┌──────────────────────────────────────────────────────┐
│                    XR Scene                          │
│                                                      │
│  ┌─────────────────────────────────────────────────┐ │
│  │  XR Origin (XR Rig)                             │ │
│  │   ├─ Main Camera (AudioListener)                │ │
│  │   ├─ Left/Right Controller (interactors)        │ │
│  │   ├─ Left/Right Hand (hand tracking)            │ │
│  │   └─ Locomotion                                 │ │
│  └────────┬────────────────────────────────────────┘ │
│           │ InputActionReference                     │
│           │ (push-to-talk button)                    │
│           ▼                                          │
│  ┌─────────────────────────────────────────────────┐ │
│  │  VoicePipelineManager                           │ │
│  │   ├─ VoiceActivation ◄── InputAction (PTT)     │ │
│  │   ├─ MicrophoneCapture                          │ │
│  │   ├─ VoicePipeline                              │ │
│  │   │    STT ──► Agent ──► TTS ──► Playback       │ │
│  │   └─ AudioPlaybackHandler (AudioSource 2D)     │ │
│  └────────┬────────────────────────────────────────┘ │
│           │ ScriptableObject Events                  │
│           ▼                                          │
│  ┌─────────────────────────────────────────────────┐ │
│  │  World Space UI                                 │ │
│  │   ├─ Pipeline State Indicator                   │ │
│  │   ├─ Transcript Display                         │ │
│  │   └─ Response Display                           │ │
│  └─────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────┘
```

---

## 5. Input Action Bindings for Push-to-Talk

The existing `VoiceActivation.cs` already uses `InputActionReference _pushToTalkAction`, which integrates cleanly with the XR Interaction Toolkit's action-based input system.

### 5.1 Recommended Button Mapping

| Controller | Binding Path | Button | Rationale |
|------------|-------------|--------|-----------|
| Oculus Touch (Meta Quest) | `<OculusTouchController>{LeftHand}/primaryButton` | **X button** (left) | Non-dominant hand, does not conflict with grab/teleport |
| Oculus Touch (Meta Quest) | `<OculusTouchController>{RightHand}/secondaryButton` | **B button** (right) | Alternative for right-hand preference |
| Generic OpenXR | `<XRController>{LeftHand}/primaryButton` | A/X button equivalent | Cross-platform fallback |
| HTC Vive | `<ViveController>{LeftHand}/primaryButton` | Menu button | Mapped to primaryButton in OpenXR |

### 5.2 Input Action Asset Configuration

Create a new Action Map called `jAIrvisXR Voice` inside the XRI Default Input Actions asset (or a separate `jAIrvisXR Input Actions` asset):

```
Action Map: "jAIrvisXR Voice"
│
└── Action: "Push To Talk"
    ├── Type: Button
    ├── Interactions: (none -- use started/canceled callbacks)
    └── Bindings:
        ├── <OculusTouchController>{LeftHand}/primaryButton
        ├── <XRController>{LeftHand}/primaryButton
        ├── <Keyboard>/v                              (editor testing)
        └── <XRController>{RightHand}/secondaryButton (alt binding)
```

### 5.3 InputActionReference Setup

1. Create the Input Action Asset file at `Assets/jAIrvisXR/Input/jAIrvisXR Input Actions.inputactions`
2. In the Inspector, create the action map and actions as described above
3. On the `VoiceActivation` component, drag the `Push To Talk` action into the `_pushToTalkAction` field
4. Ensure the `InputActionManager` on the XR Origin has this asset in its Action Assets list (so it is auto-enabled)

### 5.4 Hand Tracking Push-to-Talk Alternative

When controllers are not available (hand tracking mode), push-to-talk via a physical button is not possible. Alternatives:

1. **Wake Word Detection** -- Already implemented in `WakeWordDetector.cs`. Set `_config.UseWakeWord = true` and `_config.UsePushToTalk = false` when in hand tracking mode.
2. **Pinch Gesture** -- Use `XRHandTrackingEvents` to detect a sustained pinch gesture (thumb + middle finger) as a push-to-talk trigger. This would require a new script: `HandGestureVoiceActivation.cs` in `Assets/jAIrvisXR/Scripts/XR/Interaction/`.
3. **UI Button** -- Place a world-space "Talk" button that can be poked with the index finger.

> **Recommendation:** Use the `XRInputModalityManager` events to switch between PTT (controller mode) and wake word (hand tracking mode) automatically.

---

## 6. Project Settings Configuration

### 6.1 XR Plug-in Management

**Edit > Project Settings > XR Plug-in Management**

| Tab | Setting | Value |
|-----|---------|-------|
| **Standalone (PC)** | OpenXR | Enabled (checked) |
| **Android** | OpenXR | Enabled (checked) |

### 6.2 OpenXR Settings

**Edit > Project Settings > XR Plug-in Management > OpenXR**

#### Standalone Tab

| Setting | Value |
|---------|-------|
| Render Mode | Multi-pass (or Single Pass Instanced if shaders support it) |
| Depth Submission Mode | Depth 16 Bit |
| **Interaction Profiles** | |
| - Oculus Touch Controller Profile | Added |
| - Valve Index Controller Profile | Added (if targeting SteamVR) |
| - HTC Vive Controller Profile | Added (if targeting Vive) |
| **OpenXR Feature Groups** | |
| - Hand Tracking Subsystem | Enabled |
| - Mock Runtime | Enabled (for editor testing without headset) |

#### Android Tab

| Setting | Value |
|---------|-------|
| Render Mode | Multi-pass |
| Depth Submission Mode | Depth 16 Bit |
| **Interaction Profiles** | |
| - Oculus Touch Controller Profile | Added |
| - Hand Interaction Profile | Added |
| **OpenXR Feature Groups** | |
| - Meta Quest Support | Enabled |
| - Hand Tracking Subsystem | Enabled |
| - Meta Hand Tracking Aim | Enabled |

### 6.3 Player Settings (Android / Meta Quest)

**Edit > Project Settings > Player > Android Tab**

| Setting | Value |
|---------|-------|
| Company Name | (your company) |
| Product Name | jAIrvisXR |
| Minimum API Level | Android 10 (API 29) |
| Target API Level | Android 12 (API 32) or higher |
| Scripting Backend | IL2CPP |
| Target Architectures | ARM64 only |
| Texture Compression | ASTC |
| Color Space | Linear |
| Graphics APIs | Remove Vulkan, keep OpenGLES3 (or use Vulkan if render pipeline supports it) |
| Active Input Handling | **Input System Package (New)** |

### 6.4 Player Settings (Standalone / PC VR)

| Setting | Value |
|---------|-------|
| Active Input Handling | **Input System Package (New)** |
| Color Space | Linear |
| Graphics APIs | Platform default (DX11/DX12 on Windows, Metal on macOS) |

### 6.5 Additional Android Manifest Permissions

The following permissions must be present for Quest builds. Unity/OpenXR add some automatically, but verify in `Assets/Plugins/Android/AndroidManifest.xml`:

```xml
<!-- Hand tracking -->
<uses-permission android:name="com.oculus.permission.HAND_TRACKING" />
<uses-feature android:name="oculus.software.handtracking" android:required="false" />

<!-- Microphone for voice pipeline -->
<uses-permission android:name="android.permission.RECORD_AUDIO" />

<!-- Internet for AI API calls -->
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
```

---

## 7. Platform-Specific Notes

### 7.1 Meta Quest 2 / 3 / Pro

| Topic | Details |
|-------|---------|
| **Recommended Plugin** | `com.unity.xr.meta-openxr` (2.1.0+) replaces deprecated `com.unity.xr.oculus` |
| **Hand Tracking** | Fully supported via OpenXR Hand Tracking Subsystem; enable "Hand Tracking" in headset Settings > Movement Tracking |
| **Controller + Hand Switching** | Automatic via `XRInputModalityManager`; Quest runtime handles detection |
| **Passthrough** | Available via `com.unity.xr.meta-openxr` passthrough feature; not required for basic scene |
| **Performance** | Target 72 Hz or 90 Hz; use Fixed Foveated Rendering (FFR) level 2+; keep draw calls under 100 |
| **Build** | Build as Android APK/AAB; deploy via `adb install` or Meta Quest Developer Hub |
| **Microphone** | Requires `RECORD_AUDIO` permission; request at runtime on Android 6+ |
| **Testing** | Use Quest Link or Air Link for editor testing; or build APK for on-device testing |

### 7.2 SteamVR (HTC Vive, Valve Index, etc.)

| Topic | Details |
|-------|---------|
| **Plugin** | `com.unity.xr.openxr` with SteamVR OpenXR runtime |
| **Hand Tracking** | Limited; depends on hardware (Valve Index has finger tracking via controllers, not optical hand tracking). Ultraleap/Leap Motion can provide optical hand tracking via OpenXR. |
| **Controller Profiles** | Add Valve Index Controller Profile and/or HTC Vive Controller Profile |
| **Build** | Standalone Windows build; SteamVR runtime must be installed |

### 7.3 Apple Vision Pro

| Topic | Details |
|-------|---------|
| **Plugin** | Requires `com.unity.polyspatial` and `com.unity.xr.visionos` (separate ecosystem) |
| **Hand Tracking** | Native; visionOS uses its own hand tracking system, not OpenXR |
| **Note** | Apple Vision Pro does NOT use OpenXR. It requires a separate build target and interaction model. Cross-platform code should be abstracted at the XRI level where possible, but visionOS needs its own scene setup. |
| **Recommendation** | Defer visionOS support to a later phase. Focus on OpenXR platforms first. |

### 7.4 Windows Mixed Reality / HoloLens

| Topic | Details |
|-------|---------|
| **Plugin** | `com.unity.xr.openxr` with WMR-specific interaction profiles |
| **Hand Tracking** | HoloLens 2 supports articulated hand tracking via OpenXR |
| **Note** | Microsoft is deprecating HoloLens 2 support in Unity. Not recommended as a primary target. |

---

## 8. Step-by-Step Setup Guide

This guide assumes you are starting from the current project state (no `manifest.json`, empty XR directories).

### Phase 1: Package Installation

1. **Open the project in Unity 2022.3 LTS** (or Unity 6).
   - Unity will auto-generate `Packages/manifest.json` with default packages.

2. **Open Package Manager** (Window > Package Manager).

3. **Install packages in this order** (to avoid dependency resolution issues):
   - `XR Plugin Management` (com.unity.xr.management) -- search "XR Plugin Management" in Unity Registry
   - `OpenXR Plugin` (com.unity.xr.openxr) -- search "OpenXR"
   - `XR Interaction Toolkit` (com.unity.xr.interaction.toolkit) -- search "XR Interaction Toolkit"
     - When prompted to enable the new Input System backend, click **Yes** and allow restart
   - `XR Hands` (com.unity.xr.hands) -- search "XR Hands"
   - `Unity OpenXR: Meta` (com.unity.xr.meta-openxr) -- search "OpenXR Meta"

4. **Import Starter Assets samples:**
   - In Package Manager, select `XR Interaction Toolkit`
   - Expand the **Samples** section
   - Click **Import** next to **Starter Assets**
   - Click **Import** next to **Hands Interaction Demo** (optional, for reference)
   - When prompted to update Input Action presets, click **Accept** or **Update**

5. **Import XR Hands samples:**
   - In Package Manager, select `XR Hands`
   - Expand the **Samples** section
   - Click **Import** next to **HandVisualizer**
   - Click **Import** next to **Gestures** (optional)

### Phase 2: Project Settings

6. **Configure XR Plugin Management:**
   - Go to **Edit > Project Settings > XR Plug-in Management**
   - **Standalone tab**: Check **OpenXR**
   - **Android tab**: Check **OpenXR**

7. **Configure OpenXR:**
   - Go to **Edit > Project Settings > XR Plug-in Management > OpenXR**
   - **Standalone tab**:
     - Add Interaction Profile: **Oculus Touch Controller Profile**
     - Under OpenXR Feature Groups, enable: **Hand Tracking Subsystem**
     - Optionally enable: **Mock Runtime** (for testing without headset)
   - **Android tab**:
     - Add Interaction Profile: **Oculus Touch Controller Profile**
     - Add Interaction Profile: **Hand Interaction Profile**
     - Under OpenXR Feature Groups, enable:
       - **Meta Quest Support**
       - **Hand Tracking Subsystem**
       - **Meta Hand Tracking Aim**

8. **Configure Player Settings:**
   - **Edit > Project Settings > Player**
   - Set **Active Input Handling** to **Input System Package (New)** (if not already set by the restart prompt)
   - **Android tab**: Set Minimum API Level = 29, Scripting Backend = IL2CPP, Target Architectures = ARM64

9. **Fix any Project Validation issues:**
   - Go to **Edit > Project Settings > XR Plug-in Management > Project Validation**
   - Click **Fix All** to resolve any detected configuration issues

### Phase 3: Scene Setup

10. **Create the XR scene:**
    - **File > New Scene** (use the Basic template)
    - Save as `Assets/jAIrvisXR/Scenes/XRMainScene.unity`

11. **Remove default camera:**
    - Delete the default `Main Camera` from the hierarchy (the XR Origin will create its own)

12. **Add XR Interaction Manager:**
    - **GameObject > XR > Interaction Manager**

13. **Add XR Origin:**
    - **Option A (Recommended):** Drag the **XR Origin (XR Rig)** prefab from `Assets/Samples/XR Interaction Toolkit/<version>/Starter Assets/Prefabs/` into the scene
    - **Option B (Manual):** **GameObject > XR > XR Origin (VR)**, then manually add controller GameObjects and components as described in [Section 3](#3-scene-hierarchy)

14. **Configure Input Action Manager:**
    - Select the XR Origin (XR Rig) GameObject
    - On the `InputActionManager` component, verify that `XRI Default Input Actions` is in the Action Assets list
    - Add the jAIrvisXR custom Input Actions asset if created (see [Section 5](#5-input-action-bindings-for-push-to-talk))

15. **Set up hand tracking GameObjects:**
    - Under Camera Offset, create two empty GameObjects: `Left Hand` and `Right Hand`
    - Add components to each: `TrackedPoseDriver`, `XRHandTrackingEvents`, `XRHandSkeletonDriver`
    - Add child GameObjects for interactors (Poke, Direct) and visualizers
    - Or: Import the `Hands Interaction Demo` sample and reference its hand prefabs

16. **Configure XR Input Modality Manager:**
    - On the XR Origin (XR Rig) root, add `XRInputModalityManager` component
    - Assign: Left Controller GO, Right Controller GO, Left Hand GO, Right Hand GO
    - This component auto-toggles between controllers and hands at runtime

### Phase 4: Voice Pipeline Integration

17. **Add VoicePipelineManager:**
    - Create an empty GameObject named `VoicePipelineManager` at the scene root
    - Add components: `VoicePipeline`, `VoiceActivation`, `MicrophoneCapture`, `WakeWordDetector`, `AudioPlaybackHandler`
    - Add provider components: `MockSTTProvider`, `MockTTSProvider`, `ClaudeAgentService`
    - Wire up all `[SerializeField]` references in the Inspector

18. **Configure Push-to-Talk Input:**
    - Create Input Action Asset at `Assets/jAIrvisXR/Input/jAIrvisXR Input Actions.inputactions` (if not using the XRI Default)
    - Add Action Map "jAIrvisXR Voice" with Action "Push To Talk" (Button type)
    - Add bindings for `<OculusTouchController>{LeftHand}/primaryButton` and `<Keyboard>/v`
    - On `VoiceActivation`, assign the Push To Talk action to `_pushToTalkAction`
    - Add the custom Input Actions asset to the `InputActionManager` on XR Origin

19. **Create ScriptableObject configs:**
    - Create `VoicePipelineConfig` asset: **Assets > Create > jAIrvisXR > Config > Voice Pipeline Config**
    - Set `UsePushToTalk = true`, adjust silence thresholds and recording limits
    - Assign to all voice pipeline components that reference it

### Phase 5: Testing

20. **Editor testing (no headset):**
    - Ensure Mock Runtime is enabled in OpenXR settings (Standalone tab)
    - Enter Play mode
    - Use the XR Device Simulator (import from XRI Samples if needed) to emulate HMD and controllers
    - Press `V` on keyboard for push-to-talk (if keyboard binding was added)

21. **Quest testing via Link:**
    - Connect Quest headset via USB or Air Link
    - In Unity, set build target to Android
    - Enter Play mode -- the scene should render in the headset via Link
    - Test controller input and hand tracking switching

22. **Quest standalone build:**
    - **File > Build Settings** > Switch to Android platform
    - Click **Build and Run** (with Quest connected via USB)
    - Test all interactions on-device

---

## Appendix: File Locations Summary

| Asset | Path |
|-------|------|
| XR Main Scene | `Assets/jAIrvisXR/Scenes/XRMainScene.unity` |
| Voice Pipeline Scripts | `Assets/jAIrvisXR/Scripts/AI/Voice/` |
| XR Interaction Scripts | `Assets/jAIrvisXR/Scripts/XR/Interaction/` |
| Input Actions Asset | `Assets/jAIrvisXR/Input/jAIrvisXR Input Actions.inputactions` |
| Voice Pipeline Config | `Assets/jAIrvisXR/ScriptableObjects/Config/VoicePipelineConfig.asset` |
| XRI Starter Assets | `Assets/Samples/XR Interaction Toolkit/<version>/Starter Assets/` |
| XR Hands Visualizer | `Assets/Samples/XR Hands/<version>/HandVisualizer/` |
| Prefabs (custom) | `Assets/jAIrvisXR/Prefabs/` |

## Appendix: Key Script Interfaces

The voice pipeline already uses these patterns that work well with XR:

- `VoiceActivation._pushToTalkAction` (`InputActionReference`) -- binds directly to XRI input actions
- `VoicePipelineConfig.UsePushToTalk` / `UseWakeWord` -- runtime switchable for controller vs hand mode
- ScriptableObject events (`VoicePipelineEvent`, `StringEvent`) -- decoupled from scene hierarchy, works with any UI

## Appendix: Next Steps After This Design

1. **Implement `HandGestureVoiceActivation.cs`** in `Scripts/XR/Interaction/` -- hand gesture alternative to push-to-talk
2. **Create XR world-space UI prefabs** in `Prefabs/UI/` -- pipeline state indicator, transcript panel
3. **Build the XRMainScene** in Unity Editor following this guide
4. **Create the `jAIrvisXR Input Actions.inputactions`** asset with the push-to-talk bindings
5. **Test on Quest 3** -- validate hand tracking, controller switching, microphone access
6. **Implement real STT/TTS providers** -- replace Mock providers with Whisper/ElevenLabs or similar
