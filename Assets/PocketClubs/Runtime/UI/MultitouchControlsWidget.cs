using System;
using PocketClubs.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PocketClubs.UI
{
    /// <summary>
    /// Adapts a RectTransform to Screen.safeArea across diverse mobile aspect ratios and display cutouts.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaPanel : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void Update()
        {
            if (_lastSafeArea != Screen.safeArea ||
                _lastScreenSize.x != Screen.width ||
                _lastScreenSize.y != Screen.height)
            {
                ApplySafeArea();
            }
        }

        public void ApplySafeArea()
        {
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }

            Rect safeArea = Screen.safeArea;
            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// Left-thumb virtual joystick with strict pointerId ownership so simultaneous right-hand
    /// action touches never interfere with movement vector calculation.
    /// </summary>
    public sealed class VirtualJoystickZone : MonoBehaviour,
        IPointerDownHandler,
        IDragHandler,
        IPointerUpHandler,
        IPointerCancelHandler
    {
        public const int NoPointerId = int.MinValue;

        [SerializeField] private RectTransform backgroundRect;
        [SerializeField] private RectTransform handleRect;
        [SerializeField] private float maxHandleRadiusPixels = 75f;

        private TouchAndHardwareIntentSource _intentSource;
        private int _ownedPointerId = NoPointerId;
        private Vector2 _originLocalPoint;
        private Vector2 _currentNormalizedVector;

        public int OwnedPointerId => _ownedPointerId;
        public bool HasActiveTouch => _ownedPointerId != NoPointerId;
        public Vector2 CurrentNormalizedVector => _currentNormalizedVector;

        public void Bind(
            TouchAndHardwareIntentSource intentSource,
            RectTransform bgRect,
            RectTransform knobRect,
            float radiusPixels = 75f)
        {
            _intentSource = intentSource;
            backgroundRect = bgRect;
            handleRect = knobRect;
            maxHandleRadiusPixels = Mathf.Max(20f, radiusPixels);
            ResetJoystickState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null) return;

            if (_ownedPointerId != NoPointerId)
            {
                // Ignore secondary finger landing inside the joystick zone
                return;
            }

            _ownedPointerId = eventData.pointerId;
            if (backgroundRect != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    backgroundRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                _originLocalPoint = Vector2.zero;
                UpdateJoystickFromLocalDelta(localPoint - _originLocalPoint);
            }
            else
            {
                _originLocalPoint = eventData.position;
                UpdateJoystickFromLocalDelta(Vector2.zero);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerId != _ownedPointerId)
            {
                return;
            }

            if (backgroundRect != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    backgroundRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                UpdateJoystickFromLocalDelta(localPoint - _originLocalPoint);
            }
            else
            {
                UpdateJoystickFromLocalDelta(eventData.position - _originLocalPoint);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerId != _ownedPointerId)
            {
                return;
            }

            ResetJoystickState();
        }

        public void OnPointerCancel(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerId != _ownedPointerId)
            {
                return;
            }

            ResetJoystickState();
        }

        public void SimulateTouchDownAndDrag(int pointerId, Vector2 pixelDeltaFromCenter)
        {
            if (_ownedPointerId != NoPointerId && _ownedPointerId != pointerId)
            {
                return;
            }

            _ownedPointerId = pointerId;
            UpdateJoystickFromLocalDelta(pixelDeltaFromCenter);
        }

        public void SimulateTouchUp(int pointerId)
        {
            if (_ownedPointerId == pointerId)
            {
                ResetJoystickState();
            }
        }

        public void ResetJoystickState()
        {
            _ownedPointerId = NoPointerId;
            _currentNormalizedVector = Vector2.zero;
            if (handleRect != null)
            {
                handleRect.anchoredPosition = Vector2.zero;
            }

            _intentSource?.SetTouchMove(Vector2.zero);
        }

        private void OnDisable()
        {
            ResetJoystickState();
        }

        private void UpdateJoystickFromLocalDelta(Vector2 deltaPixels)
        {
            float radius = Mathf.Max(20f, maxHandleRadiusPixels);
            Vector2 normalized = deltaPixels / radius;
            normalized = PlayerCommandIntent.NormalizeDiagonalInput(normalized);

            _currentNormalizedVector = normalized;
            if (handleRect != null)
            {
                handleRect.anchoredPosition = normalized * radius;
            }

            _intentSource?.SetTouchMove(normalized);
        }
    }

    public enum TouchActionKind : byte
    {
        Pass = 0,
        HoldShoot = 1,
        Tackle = 2,
        HoldSprint = 3
    }

    /// <summary>
    /// Right-side action button supporting independent pointerId ownership so players can move,
    /// sprint, and charge/release shots or passes simultaneously without touch cross-talk.
    /// </summary>
    public sealed class TouchActionButton : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerCancelHandler
    {
        public const int NoPointerId = int.MinValue;

        [SerializeField] private TouchActionKind actionKind;
        [SerializeField] private Image buttonBackground;

        private TouchAndHardwareIntentSource _intentSource;
        private int _ownedPointerId = NoPointerId;
        private Color _idleColor = new Color(0.14f, 0.22f, 0.34f, 0.88f);
        private Color _pressedColor = new Color(0.24f, 0.68f, 0.92f, 0.96f);

        public int OwnedPointerId => _ownedPointerId;
        public bool IsPressed => _ownedPointerId != NoPointerId;
        public TouchActionKind ActionKind => actionKind;

        public void Bind(
            TouchAndHardwareIntentSource intentSource,
            TouchActionKind kind,
            Image bgImage,
            Color idleColor,
            Color pressedColor)
        {
            _intentSource = intentSource;
            actionKind = kind;
            buttonBackground = bgImage;
            _idleColor = idleColor;
            _pressedColor = pressedColor;
            CancelOwnedTouch();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null) return;
            HandlePointerDownById(eventData.pointerId);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData == null) return;
            HandlePointerUpById(eventData.pointerId, isCancel: false);
        }

        public void OnPointerCancel(PointerEventData eventData)
        {
            if (eventData == null) return;
            HandlePointerUpById(eventData.pointerId, isCancel: true);
        }

        public void HandlePointerDownById(int pointerId)
        {
            if (_ownedPointerId != NoPointerId)
            {
                return;
            }

            _ownedPointerId = pointerId;
            UpdateVisual(true);

            if (_intentSource == null) return;

            switch (actionKind)
            {
                case TouchActionKind.Pass:
                    _intentSource.QueueTouchPass();
                    break;
                case TouchActionKind.HoldShoot:
                    _intentSource.SetTouchShootHeld(true);
                    break;
                case TouchActionKind.Tackle:
                    _intentSource.QueueTouchTackle();
                    break;
                case TouchActionKind.HoldSprint:
                    _intentSource.SetTouchSprintHeld(true);
                    break;
            }
        }

        public void HandlePointerUpById(int pointerId, bool isCancel)
        {
            if (_ownedPointerId != pointerId)
            {
                return;
            }

            _ownedPointerId = NoPointerId;
            UpdateVisual(false);

            if (_intentSource == null) return;

            switch (actionKind)
            {
                case TouchActionKind.HoldShoot:
                    if (isCancel) _intentSource.CancelTouchShoot();
                    else _intentSource.SetTouchShootHeld(false);
                    break;
                case TouchActionKind.HoldSprint:
                    _intentSource.SetTouchSprintHeld(false);
                    break;
            }
        }

        public void CancelOwnedTouch()
        {
            if (_ownedPointerId != NoPointerId)
            {
                HandlePointerUpById(_ownedPointerId, isCancel: true);
            }
            else
            {
                UpdateVisual(false);
            }
        }

        private void OnDisable()
        {
            CancelOwnedTouch();
        }

        private void UpdateVisual(bool pressed)
        {
            if (buttonBackground != null)
            {
                buttonBackground.color = pressed ? _pressedColor : _idleColor;
            }
        }
    }
}
