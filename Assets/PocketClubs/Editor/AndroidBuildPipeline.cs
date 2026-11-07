#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PocketClubs.Editor
{
    /// <summary>
    /// Reproducible Android Development APK build pipeline for Gate 1.
    /// Can be invoked from the Unity Editor menu or via CLI batchmode:
    /// -executeMethod PocketClubs.Editor.AndroidBuildPipeline.BuildDebugApkFromCommandLine
    /// </summary>
    public static class AndroidBuildPipeline
    {
        public const string DefaultApkOutputPath = "Builds/Android/PocketClubs-Gate1-Debug.apk";

        [MenuItem("PocketClubs/Build/Build Android Development APK (Gate 1)")]
        public static BuildReport BuildDebugApk()
        {
            string outputPath = Environment.GetEnvironmentVariable("POCKET_CLUBS_APK_PATH");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = DefaultApkOutputPath;
            }

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Ensure Gate 0 / Gate 1 scenes, configs, and Android PlayerSettings are generated and up to date
            ProjectBootstrap.GenerateAllAssetsAndScenes();

            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = true;
            EditorUserBuildSettings.allowDebugging = true;

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[]
                {
                    ProjectBootstrap.BootScenePath,
                    ProjectBootstrap.TrainingScenePath
                },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            };

            Debug.Log($"[PocketClubs] Starting Android Development APK build -> {outputPath}");
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            Debug.Log(
                $"[PocketClubs] Build result: {summary.result} | " +
                $"Duration: {summary.totalTime.TotalSeconds:F1}s | " +
                $"Size: {summary.totalSize} bytes | " +
                $"Errors: {summary.totalErrors} | Warnings: {summary.totalWarnings}");

            return report;
        }

        public static void BuildDebugApkFromCommandLine()
        {
            try
            {
                BuildReport report = BuildDebugApk();
                bool ok = report.summary.result == BuildResult.Succeeded &&
                          File.Exists(report.summary.outputPath) &&
                          new FileInfo(report.summary.outputPath).Length > 0;

                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(ok ? 0 : 1);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PocketClubs] Android APK build failed with exception: {ex}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                throw;
            }
        }
    }
}
#endif
