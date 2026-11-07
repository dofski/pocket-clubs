using System;
using PocketClubs.Core;
using UnityEngine;

namespace PocketClubs.Gameplay
{
    [Serializable]
    public struct PossessionCandidate
    {
        public int playerId;
        public TeamSide team;
        public Vector3 position;
        public Vector3 forward;
        public float playerRadius;
    }

    /// <summary>
    /// Single authoritative ball state and physics simulation.
    /// Offline: owned by local match coordinator.
    /// Online (Gate 2+): owned exclusively by dedicated headless server.
    /// </summary>
    public sealed class BallSimulation
    {
        public const int NoOwnerId = -1;

        private readonly BallTuningConfig _tuning;
        private readonly PitchConfig _pitchConfig;

        public BallState CurrentState { get; private set; } = BallState.Free;
        public int OwnerPlayerId { get; private set; } = NoOwnerId;
        public int LastTouchPlayerId { get; private set; } = NoOwnerId;
        public TeamSide LastTouchTeam { get; private set; } = TeamSide.None;

        public Vector3 PreviousPosition { get; private set; }
        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }

        private int _releaseLockedPlayerId = NoOwnerId;
        private float _releasePickupLockTimer;

        public float BallRadius => _tuning.ballRadius;

        public BallSimulation(BallTuningConfig tuning, PitchConfig pitchConfig, Vector3 initialPosition)
        {
            _tuning = tuning != null ? tuning : BallTuningConfig.CreateRuntimeDefault();
            _pitchConfig = pitchConfig != null ? pitchConfig : PitchConfig.CreateRuntimeDefault();
            ResetForRestart(initialPosition);
            CompleteRestartToFreeState();
        }

        public void ResetForRestart(Vector3 worldPosition)
        {
            CurrentState = BallState.RestartReset;
            OwnerPlayerId = NoOwnerId;
            _releaseLockedPlayerId = NoOwnerId;
            _releasePickupLockTimer = 0f;
            Vector3 grounded = new Vector3(worldPosition.x, _tuning.ballRadius, worldPosition.z);
            PreviousPosition = grounded;
            Position = grounded;
            Velocity = Vector3.zero;
        }

        public void CompleteRestartToFreeState()
        {
            if (CurrentState == BallState.RestartReset)
            {
                CurrentState = BallState.Free;
                PreviousPosition = Position;
            }
        }

        /// <summary>
        /// Attempts to assign possession to a single player or resolves a simultaneous contest
        /// among multiple candidates without allowing attachment through obstacles/opponents.
        /// </summary>
        public bool TryEvaluatePossessionContest(
            ReadOnlySpan<PossessionCandidate> candidates,
            ReadOnlySpan<ObstacleCircleXZ> blockers,
            out int winningPlayerId)
        {
            winningPlayerId = NoOwnerId;

            if (CurrentState == BallState.ControlledDribble || CurrentState == BallState.RestartReset)
            {
                winningPlayerId = OwnerPlayerId;
                return false;
            }

            if (Position.y > _tuning.maxPickupBallHeight)
            {
                return false;
            }

            float bestScore = float.MaxValue;
            int bestCandidateIndex = -1;

            for (int i = 0; i < candidates.Length; i++)
            {
                ref readonly PossessionCandidate candidate = ref candidates[i];
                if (candidate.playerId == NoOwnerId)
                {
                    continue;
                }

                if (_releasePickupLockTimer > 0f && candidate.playerId == _releaseLockedPlayerId)
                {
                    continue;
                }

                Vector2 deltaXZ = new Vector2(Position.x - candidate.position.x, Position.z - candidate.position.z);
                float planarDist = deltaXZ.magnitude;
                if (planarDist > _tuning.pickupRadius)
                {
                    continue;
                }

                if (IsSegmentBlockedByObstacleOrOpponent(candidate.position, Position, candidate.playerId, candidates, blockers))
                {
                    continue;
                }

                // Deterministic scoring: planar distance weighted slightly by facing alignment,
                // tie-broken deterministically by lower playerId.
                Vector2 forwardXZ = new Vector2(candidate.forward.x, candidate.forward.z).normalized;
                Vector2 dirToBall = planarDist > 0.001f ? deltaXZ / planarDist : forwardXZ;
                float alignmentBonus = Mathf.Clamp01(Vector2.Dot(forwardXZ, dirToBall)) * 0.08f;
                float candidateScore = planarDist - alignmentBonus;

                if (candidateScore < bestScore - 0.0001f ||
                    (Mathf.Abs(candidateScore - bestScore) <= 0.0001f &&
                     (bestCandidateIndex < 0 || candidate.playerId < candidates[bestCandidateIndex].playerId)))
                {
                    bestScore = candidateScore;
                    bestCandidateIndex = i;
                }
            }

            if (bestCandidateIndex < 0)
            {
                return false;
            }

            ref readonly PossessionCandidate winner = ref candidates[bestCandidateIndex];
            ClaimPossessionInternal(winner.playerId, winner.team, winner.position, winner.forward);
            winningPlayerId = winner.playerId;
            return true;
        }

