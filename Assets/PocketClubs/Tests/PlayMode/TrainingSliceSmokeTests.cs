using System.Collections;
using NUnit.Framework;
using PocketClubs.Core;
using PocketClubs.Gameplay;
using PocketClubs.Persistence;
using UnityEngine;
using UnityEngine.TestTools;

namespace PocketClubs.Tests.PlayMode
{
    /// <summary>
    /// PlayMode smoke tests verifying Gate 1 end-to-end acceptance flow:
    /// Launch -> Training -> Move -> Pass/Shoot -> Score once -> Reset -> Play again,
    /// simultaneous multitouch ownership, pause/resume, and settings persistence.
    /// </summary>
    public sealed class TrainingSliceSmokeTests
    {
        private GameObject _coordinatorHost;
        private TrainingSliceCoordinator _coordinator;

        [SetUp]
        public void SetUp()
        {
            TrainingSliceCoordinator.SuppressAutoBootstrapForTests = true;
            _coordinatorHost = new GameObject("TestTrainingCoordinatorHost");
            _coordinator = _coordinatorHost.AddComponent<TrainingSliceCoordinator>();
            _coordinator.InitializeIfNeeded();
        }

        [TearDown]
        public void TearDown()
        {
            if (_coordinatorHost != null)
            {
                Object.DestroyImmediate(_coordinatorHost);
            }
            TrainingSliceCoordinator.SuppressAutoBootstrapForTests = false;
        }

        [UnityTest]
        public IEnumerator AcceptanceFlow_LaunchToTraining_MoveShootScoreResetAndPlayAgain()
        {
            yield return null;

            // 1. Verify Launch Screen is visible on boot
            Assert.That(_coordinator.UiView.IsLaunchScreenVisible, Is.True);
            Assert.That(_coordinator.MatchState.CurrentPhase, Is.EqualTo(MatchPhase.Waiting));

            // 2. Transition Launch -> Training
            _coordinator.StartTrainingSession();
            yield return null;

            Assert.That(_coordinator.UiView.IsLaunchScreenVisible, Is.False);
            Assert.That(_coordinator.UiView.IsTrainingHudVisible, Is.True);
            Assert.That(_coordinator.MatchState.CurrentPhase, Is.EqualTo(MatchPhase.Playing));
            Assert.That(_coordinator.MatchState.HomeScore, Is.EqualTo(0));

            // 3. Move forward toward the ball to claim possession
            Vector3 startPlayerPos = _coordinator.ControlledPlayer.Motor.Position;
            _coordinator.UiView.JoystickZone.SimulateTouchDownAndDrag(pointerId: 1, new Vector2(0f, 85f));
            for (int i = 0; i < 20; i++)
            {
                _coordinator.StepTrainingSimulation(1f / 30f);
            }
            _coordinator.UiView.JoystickZone.SimulateTouchUp(pointerId: 1);

            Assert.That(_coordinator.ControlledPlayer.Motor.Position.z, Is.GreaterThan(startPlayerPos.z + 0.5f));
            Assert.That(_coordinator.Ball.Simulation.CurrentState, Is.EqualTo(BallState.ControlledDribble));

            // 4. Position player near North Goal (+Z) and charge + release a shot to score once
            float goalLineZ = PitchConfig.CreateRuntimeDefault().HalfLength;
            _coordinator.ControlledPlayer.ResetToSpawn(new Vector3(0f, 0f, goalLineZ - 5.5f), Vector3.forward);
            _coordinator.Ball.ResetBall(new Vector3(0f, 0.22f, goalLineZ - 5.0f));
            _coordinator.StepTrainingSimulation(1f / 30f); // Claims possession

            // Simultaneous touch test: hold joystick forward (pointer 1) + hold shoot button (pointer 2)
            _coordinator.UiView.JoystickZone.SimulateTouchDownAndDrag(pointerId: 1, new Vector2(0f, 85f));
            _coordinator.UiView.ShootButton.HandlePointerDownById(pointerId: 2);

            Assert.That(_coordinator.UiView.JoystickZone.HasActiveTouch, Is.True);
            Assert.That(_coordinator.UiView.ShootButton.IsPressed, Is.True);

            for (int i = 0; i < 12; i++)
            {
                _coordinator.StepTrainingSimulation(1f / 30f);
            }

            // Release shot (pointer 2) while joystick (pointer 1) remains owned
            _coordinator.UiView.ShootButton.HandlePointerUpById(pointerId: 2, isCancel: false);
            Assert.That(_coordinator.UiView.JoystickZone.HasActiveTouch, Is.True);

            for (int i = 0; i < 25 && _coordinator.MatchState.HomeScore == 0; i++)
            {
                _coordinator.StepTrainingSimulation(1f / 30f);
            }

            // 5. Verify goal scored once
            Assert.That(_coordinator.MatchState.HomeScore, Is.EqualTo(1));

            // 6. Reset and play again
            _coordinator.UiView.JoystickZone.SimulateTouchUp(pointerId: 1);
            _coordinator.ResetFieldPositions("TEST RESET");

            Assert.That(_coordinator.MatchState.CurrentPhase, Is.EqualTo(MatchPhase.Playing));
            Assert.That(_coordinator.Ball.Simulation.Position.z, Is.EqualTo(-4.5f).Within(0.05f));

            // Move again after reset
            _coordinator.UiView.JoystickZone.SimulateTouchDownAndDrag(pointerId: 3, new Vector2(85f, 0f));
            _coordinator.StepTrainingSimulation(0.1f);
            _coordinator.UiView.JoystickZone.SimulateTouchUp(pointerId: 3);
            Assert.That(_coordinator.ControlledPlayer.Motor.Position.x, Is.GreaterThan(0.01f));
        }

        [UnityTest]
        public IEnumerator PauseResumeAndSettingsPersistence_BehaveCorrectly()
        {
            yield return null;
            _coordinator.StartTrainingSession();
            _coordinator.StepTrainingSimulation(1.0f);
            float timeBeforePause = _coordinator.MatchState.ElapsedSeconds;

            // Pause
            _coordinator.TogglePauseMenu();
            Assert.That(_coordinator.MatchState.IsOfflineSimulationPaused, Is.True);
            Assert.That(_coordinator.UiView.IsPauseModalVisible, Is.True);

            // Stepping while paused must not advance elapsed match time
            _coordinator.StepTrainingSimulation(2.0f);
            Assert.That(_coordinator.MatchState.ElapsedSeconds, Is.EqualTo(timeBeforePause).Within(0.0001f));

            // Resume
            _coordinator.TogglePauseMenu();
            Assert.That(_coordinator.MatchState.IsOfflineSimulationPaused, Is.False);
            _coordinator.StepTrainingSimulation(0.5f);
            Assert.That(_coordinator.MatchState.ElapsedSeconds, Is.GreaterThan(timeBeforePause));

            // Verify PlayerPrefs settings round-trip
            var savedSettings = new GameSettingsDto
            {
                schemaVersion = GameSettingsDto.CurrentSchemaVersion,
                guestNamespace = GameSettingsDto.DefaultGuestNamespace,
                isAudioMuted = true,
                graphicsQualityTier = (int)GraphicsQualityTier.Low30Fps,
                cameraZoomScale = 1.15f
            };
            LocalSettingsRepository.SaveToPlayerPrefs(in savedSettings);
            GameSettingsDto reloaded = LocalSettingsRepository.LoadFromPlayerPrefs();
            Assert.That(reloaded.isAudioMuted, Is.True);
            Assert.That(reloaded.cameraZoomScale, Is.EqualTo(1.15f).Within(0.001f));
        }
    }
}
