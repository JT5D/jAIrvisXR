#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEditor.XR.Management.Metadata;

namespace jAIrvisXR.Editor
{
    /// <summary>
    /// Configures project for Meta Quest 3 and builds APK.
    /// Called from batch mode: -executeMethod jAIrvisXR.Editor.QuestBuilder.Build
    /// </summary>
    public static class QuestBuilder
    {
        private const string APK_PATH = "Builds/jAIrvisXR.apk";
        private const string SCENE_PATH = "Assets/jAIrvisXR/Scenes/MainScene.unity";

        [MenuItem("jAIrvisXR/Build Quest 3 APK", false, 200)]
        public static void Build()
        {
            Debug.Log("[QuestBuilder] Starting Quest 3 build...");

            // 1. Configure Player Settings
            ConfigurePlayerSettings();

            // 2. Configure XR Management for Android
            ConfigureXR();

            // 3. Ensure a scene exists
            EnsureScene();

            // 4. Build APK
            BuildAPK();
        }

        [MenuItem("jAIrvisXR/Configure Quest 3 Settings (No Build)", false, 201)]
        public static void ConfigureOnly()
        {
            ConfigurePlayerSettings();
            ConfigureXR();
            EnsureScene();
            Debug.Log("[QuestBuilder] Quest 3 settings applied. Build manually via File > Build Settings.");
            EditorUtility.DisplayDialog("Quest 3 Configuration",
                "Settings applied:\n" +
                "- IL2CPP + ARM64\n" +
                "- Min API 29 (Android 10)\n" +
                "- OpenGLES3 only\n" +
                "- Linear color space\n" +
                "- OpenXR + Meta Quest enabled\n" +
                "- MainScene created and assigned\n\n" +
                "Build via File > Build Settings > Build",
                "OK");
        }

        private static void ConfigurePlayerSettings()
        {
            Debug.Log("[QuestBuilder] Configuring Player Settings...");

            // Switch to Android
            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android, BuildTarget.Android);

            // Package name
            PlayerSettings.SetApplicationIdentifier(
                BuildTargetGroup.Android, "com.jairvisxr.app");

            // Scripting backend: IL2CPP (required for ARM64)
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);

            // Target ARM64 only
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // Min API level 29 (Android 10, required for Quest 3)
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;