        public bool TryClaimPossession(
            int playerId,
            TeamSide team,
            Vector3 playerPosition,
            Vector3 playerForward,
            ReadOnlySpan<ObstacleCircleXZ> blockers = default)
        {
            Span<PossessionCandidate> single = stackalloc PossessionCandidate[1];
            single[0] = new PossessionCandidate
            {
                playerId = playerId,
                team = team,
                position = playerPosition,
                forward = playerForward,
                playerRadius = 0.45f
            };
            return TryEvaluatePossessionContest(single, blockers, out _);
        }

        public void UpdateControlledDribble(
            Vector3 ownerPosition,
            Vector3 ownerForward,
            Vector3 ownerVelocity,
            float deltaTime,
            ReadOnlySpan<ObstacleCircleXZ> blockers = default)
        {
            if (CurrentState != BallState.ControlledDribble || OwnerPlayerId == NoOwnerId || deltaTime <= 0f)
            {
                return;
            }

            if (_releasePickupLockTimer > 0f)
            {
                _releasePickupLockTimer = Mathf.Max(0f, _releasePickupLockTimer - deltaTime);
            }

            Vector2 planarDelta = new Vector2(Position.x - ownerPosition.x, Position.z - ownerPosition.z);
            if (planarDelta.magnitude > _tuning.pickupRadius * 1.85f ||
                IsSegmentBlockedByBlockerOnly(ownerPosition, Position, blockers))
            {
                // Player was separated from ball or obstructed by an obstacle/post; drop to Free state
                ReleaseToFreeBall(ownerVelocity * 0.5f, _tuning.pickupCooldownAfterReleaseSeconds);
                return;
            }

            Vector3 forwardDir = ownerForward.sqrMagnitude > 0.001f ? ownerForward.normalized : Vector3.forward;
            Vector3 desiredBallPos = ownerPosition + forwardDir * _tuning.dribbleLeadDistance;
            desiredBallPos.y = _tuning.ballRadius;

            // Prevent dribble target from clipping through goalposts or blockers
            if (IsSegmentBlockedByBlockerOnly(ownerPosition, desiredBallPos, blockers))
            {
                ReleaseToFreeBall(-forwardDir * 1.5f, _tuning.pickupCooldownAfterReleaseSeconds);
                return;
            }

            PreviousPosition = Position;
            Position = Vector3.Lerp(Position, desiredBallPos, 1f - Mathf.Exp(-_tuning.dribbleFollowSharpness * deltaTime));
            Position = new Vector3(Position.x, _tuning.ballRadius, Position.z);
            Velocity = ownerVelocity;
        }

        public bool ReleaseWithImpulse(
            int actingPlayerId,
            TeamSide actingTeam,
            Vector3 launchVelocity)
        {
            if (CurrentState == BallState.RestartReset)
            {
                return false;
            }

            if (CurrentState == BallState.ControlledDribble && OwnerPlayerId != actingPlayerId)
            {
                return false;
            }

            float speed = launchVelocity.magnitude;
            if (speed > _tuning.maxBallSpeed)
            {
                launchVelocity = launchVelocity * (_tuning.maxBallSpeed / speed);
            }

            OwnerPlayerId = NoOwnerId;
            LastTouchPlayerId = actingPlayerId;
            LastTouchTeam = actingTeam;
            _releaseLockedPlayerId = actingPlayerId;
            _releasePickupLockTimer = _tuning.pickupCooldownAfterReleaseSeconds;
            CurrentState = BallState.PassOrShotFlight;
            Velocity = launchVelocity;
            return true;
        }

        public void ForceTransferOrDislodgeOnTackle(
            int tacklerPlayerId,
            TeamSide tacklerTeam,
            Vector3 tacklerPosition,
            Vector3 tacklerForward,
            bool cleanSteal,
            float dislodgeImpulse)
        {
            int formerOwner = OwnerPlayerId;
            _releaseLockedPlayerId = formerOwner;
            _releasePickupLockTimer = _tuning.pickupCooldownAfterReleaseSeconds * 1.5f;

            if (cleanSteal)
            {
                ClaimPossessionInternal(tacklerPlayerId, tacklerTeam, tacklerPosition, tacklerForward);
            }
            else
            {
                OwnerPlayerId = NoOwnerId;
                LastTouchPlayerId = tacklerPlayerId;
                LastTouchTeam = tacklerTeam;
                CurrentState = BallState.Free;
                Vector3 pushDir = tacklerForward.sqrMagnitude > 0.001f ? tacklerForward.normalized : Vector3.forward;
                Velocity = pushDir * dislodgeImpulse;
            }
        }

