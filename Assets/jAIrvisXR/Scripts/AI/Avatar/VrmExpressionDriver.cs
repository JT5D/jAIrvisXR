using jAIrvisXR.Core.Config;
using jAIrvisXR.Core.Events;
using UnityEngine;

namespace jAIrvisXR.AI.Avatar
{
    /// <summary>
    /// Maps VoicePipeline states to VRM facial expressions and drives
    /// autonomous blinking. Subscribes to PipelineState via SO event.
    /// </summary>
    public class VrmExpressionDriver : MonoBehaviour
    {
        [SerializeField] private AvatarConfig _config;

        private GameObject _avatarRoot;
        private PipelineState _currentPipelineState = PipelineState.Idle;

        // Blinking state
        private float _nextBlinkTime;
        private float _blinkTimer;
        private bool _isBlinking;

        // Expression lerp
        private float _targetJoy;
        private float _targetSorrow;
        private float _targetFun;
        private float _currentJoy;
        private float _currentSorrow;
        private float _currentFun;

        public void OnAvatarLoaded(GameObject avatar)
        {
            _avatarRoot = avatar;
            Debug.Log($"[ExpressionDriver] Avatar assigned: {(avatar != null ? avatar.name : "null")}");
            ScheduleNextBlink();
        }

        public void OnPipelineStateChanged(PipelineState state)
        {
            _currentPipelineState = state;
            UpdateExpressionTargets();
        }

        private void Update()
        {
            if (_avatarRoot == null) return;
            if (_config != null && !_config.EnableExpressions) return;

            float transitionTime = _config != null ? _config.ExpressionTransitionTime : 0.3f;

            // Lerp expressions
            _currentJoy = Mathf.MoveTowards(_currentJoy, _targetJoy, Time.deltaTime / transitionTime);
            _currentSorrow = Mathf.MoveTowards(_currentSorrow, _targetSorrow, Time.deltaTime / transitionTime);
            _currentFun = Mathf.MoveTowards(_currentFun, _targetFun, Time.deltaTime / transitionTime);

            // Autonomous blinking
            UpdateBlink();

#if HAS_UNIVRM
            ApplyToVrm();
#endif
        }

        private void UpdateExpressionTargets()
        {
            _targetJoy = 0f;
            _targetSorrow = 0f;
            _targetFun = 0f;

            switch (_currentPipelineState)
            {
                case PipelineState.Idle:
                    // Neutral — all zero
                    break;
                case PipelineState.Listening:
                    _targetFun = 0.3f; // Attentive/interested
                    break;
                case PipelineState.Processing:
                    _targetFun = 0.15f; // Thinking
                    break;
                case PipelineState.Speaking:
                    _targetJoy = 0.4f; // Animated while talking
                    break;
                case PipelineState.Error:
                    _targetSorrow = 0.5f;
                    break;
            }
        }

        private void UpdateBlink()
        {
            float blinkDuration = _config != null ? _config.BlinkDuration : 0.15f;

            if (_isBlinking)
            {
                _blinkTimer += Time.deltaTime;
                if (_blinkTimer >= blinkDuration)
                {
                    _isBlinking = false;
                    _blinkTimer = 0f;
                    ScheduleNextBlink();
                }
            }
            else if (Time.time >= _nextBlinkTime)
            {
                _isBlinking = true;
                _blinkTimer = 0f;
            }
        }

        private void ScheduleNextBlink()
        {
            float min = _config != null ? _config.BlinkIntervalMin : 2f;
            float max = _config != null ? _config.BlinkIntervalMax : 6f;
            _nextBlinkTime = Time.time + Random.Range(min, max);
        }

        public float BlinkWeight => _isBlinking
            ? Mathf.PingPong(_blinkTimer / ((_config != null ? _config.BlinkDuration : 0.15f) * 0.5f), 1f)
            : 0f;

#if HAS_UNIVRM
        private void ApplyToVrm()
        {
            if (_avatarRoot == null) return;
            // var vrm = _avatarRoot.GetComponent<UniVRM10.Vrm10Instance>();
            // if (vrm == null) return;
            // var expression = vrm.Runtime.Expression;
            // expression.SetWeight(UniVRM10.ExpressionKey.Happy, _currentJoy);
            // expression.SetWeight(UniVRM10.ExpressionKey.Sad, _currentSorrow);
            // expression.SetWeight(UniVRM10.ExpressionKey.Relaxed, _currentFun);
            // expression.SetWeight(UniVRM10.ExpressionKey.BlinkLeft, BlinkWeight);
            // expression.SetWeight(UniVRM10.ExpressionKey.BlinkRight, BlinkWeight);
        }
#endif
    }
}
