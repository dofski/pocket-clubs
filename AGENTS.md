# AGENTS.md — Pocket Clubs Engineering Rules

## 1. Mission & Scope Discipline
- **Working Title**: Pocket Clubs (original small-sided Android football game in Unity).
- **No Proprietary IP**: Never copy EA SPORTS FC branding, assets, menus, likenesses, commentary, or proprietary systems.
- **Incremental Gates**:
  - **Gate 0**: Reproducible Unity 6 LTS foundation.
  - **Gate 1**: Playable offline Android training slice.
  - **Gate 2**: FishNet authoritative networking risk spike (headless Linux server + 2 independent clients).
  - **Gate 3**: Complete 4v4 small match (3 outfield + 1 AI GK per team, bot filling, restarts).
  - **Gate 4**: Supabase Auth & Postgres persistence (characters, clubs, match history, bounded progression).
  - **Gate 5**: Real-device profiling, quality tiers, hardening, and release candidate.
- Complete and verify the active milestone before adding scope from later milestones.

## 2. Architecture & Code Standards
- **Separation of Concerns**:
  - Hardware/touch input is translated into `PlayerCommandIntent` (`PocketClubs.Input`).
  - Core movement (`PlayerMovementMotor`), ball state (`BallSimulation`), actions (`PassingService`, `ShotChargeController`, `TackleService`), goal detection (`GoalAndBoundaryDetector`), and match flow (`MatchStateController`) are pure C# classes testable in EditMode without scene dependencies.
  - MonoBehaviour actors (`FootballerActor`, `BallActor`, `StylizedPitchBuilder`, `TrainingSliceCoordinator`) bind simulation state to Unity transforms and visuals.
- **Assembly Boundaries**:
  - `PocketClubs.Runtime` (`Assets/PocketClubs/Runtime/`) — Runtime player code.
  - `PocketClubs.Editor` (`Assets/PocketClubs/Editor/`) — Editor-only bootstrap and build pipelines (`includePlatforms: ["Editor"]`).
  - `PocketClubs.Tests.EditMode` & `PocketClubs.Tests.PlayMode` — NUnit / Unity Test Framework suites.
- **Performance Discipline**:
  - Avoid steady-state managed heap allocations in per-frame movement, ball physics, camera, and HUD update paths.
  - Target 60 FPS on mid-range Android devices with a 30 FPS low-quality battery-saver tier.

## 3. Canonical Commands
- Check toolchain prerequisites:
  ```bash
  ./BuildScripts/check_environment.sh
  ```
- Generate ScriptableObject configs, prefabs, and scenes (`Boot.unity`, `Training.unity`):
  ```bash
  ./BuildScripts/generate_project_assets.sh
  ```
- Run EditMode and PlayMode tests:
  ```bash
  ./BuildScripts/run_unity_tests.sh
  ```
- Build Android Development APK (`Builds/Android/PocketClubs-Gate1-Debug.apk`):
  ```bash
  ./BuildScripts/build_android_apk.sh
  ```
- Install and smoke-test on a connected Android device:
  ```bash
  ./BuildScripts/install_and_smoke_test_android.sh
  ```

## 4. Verification Honesty
- Report every check as **PASS**, **FAIL**, **BLOCKED**, or **NOT RUN** with exact command and evidence location.
- Never use grep checks or an unrelated `.NET` build as proof of Unity compilation or runtime verification.
- Never fabricate complex scene YAML or claim an APK/device test succeeded when Unity or ADB is unavailable.
