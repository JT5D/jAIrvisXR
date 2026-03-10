# jAIrvisXR

AI-Powered XR Applications — a multiplatform XR project with integrated AI capabilities including voice interaction, computer vision, spatial understanding, and autonomous agent behavior.

## Project Structure

```
Assets/jAIrvisXR/
├── Scripts/
│   ├── AI/
│   │   ├── Voice/        # STT, TTS, voice command processing
│   │   ├── Vision/       # Object detection, scene understanding
│   │   ├── Spatial/      # Spatial mapping, anchors, environment awareness
│   │   └── Agent/        # LLM integration, decision-making, task execution
│   ├── XR/
│   │   ├── Interaction/  # Hand tracking, controllers, gaze input
│   │   ├── Locomotion/   # Movement, teleportation, navigation
│   │   ├── UI/           # World-space UI, HUD, panels
│   │   └── Tracking/     # Device tracking, body tracking
│   ├── Core/
│   │   ├── Events/       # Event bus, messaging
│   │   ├── Config/       # Runtime configuration
│   │   └── Utils/        # Shared utilities
│   └── Networking/       # Multiplayer, cloud services
├── Prefabs/              # Reusable prefab assets
├── Scenes/               # Unity scenes
├── Materials/            # Materials and physics materials
├── Audio/                # SFX and voice assets
├── Shaders/              # Custom shaders
├── Art/                  # Models, textures, animations
├── StreamingAssets/      # Runtime-loaded assets
└── Resources/            # Unity Resources folder
```

## Platform Support

Built with OpenXR for cross-platform headset compatibility:
- Meta Quest 2/3/Pro
- Apple Vision Pro
- HTC Vive / Focus
- Windows Mixed Reality
- And other OpenXR-compatible devices

## Getting Started

1. Clone the repo
2. Open in Unity (2022.3 LTS or later recommended)
3. Install required packages via Package Manager (OpenXR, XR Interaction Toolkit)
4. Open `Assets/jAIrvisXR/Scenes/` and load a scene

## License

TBD
