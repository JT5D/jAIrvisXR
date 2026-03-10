using System.Collections.Generic;
using UnityEngine;

namespace jAIrvisXR.Core.Events
{
    [CreateAssetMenu(menuName = "jAIrvisXR/Events/AudioClip Event")]
    public class AudioClipEvent : ScriptableObject
    {
        private readonly List<AudioClipEventListener> _listeners = new();

        public void Raise(AudioClip clip)
        {
            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                _listeners[i].OnEventRaised(clip);
            }
        }

        public void RegisterListener(AudioClipEventListener listener)
        {
            if (!_listeners.Contains(listener))
                _listeners.Add(listener);
        }

        public void UnregisterListener(AudioClipEventListener listener)
        {
            _listeners.Remove(listener);
        }
    }
}
