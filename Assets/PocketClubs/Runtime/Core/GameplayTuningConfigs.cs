using UnityEngine;

namespace PocketClubs.Core
{
    [CreateAssetMenu(fileName = "MovementTuningConfig", menuName = "PocketClubs/Tuning/Movement Tuning")]
    public sealed class MovementTuningConfig : ScriptableObject
    {
        [Header("Planar Speed & Acceleration")]
        [Min(1f)] public float baseMaxSpeed = 6.8f;
        [Range(1.05f, 2.0f)] public float sprintSpeedMultiplier = 1.32f;
        [Range(0.5f, 1.0f)] public float dribbleSpeedMultiplier = 0.92f;
        [Min(1f)] public float acceleration = 24.0f;
        [Min(1f)] public float deceleration = 28.0f;
        [Min(90f)] public float turnSpeedDegreesPerSecond = 540.0f;
        [Min(0.1f)] public float playerRadius = 0.45f;

        [Header("Stamina & Sprint")]
        [Min(10f)] public float maxStamina = 100.0f;
        [Min(1f)] public float sprintStaminaDrainPerSecond = 22.0f;
        [Min(1f)] public float staminaRegenPerSecond = 14.0f;
        [Min(0f)] public float staminaRegenDelaySeconds = 1.1f;
        [Min(1f)] public float minStaminaToStartSprint = 15.0f;

        public static MovementTuningConfig CreateRuntimeDefault()
        {
            var config = CreateInstance<MovementTuningConfig>();
            config.name = "RuntimeDefaultMovementTuning";
            return config;
        }
    }

    [CreateAssetMenu(fileName = "BallTuningConfig", menuName = "PocketClubs/Tuning/Ball Tuning")]
    public sealed class BallTuningConfig : ScriptableObject
    {
        [Header("Geometry & Possession")]
        [Min(0.1f)] public float ballRadius = 0.22f;
        [Min(0.25f)] public float pickupRadius = 0.85f;
        [Min(0.2f)] public float dribbleLeadDistance = 0.68f;
        [Min(1f)] public float dribbleFollowSharpness = 18.0f;
        [Min(0.1f)] public float maxPickupBallHeight = 1.15f;
        [Min(0.05f)] public float pickupCooldownAfterReleaseSeconds = 0.25f;

        [Header("Physics & Sub-Stepping")]
        [Min(0.1f)] public float groundFriction = 3.2f;
        [Min(0.0f)] public float airDrag = 0.45f;
        [Min(1f)] public float gravity = 9.81f;
        [Range(0.1f, 0.95f)] public float restitution = 0.58f;
        [Min(5f)] public float maxBallSpeed = 28.0f;

        public static BallTuningConfig CreateRuntimeDefault()
        {
            var config = CreateInstance<BallTuningConfig>();
            config.name = "RuntimeDefaultBallTuning";
            return config;
        }
    }

    [CreateAssetMenu(fileName = "ActionTuningConfig", menuName = "PocketClubs/Tuning/Action Tuning")]
    public sealed class ActionTuningConfig : ScriptableObject
    {
        [Header("Passing")]
        [Min(2f)] public float minPassSpeed = 9.0f;
        [Min(4f)] public float maxPassSpeed = 17.5f;
        [Range(5f, 90f)] public float passAssistMaxAngleDegrees = 38.0f;
        [Min(3f)] public float passAssistMaxDistance = 24.0f;
        [Range(0f, 1f)] public float passDirectionAssistWeight = 0.65f;

        [Header("Shooting")]
        [Min(4f)] public float minShotSpeed = 11.0f;
        [Min(8f)] public float maxShotSpeed = 25.5f;
        [Min(0.1f)] public float maxShotChargeSeconds = 0.85f;
        [Range(0f, 0.45f)] public float shotLoftRatioAtFullCharge = 0.18f;
        [Range(0f, 45f)] public float shotGoalAssistAngleDegrees = 18.0f;
        [Range(0f, 1f)] public float shotGoalAssistWeight = 0.35f;

        [Header("Standing Tackle")]
        [Min(0.4f)] public float tackleReachDistance = 1.45f;
        [Range(15f, 120f)] public float tackleMaxAngleDegrees = 65.0f;
        [Min(0.1f)] public float tackleCooldownSeconds = 0.95f;
        [Min(1f)] public float tackleDislodgeImpulse = 5.5f;

        public static ActionTuningConfig CreateRuntimeDefault()
        {
            var config = CreateInstance<ActionTuningConfig>();
            config.name = "RuntimeDefaultActionTuning";
            return config;
        }
    }

    [CreateAssetMenu(fileName = "PitchConfig", menuName = "PocketClubs/Tuning/Pitch Config")]
    public sealed class PitchConfig : ScriptableObject
    {
        [Header("Small-Sided Pitch Dimensions (Meters)")]
        [Min(20f)] public float pitchLength = 56.0f;
        [Min(16f)] public float pitchWidth = 36.0f;
        [Min(2f)] public float goalWidth = 5.0f;
        [Min(1.5f)] public float goalHeight = 2.2f;
        [Min(0.8f)] public float goalDepth = 1.6f;
        [Min(1.0f)] public float boundaryRunoff = 3.0f;

        public float HalfLength => pitchLength * 0.5f;
        public float HalfWidth => pitchWidth * 0.5f;
        public float HalfGoalWidth => goalWidth * 0.5f;

        public static PitchConfig CreateRuntimeDefault()
        {
            var config = CreateInstance<PitchConfig>();
            config.name = "RuntimeDefaultPitchConfig";
            return config;
        }
    }

    [CreateAssetMenu(fileName = "CameraTuningConfig", menuName = "PocketClubs/Tuning/Camera Tuning")]
    public sealed class CameraTuningConfig : ScriptableObject
    {
        [Header("Elevated Broadcast Framing")]
        [Min(5f)] public float baseHeight = 14.5f;
        [Min(5f)] public float baseDistance = 13.5f;
        [Range(25f, 70f)] public float pitchAngleDegrees = 46.0f;
        [Min(0.01f)] public float followSmoothTime = 0.14f;
        [Min(0f)] public float passLookAheadDistance = 3.2f;
        [Range(0f, 0.5f)] public float ballInfluenceWeight = 0.22f;
        [Min(0.5f)] public float maxBallInfluenceOffset = 4.5f;

        [Header("Zoom & Shake")]
        [Range(0.7f, 1.0f)] public float minZoomScale = 0.85f;
        [Range(1.0f, 1.4f)] public float maxZoomScale = 1.20f;
        [Range(0f, 1.5f)] public float goalShakeIntensity = 0.35f;
        [Range(0f, 1.0f)] public float goalShakeDuration = 0.28f;

        public static CameraTuningConfig CreateRuntimeDefault()
        {
            var config = CreateInstance<CameraTuningConfig>();
            config.name = "RuntimeDefaultCameraTuning";
            return config;
        }
    }
}
