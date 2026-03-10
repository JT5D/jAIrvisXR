#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace jAIrvisXR.Editor
{
    /// <summary>
    /// Editor menu for setting up the XR scene hierarchy.
    /// Creates the XR Origin, managers, voice pipeline, and HUD.
    /// Menu: jAIrvisXR > Setup XR Scene
    /// </summary>
    public static class XRSceneSetup
    {
        [MenuItem("jAIrvisXR/Setup XR Scene", false, 100)]
        public static void SetupScene()
        {
            // 1. Managers group
            var managers = CreateEmpty("[Managers]");

            var interactionMgr = CreateEmpty("XR Interaction Manager", managers.transform);
            AddComponentSafe(interactionMgr, "UnityEngine.XR.Interaction.Toolkit.XRInteractionManager");

            var eventSystem = CreateEmpty("EventSystem", managers.transform);
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            AddComponentSafe(eventSystem, "UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule");

            // 2. Voice Pipeline Manager
            var voiceMgr = CreateEmpty("VoicePipelineManager", managers.transform);
            AddComponentSafe(voiceMgr, "jAIrvisXR.AI.Voice.VoicePipeline");
            AddComponentSafe(voiceMgr, "jAIrvisXR.AI.Voice.VoiceActivation");
            AddComponentSafe(voiceMgr, "jAIrvisXR.AI.Voice.MicrophoneCapture");
            AddComponentSafe(voiceMgr, "jAIrvisXR.AI.Voice.WakeWordDetector");
            AddComponentSafe(voiceMgr, "jAIrvisXR.AI.Voice.AudioPlaybackHandler");
            AddComponentSafe(voiceMgr, "jAIrvisXR.AI.Voice.MockSTTProvider");
            AddComponentSafe(voiceMgr, "jAIrvisXR.AI.Voice.MockTTSProvider");
            AddComponentSafe(voiceMgr, "jAIrvisXR.AI.Voice.GroqWhisperSTTProvider");
            AddComponentSafe(voiceMgr, "jAIrvisXR.AI.Voice.ElevenLabsTTSProvider");
            AddComponentSafe(voiceMgr, "jAIrvisXR.AI.Agent.ClaudeAgentService");

            // 2b. Network Manager
            var networkMgr = CreateEmpty("NetworkManager", managers.transform);
            AddComponentSafe(networkMgr, "jAIrvisXR.Networking.LiveKitRoomManager");
            AddComponentSafe(networkMgr, "jAIrvisXR.Networking.MockMultiplayerProvider");
            AddComponentSafe(networkMgr, "jAIrvisXR.Networking.LiveKitAudioBridge");

            // 2c. Avatar system
            var avatar = CreateEmpty("[Avatar]");
            AddComponentSafe(avatar, "jAIrvisXR.AI.Avatar.VrmAvatarLoader");
            AddComponentSafe(avatar, "jAIrvisXR.AI.Avatar.MockAvatarProvider");
            AddComponentSafe(avatar, "jAIrvisXR.AI.Avatar.VrmLipSync");
            AddComponentSafe(avatar, "jAIrvisXR.AI.Avatar.VrmExpressionDriver");
            AddComponentSafe(avatar, "jAIrvisXR.AI.Avatar.VrmHandTrackingMapper");
            AddComponentSafe(avatar, "jAIrvisXR.Core.Events.GameObjectEventListener");
            AddComponentSafe(avatar, "jAIrvisXR.Core.Events.VoicePipelineEventListener");

            var spawnPoint = CreateEmpty("AvatarSpawnPoint", avatar.transform);
            spawnPoint.transform.localPosition = new Vector3(0, 0, 1.5f);

            // 3. Environment group
            var env = CreateEmpty("[Environment]");

            var light = new GameObject("Directional Light");
            light.transform.SetParent(env.transform);
            var dl = light.AddComponent<Light>();
            dl.type = LightType.Directional;
            dl.intensity = 1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(env.transform);
            floor.transform.localScale = new Vector3(5f, 1f, 5f);

            // 4. UI group
            var ui = CreateEmpty("[UI]");
            var canvas = CreateEmpty("World Space Canvas", ui.transform);
            var canvasComp = canvas.AddComponent<Canvas>();
            canvasComp.renderMode = RenderMode.WorldSpace;
            canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            AddComponentSafe(canvas, "jAIrvisXR.XR.UI.FollowCamera");

            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(800, 400);
            rt.localScale = Vector3.one * 0.001f;

            // State label
            var stateGO = new GameObject("StateLabel");
            stateGO.transform.SetParent(canvas.transform, false);
            var stateRT = stateGO.AddComponent<RectTransform>();
            stateRT.anchorMin = new Vector2(0, 1);
            stateRT.anchorMax = new Vector2(1, 1);
            stateRT.pivot = new Vector2(0.5f, 1);
            stateRT.sizeDelta = new Vector2(0, 60);
            stateRT.anchoredPosition = Vector2.zero;
            AddTMPText(stateGO, "IDLE", 36);

            // Transcript
            var transcriptGO = new GameObject("TranscriptText");
            transcriptGO.transform.SetParent(canvas.transform, false);
            var tRT = transcriptGO.AddComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0, 0.5f);
            tRT.anchorMax = new Vector2(1, 0.85f);
            tRT.offsetMin = new Vector2(20, 0);
            tRT.offsetMax = new Vector2(-20, 0);
            AddTMPText(transcriptGO, "Transcript appears here...", 24);

            // Response
            var responseGO = new GameObject("ResponseText");
            responseGO.transform.SetParent(canvas.transform, false);
            var rRT = responseGO.AddComponent<RectTransform>();
            rRT.anchorMin = new Vector2(0, 0);
            rRT.anchorMax = new Vector2(1, 0.45f);
            rRT.offsetMin = new Vector2(20, 20);
            rRT.offsetMax = new Vector2(-20, 0);
            AddTMPText(responseGO, "Response appears here...", 20);

            // Add HUD component
            AddComponentSafe(canvas, "jAIrvisXR.XR.UI.VoicePipelineHUD");

            Debug.Log("[XRSceneSetup] Scene hierarchy created. " +
                "Add XR Origin via GameObject > XR > XR Origin (VR), " +
                "then configure InputActionManager and XRInputModalityManager.");

            EditorUtility.DisplayDialog("XR Scene Setup",
                "Scene hierarchy created!\n\n" +
                "Next steps:\n" +
                "1. Add XR Origin: GameObject > XR > XR Origin (VR)\n" +
                "2. Import XRI Starter Assets sample\n" +
                "3. Configure push-to-talk input binding\n" +
                "4. Wire VoicePipelineHUD fields in Inspector",
                "OK");
        }

        private static GameObject CreateEmpty(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent);
            return go;
        }

        private static void AddComponentSafe(GameObject go, string typeName)
        {
            var type = System.Type.GetType(typeName);
            if (type == null)
            {
                // Try finding in all assemblies
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType(typeName);
                    if (type != null) break;
                }
            }
            if (type != null)
                go.AddComponent(type);
            else
                Debug.LogWarning($"[XRSceneSetup] Could not find type: {typeName}");
        }

        private static void AddTMPText(GameObject go, string text, int fontSize)
        {
            var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TMPro.TextAlignmentOptions.TopLeft;
            tmp.color = Color.white;
        }
    }
}
#endif