            // Graphics API: OpenGLES3 only (Vulkan breaks WebRTC video)
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });

            // Color space
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // Internet permission (required for API calls)
            PlayerSettings.Android.forceInternetPermission = true;

            // VR settings
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            // Company/product
            PlayerSettings.companyName = "jAIrvisXR";
            PlayerSettings.productName = "jAIrvisXR";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.Android.bundleVersionCode = 1;

            // Stripping
            PlayerSettings.SetManagedStrippingLevel(
                BuildTargetGroup.Android, ManagedStrippingLevel.Low);

            Debug.Log("[QuestBuilder] Player Settings configured.");
        }

        private static void ConfigureXR()
        {
            Debug.Log("[QuestBuilder] Configuring XR Management for Android...");

            // Get or create the per-build-target settings container
            var perBuildTarget = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(
                BuildTargetGroup.Android);

            if (perBuildTarget == null)
            {
                // Create settings for Android
                perBuildTarget = ScriptableObject.CreateInstance<XRGeneralSettings>();

                // Ensure directory exists
                const string settingsDir = "Assets/XR/Settings";
                if (!Directory.Exists(settingsDir))
                    Directory.CreateDirectory(settingsDir);

                const string settingsPath = settingsDir + "/XRGeneralSettings_Android.asset";
                AssetDatabase.CreateAsset(perBuildTarget, settingsPath);

                // Register in the per-build-target container
                var container = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(
                    "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                if (container != null)
                {
                    container.SetSettingsForBuildTarget(BuildTargetGroup.Android, perBuildTarget);
                    EditorUtility.SetDirty(container);
                }

                Debug.Log("[QuestBuilder] Created XRGeneralSettings for Android.");
            }

            // Get or create XR Manager Settings
            var managerSettings = perBuildTarget.AssignedSettings;
            if (managerSettings == null)
            {
                managerSettings = ScriptableObject.CreateInstance<XRManagerSettings>();
                const string managerPath = "Assets/XR/Settings/XRManagerSettings_Android.asset";
                AssetDatabase.CreateAsset(managerSettings, managerPath);
                perBuildTarget.AssignedSettings = managerSettings;
                EditorUtility.SetDirty(perBuildTarget);
                Debug.Log("[QuestBuilder] Created XRManagerSettings for Android.");
            }

            // Ensure OpenXR loader is registered
            var loaders = managerSettings.activeLoaders;
            bool hasOpenXR = loaders != null && loaders.Any(l => l is OpenXRLoader);

            if (!hasOpenXR)
            {
                // Find or create OpenXR loader
                var openxrLoaders = AssetDatabase.FindAssets("t:OpenXRLoader");
                OpenXRLoader openxrLoader = null;

                if (openxrLoaders.Length > 0)
                {
                    openxrLoader = AssetDatabase.LoadAssetAtPath<OpenXRLoader>(
                        AssetDatabase.GUIDToAssetPath(openxrLoaders[0]));
                }
                else
                {
                    openxrLoader = ScriptableObject.CreateInstance<OpenXRLoader>();
                    AssetDatabase.CreateAsset(openxrLoader,
                        "Assets/XR/Settings/OpenXRLoader_Android.asset");
                }

                if (openxrLoader != null)
                {
                    // Use XRPackageMetadataStore to assign the loader
                    var success = XRPackageMetadataStore.AssignLoader(
                        managerSettings, typeof(OpenXRLoader).FullName, BuildTargetGroup.Android);
                    if (success)
                        Debug.Log("[QuestBuilder] OpenXR loader assigned for Android.");
                    else
                        Debug.LogWarning("[QuestBuilder] Failed to assign OpenXR loader. Try manually in XR Plug-in Management.");
                }
            }
            else
            {
                Debug.Log("[QuestBuilder] OpenXR loader already configured for Android.");
            }

            // Enable Meta Quest OpenXR features
            EnableOpenXRFeatures();

            AssetDatabase.SaveAssets();
            Debug.Log("[QuestBuilder] XR Management configured.");
        }

        private static void EnableOpenXRFeatures()
        {
            // Get OpenXR settings for Android
            var openxrSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (openxrSettings == null)
            {
                Debug.LogWarning("[QuestBuilder] OpenXR settings not found for Android. " +
                    "Enable features manually in Project Settings > XR > OpenXR.");
                return;
            }

            // Enable all Meta Quest related features
            var features = openxrSettings.GetFeatures();
            foreach (var feature in features)
            {
                string name = feature.GetType().Name;
                // Enable Meta Quest Target, Hand Tracking, and other Quest features
                if (name.Contains("MetaQuest") || name.Contains("HandTracking") ||
                    name.Contains("MetaQuestFeature"))
                {
                    if (!feature.enabled)
                    {
                        feature.enabled = true;
                        Debug.Log($"[QuestBuilder] Enabled OpenXR feature: {name}");
                    }
                }
            }

            EditorUtility.SetDirty(openxrSettings);
        }

        private static void EnsureScene()
        {
            if (File.Exists(SCENE_PATH))
            {
                Debug.Log($"[QuestBuilder] Scene exists: {SCENE_PATH}");
            }
            else
            {
                Debug.Log("[QuestBuilder] Creating MainScene...");

                // Create directory
                var dir = Path.GetDirectoryName(SCENE_PATH);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // Create new scene
                var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                    UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                    UnityEditor.SceneManagement.NewSceneMode.Single);

                // Run XR scene setup if available
                var setupType = System.Type.GetType("jAIrvisXR.Editor.XRSceneSetup");
                if (setupType != null)
                {
                    var setupMethod = setupType.GetMethod("SetupScene",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    setupMethod?.Invoke(null, null);
                    Debug.Log("[QuestBuilder] XRSceneSetup applied to MainScene.");
                }

                // Save scene
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, SCENE_PATH);
                AssetDatabase.Refresh();
                Debug.Log($"[QuestBuilder] MainScene saved: {SCENE_PATH}");
            }

            // Add to build settings
            var currentScenes = EditorBuildSettings.scenes;
            bool sceneInBuild = currentScenes.Any(s => s.path == SCENE_PATH);
            if (!sceneInBuild)
            {
                var newScenes = new EditorBuildSettingsScene[currentScenes.Length + 1];
                currentScenes.CopyTo(newScenes, 0);
                newScenes[newScenes.Length - 1] = new EditorBuildSettingsScene(SCENE_PATH, true);
                EditorBuildSettings.scenes = newScenes;
                Debug.Log($"[QuestBuilder] Added {SCENE_PATH} to build settings.");
            }
        }

        private static void BuildAPK()
        {
            Debug.Log($"[QuestBuilder] Building APK to {APK_PATH}...");

            // Ensure output directory exists
            var buildDir = Path.GetDirectoryName(APK_PATH);
            if (!string.IsNullOrEmpty(buildDir) && !Directory.Exists(buildDir))
                Directory.CreateDirectory(buildDir);

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[QuestBuilder] No scenes in build settings!");
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = APK_PATH,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[QuestBuilder] BUILD SUCCEEDED! APK: {APK_PATH} ({summary.totalSize / (1024*1024)}MB)");
                Debug.Log($"[QuestBuilder] Install: adb install -r {APK_PATH}");
            }
            else
            {
                Debug.LogError($"[QuestBuilder] BUILD FAILED: {summary.result}");
                Debug.LogError($"[QuestBuilder] Errors: {summary.totalErrors}, Warnings: {summary.totalWarnings}");

                foreach (var step in report.steps)
                {
                    foreach (var msg in step.messages)
                    {
                        if (msg.type == LogType.Error)
                            Debug.LogError($"  {msg.content}");
                    }
                }
            }
        }
    }
}
#endif
