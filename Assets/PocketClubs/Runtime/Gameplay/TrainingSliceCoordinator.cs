using PocketClubs.CameraSystem;
using PocketClubs.Core;
using PocketClubs.Input;
using PocketClubs.Persistence;
using PocketClubs.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PocketClubs.Gameplay
{
    /// <summary>
    /// Coordinates the Gate 1 Offline Training Slice lifecycle:
    /// Launch Screen -> Play Training -> Move / Sprint / Pass / Shoot / Tackle ->
    /// Goal Detection -> Score & Reset -> Pause / Resume -> Settings.
    /// </summary>
    public sealed class TrainingSliceCoordinator : MonoBehaviour
    {
        [Header("Tuning Configs (Optional - Auto-Filled with Runtime Defaults if Unassigned)")]
        [SerializeField] private MovementTuningConfig movementTuning;
        [SerializeField] private BallTuningConfig ballTuning;
        [SerializeField] private ActionTuningConfig actionTuning;
        [SerializeField] private PitchConfig pitchConfig;
        [SerializeField] private CameraTuningConfig cameraTuning;

        private StylizedPitchBuilder _pitchBuilder;
        private FootballerActor _controlledPlayer;
        private BallActor _ballActor;
        private BroadcastMatchCamera _broadcastCamera;
        private TouchAndHardwareIntentSource _intentSource;
        private TrainingHudAndMenusView _uiView;

        private MatchStateController _matchState;
        private GoalAndBoundaryDetector _goalDetector;
        private GameSettingsDto _currentSettings;

        private string _statusBannerMessage = "READY";
        private float _postGoalResetCountdown = -1f;
        private bool _isInitialized;

        public static bool SuppressAutoBootstrapForTests { get; set; }

        public MatchStateController MatchState => _matchState;
        public FootballerActor ControlledPlayer => _controlledPlayer;
        public BallActor Ball => _ballActor;
        public BroadcastMatchCamera MatchCamera => _broadcastCamera;
        public TouchAndHardwareIntentSource IntentSource => _intentSource;
        public TrainingHudAndMenusView UiView => _uiView;
        public GoalAndBoundaryDetector GoalDetector => _goalDetector;
        public string StatusBannerMessage => _statusBannerMessage;

        private static readonly Vector3 DefaultPlayerSpawnPos = new Vector3(0f, 0f, -6.5f);
        private static readonly Vector3 DefaultPlayerSpawnForward = Vector3.forward;
        private static readonly Vector3 DefaultBallSpawnPos = new Vector3(0f, 0.22f, -4.5f);

        private void Awake()
        {
            InitializeIfNeeded();
        }

        public void InitializeIfNeeded()
        {
            if (_isInitialized)
            {
                return;
            }
            _isInitialized = true;

            if (movementTuning == null) movementTuning = MovementTuningConfig.CreateRuntimeDefault();
            if (ballTuning == null) ballTuning = BallTuningConfig.CreateRuntimeDefault();
            if (actionTuning == null) actionTuning = ActionTuningConfig.CreateRuntimeDefault();
            if (pitchConfig == null) pitchConfig = PitchConfig.CreateRuntimeDefault();
            if (cameraTuning == null) cameraTuning = CameraTuningConfig.CreateRuntimeDefault();

            _currentSettings = LocalSettingsRepository.LoadFromPlayerPrefs();
            LocalSettingsRepository.ApplyRuntimeSettings(in _currentSettings);

            _matchState = new MatchStateController(isUnlimitedTraining: true, regulationDurationSeconds: 300f);
            _goalDetector = new GoalAndBoundaryDetector(pitchConfig);

            EnsureDirectionalLight();
            EnsurePitch();
            EnsurePlayerAndBall();
            EnsureCamera();
            EnsureInputAndUi();

            _matchState.BeginWaitingForPlayers();
            _uiView.ShowLaunchScreen();
        }

        public void StartTrainingSession()
        {
            InitializeIfNeeded();
            _postGoalResetCountdown = -1f;
            _matchState.ResetScoresAndTimerForTraining();
            ResetFieldPositions("TRAINING STARTED — SCORE IN THE NORTH GOAL (+Z)");
            _uiView.ShowTrainingHud();
        }

        public void ResetFieldPositions(string bannerReason = "POSITIONS RESET")
        {
            InitializeIfNeeded();
            _postGoalResetCountdown = -1f;
            _intentSource.CancelAllActiveInputs();
            _goalDetector.NotifyResetOrTeleport();

            _controlledPlayer.ResetToSpawn(DefaultPlayerSpawnPos, DefaultPlayerSpawnForward);
            _ballActor.ResetBall(DefaultBallSpawnPos);
            _broadcastCamera.SnapImmediately(DefaultPlayerSpawnForward);
            _matchState.ResumePlayingFromRestart();

            _statusBannerMessage = bannerReason;
            RefreshHudNow();
        }

        public void TogglePauseMenu()
        {
            InitializeIfNeeded();
            if (_uiView.IsLaunchScreenVisible)
            {
                return;
            }

            bool nextPaused = !_matchState.IsOfflineSimulationPaused;
            _matchState.SetPauseMenuOpen(nextPaused, isOnlineMatch: false);
            if (nextPaused)
            {
                _intentSource.CancelAllActiveInputs();
                _controlledPlayer.CancelAllActiveActions();
            }

            _uiView.SetPauseModalOpen(nextPaused);
        }

        public void ReturnToLaunchScreen()
        {
            InitializeIfNeeded();
            _postGoalResetCountdown = -1f;
            _intentSource.CancelAllActiveInputs();
            _controlledPlayer.CancelAllActiveActions();
            _matchState.SetPauseMenuOpen(false, isOnlineMatch: false);
            _matchState.BeginWaitingForPlayers();
            _uiView.ShowLaunchScreen();
        }

        private void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            PollHardwareNavigationShortcuts();
            StepTrainingSimulation(Time.deltaTime);
        }

        public void StepTrainingSimulation(float deltaTime)
        {
            if (!_isInitialized || deltaTime <= 0f)
            {
                return;
            }

            if (_uiView.IsLaunchScreenVisible || _matchState.IsOfflineSimulationPaused)
            {
                return;
            }

            _matchState.Tick(deltaTime);

            if (_postGoalResetCountdown > 0f)
            {
                _postGoalResetCountdown -= deltaTime;
                _ballActor.TickPhysics(deltaTime);
                _broadcastCamera.TickCamera(_controlledPlayer.Motor.Forward, deltaTime);

                if (_postGoalResetCountdown <= 0f)
                {
                    ResetFieldPositions("KICKOFF RESET — PLAY AGAIN!");
                }
                else
                {
                    RefreshHudNow();
                }
                return;
            }

            PlayerCommandIntent intent = _intentSource.SampleIntent(_broadcastCamera.CameraYawDegrees);
            _controlledPlayer.TickIntentAndActions(in intent, _ballActor.Simulation, deltaTime);
            _ballActor.TickPhysics(deltaTime);
            _ballActor.SyncTransformsFromSimulation(deltaTime);

            // Evaluate Goal Detection
            GoalEvaluationEvent goalEvt = _goalDetector.EvaluateGoal(
                _ballActor.Simulation.PreviousPosition,
                _ballActor.Simulation.Position,
                _ballActor.Simulation.BallRadius,
                _ballActor.Simulation.CurrentState);

            if (goalEvt.isGoalScored)
            {
                // In single-player training, scoring into either goal increments the training goal tally
                _matchState.RecordGoal(TeamSide.Home);
                _broadcastCamera.TriggerGoalShake();
                _statusBannerMessage = $"GOAL SCORED! TOTAL GOALS: {_matchState.HomeScore}";
                _postGoalResetCountdown = 1.15f;
            }
            else
            {
                OutOfPlayRestartEvent outEvt = _goalDetector.EvaluateOutOfPlay(
                    _ballActor.Simulation.Position,
                    _ballActor.Simulation.BallRadius,
                    _ballActor.Simulation.LastTouchTeam,
                    _ballActor.Simulation.CurrentState);

                if (outEvt.isOutOfPlay)
                {
                    _goalDetector.NotifyResetOrTeleport();
                    _ballActor.ResetBall(outEvt.restartPosition);
                    _matchState.ScheduleRestart(outEvt.restartType, outEvt.awardedTeam, outEvt.restartPosition);
                    _matchState.ResumePlayingFromRestart();
                    _statusBannerMessage = $"OUT OF PLAY — {outEvt.restartType} AT ({outEvt.restartPosition.x:F1}, {outEvt.restartPosition.z:F1})";
                }
            }

            _broadcastCamera.TickCamera(_controlledPlayer.Motor.Forward, deltaTime);
            RefreshHudNow();
        }

        private void RefreshHudNow()
        {
            if (_uiView == null || _controlledPlayer == null || _ballActor == null)
            {
                return;
            }

            _uiView.UpdateHudTelemetry(
                _matchState.HomeScore,
                _matchState.ElapsedSeconds,
                _controlledPlayer.Motor.NormalizedStamina,
                _controlledPlayer.Motor.IsExhausted,
                _controlledPlayer.ShotController.NormalizedCharge,
                _controlledPlayer.ShotController.IsCharging,
                _statusBannerMessage);

            bool isOffScreen = _broadcastCamera.TryGetOffScreenBallIndicator(
                _ballActor.Simulation.Position,
                out Vector2 clampedViewportPos,
                out float indicatorAngle);
            _uiView.UpdateOffScreenBallIndicator(isOffScreen, clampedViewportPos, indicatorAngle);
        }

        private void PollHardwareNavigationShortcuts()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }

            // Android Back button maps to Escape key in Unity Input System
            if (kb.escapeKey.wasPressedThisFrame)
            {
                HandleBackNavigation();
            }

            if (kb.rKey.wasPressedThisFrame && !_uiView.IsLaunchScreenVisible && !_matchState.IsOfflineSimulationPaused)
            {
                ResetFieldPositions("MANUAL TRAINING RESET");
            }
        }

        public void HandleBackNavigation()
        {
            if (_uiView.IsSettingsModalVisible)
            {
                _uiView.SetSettingsModalOpen(false);
                return;
            }

            if (!_uiView.IsLaunchScreenVisible)
            {
                TogglePauseMenu();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && _isInitialized && _uiView != null && !_uiView.IsLaunchScreenVisible)
            {
                _intentSource?.CancelAllActiveInputs();
                _controlledPlayer?.CancelAllActiveActions();
                if (!_matchState.IsOfflineSimulationPaused)
                {
                    TogglePauseMenu();
                }
            }
        }

        private static void EnsureDirectionalLight()
        {
            if (FindFirstObjectByType<Light>() != null)
            {
                return;
            }

            var lightGo = new GameObject("StadiumSunDirectionalLight");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.97f, 0.90f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        }

        private void EnsurePitch()
        {
            _pitchBuilder = FindFirstObjectByType<StylizedPitchBuilder>();
            if (_pitchBuilder == null)
            {
                var pitchGo = new GameObject("StylizedFootballPitch");
                _pitchBuilder = pitchGo.AddComponent<StylizedPitchBuilder>();
            }
            _pitchBuilder.BuildPitchGeometry(pitchConfig);
        }

        private void EnsurePlayerAndBall()
        {
            _controlledPlayer = FindFirstObjectByType<FootballerActor>();
            if (_controlledPlayer == null)
            {
                var playerGo = new GameObject("ControlledFootballer_Home10");
                _controlledPlayer = playerGo.AddComponent<FootballerActor>();
            }

            _controlledPlayer.Initialize(
                id: 1,
                side: TeamSide.Home,
                humanControlled: true,
                spawnPosition: DefaultPlayerSpawnPos,
                spawnForward: DefaultPlayerSpawnForward,
                moveTuning: movementTuning,
                actTuning: actionTuning,
                pitchCfg: pitchConfig);

            _controlledPlayer.OnActionFeedback += msg => _statusBannerMessage = msg;

            _ballActor = FindFirstObjectByType<BallActor>();
            if (_ballActor == null)
            {
                var ballGo = new GameObject("AuthoritativeFootball");
                _ballActor = ballGo.AddComponent<BallActor>();
            }
            _ballActor.Initialize(ballTuning, pitchConfig, DefaultBallSpawnPos);
        }

        private void EnsureCamera()
        {
            _broadcastCamera = FindFirstObjectByType<BroadcastMatchCamera>();
            if (_broadcastCamera == null)
            {
                Camera mainCam = Camera.main;
                if (mainCam == null)
                {
                    var camGo = new GameObject("MainBroadcastCamera");
                    camGo.tag = "MainCamera";
                    mainCam = camGo.AddComponent<Camera>();
                    camGo.AddComponent<AudioListener>();
                }
                _broadcastCamera = mainCam.gameObject.AddComponent<BroadcastMatchCamera>();
            }

            _broadcastCamera.Initialize(
                cameraTuning,
                pitchConfig,
                _controlledPlayer.transform,
                _ballActor.transform);
            _broadcastCamera.SetZoomScale(_currentSettings.cameraZoomScale);
        }

        private void EnsureInputAndUi()
        {
            _intentSource = FindFirstObjectByType<TouchAndHardwareIntentSource>();
            if (_intentSource == null)
            {
                var inputGo = new GameObject("PlayerInputIntentSource");
                _intentSource = inputGo.AddComponent<TouchAndHardwareIntentSource>();
            }

            _uiView = FindFirstObjectByType<TrainingHudAndMenusView>();
            if (_uiView == null)
            {
                var uiGo = new GameObject("PocketClubsCanvasUI");
                _uiView = uiGo.AddComponent<TrainingHudAndMenusView>();
            }

            _uiView.InitializeUI(_intentSource, _currentSettings);
            _uiView.OnPlayTrainingRequested += StartTrainingSession;
            _uiView.OnResetTrainingRequested += () => ResetFieldPositions("BALL & PLAYER RESET");
            _uiView.OnPauseToggleRequested += TogglePauseMenu;
            _uiView.OnReturnToLaunchRequested += ReturnToLaunchScreen;
            _uiView.OnQuitRequested += () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            };
            _uiView.OnSettingsUpdated += updatedSettings =>
            {
                _currentSettings = updatedSettings;
                _broadcastCamera?.SetZoomScale(_currentSettings.cameraZoomScale);
            };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureCoordinatorOnBoot()
        {
            if (SuppressAutoBootstrapForTests)
            {
                return;
            }

            if (FindFirstObjectByType<TrainingSliceCoordinator>() == null)
            {
                var bootstrapGo = new GameObject("PocketClubsTrainingCoordinator");
                bootstrapGo.AddComponent<TrainingSliceCoordinator>();
            }
        }
    }
}
