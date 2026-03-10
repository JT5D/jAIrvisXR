using System;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace jAIrvisXR.XR.Interaction
{
    /// <summary>
    /// Activates voice pipeline via hand gesture (pinch) when controllers are absent.
    /// Uses XR Hands subsystem to detect thumb-to-index pinch as push-to-talk.
    /// </summary>
    public class HandGestureVoiceActivation : MonoBehaviour
    {
        [SerializeField] private float _pinchThreshold = 0.02f;
        [SerializeField] private Handedness _activationHand = Handedness.Left;

        public event Action OnGestureActivated;
        public event Action OnGestureDeactivated;

        private XRHandSubsystem _handSubsystem;
        private bool _isPinching;

        private void Update()
        {
            if (_handSubsystem == null || !_handSubsystem.running)
            {
                TryGetHandSubsystem();
                return;
            }

            var hand = _activationHand == Handedness.Left
                ? _handSubsystem.leftHand
                : _handSubsystem.rightHand;

            if (!hand.isTracked) return;

            bool thumbOk = hand.GetJoint(XRHandJointID.ThumbTip)
                .TryGetPose(out Pose thumbPose);
            bool indexOk = hand.GetJoint(XRHandJointID.IndexTip)
                .TryGetPose(out Pose indexPose);

            if (!thumbOk || !indexOk) return;

            float distance = Vector3.Distance(thumbPose.position, indexPose.position);
            bool pinching = distance < _pinchThreshold;

            if (pinching && !_isPinching)
            {
                _isPinching = true;
                OnGestureActivated?.Invoke();
            }
            else if (!pinching && _isPinching)
            {
                _isPinching = false;
                OnGestureDeactivated?.Invoke();
            }
        }

        private void TryGetHandSubsystem()
        {
            var subsystems = new System.Collections.Generic.List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            if (subsystems.Count > 0)
                _handSubsystem = subsystems[0];
        }
    }
}
