using System;
using NUnit.Framework;
using PocketClubs.Core;
using PocketClubs.Gameplay;
using UnityEngine;

namespace PocketClubs.Tests.EditMode
{
    [TestFixture]
    public sealed class BallAndActionsTests
    {
        private BallTuningConfig _ballTuning;
        private ActionTuningConfig _actionTuning;
        private PitchConfig _pitchConfig;

        [SetUp]
        public void SetUp()
        {
            _ballTuning = BallTuningConfig.CreateRuntimeDefault();
            _actionTuning = ActionTuningConfig.CreateRuntimeDefault();
            _pitchConfig = PitchConfig.CreateRuntimeDefault();
        }

        [Test]
        public void PassingService_UsesDirectionalFallbackWhenNoTeammateExists_AndClampsPower()
        {
            var passing = new PassingService(_actionTuning);
            Vector2 aimDir = new Vector2(1f, 0f);

            PassExecutionResult minPass = passing.ComputePass(
                Vector3.zero,
                Vector3.forward,
                aimDir,
                requestedPowerNormalized: -2f,
                ReadOnlySpan<TeammatePassTarget>.Empty);

            Assert.That(minPass.wasAssisted, Is.False);
            Assert.That(minPass.speed, Is.EqualTo(_actionTuning.minPassSpeed).Within(0.001f));
            Assert.That(minPass.directionXZ.x, Is.EqualTo(1f).Within(0.001f));

            PassExecutionResult maxPass = passing.ComputePass(
                Vector3.zero,
                Vector3.forward,
                aimDir,
                requestedPowerNormalized: 5f,
                ReadOnlySpan<TeammatePassTarget>.Empty);

            Assert.That(maxPass.speed, Is.EqualTo(_actionTuning.maxPassSpeed).Within(0.001f));
        }

        [Test]
        public void PassingService_SelectsEligibleTeammateWithinAngleAndDistance()
        {
            var passing = new PassingService(_actionTuning);
            TeammatePassTarget[] mates =
            {
                new TeammatePassTarget { playerId = 2, worldPosition = new Vector3(2f, 0f, 12f) },
                new TeammatePassTarget { playerId = 3, worldPosition = new Vector3(-20f, 0f, -15f) } // behind passer
            };

            PassExecutionResult result = passing.ComputePass(
                Vector3.zero,
                Vector3.forward,
                new Vector2(0f, 1f),
                requestedPowerNormalized: 0.5f,
                mates);

            Assert.That(result.wasAssisted, Is.True);
            Assert.That(result.assistedTargetPlayerId, Is.EqualTo(2));
            Assert.That(result.directionXZ.x, Is.GreaterThan(0.05f));
            Assert.That(result.speed, Is.InRange(_actionTuning.minPassSpeed, _actionTuning.maxPassSpeed));
        }

        [Test]
        public void ShotChargeController_ClampsChargeAndPower_AndSafelyCancelsInterruptedCharge()
        {
            var shooter = new ShotChargeController(_actionTuning);

            // Overcharge well past maxShotChargeSeconds
            shooter.BeginOrContinueCharge(_actionTuning.maxShotChargeSeconds * 4.0f);
            Assert.That(shooter.IsCharging, Is.True);
            Assert.That(shooter.CurrentChargeSeconds, Is.EqualTo(_actionTuning.maxShotChargeSeconds).Within(0.0001f));
            Assert.That(shooter.NormalizedCharge, Is.EqualTo(1.0f).Within(0.0001f));

            ShotExecutionResult shot = shooter.ReleaseShot(
                Vector3.zero,
                Vector3.forward,
                new Vector2(0f, 1f),
                new Vector3(0f, 1f, _pitchConfig.HalfLength));

            Assert.That(shot.executed, Is.True);
            Assert.That(shot.speed, Is.LessThanOrEqualTo(_actionTuning.maxShotSpeed + 0.001f));
            Assert.That(shot.speed, Is.GreaterThanOrEqualTo(_actionTuning.minShotSpeed - 0.001f));
            Assert.That(shooter.IsCharging, Is.False);

            // Begin another charge and cancel it before release
            shooter.BeginOrContinueCharge(0.4f);
            shooter.CancelCharge();
            Assert.That(shooter.IsCharging, Is.False);
            Assert.That(shooter.CurrentChargeSeconds, Is.EqualTo(0f));

            ShotExecutionResult afterCancel = shooter.ReleaseShot(
                Vector3.zero,
                Vector3.forward,
                new Vector2(0f, 1f),
                new Vector3(0f, 1f, _pitchConfig.HalfLength));
            Assert.That(afterCancel.executed, Is.False);
        }

