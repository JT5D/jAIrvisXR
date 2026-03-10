using UnityEngine;
using UnityEngine.Events;

namespace jAIrvisXR.Core.Events
{
    public class StringEventListener : MonoBehaviour
    {
        [SerializeField] private StringEvent _event;
        [SerializeField] private UnityEvent<string> _response;

        private void OnEnable() => _event?.RegisterListener(this);
        private void OnDisable() => _event?.UnregisterListener(this);

        public void OnEventRaised(string value)
        {
            _response?.Invoke(value);
        }
    }
}
