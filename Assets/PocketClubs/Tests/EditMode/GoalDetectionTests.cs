using NUnit.Framework;
using PocketClubs.Core;
using PocketClubs.Gameplay;
using UnityEngine;

namespace PocketClubs.Tests.EditMode
{
    [TestFixture]
    public sealed class GoalDetectionTests
    {
        private PitchConfig _pitchConfig;
        private BallTuningConfig _ballTuning;
        private GoalAndBoundaryDetector _detector;

        [SetUp]
        public void SetUp()
        {
            _pitchConfig = PitchConfig.CreateRuntimeDefault();
            _ballTuning = BallTuningConfig.CreateRuntimeDefault();
            _detector = new GoalAndBoundaryDetector(_pitchConfig);
        }

        [Test]
        public void FullCrossingWithinGoalOpening_CountsSingleGoal_AndPreventsDuplicateCallbacks()
        {
            float r = _ballTuning.ballRadius;
            float lineZ = _pitchConfig.HalfLength;

            Vector3 beforeLine = new Vector3(0f, r, lineZ - 0.30f);
            Vector3 fullyAcrossLine = new Vector3(0f, r, lineZ + r + 0.08f);

            GoalEvaluationEvent firstEval = _detector.EvaluateGoal(beforeLine, fullyAcrossLine, r, BallState.PassOrShotFlight);
            Assert.That(firstEval.isGoalScored, Is.True);
            Assert.That(firstEval.scoringTeam, Is.EqualTo(TeamSide.Home));

            // Subsequent frame inside net must NOT double-count the goal
            Vector3 deeperInNet = new Vector3(0f, r, lineZ + r + 0.45f);
            GoalEvaluationEvent duplicateEval = _detector.EvaluateGoal(fullyAcrossLine, deeperInNet, r, BallState.PassOrShotFlight);
            Assert.That(duplicateEval.isGoalScored, Is.False);
        }

        [Test]
        public void PartialCrossingOnGoalLine_DoesNotCountAsGoal()
        {
            float r = _ballTuning.ballRadius;
            float lineZ = _pitchConfig.HalfLength;

            // Ball center is slightly past lineZ, but trailing edge (z - r) is still touching/inside the line
            Vector3 beforeLine = new Vector3(0f, r, lineZ - 0.2f);
            Vector3 partialCross = new Vector3(0f, r, lineZ + r * 0.5f);

            GoalEvaluationEvent eval = _detector.EvaluateGoal(beforeLine, partialCross, r, BallState.PassOrShotFlight);
            Assert.That(eval.isGoalScored, Is.False);
        }

        [Test]
        public void CrossingOutsideGoalPosts_DoesNotCountAsGoal()
        {
            float r = _ballTuning.ballRadius;
            float lineZ = _pitchConfig.HalfLength;
            float outsidePostX = _pitchConfig.HalfGoalWidth + 0.6f;

            Vector3 beforeLine = new Vector3(outsidePostX, r, lineZ - 0.5f);
            Vector3 pastLineWide = new Vector3(outsidePostX, r, lineZ + r + 0.3f);

            GoalEvaluationEvent eval = _detector.EvaluateGoal(beforeLine, pastLineWide, r, BallState.PassOrShotFlight);
            Assert.That(eval.isGoalScored, Is.False);
        }

        [Test]
        public void CrossingAboveCrossbar_DoesNotCountAsGoal()
        {
            float r = _ballTuning.ballRadius;
            float lineZ = _pitchConfig.HalfLength;
            float aboveBarY = _pitchConfig.goalHeight + 0.4f;

            Vector3 beforeLine = new Vector3(0f, aboveBarY, lineZ - 0.5f);
            Vector3 pastLineHigh = new Vector3(0f, aboveBarY, lineZ + r + 0.3f);

            GoalEvaluationEvent eval = _detector.EvaluateGoal(beforeLine, pastLineHigh, r, BallState.PassOrShotFlight);
            Assert.That(eval.isGoalScored, Is.False);
        }

        [Test]
        public void ResetTeleport_DoesNotTriggerGoal()
        {
            float r = _ballTuning.ballRadius;
            float lineZ = _pitchConfig.HalfLength;

            _detector.NotifyResetOrTeleport();

            Vector3 fromCenter = new Vector3(0f, r, 0f);
            Vector3 intoGoal = new Vector3(0f, r, lineZ + r + 0.25f);

            GoalEvaluationEvent evalOnTeleportStep = _detector.EvaluateGoal(fromCenter, intoGoal, r, BallState.Free);
            Assert.That(evalOnTeleportStep.isGoalScored, Is.False);

            GoalEvaluationEvent evalWhileInRestartResetState = _detector.EvaluateGoal(fromCenter, intoGoal, r, BallState.RestartReset);
            Assert.That(evalWhileInRestartResetState.isGoalScored, Is.False);
        }

        [Test]
        public void FastShotSubStepping_CrossesGoalLineAndSettlesInNetWithoutTunneling()
        {
            float r = _ballTuning.ballRadius;
            float lineZ = _pitchConfig.HalfLength;
            var ballSim = new BallSimulation(_ballTuning, _pitchConfig, new Vector3(0f, r, lineZ - 2.0f));

            // Fire a maximum-velocity shot straight into the center of the North goal
            bool released = ballSim.ReleaseWithImpulse(1, TeamSide.Home, new Vector3(0f, 0.5f, _ballTuning.maxBallSpeed));
            Assert.That(released, Is.True);

            bool goalDetected = false;
            for (int step = 0; step < 10 && !goalDetected; step++)
            {
                ballSim.StepPhysics(1f / 30f);
                GoalEvaluationEvent evt = _detector.EvaluateGoal(
                    ballSim.PreviousPosition,
                    ballSim.Position,
                    ballSim.BallRadius,
                    ballSim.CurrentState);
                if (evt.isGoalScored)
                {
                    goalDetected = true;
                }
            }

            Assert.That(goalDetected, Is.True);
            Assert.That(ballSim.Position.z, Is.LessThanOrEqualTo(lineZ + _pitchConfig.goalDepth));
        }
    }
}
