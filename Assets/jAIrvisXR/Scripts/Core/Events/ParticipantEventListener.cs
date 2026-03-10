using UnityEngine;
using UnityEngine.Events;

namespace jAIrvisXR.Core.Events
{
    public class ParticipantEventListener : MonoBehaviour
    {
        [SerializeField] private ParticipantEvent _event;
        [SerializeField] private UnityEvent<string> _response;

        private void OnEnable() => _event?.RegisterListener(this);
        private void OnDisable() => _event?.UnregisterListener(this);

        public void OnEventRaised(string participantIdentity)
        {
            _response?.Invoke(participantIdentity);
        }
    }
}
