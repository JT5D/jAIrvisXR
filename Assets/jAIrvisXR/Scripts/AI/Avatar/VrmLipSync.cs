using jAIrvisXR.Core.Config;
using UnityEngine;

namespace jAIrvisXR.AI.Avatar
{
    /// <summary>
    /// Drives VRM lip sync blendshapes (A/I/U/E/O) from AudioSource output.
    /// Uses spectrum analysis to estimate viseme weights in real-time.
    /// Works with or without UniVRM — stores weights that VrmExpressionDriver applies.
    /// </summary>
    public class VrmLipSync : MonoBehaviour
    {
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AvatarConfig _config;

        private const int SPECTRUM_SIZE = 256;
        private float[] _spectrum = new float[SPECTRUM_SIZE];
        private float[] _outputSamples = new float[1024];

        // Current viseme weights (0-1) — read by VrmExpressionDriver
        public float WeightA { get; private set; }
        public float WeightI { get; private set; }
        public float WeightU { get; private set; }
        public float WeightE { get; private set; }
        public float WeightO { get; private set; }
        public bool IsSpeaking { get; private set; }

        private float _smoothA, _smoothI, _smoothU, _smoothE, _smoothO;
        private float _velocityA, _velocityI, _velocityU, _velocityE, _velocityO;
        private GameObject _avatarRoot;

        public void OnAvatarLoaded(GameObject avatar)
        {
            _avatarRoot = avatar;
            Debug.Log($"[LipSync] Avatar assigned: {(avatar != null ? avatar.name : "null")}");
        }

        private void Update()
        {
            if (_audioSource == null || !_audioSource.isPlaying)
            {
                DecayWeights();
                IsSpeaking = false;
                return;
            }

            float sensitivity = _config != null ? _config.LipSyncSensitivity : 0.15f;
            float smoothTime = _config != null ? _config.LipSyncSmoothTime : 0.1f;
            float maxWeight = _config != null ? _config.LipSyncMaxWeight : 1f;

            // Get volume level
            _audioSource.GetOutputData(_outputSamples, 0);
            float rms = 0f;
            for (int i = 0; i < _outputSamples.Length; i++)
                rms += _outputSamples[i] * _outputSamples[i];
            rms = Mathf.Sqrt(rms / _outputSamples.Length);

            IsSpeaking = rms > sensitivity * 0.5f;
            if (!IsSpeaking)
            {
                DecayWeights();
                return;
            }

            // Spectrum analysis — 3 frequency bands
            _audioSource.GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);

            // Low frequencies (vowels like A, O) — bins 1-8 (~86-690Hz)
            float low = 0f;
            for (int i = 1; i <= 8; i++) low += _spectrum[i];
            low /= 8f;

            // Mid frequencies (vowels like E, I) — bins 9-32 (~690-2760Hz)
            float mid = 0f;
            for (int i = 9; i <= 32; i++) mid += _spectrum[i];
            mid /= 24f;

            // High frequencies (sibilants) — bins 33-64 (~2760-5520Hz)
            float high = 0f;
            for (int i = 33; i <= 64; i++) high += _spectrum[i];
            high /= 32f;

            // Map frequency bands to visemes
            float targetA = Mathf.Clamp01(low * 20f * rms / sensitivity) * maxWeight;
            float targetI = Mathf.Clamp01(high * 30f * rms / sensitivity) * maxWeight;
            float targetU = Mathf.Clamp01((low - mid) * 15f * rms / sensitivity) * maxWeight;
            float targetE = Mathf.Clamp01(mid * 25f * rms / sensitivity) * maxWeight;
            float targetO = Mathf.Clamp01((low * 0.5f + mid * 0.3f) * 20f * rms / sensitivity) * maxWeight;

            // Smooth transitions
            WeightA = Mathf.SmoothDamp(_smoothA, targetA, ref _velocityA, smoothTime);
            WeightI = Mathf.SmoothDamp(_smoothI, targetI, ref _velocityI, smoothTime);
            WeightU = Mathf.SmoothDamp(_smoothU, targetU, ref _velocityU, smoothTime);
            WeightE = Mathf.SmoothDamp(_smoothE, targetE, ref _velocityE, smoothTime);
            WeightO = Mathf.SmoothDamp(_smoothO, targetO, ref _velocityO, smoothTime);

            _smoothA = WeightA;
            _smoothI = WeightI;
            _smoothU = WeightU;
            _smoothE = WeightE;
            _smoothO = WeightO;

#if HAS_UNIVRM
            ApplyToVrm();
#endif
        }

        private void DecayWeights()
        {
            float smoothTime = _config != null ? _config.LipSyncSmoothTime : 0.1f;
            WeightA = Mathf.SmoothDamp(_smoothA, 0f, ref _velocityA, smoothTime);
            WeightI = Mathf.SmoothDamp(_smoothI, 0f, ref _velocityI, smoothTime);
            WeightU = Mathf.SmoothDamp(_smoothU, 0f, ref _velocityU, smoothTime);
            WeightE = Mathf.SmoothDamp(_smoothE, 0f, ref _velocityE, smoothTime);
            WeightO = Mathf.SmoothDamp(_smoothO, 0f, ref _velocityO, smoothTime);
            _smoothA = WeightA;
            _smoothI = WeightI;
            _smoothU = WeightU;
            _smoothE = WeightE;
            _smoothO = WeightO;
        }

#if HAS_UNIVRM
        private void ApplyToVrm()
        {
            if (_avatarRoot == null) return;
            // var vrm = _avatarRoot.GetComponent<UniVRM10.Vrm10Instance>();
            // if (vrm == null) return;
            // var expression = vrm.Runtime.Expression;
            // expression.SetWeight(UniVRM10.ExpressionKey.Aa, WeightA);
            // expression.SetWeight(UniVRM10.ExpressionKey.Ih, WeightI);
            // expression.SetWeight(UniVRM10.ExpressionKey.Ou, WeightU);
            // expression.SetWeight(UniVRM10.ExpressionKey.Ee, WeightE);
            // expression.SetWeight(UniVRM10.ExpressionKey.Oh, WeightO);
        }
#endif
    }
}
