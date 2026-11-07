using System;
using PocketClubs.Core;
using UnityEngine;

namespace PocketClubs.Persistence
{
    [Serializable]
    public struct GameSettingsDto
    {
        public int schemaVersion;
        public string guestNamespace;
        public bool isAudioMuted;
        public int graphicsQualityTier;
        public float cameraZoomScale;

        public const int CurrentSchemaVersion = 1;
        public const string DefaultGuestNamespace = "local_guest";

        public static GameSettingsDto CreateDefault()
        {
            return new GameSettingsDto
            {
                schemaVersion = CurrentSchemaVersion,
                guestNamespace = DefaultGuestNamespace,
                isAudioMuted = false,
                graphicsQualityTier = (int)GraphicsQualityTier.Balanced60Fps,
                cameraZoomScale = 1.0f
            };
        }
    }

    /// <summary>
    /// Versioned local settings repository with untrusted-input sanitization and corruption recovery.
    /// Offline settings and guest state are strictly isolated to the local guest namespace.
    /// </summary>
    public static class LocalSettingsRepository
    {
        public const string PlayerPrefsKey = "PocketClubs.LocalGuest.Settings.v1";

        public static GameSettingsDto SanitizeAndRecover(in GameSettingsDto untrusted, float minZoom = 0.85f, float maxZoom = 1.20f)
        {
            GameSettingsDto fallback = GameSettingsDto.CreateDefault();

            if (untrusted.schemaVersion != GameSettingsDto.CurrentSchemaVersion)
            {
                return fallback;
            }

            string ns = string.IsNullOrWhiteSpace(untrusted.guestNamespace)
                ? GameSettingsDto.DefaultGuestNamespace
                : untrusted.guestNamespace.Trim();

            int clampedTier = Mathf.Clamp(
                untrusted.graphicsQualityTier,
                (int)GraphicsQualityTier.Low30Fps,
                (int)GraphicsQualityTier.Balanced60Fps);

            float zoom = untrusted.cameraZoomScale;
            if (float.IsNaN(zoom) || float.IsInfinity(zoom) || zoom <= 0f)
            {
                zoom = fallback.cameraZoomScale;
            }
            else
            {
                zoom = Mathf.Clamp(zoom, minZoom, maxZoom);
            }

            return new GameSettingsDto
            {
                schemaVersion = GameSettingsDto.CurrentSchemaVersion,
                guestNamespace = ns,
                isAudioMuted = untrusted.isAudioMuted,
                graphicsQualityTier = clampedTier,
                cameraZoomScale = zoom
            };
        }

        public static string SerializeToJson(in GameSettingsDto settings)
        {
            GameSettingsDto sanitized = SanitizeAndRecover(in settings);
            return JsonUtility.ToJson(sanitized);
        }

        public static GameSettingsDto DeserializeFromJsonOrRecover(string rawJson, out bool recoveredFromCorruption)
        {
            recoveredFromCorruption = false;
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                recoveredFromCorruption = true;
                return GameSettingsDto.CreateDefault();
            }

            try
            {
                GameSettingsDto parsed = JsonUtility.FromJson<GameSettingsDto>(rawJson);
                if (parsed.schemaVersion != GameSettingsDto.CurrentSchemaVersion)
                {
                    recoveredFromCorruption = true;
                    return GameSettingsDto.CreateDefault();
                }

                return SanitizeAndRecover(in parsed);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PocketClubs] Corrupted local settings JSON detected; resetting to safe defaults. ({ex.Message})");
                recoveredFromCorruption = true;
                return GameSettingsDto.CreateDefault();
            }
        }

        public static GameSettingsDto LoadFromPlayerPrefs()
        {
            if (!PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                return GameSettingsDto.CreateDefault();
            }

            string raw = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            GameSettingsDto dto = DeserializeFromJsonOrRecover(raw, out bool recovered);
            if (recovered)
            {
                SaveToPlayerPrefs(in dto);
            }
            return dto;
        }

        public static void SaveToPlayerPrefs(in GameSettingsDto settings)
        {
            string json = SerializeToJson(in settings);
            PlayerPrefs.SetString(PlayerPrefsKey, json);
            PlayerPrefs.Save();
        }

        public static void ApplyRuntimeSettings(in GameSettingsDto settings)
        {
            GameSettingsDto safe = SanitizeAndRecover(in settings);
            AudioListener.volume = safe.isAudioMuted ? 0f : 1f;

            var tier = (GraphicsQualityTier)safe.graphicsQualityTier;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = tier == GraphicsQualityTier.Low30Fps ? 30 : 60;
        }
    }
}
