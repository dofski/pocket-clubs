# Project Progress (`docs/PROGRESS.md`)

## Completed Milestones
- **None fully verified in Unity runtime yet** (Source and reproducible bootstrap for **Gate 0** and **Gate 1** are implemented in repository; compilation, test execution, APK build, and device verification are **BLOCKED** by missing Unity Editor and Android SDK/NDK/JDK/ADB in the current Linux sandbox).

## Current Milestone
- **Gate 0 (Reproducible Foundation) & Gate 1 (Playable Android Training Slice)**
  - [x] Inspect workspace and toolchain environment.
  - [x] Pin Unity 6 LTS (`6000.0.24f1`) and compatible UPM packages (`Packages/manifest.json`, `Packages/packages-lock.json`).
  - [x] Create assembly definitions (`PocketClubs.Runtime`, `PocketClubs.Editor`, `PocketClubs.Tests.EditMode`, `PocketClubs.Tests.PlayMode`) and deterministic `.meta` files.
  - [x] Implement hardware-independent `PlayerCommandIntent` and `TouchAndHardwareIntentSource` with focus/pause cancellation.
  - [x] Implement `PlayerMovementMotor` (camera-relative planar movement, diagonal normalization, acceleration/deceleration, sprint stamina drain, exhaustion lockout, and delayed regeneration).
  - [x] Implement `BallSimulation` (`Free`, `ControlledDribble`, `PassOrShotFlight`, `RestartReset`), bounded pickup radius, obstacle occlusion checks, simultaneous contest resolution, and high-speed shot sub-stepping.
  - [x] Implement `PassingService` (directional pass + angle/distance teammate assist + solo training directional fallback), `ShotChargeController` (hold-to-charge, clamped power, safe cancellation), and `TackleService` (reach, facing, cooldown, and possession validation).
  - [x] Implement `GoalAndBoundaryDetector` (full-sphere crossing inside goal opening, duplicate-score latching, reset-teleport suppression, and simplified out-of-play restarts).
  - [x] Implement `BroadcastMatchCamera` (player framing, passing look-ahead, bounded ball offset, pitch boundary clamping, zoom, goal shake, and off-screen ball indicator calculation).
  - [x] Implement `VirtualJoystickZone`, `TouchActionButton`, `SafeAreaPanel`, and `TrainingHudAndMenusView` (Launch screen, Training HUD, Pause modal, Settings modal).
  - [x] Implement `LocalSettingsRepository` (`GameSettingsDto` v1 with corruption recovery).
  - [x] Implement `ProjectBootstrap` and `AndroidBuildPipeline` Editor tools + `BuildScripts/*.sh`.
  - [x] Write EditMode and PlayMode automated tests (`MovementAndInputTests`, `BallAndActionsTests`, `GoalDetectionTests`, `MatchAndSettingsTests`, `TrainingSliceSmokeTests`).
  - [ ] **BLOCKED**: Execute `BuildScripts/generate_project_assets.sh`, `BuildScripts/run_unity_tests.sh`, `BuildScripts/build_android_apk.sh`, and `BuildScripts/install_and_smoke_test_android.sh` on a machine with Unity `6000.0.24f1`, Android Build Support, and an ADB device.

## Next Milestone (After Gate 0 & Gate 1 Unblocked and Verified)
- **Gate 2: Networking Risk Spike**
  - Verify FishNet package version, license, Android IL2CPP compatibility, and headless Linux server build support.
  - Implement dedicated headless server + two independent client processes with authoritative player movement, ball ownership validation, single authoritative goal emission, and client prediction/reconciliation under 0 ms, 100 ms, and 200 ms RTT / 2% packet loss profiles.

## Active Blockers
1. **Missing Unity Editor (`6000.0.24f1`)**: No Unity Editor installation or license exists on the current Linux sandbox (`Ubuntu 26.04.1 LTS x86_64`).
2. **Missing Android Build Toolchain**: `ANDROID_SDK_ROOT`, `ANDROID_NDK_ROOT`, OpenJDK 17 (`java`/`javac`), and `adb` are not installed in the sandbox.
3. **No Connected Android Device**: No physical or emulated Android device is attached to the sandbox over ADB.

## Exact Next Actions (On Host Machine with Unity + Android Toolchain)
```bash
export UNITY_EDITOR_PATH="/path/to/Unity/Hub/Editor/6000.0.24f1/Editor/Unity"
./BuildScripts/check_environment.sh
./BuildScripts/generate_project_assets.sh
./BuildScripts/run_unity_tests.sh
./BuildScripts/build_android_apk.sh
./BuildScripts/install_and_smoke_test_android.sh
```
