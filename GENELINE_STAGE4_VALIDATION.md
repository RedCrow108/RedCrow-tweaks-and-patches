# Geneline Follow-up Validation

## Scope

This feature branch adds three autonomous Geneline elements after release
`0.8.0`:

- `RC_Mutation_ColdHiveLogic` — mutation tier 1;
- `RC_Evolution_HiveSynapticNode` — evolution tier 3;
- `RC_Evolution_CoagulatingSecretion` — evolution tier 4.

They are not part of the released `0.8.0` count until this branch is reviewed
and merged.

## Installed-mod audit

The installed HSK VRE Insector build does not represent insect jelly as a
`NeedDef`. Its exact resource is:

```text
GeneDef: VRE_InsectJellyDependency
Runtime class: VanillaRacesExpandedInsector.Gene_Resource_InsectJelly
Base class used by this patch: RimWorld.Gene_Resource
Internal range: 0.0–1.0
Displayed range: 0–100
Ability cost: 0.20 internal / 20 displayed points
```

The source definition was confirmed in the installed mod at:

```text
HSK-VRE-Insector/1.5/Defs/GeneDefs/GeneDefs_Needs.xml
```

The implementation locates the active resource by its exact `GeneDef` and
uses only the stock `Gene_Resource` API. It does not reference the VRE
Insector assembly, create a new need, or consume a royal-jelly item.

The target blood predicate resolves the actual blood `ThingDef`:

1. active gene extensions are inspected for `customBloodThingDef`;
2. otherwise the pawn's `RaceProps.BloodDef` is used;
3. the resolved object is compared with the loaded `Filth_BloodInsect` and
   `VRE_Filth_BugBlood` definitions.

The pawn's race name or race `defName` is not used as the blood test.

`Pragmatist` degree 0 was confirmed in the installed Core_SK definition:

```text
Core_SK/Defs/TraitDefs/Expanded Traits.xml
```

Cold Hive Logic has no HSK More Content condition. Its forced trait uses the
existing gene-owned `sourceGene` lifecycle and the mod's source-aware gene
reference cleanup.

## Implemented behavior

Coagulating Secretion is a local touch ability, not the Biotech Coagulate
ability. It:

- rejects self, hostile pawns, mechs, targets without health, targets without
  insect blood, and targets without a tendable injury or missing part;
- tends every currently `TendableNow` injury and missing-part wound at an
  independently rolled quality of `0.4–0.8`;
- displays the number of tended wounds;
- deducts `0.20` insect-jelly resource only after at least one wound was
  tended;
- does not directly restore hit points and has no cooldown;
- is granted and removed through the owning Geneline gene.

Hive Synaptic Node adds the three requested stat offsets and patches only the
Brain body's maximum health by `+10`. If the gene is removed while the brain
would otherwise be destroyed by existing damage, a local temporary support
Hediff keeps the same `+10` maximum health without reducing any injury
severity. The support removes itself after ordinary brain health is safe, or
when the gene is acquired again. This state is deep-saved as a normal Hediff.

## Automated checks

The following checks passed on 2026-07-29:

- Release / AnyCPU build for .NET Framework 4.7.2;
- `validate-stage1.ps1`;
- `validate-stage2.ps1`;
- `validate-stage3.ps1`;
- `validate-stage4.ps1`;
- XML parsing;
- assembly metadata inspection;
- `git diff --check`.

The checked assembly has:

```text
File: RedCrow_Insector_Tweaks/1.5/Assemblies/RedCrow.InsectorTweaks.dll
Assembly version: 0.8.0.0
Configuration: Release / AnyCPU
Target framework: .NET Framework 4.7.2
PE kind: MSIL
Size: 37888 bytes
SHA-256: 19b0d4bb82a0f3209a0d9ab340bd059c09ed4ea2a455ed1630687898693b9df9
Build warnings: 0
Build errors: 0
```

## Deferred in-game checks

Static validation does not claim these runtime scenarios as passed:

- valid/invalid Coagulating Secretion targets, all-wound tending, independent
  quality rolls, and the displayed tended count;
- no jelly cost for an invalid target, no wounds, or an interrupted cast;
- exactly 20 displayed jelly points spent after a successful cast;
- no direct hit-point restoration and no cooldown;
- ability removal when the evolution is removed;
- all three Synaptic Node stat offsets and the flat Brain `+10`;
- add/remove with a heavily damaged brain, temporary support cleanup, and
  save/load during that support;
- natural and gene-forced Pragmatist removal, conflict restoration, and
  absence of stale `suppressedBy` references after save/load.
