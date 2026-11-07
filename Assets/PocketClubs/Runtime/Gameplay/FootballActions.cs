using System;
using PocketClubs.Core;
using UnityEngine;

namespace PocketClubs.Gameplay
{
    [Serializable]
    public struct TeammatePassTarget
    {
        public int playerId;
        public Vector3 worldPosition;
    }

    [Serializable]
    public struct PassExecutionResult
    {
        public bool wasAssisted;
        public int assistedTargetPlayerId;
        public Vector3 directionXZ;
        public float speed;
        public Vector3 launchVelocity;
    }

    [Serializable]
    public struct ShotExecutionResult
    {
        public bool executed;
        public float chargeSeconds;
        public float normalizedCharge;
        public float speed;
        public Vector3 launchVelocity;
    }

    /// <summary>
    /// Computes directional passing with optional teammate angle/distance assistance
    /// and a guaranteed directional fallback when no teammate qualifies (such as solo Training).
    /// </summary>
    public sealed class PassingService
    {
        private readonly ActionTuningConfig _tuning;

        public PassingService(ActionTuningConfig tuning)
        {
            _tuning = tuning != null ? tuning : ActionTuningConfig.CreateRuntimeDefault();
        }

        public PassExecutionResult ComputePass(
            Vector3 passerPosition,
            Vector3 passerForward,
            Vector2 aimDirectionXZ,
            float requestedPowerNormalized,
            ReadOnlySpan<TeammatePassTarget> teammates = default)
        {
            Vector3 baseDirection = ResolvePlanarAimDirection(passerForward, aimDirectionXZ);
            int bestTargetId = BallSimulation.NoOwnerId;
            Vector3 bestTargetDir = baseDirection;
            float bestTargetDist = 0f;
            float bestScore = float.MaxValue;

            for (int i = 0; i < teammates.Length; i++)
            {
                Vector3 toMate = teammates[i].worldPosition - passerPosition;
                toMate.y = 0f;
                float dist = toMate.magnitude;

                if (dist < 1.0f || dist > _tuning.passAssistMaxDistance)
                {
                    continue;
                }

                Vector3 dirToMate = toMate / dist;
                float angle = Vector3.Angle(baseDirection, dirToMate);
                if (angle > _tuning.passAssistMaxAngleDegrees)
                {
                    continue;
                }

                float normalizedAngle = angle / Mathf.Max(1f, _tuning.passAssistMaxAngleDegrees);
                float normalizedDist = dist / Mathf.Max(1f, _tuning.passAssistMaxDistance);
                float score = normalizedAngle * 0.7f + normalizedDist * 0.3f;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestTargetId = teammates[i].playerId;
                    bestTargetDir = dirToMate;
                    bestTargetDist = dist;
                }
            }

            Vector3 finalDir = baseDirection;
            float speed;

            if (bestTargetId != BallSimulation.NoOwnerId)
            {
                finalDir = Vector3.Slerp(baseDirection, bestTargetDir, _tuning.passDirectionAssistWeight).normalized;
                float distanceRatio = Mathf.Clamp01(bestTargetDist / Mathf.Max(1f, _tuning.passAssistMaxDistance));
                float blendedRatio = Mathf.Lerp(Mathf.Clamp01(requestedPowerNormalized), distanceRatio, 0.65f);
                speed = Mathf.Lerp(_tuning.minPassSpeed, _tuning.maxPassSpeed, blendedRatio);
            }
            else
            {
                // Directional fallback when no teammate qualifies
                float clampedPower = Mathf.Clamp01(requestedPowerNormalized);
                speed = Mathf.Lerp(_tuning.minPassSpeed, _tuning.maxPassSpeed, clampedPower);
            }

            speed = Mathf.Clamp(speed, _tuning.minPassSpeed, _tuning.maxPassSpeed);

