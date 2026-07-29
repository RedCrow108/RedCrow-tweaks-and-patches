# Changelog

## 0.8.0

Release date: 2026-07-29

### Fixed

- Gene overrides and trait suppression are recalculated after Geneline
  changes and save loading, preventing dangling `Gene_*` references without
  deleting existing traits or active genes.
- Ability grants are restored safely when another active gene still provides
  the same ability.
- Segment restoration now creates a controlled injury directly under
  HSK/Combat Extended instead of applying combat damage that could destroy
  the restored part again.
- Segment restoration now treats the vanilla `Spine` body part as a valid
  structural segment, allowing crushed-spine injuries to heal.
- Contradictory light combinations are blocked: UV sensitivity conflicts
  with light stride and solar nutrition, while twilight stride conflicts
  with solar nutrition. Aligned combinations remain available.
- Optional source-icon detection uses the installed mod names expected by
  RimWorld 1.5 `PatchOperationFindMod`.
- Compatibility patches append exclusion tags to existing lists and create
  the list only when absent, avoiding duplicate XML fields.

### Added

- Added 24 stage-1 Geneline mutations and evolutions covering stats, needs,
  traits, and dependency effects.
- Added 28 stage-2 Geneline mutations and evolutions covering sunlight,
  lighting, aging, curiosity, and conditional Alpha Genes abilities.
- Added 12 mutually exclusive curiosity instincts with skill-specific
  recreation and skill-loss protection.
- Added autonomous sunlight, antenna, light-striding, and photosynthesis
  effects with conditional source icons.
- Added eight autonomous stage-3 Geneline effects: alien hive visage,
  pheromone unity, unconstrained carapace, hive electro-organ, two raid
  presence tiers, matriarch calm aura, and segment restoration.
- Added source-aware mood aura memories, apparel movement-penalty removal,
  pawn-specific raid wealth multipliers, and persisted limb-regeneration
  timing.
- Reused Alpha Genes and VRE Hussar art only through conditional
  compatibility patches; all stage-3 mechanics remain available without
  those source mods.
- Added a local fallback gene icon and conditionally reused compatible source
  icons only when their parent mods are loaded.
- Guarded optional definitions and foreign resources with `MayRequire` and
  `PatchOperationFindMod`, so optional parent mods are not hard dependencies.
- Added the open Natural Symbiosis evolution, which grants the HMC Touch of Nature trait when HSK More Content is active.
- Natural Symbiosis does not yet participate in the VFE Insectoids 2 core-based evolution unlock system.
- Added the open Strong Back evolution.
- Added the open Ambidexterity evolution.
- Added separate Cleaner and Jack of All Trades mutations.
- Cleaner and Jack of All Trades are mutually exclusive.
- Added melee and ranged combat enhancement mutations.
- Melee and ranged combat enhancement mutations are mutually exclusive.
- New mutations and evolutions do not yet use the VFE Insectoids 2 core-based unlock system.

### Validation status

- Implementation and static validation are complete for Geneline stages 1–3.
- In-game smoke testing confirmed save/load stability, Crafting curiosity
  recreation gain, leg-scar healing, and the tested light interactions.
- Runtime verification of curiosity skill-decay protection remains pending.
- Healing of `Spine` injuries was implemented after the final smoke test and
  is intentionally not verified in game.
- The exhaustive per-element and optional-mod startup matrices were not run.
- See [GENELINE_VALIDATION.md](GENELINE_VALIDATION.md) for the exact scope.

## 0.7.4

### Fixed

- Rebuilt RedCrow.InsectorTweaks.dll using a real C# compiler.
- Fixed Mono loading errors from earlier DLL builds.
- Applied the quality postfix with Harmony Priority.Last.

### Added

- Diagnostic Player.log messages for patch installation and quality changes.

### Verified

- Artistic specialization guarantees Legendary artistic products.
- Confirmed with two small sculptures in RimWorld.
- Observed quality changes: Good -> Legendary and Excellent -> Legendary.

### Pending verification

- Crafting specialization is intended to add +1 quality tier.
- Construction specialization is intended to add +1 quality tier.
- These two effects still require separate in-game verification.
