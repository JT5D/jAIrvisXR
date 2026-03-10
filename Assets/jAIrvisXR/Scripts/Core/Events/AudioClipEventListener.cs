using UnityEngine;
using UnityEngine.Events;

namespace jAIrvisXR.Core.Events
{
    public class AudioClipEventListener : MonoBehaviour
    {
        [SerializeField] private AudioClipEvent _event;
        [SerializeField] private UnityEvent<AudioClip> _response;

        private void OnEnable() => _event?.RegisterListener(this);
        private void OnDisable() => _event?.UnregisterListener(this);

        public void OnEventRaised(AudioClip clip)
        {
            _response?.Invoke(clip);
        }
    }
}
