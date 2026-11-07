using System;
using PocketClubs.Core;
using PocketClubs.Input;
using UnityEngine;

namespace PocketClubs.Gameplay
{
    [Serializable]
    public struct ObstacleCircleXZ
    {
        public Vector3 center;
        public float radius;
    }

    /// <summary>
    /// Deterministic, allocation-free player movement and stamina motor that can run
    /// identically in offline simulation, EditMode tests, and future authoritative server ticks.
    /// </summary>
    public sealed class PlayerMovementMotor
    {
        private readonly MovementTuningConfig _tuning;
        private readonly PitchConfig _pitchConfig;

        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Vector3 Forward { get; private set; } = Vector3.forward;
        public float Stamina { get; private set; }
        public float TimeSinceLastSprint { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsExhausted { get; private set; }

        public float NormalizedStamina => _tuning.maxStamina > 0f
            ? Mathf.Clamp01(Stamina / _tuning.maxStamina)
            : 0f;

        public float CurrentPlanarSpeed => new Vector2(Velocity.x, Velocity.z).magnitude;

        public PlayerMovementMotor(MovementTuningConfig tuning, PitchConfig pitchConfig, Vector3 initialPosition, Vector3 initialForward)
        {
            _tuning = tuning != null ? tuning : MovementTuningConfig.CreateRuntimeDefault();
            _pitchConfig = pitchConfig != null ? pitchConfig : PitchConfig.CreateRuntimeDefault();

            Position = initialPosition;
            Velocity = Vector3.zero;
            Forward = initialForward.sqrMagnitude > 0.001f ? initialForward.normalized : Vector3.forward;
            Stamina = _tuning.maxStamina;
            TimeSinceLastSprint = _tuning.staminaRegenDelaySeconds;
            IsSprinting = false;
            IsExhausted = false;
        }

        public void Teleport(Vector3 newPosition, Vector3 newForward, bool refillStamina = true)
        {
            Position = new Vector3(newPosition.x, 0f, newPosition.z);
            Velocity = Vector3.zero;
            if (newForward.sqrMagnitude > 0.001f)
            {
                Forward = new Vector3(newForward.x, 0f, newForward.z).normalized;
            }

            IsSprinting = false;
            if (refillStamina)
            {
                Stamina = _tuning.maxStamina;
                IsExhausted = false;
                TimeSinceLastSprint = _tuning.staminaRegenDelaySeconds;
            }
        }

        public void CancelActiveInput()
        {
            IsSprinting = false;
            Velocity = Vector3.zero;
        }

        public void Step(
            in PlayerCommandIntent rawIntent,
            float deltaTime,
            bool hasBallPossession,
            ReadOnlySpan<ObstacleCircleXZ> dynamicObstacles = default)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            if (rawIntent.shootCancelled)
            {
                CancelActiveInput();
                UpdateStamina(false, deltaTime);
                return;
            }

            Vector2 moveInput = PlayerCommandIntent.NormalizeDiagonalInput(
                rawIntent.worldMoveDirection.sqrMagnitude > 0.0001f
                    ? rawIntent.worldMoveDirection
                    : rawIntent.cameraRelativeMove);

            float inputMagnitude = moveInput.magnitude;
            bool wantsToSprint = rawIntent.sprintHeld && inputMagnitude > 0.1f;

            bool canSprint = UpdateStamina(wantsToSprint, deltaTime);
            IsSprinting = canSprint;

            float maxAllowedSpeed = _tuning.baseMaxSpeed;
            if (IsSprinting)
            {
                maxAllowedSpeed *= _tuning.sprintSpeedMultiplier;
            }
            if (hasBallPossession)
            {
                maxAllowedSpeed *= _tuning.dribbleSpeedMultiplier;
            }

            Vector3 desiredVelocity = new Vector3(moveInput.x, 0f, moveInput.y) * maxAllowedSpeed;
            float accelRate = inputMagnitude > 0.01f ? _tuning.acceleration : _tuning.deceleration;

            Velocity = Vector3.MoveTowards(Velocity, desiredVelocity, accelRate * deltaTime);

            // Enforce hard speed bound
            float speed = Velocity.magnitude;
            if (speed > maxAllowedSpeed)
            {
                Velocity = Velocity * (maxAllowedSpeed / speed);
            }

            if (inputMagnitude > 0.01f)
            {
                Vector3 targetDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
                Quaternion currentRot = Quaternion.LookRotation(Forward, Vector3.up);
                Quaternion targetRot = Quaternion.LookRotation(targetDirection, Vector3.up);
                Quaternion nextRot = Quaternion.RotateTowards(
                    currentRot,
                    targetRot,
                    _tuning.turnSpeedDegreesPerSecond * deltaTime);
                Forward = (nextRot * Vector3.forward).normalized;
            }

            Vector3 nextPosition = Position + Velocity * deltaTime;
            nextPosition.y = 0f;

            nextPosition = ResolveCollisions(nextPosition, dynamicObstacles);
            Position = nextPosition;
        }

