using System.Collections.Generic;
using UnityEngine;

namespace jAIrvisXR.AI.Avatar
{
    /// <summary>
    /// Maps XR Hand joint rotations to VRM humanoid finger bones.
    /// Uses Unity's XR Hands subsystem (com.unity.xr.hands).
    /// When XR Hands is unavailable, does nothing gracefully.
    /// </summary>
    public class VrmHandTrackingMapper : MonoBehaviour
    {
        private GameObject _avatarRoot;
        private Animator _animator;
        private bool _hasXrHands;

        // XR Hand joint → HumanBodyBones mapping
        private static readonly Dictionary<int, HumanBodyBones> LeftHandMap = new()
        {
            // Thumb
            { 2, HumanBodyBones.LeftThumbProximal },
            { 3, HumanBodyBones.LeftThumbIntermediate },
            { 4, HumanBodyBones.LeftThumbDistal },
            // Index
            { 6, HumanBodyBones.LeftIndexProximal },
            { 7, HumanBodyBones.LeftIndexIntermediate },
            { 8, HumanBodyBones.LeftIndexDistal },
            // Middle
            { 11, HumanBodyBones.LeftMiddleProximal },
            { 12, HumanBodyBones.LeftMiddleIntermediate },
            { 13, HumanBodyBones.LeftMiddleDistal },
            // Ring
            { 16, HumanBodyBones.LeftRingProximal },
            { 17, HumanBodyBones.LeftRingIntermediate },
            { 18, HumanBodyBones.LeftRingDistal },
            // Little
            { 21, HumanBodyBones.LeftLittleProximal },
            { 22, HumanBodyBones.LeftLittleIntermediate },
            { 23, HumanBodyBones.LeftLittleDistal },
        };

        private static readonly Dictionary<int, HumanBodyBones> RightHandMap = new()
        {
            // Thumb
            { 2, HumanBodyBones.RightThumbProximal },
            { 3, HumanBodyBones.RightThumbIntermediate },
            { 4, HumanBodyBones.RightThumbDistal },
            // Index
            { 6, HumanBodyBones.RightIndexProximal },
            { 7, HumanBodyBones.RightIndexIntermediate },
            { 8, HumanBodyBones.RightIndexDistal },
            // Middle
            { 11, HumanBodyBones.RightMiddleProximal },
            { 12, HumanBodyBones.RightMiddleIntermediate },
            { 13, HumanBodyBones.RightMiddleDistal },
            // Ring
            { 16, HumanBodyBones.RightRingProximal },
            { 17, HumanBodyBones.RightRingIntermediate },
            { 18, HumanBodyBones.RightRingDistal },
            // Little
            { 21, HumanBodyBones.RightLittleProximal },
            { 22, HumanBodyBones.RightLittleIntermediate },
            { 23, HumanBodyBones.RightLittleDistal },
        };

        public void OnAvatarLoaded(GameObject avatar)
        {
            _avatarRoot = avatar;
            _animator = avatar != null ? avatar.GetComponent<Animator>() : null;

            if (_animator != null && _animator.isHuman)
                Debug.Log("[HandTracking] Avatar has humanoid rig — hand tracking enabled.");
            else if (avatar != null)
                Debug.LogWarning("[HandTracking] Avatar lacks humanoid Animator. Hand tracking disabled.");
        }

        private void Start()
        {
            CheckXrHandsAvailability();
        }

        private void Update()
        {
            if (_animator == null || !_animator.isHuman || !_hasXrHands) return;

#if UNITY_XR_HANDS
            UpdateHandTracking();
#endif
        }

        private void CheckXrHandsAvailability()
        {
#if UNITY_XR_HANDS
            var subsystems = new List<UnityEngine.XR.Hands.XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            _hasXrHands = subsystems.Count > 0;
            if (_hasXrHands)
                Debug.Log("[HandTracking] XR Hands subsystem found.");
            else
                Debug.Log("[HandTracking] XR Hands subsystem not available.");
#else
            _hasXrHands = false;
            Debug.Log("[HandTracking] XR Hands package not detected. Hand tracking disabled.");
#endif
        }

#if UNITY_XR_HANDS
        private void UpdateHandTracking()
        {
            var subsystems = new List<UnityEngine.XR.Hands.XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            if (subsystems.Count == 0) return;

            var subsystem = subsystems[0];

            // Left hand
            var leftHand = subsystem.leftHand;
            if (leftHand.isTracked)
                ApplyJointRotations(leftHand, LeftHandMap);

            // Right hand
            var rightHand = subsystem.rightHand;
            if (rightHand.isTracked)
                ApplyJointRotations(rightHand, RightHandMap);
        }

        private void ApplyJointRotations(UnityEngine.XR.Hands.XRHand hand,
            Dictionary<int, HumanBodyBones> jointMap)
        {
            foreach (var kvp in jointMap)
            {
                var jointId = (UnityEngine.XR.Hands.XRHandJointID)kvp.Key;
                var bone = kvp.Value;

                if (hand.GetJoint(jointId).TryGetPose(out var pose))
                {
                    var boneTransform = _animator.GetBoneTransform(bone);
                    if (boneTransform != null)
                        boneTransform.localRotation = pose.rotation;
                }
            }
        }
#endif
    }
}
