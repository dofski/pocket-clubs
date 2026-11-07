using System;
using PocketClubs.Core;
using UnityEngine;

namespace PocketClubs.Gameplay
{
    [Serializable]
    public struct GoalEvaluationEvent
    {
        public bool isGoalScored;
        public TeamSide scoringTeam;
        public TeamSide defendedGoalSide;
        public Vector3 crossingPoint;
    }

    [Serializable]
    public struct OutOfPlayRestartEvent
    {
        public bool isOutOfPlay;
        public RestartType restartType;
        public TeamSide awardedTeam;
        public Vector3 restartPosition;
    }

    /// <summary>
    /// Evaluates goal line crossings and simplified out-of-play restarts.
    /// Counts a goal ONLY when the entire ball sphere crosses the goal line inside the goal opening
    /// (between the inner post faces and beneath the crossbar) via valid entry across the front plane.
    /// Prevents duplicate scoring, outside-post crossings, above-crossbar crossings, partial crossings,
    /// and reset teleports triggering goals.
    /// </summary>
    public sealed class GoalAndBoundaryDetector
    {
        private readonly PitchConfig _pitchConfig;
        private bool _isGoalLatchedUntilReset;
        private bool _ignoreNextStepAfterTeleport;

        public bool IsGoalLatched => _isGoalLatchedUntilReset;

        public GoalAndBoundaryDetector(PitchConfig pitchConfig)
        {
            _pitchConfig = pitchConfig != null ? pitchConfig : PitchConfig.CreateRuntimeDefault();
        }

        public void NotifyResetOrTeleport()
        {
            _isGoalLatchedUntilReset = false;
            _ignoreNextStepAfterTeleport = true;
        }

        public GoalEvaluationEvent EvaluateGoal(
            Vector3 previousBallPos,
            Vector3 currentBallPos,
            float ballRadius,
            BallState currentBallState)
        {
            if (currentBallState == BallState.RestartReset)
            {
                return default;
            }

            if (_ignoreNextStepAfterTeleport)
            {
                _ignoreNextStepAfterTeleport = false;
                return default;
            }

            if (_isGoalLatchedUntilReset)
            {
                return default;
            }

            float halfLength = _pitchConfig.HalfLength;
            float halfGoalWidth = _pitchConfig.HalfGoalWidth;
            float goalHeight = _pitchConfig.goalHeight;

            // Check North Goal (+Z, defended by Away team -> Home scores)
            if (DidEntireBallCrossGoalOpening(
                    previousBallPos,
                    currentBallPos,
                    ballRadius,
                    goalLineZ: halfLength,
                    outwardSign: 1f,
                    halfGoalWidth: halfGoalWidth,
                    goalHeight: goalHeight,
                    out Vector3 northCrossingPoint))
            {
                _isGoalLatchedUntilReset = true;
                return new GoalEvaluationEvent
                {
                    isGoalScored = true,
                    scoringTeam = TeamSide.Home,
                    defendedGoalSide = TeamSide.Away,
                    crossingPoint = northCrossingPoint
                };
            }

            // Check South Goal (-Z, defended by Home team -> Away scores)
            if (DidEntireBallCrossGoalOpening(
                    previousBallPos,
                    currentBallPos,
                    ballRadius,
                    goalLineZ: -halfLength,
                    outwardSign: -1f,
                    halfGoalWidth: halfGoalWidth,
                    goalHeight: goalHeight,
                    out Vector3 southCrossingPoint))
            {
                _isGoalLatchedUntilReset = true;
                return new GoalEvaluationEvent
                {
                    isGoalScored = true,
                    scoringTeam = TeamSide.Away,
                    defendedGoalSide = TeamSide.Home,
                    crossingPoint = southCrossingPoint
                };
            }

            return default;
        }

