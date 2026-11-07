using System;
using UnityEngine;

namespace PocketClubs.Input
{
    /// <summary>
    /// Hardware-independent player command intent shared across local touch/keyboard input,
    /// bots, and future authoritative network input serialization.
    /// </summary>
    [Serializable]
    public struct PlayerCommandIntent
    {
        public uint sequenceNumber;
        public Vector2 cameraRelativeMove;
        public Vector2 worldMoveDirection;
        public bool sprintHeld;
        public bool passPressed;
        public bool shootHeld;
        public bool shootReleased;
        public bool shootCancelled;
        public bool tacklePressed;

        public bool HasMoveInput => worldMoveDirection.sqrMagnitude > 0.0001f;

        public static Vector2 NormalizeDiagonalInput(Vector2 rawInput)
        {
            if (float.IsNaN(rawInput.x) || float.IsNaN(rawInput.y) ||
                float.IsInfinity(rawInput.x) || float.IsInfinity(rawInput.y))
            {
                return Vector2.zero;
            }

            float sqrMag = rawInput.sqrMagnitude;
            if (sqrMag < 0.0001f)
            {
                return Vector2.zero;
            }

            return sqrMag > 1.0f ? rawInput / Mathf.Sqrt(sqrMag) : rawInput;
        }

        public static Vector2 ConvertCameraRelativeToWorldXZ(Vector2 normalizedCameraInput, float cameraYawDegrees)
        {
            if (normalizedCameraInput.sqrMagnitude < 0.0001f)
            {
                return Vector2.zero;
            }

            float rad = cameraYawDegrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(rad);
            float cos = Mathf.Cos(rad);

            // Camera forward on XZ is (sin, cos); camera right on XZ is (cos, -sin)
            float worldX = normalizedCameraInput.x * cos + normalizedCameraInput.y * sin;
            float worldZ = -normalizedCameraInput.x * sin + normalizedCameraInput.y * cos;
            return NormalizeDiagonalInput(new Vector2(worldX, worldZ));
        }

        public static PlayerCommandIntent Sanitize(in PlayerCommandIntent raw, float cameraYawDegrees)
        {
            Vector2 clampedCameraMove = NormalizeDiagonalInput(raw.cameraRelativeMove);
            Vector2 computedWorldMove = raw.worldMoveDirection.sqrMagnitude > 0.0001f && clampedCameraMove.sqrMagnitude < 0.0001f
                ? NormalizeDiagonalInput(raw.worldMoveDirection)
                : ConvertCameraRelativeToWorldXZ(clampedCameraMove, cameraYawDegrees);

            bool cancelled = raw.shootCancelled;
            return new PlayerCommandIntent
            {
                sequenceNumber = raw.sequenceNumber,
                cameraRelativeMove = clampedCameraMove,
                worldMoveDirection = computedWorldMove,
                sprintHeld = !cancelled && raw.sprintHeld,
                passPressed = !cancelled && raw.passPressed,
                shootHeld = !cancelled && raw.shootHeld,
                shootReleased = !cancelled && raw.shootReleased,
                shootCancelled = cancelled,
                tacklePressed = !cancelled && raw.tacklePressed
            };
        }

        public static PlayerCommandIntent CreateCancelled(uint sequenceNumber)
        {
            return new PlayerCommandIntent
            {
                sequenceNumber = sequenceNumber,
                cameraRelativeMove = Vector2.zero,
                worldMoveDirection = Vector2.zero,
                sprintHeld = false,
                passPressed = false,
                shootHeld = false,
                shootReleased = false,
                shootCancelled = true,
                tacklePressed = false
            };
        }
    }

    public interface IPlayerIntentSource
    {
        PlayerCommandIntent SampleIntent(float cameraYawDegrees);
        void CancelAllActiveInputs();
    }
}
