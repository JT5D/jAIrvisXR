using UnityEngine;

namespace jAIrvisXR.Core.Config
{
    [CreateAssetMenu(menuName = "jAIrvisXR/Config/Daemon Config")]
    public class DaemonConfig : ScriptableObject
    {
        [Header("Daemon Connection")]
        [SerializeField] private string _host = "127.0.0.1";
        [SerializeField] private int _port = 7437;
        [SerializeField] private float _timeoutSeconds = 10f;
        [SerializeField] private float _healthCheckIntervalSeconds = 30f;

        public string Host => _host;
        public int Port => _port;
        public float TimeoutSeconds => _timeoutSeconds;
        public float HealthCheckIntervalSeconds => _healthCheckIntervalSeconds;
        public string BaseUrl => $"http://{_host}:{_port}";
    }
}
