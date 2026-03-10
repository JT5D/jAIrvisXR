using System.Threading;
using System.Threading.Tasks;
using jAIrvisXR.Core.Events;
using UnityEngine;

namespace jAIrvisXR.AI.Avatar
{
    /// <summary>
    /// Mock avatar provider that spawns a primitive humanoid shape.
    /// Useful for testing the pipeline without UniVRM installed.
    /// </summary>
    public class MockAvatarProvider : MonoBehaviour, IAvatarProvider
    {
        [SerializeField] private float _simulatedDelaySeconds = 0.5f;
        [SerializeField] private GameObjectEvent _avatarLoadedEvent;
        [SerializeField] private Color _avatarColor = new(0.3f, 0.6f, 1f, 1f);

        private GameObject _currentAvatar;

        public string ProviderName => "Mock Avatar";
        public bool IsReady { get; private set; }
        public GameObject CurrentAvatar => _currentAvatar;

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            IsReady = true;
            Debug.Log($"[{ProviderName}] Initialized (mock).");
            return Task.CompletedTask;
        }

        public async Task<AvatarResult> LoadAvatarAsync(string pathOrUrl,
            CancellationToken cancellationToken = default)
        {
            Debug.Log($"[{ProviderName}] Loading avatar from '{pathOrUrl}' (mock)...");
            await Task.Delay((int)(_simulatedDelaySeconds * 1000), cancellationToken);
            return CreateMockAvatar();
        }

        public async Task<AvatarResult> LoadAvatarAsync(byte[] vrmData,
            CancellationToken cancellationToken = default)
        {
            Debug.Log($"[{ProviderName}] Loading avatar from {vrmData?.Length ?? 0} bytes (mock)...");
            await Task.Delay((int)(_simulatedDelaySeconds * 1000), cancellationToken);
            return CreateMockAvatar();
        }

        public void UnloadAvatar()
        {
            if (_currentAvatar != null)
            {
                Destroy(_currentAvatar);
                _currentAvatar = null;
                Debug.Log($"[{ProviderName}] Avatar unloaded (mock).");
            }
        }

        public void Dispose()
        {
            UnloadAvatar();
            IsReady = false;
        }

        private void OnDestroy() => Dispose();

        private AvatarResult CreateMockAvatar()
        {
            UnloadAvatar();

            _currentAvatar = new GameObject("MockAvatar");

            // Body
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(_currentAvatar.transform);
            body.transform.localPosition = new Vector3(0, 1f, 0);
            body.transform.localScale = new Vector3(0.4f, 0.5f, 0.3f);

            // Head
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(_currentAvatar.transform);
            head.transform.localPosition = new Vector3(0, 1.7f, 0);
            head.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

            // Apply color
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = _avatarColor;
            body.GetComponent<Renderer>().material = mat;
            head.GetComponent<Renderer>().material = mat;

            // Position in front of camera
            if (Camera.main != null)
            {
                _currentAvatar.transform.position =
                    Camera.main.transform.position + Camera.main.transform.forward * 1.5f;
            }
            else
            {
                _currentAvatar.transform.position = new Vector3(0, 0, 1.5f);
            }

            _avatarLoadedEvent?.Raise(_currentAvatar);
            Debug.Log($"[{ProviderName}] Mock avatar created.");
            return AvatarResult.Success(_currentAvatar);
        }
    }
}
