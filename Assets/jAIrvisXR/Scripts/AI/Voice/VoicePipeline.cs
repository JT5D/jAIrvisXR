using System;
using System.Threading;
using System.Threading.Tasks;
using jAIrvisXR.AI.Agent;
using jAIrvisXR.Core.Config;
using jAIrvisXR.Core.Events;
using UnityEngine;

namespace jAIrvisXR.AI.Voice
{
    public class VoicePipeline : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private VoicePipelineConfig _pipelineConfig;

        [Header("Components")]
        [SerializeField] private VoiceActivation _voiceActivation;
        [SerializeField] private MicrophoneCapture _microphoneCapture;
        [SerializeField] private AudioPlaybackHandler _audioPlayback;

        [Header("Providers (assign MonoBehaviours implementing the interfaces)")]
        [SerializeField] private MonoBehaviour _sttProviderComponent;
        [SerializeField] private MonoBehaviour _ttsProviderComponent;
        [SerializeField] private MonoBehaviour _agentServiceComponent;

        [Header("Events")]
        [SerializeField] private VoicePipelineEvent _pipelineStateChangedEvent;
        [SerializeField] private StringEvent _transcriptReadyEvent;
        [SerializeField] private StringEvent _agentResponseReadyEvent;

        private ISTTProvider _sttProvider;
        private ITTSProvider _ttsProvider;
        private IAgentService _agentService;

        private PipelineState _currentState = PipelineState.Idle;
        private CancellationTokenSource _pipelineCts;
        private float _lastSpeechTime;

        public PipelineState CurrentState => _currentState;

        private async void Start()
        {
            _sttProvider = _sttProviderComponent as ISTTProvider;
            _ttsProvider = _ttsProviderComponent as ITTSProvider;
            _agentService = _agentServiceComponent as IAgentService;

            if (_sttProvider == null)
                Debug.LogError("[VoicePipeline] STT provider component does not implement ISTTProvider.");
            if (_ttsProvider == null)
                Debug.LogError("[VoicePipeline] TTS provider component does not implement ITTSProvider.");
            if (_agentService == null)
                Debug.LogError("[VoicePipeline] Agent service component does not implement IAgentService.");

            var cts = new CancellationTokenSource();
            try
            {
                if (_sttProvider != null) await _sttProvider.InitializeAsync(cts.Token);
                if (_ttsProvider != null) await _ttsProvider.InitializeAsync(cts.Token);
                if (_agentService != null) await _agentService.InitializeAsync(cts.Token);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoicePipeline] Provider initialization failed: {ex.Message}");
                SetState(PipelineState.Error);
                return;
            }

            if (_voiceActivation != null)
            {
                _voiceActivation.OnActivationStarted += HandleActivationStarted;
                _voiceActivation.OnActivationEnded += HandleActivationEnded;
            }

