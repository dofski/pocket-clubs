using NUnit.Framework;
using PocketClubs.Core;
using PocketClubs.Gameplay;
using PocketClubs.Persistence;
using UnityEngine;

namespace PocketClubs.Tests.EditMode
{
    [TestFixture]
    public sealed class MatchAndSettingsTests
    {
        [Test]
        public void MatchStateController_TransitionsCleanly_AndEmitsSingleFinalResultOnTimerCompletion()
        {
            var match = new MatchStateController(isUnlimitedTraining: false, regulationDurationSeconds: 10f, matchId: "test-match-01");
            int resultEmissionCount = 0;
            MatchResultDto capturedResult = default;

            match.OnMatchFinished += dto =>
            {
                resultEmissionCount++;
                capturedResult = dto;
            };

            Assert.That(match.CurrentPhase, Is.EqualTo(MatchPhase.Loading));

            match.BeginWaitingForPlayers();
            Assert.That(match.CurrentPhase, Is.EqualTo(MatchPhase.Waiting));

            match.BeginKickoff(TeamSide.Home);
            Assert.That(match.CurrentPhase, Is.EqualTo(MatchPhase.Playing));

            // Score one goal for Home
            bool recorded = match.RecordGoal(TeamSide.Home);
            Assert.That(recorded, Is.True);
            Assert.That(match.HomeScore, Is.EqualTo(1));
            Assert.That(match.CurrentPhase, Is.EqualTo(MatchPhase.Restart));

            match.ResumePlayingFromRestart();
            Assert.That(match.CurrentPhase, Is.EqualTo(MatchPhase.Playing));

            // Advance timer to regulation completion
            match.Tick(6f);
            Assert.That(match.CurrentPhase, Is.EqualTo(MatchPhase.Playing));
            match.Tick(5f);

            Assert.That(match.CurrentPhase, Is.EqualTo(MatchPhase.Finished));
            Assert.That(match.HasEmittedFinalResult, Is.True);
            Assert.That(resultEmissionCount, Is.EqualTo(1));
            Assert.That(capturedResult.homeScore, Is.EqualTo(1));
            Assert.That(capturedResult.awayScore, Is.EqualTo(0));
            Assert.That(capturedResult.winningSide, Is.EqualTo(TeamSide.Home));

            // Calling CompleteMatch or Tick again must NOT emit a second result
            bool secondEmit = match.CompleteMatch(out _);
            match.Tick(5f);
            Assert.That(secondEmit, Is.False);
            Assert.That(resultEmissionCount, Is.EqualTo(1));
        }

        [Test]
        public void PauseMenu_FreezesOfflineSimulation_ButDoesNotPauseOnlineMatchTimer()
        {
            var match = new MatchStateController(isUnlimitedTraining: false, regulationDurationSeconds: 60f);
            match.BeginKickoff();

            // Offline pause freezes timer
            match.SetPauseMenuOpen(isOpen: true, isOnlineMatch: false);
            match.Tick(5f);
            Assert.That(match.ElapsedSeconds, Is.EqualTo(0f).Within(0.0001f));

            // Online pause menu opens local menu but does NOT freeze shared timer
            match.SetPauseMenuOpen(isOpen: true, isOnlineMatch: true);
            Assert.That(match.IsLocalMenuOpen, Is.True);
            Assert.That(match.IsOfflineSimulationPaused, Is.False);
            match.Tick(5f);
            Assert.That(match.ElapsedSeconds, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void OutOfPlayDetector_ClassifiesThrowInCornerAndGoalKickCorrectly()
        {
            var pitch = PitchConfig.CreateRuntimeDefault();
            var detector = new GoalAndBoundaryDetector(pitch);
            float r = 0.22f;

            // 1. Touchline out of play last touched by Home -> Throw-In awarded to Away
            OutOfPlayRestartEvent throwIn = detector.EvaluateOutOfPlay(
                new Vector3(pitch.HalfWidth + r + 0.1f, r, 4f),
                r,
                TeamSide.Home,
                BallState.Free);
            Assert.That(throwIn.isOutOfPlay, Is.True);
            Assert.That(throwIn.restartType, Is.EqualTo(RestartType.SidelineThrowIn));
            Assert.That(throwIn.awardedTeam, Is.EqualTo(TeamSide.Away));

            // 2. North goal line wide of posts, last touched by defender (Away) -> Corner Kick for Home
            OutOfPlayRestartEvent corner = detector.EvaluateOutOfPlay(
                new Vector3(8f, r, pitch.HalfLength + r + 0.15f),
                r,
                TeamSide.Away,
                BallState.Free);
            Assert.That(corner.isOutOfPlay, Is.True);
            Assert.That(corner.restartType, Is.EqualTo(RestartType.CornerKick));
            Assert.That(corner.awardedTeam, Is.EqualTo(TeamSide.Home));

            // 3. North goal line wide of posts, last touched by attacker (Home) -> Goal Kick for Away
            OutOfPlayRestartEvent goalKick = detector.EvaluateOutOfPlay(
                new Vector3(8f, r, pitch.HalfLength + r + 0.15f),
                r,
                TeamSide.Home,
                BallState.Free);
            Assert.That(goalKick.isOutOfPlay, Is.True);
            Assert.That(goalKick.restartType, Is.EqualTo(RestartType.GoalKick));
            Assert.That(goalKick.awardedTeam, Is.EqualTo(TeamSide.Away));
        }

        [Test]
        public void LocalSettingsRepository_RoundTripsValidData_AndRecoversFromCorruptedJson()
        {
            var custom = new GameSettingsDto
            {
                schemaVersion = GameSettingsDto.CurrentSchemaVersion,
                guestNamespace = "local_guest",
                isAudioMuted = true,
                graphicsQualityTier = (int)GraphicsQualityTier.Low30Fps,
                cameraZoomScale = 1.12f
            };

            string json = LocalSettingsRepository.SerializeToJson(in custom);
            GameSettingsDto loaded = LocalSettingsRepository.DeserializeFromJsonOrRecover(json, out bool recovered);

            Assert.That(recovered, Is.False);
            Assert.That(loaded.isAudioMuted, Is.True);
            Assert.That(loaded.graphicsQualityTier, Is.EqualTo((int)GraphicsQualityTier.Low30Fps));
            Assert.That(loaded.cameraZoomScale, Is.EqualTo(1.12f).Within(0.001f));

            // Corrupted JSON recovers cleanly to safe defaults
            GameSettingsDto fromCorrupt = LocalSettingsRepository.DeserializeFromJsonOrRecover("{broken_json", out bool corruptRecovered);
            Assert.That(corruptRecovered, Is.True);
            Assert.That(fromCorrupt.schemaVersion, Is.EqualTo(GameSettingsDto.CurrentSchemaVersion));
            Assert.That(fromCorrupt.guestNamespace, Is.EqualTo(GameSettingsDto.DefaultGuestNamespace));

            // Outdated schema version recovers cleanly to safe defaults
            string wrongVersionJson = "{\"schemaVersion\":999,\"isAudioMuted\":true,\"cameraZoomScale\":99.0}";
            GameSettingsDto fromWrongSchema = LocalSettingsRepository.DeserializeFromJsonOrRecover(wrongVersionJson, out bool schemaRecovered);
            Assert.That(schemaRecovered, Is.True);
            Assert.That(fromWrongSchema.cameraZoomScale, Is.EqualTo(1.0f).Within(0.001f));
        }
    }
}