        public void ReleaseToFreeBall(Vector3 residualVelocity, float lockDurationSeconds)
        {
            _releaseLockedPlayerId = OwnerPlayerId;
            _releasePickupLockTimer = Mathf.Max(0f, lockDurationSeconds);
            OwnerPlayerId = NoOwnerId;
            CurrentState = BallState.Free;
            Velocity = residualVelocity;
        }

        /// <summary>
        /// Steps free/flight ball physics with automatic sub-stepping for high-velocity shots
        /// so fast shots never tunnel through goal frames or goal-line planes.
        /// </summary>
        public void StepPhysics(float deltaTime)
        {
            if (deltaTime <= 0f || CurrentState == BallState.ControlledDribble || CurrentState == BallState.RestartReset)
            {
                return;
            }

            if (_releasePickupLockTimer > 0f)
            {
                _releasePickupLockTimer = Mathf.Max(0f, _releasePickupLockTimer - deltaTime);
                if (_releasePickupLockTimer <= 0f)
                {
                    _releaseLockedPlayerId = NoOwnerId;
                }
            }

            PreviousPosition = Position;

            float maxStepDistance = Mathf.Max(0.08f, _tuning.ballRadius * 0.75f);
            float expectedTravel = Velocity.magnitude * deltaTime;
            int subSteps = Mathf.Clamp(Mathf.CeilToInt(expectedTravel / maxStepDistance), 1, 16);
            float subDt = deltaTime / subSteps;

            for (int step = 0; step < subSteps; step++)
            {
                IntegrateSingleSubStep(subDt);
            }

            if (CurrentState == BallState.PassOrShotFlight &&
                Position.y <= _tuning.ballRadius + 0.02f &&
                Velocity.sqrMagnitude < 4.0f)
            {
                CurrentState = BallState.Free;
            }
        }

        private void IntegrateSingleSubStep(float dt)
        {
            Vector3 nextPos = Position + Velocity * dt;

            if (nextPos.y > _tuning.ballRadius + 0.001f || Velocity.y > 0.01f)
            {
                // Airborne
                Velocity -= new Vector3(0f, _tuning.gravity * dt, 0f);
                Velocity *= Mathf.Max(0f, 1f - _tuning.airDrag * dt);
            }
            else
            {
                // Ground roll friction
                nextPos.y = _tuning.ballRadius;
                Velocity = new Vector3(Velocity.x, 0f, Velocity.z);
                float planarSpeed = Velocity.magnitude;
                if (planarSpeed > 0.001f)
                {
                    float nextSpeed = Mathf.Max(0f, planarSpeed - _tuning.groundFriction * dt);
                    Velocity = Velocity * (nextSpeed / planarSpeed);
                }
            }

            if (nextPos.y < _tuning.ballRadius)
            {
                nextPos.y = _tuning.ballRadius;
                if (Velocity.y < -0.5f)
                {
                    Velocity = new Vector3(Velocity.x, -Velocity.y * _tuning.restitution, Velocity.z);
                }
                else
                {
                    Velocity = new Vector3(Velocity.x, 0f, Velocity.z);
                }
            }

            ResolveGoalPostAndNetCollisions(ref nextPos);
            Position = nextPos;
        }

        private void ResolveGoalPostAndNetCollisions(ref Vector3 candidatePos)
        {
            float postRadius = 0.10f;
            float combinedRadius = _tuning.ballRadius + postRadius;
            float halfGoalW = _pitchConfig.HalfGoalWidth;
            float halfPitchL = _pitchConfig.HalfLength;

            if (candidatePos.y <= _pitchConfig.goalHeight + _tuning.ballRadius)
            {
                BounceOffPostXZ(ref candidatePos, new Vector2(-halfGoalW, halfPitchL), combinedRadius);
                BounceOffPostXZ(ref candidatePos, new Vector2(halfGoalW, halfPitchL), combinedRadius);
                BounceOffPostXZ(ref candidatePos, new Vector2(-halfGoalW, -halfPitchL), combinedRadius);
                BounceOffPostXZ(ref candidatePos, new Vector2(halfGoalW, -halfPitchL), combinedRadius);
            }

            // Back net stop inside goal mouth so scored shots settle inside the net
            float maxGoalDepthZ = halfPitchL + _pitchConfig.goalDepth - _tuning.ballRadius;
            if (Mathf.Abs(candidatePos.x) <= halfGoalW && candidatePos.y <= _pitchConfig.goalHeight)
            {
                if (candidatePos.z > maxGoalDepthZ)
                {
                    candidatePos.z = maxGoalDepthZ;
                    if (Velocity.z > 0f) Velocity = new Vector3(Velocity.x * 0.35f, Velocity.y * 0.35f, -Velocity.z * 0.2f);
                }
                else if (candidatePos.z < -maxGoalDepthZ)
                {
                    candidatePos.z = -maxGoalDepthZ;
                    if (Velocity.z < 0f) Velocity = new Vector3(Velocity.x * 0.35f, Velocity.y * 0.35f, -Velocity.z * 0.2f);
                }
            }
        }

