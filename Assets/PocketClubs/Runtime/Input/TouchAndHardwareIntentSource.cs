using UnityEngine;
using UnityEngine.InputSystem;

namespace PocketClubs.Input
{
    /// <summary>
    /// Aggregates multitouch virtual controls and optional keyboard/gamepad inputs into a
    /// hardware-agnostic PlayerCommandIntent without allocations.
    /// Ensures safe input cancellation when application focus or touch ownership is lost.
    /// </summary>
    public sealed class TouchAndHardwareIntentSource : MonoBehaviour, IPlayerIntentSource
    {
        private uint _sequenceCounter;
        private Vector2 _touchMoveVector;
        private bool _touchSprintHeld;
        private bool _touchPassQueued;
        private bool _touchShootHeld;
        private bool _touchShootReleasedQueued;
        private bool _touchShootCancelledQueued;
        private bool _touchTackleQueued;
        private bool _wasHardwareShootHeld;

        public Vector2 CurrentTouchMoveVector => _touchMoveVector;
        public bool IsTouchSprintHeld => _touchSprintHeld;
        public bool IsTouchShootHeld => _touchShootHeld;

        public void SetTouchMove(Vector2 rawMove)
        {
            _touchMoveVector = PlayerCommandIntent.NormalizeDiagonalInput(rawMove);
        }

        public void SetTouchSprintHeld(bool held)
        {
            _touchSprintHeld = held;
        }

        public void QueueTouchPass()
        {
            _touchPassQueued = true;
        }

        public void SetTouchShootHeld(bool held)
        {
            if (_touchShootHeld && !held)
            {
                _touchShootReleasedQueued = true;
            }
            _touchShootHeld = held;
        }

        public void CancelTouchShoot()
        {
            if (_touchShootHeld || _touchShootReleasedQueued)
            {
                _touchShootCancelledQueued = true;
            }
            _touchShootHeld = false;
            _touchShootReleasedQueued = false;
        }

        public void QueueTouchTackle()
        {
            _touchTackleQueued = true;
        }

        public void CancelAllActiveInputs()
        {
            bool hadActiveShoot = _touchShootHeld || _wasHardwareShootHeld || _touchShootReleasedQueued;
            _touchMoveVector = Vector2.zero;
            _touchSprintHeld = false;
            _touchPassQueued = false;
            _touchShootHeld = false;
            _touchShootReleasedQueued = false;
            _touchTackleQueued = false;
            _wasHardwareShootHeld = false;

            if (hadActiveShoot)
            {
                _touchShootCancelledQueued = true;
            }
        }

        public PlayerCommandIntent SampleIntent(float cameraYawDegrees)
        {
            _sequenceCounter++;

            Vector2 hardwareMove = ReadHardwareMove();
            bool hardwareSprint = ReadHardwareSprint();
            bool hardwarePass = ReadHardwarePassPressed();
            bool hardwareTackle = ReadHardwareTacklePressed();
            bool hardwareShootHeld = ReadHardwareShootHeld();
            bool hardwareShootReleased = _wasHardwareShootHeld && !hardwareShootHeld;
            _wasHardwareShootHeld = hardwareShootHeld;

            Vector2 combinedMove = _touchMoveVector.sqrMagnitude >= hardwareMove.sqrMagnitude
                ? _touchMoveVector
                : hardwareMove;

            bool cancelQueued = _touchShootCancelledQueued;
            bool passPressed = !cancelQueued && (_touchPassQueued || hardwarePass);
            bool tacklePressed = !cancelQueued && (_touchTackleQueued || hardwareTackle);
            bool shootHeld = !cancelQueued && (_touchShootHeld || hardwareShootHeld);
            bool shootReleased = !cancelQueued && (_touchShootReleasedQueued || hardwareShootReleased);
            bool sprintHeld = !cancelQueued && (_touchSprintHeld || hardwareSprint);

            _touchPassQueued = false;
            _touchTackleQueued = false;
            _touchShootReleasedQueued = false;
            _touchShootCancelledQueued = false;

            var rawIntent = new PlayerCommandIntent
            {
                sequenceNumber = _sequenceCounter,
                cameraRelativeMove = combinedMove,
                sprintHeld = sprintHeld,
                passPressed = passPressed,
                shootHeld = shootHeld,
                shootReleased = shootReleased,
                shootCancelled = cancelQueued,
                tacklePressed = tacklePressed
            };

            return PlayerCommandIntent.Sanitize(in rawIntent, cameraYawDegrees);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                CancelAllActiveInputs();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                CancelAllActiveInputs();
            }
        }

        private void OnDisable()
        {
            CancelAllActiveInputs();
        }

        private static Vector2 ReadHardwareMove()
        {
            Vector2 move = Vector2.zero;

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
            }

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > move.sqrMagnitude)
                {
                    move = stick;
                }
            }

            return PlayerCommandIntent.NormalizeDiagonalInput(move);
        }

        private static bool ReadHardwareSprint()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed))
            {
                return true;
            }

            Gamepad pad = Gamepad.current;
            return pad != null && pad.rightTrigger.ReadValue() > 0.35f;
        }

        private static bool ReadHardwarePassPressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.jKey.wasPressedThisFrame)
            {
                return true;
            }

            Gamepad pad = Gamepad.current;
            return pad != null && pad.buttonSouth.wasPressedThisFrame;
        }

        private static bool ReadHardwareShootHeld()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.spaceKey.isPressed)
            {
                return true;
            }

            Gamepad pad = Gamepad.current;
            return pad != null && pad.buttonEast.isPressed;
        }

        private static bool ReadHardwareTacklePressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.kKey.wasPressedThisFrame)
            {
                return true;
            }

            Gamepad pad = Gamepad.current;
            return pad != null && pad.buttonWest.wasPressedThisFrame;
        }
    }
}
