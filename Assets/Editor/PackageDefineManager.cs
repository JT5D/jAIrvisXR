#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;

namespace jAIrvisXR.Editor
{
    /// <summary>
    /// Auto-detects installed packages and sets scripting defines.
    /// Runs on domain reload and after package changes.
    /// </summary>
    [InitializeOnLoad]
    public static class PackageDefineManager
    {
        private static readonly (string packageId, string define)[] PackageDefines =
        {
            ("io.livekit.livekit-sdk", "HAS_LIVEKIT"),
            ("com.vrmc.vrm", "HAS_UNIVRM"),
            ("com.unity.xr.hands", "UNITY_XR_HANDS"),
        };

        static PackageDefineManager()
        {
            Events.registeredPackages += OnPackagesChanged;
            RefreshDefines();
        }

        private static void OnPackagesChanged(PackageRegistrationEventArgs args)
        {
            RefreshDefines();
        }

        private static void RefreshDefines()
        {
            var request = Client.List(true);
            EditorApplication.update += WaitForList;

            void WaitForList()
            {
                if (!request.IsCompleted) return;
                EditorApplication.update -= WaitForList;

                if (request.Status != StatusCode.Success) return;

                var installedIds = new HashSet<string>(
                    request.Result.Select(p => p.name));

                // Process each build target group that matters
                var targets = new[]
                {
                    BuildTargetGroup.Standalone,
                    BuildTargetGroup.Android,
                    BuildTargetGroup.iOS,
                };

                foreach (var target in targets)
                {
                    UpdateDefinesForTarget(target, installedIds);
                }
            }
        }

        private static void UpdateDefinesForTarget(
            BuildTargetGroup target, HashSet<string> installedIds)
        {
            var currentDefines = PlayerSettings
                .GetScriptingDefineSymbolsForGroup(target)
                .Split(';')
                .Where(d => !string.IsNullOrEmpty(d))
                .ToList();

            bool changed = false;

            foreach (var (packageId, define) in PackageDefines)
            {
                bool installed = installedIds.Contains(packageId);
                bool defined = currentDefines.Contains(define);

                if (installed && !defined)
                {
                    currentDefines.Add(define);
                    changed = true;
                    UnityEngine.Debug.Log(
                        $"[PackageDefineManager] Added {define} (package {packageId} detected)");
                }
                else if (!installed && defined)
                {
                    currentDefines.Remove(define);
                    changed = true;
                    UnityEngine.Debug.Log(
                        $"[PackageDefineManager] Removed {define} (package {packageId} not found)");
                }
            }

            if (changed)
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(
                    target, string.Join(";", currentDefines));
            }
        }
    }
}
#endif
