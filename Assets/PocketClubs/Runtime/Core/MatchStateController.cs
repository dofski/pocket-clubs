using System;
using UnityEngine;

namespace PocketClubs.Core
{
    [Serializable]
    public struct MatchResultDto
    {
        public string matchId;
        public int homeScore;
        public int awayScore;
        public float elapsedDurationSeconds;
        public TeamSide winningSide;
        public bool isDraw;
        public bool isTrainingSession;
    }

    [Serializable]
    public struct PendingRestartInfo
    {
        public RestartType restartType;
        public TeamSide awardedTeam;
        public Vector3 restartWorldPosition;
    }

    /// <summary>
    /// Explicit authoritative state machine governing match lifecycle, timer, pause semantics,
    /// restarts, and single-emission final results.
    /// </summary>
    public sealed class MatchStateController
    {
        public MatchPhase CurrentPhase { get; private set; } = MatchPhase.Loading;
        public bool IsUnlimitedTraining { get; }
        public float RegulationDurationSeconds { get; }
        public float ElapsedSeconds { get; private set; }
        public float RemainingSeconds => IsUnlimitedTraining
            ? 0f
            : Mathf.Max(0f, RegulationDurationSeconds - ElapsedSeconds);

        public int HomeScore { get; private set; }
        public int AwayScore { get; private set; }
        public bool IsOfflineSimulationPaused { get; private set; }
        public bool IsLocalMenuOpen { get; private set; }
        public bool HasEmittedFinalResult { get; private set; }
        public PendingRestartInfo LastRestart { get; private set; }

        public event Action<MatchPhase, MatchPhase> OnPhaseChanged;
        public event Action<int, int, TeamSide> OnScoreChanged;
        public event Action<MatchResultDto> OnMatchFinished;

        private readonly string _matchId;

        public MatchStateController(bool isUnlimitedTraining = true, float regulationDurationSeconds = 300f, string matchId = null)
        {
            IsUnlimitedTraining = isUnlimitedTraining;
            RegulationDurationSeconds = Mathf.Max(1f, regulationDurationSeconds);
            _matchId = string.IsNullOrWhiteSpace(matchId) ? Guid.NewGuid().ToString("N") : matchId;
        }

        public bool TransitionTo(MatchPhase nextPhase)
        {
            if (CurrentPhase == MatchPhase.Finished && nextPhase != MatchPhase.Loading && nextPhase != MatchPhase.Kickoff)
            {
                return false;
            }

            if (CurrentPhase == nextPhase)
            {
                return true;
            }

            MatchPhase previous = CurrentPhase;
            CurrentPhase = nextPhase;
            OnPhaseChanged?.Invoke(previous, nextPhase);
            return true;
        }

        public void BeginWaitingForPlayers()
        {
            TransitionTo(MatchPhase.Waiting);
        }

        public void BeginKickoff(TeamSide kickoffTeam = TeamSide.Home)
        {
            LastRestart = new PendingRestartInfo
            {
                restartType = RestartType.Kickoff,
                awardedTeam = kickoffTeam,
                restartWorldPosition = Vector3.zero
            };
            TransitionTo(MatchPhase.Kickoff);
            TransitionTo(MatchPhase.Playing);
        }

        /// <summary>
        /// Offline pause halts simulation progression.
        /// Online pause menu opens local UI without halting shared match simulation.
        /// </summary>
        public void SetPauseMenuOpen(bool isOpen, bool isOnlineMatch = false)
        {
            IsLocalMenuOpen = isOpen;
            IsOfflineSimulationPaused = !isOnlineMatch && isOpen;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || IsOfflineSimulationPaused || CurrentPhase != MatchPhase.Playing)
            {
                return;
            }

            ElapsedSeconds += deltaTime;

            if (!IsUnlimitedTraining && ElapsedSeconds >= RegulationDurationSeconds)
            {
                ElapsedSeconds = RegulationDurationSeconds;
                CompleteMatch();
            }
        }

        public bool RecordGoal(TeamSide scoringTeam)
        {
            if (CurrentPhase != MatchPhase.Playing || IsOfflineSimulationPaused || scoringTeam == TeamSide.None)
            {
                return false;
            }

            if (scoringTeam == TeamSide.Home)
            {
                HomeScore++;
            }
            else if (scoringTeam == TeamSide.Away)
            {
                AwayScore++;
            }

            OnScoreChanged?.Invoke(HomeScore, AwayScore, scoringTeam);

            TeamSide concedingTeam = scoringTeam == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
            LastRestart = new PendingRestartInfo
            {
                restartType = RestartType.Kickoff,
                awardedTeam = concedingTeam,
                restartWorldPosition = Vector3.zero
            };

            TransitionTo(MatchPhase.Restart);
            return true;
        }

        public bool ScheduleRestart(RestartType restartType, TeamSide awardedTeam, Vector3 restartPosition)
        {
            if (CurrentPhase == MatchPhase.Finished)
            {
                return false;
            }

            LastRestart = new PendingRestartInfo
            {
                restartType = restartType,
                awardedTeam = awardedTeam,
                restartWorldPosition = restartPosition
            };

            TransitionTo(MatchPhase.Restart);
            return true;
        }

        public void ResumePlayingFromRestart()
        {
            if (CurrentPhase == MatchPhase.Restart || CurrentPhase == MatchPhase.Kickoff || CurrentPhase == MatchPhase.Waiting)
            {
                TransitionTo(MatchPhase.Playing);
            }
        }

        public void ResetScoresAndTimerForTraining()
        {
            HomeScore = 0;
            AwayScore = 0;
            ElapsedSeconds = 0f;
            HasEmittedFinalResult = false;
            IsOfflineSimulationPaused = false;
            IsLocalMenuOpen = false;
            LastRestart = new PendingRestartInfo
            {
                restartType = RestartType.TrainingReset,
                awardedTeam = TeamSide.Home,
                restartWorldPosition = Vector3.zero
            };
            TransitionTo(MatchPhase.Playing);
            OnScoreChanged?.Invoke(HomeScore, AwayScore, TeamSide.None);
        }

        public bool CompleteMatch(out MatchResultDto emittedResult)
        {
            if (HasEmittedFinalResult)
            {
                emittedResult = default;
                return false;
            }

            HasEmittedFinalResult = true;
            TransitionTo(MatchPhase.Finished);

            TeamSide winner = TeamSide.None;
            if (HomeScore > AwayScore)
            {
                winner = TeamSide.Home;
            }
            else if (AwayScore > HomeScore)
            {
                winner = TeamSide.Away;
            }

            emittedResult = new MatchResultDto
            {
                matchId = _matchId,
                homeScore = HomeScore,
                awayScore = AwayScore,
                elapsedDurationSeconds = ElapsedSeconds,
                winningSide = winner,
                isDraw = HomeScore == AwayScore,
                isTrainingSession = IsUnlimitedTraining
            };

            OnMatchFinished?.Invoke(emittedResult);
            return true;
        }

        private void CompleteMatch()
        {
            CompleteMatch(out _);
        }
    }
}
