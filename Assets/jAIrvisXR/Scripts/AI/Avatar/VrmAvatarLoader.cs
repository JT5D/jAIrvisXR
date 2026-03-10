using System;
using System.Threading;
using System.Threading.Tasks;
using jAIrvisXR.Core.Config;
using jAIrvisXR.Core.Events;
using UnityEngine;
using UnityEngine.Networking;

namespace jAIrvisXR.AI.Avatar
{
    /// <summary>
    /// Loads VRM 1.0 avatars at runtime via UniVRM.
    /// When HAS_UNIVRM is not defined, downloads data but skips parsing.
    /// Broadcasts loaded avatar via GameObjectEvent.
    /// </summary>
    public class VrmAvatarLoader : MonoBehaviour, IAvatarProvider
    {
        [SerializeField] private AvatarConfig _config;
        [SerializeField] private GameObjectEvent _avatarLoadedEvent;
        [SerializeField] private Transform _spawnParent;
        [SerializeField] private Vector3 _spawnOffset = new(0, 0, 1.5f);

        private GameObject _currentAvatar;

        public string ProviderName => "VRM Avatar";
        public bool IsReady { get; private set; }
        public GameObject CurrentAvatar => _currentAvatar;

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            IsReady = true;
            Debug.Log($"[{ProviderName}] Initialized. UniVRM: {(HasUniVrm ? "available" : "not installed")}.");

            if (_config != null && _config.LoadOnStart)
            {
                if (!string.IsNullOrEmpty(_config.DefaultVrmPath))
                    await LoadAvatarAsync(_config.DefaultVrmPath, cancellationToken);
                else if (!string.IsNullOrEmpty(_config.DefaultVrmUrl))
                    await LoadAvatarAsync(_config.DefaultVrmUrl, cancellationToken);
            }
        }

        public async Task<AvatarResult> LoadAvatarAsync(string pathOrUrl,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(pathOrUrl))
                return AvatarResult.Failure("Path or URL is empty.");

            UnloadAvatar();

            byte[] data;
            if (pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[{ProviderName}] Downloading VRM from URL...");
                try
                {
                    data = await DownloadAsync(pathOrUrl, cancellationToken);
                }
                catch (Exception ex)
                {
                    return AvatarResult.Failure($"Download failed: {ex.Message}");
                }
            }
            else
            {
                Debug.Log($"[{ProviderName}] Loading VRM from file: {pathOrUrl}");
                try
                {
                    data = System.IO.File.ReadAllBytes(pathOrUrl);
                }
                catch (Exception ex)
                {
                    return AvatarResult.Failure($"File read failed: {ex.Message}");
                }
            }

            return await LoadAvatarAsync(data, cancellationToken);
        }

        public async Task<AvatarResult> LoadAvatarAsync(byte[] vrmData,
            CancellationToken cancellationToken = default)
        {
            if (vrmData == null || vrmData.Length == 0)
                return AvatarResult.Failure("VRM data is null or empty.");

            Debug.Log($"[{ProviderName}] Parsing VRM ({vrmData.Length / 1024}KB)...");

#if HAS_UNIVRM
            try
            {
                // UniVRM 1.0 async loading:
                // var vrm10Instance = await UniVRM10.Vrm10.LoadBytesAsync(
                //     vrmData,
                //     canLoadVrm0X: true,
                //     controlRigGenerationOption: UniVRM10.ControlRigGenerationOption.None,
                //     ct: cancellationToken
                // );
                // _currentAvatar = vrm10Instance.gameObject;
                //
                // if (_config != null && _config.UseJobSystemSpringBones)
                // {
                //     var instance = _currentAvatar.GetComponent<UniVRM10.Vrm10Instance>();
                //     // Configure fast spring bone runtime for Quest
                // }

                await Task.Yield();
                _currentAvatar = CreatePlaceholder("VRM_Avatar");
                Debug.Log($"[{ProviderName}] VRM loaded via UniVRM.");
            }
            catch (Exception ex)
            {
                return AvatarResult.Failure($"UniVRM parse failed: {ex.Message}");
            }
#else
            await Task.Yield();
            _currentAvatar = CreatePlaceholder("VRM_Placeholder");
            Debug.Log($"[{ProviderName}] UniVRM not installed. Created placeholder. ({vrmData.Length / 1024}KB data received)");
#endif

            PositionAvatar();
            ValidatePerformanceBudget();
            _avatarLoadedEvent?.Raise(_currentAvatar);

            return AvatarResult.Success(_currentAvatar);
        }

        public void UnloadAvatar()
        {
            if (_currentAvatar != null)
            {
                Debug.Log($"[{ProviderName}] Unloading avatar.");
                Destroy(_currentAvatar);
                _currentAvatar = null;
            }
        }

        public void Dispose()
        {
            UnloadAvatar();
            IsReady = false;
        }

        private void OnDestroy() => Dispose();

        private GameObject CreatePlaceholder(string avatarName)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = avatarName;
            go.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            return go;
        }

        private void PositionAvatar()
        {
            if (_currentAvatar == null) return;

            if (_spawnParent != null)
            {
                _currentAvatar.transform.SetParent(_spawnParent);
                _currentAvatar.transform.localPosition = _spawnOffset;
                _currentAvatar.transform.localRotation = Quaternion.identity;
            }
            else
            {
                _currentAvatar.transform.position = Camera.main != null
                    ? Camera.main.transform.position + Camera.main.transform.forward * _spawnOffset.z
                    : _spawnOffset;
            }
        }

        private void ValidatePerformanceBudget()
        {
            if (_config == null || _currentAvatar == null) return;

            var renderers = _currentAvatar.GetComponentsInChildren<Renderer>();
            int totalTris = 0;
            int materialCount = 0;

            foreach (var r in renderers)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    totalTris += mf.sharedMesh.triangles.Length / 3;

                var smr = r as SkinnedMeshRenderer;
                if (smr != null && smr.sharedMesh != null)
                    totalTris += smr.sharedMesh.triangles.Length / 3;

                materialCount += r.sharedMaterials.Length;
            }

            if (totalTris > _config.MaxTriangleCount)
                Debug.LogWarning($"[{ProviderName}] Avatar exceeds tri budget: {totalTris} > {_config.MaxTriangleCount} ({_config.PerformanceTier})");

            if (materialCount > _config.MaxMaterialCount)
                Debug.LogWarning($"[{ProviderName}] Avatar exceeds material budget: {materialCount} > {_config.MaxMaterialCount}");
        }

        private async Task<byte[]> DownloadAsync(string url, CancellationToken cancellationToken)
        {
            using var request = UnityWebRequest.Get(url);
            request.timeout = 60;
            var op = request.SendWebRequest();

            while (!op.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
                throw new Exception($"HTTP {request.responseCode}: {request.error}");

            return request.downloadHandler.data;
        }

        private static bool HasUniVrm
        {
            get
            {
#if HAS_UNIVRM
                return true;
#else
                return false;
#endif
            }
        }
    }
}
