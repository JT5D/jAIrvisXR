using UnityEngine;
using UnityEngine.Events;

namespace jAIrvisXR.Core.Events
{
    public class GameObjectEventListener : MonoBehaviour
    {
        [SerializeField] private GameObjectEvent _event;
        [SerializeField] private UnityEvent<GameObject> _response;

        private void OnEnable() => _event?.RegisterListener(this);
        private void OnDisable() => _event?.UnregisterListener(this);

        public void OnEventRaised(GameObject value)
        {
            _response?.Invoke(value);
        }
    }
}
