using NUnit.Framework;
using PocketClubs.Core;
using PocketClubs.Gameplay;
using PocketClubs.Input;
using UnityEngine;

namespace PocketClubs.Tests.EditMode
{
    [TestFixture]
    public sealed class MovementAndInputTests
    {
        private MovementTuningConfig _movementTuning;
        private PitchConfig _pitchConfig;

        [SetUp]
        public void SetUp()
        {
            _movementTuning = MovementTuningConfig.CreateRuntimeDefault();
            _pitchConfig = PitchConfig.CreateRuntimeDefault();
        }

        [Test]
        public void DiagonalInput_IsNormalizedToUnitMagnitude()
        {
            Vector2 rawDiagonal = new Vector2(1f, 1f);
            Vector2 normalized = PlayerCommandIntent.NormalizeDiagonalInput(rawDiagonal);

            Assert.That(normalized.magnitude, Is.EqualTo(1.0f).Within(0.0001f));
            Assert.That(normalized.x, Is.EqualTo(0.7071f).Within(0.001f));
            Assert.That(normalized.y, Is.EqualTo(0.7071f).Within(0.001f));
        }

        [Test]
        public void InvalidNaNOrInfiniteInput_SanitizesToZero()
        {
            Vector2 nanInput = new Vector2(float.NaN, 1f);
            Vector2 infInput = new Vector2(float.PositiveInfinity, -1f);

            Assert.That(PlayerCommandIntent.NormalizeDiagonalInput(nanInput), Is.EqualTo(Vector2.zero));
            Assert.That(PlayerCommandIntent.NormalizeDiagonalInput(infInput), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void CameraRelativeMove_RotatesCorrectlyWithCameraYaw()
        {
            // Pushing "up" (0, 1) with 90-degree yaw should point along +X in world XZ
            Vector2 worldAt90Yaw = PlayerCommandIntent.ConvertCameraRelativeToWorldXZ(new Vector2(0f, 1f), 90f);
            Assert.That(worldAt90Yaw.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(worldAt90Yaw.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void MovementMotor_EnforcesWalkSprintAndDribbleSpeedBounds()
        {
            var motor = new PlayerMovementMotor(_movementTuning, _pitchConfig, Vector3.zero, Vector3.forward);
            var walkIntent = new PlayerCommandIntent
            {
                worldMoveDirection = new Vector2(1f, 1f).normalized,
                sprintHeld = false
            };

            // Step for 2 seconds without sprint
            for (int i = 0; i < 60; i++)
            {
                motor.Step(in walkIntent, 1f / 30f, hasBallPossession: false);
            }

            Assert.That(motor.CurrentPlanarSpeed, Is.LessThanOrEqualTo(_movementTuning.baseMaxSpeed + 0.001f));
            Assert.That(motor.CurrentPlanarSpeed, Is.EqualTo(_movementTuning.baseMaxSpeed).Within(0.05f));

            // Step with sprint held
            var sprintIntent = new PlayerCommandIntent
            {
                worldMoveDirection = Vector2.up,
                sprintHeld = true
            };
            for (int i = 0; i < 20; i++)
            {
                motor.Step(in sprintIntent, 1f / 30f, hasBallPossession: false);
            }

            float expectedSprintMax = _movementTuning.baseMaxSpeed * _movementTuning.sprintSpeedMultiplier;
            Assert.That(motor.CurrentPlanarSpeed, Is.LessThanOrEqualTo(expectedSprintMax + 0.001f));
            Assert.That(motor.CurrentPlanarSpeed, Is.GreaterThan(_movementTuning.baseMaxSpeed));

            // Step with ball possession (dribble multiplier cap)
            for (int i = 0; i < 20; i++)
            {
                motor.Step(in walkIntent, 1f / 30f, hasBallPossession: true);
            }

            float expectedDribbleMax = _movementTuning.baseMaxSpeed * _movementTuning.dribbleSpeedMultiplier;
            Assert.That(motor.CurrentPlanarSpeed, Is.LessThanOrEqualTo(expectedDribbleMax + 0.01f));
        }

        [Test]
        public void InputCancellation_ImmediatelyHaltsSprintAndZeroesVelocity()
        {
            var motor = new PlayerMovementMotor(_movementTuning, _pitchConfig, Vector3.zero, Vector3.forward);
            var sprintIntent = new PlayerCommandIntent
            {
                worldMoveDirection = Vector2.up,
                sprintHeld = true
            };

            for (int i = 0; i < 15; i++)
            {
                motor.Step(in sprintIntent, 1f / 30f, hasBallPossession: false);
            }

            Assert.That(motor.IsSprinting, Is.True);
            Assert.That(motor.CurrentPlanarSpeed, Is.GreaterThan(1f));

            PlayerCommandIntent cancelIntent = PlayerCommandIntent.CreateCancelled(99);
            motor.Step(in cancelIntent, 1f / 30f, hasBallPossession: false);

            Assert.That(motor.IsSprinting, Is.False);
            Assert.That(motor.CurrentPlanarSpeed, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Stamina_DrainsOnSprint_EnforcesExhaustionThreshold_AndRegeneratesAfterDelay()
        {
            var motor = new PlayerMovementMotor(_movementTuning, _pitchConfig, Vector3.zero, Vector3.forward);
            var sprintIntent = new PlayerCommandIntent
            {
                worldMoveDirection = Vector2.up,
                sprintHeld = true
            };

            // Drain stamina completely to 0
            for (int i = 0; i < 200; i++)
            {
                motor.Step(in sprintIntent, 0.05f, hasBallPossession: false);
            }

            Assert.That(motor.Stamina, Is.EqualTo(0f).Within(0.001f));
            Assert.That(motor.IsExhausted, Is.True);
            Assert.That(motor.IsSprinting, Is.False);

            // Rest for less than staminaRegenDelaySeconds -> stamina must remain 0
            var idleIntent = new PlayerCommandIntent { worldMoveDirection = Vector2.zero, sprintHeld = false };
            motor.Step(in idleIntent, _movementTuning.staminaRegenDelaySeconds * 0.5f, hasBallPossession: false);
            Assert.That(motor.Stamina, Is.EqualTo(0f).Within(0.001f));

            // Advance past regen delay -> stamina regenerates
            motor.Step(in idleIntent, _movementTuning.staminaRegenDelaySeconds + 0.5f, hasBallPossession: false);
            Assert.That(motor.Stamina, Is.GreaterThan(0f));

            // Recover above minStaminaToStartSprint -> exhaustion clears and sprint can re-engage
            for (int i = 0; i < 60; i++)
            {
                motor.Step(in idleIntent, 0.1f, hasBallPossession: false);
            }

            Assert.That(motor.Stamina, Is.GreaterThanOrEqualTo(_movementTuning.minStaminaToStartSprint));
            Assert.That(motor.IsExhausted, Is.False);
            Assert.That(motor.Stamina, Is.LessThanOrEqualTo(_movementTuning.maxStamina));
        }
    }
}
