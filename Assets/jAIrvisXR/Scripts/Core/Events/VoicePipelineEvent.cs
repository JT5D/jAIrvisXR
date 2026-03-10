using System.Collections.Generic;
using UnityEngine;

namespace jAIrvisXR.Core.Events
{
    [CreateAssetMenu(menuName = "jAIrvisXR/Events/Voice Pipeline Event")]
    public class VoicePipelineEvent : ScriptableObject
    {
        private readonly List<VoicePipelineEventListener> _listeners = new();

        public void Raise(PipelineState state)
        {
            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                _listeners[i].OnEventRaised(state);
            }
        }

        public void RegisterListener(VoicePipelineEventListener listener)
        {
            if (!_listeners.Contains(listener))
                _listeners.Add(listener);
        }

        public void UnregisterListener(VoicePipelineEventListener listener)
        {
            _listeners.Remove(listener);
        }
    }
}
