#if UNITY_EDITOR
using System;
using System.IO;
using PocketClubs.CameraSystem;
using PocketClubs.Core;
using PocketClubs.Gameplay;
using PocketClubs.Input;
using PocketClubs.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PocketClubs.Editor
{
    /// <summary>
    /// Reproducible Editor generator for Gate 0 & Gate 1 ScriptableObject configs, prefabs,
    /// startup/training scenes, and Android PlayerSettings.
    /// Avoids fragile hand-edited scene YAML and undocumented Inspector wiring.
    /// </summary>
    public static class ProjectBootstrap
    {
        public const string DataFolder = "Assets/PocketClubs/Data";
        public const string PrefabsFolder = "Assets/PocketClubs/Prefabs";
        public const string ScenesFolder = "Assets/PocketClubs/Scenes";

        public const string BootScenePath = ScenesFolder + "/Boot.unity";
        public const string TrainingScenePath = ScenesFolder + "/Training.unity";

        [MenuItem("PocketClubs/Bootstrap/Generate Gate 0 & Gate 1 Assets and Scenes")]
        public static void GenerateAllAssetsAndScenes()
        {
            EnsureFolderExists("Assets/PocketClubs");
            EnsureFolderExists(DataFolder);
            EnsureFolderExists(PrefabsFolder);
            EnsureFolderExists(ScenesFolder);

            ConfigureProjectAndAndroidPlayerSettings();

            var movementTuning = CreateOrLoadAsset<MovementTuningConfig>(DataFolder + "/DefaultMovementTuning.asset");
            var ballTuning = CreateOrLoadAsset<BallTuningConfig>(DataFolder + "/DefaultBallTuning.asset");
            var actionTuning = CreateOrLoadAsset<ActionTuningConfig>(DataFolder + "/DefaultActionTuning.asset");
            var pitchConfig = CreateOrLoadAsset<PitchConfig>(DataFolder + "/DefaultPitchConfig.asset");
            var cameraTuning = CreateOrLoadAsset<CameraTuningConfig>(DataFolder + "/DefaultCameraTuning.asset");

            GeneratePrefabs(pitchConfig, movementTuning, ballTuning, actionTuning);
            GenerateSceneWithCoordinator(BootScenePath, movementTuning, ballTuning, actionTuning, pitchConfig, cameraTuning);
            GenerateSceneWithCoordinator(TrainingScenePath, movementTuning, ballTuning, actionTuning, pitchConfig, cameraTuning);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(TrainingScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PocketClubs] Gate 0 & Gate 1 assets, prefabs, scenes, and Android settings generated successfully.");
        }

        public static void GenerateAllFromCommandLine()
        {
            try
            {
                GenerateAllAssetsAndScenes();
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PocketClubs] ProjectBootstrap failed: {ex}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                throw;
            }
        }

        public static void ConfigureProjectAndAndroidPlayerSettings()
        {
            PlayerSettings.companyName = "GPT Builder LLC";
            PlayerSettings.productName = "Pocket Clubs";
            PlayerSettings.bundleVersion = "0.1.0-gate1";

            // Enforce Landscape orientation for mobile football play
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            NamedBuildTarget androidTarget = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(androidTarget, "com.gptbuilder.pocketclubs");
            PlayerSettings.SetScriptingBackend(androidTarget, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(androidTarget, ApiCompatibilityLevel.NET_Standard);

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        }

        private static void GeneratePrefabs(
            PitchConfig pitchConfig,
            MovementTuningConfig moveTuning,
            BallTuningConfig ballTuning,
            ActionTuningConfig actionTuning)
        {
            // Pitch Prefab
            var pitchTemp = new GameObject("StylizedFootballPitch");
            var pitchBuilder = pitchTemp.AddComponent<StylizedPitchBuilder>();
            pitchBuilder.BuildPitchGeometry(pitchConfig);
            PrefabUtility.SaveAsPrefabAsset(pitchTemp, PrefabsFolder + "/StylizedPitch.prefab");
            UnityEngine.Object.DestroyImmediate(pitchTemp);

            // Player Prefab
            var playerTemp = new GameObject("ControlledFootballer");
            var playerActor = playerTemp.AddComponent<FootballerActor>();
            playerActor.Initialize(1, TeamSide.Home, true, Vector3.zero, Vector3.forward, moveTuning, actionTuning, pitchConfig);
            PrefabUtility.SaveAsPrefabAsset(playerTemp, PrefabsFolder + "/ControlledFootballer.prefab");
            UnityEngine.Object.DestroyImmediate(playerTemp);

            // Ball Prefab
            var ballTemp = new GameObject("AuthoritativeFootball");
            var ballActor = ballTemp.AddComponent<BallActor>();
            ballActor.Initialize(ballTuning, pitchConfig, new Vector3(0f, ballTuning.ballRadius, 0f));
            PrefabUtility.SaveAsPrefabAsset(ballTemp, PrefabsFolder + "/AuthoritativeFootball.prefab");
            UnityEngine.Object.DestroyImmediate(ballTemp);
        }

        private static void GenerateSceneWithCoordinator(
            string scenePath,
            MovementTuningConfig moveTuning,
            BallTuningConfig ballTuning,
            ActionTuningConfig actionTuning,
            PitchConfig pitchConfig,
            CameraTuningConfig cameraTuning)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("StadiumSunDirectionalLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.97f, 0.90f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            var camGo = new GameObject("MainBroadcastCamera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            var broadcastCam = camGo.AddComponent<BroadcastMatchCamera>();

            var inputGo = new GameObject("PlayerInputIntentSource");
            inputGo.AddComponent<TouchAndHardwareIntentSource>();

            var uiGo = new GameObject("PocketClubsCanvasUI");
            uiGo.AddComponent<TrainingHudAndMenusView>();

            var coordGo = new GameObject("PocketClubsTrainingCoordinator");
            var coordinator = coordGo.AddComponent<TrainingSliceCoordinator>();

            var so = new SerializedObject(coordinator);
            so.FindProperty("movementTuning").objectReferenceValue = moveTuning;
            so.FindProperty("ballTuning").objectReferenceValue = ballTuning;
            so.FindProperty("actionTuning").objectReferenceValue = actionTuning;
            so.FindProperty("pitchConfig").objectReferenceValue = pitchConfig;
            so.FindProperty("cameraTuning").objectReferenceValue = cameraTuning;
            so.ApplyModifiedPropertiesWithoutUndo();

            var camSo = new SerializedObject(broadcastCam);
            camSo.FindProperty("tuning").objectReferenceValue = cameraTuning;
            camSo.FindProperty("pitchConfig").objectReferenceValue = pitchConfig;
            camSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static T CreateOrLoadAsset<T>(string assetPath) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            T instance = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(instance, assetPath);
            EditorUtility.SetDirty(instance);
            return instance;
        }

        private static void EnsureFolderExists(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/') ?? "Assets";
            string folderName = Path.GetFileName(assetFolderPath);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolderExists(parent);
            }
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
#endif
