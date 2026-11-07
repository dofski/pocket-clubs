# Pocket Clubs — Progression-Ready Character Customization Plan

## 1. Objective & Scope

This specification defines how **Pocket Clubs** transitions from Gate 1's procedural capsule footballer (`FootballerActor`) to a stylized low-poly 3D humanoid pipeline that supports:

1. **Persistent Character Identity**: Name, body silhouette, skin/eye palette, modular hairstyle, and preferred role (`Playmaker`, `Finisher`, `Defender`).
2. **Club Visual Identity**: Primary, secondary, and trim kit colors plus badge patterns applied via zero-allocation `MaterialPropertyBlock` updates.
3. **Bounded Role Progression**: Modest, non-monetized attribute trade-offs that map cleanly to existing `MovementTuningConfig` and `ActionTuningConfig` parameters and visually drive Unity Mecanim blend trees without breaking authoritative server validation.

---

## 2. Canonical Free Asset Stack & Provenance

| Layer | Asset Package | License | Role in Pocket Clubs |
| :--- | :--- | :--- | :--- |
| **Base Humanoid & Hair** | [Quaternius Universal Base Characters](https://quaternius.com/packs/universalbasecharacters.html) | CC0 1.0 (Public Domain) | 6 humanoid-rigged base bodies across 3 silhouettes (`Teen`, `Regular`, `Superhero`, male & female) + 20 modular hairstyles on a unified 65-joint skeleton (~13k tris). |
| **Modular Kit Pieces** | [Quaternius Modular Character Outfits](https://quaternius.com/packs/modularcharacteroutfitsfantasy.html) | CC0 1.0 (Public Domain) | 62 modular skinned parts sharing the identical 65-joint bind pose; athletic tunic/shorts/boots subsets serve as base kit geometry. |
| **Core Locomotion Clips** | [Quaternius Universal Animation Library](https://quaternius.com/packs/universalanimationlibrary.html) | CC0 1.0 (Public Domain) | 250+ native clips (`Idle`, `Walk`, `Jog_Fwd_Loop`, `Sprint`, `Celebration`) authored on the exact same 65-joint skeleton with zero retargeting drift. |
| **Football Action Clips** | [Adobe Mixamo](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html) | Free Royalty-Free (No raw redistribution) | `Soccer Pass`, `Soccer Kick`, `Soccer Tackle`, `Goalkeeper Idle`, and `Goalkeeper Dive` imported as Unity `Humanoid` clips retargeted onto the Quaternius Avatar. |

---

## 3. Visual Customization Architecture (`CharacterAppearanceDto`)

All meshes in the Quaternius Universal family bind to the same 65-bone hierarchy (`root` -> `pelvis` -> `spine` -> `head` and mirrored limbs), allowing runtime equipment and hair swapping without instantiating separate skeletons.

### 3.1 Customizable Slots

- **Body Silhouette (`BodyTypeIndex`: 0–5)**:
  - `0 / 1`: `Teen_Male` / `Teen_Female` (Agile silhouette — default for `Playmaker`).
  - `2 / 3`: `Regular_Male` / `Regular_Female` (Balanced silhouette — default for `Finisher`).
  - `4 / 5`: `Superhero_Male` / `Superhero_Female` (Broad-shouldered athletic silhouette — default for `Defender`).
- **Modular Hairstyle (`HairMeshId`: 0–20)**:
  - `0` = shaved/bald; `1–20` = Quaternius modular hair meshes parented to the `head` bone transform.
- **Skin & Eye Palette (`SkinToneIndex`: 0–7, `EyeColorIndex`: 0–5)**:
  - Applied via `MaterialPropertyBlock` (`_BaseColor`) on a single shared URP Simple Lit material to preserve SRP Batcher / GPU instancing compatibility on Android.
- **Kit & Club Styling (`ClubKitPaletteDto`)**:
  - Controlled by the match team assignment (`TeamSide.Home` vs `TeamSide.Away`) or the player's Club roster settings.
  - Uses non-color-only pattern differentiation (e.g., `Solid`, `ChevronStripe`, `VerticalHalves`, `Hoops`) so teams remain distinguishable for color-blind players.

### 3.2 Serializable Data Contract

```csharp
using System;

namespace PocketClubs.Core
{
    public enum PlayerRole : byte
    {
        Playmaker = 0,
        Finisher = 1,
        Defender = 2
    }

    public enum KitPatternType : byte
    {
        ChevronStripe = 0,
        VerticalHalves = 1,
        HorizontalBand = 2,
        SolidContrastTrim = 3
    }

    [Serializable]
    public struct CharacterAppearanceDto
    {
        public int schemaVersion;          // Current: 1
        public string displayName;         // Validated 3-16 printable chars
        public byte bodyTypeIndex;         // 0..5 (Teen/Regular/Superhero M/F)
        public byte hairMeshId;            // 0..20 (Quaternius modular hair)
        public byte skinToneIndex;         // 0..7
        public byte hairColorIndex;        // 0..9
        public byte bootAccentIndex;       // 0..5 (Unlocked via progression)
    }

    [Serializable]
    public struct CharacterProgressionDto
    {
        public int schemaVersion;          // Current: 1
        public PlayerRole preferredRole;
        public int careerMatchesPlayed;
        public int careerXp;               // Server-awarded in online matches
        public byte roleTier;              // 0..5 (Bounded progression cap)
        public byte agilityPoints;         // 0..3
        public byte techniquePoints;       // 0..3
        public byte physicalPoints;        // 0..3
    }
}
```

---

## 4. Bounded Role Progression & Gameplay Tuning Mapping

Per Section 11 of the Pocket Clubs engineering specification, progression uses **bounded trade-offs** with **no unbounded stat inflation** and **no monetization**. Every character starts with a competitive baseline (`RoleTier 0`), and advancing through `RoleTier 1–5` grants at most **6 total specialization points** (`max 3 per axis`) that shift parameters within a strict **±8% envelope** around `MovementTuningConfig` and `ActionTuningConfig`.

### 4.1 Role Archetypes & Trade-Off Envelopes

| Role | Primary Silhouette | Core Strength (+Up to 8%) | Trade-Off (-4% to 0%) | Mapped Config Fields (`GameplayTuningConfigs.cs`) |
| :--- | :--- | :--- | :--- | :--- |
| **Playmaker** | `Teen` (Compact / Quick Turn) | Faster turn rate, tighter dribble follow, wider pass assist cone, faster stamina regen. | Slightly lower peak shot speed and shorter tackle dislodge impulse. | `turnSpeedDegreesPerSecond` (`540 -> 583`), `dribbleSpeedMultiplier` (`0.92 -> 0.96`), `passAssistMaxAngleDegrees` (`38° -> 42°`), `staminaRegenPerSecond` (`14.0 -> 15.1`). |
| **Finisher** | `Regular` (Balanced Striker) | Faster shot charge wind-up, higher max shot speed, higher sprint burst speed. | Slightly longer tackle cooldown and baseline stamina regen. | `maxShotChargeSeconds` (`0.85s -> 0.78s`), `maxShotSpeed` (`25.5 -> 27.2`), `sprintSpeedMultiplier` (`1.32 -> 1.37`). |
| **Defender** | `Superhero` (Broad Frame) | Extended standing tackle reach, wider tackle angle tolerance, stronger dislodge impulse, lower sprint drain. | Slightly slower dribble speed multiplier and longer shot charge time. | `tackleReachDistance` (`1.45m -> 1.56m`), `tackleMaxAngleDegrees` (`65° -> 70°`), `tackleCooldownSeconds` (`0.95s -> 0.88s`), `sprintStaminaDrainPerSecond` (`22.0 -> 20.4`). |

### 4.2 Authoritative Server Validation Rules

- Clients never transmit raw speed or reach floats; they submit only their authenticated `CharacterId` (online) or validated `CharacterProgressionDto` (offline).
- Both the client prediction motor (`PlayerMovementMotor`) and the headless Linux server compute effective tuning parameters deterministically from `(BaseConfig, PlayerRole, RoleTier, AllocatedPoints)` clamped by server-enforced min/max bounds.

---

## 5. Animation State Machine & Progression Coupling

To preserve deterministic simulation and prevent animation glitches from affecting physics, **Root Motion remains disabled (`applyRootMotion = false`)**. `PlayerMovementMotor` and `FootballActions` own world position and velocity, while `FootballerVisualAnimator` reads simulation state every frame to drive Mecanim parameters.

### 5.1 Mecanim Layers & Blend Trees

1. **Base Layer (Full-Body Locomotion — Quaternius Universal Animation Library)**:
   - 1D Blend Tree driven by `PlanarSpeedNormalized` (`0.0` = `Idle`, `0.35` = `Walk`, `0.75` = `Jog_Fwd_Loop`, `1.0` = `Sprint`).
   - Animation playback speed scales subtly with the character's effective speed multiplier (`0.95x–1.08x`) so a `Playmaker` with high agility visibly takes quicker steps without foot sliding.
2. **Action Override Layer (Lower/Full-Body — Mixamo Retargeted Humanoid Clips)**:
   - `PassTrigger`: Plays `Soccer Pass` (`0.35s` blend-in/out).
   - `ShotChargeNormalized` (`0.0 -> 1.0`): Scrubs/holds the wind-up phase of `Soccer Kick` proportional to `FootballActions.CurrentShotChargeNormalized`, then releases into the follow-through upon `ShotReleaseTrigger`. A `Finisher` with reduced `maxShotChargeSeconds` visibly completes their backswing faster.
   - `TackleTrigger`: Plays `Soccer Tackle` lunge; playback recovery scales with effective `tackleCooldownSeconds`.

---

## 6. Cosmetic & Milestone Unlock Ladder (Tiers 0–5)

Offline training remains unlocked and unmonetized; online club matches award verified server XP (`+100 XP` match completion, `+25 XP` team win/draw bonus) toward 5 bounded tiers:

| Tier | Cumulative XP | Gameplay Specialization Points | Visual & Club Customization Unlocks |
| :--- | :--- | :--- | :--- |
| **Tier 0 (Rookie)** | `0 XP` | `1 Point` (Role starter trait) | All 6 base silhouettes, 8 starter hairstyles (`#0–#7`), all skin/eye tones, `ChevronStripe` & `SolidContrastTrim` kit patterns. |
| **Tier 1 (Club Debut)** | `300 XP` | `2 Points` | Hairstyles `#8–#11`, `VerticalHalves` kit pattern, Boot Accent `#1 (Volt Yellow)`. |
| **Tier 2 (Regular)** | `800 XP` | `3 Points` | Hairstyles `#12–#15`, `HorizontalBand` kit pattern, Boot Accent `#2 (Crimson)`. |
| **Tier 3 (First Team)** | `1,600 XP` | `4 Points` | Hairstyles `#16–#18`, Captain armband accessory mesh, Boot Accent `#3 (Cyan)`. |
| **Tier 4 (Key Player)** | `2,800 XP` | `5 Points` | Hairstyles `#19–#20`, Goal celebration clip `#2`, Boot Accent `#4 (Gold Trim)`. |
| **Tier 5 (Club Icon — Cap)** | `4,500 XP` | `6 Points` (Hard Cap) | Club Icon badge border on HUD nameplate, Boot Accent `#5 (Iridescent White)`. |

---

## 7. Incremental Implementation Roadmap

1. **Gate 1.5 (Visual Upgrade on MacBook Air M1)**:
   - Import `UniversalBaseCharacters[Standard].zip` (`FBX`) and `UniversalAnimationLibrary.zip` into `Assets/PocketClubs/Art/Quaternius/` and record SHA-256 + CC0 license in `docs/ASSET_LICENSES.md`.
   - Add `CharacterAppearanceDto` and `CharacterProgressionDto` to `PocketClubs.Runtime` and update `FootballerActor` so it binds either the procedural graybox fallback or the rigged Quaternius prefab when assigned.
2. **Gate 3 (4v4 Small-Sided Match Integration)**:
   - Assign distinct `PlayerRole` builds (`Playmaker`, `Finisher`, `Defender`) and non-overlapping hairstyles/silhouettes to the 5 outfield bots so teammates and opponents are immediately distinguishable on pitch.
3. **Gate 4 (Supabase Persistence & Club Wardrobe)**:
   - Persist `characters` and `progression_events` in Supabase Postgres with Row Level Security ensuring clients can update `appearance_json` and role point allocations within unlocked caps, while only the dedicated match server can insert `progression_events` (`careerXp`).