        /// <summary>
        /// Documented stamina rules:
        /// 1. Starting a sprint from non-sprinting state requires Stamina >= minStaminaToStartSprint and !IsExhausted.
        /// 2. Active sprinting drains sprintStaminaDrainPerSecond * dt.
        /// 3. Reaching 0 stamina forces IsExhausted = true and halts sprinting until Stamina recovers >= minStaminaToStartSprint.
        /// 4. Regeneration waits staminaRegenDelaySeconds after the last sprint frame, then restores staminaRegenPerSecond * dt up to maxStamina.
        /// </summary>
        private bool UpdateStamina(bool wantsToSprint, float deltaTime)
        {
            if (IsExhausted && Stamina >= _tuning.minStaminaToStartSprint)
            {
                IsExhausted = false;
            }

            bool canEngageSprint = wantsToSprint && !IsExhausted &&
                                   (IsSprinting ? Stamina > 0f : Stamina >= _tuning.minStaminaToStartSprint);

            if (canEngageSprint)
            {
                Stamina = Mathf.Max(0f, Stamina - _tuning.sprintStaminaDrainPerSecond * deltaTime);
                TimeSinceLastSprint = 0f;

                if (Stamina <= 0.0001f)
                {
                    Stamina = 0f;
                    IsExhausted = true;
                    return false;
                }

                return true;
            }

            TimeSinceLastSprint += deltaTime;
            if (TimeSinceLastSprint >= _tuning.staminaRegenDelaySeconds)
            {
                Stamina = Mathf.Min(_tuning.maxStamina, Stamina + _tuning.staminaRegenPerSecond * deltaTime);
                if (IsExhausted && Stamina >= _tuning.minStaminaToStartSprint)
                {
                    IsExhausted = false;
                }
            }

            return false;
        }

        private Vector3 ResolveCollisions(Vector3 candidatePosition, ReadOnlySpan<ObstacleCircleXZ> dynamicObstacles)
        {
            float r = _tuning.playerRadius;
            float maxX = _pitchConfig.HalfWidth + _pitchConfig.boundaryRunoff - r;
            float maxZ = _pitchConfig.HalfLength + _pitchConfig.boundaryRunoff - r;

            candidatePosition.x = Mathf.Clamp(candidatePosition.x, -maxX, maxX);
            candidatePosition.z = Mathf.Clamp(candidatePosition.z, -maxZ, maxZ);

            // Resolve against the four goalposts at (+-HalfGoalWidth, +-HalfLength)
            float postRadius = 0.12f;
            float halfGoalW = _pitchConfig.HalfGoalWidth;
            float halfPitchL = _pitchConfig.HalfLength;

            candidatePosition = PushOutFromCircleXZ(candidatePosition, r, new Vector3(-halfGoalW, 0f, halfPitchL), postRadius);
            candidatePosition = PushOutFromCircleXZ(candidatePosition, r, new Vector3(halfGoalW, 0f, halfPitchL), postRadius);
            candidatePosition = PushOutFromCircleXZ(candidatePosition, r, new Vector3(-halfGoalW, 0f, -halfPitchL), postRadius);
            candidatePosition = PushOutFromCircleXZ(candidatePosition, r, new Vector3(halfGoalW, 0f, -halfPitchL), postRadius);

            for (int i = 0; i < dynamicObstacles.Length; i++)
            {
                candidatePosition = PushOutFromCircleXZ(
                    candidatePosition,
                    r,
                    dynamicObstacles[i].center,
                    dynamicObstacles[i].radius);
            }

            return candidatePosition;
        }

        private static Vector3 PushOutFromCircleXZ(Vector3 pos, float selfRadius, Vector3 obstacleCenter, float obstacleRadius)
        {
            float minDist = selfRadius + obstacleRadius;
            Vector2 delta = new Vector2(pos.x - obstacleCenter.x, pos.z - obstacleCenter.z);
            float distSq = delta.sqrMagnitude;

            if (distSq >= minDist * minDist)
            {
                return pos;
            }

            if (distSq < 0.00001f)
            {
                return new Vector3(pos.x, 0f, pos.z - minDist);
            }

            float dist = Mathf.Sqrt(distSq);
            Vector2 push = (delta / dist) * (minDist - dist);
            pos.x += push.x;
            pos.z += push.y;
            return pos;
        }
    }
}
