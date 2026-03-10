using UnityEngine;
using UnityEngine.Events;

namespace jAIrvisXR.Core.Events
{
    public class VoicePipelineEventListener : MonoBehaviour
    {
        [SerializeField] private VoicePipelineEvent _event;
        [SerializeField] private UnityEvent<PipelineState> _response;

        private void OnEnable() => _event?.RegisterListener(this);
        private void OnDisable() => _event?.UnregisterListener(this);

        public void OnEventRaised(PipelineState state)
        {
            _response?.Invoke(state);
        }
    }
}
