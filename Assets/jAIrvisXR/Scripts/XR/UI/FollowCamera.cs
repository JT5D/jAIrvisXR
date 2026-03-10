using UnityEngine;

namespace jAIrvisXR.XR.UI
{
    /// <summary>
    /// Makes a world-space UI canvas follow the camera at a fixed distance.
    /// Smooth lerp for comfortable viewing in XR.
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        [SerializeField] private float _distance = 2f;
        [SerializeField] private float _height = -0.3f;
        [SerializeField] private float _followSpeed = 2f;
        [SerializeField] private float _rotationSpeed = 3f;
        [SerializeField] private float _deadZoneAngle = 15f;

        private Transform _camera;
        private Vector3 _targetPosition;
        private bool _initialized;

        private void Start()
        {
            _camera = Camera.main?.transform;
            if (_camera != null)
            {
                SnapToPosition();
                _initialized = true;
            }
        }

        private void LateUpdate()
        {
            if (_camera == null)
            {
                _camera = Camera.main?.transform;
                return;
            }

            if (!_initialized)
            {
                SnapToPosition();
                _initialized = true;
                return;
            }

            Vector3 forward = _camera.forward;
            forward.y = 0;
            forward.Normalize();

            _targetPosition = _camera.position + forward * _distance
                + Vector3.up * _height;

            float angle = Vector3.Angle(
                transform.position - _camera.position,
                _camera.forward);

            if (angle > _deadZoneAngle)
            {
                transform.position = Vector3.Lerp(
                    transform.position, _targetPosition,
                    Time.deltaTime * _followSpeed);
            }

            Quaternion targetRotation = Quaternion.LookRotation(
                transform.position - _camera.position);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRotation,
                Time.deltaTime * _rotationSpeed);
        }

        private void SnapToPosition()
        {
            Vector3 forward = _camera.forward;
            forward.y = 0;
            forward.Normalize();

            transform.position = _camera.position + forward * _distance
                + Vector3.up * _height;
            transform.rotation = Quaternion.LookRotation(
                transform.position - _camera.position);
        }
    }
}
