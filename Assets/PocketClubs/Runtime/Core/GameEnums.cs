using System;

namespace PocketClubs.Core
{
    public enum TeamSide : byte
    {
        None = 0,
        Home = 1,
        Away = 2
    }

    public enum MatchPhase : byte
    {
        Loading = 0,
        Waiting = 1,
        Kickoff = 2,
        Playing = 3,
        Restart = 4,
        Finished = 5
    }

    public enum BallState : byte
    {
        Free = 0,
        ControlledDribble = 1,
        PassOrShotFlight = 2,
        RestartReset = 3
    }

    public enum RestartType : byte
    {
        None = 0,
        Kickoff = 1,
        SidelineThrowIn = 2,
        GoalKick = 3,
        CornerKick = 4,
        TrainingReset = 5
    }

    public enum TackleAttemptResult : byte
    {
        None = 0,
        SuccessSteal = 1,
        SuccessDislodge = 2,
        MissedOutOfRange = 3,
        MissedWrongFacing = 4,
        RejectedCooldown = 5,
        RejectedNoPossessionTarget = 6,
        RejectedSelfAlreadyHasBall = 7
    }

    public enum GraphicsQualityTier : byte
    {
        Low30Fps = 0,
        Balanced60Fps = 1
    }
}
