# Verification & Test Results (`docs/TEST_RESULTS.md`)

Date: 2026-11-07
Environment: Linux `space-sandbox` (`6.1.155 x86_64`, Ubuntu 26.04.1 LTS)

## Summary Table

| Check | Status | Command | Evidence / Reason |
| :--- | :--- | :--- | :--- |
| **1. Environment & Toolchain Inspection** | **PASS** (Inspection completed; missing prerequisites identified) | `./BuildScripts/check_environment.sh` | Confirmed OS (`Linux 6.1.155 x86_64`), Git `2.53.0`, and absence of Unity Editor, Android SDK/NDK, JDK, and `adb`. |
| **2. Editor Asset & Scene Bootstrap Generation** | **BLOCKED** | `./BuildScripts/generate_project_assets.sh` | Exit code `2`: `[BLOCKED] Unity Editor 6000.0.24f1 is not installed in this environment.` |
| **3. Unity C# Compilation (`PocketClubs.Runtime`, `Editor`, `Tests`)** | **BLOCKED** | `./BuildScripts/run_unity_tests.sh` | Exit code `2`: Unity Editor compiler unavailable in sandbox. Per engineering rules, no unrelated `.NET` build or text search is used to fake Unity compilation. |
| **4. EditMode Unit Tests (`Movement`, `Ball/Actions`, `Goals`, `Match/Settings`)** | **BLOCKED** | `./BuildScripts/run_unity_tests.sh` | Test suites authored in `Assets/PocketClubs/Tests/EditMode/`; execution blocked on Unity Test Runner binary. |
| **5. PlayMode Smoke Tests (`TrainingSliceSmokeTests`)** | **BLOCKED** | `./BuildScripts/run_unity_tests.sh` | Test suite authored in `Assets/PocketClubs/Tests/PlayMode/TrainingSliceSmokeTests.cs`; execution blocked on Unity Editor. |
| **6. Android Development APK Build** | **BLOCKED** | `./BuildScripts/build_android_apk.sh` | Exit code `2`: Requires Unity Editor `6000.0.24f1` + Android Build Support (SDK/NDK/OpenJDK 17). No APK artifact was produced in this sandbox run. |
| **7. Real-Device Android Install & Smoke Test** | **BLOCKED** | `./BuildScripts/install_and_smoke_test_android.sh` | Exit code `2`: `[BLOCKED] adb is not installed in this environment.` |

## Authored Test Coverage Ready for Unity Test Runner

1. `Assets/PocketClubs/Tests/EditMode/MovementAndInputTests.cs`:
   - `DiagonalInput_IsNormalizedToUnitMagnitude`
   - `InvalidNaNOrInfiniteInput_SanitizesToZero`
   - `CameraRelativeMove_RotatesCorrectlyWithCameraYaw`
   - `MovementMotor_EnforcesWalkSprintAndDribbleSpeedBounds`
   - `InputCancellation_ImmediatelyHaltsSprintAndZeroesVelocity`
   - `Stamina_DrainsOnSprint_EnforcesExhaustionThreshold_AndRegeneratesAfterDelay`
2. `Assets/PocketClubs/Tests/EditMode/BallAndActionsTests.cs`:
   - `PassingService_UsesDirectionalFallbackWhenNoTeammateExists_AndClampsPower`
   - `PassingService_SelectsEligibleTeammateWithinAngleAndDistance`
   - `ShotChargeController_ClampsChargeAndPower_AndSafelyCancelsInterruptedCharge`
   - `TackleService_ValidatesReachFacingCooldownAndPossessionState`
   - `BallPossession_RejectsPickupThroughObstacles_AndResolvesSimultaneousContestDeterministically`
3. `Assets/PocketClubs/Tests/EditMode/GoalDetectionTests.cs`:
   - `FullCrossingWithinGoalOpening_CountsSingleGoal_AndPreventsDuplicateCallbacks`
   - `PartialCrossingOnGoalLine_DoesNotCountAsGoal`
   - `CrossingOutsideGoalPosts_DoesNotCountAsGoal`
   - `CrossingAboveCrossbar_DoesNotCountAsGoal`
   - `ResetTeleport_DoesNotTriggerGoal`
   - `FastShotSubStepping_CrossesGoalLineAndSettlesInNetWithoutTunneling`
4. `Assets/PocketClubs/Tests/EditMode/MatchAndSettingsTests.cs`:
   - `MatchStateController_TransitionsCleanly_AndEmitsSingleFinalResultOnTimerCompletion`
   - `PauseMenu_FreezesOfflineSimulation_ButDoesNotPauseOnlineMatchTimer`
   - `OutOfPlayDetector_ClassifiesThrowInCornerAndGoalKickCorrectly`
   - `LocalSettingsRepository_RoundTripsValidData_AndRecoversFromCorruptedJson`
5. `Assets/PocketClubs/Tests/PlayMode/TrainingSliceSmokeTests.cs`:
   - `AcceptanceFlow_LaunchToTraining_MoveShootScoreResetAndPlayAgain`
   - `PauseResumeAndSettingsPersistence_BehaveCorrectly`
