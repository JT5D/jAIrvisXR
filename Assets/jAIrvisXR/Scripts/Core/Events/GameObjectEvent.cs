using System.Collections.Generic;
using UnityEngine;

namespace jAIrvisXR.Core.Events
{
    /// <summary>
    /// SO event channel for broadcasting GameObject references.
    /// Used for avatar loaded notifications.
    /// </summary>
    [CreateAssetMenu(menuName = "jAIrvisXR/Events/GameObject Event")]
    public class GameObjectEvent : ScriptableObject
    {
        private readonly List<GameObjectEventListener> _listeners = new();

        public void Raise(GameObject value)
        {
            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                _listeners[i].OnEventRaised(value);
            }
        }

        public void RegisterListener(GameObjectEventListener listener)
        {
            if (!_listeners.Contains(listener))
                _listeners.Add(listener);
        }

        public void UnregisterListener(GameObjectEventListener listener)
        {
            _listeners.Remove(listener);
        }
    }
}
