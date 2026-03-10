# jAIrvisXR - Project Rules

## Project Overview
Unity XR project with OpenXR multi-platform support and full AI suite (voice, vision, spatial, agent).

## Unity Conventions
- C# scripts go in `Assets/jAIrvisXR/Scripts/` under the appropriate module
- Use namespaces matching folder structure: `jAIrvisXR.AI.Voice`, `jAIrvisXR.XR.Interaction`, etc.
- Prefabs in `Assets/jAIrvisXR/Prefabs/`, organized by category
- Third-party assets/plugins go in `Assets/ThirdParty/`
- Never modify files in `Assets/ThirdParty/` directly

## Code Style
- PascalCase for public methods, properties, classes
- _camelCase for private fields
- Use `[SerializeField]` over public fields for inspector exposure
- Prefer composition over inheritance
- Use Unity events and ScriptableObjects for decoupling

## AI Module Rules
- All LLM API calls go through `Scripts/AI/Agent/`
- Voice pipeline: STT -> Agent -> TTS (never skip the agent layer)
- Keep API keys in environment variables or ScriptableObject configs excluded from git
- No hardcoded model names — use config

## XR Rules
- Target OpenXR for all interaction code
- Use XR Interaction Toolkit abstractions, not vendor-specific APIs
- Test hand tracking and controller paths separately

## Git
- No large binary assets (>50MB) without Git LFS
- Never commit Library/, Temp/, or UserSettings/