            Debug.Log("[VoicePipeline] All providers initialized. Pipeline ready.");
            SetState(PipelineState.Idle);
        }

        private void OnDestroy()
        {
            _pipelineCts?.Cancel();
            _pipelineCts?.Dispose();

            if (_voiceActivation != null)
            {
                _voiceActivation.OnActivationStarted -= HandleActivationStarted;
                _voiceActivation.OnActivationEnded -= HandleActivationEnded;
            }

            _sttProvider?.Dispose();
            _ttsProvider?.Dispose();
            _agentService?.Dispose();
        }

        private void Update()
        {
            if (_currentState == PipelineState.Listening
                && _pipelineConfig.UseWakeWord
                && !_pipelineConfig.UsePushToTalk)
            {
                float audioLevel = _microphoneCapture.GetCurrentAudioLevel();
                if (audioLevel > _pipelineConfig.SilenceThreshold)
                {
                    _lastSpeechTime = Time.time;
                }
                else if (Time.time - _lastSpeechTime > _pipelineConfig.SilenceTimeoutSeconds)
                {
                    _voiceActivation.DeactivateFromSilence();
                }

                if (_microphoneCapture.IsRecording &&
                    Time.time - _lastSpeechTime > _pipelineConfig.MaxRecordingSeconds)
                {
                    _voiceActivation.DeactivateFromSilence();
                }
            }
        }

        private void HandleActivationStarted()
        {
            if (_currentState != PipelineState.Idle) return;

            _audioPlayback.StopPlayback();
            _microphoneCapture.StartRecording();
            _lastSpeechTime = Time.time;
            SetState(PipelineState.Listening);
        }

        private async void HandleActivationEnded()
        {
            if (_currentState != PipelineState.Listening) return;

            AudioClip recordedAudio = _microphoneCapture.StopRecording();
            if (recordedAudio == null || recordedAudio.length < 0.1f)
            {
                Debug.LogWarning("[VoicePipeline] Recording too short, discarding.");
                SetState(PipelineState.Idle);
                return;
            }

            _pipelineCts?.Cancel();
            _pipelineCts = new CancellationTokenSource();
            var token = _pipelineCts.Token;

            try
            {
                await RunPipelineAsync(recordedAudio, token);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[VoicePipeline] Pipeline cancelled.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoicePipeline] Pipeline error: {ex.Message}");
                SetState(PipelineState.Error);
                await Task.Delay(1000);
            }
            finally
            {
                SetState(PipelineState.Idle);
            }
        }

        private async Task RunPipelineAsync(AudioClip audio, CancellationToken token)
        {
            // Phase 1: STT
            SetState(PipelineState.Processing);
            Debug.Log("[VoicePipeline] Phase 1: Speech-to-Text...");

            STTResult sttResult = await _sttProvider.TranscribeAsync(audio, token);
            if (!sttResult.IsSuccess)
            {
                Debug.LogError($"[VoicePipeline] STT failed: {sttResult.ErrorMessage}");
                return;
            }

            string transcript = sttResult.Transcript;
            Debug.Log($"[VoicePipeline] Transcript: \"{transcript}\" (confidence: {sttResult.Confidence:F2})");
            _transcriptReadyEvent?.Raise(transcript);

            if (string.IsNullOrWhiteSpace(transcript))
            {
                Debug.Log("[VoicePipeline] Empty transcript, skipping agent.");
                return;
            }

            // Phase 2: Agent
            Debug.Log("[VoicePipeline] Phase 2: Agent processing...");
            var agentRequest = new AgentRequest { UserMessage = transcript };
            AgentResponse agentResponse = await _agentService.SendMessageAsync(agentRequest, token);

            if (!agentResponse.IsSuccess)
            {
                Debug.LogError($"[VoicePipeline] Agent failed: {agentResponse.ErrorMessage}");
                return;
            }

            string responseText = agentResponse.Text;
            Debug.Log($"[VoicePipeline] Agent response: \"{responseText}\"");
            _agentResponseReadyEvent?.Raise(responseText);

            // Phase 3: TTS
            SetState(PipelineState.Speaking);
            Debug.Log("[VoicePipeline] Phase 3: Text-to-Speech...");

            TTSResult ttsResult = await _ttsProvider.SynthesizeAsync(responseText, token);
            if (!ttsResult.IsSuccess)
            {
                Debug.LogError($"[VoicePipeline] TTS failed: {ttsResult.ErrorMessage}");
                return;
            }

            // Phase 4: Playback
            Debug.Log("[VoicePipeline] Phase 4: Playback...");
            await _audioPlayback.PlayAsync(ttsResult.AudioClip, token);
            Debug.Log("[VoicePipeline] Pipeline complete.");
        }

        private void SetState(PipelineState newState)
        {
            if (_currentState == newState) return;
            Debug.Log($"[VoicePipeline] State: {_currentState} -> {newState}");
            _currentState = newState;
            _pipelineStateChangedEvent?.Raise(newState);
        }
    }
}
