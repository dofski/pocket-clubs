# Pocket Clubs

Original small-sided Android football game built with Unity 6 LTS (`6000.0.24f1`).
*Note: "Pocket Clubs" is an internal working placeholder title, not a legally cleared trademark. This project is an independent original game and contains no EA SPORTS FC branding, code, assets, or server dependencies.*

## Current Status: Gate 0 & Gate 1 Source and Bootstrap Handoff

- **Gate 0 (Reproducible Foundation)**: Implemented source, assembly definitions (`PocketClubs.Runtime`, `PocketClubs.Editor`, `PocketClubs.Tests.EditMode`, `PocketClubs.Tests.PlayMode`), pinned Unity 6 LTS manifest (`Packages/manifest.json`), Input System action map (`Assets/PocketClubs/Data/PocketClubsInputActions.inputactions`), programmatic scene/prefab/config generator (`PocketClubs.Editor.ProjectBootstrap`), and reproducible CLI build scripts (`BuildScripts/`).
- **Gate 1 (Playable Android Training Slice)**: Implemented offline single-player training loop:
  - Launch screen (`PLAY TRAINING`, `SETTINGS`, `EXIT` — no inert placeholder buttons).
  - Stylized low-poly 3D pitch with mowed grass stripes, touchlines, penalty boxes, center circle, and North/South 3D goals.
  - Single human-controlled outfield footballer (`#10 Playmaker`) with non-color-only kit identification (Home Azure Vertical Stripe vs Away Coral Horizontal Band), ground ring, and overhead chevron.
  - Camera-relative movement with diagonal normalization, configurable acceleration/deceleration, sprint stamina drain, exhaustion lockout threshold, and delayed stamina regeneration.
  - Authoritative ball state machine (`Free`, `ControlledDribble`, `PassOrShotFlight`, `RestartReset`) with bounded pickup radius, obstacle occlusion checks, deterministic simultaneous contest resolution, ground contact shadow, and sub-stepped high-speed shot integration.
  - Directional passing with angle/distance teammate assistance and guaranteed directional fallback for solo training.
  - Hold-to-charge, release-to-shoot controller with clamped power, bounded goal-opening aim assistance, and safe input cancellation on lost focus/touch.
  - Standing tackle with cooldown, reach, facing angle, and possession-state validation.
  - Full-sphere goal-line opening detector preventing partial crossings, outside-post crossings, above-crossbar crossings, duplicate scoring callbacks, and reset-teleport scoring.
  - Multitouch virtual joystick (left thumb) and independent action buttons (right thumb: `PASS`, `HOLD SHOOT`, `TACKLE`, `HOLD SPRINT`) with strict `pointerId` ownership, `Screen.safeArea` fitting, and off-screen ball edge indicator.
  - Local guest settings persistence (`LocalSettingsRepository` / `GameSettingsDto`) with schema versioning and corruption recovery.
- **Verification Blocker**: Unity Editor (`6000.0.24f1`), Android Build Support (SDK/NDK/JDK), and `adb` are not installed in the cloud sandbox environment. Consequently, Unity compilation, EditMode/PlayMode test execution, APK packaging, and physical Android device installation are marked **BLOCKED** until executed on a host with the prerequisites below.

---

## Prerequisites

1. **Unity Editor**: Unity 6 LTS `6000.0.24f1` (changeset `11fa355cd605`) installed via Unity Hub or CLI, with an active Unity license (Personal, Plus, or Pro).
2. **Unity Modules**:
   - **Android Build Support**
   - **Android SDK & NDK Tools** (API Level 29+ minimum; API 34/35 target SDK)
   - **OpenJDK 17** (bundled with Unity Android Build Support)
3. **Android Platform Tools**: `adb` on `PATH` and an authorized Android 10+ (API 29+, ARM64) test device connected over USB or wireless debugging.

---

## Setup & Asset Generation

Because complex Unity scenes and prefabs are generated deterministically via Editor code rather than fragile hand-fabricated YAML, run the bootstrap generator first when opening the repository on a machine with Unity installed:

### Option A: Command Line (Recommended)
```bash
export UNITY_EDITOR_PATH="/path/to/Unity/Hub/Editor/6000.0.24f1/Editor/Unity"
./BuildScripts/check_environment.sh
./BuildScripts/generate_project_assets.sh
```

### Option B: Inside Unity Editor GUI
1. Open the repository root folder in Unity `6000.0.24f1`.
2. In the top menu bar, select **`PocketClubs -> Bootstrap -> Generate Gate 0 & Gate 1 Assets and Scenes`**.
3. Open `Assets/PocketClubs/Scenes/Boot.unity` (or press Play in any scene; `TrainingSliceCoordinator` also self-bootstraps via `[RuntimeInitializeOnLoadMethod]` if launched directly).

---

## Running Automated Tests

```bash
export UNITY_EDITOR_PATH="/path/to/Unity/Hub/Editor/6000.0.24f1/Editor/Unity"
./BuildScripts/run_unity_tests.sh
```
Test XML artifacts are written to:
- `Logs/TestResults-EditMode.xml`
- `Logs/TestResults-PlayMode.xml`

---

## Building and Installing the Android Development APK

```bash
export UNITY_EDITOR_PATH="/path/to/Unity/Hub/Editor/6000.0.24f1/Editor/Unity"
./BuildScripts/build_android_apk.sh
./BuildScripts/install_and_smoke_test_android.sh
```
The debug-signed development APK is output to:
- `Builds/Android/PocketClubs-Gate1-Debug.apk`

---

## Controls (Training Slice)

| Action | Android Touch Controls | Keyboard (Development) | Gamepad (Development) |
| :--- | :--- | :--- | :--- |
| **Move** | Left Virtual Joystick | `W A S D` / Arrow Keys | Left Stick |
| **Sprint** | Hold `SPRINT` (Bottom-Right) | Hold `Left Shift` | Hold Right Trigger (`RT`) |
| **Pass** | Press `PASS` (Bottom-Right) | `J` | South Button (`A` / Cross) |
| **Charge / Release Shot** | Hold & Release `SHOOT` | Hold & Release `Space` | Hold & Release East Button (`B` / Circle) |
| **Standing Tackle** | Press `TACKLE` | `K` | West Button (`X` / Square) |
| **Reset Training** | Top-Right `RESET` Button | `R` | Select / Back |
| **Pause / Back** | Top-Right `PAUSE` / Android Back | `Escape` | Start / Menu |

---

## Troubleshooting & Known Limitations

- **Current Scope**: Only Gate 0 and Gate 1 (single-player offline training slice) are implemented in this increment. Do not expect bots (Gate 3), FishNet multiplayer (Gate 2), or Supabase cloud accounts/clubs (Gate 4) yet.
- **Audio**: Gate 1 uses a silent audio fallback with functional mute state persistence (`AudioListener.volume`) in `LocalSettingsRepository`.
- **Missing Unity Binary (`Exit code: 2`)**: All scripts in `BuildScripts/` return exit code `2` (`[BLOCKED]`) if `UNITY_EDITOR_PATH` or `adb` is missing on the host machine.
