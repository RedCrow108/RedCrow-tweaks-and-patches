# Geneline Effects Validation — 0.8.0

## Status

Implementation of Geneline stages 1–3 is complete. Runtime validation is
partial because the remaining exhaustive tests were explicitly deferred.

Release version: `0.8.0`

Release commit: the commit containing this document (PR #8 final head).

The implementation baseline through the final code fix is commit `0eef0bf`.

## Implemented scope

- Stage 1: 24 simple stat, need, trait, and dependency effects.
- Stage 2: 28 lighting, aging, curiosity, and conditional ability effects.
- Stage 3: eight autonomous custom effects, including raid-wealth modifiers,
  auras, the hive electro-organ, and segment restoration.
- Gene and trait relationships are recalculated after a Geneline element is
  removed or an older save is loaded.
- Contradictory light combinations are blocked while aligned combinations
  remain available.

The implementation does not add the source genes to pawns and does not change
their xenotype.

## Automated validation

The following checks passed on 2026-07-29:

- `validate-stage1.ps1`
- `validate-stage2.ps1`
- `validate-stage3.ps1`
- XML parsing and static Def checks
- Release build with zero compiler warnings and errors
- .NET metadata and IL decompilation inspection

The release assembly has:

```text
File: RedCrow_Insector_Tweaks/1.5/Assemblies/RedCrow.InsectorTweaks.dll
Assembly version: 0.8.0.0
Configuration: Release / AnyCPU
Target framework: .NET Framework 4.7.2
Size: 29696 bytes
SHA-256: e2a959a4b0de9082bbaafa71b48bff8f1bc98165b748f4966643d3755662f77d
```

`0Harmony.dll` and `Assembly-CSharp.dll` are compile-time references with
`Copy Local = false` and are not distributed in the mod's `Assemblies`
directory.

## In-game observations

The performed smoke tests established the following:

- RimWorld loaded the assembly and installed its Harmony patches without
  Mono metadata loader errors.
- Repeated save/load testing did not reproduce the repaired dangling
  `Gene_*` references.
- Crafting curiosity increased the `Joy` need while matching Crafting XP was
  gained; without the curiosity it did not increase Joy.
- A leg scar was healed by segment restoration.
- The tested light and darkness combinations behaved acceptably after their
  exclusion rules were corrected.
- Other effects exercised during the smoke tests did not show an obvious
  gameplay problem.

## Deferred runtime checks

The following items are intentionally not claimed as verified:

- Healing a crushed `Spine` injury after `Spine` was added to the segment
  restoration whitelist.
- Preventing skill decay for the skill selected by a curiosity evolution.
- The complete acquire/save/load/remove/reacquire cycle for every new
  Geneline element.
- Duplicate prevention and every exclusion combination across all elements.
- Separate game startups with each optional parent mod removed.
- Every conditional Alpha Genes ability with both its parent mod present and
  absent.

Segment restoration selects a random eligible injury and heals one point of
severity every 55,000–65,000 ticks. A `Spine` injury can therefore require
multiple cycles and can be delayed by other eligible injuries.

## Separate known pending work

These items are not part of the three-stage Geneline implementation:

- Crafting and Construction Quality Patch bonuses still require separate
  in-game verification.
- Additional HSK More Content mutations and evolutions do not yet
  participate in the VFE Insectoids 2 core-based unlock system.