        [Test]
        public void TackleService_ValidatesReachFacingCooldownAndPossessionState()
        {
            var ball = new BallSimulation(_ballTuning, _pitchConfig, Vector3.zero);
            var tackle = new TackleService(_actionTuning);

            // 1. Reject when ball is Free (nobody holds possession)
            TackleAttemptResult noOwnerResult = tackle.AttemptStandingTackle(
                tacklerPlayerId: 2,
                tacklerTeam: TeamSide.Away,
                tacklerPosition: new Vector3(0f, 0f, -0.8f),
                tacklerForward: Vector3.forward,
                ball);
            Assert.That(noOwnerResult, Is.EqualTo(TackleAttemptResult.RejectedNoPossessionTarget));

            // 2. Immediately attempting again must be rejected by cooldown
            TackleAttemptResult cooldownResult = tackle.AttemptStandingTackle(
                2, TeamSide.Away, new Vector3(0f, 0f, -0.8f), Vector3.forward, ball);
            Assert.That(cooldownResult, Is.EqualTo(TackleAttemptResult.RejectedCooldown));

            // Tick past cooldown and give Player 1 possession
            tackle.TickCooldown(_actionTuning.tackleCooldownSeconds + 0.1f);
            bool p1Claimed = ball.TryClaimPossession(1, TeamSide.Home, Vector3.zero, Vector3.forward);
            Assert.That(p1Claimed, Is.True);

            // 3. Reject remote possession steal (distance > tackleReachDistance)
            TackleAttemptResult outOfRange = tackle.AttemptStandingTackle(
                2, TeamSide.Away, new Vector3(0f, 0f, -6.0f), Vector3.forward, ball);
            Assert.That(outOfRange, Is.EqualTo(TackleAttemptResult.MissedOutOfRange));
            Assert.That(ball.OwnerPlayerId, Is.EqualTo(1));

            // 4. Reject wrong facing angle (tackler facing away from ball carrier)
            tackle.TickCooldown(_actionTuning.tackleCooldownSeconds + 0.1f);
            TackleAttemptResult wrongFacing = tackle.AttemptStandingTackle(
                2, TeamSide.Away, new Vector3(0f, 0f, 1.2f), Vector3.forward, ball);
            Assert.That(wrongFacing, Is.EqualTo(TackleAttemptResult.MissedWrongFacing));
            Assert.That(ball.OwnerPlayerId, Is.EqualTo(1));

            // 5. Succeed when within reach and facing the ball carrier
            tackle.TickCooldown(_actionTuning.tackleCooldownSeconds + 0.1f);
            TackleAttemptResult validTackle = tackle.AttemptStandingTackle(
                2, TeamSide.Away, new Vector3(0f, 0f, 1.2f), Vector3.back, ball);
            Assert.That(
                validTackle == TackleAttemptResult.SuccessSteal || validTackle == TackleAttemptResult.SuccessDislodge,
                Is.True);
            Assert.That(ball.OwnerPlayerId, Is.Not.EqualTo(1));
        }

        [Test]
        public void BallPossession_RejectsPickupThroughObstacles_AndResolvesSimultaneousContestDeterministically()
        {
            var ball = new BallSimulation(_ballTuning, _pitchConfig, Vector3.zero);

            // Blocker directly between player and ball prevents attachment through obstacles
            ObstacleCircleXZ[] obstacles =
            {
                new ObstacleCircleXZ { center = new Vector3(0f, 0f, -0.35f), radius = 0.25f }
            };

            bool blockedClaim = ball.TryClaimPossession(
                1,
                TeamSide.Home,
                new Vector3(0f, 0f, -0.70f),
                Vector3.forward,
                obstacles);
            Assert.That(blockedClaim, Is.False);
            Assert.That(ball.CurrentState, Is.EqualTo(BallState.Free));

            // Simultaneous contest between two players at identical distance resolves deterministically
            PossessionCandidate[] contenders =
            {
                new PossessionCandidate
                {
                    playerId = 5,
                    team = TeamSide.Away,
                    position = new Vector3(0.6f, 0f, 0f),
                    forward = Vector3.left,
                    playerRadius = 0.45f
                },
                new PossessionCandidate
                {
                    playerId = 2,
                    team = TeamSide.Home,
                    position = new Vector3(-0.6f, 0f, 0f),
                    forward = Vector3.right,
                    playerRadius = 0.45f
                }
            };

            bool contestResolved = ball.TryEvaluatePossessionContest(
                contenders,
                ReadOnlySpan<ObstacleCircleXZ>.Empty,
                out int winnerId);

            Assert.That(contestResolved, Is.True);
            Assert.That(winnerId, Is.EqualTo(2));
            Assert.That(ball.OwnerPlayerId, Is.EqualTo(2));
            Assert.That(ball.CurrentState, Is.EqualTo(BallState.ControlledDribble));
        }
    }
}
