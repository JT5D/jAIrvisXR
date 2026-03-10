using System.Collections.Generic;
using UnityEngine;

namespace jAIrvisXR.Core.Events
{
    [CreateAssetMenu(menuName = "jAIrvisXR/Events/String Event")]
    public class StringEvent : ScriptableObject
    {
        private readonly List<StringEventListener> _listeners = new();

        public void Raise(string value)
        {
            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                _listeners[i].OnEventRaised(value);
            }
        }

        public void RegisterListener(StringEventListener listener)
        {
            if (!_listeners.Contains(listener))
                _listeners.Add(listener);
        }

        public void UnregisterListener(StringEventListener listener)
        {
            _listeners.Remove(listener);
        }
    }
}