        /// <summary>
        /// Simplified documented out-of-play restart rules:
        /// - Entire ball beyond touchline (+-HalfWidth): SidelineThrowIn (ground kick/pass-in from touchline spot)
        ///   awarded to opponent of LastTouchTeam.
        /// - Entire ball beyond goal line (+-HalfLength) outside goal opening:
        ///   - If last touched by defending team -> CornerKick from nearest corner arc.
        ///   - Otherwise -> GoalKick from defending goal area.
        /// </summary>
        public OutOfPlayRestartEvent EvaluateOutOfPlay(
            Vector3 currentBallPos,
            float ballRadius,
            TeamSide lastTouchTeam,
            BallState currentBallState)
        {
            if (_isGoalLatchedUntilReset || currentBallState == BallState.RestartReset)
            {
                return default;
            }

            float halfWidth = _pitchConfig.HalfWidth;
            float halfLength = _pitchConfig.HalfLength;
            TeamSide awarded = GetOppositeTeam(lastTouchTeam);

            // Sideline check
            if (Mathf.Abs(currentBallPos.x) - ballRadius > halfWidth)
            {
                float clampedX = Mathf.Sign(currentBallPos.x) * (halfWidth - 0.35f);
                float clampedZ = Mathf.Clamp(currentBallPos.z, -halfLength + 1.0f, halfLength - 1.0f);
                return new OutOfPlayRestartEvent
                {
                    isOutOfPlay = true,
                    restartType = RestartType.SidelineThrowIn,
                    awardedTeam = awarded,
                    restartPosition = new Vector3(clampedX, ballRadius, clampedZ)
                };
            }

            // North goal line (+Z, defended by Away)
            if (currentBallPos.z - ballRadius > halfLength)
            {
                bool defenderTouchedLast = lastTouchTeam == TeamSide.Away;
                RestartType type = defenderTouchedLast ? RestartType.CornerKick : RestartType.GoalKick;
                TeamSide restartTeam = defenderTouchedLast ? TeamSide.Home : TeamSide.Away;
                Vector3 spot = defenderTouchedLast
                    ? new Vector3(Mathf.Sign(currentBallPos.x == 0f ? 1f : currentBallPos.x) * (halfWidth - 0.8f), ballRadius, halfLength - 0.8f)
                    : new Vector3(0f, ballRadius, halfLength - 4.0f);

                return new OutOfPlayRestartEvent
                {
                    isOutOfPlay = true,
                    restartType = type,
                    awardedTeam = restartTeam,
                    restartPosition = spot
                };
            }

            // South goal line (-Z, defended by Home)
            if (currentBallPos.z + ballRadius < -halfLength)
            {
                bool defenderTouchedLast = lastTouchTeam == TeamSide.Home;
                RestartType type = defenderTouchedLast ? RestartType.CornerKick : RestartType.GoalKick;
                TeamSide restartTeam = defenderTouchedLast ? TeamSide.Away : TeamSide.Home;
                Vector3 spot = defenderTouchedLast
                    ? new Vector3(Mathf.Sign(currentBallPos.x == 0f ? 1f : currentBallPos.x) * (halfWidth - 0.8f), ballRadius, -halfLength + 0.8f)
                    : new Vector3(0f, ballRadius, -halfLength + 4.0f);

                return new OutOfPlayRestartEvent
                {
                    isOutOfPlay = true,
                    restartType = type,
                    awardedTeam = restartTeam,
                    restartPosition = spot
                };
            }

            return default;
        }

        private static bool DidEntireBallCrossGoalOpening(
            Vector3 prevPos,
            Vector3 currPos,
            float ballRadius,
            float goalLineZ,
            float outwardSign,
            float halfGoalWidth,
            float goalHeight,
            out Vector3 crossingPoint)
        {
            crossingPoint = currPos;

            // For the entire sphere to have crossed the line in outwardSign direction (+1 or -1):
            // trailing edge along Z must be strictly past goalLineZ.
            float trailingDistancePastLine = (currPos.z - goalLineZ) * outwardSign - ballRadius;
            if (trailingDistancePastLine <= 0f)
            {
                return false;
            }

            // Verify the ball did not start outside the pitch and enter through the side/back netting
            float prevSignedOffset = (prevPos.z - goalLineZ) * outwardSign;
            if (prevSignedOffset > ballRadius && Mathf.Abs(prevPos.x) > halfGoalWidth)
            {
                return false;
            }

            // Compute interpolated position at the exact plane where the trailing edge crossed the goal line
            float targetCenterZ = goalLineZ + outwardSign * ballRadius;
            float deltaZ = currPos.z - prevPos.z;
            Vector3 checkPoint = currPos;

            if (Mathf.Abs(deltaZ) > 0.0001f)
            {
                float t = Mathf.Clamp01((targetCenterZ - prevPos.z) / deltaZ);
                checkPoint = Vector3.Lerp(prevPos, currPos, t);
            }

            // Entire ball must be inside the inner post width and underneath the crossbar at crossing
            bool insidePostsAtCrossing = Mathf.Abs(checkPoint.x) + ballRadius <= halfGoalWidth;
            bool insidePostsAtCurrent = Mathf.Abs(currPos.x) + ballRadius <= halfGoalWidth;
            if (!insidePostsAtCrossing || !insidePostsAtCurrent)
            {
                return false;
            }

            bool belowCrossbarAtCrossing = checkPoint.y + ballRadius <= goalHeight && checkPoint.y - ballRadius >= -0.05f;
            bool belowCrossbarAtCurrent = currPos.y + ballRadius <= goalHeight && currPos.y - ballRadius >= -0.05f;
            if (!belowCrossbarAtCrossing || !belowCrossbarAtCurrent)
            {
                return false;
            }

            crossingPoint = checkPoint;
            return true;
        }

        private static TeamSide GetOppositeTeam(TeamSide team)
        {
            if (team == TeamSide.Home) return TeamSide.Away;
            if (team == TeamSide.Away) return TeamSide.Home;
            return TeamSide.Home;
        }
    }
}
