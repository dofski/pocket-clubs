using PocketClubs.Core;
using UnityEngine;

namespace PocketClubs.CameraSystem
{
    /// <summary>
    /// Elevated broadcast-style match camera that keeps the controlled player visible at all times,
    /// applies passing look-ahead, clamps distant ball influence so it never abandons the player,
    /// respects pitch boundaries, and calculates off-screen ball indicator coordinates.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class BroadcastMatchCamera : MonoBehaviour
    {
        [SerializeField] private CameraTuningConfig tuning;
        [SerializeField] private PitchConfig pitchConfig;

        private Camera _unityCamera;
        private Transform _controlledPlayerTarget;
        private Transform _ballTarget;
        private Vector3 _smoothedFocusPoint;
        private Vector3 _smoothVelocity;
        private float _currentZoomScale = 1.0f;
        private float _shakeRemainingSeconds;
        private float _shakeCurrentIntensity;

        public float CameraYawDegrees => transform.eulerAngles.y;
        public float CurrentZoomScale => _currentZoomScale;
        public Camera UnityCamera => _unityCamera != null ? _unityCamera : (_unityCamera = GetComponent<Camera>());

        public void Initialize(
            CameraTuningConfig cameraTuning,
            PitchConfig pitchCfg,
            Transform controlledPlayer,
            Transform ballTransform)
        {
            tuning = cameraTuning != null ? cameraTuning : CameraTuningConfig.CreateRuntimeDefault();
            pitchConfig = pitchCfg != null ? pitchCfg : PitchConfig.CreateRuntimeDefault();
            _controlledPlayerTarget = controlledPlayer;
            _ballTarget = ballTransform;
            _unityCamera = GetComponent<Camera>();
            _currentZoomScale = Mathf.Clamp(_currentZoomScale, tuning.minZoomScale, tuning.maxZoomScale);

            if (_controlledPlayerTarget != null)
            {
                _smoothedFocusPoint = _controlledPlayerTarget.position;
                SnapImmediately(Vector3.forward);
            }
        }

        public void SetTargets(Transform controlledPlayer, Transform ballTransform)
        {
            _controlledPlayerTarget = controlledPlayer;
            _ballTarget = ballTransform;
        }

        public void SetZoomScale(float zoomScale)
        {
            EnsureConfigs();
            _currentZoomScale = Mathf.Clamp(zoomScale, tuning.minZoomScale, tuning.maxZoomScale);
        }

        public void TriggerGoalShake()
        {
            EnsureConfigs();
            _shakeRemainingSeconds = tuning.goalShakeDuration;
            _shakeCurrentIntensity = tuning.goalShakeIntensity;
        }

        public void SnapImmediately(Vector3 playerForwardXZ)
        {
            EnsureConfigs();
            if (_controlledPlayerTarget == null)
            {
                return;
            }

            Vector3 ballPos = _ballTarget != null ? _ballTarget.position : _controlledPlayerTarget.position;
            _smoothedFocusPoint = ComputeDesiredFocusPoint(
                _controlledPlayerTarget.position,
                playerForwardXZ,
                ballPos);
            ApplyCameraTransform(_smoothedFocusPoint, Vector3.zero);
        }

        public void TickCamera(Vector3 playerForwardXZ, float deltaTime)
        {
            EnsureConfigs();
            if (_controlledPlayerTarget == null || deltaTime <= 0f)
            {
                return;
            }

            Vector3 ballPos = _ballTarget != null ? _ballTarget.position : _controlledPlayerTarget.position;
            Vector3 desiredFocus = ComputeDesiredFocusPoint(
                _controlledPlayerTarget.position,
                playerForwardXZ,
                ballPos);

            _smoothedFocusPoint = Vector3.SmoothDamp(
                _smoothedFocusPoint,
                desiredFocus,
                ref _smoothVelocity,
                tuning.followSmoothTime,
                Mathf.Infinity,
                deltaTime);

            Vector3 shakeOffset = Vector3.zero;
            if (_shakeRemainingSeconds > 0f)
            {
                _shakeRemainingSeconds = Mathf.Max(0f, _shakeRemainingSeconds - deltaTime);
                float damper = tuning.goalShakeDuration > 0f ? (_shakeRemainingSeconds / tuning.goalShakeDuration) : 0f;
                float phase = Time.unscaledTime * 42f;
                shakeOffset = new Vector3(
                    Mathf.Sin(phase) * _shakeCurrentIntensity * damper,
                    Mathf.Cos(phase * 1.3f) * _shakeCurrentIntensity * 0.5f * damper,
                    0f);
            }

            ApplyCameraTransform(_smoothedFocusPoint, shakeOffset);
        }

        public Vector3 ComputeDesiredFocusPoint(
            Vector3 playerPosition,
            Vector3 playerForwardXZ,
            Vector3 ballPosition)
        {
            EnsureConfigs();

            Vector3 planarForward = new Vector3(playerForwardXZ.x, 0f, playerForwardXZ.z);
            if (planarForward.sqrMagnitude > 0.001f)
            {
                planarForward.Normalize();
            }

            Vector3 lookAheadOffset = planarForward * tuning.passLookAheadDistance;

            Vector3 toBall = new Vector3(ballPosition.x - playerPosition.x, 0f, ballPosition.z - playerPosition.z);
            Vector3 weightedBallOffset = toBall * tuning.ballInfluenceWeight;
            if (weightedBallOffset.magnitude > tuning.maxBallInfluenceOffset)
            {
                weightedBallOffset = weightedBallOffset.normalized * tuning.maxBallInfluenceOffset;
            }

            Vector3 rawFocus = playerPosition + lookAheadOffset + weightedBallOffset;
            rawFocus.y = 0f;

            // Clamp focus within pitch boundary margins while guaranteeing the player stays in frame
            float maxFocusX = Mathf.Max(4f, pitchConfig.HalfWidth - 4.0f);
            float maxFocusZ = Mathf.Max(6f, pitchConfig.HalfLength - 5.0f);
            rawFocus.x = Mathf.Clamp(rawFocus.x, -maxFocusX, maxFocusX);
            rawFocus.z = Mathf.Clamp(rawFocus.z, -maxFocusZ, maxFocusZ);

            // Never drift farther than maxBallInfluenceOffset + passLookAheadDistance from the player
            Vector3 deltaFromPlayer = rawFocus - new Vector3(playerPosition.x, 0f, playerPosition.z);
            float maxAnchorDrift = tuning.passLookAheadDistance + tuning.maxBallInfluenceOffset;
            if (deltaFromPlayer.magnitude > maxAnchorDrift)
            {
                rawFocus = new Vector3(playerPosition.x, 0f, playerPosition.z) + deltaFromPlayer.normalized * maxAnchorDrift;
            }

            return rawFocus;
        }

        /// <summary>
        /// Computes whether the ball is off-screen and provides normalized screen-edge coordinates
        /// and rotation angle for the HUD off-screen ball indicator.
        /// </summary>
        public bool TryGetOffScreenBallIndicator(
            Vector3 ballWorldPosition,
            out Vector2 clampedViewportPos,
            out float indicatorAngleDegrees)
        {
            clampedViewportPos = new Vector2(0.5f, 0.5f);
            indicatorAngleDegrees = 0f;

            Camera cam = UnityCamera;
            if (cam == null)
            {
                return false;
            }

            Vector3 viewportPoint = cam.WorldToViewportPoint(ballWorldPosition);
            bool behindCamera = viewportPoint.z < 0f;
            if (behindCamera)
            {
                viewportPoint.x = 1f - viewportPoint.x;
                viewportPoint.y = 1f - viewportPoint.y;
            }

            const float margin = 0.06f;
            bool isInside = !behindCamera &&
                            viewportPoint.x >= margin && viewportPoint.x <= (1f - margin) &&
                            viewportPoint.y >= margin && viewportPoint.y <= (1f - margin);

            if (isInside)
            {
                return false;
            }

            Vector2 fromCenter = new Vector2(viewportPoint.x - 0.5f, viewportPoint.y - 0.5f);
            if (fromCenter.sqrMagnitude < 0.0001f)
            {
                fromCenter = Vector2.up;
            }

            indicatorAngleDegrees = Mathf.Atan2(fromCenter.y, fromCenter.x) * Mathf.Rad2Deg;

            float scaleX = Mathf.Abs(fromCenter.x) > 0.0001f ? (0.5f - margin) / Mathf.Abs(fromCenter.x) : float.MaxValue;
            float scaleY = Mathf.Abs(fromCenter.y) > 0.0001f ? (0.5f - margin) / Mathf.Abs(fromCenter.y) : float.MaxValue;
            float edgeScale = Mathf.Min(scaleX, scaleY);

            clampedViewportPos = new Vector2(0.5f, 0.5f) + fromCenter * edgeScale;
            return true;
        }

        private void ApplyCameraTransform(Vector3 focusPoint, Vector3 shakeOffset)
        {
            float zoomedHeight = tuning.baseHeight * _currentZoomScale;
            float zoomedDistance = tuning.baseDistance * _currentZoomScale;

            Vector3 offset = new Vector3(0f, zoomedHeight, -zoomedDistance);
            transform.position = focusPoint + offset + shakeOffset;
            transform.rotation = Quaternion.Euler(tuning.pitchAngleDegrees, 0f, 0f);
        }

        private void EnsureConfigs()
        {
            if (tuning == null) tuning = CameraTuningConfig.CreateRuntimeDefault();
            if (pitchConfig == null) pitchConfig = PitchConfig.CreateRuntimeDefault();
        }
    }
}
