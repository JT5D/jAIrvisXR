using UnityEngine;

namespace jAIrvisXR.Core.Config
{
    public enum AvatarPerformanceTier
    {
        Quest,   // <15K tris, 1024x1024, 1-2 materials
        PCVR,    // <32K tris, 2048x2048, 4 materials
        Desktop  // <70K tris, 4096x4096, 8 materials
    }

    [CreateAssetMenu(menuName = "jAIrvisXR/Config/Avatar Config")]
    public class AvatarConfig : ScriptableObject
    {
        [Header("Default Avatar")]
        [SerializeField] private string _defaultVrmPath;
        [SerializeField] private string _defaultVrmUrl;
        [SerializeField] private bool _loadOnStart = true;

        [Header("Performance")]
        [SerializeField] private AvatarPerformanceTier _performanceTier = AvatarPerformanceTier.Quest;
        [SerializeField] private int _maxTriangleCount = 15000;
        [SerializeField] private int _maxTextureSize = 1024;
        [SerializeField] private int _maxMaterialCount = 2;

        [Header("Lip Sync")]
        [SerializeField, Range(0.01f, 1f)] private float _lipSyncSensitivity = 0.15f;
        [SerializeField, Range(0.05f, 0.5f)] private float _lipSyncSmoothTime = 0.1f;
        [SerializeField, Range(0f, 1f)] private float _lipSyncMaxWeight = 1f;

        [Header("Expressions")]
        [SerializeField, Range(0.05f, 1f)] private float _expressionTransitionTime = 0.3f;
        [SerializeField, Range(0.1f, 5f)] private float _blinkIntervalMin = 2f;
        [SerializeField, Range(2f, 8f)] private float _blinkIntervalMax = 6f;
        [SerializeField, Range(0.05f, 0.3f)] private float _blinkDuration = 0.15f;

        [Header("Expressions")]
        [SerializeField] private bool _enableExpressions = true;

        [Header("Spring Bone Physics")]
        [SerializeField] private bool _enableSpringBones = true;
        [SerializeField] private bool _useJobSystemSpringBones = true;

        [Header("VRoid Hub (Optional)")]
        [SerializeField] private string _vroidHubApiKey;
        [SerializeField] private string _vroidHubEnvVarName = "VROID_HUB_API_KEY";

        public string DefaultVrmPath => _defaultVrmPath;
        public string DefaultVrmUrl => _defaultVrmUrl;
        public bool LoadOnStart => _loadOnStart;
        public AvatarPerformanceTier PerformanceTier => _performanceTier;
        public int MaxTriangleCount => _maxTriangleCount;
        public int MaxTextureSize => _maxTextureSize;
        public int MaxMaterialCount => _maxMaterialCount;
        public float LipSyncSensitivity => _lipSyncSensitivity;
        public float LipSyncSmoothTime => _lipSyncSmoothTime;
        public float LipSyncMaxWeight => _lipSyncMaxWeight;
        public float ExpressionTransitionTime => _expressionTransitionTime;
        public float BlinkIntervalMin => _blinkIntervalMin;
        public float BlinkIntervalMax => _blinkIntervalMax;
        public float BlinkDuration => _blinkDuration;
        public bool EnableExpressions => _enableExpressions;
        public bool EnableSpringBones => _enableSpringBones;
        public bool UseJobSystemSpringBones => _useJobSystemSpringBones;
        public string VRoidHubApiKey => string.IsNullOrEmpty(_vroidHubApiKey)
            ? System.Environment.GetEnvironmentVariable(_vroidHubEnvVarName)
            : _vroidHubApiKey;
    }
}
