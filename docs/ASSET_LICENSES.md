# Asset & Dependency Provenance (`docs/ASSET_LICENSES.md`)

## 1. Visual, Geometry, and UI Assets
- **Pitch, Markings, Goals, Footballer Character, Ball, Shadow, and UI Layout**:
  - **Origin**: 100% original procedural low-poly geometry and uGUI layouts authored directly in this repository (`StylizedPitchBuilder.cs`, `FootballerActor.cs`, `BallActor.cs`, `TrainingHudAndMenusView.cs`).
  - **License**: Proprietary / Original work created for the Pocket Clubs project. Contains zero third-party meshes, textures, fonts, audio clips, or EA SPORTS FC trademarks/likenesses.
- **Audio**:
  - **Origin**: Silent fallback (`AudioListener.volume` controlled by `LocalSettingsRepository`). No third-party audio files are bundled in Gate 1.

## 2. Unity Package Manager (UPM) Dependencies (`Packages/manifest.json`)
All declared packages are official Unity Registry packages governed by the [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license) / Unity Editor Terms of Service:

| Package Identifier | Pinned Version | Purpose |
| :--- | :--- | :--- |
| `com.unity.inputsystem` | `1.11.1` | Touch, keyboard, and gamepad input handling |
| `com.unity.render-pipelines.universal` | `17.0.3` | Mobile Universal Render Pipeline (URP) |
| `com.unity.render-pipelines.core` | `17.0.3` | Core SRP runtime support |
| `com.unity.shadergraph` | `17.0.3` | URP shader support |
| `com.unity.ugui` | `2.0.0` | Unity UI & TextMeshPro runtime/editor components |
| `com.unity.test-framework` | `1.4.5` | NUnit EditMode & PlayMode automated test runner |
| `com.unity.ide.rider` | `3.0.31` | JetBrains Rider IDE integration |
| `com.unity.ide.visualstudio` | `2.0.22` | Visual Studio IDE integration |
| `com.unity.collab-proxy` | `2.5.2` | Version Control package |