            return new PassExecutionResult
            {
                wasAssisted = bestTargetId != BallSimulation.NoOwnerId,
                assistedTargetPlayerId = bestTargetId,
                directionXZ = finalDir,
                speed = speed,
                launchVelocity = finalDir * speed
            };
        }

        public static Vector3 ResolvePlanarAimDirection(Vector3 fallbackForward, Vector2 aimInputXZ)
        {
            if (!float.IsNaN(aimInputXZ.x) && !float.IsNaN(aimInputXZ.y) && aimInputXZ.sqrMagnitude > 0.01f)
            {
                return new Vector3(aimInputXZ.x, 0f, aimInputXZ.y).normalized;
            }

            Vector3 planarForward = new Vector3(fallbackForward.x, 0f, fallbackForward.z);
            return planarForward.sqrMagnitude > 0.001f ? planarForward.normalized : Vector3.forward;
        }
    }

    /// <summary>
    /// Hold-to-charge, release-to-shoot controller with clamped power, bounded goal-opening
    /// aim assistance, and safe cancellation when input or possession is interrupted.
    /// </summary>
    public sealed class ShotChargeController
    {
        private readonly ActionTuningConfig _tuning;

        public bool IsCharging { get; private set; }
        public float CurrentChargeSeconds { get; private set; }

        public float NormalizedCharge => _tuning.maxShotChargeSeconds > 0f
            ? Mathf.Clamp01(CurrentChargeSeconds / _tuning.maxShotChargeSeconds)
            : 0f;

        public ShotChargeController(ActionTuningConfig tuning)
        {
            _tuning = tuning != null ? tuning : ActionTuningConfig.CreateRuntimeDefault();
        }

        public void BeginOrContinueCharge(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                return;
            }

            IsCharging = true;
            CurrentChargeSeconds = Mathf.Clamp(
                CurrentChargeSeconds + deltaTime,
                0f,
                _tuning.maxShotChargeSeconds);
        }

        public void CancelCharge()
        {
            IsCharging = false;
            CurrentChargeSeconds = 0f;
        }

        public ShotExecutionResult ReleaseShot(
            Vector3 shooterPosition,
            Vector3 shooterForward,
            Vector2 aimDirectionXZ,
            Vector3 targetGoalCenter)
        {
            if (!IsCharging)
            {
                return new ShotExecutionResult { executed = false };
            }

            float chargeSecs = Mathf.Clamp(CurrentChargeSeconds, 0f, _tuning.maxShotChargeSeconds);
            float normalized = _tuning.maxShotChargeSeconds > 0f
                ? Mathf.Clamp01(chargeSecs / _tuning.maxShotChargeSeconds)
                : 0f;

            IsCharging = false;
            CurrentChargeSeconds = 0f;

            Vector3 rawDir = PassingService.ResolvePlanarAimDirection(shooterForward, aimDirectionXZ);
            Vector3 toGoal = targetGoalCenter - shooterPosition;
            toGoal.y = 0f;

            Vector3 finalPlanarDir = rawDir;
            if (toGoal.sqrMagnitude > 0.25f)
            {
                Vector3 goalDir = toGoal.normalized;
                float angleToGoal = Vector3.Angle(rawDir, goalDir);
                if (angleToGoal <= _tuning.shotGoalAssistAngleDegrees)
                {
                    finalPlanarDir = Vector3.Slerp(rawDir, goalDir, _tuning.shotGoalAssistWeight).normalized;
                }
            }

            float planarSpeed = Mathf.Clamp(
                Mathf.Lerp(_tuning.minShotSpeed, _tuning.maxShotSpeed, normalized),
                _tuning.minShotSpeed,
                _tuning.maxShotSpeed);

            float verticalSpeed = planarSpeed * (_tuning.shotLoftRatioAtFullCharge * normalized);
            Vector3 launchVelocity = new Vector3(
                finalPlanarDir.x * planarSpeed,
                verticalSpeed,
                finalPlanarDir.z * planarSpeed);

            float maxAllowed = _tuning.maxShotSpeed;
            if (launchVelocity.magnitude > maxAllowed)
            {
                launchVelocity = launchVelocity.normalized * maxAllowed;
            }

            return new ShotExecutionResult
            {
                executed = true,
                chargeSeconds = chargeSecs,
                normalizedCharge = normalized,
                speed = launchVelocity.magnitude,
                launchVelocity = launchVelocity
            };
        }
    }

    /// <summary>
    /// Standing tackle validator enforcing cooldown, reach, facing angle, and possession state.
    /// Explicitly rejects remote possession steals.
    /// </summary>
    public sealed class TackleService
    {
        private readonly ActionTuningConfig _tuning;

        public float CooldownRemaining { get; private set; }
        public bool IsReady => CooldownRemaining <= 0.0001f;

        public TackleService(ActionTuningConfig tuning)
        {
            _tuning = tuning != null ? tuning : ActionTuningConfig.CreateRuntimeDefault();
        }

        public void TickCooldown(float deltaTime)
        {
            if (deltaTime > 0f && CooldownRemaining > 0f)
            {
                CooldownRemaining = Mathf.Max(0f, CooldownRemaining - deltaTime);
            }
        }

        public void ResetCooldown()
        {
            CooldownRemaining = 0f;
        }

        public TackleAttemptResult AttemptStandingTackle(
            int tacklerPlayerId,
            TeamSide tacklerTeam,
            Vector3 tacklerPosition,
            Vector3 tacklerForward,
            BallSimulation ballSimulation)
        {
            if (ballSimulation == null)
            {
                return TackleAttemptResult.RejectedNoPossessionTarget;
            }

            if (!IsReady)
            {
                return TackleAttemptResult.RejectedCooldown;
            }

            if (ballSimulation.CurrentState == BallState.ControlledDribble &&
                ballSimulation.OwnerPlayerId == tacklerPlayerId)
            {
                return TackleAttemptResult.RejectedSelfAlreadyHasBall;
            }

            // Trigger cooldown on any committed tackle attempt
            CooldownRemaining = _tuning.tackleCooldownSeconds;

            if (ballSimulation.CurrentState != BallState.ControlledDribble ||
                ballSimulation.OwnerPlayerId == BallSimulation.NoOwnerId)
            {
                return TackleAttemptResult.RejectedNoPossessionTarget;
            }

            Vector3 toBall = ballSimulation.Position - tacklerPosition;
            toBall.y = 0f;
            float planarDistance = toBall.magnitude;

            if (planarDistance > _tuning.tackleReachDistance)
            {
                return TackleAttemptResult.MissedOutOfRange;
            }

            Vector3 forwardXZ = new Vector3(tacklerForward.x, 0f, tacklerForward.z);
            if (forwardXZ.sqrMagnitude < 0.001f)
            {
                forwardXZ = Vector3.forward;
            }
            else
            {
                forwardXZ.Normalize();
            }

            Vector3 dirToBall = planarDistance > 0.001f ? toBall / planarDistance : forwardXZ;
            float angle = Vector3.Angle(forwardXZ, dirToBall);
            if (angle > _tuning.tackleMaxAngleDegrees)
            {
                return TackleAttemptResult.MissedWrongFacing;
            }

            bool cleanSteal = planarDistance <= _tuning.tackleReachDistance * 0.78f &&
                              angle <= _tuning.tackleMaxAngleDegrees * 0.65f;

            ballSimulation.ForceTransferOrDislodgeOnTackle(
                tacklerPlayerId,
                tacklerTeam,
                tacklerPosition,
                forwardXZ,
                cleanSteal,
                _tuning.tackleDislodgeImpulse);

            return cleanSteal
                ? TackleAttemptResult.SuccessSteal
                : TackleAttemptResult.SuccessDislodge;
        }
    }
}
