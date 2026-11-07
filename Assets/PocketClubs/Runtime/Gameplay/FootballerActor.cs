using System;
using PocketClubs.Core;
using PocketClubs.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace PocketClubs.Gameplay
{
    /// <summary>
    /// Stylized footballer actor binding hardware-independent PlayerCommandIntent to the
    /// authoritative PlayerMovementMotor, ShotChargeController, PassingService, and TackleService.
    /// Provides non-color-only team kit striping (vertical stripe vs horizontal sash),
    /// a persistent controlled-player ring + overhead chevron, and procedural running/kicking motion.
    /// </summary>
    public sealed class FootballerActor : MonoBehaviour
    {
        [SerializeField] private int playerId = 1;
        [SerializeField] private TeamSide teamSide = TeamSide.Home;
        [SerializeField] private bool isHumanControlled = true;
        [SerializeField] private string displayRoleLabel = "PLAYMAKER #10";

        private MovementTuningConfig _movementTuning;
        private ActionTuningConfig _actionTuning;
        private PitchConfig _pitchConfig;

        private PlayerMovementMotor _motor;
        private PassingService _passingService;
        private ShotChargeController _shotController;
        private TackleService _tackleService;

        private Transform _visualRoot;
        private Transform _leftLegPivot;
        private Transform _rightLegPivot;
        private Transform _controlledRingTransform;
        private Transform _overheadChevronTransform;
        private float _stridePhase;
        private float _kickAnimTimer;

        public int PlayerId => playerId;
        public TeamSide Team => teamSide;
        public bool IsHumanControlled => isHumanControlled;
        public string DisplayRoleLabel => displayRoleLabel;

        public PlayerMovementMotor Motor => _motor;
        public ShotChargeController ShotController => _shotController;
        public TackleService Tackle => _tackleService;

        public string LastActionSummary { get; private set; } = "READY";
        public int PassesCompletedCount { get; private set; }
        public int ShotsTakenCount { get; private set; }
        public int TacklesAttemptedCount { get; private set; }

        public event Action<string> OnActionFeedback;

        public void Initialize(
            int id,
            TeamSide side,
            bool humanControlled,
            Vector3 spawnPosition,
            Vector3 spawnForward,
            MovementTuningConfig moveTuning,
            ActionTuningConfig actTuning,
            PitchConfig pitchCfg)
        {
            playerId = id;
            teamSide = side;
            isHumanControlled = humanControlled;
            _movementTuning = moveTuning != null ? moveTuning : MovementTuningConfig.CreateRuntimeDefault();
            _actionTuning = actTuning != null ? actTuning : ActionTuningConfig.CreateRuntimeDefault();
            _pitchConfig = pitchCfg != null ? pitchCfg : PitchConfig.CreateRuntimeDefault();

            _motor = new PlayerMovementMotor(_movementTuning, _pitchConfig, spawnPosition, spawnForward);
            _passingService = new PassingService(_actionTuning);
            _shotController = new ShotChargeController(_actionTuning);
            _tackleService = new TackleService(_actionTuning);

            EnsureStylizedCharacterVisuals();
            SyncTransformAndProceduralMotion(0f);
        }

        public void ResetToSpawn(Vector3 spawnPosition, Vector3 spawnForward)
        {
            _motor?.Teleport(spawnPosition, spawnForward, refillStamina: true);
            _shotController?.CancelCharge();
            _tackleService?.ResetCooldown();
            _kickAnimTimer = 0f;
            LastActionSummary = "READY";
            SyncTransformAndProceduralMotion(0f);
        }

        public void CancelAllActiveActions()
        {
            _motor?.CancelActiveInput();
            if (_shotController != null && _shotController.IsCharging)
            {
                _shotController.CancelCharge();
                EmitFeedback("SHOT CANCELLED");
            }
        }

        public void TickIntentAndActions(
            in PlayerCommandIntent intent,
            BallSimulation ballSim,
            float deltaTime,
            ReadOnlySpan<TeammatePassTarget> teammates = default,
            ReadOnlySpan<ObstacleCircleXZ> obstacles = default)
        {
            if (_motor == null || ballSim == null || deltaTime <= 0f)
            {
                return;
            }

            _tackleService.TickCooldown(deltaTime);

            bool hasBall = ballSim.CurrentState == BallState.ControlledDribble &&
                           ballSim.OwnerPlayerId == playerId;

            if (intent.shootCancelled)
            {
                CancelAllActiveActions();
                return;
            }

            _motor.Step(in intent, deltaTime, hasBall, obstacles);

            // Attempt automatic ball pickup when within bounded pickup radius
            if (!hasBall && (ballSim.CurrentState == BallState.Free || ballSim.CurrentState == BallState.PassOrShotFlight))
            {
                if (ballSim.TryClaimPossession(playerId, teamSide, _motor.Position, _motor.Forward, obstacles))
                {
                    hasBall = true;
                    EmitFeedback("POSSESSION CLAIMED");
                }
            }

            if (hasBall)
            {
                ballSim.UpdateControlledDribble(_motor.Position, _motor.Forward, _motor.Velocity, deltaTime, obstacles);
                hasBall = ballSim.CurrentState == BallState.ControlledDribble && ballSim.OwnerPlayerId == playerId;
            }
            else if (_shotController.IsCharging)
            {
                // Safely cancel shot charge if player lost possession before release
                _shotController.CancelCharge();
            }

            // Process Pass
            if (intent.passPressed && hasBall)
            {
                _shotController.CancelCharge();
                PassExecutionResult passResult = _passingService.ComputePass(
                    _motor.Position,
                    _motor.Forward,
                    intent.worldMoveDirection,
                    requestedPowerNormalized: _motor.IsSprinting ? 0.72f : 0.52f,
                    teammates);

                if (ballSim.ReleaseWithImpulse(playerId, teamSide, passResult.launchVelocity))
                {
                    PassesCompletedCount++;
                    _kickAnimTimer = 0.22f;
                    EmitFeedback($"PASS ({passResult.speed:F1} m/s)");
                    hasBall = false;
                }
            }

            // Process Hold/Release Shot
            if (hasBall)
            {
                if (intent.shootHeld)
                {
                    _shotController.BeginOrContinueCharge(deltaTime);
                }
                else if (intent.shootReleased && _shotController.IsCharging)
                {
                    Vector3 targetGoalCenter = new Vector3(
                        0f,
                        _pitchConfig.goalHeight * 0.45f,
                        teamSide == TeamSide.Home ? _pitchConfig.HalfLength : -_pitchConfig.HalfLength);

                    ShotExecutionResult shotResult = _shotController.ReleaseShot(
                        _motor.Position,
                        _motor.Forward,
                        intent.worldMoveDirection,
                        targetGoalCenter);

                    if (shotResult.executed && ballSim.ReleaseWithImpulse(playerId, teamSide, shotResult.launchVelocity))
                    {
                        ShotsTakenCount++;
                        _kickAnimTimer = 0.28f;
                        int pct = Mathf.RoundToInt(shotResult.normalizedCharge * 100f);
                        EmitFeedback($"SHOT ({pct}% | {shotResult.speed:F1} m/s)");
                    }
                }
            }

            // Process Standing Tackle
            if (intent.tacklePressed && !hasBall)
            {
                TacklesAttemptedCount++;
                _kickAnimTimer = 0.20f;
                TackleAttemptResult tackleResult = _tackleService.AttemptStandingTackle(
                    playerId,
                    teamSide,
                    _motor.Position,
                    _motor.Forward,
                    ballSim);

                EmitFeedback($"TACKLE: {tackleResult}");
            }

            SyncTransformAndProceduralMotion(deltaTime);
        }

        private void EmitFeedback(string message)
        {
            LastActionSummary = message;
            OnActionFeedback?.Invoke(message);
        }

        private void SyncTransformAndProceduralMotion(float deltaTime)
        {
            if (_motor == null)
            {
                return;
            }

            transform.position = _motor.Position;
            if (_motor.Forward.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(_motor.Forward, Vector3.up);
            }

            float planarSpeed = _motor.CurrentPlanarSpeed;
            if (planarSpeed > 0.15f && deltaTime > 0f)
            {
                _stridePhase += planarSpeed * 3.4f * deltaTime;
            }
            else
            {
                _stridePhase = 0f;
            }

            if (_kickAnimTimer > 0f && deltaTime > 0f)
            {
                _kickAnimTimer = Mathf.Max(0f, _kickAnimTimer - deltaTime);
            }

            if (_visualRoot != null)
            {
                float bobY = planarSpeed > 0.15f ? Mathf.Abs(Mathf.Sin(_stridePhase)) * 0.06f : 0f;
                float sprintLean = _motor.IsSprinting ? 10f : (planarSpeed > 0.2f ? 4f : 0f);
                _visualRoot.localPosition = new Vector3(0f, bobY, 0f);
                _visualRoot.localRotation = Quaternion.Euler(sprintLean, 0f, 0f);
            }

            if (_leftLegPivot != null && _rightLegPivot != null)
            {
                float legSwing = planarSpeed > 0.15f ? Mathf.Sin(_stridePhase) * 32f : 0f;
                float rightKickAngle = _kickAnimTimer > 0f ? -48f * (_kickAnimTimer / 0.28f) : -legSwing;
                _leftLegPivot.localRotation = Quaternion.Euler(legSwing, 0f, 0f);
                _rightLegPivot.localRotation = Quaternion.Euler(rightKickAngle, 0f, 0f);
            }

            if (_controlledRingTransform != null)
            {
                _controlledRingTransform.gameObject.SetActive(isHumanControlled);
            }

            if (_overheadChevronTransform != null)
            {
                _overheadChevronTransform.gameObject.SetActive(isHumanControlled);
                float hoverY = 2.15f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.06f;
                _overheadChevronTransform.localPosition = new Vector3(0f, hoverY, 0f);
            }
        }

        private void EnsureStylizedCharacterVisuals()
        {
            if (_visualRoot != null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // Distinct kit colors + geometric striping so team identity never relies only on color:
            // Home = Azure Blue with Vertical Center Stripe; Away = Coral Gold with Horizontal Chest Band
            Color primaryKit = teamSide == TeamSide.Home
                ? new Color(0.10f, 0.45f, 0.88f)
                : new Color(0.92f, 0.36f, 0.16f);
            Color patternAccent = teamSide == TeamSide.Home
                ? new Color(0.96f, 0.98f, 1.00f)
                : new Color(0.14f, 0.16f, 0.22f);
            Color shortsColor = new Color(0.12f, 0.15f, 0.22f);
            Color skinTone = new Color(0.84f, 0.65f, 0.50f);
            Color indicatorCyan = new Color(0.15f, 0.95f, 0.85f);

            Material kitMat = CreateMat(shader, "Mat_KitPrimary", primaryKit);
            Material accentMat = CreateMat(shader, "Mat_KitPattern", patternAccent);
            Material shortsMat = CreateMat(shader, "Mat_Shorts", shortsColor);
            Material skinMat = CreateMat(shader, "Mat_Skin", skinTone);
            Material ringMat = CreateMat(shader, "Mat_ControlledIndicator", indicatorCyan);

            _visualRoot = new GameObject("StylizedFootballerVisual").transform;
            _visualRoot.SetParent(transform, false);

            // Torso
            CreatePart(_visualRoot, PrimitiveType.Capsule, "Torso", new Vector3(0f, 1.05f, 0f), new Vector3(0.52f, 0.42f, 0.34f), kitMat, true);

            // Non-color-only geometric kit identifier (Home = vertical stripe, Away = horizontal chest band)
            if (teamSide == TeamSide.Home)
            {
                CreatePart(_visualRoot, PrimitiveType.Cube, "HomeVerticalStripe", new Vector3(0f, 1.08f, 0.16f), new Vector3(0.14f, 0.52f, 0.06f), accentMat, false);
            }
            else
            {
                CreatePart(_visualRoot, PrimitiveType.Cube, "AwayHorizontalBand", new Vector3(0f, 1.12f, 0.16f), new Vector3(0.48f, 0.14f, 0.06f), accentMat, false);
            }

            // Head + forward visor band so facing direction is always clear
            CreatePart(_visualRoot, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.62f, 0f), new Vector3(0.36f, 0.38f, 0.36f), skinMat, true);
            CreatePart(_visualRoot, PrimitiveType.Cube, "FacingHeadband", new Vector3(0f, 1.68f, 0.14f), new Vector3(0.32f, 0.07f, 0.14f), accentMat, false);

            // Legs with hip pivots for procedural stride & kick swing
            _leftLegPivot = new GameObject("LeftLegPivot").transform;
            _leftLegPivot.SetParent(_visualRoot, false);
            _leftLegPivot.localPosition = new Vector3(-0.14f, 0.68f, 0f);
            CreatePart(_leftLegPivot, PrimitiveType.Cylinder, "LeftLeg", new Vector3(0f, -0.32f, 0f), new Vector3(0.16f, 0.32f, 0.16f), shortsMat, true);

            _rightLegPivot = new GameObject("RightLegPivot").transform;
            _rightLegPivot.SetParent(_visualRoot, false);
            _rightLegPivot.localPosition = new Vector3(0.14f, 0.68f, 0f);
            CreatePart(_rightLegPivot, PrimitiveType.Cylinder, "RightLeg", new Vector3(0f, -0.32f, 0f), new Vector3(0.16f, 0.32f, 0.16f), shortsMat, true);

            // Controlled-player ground ring & directional nose + overhead chevron
            GameObject ringRoot = new GameObject("ControlledPlayerGroundRing");
            ringRoot.transform.SetParent(transform, false);
            ringRoot.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            _controlledRingTransform = ringRoot.transform;

            CreatePart(_controlledRingTransform, PrimitiveType.Cylinder, "RingDisc", Vector3.zero, new Vector3(1.15f, 0.01f, 1.15f), ringMat, false);
            CreatePart(_controlledRingTransform, PrimitiveType.Cube, "ForwardArrowTip", new Vector3(0f, 0.015f, 0.68f), new Vector3(0.24f, 0.02f, 0.28f), ringMat, false);

            GameObject chevron = CreatePart(transform, PrimitiveType.Cube, "OverheadControlledChevron", new Vector3(0f, 2.15f, 0f), new Vector3(0.26f, 0.26f, 0.26f), ringMat, false);
            chevron.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            _overheadChevronTransform = chevron.transform;
        }

        private static Material CreateMat(Shader shader, string matName, Color color)
        {
            var m = new Material(shader) { name = matName, color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            return m;
        }

        private static GameObject CreatePart(Transform parent, PrimitiveType type, string partName, Vector3 localPos, Vector3 localScale, Material mat, bool castShadow)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = partName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }

            return go;
        }
    }
}
