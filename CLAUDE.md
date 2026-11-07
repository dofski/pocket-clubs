# CLAUDE.md

Canonical engineering rules, architecture boundaries, and milestone gates are defined in [AGENTS.md](./AGENTS.md) and [README.md](./README.md).

## Quick Commands

```bash
# 1. Inspect local Unity 6 LTS + Android SDK/NDK/JDK/ADB prerequisites
./BuildScripts/check_environment.sh

# 2. Generate Data assets, Prefabs, and Scenes (Boot.unity, Training.unity) via Unity batchmode
./BuildScripts/generate_project_assets.sh

# 3. Execute EditMode and PlayMode automated tests
./BuildScripts/run_unity_tests.sh

# 4. Build debug-signed Android development APK
./BuildScripts/build_android_apk.sh

# 5. Install APK on connected Android device and capture smoke-test evidence
./BuildScripts/install_and_smoke_test_android.sh
```

## Key Documentation
- Technical decisions & trade-offs: `docs/DECISIONS.md`
- Milestone progress & next actions: `docs/PROGRESS.md`
- Verification status & test evidence: `docs/TEST_RESULTS.md`
- Asset & package provenance: `docs/ASSET_LICENSES.md`
