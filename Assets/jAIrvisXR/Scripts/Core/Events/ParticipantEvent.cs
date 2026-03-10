using System.Collections.Generic;
using UnityEngine;

namespace jAIrvisXR.Core.Events
{
    /// <summary>
    /// SO event channel for participant join/leave notifications.
    /// Payload is participant identity string.
    /// </summary>
    [CreateAssetMenu(menuName = "jAIrvisXR/Events/Participant Event")]
    public class ParticipantEvent : ScriptableObject
    {
        private readonly List<ParticipantEventListener> _listeners = new();

        public void Raise(string participantIdentity)
        {
            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                _listeners[i].OnEventRaised(participantIdentity);
            }
        }

        public void RegisterListener(ParticipantEventListener listener)
        {
            if (!_listeners.Contains(listener))
                _listeners.Add(listener);
        }

        public void UnregisterListener(ParticipantEventListener listener)
        {
            _listeners.Remove(listener);
        }
    }
}
