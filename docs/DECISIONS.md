# Technical Decisions (`docs/DECISIONS.md`)

## ADR-001: Pin Unity 6 LTS (`6000.0.24f1`) with URP `17.0.3`, Input System `1.11.1`, and uGUI `2.0.0`
- **Decision**: Pin `m_EditorVersion: 6000.0.24f1 (11fa355cd605)` in `ProjectSettings/ProjectVersion.txt` and verified Unity 6 LTS package versions in `Packages/manifest.json` (`com.unity.render-pipelines.universal@17.0.3`, `com.unity.inputsystem@1.11.1`, `com.unity.ugui@2.0.0`, `com.unity.test-framework@1.4.5`).
- **Rationale**: Unity 6 LTS provides long-term Android ARM64 IL2CPP stability, merges TextMeshPro into `com.unity.ugui@2.0.0`, and includes mobile URP improvements.
- **Trade-off**: Requires Unity 6000.0.x Editor installation rather than older 2022.3 LTS installations.

## ADR-002: Pure C# Simulation Core Separated from MonoBehaviour Presentation
- **Decision**: Implement `PlayerMovementMotor`, `BallSimulation`, `PassingService`, `ShotChargeController`, `TackleService`, `GoalAndBoundaryDetector`, and `MatchStateController` as pure C# classes driven by hardware-independent `PlayerCommandIntent` structs.
- **Rationale**:
  1. Allows deterministic unit testing of movement, stamina, ball possession, shot charge cancellation, tackle validation, and goal-line crossing in EditMode without scene overhead.
  2. Prepares clean simulation boundaries so Gate 2 (FishNet authoritative server + client prediction) and Gate 3 (outfield/GK bots) can submit `PlayerCommandIntent` structs directly without refactoring gameplay math or simulating touch events.
- **Trade-off**: Requires thin MonoBehaviour wrapper classes (`FootballerActor`, `BallActor`, `TrainingSliceCoordinator`) to sync simulation state to Unity transforms.

## ADR-003: Programmatic Scene/Prefab Bootstrap Instead of Hand-Fabricated YAML
- **Decision**: Provide `PocketClubs.Editor.ProjectBootstrap` (`BuildScripts/generate_project_assets.sh`) to generate `Default*.asset` ScriptableObjects, prefabs, and `Boot.unity` / `Training.unity` scenes in Unity Editor, combined with a runtime initialization fallback in `TrainingSliceCoordinator`.
- **Rationale**: Hand-authored Unity scene YAML is brittle and prone to broken fileIDs/GUIDs when created outside the Unity Editor. Programmatic Editor generation plus runtime fallback guarantees zero undocumented Inspector wiring.
- **Trade-off**: Requires running `generate_project_assets.sh` (or the `PocketClubs/Bootstrap` menu item) once when opening the project in Unity Editor before building a player binary.

## ADR-004: Authoritative Planar + Vertical Ball Sub-Stepping Instead of Unconstrained PhysX Rigidbodies
- **Decision**: Use an explicit authoritative `BallSimulation` state machine (`Free`, `ControlledDribble`, `PassOrShotFlight`, `RestartReset`) with automatic high-velocity sub-stepping and analytical goalpost/net/ground collision resolution.
- **Rationale**: Cross-platform PhysX rigidbody simulation is non-deterministic and prone to fast-shot tunneling or awkward physics jitter during close-control dribbling. Explicit states guarantee bounded pickup distance, obstacle occlusion checks, deterministic simultaneous-contest tie-breaking, and tunnel-free goal detection.
- **Trade-off**: Custom collision math is intentionally scoped to flat pitch geometry, cylindrical goalposts, and spherical/cylindrical player bounds rather than arbitrary concave meshes.

## ADR-005: Non-Color-Only Team Identification & Independent Pointer Ownership
- **Decision**: Distinguish kits by both color and geometry (`Home` = Azure Blue with Vertical Center Stripe; `Away` = Coral Orange with Horizontal Chest Band) plus explicit HUD text tags (`[HOME | AZURE V-STRIPE]`). Track `pointerId` independently in `VirtualJoystickZone` and each `TouchActionButton`.
- **Rationale**: Meets accessibility requirements for color-vision deficiency and guarantees simultaneous left-thumb movement and right-thumb pass/shoot/sprint multitouch without pointer cross-talk.
