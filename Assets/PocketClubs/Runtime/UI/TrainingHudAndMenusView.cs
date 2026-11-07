using System;
using PocketClubs.Core;
using PocketClubs.Input;
using PocketClubs.Persistence;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PocketClubs.UI
{
    /// <summary>
    /// Builds and manages the Gate 1 Launch Screen, Training HUD, Multitouch Controls,
    /// Pause Menu, and Settings Modal with zero-allocation cached string/state updates when unchanged.
    /// Does not include inert buttons for unimplemented online/club features.
    /// </summary>
    public sealed class TrainingHudAndMenusView : MonoBehaviour
    {
        private GameObject _launchScreenRoot;
        private GameObject _trainingHudRoot;
        private GameObject _pauseModalRoot;
        private GameObject _settingsModalRoot;

        private Text _scoreAndTeamLabel;
        private Text _timerLabel;
        private Text _controlledPlayerBadgeLabel;
        private Text _staminaLabel;
        private Image _staminaFillImage;
        private Text _shotChargeLabel;
        private Image _shotChargeFillImage;
        private Text _actionFeedbackLabel;

        private RectTransform _offScreenBallIndicatorRect;
        private Text _offScreenBallIndicatorText;

        private Text _muteSettingButtonLabel;
        private Text _qualitySettingButtonLabel;
        private Text _zoomSettingButtonLabel;

        public VirtualJoystickZone JoystickZone { get; private set; }
        public TouchActionButton PassButton { get; private set; }
        public TouchActionButton ShootButton { get; private set; }
        public TouchActionButton TackleButton { get; private set; }
        public TouchActionButton SprintButton { get; private set; }

        public bool IsLaunchScreenVisible => _launchScreenRoot != null && _launchScreenRoot.activeSelf;
        public bool IsTrainingHudVisible => _trainingHudRoot != null && _trainingHudRoot.activeSelf;
        public bool IsPauseModalVisible => _pauseModalRoot != null && _pauseModalRoot.activeSelf;
        public bool IsSettingsModalVisible => _settingsModalRoot != null && _settingsModalRoot.activeSelf;

        public event Action OnPlayTrainingRequested;
        public event Action OnResetTrainingRequested;
        public event Action OnPauseToggleRequested;
        public event Action OnReturnToLaunchRequested;
        public event Action OnQuitRequested;
        public event Action<GameSettingsDto> OnSettingsUpdated;

        private GameSettingsDto _currentSettings;
        private int _lastDisplayedScore = -1;
        private int _lastDisplayedSeconds = -1;
        private int _lastDisplayedStaminaPct = -1;
        private bool _lastExhaustedState;
        private int _lastDisplayedChargePct = -1;

        public void InitializeUI(TouchAndHardwareIntentSource intentSource, GameSettingsDto initialSettings)
        {
            _currentSettings = initialSettings;
            EnsureEventSystem();
            BuildCanvasHierarchy(intentSource);
            RefreshSettingsLabels();
            ShowLaunchScreen();
        }

        public void ShowLaunchScreen()
        {
            JoystickZone?.ResetJoystickState();
            ShootButton?.CancelOwnedTouch();
            SprintButton?.CancelOwnedTouch();

            if (_launchScreenRoot != null) _launchScreenRoot.SetActive(true);
            if (_trainingHudRoot != null) _trainingHudRoot.SetActive(false);
            if (_pauseModalRoot != null) _pauseModalRoot.SetActive(false);
            if (_settingsModalRoot != null) _settingsModalRoot.SetActive(false);
        }

        public void ShowTrainingHud()
        {
            if (_launchScreenRoot != null) _launchScreenRoot.SetActive(false);
            if (_trainingHudRoot != null) _trainingHudRoot.SetActive(true);
            if (_pauseModalRoot != null) _pauseModalRoot.SetActive(false);
            if (_settingsModalRoot != null) _settingsModalRoot.SetActive(false);
        }

        public void SetPauseModalOpen(bool isOpen)
        {
            if (isOpen)
            {
                JoystickZone?.ResetJoystickState();
                ShootButton?.CancelOwnedTouch();
                SprintButton?.CancelOwnedTouch();
            }

            if (_pauseModalRoot != null)
            {
                _pauseModalRoot.SetActive(isOpen);
            }

            if (!isOpen && _settingsModalRoot != null)
            {
                _settingsModalRoot.SetActive(false);
            }
        }

        public void SetSettingsModalOpen(bool isOpen)
        {
            if (_settingsModalRoot != null)
            {
                _settingsModalRoot.SetActive(isOpen);
                if (isOpen)
                {
                    RefreshSettingsLabels();
                }
            }
        }

        public void UpdateHudTelemetry(
            int homeScore,
            float elapsedSeconds,
            float normalizedStamina,
            bool isExhausted,
            float normalizedShotCharge,
            bool isChargingShot,
            string feedbackBannerText)
        {
            if (_scoreAndTeamLabel != null && homeScore != _lastDisplayedScore)
            {
                _lastDisplayedScore = homeScore;
                _scoreAndTeamLabel.text = $"[HOME | AZURE V-STRIPE]  GOALS: {homeScore}";
            }

            int wholeSeconds = Mathf.FloorToInt(elapsedSeconds);
            if (_timerLabel != null && wholeSeconds != _lastDisplayedSeconds)
            {
                _lastDisplayedSeconds = wholeSeconds;
                int mins = wholeSeconds / 60;
                int secs = wholeSeconds % 60;
                _timerLabel.text = $"TRAINING  {mins:00}:{secs:00}";
            }

            int staminaPct = Mathf.RoundToInt(Mathf.Clamp01(normalizedStamina) * 100f);
            if (staminaPct != _lastDisplayedStaminaPct || isExhausted != _lastExhaustedState)
            {
                _lastDisplayedStaminaPct = staminaPct;
                _lastExhaustedState = isExhausted;
                if (_staminaFillImage != null)
                {
                    _staminaFillImage.fillAmount = Mathf.Clamp01(normalizedStamina);
                    _staminaFillImage.color = isExhausted
                        ? new Color(0.92f, 0.30f, 0.24f, 1f)
                        : new Color(0.18f, 0.86f, 0.48f, 1f);
                }

                if (_staminaLabel != null)
                {
                    _staminaLabel.text = isExhausted
                        ? $"STAMINA: {staminaPct}% (RECOVERING)"
                        : $"STAMINA: {staminaPct}%";
                }
            }

            int chargePct = isChargingShot ? Mathf.RoundToInt(Mathf.Clamp01(normalizedShotCharge) * 100f) : 0;
            if (chargePct != _lastDisplayedChargePct)
            {
                _lastDisplayedChargePct = chargePct;
                if (_shotChargeFillImage != null)
                {
                    _shotChargeFillImage.fillAmount = isChargingShot ? Mathf.Clamp01(normalizedShotCharge) : 0f;
                }
                if (_shotChargeLabel != null)
                {
                    _shotChargeLabel.text = isChargingShot
                        ? $"SHOT CHARGE: {chargePct}% (RELEASE TO SHOOT)"
                        : "SHOT CHARGE: READY";
                }
            }

            if (_actionFeedbackLabel != null && !string.IsNullOrEmpty(feedbackBannerText) &&
                _actionFeedbackLabel.text != feedbackBannerText)
            {
                _actionFeedbackLabel.text = feedbackBannerText;
            }
        }

        public void UpdateOffScreenBallIndicator(bool isOffScreen, Vector2 clampedViewportPos, float angleDegrees)
        {
            if (_offScreenBallIndicatorRect == null)
            {
                return;
            }

            if (_offScreenBallIndicatorRect.gameObject.activeSelf != isOffScreen)
            {
                _offScreenBallIndicatorRect.gameObject.SetActive(isOffScreen);
            }

            if (!isOffScreen)
            {
                return;
            }

            _offScreenBallIndicatorRect.anchorMin = clampedViewportPos;
            _offScreenBallIndicatorRect.anchorMax = clampedViewportPos;
            _offScreenBallIndicatorRect.anchoredPosition = Vector2.zero;

            if (_offScreenBallIndicatorText != null)
            {
                string dirLabel = Mathf.Abs(angleDegrees) < 45f ? "BALL >" :
                                  Mathf.Abs(angleDegrees) > 135f ? "< BALL" :
                                  angleDegrees > 0f ? "BALL ^" : "BALL v";
                if (_offScreenBallIndicatorText.text != dirLabel)
                {
                    _offScreenBallIndicatorText.text = dirLabel;
                }
            }
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildCanvasHierarchy(TouchAndHardwareIntentSource intentSource)
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            Font builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtinFont == null)
            {
                builtinFont = Font.CreateDynamicFontFromOSFont("Arial", 24);
            }

            BuildLaunchScreen(builtinFont);
            BuildTrainingHud(builtinFont, intentSource);
            BuildPauseModal(builtinFont);
            BuildSettingsModal(builtinFont);
        }

        private void BuildLaunchScreen(Font font)
        {
            _launchScreenRoot = CreatePanel(transform, "LaunchScreenPanel", new Color(0.06f, 0.11f, 0.18f, 0.96f));
            RectTransform rt = _launchScreenRoot.GetComponent<RectTransform>();
            StretchFull(rt);

            CreateLabel(_launchScreenRoot.transform, "TitleLabel", "POCKET CLUBS", font, 64,
                new Vector2(0.5f, 0.74f), new Vector2(900f, 90f), new Color(0.95f, 0.98f, 1.0f));

            CreateLabel(_launchScreenRoot.transform, "SubtitleLabel",
                "ORIGINAL SMALL-SIDED ANDROID FOOTBALL  |  GATE 1 TRAINING SLICE", font, 24,
                new Vector2(0.5f, 0.65f), new Vector2(1100f, 48f), new Color(0.55f, 0.82f, 0.95f));

            CreateMenuButton(_launchScreenRoot.transform, "PlayTrainingButton", "PLAY TRAINING", font,
                new Vector2(0.5f, 0.48f), new Vector2(420f, 82f), new Color(0.12f, 0.62f, 0.36f, 1f),
                () => OnPlayTrainingRequested?.Invoke());

            CreateMenuButton(_launchScreenRoot.transform, "OpenSettingsButton", "SETTINGS", font,
                new Vector2(0.5f, 0.36f), new Vector2(420f, 74f), new Color(0.18f, 0.32f, 0.50f, 1f),
                () => SetSettingsModalOpen(true));

            CreateMenuButton(_launchScreenRoot.transform, "QuitButton", "EXIT", font,
                new Vector2(0.5f, 0.24f), new Vector2(420f, 68f), new Color(0.42f, 0.18f, 0.20f, 1f),
                () => OnQuitRequested?.Invoke());
        }

        private void BuildTrainingHud(Font font, TouchAndHardwareIntentSource intentSource)
        {
            _trainingHudRoot = new GameObject("TrainingHudSafeAreaRoot", typeof(RectTransform), typeof(SafeAreaPanel));
            _trainingHudRoot.transform.SetParent(transform, false);
            StretchFull(_trainingHudRoot.GetComponent<RectTransform>());

            // Top Telemetry Bar
            GameObject topBar = CreatePanel(_trainingHudRoot.transform, "TopScoreBar", new Color(0.05f, 0.09f, 0.15f, 0.84f));
            RectTransform topRt = topBar.GetComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0.02f, 0.89f);
            topRt.anchorMax = new Vector2(0.98f, 0.98f);
            topRt.offsetMin = Vector2.zero;
            topRt.offsetMax = Vector2.zero;

            _scoreAndTeamLabel = CreateLabel(topBar.transform, "ScoreAndTeamLabel",
                "[HOME | AZURE V-STRIPE]  GOALS: 0", font, 26,
                new Vector2(0.20f, 0.5f), new Vector2(580f, 54f), new Color(0.96f, 0.98f, 1.0f));

            _timerLabel = CreateLabel(topBar.transform, "TimerLabel",
                "TRAINING  00:00", font, 28,
                new Vector2(0.50f, 0.5f), new Vector2(320f, 54f), new Color(0.98f, 0.88f, 0.32f));

            _controlledPlayerBadgeLabel = CreateLabel(topBar.transform, "ControlledPlayerBadge",
                "YOU: #10 PLAYMAKER (OFFLINE LOCAL AUTHORITY)", font, 20,
                new Vector2(0.74f, 0.5f), new Vector2(500f, 54f), new Color(0.32f, 0.96f, 0.88f));

            CreateMenuButton(topBar.transform, "ResetBallHudButton", "RESET", font,
                new Vector2(0.89f, 0.5f), new Vector2(140f, 56f), new Color(0.20f, 0.44f, 0.68f, 0.95f),
                () => OnResetTrainingRequested?.Invoke());

            CreateMenuButton(topBar.transform, "PauseHudButton", "PAUSE", font,
                new Vector2(0.96f, 0.5f), new Vector2(120f, 56f), new Color(0.28f, 0.32f, 0.42f, 0.95f),
                () => OnPauseToggleRequested?.Invoke());

            // Stamina & Shot Charge Bars
            GameObject gaugesPanel = CreatePanel(_trainingHudRoot.transform, "GaugesPanel", new Color(0.05f, 0.09f, 0.15f, 0.76f));
            RectTransform gaugesRt = gaugesPanel.GetComponent<RectTransform>();
            gaugesRt.anchorMin = new Vector2(0.02f, 0.77f);
            gaugesRt.anchorMax = new Vector2(0.38f, 0.87f);
            gaugesRt.offsetMin = Vector2.zero;
            gaugesRt.offsetMax = Vector2.zero;

            _staminaFillImage = CreateProgressBar(gaugesPanel.transform, "StaminaBar", new Vector2(0.5f, 0.70f), new Vector2(620f, 24f), new Color(0.18f, 0.86f, 0.48f, 1f));
            _staminaLabel = CreateLabel(gaugesPanel.transform, "StaminaLabel", "STAMINA: 100%", font, 18, new Vector2(0.5f, 0.70f), new Vector2(600f, 26f), Color.white);

            _shotChargeFillImage = CreateProgressBar(gaugesPanel.transform, "ShotChargeBar", new Vector2(0.5f, 0.28f), new Vector2(620f, 24f), new Color(0.98f, 0.66f, 0.16f, 1f));
            _shotChargeFillImage.fillAmount = 0f;
            _shotChargeLabel = CreateLabel(gaugesPanel.transform, "ShotChargeLabel", "SHOT CHARGE: READY", font, 18, new Vector2(0.5f, 0.28f), new Vector2(600f, 26f), Color.white);

            // Action Feedback Banner
            _actionFeedbackLabel = CreateLabel(_trainingHudRoot.transform, "ActionFeedbackBanner",
                "READY — MOVE WITH LEFT STICK, PASS / CHARGE SHOT / TACKLE ON RIGHT", font, 24,
                new Vector2(0.5f, 0.82f), new Vector2(820f, 48f), new Color(0.95f, 0.98f, 1.0f));

            // Off-Screen Ball Indicator Badge
            GameObject offScreenBadge = CreatePanel(_trainingHudRoot.transform, "OffScreenBallIndicator", new Color(0.95f, 0.72f, 0.12f, 0.94f));
            _offScreenBallIndicatorRect = offScreenBadge.GetComponent<RectTransform>();
            _offScreenBallIndicatorRect.sizeDelta = new Vector2(140f, 48f);
            _offScreenBallIndicatorText = CreateLabel(offScreenBadge.transform, "IndicatorText", "BALL >", font, 22,
                new Vector2(0.5f, 0.5f), new Vector2(130f, 42f), new Color(0.08f, 0.10f, 0.14f));
            offScreenBadge.SetActive(false);

            // Bottom-Left Virtual Joystick Zone
            GameObject joystickBg = CreatePanel(_trainingHudRoot.transform, "LeftVirtualJoystickZone", new Color(0.10f, 0.16f, 0.24f, 0.65f));
            RectTransform joyBgRt = joystickBg.GetComponent<RectTransform>();
            joyBgRt.anchorMin = new Vector2(0.14f, 0.24f);
            joyBgRt.anchorMax = new Vector2(0.14f, 0.24f);
            joyBgRt.sizeDelta = new Vector2(260f, 260f);

            GameObject joystickKnob = CreatePanel(joystickBg.transform, "JoystickHandle", new Color(0.24f, 0.82f, 0.96f, 0.92f));
            RectTransform knobRt = joystickKnob.GetComponent<RectTransform>();
            knobRt.anchorMin = new Vector2(0.5f, 0.5f);
            knobRt.anchorMax = new Vector2(0.5f, 0.5f);
            knobRt.sizeDelta = new Vector2(96f, 96f);

            CreateLabel(joystickBg.transform, "JoystickCaption", "MOVE", font, 20,
                new Vector2(0.5f, 0.12f), new Vector2(160f, 32f), new Color(0.80f, 0.90f, 1.0f, 0.8f));

            JoystickZone = joystickBg.AddComponent<VirtualJoystickZone>();
            JoystickZone.Bind(intentSource, joyBgRt, knobRt, radiusPixels: 85f);

            // Bottom-Right Multitouch Action Cluster (Pass, Hold Shoot, Tackle, Hold Sprint)
            PassButton = CreateTouchActionBtn(_trainingHudRoot.transform, "PassActionButton", "PASS\n(J)", font,
                new Vector2(0.76f, 0.16f), new Vector2(170f, 110f),
                new Color(0.12f, 0.52f, 0.34f, 0.88f), new Color(0.22f, 0.86f, 0.54f, 0.98f),
                intentSource, TouchActionKind.Pass);

            ShootButton = CreateTouchActionBtn(_trainingHudRoot.transform, "ShootActionButton", "HOLD\nSHOOT\n(SPACE)", font,
                new Vector2(0.88f, 0.28f), new Vector2(190f, 130f),
                new Color(0.76f, 0.24f, 0.18f, 0.90f), new Color(0.98f, 0.44f, 0.22f, 0.98f),
                intentSource, TouchActionKind.HoldShoot);

            TackleButton = CreateTouchActionBtn(_trainingHudRoot.transform, "TackleActionButton", "TACKLE\n(K)", font,
                new Vector2(0.76f, 0.34f), new Vector2(165f, 105f),
                new Color(0.56f, 0.42f, 0.14f, 0.88f), new Color(0.92f, 0.74f, 0.22f, 0.98f),
                intentSource, TouchActionKind.Tackle);

            SprintButton = CreateTouchActionBtn(_trainingHudRoot.transform, "SprintActionButton", "HOLD\nSPRINT\n(SHIFT)", font,
                new Vector2(0.90f, 0.12f), new Vector2(175f, 105f),
                new Color(0.16f, 0.38f, 0.68f, 0.88f), new Color(0.28f, 0.68f, 0.98f, 0.98f),
                intentSource, TouchActionKind.HoldSprint);
        }

        private void BuildPauseModal(Font font)
        {
            _pauseModalRoot = CreatePanel(transform, "PauseModalOverlay", new Color(0.03f, 0.06f, 0.10f, 0.88f));
            StretchFull(_pauseModalRoot.GetComponent<RectTransform>());

            CreateLabel(_pauseModalRoot.transform, "PauseHeader", "TRAINING PAUSED", font, 48,
                new Vector2(0.5f, 0.74f), new Vector2(640f, 70f), Color.white);

            CreateMenuButton(_pauseModalRoot.transform, "ResumeButton", "RESUME", font,
                new Vector2(0.5f, 0.56f), new Vector2(400f, 72f), new Color(0.14f, 0.60f, 0.36f, 1f),
                () => OnPauseToggleRequested?.Invoke());

            CreateMenuButton(_pauseModalRoot.transform, "ResetFromPauseButton", "RESET TRAINING", font,
                new Vector2(0.5f, 0.44f), new Vector2(400f, 72f), new Color(0.18f, 0.42f, 0.66f, 1f),
                () =>
                {
                    OnResetTrainingRequested?.Invoke();
                    OnPauseToggleRequested?.Invoke();
                });

            CreateMenuButton(_pauseModalRoot.transform, "SettingsFromPauseButton", "SETTINGS", font,
                new Vector2(0.5f, 0.32f), new Vector2(400f, 72f), new Color(0.24f, 0.32f, 0.46f, 1f),
                () => SetSettingsModalOpen(true));

            CreateMenuButton(_pauseModalRoot.transform, "MainMenuFromPauseButton", "MAIN MENU", font,
                new Vector2(0.5f, 0.20f), new Vector2(400f, 72f), new Color(0.48f, 0.20f, 0.22f, 1f),
                () => OnReturnToLaunchRequested?.Invoke());

            _pauseModalRoot.SetActive(false);
        }

        private void BuildSettingsModal(Font font)
        {
            _settingsModalRoot = CreatePanel(transform, "SettingsModalOverlay", new Color(0.04f, 0.08f, 0.14f, 0.95f));
            StretchFull(_settingsModalRoot.GetComponent<RectTransform>());

            CreateLabel(_settingsModalRoot.transform, "SettingsHeader", "SETTINGS (LOCAL GUEST PROFILE)", font, 42,
                new Vector2(0.5f, 0.76f), new Vector2(820f, 68f), Color.white);

            Button muteBtn = CreateMenuButton(_settingsModalRoot.transform, "ToggleMuteButton", "AUDIO MUTE: OFF", font,
                new Vector2(0.5f, 0.58f), new Vector2(520f, 72f), new Color(0.18f, 0.34f, 0.52f, 1f),
                ToggleAudioMute);
            _muteSettingButtonLabel = muteBtn.GetComponentInChildren<Text>();

            Button qualityBtn = CreateMenuButton(_settingsModalRoot.transform, "ToggleQualityButton", "GRAPHICS: 60 FPS BALANCED", font,
                new Vector2(0.5f, 0.45f), new Vector2(520f, 72f), new Color(0.18f, 0.34f, 0.52f, 1f),
                ToggleGraphicsQuality);
            _qualitySettingButtonLabel = qualityBtn.GetComponentInChildren<Text>();

            Button zoomBtn = CreateMenuButton(_settingsModalRoot.transform, "CycleZoomButton", "CAMERA ZOOM: 1.00x", font,
                new Vector2(0.5f, 0.32f), new Vector2(520f, 72f), new Color(0.18f, 0.34f, 0.52f, 1f),
                CycleCameraZoom);
            _zoomSettingButtonLabel = zoomBtn.GetComponentInChildren<Text>();

            CreateMenuButton(_settingsModalRoot.transform, "CloseSettingsButton", "SAVE & CLOSE", font,
                new Vector2(0.5f, 0.18f), new Vector2(400f, 72f), new Color(0.14f, 0.60f, 0.36f, 1f),
                () => SetSettingsModalOpen(false));

            _settingsModalRoot.SetActive(false);
        }

        private void ToggleAudioMute()
        {
            _currentSettings.isAudioMuted = !_currentSettings.isAudioMuted;
            CommitSettingsChange();
        }

        private void ToggleGraphicsQuality()
        {
            _currentSettings.graphicsQualityTier = _currentSettings.graphicsQualityTier == (int)GraphicsQualityTier.Balanced60Fps
                ? (int)GraphicsQualityTier.Low30Fps
                : (int)GraphicsQualityTier.Balanced60Fps;
            CommitSettingsChange();
        }

        private void CycleCameraZoom()
        {
            if (_currentSettings.cameraZoomScale < 0.95f)
                _currentSettings.cameraZoomScale = 1.00f;
            else if (_currentSettings.cameraZoomScale < 1.10f)
                _currentSettings.cameraZoomScale = 1.15f;
            else
                _currentSettings.cameraZoomScale = 0.88f;

            CommitSettingsChange();
        }

        private void CommitSettingsChange()
        {
            _currentSettings = LocalSettingsRepository.SanitizeAndRecover(in _currentSettings);
            LocalSettingsRepository.SaveToPlayerPrefs(in _currentSettings);
            LocalSettingsRepository.ApplyRuntimeSettings(in _currentSettings);
            RefreshSettingsLabels();
            OnSettingsUpdated?.Invoke(_currentSettings);
        }

        private void RefreshSettingsLabels()
        {
            if (_muteSettingButtonLabel != null)
            {
                _muteSettingButtonLabel.text = _currentSettings.isAudioMuted
                    ? "AUDIO MUTE: ON (SILENT)"
                    : "AUDIO MUTE: OFF";
            }

            if (_qualitySettingButtonLabel != null)
            {
                bool is60 = _currentSettings.graphicsQualityTier == (int)GraphicsQualityTier.Balanced60Fps;
                _qualitySettingButtonLabel.text = is60
                    ? "GRAPHICS: 60 FPS BALANCED"
                    : "GRAPHICS: 30 FPS LOW BATTERY SAVER";
            }

            if (_zoomSettingButtonLabel != null)
            {
                _zoomSettingButtonLabel.text = $"CAMERA ZOOM: {_currentSettings.cameraZoomScale:F2}x";
            }
        }

        private static GameObject CreatePanel(Transform parent, string panelName, Color bgColor)
        {
            var go = new GameObject(panelName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = bgColor;
            return go;
        }

        private static Text CreateLabel(
            Transform parent,
            string labelName,
            string content,
            Font font,
            int fontSize,
            Vector2 anchorCenter,
            Vector2 sizeDelta,
            Color textColor)
        {
            var go = new GameObject(labelName, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorCenter;
            rt.anchorMax = anchorCenter;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = sizeDelta;

            Text txt = go.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = fontSize;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = textColor;
            txt.text = content;
            txt.raycastTarget = false;
            return txt;
        }

        private static Image CreateProgressBar(
            Transform parent,
            string barName,
            Vector2 anchorCenter,
            Vector2 sizeDelta,
            Color fillColor)
        {
            GameObject bg = CreatePanel(parent, $"{barName}_Bg", new Color(0.08f, 0.12f, 0.18f, 0.95f));
            RectTransform bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = anchorCenter;
            bgRt.anchorMax = anchorCenter;
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = sizeDelta;
            bg.GetComponent<Image>().raycastTarget = false;

            GameObject fill = CreatePanel(bg.transform, $"{barName}_Fill", fillColor);
            RectTransform fillRt = fill.GetComponent<RectTransform>();
            StretchFull(fillRt);
            Image fillImg = fill.GetComponent<Image>();
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = 0;
            fillImg.fillAmount = 1f;
            fillImg.raycastTarget = false;
            return fillImg;
        }

        private static Button CreateMenuButton(
            Transform parent,
            string btnName,
            string labelText,
            Font font,
            Vector2 anchorCenter,
            Vector2 sizeDelta,
            Color bgColor,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = CreatePanel(parent, btnName, bgColor);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorCenter;
            rt.anchorMax = anchorCenter;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = sizeDelta;

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            if (onClick != null)
            {
                btn.onClick.AddListener(onClick);
            }

            CreateLabel(go.transform, "ButtonText", labelText, font, 24,
                new Vector2(0.5f, 0.5f), sizeDelta, Color.white);
            return btn;
        }

        private static TouchActionButton CreateTouchActionBtn(
            Transform parent,
            string btnName,
            string labelText,
            Font font,
            Vector2 anchorCenter,
            Vector2 sizeDelta,
            Color idleColor,
            Color pressedColor,
            TouchAndHardwareIntentSource intentSource,
            TouchActionKind kind)
        {
            GameObject go = CreatePanel(parent, btnName, idleColor);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorCenter;
            rt.anchorMax = anchorCenter;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = sizeDelta;

            Image bgImage = go.GetComponent<Image>();
            bgImage.raycastTarget = true;

            TouchActionButton actionBtn = go.AddComponent<TouchActionButton>();
            actionBtn.Bind(intentSource, kind, bgImage, idleColor, pressedColor);

            CreateLabel(go.transform, "ActionLabel", labelText, font, 22,
                new Vector2(0.5f, 0.5f), sizeDelta - new Vector2(12f, 12f), Color.white);
            return actionBtn;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