        private void BounceOffPostXZ(ref Vector3 candidatePos, Vector2 postXZ, float combinedRadius)
        {
            Vector2 delta = new Vector2(candidatePos.x - postXZ.x, candidatePos.z - postXZ.y);
            float distSq = delta.sqrMagnitude;
            if (distSq >= combinedRadius * combinedRadius || distSq < 0.000001f)
            {
                return;
            }

            float dist = Mathf.Sqrt(distSq);
            Vector2 normal = delta / dist;
            candidatePos.x = postXZ.x + normal.x * combinedRadius;
            candidatePos.z = postXZ.y + normal.y * combinedRadius;

            Vector2 velXZ = new Vector2(Velocity.x, Velocity.z);
            float vn = Vector2.Dot(velXZ, normal);
            if (vn < 0f)
            {
                Vector2 reflected = (velXZ - (1f + _tuning.restitution) * vn * normal) * 0.85f;
                Velocity = new Vector3(reflected.x, Velocity.y, reflected.y);
            }
        }

        private void ClaimPossessionInternal(int playerId, TeamSide team, Vector3 playerPosition, Vector3 playerForward)
        {
            OwnerPlayerId = playerId;
            LastTouchPlayerId = playerId;
            LastTouchTeam = team;
            CurrentState = BallState.ControlledDribble;
            Vector3 forward = playerForward.sqrMagnitude > 0.001f ? playerForward.normalized : Vector3.forward;
            PreviousPosition = Position;
            Position = new Vector3(
                playerPosition.x + forward.x * _tuning.dribbleLeadDistance,
                _tuning.ballRadius,
                playerPosition.z + forward.z * _tuning.dribbleLeadDistance);
            Velocity = Vector3.zero;
        }

        private static bool IsSegmentBlockedByObstacleOrOpponent(
            Vector3 playerPos,
            Vector3 ballPos,
            int selfPlayerId,
            ReadOnlySpan<PossessionCandidate> allCandidates,
            ReadOnlySpan<ObstacleCircleXZ> blockers)
        {
            if (IsSegmentBlockedByBlockerOnly(playerPos, ballPos, blockers))
            {
                return true;
            }

            Vector2 a = new Vector2(playerPos.x, playerPos.z);
            Vector2 b = new Vector2(ballPos.x, ballPos.z);

            for (int i = 0; i < allCandidates.Length; i++)
            {
                if (allCandidates[i].playerId == selfPlayerId)
                {
                    continue;
                }

                Vector2 c = new Vector2(allCandidates[i].position.x, allCandidates[i].position.z);
                float r = Mathf.Max(0.2f, allCandidates[i].playerRadius * 0.85f);
                if (DistancePointToSegmentXZ(c, a, b) < r)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSegmentBlockedByBlockerOnly(
            Vector3 startPos,
            Vector3 endPos,
            ReadOnlySpan<ObstacleCircleXZ> blockers)
        {
            if (blockers.Length == 0)
            {
                return false;
            }

            Vector2 a = new Vector2(startPos.x, startPos.z);
            Vector2 b = new Vector2(endPos.x, endPos.z);

            for (int i = 0; i < blockers.Length; i++)
            {
                Vector2 c = new Vector2(blockers[i].center.x, blockers[i].center.z);
                if (DistancePointToSegmentXZ(c, a, b) < blockers[i].radius)
                {
                    return true;
                }
            }

            return false;
        }

        private static float DistancePointToSegmentXZ(Vector2 point, Vector2 segA, Vector2 segB)
        {
            Vector2 ab = segB - segA;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 0.00001f)
            {
                return Vector2.Distance(point, segA);
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - segA, ab) / lenSq);
            Vector2 projection = segA + ab * t;
            return Vector2.Distance(point, projection);
        }
    }
}
