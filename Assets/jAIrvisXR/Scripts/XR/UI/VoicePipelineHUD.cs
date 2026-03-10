using UnityEngine;
using TMPro;
using jAIrvisXR.Core.Events;

namespace jAIrvisXR.XR.UI
{
    /// <summary>
    /// World-space HUD for voice pipeline status.
    /// Displays pipeline state, latest transcript, and agent response.
    /// Attach to a world-space Canvas.
    /// </summary>
    public class VoicePipelineHUD : MonoBehaviour
    {
        [Header("State Indicator")]
        [SerializeField] private TextMeshProUGUI _stateLabel;
        [SerializeField] private UnityEngine.UI.Image _stateIndicator;

        [Header("Text Displays")]
        [SerializeField] private TextMeshProUGUI _transcriptText;
        [SerializeField] private TextMeshProUGUI _responseText;

        [Header("Colors")]
        [SerializeField] private Color _idleColor = new Color(0.4f, 0.4f, 0.4f);
        [SerializeField] private Color _listeningColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] private Color _processingColor = new Color(0.9f, 0.7f, 0.1f);
        [SerializeField] private Color _speakingColor = new Color(0.2f, 0.6f, 1f);
        [SerializeField] private Color _errorColor = new Color(1f, 0.2f, 0.2f);

        public void OnPipelineStateChanged(PipelineState state)
        {
            if (_stateLabel != null)
                _stateLabel.text = state.ToString().ToUpper();

            if (_stateIndicator != null)
            {
                _stateIndicator.color = state switch
                {
                    PipelineState.Idle => _idleColor,
                    PipelineState.Listening => _listeningColor,
                    PipelineState.Processing => _processingColor,
                    PipelineState.Speaking => _speakingColor,
                    PipelineState.Error => _errorColor,
                    _ => _idleColor,
                };
            }
        }

        public void OnTranscriptReady(string transcript)
        {
            if (_transcriptText != null)
                _transcriptText.text = transcript;
        }

        public void OnAgentResponse(string response)
        {
            if (_responseText != null)
                _responseText.text = response;
        }
    }
}
