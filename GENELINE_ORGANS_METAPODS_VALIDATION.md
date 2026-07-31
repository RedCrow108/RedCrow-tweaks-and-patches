# Geneline Organs and Metapods Validation

## Scope

This feature branch adds 15 Geneline elements after release `0.8.0`:

- 11 ordinary organ, limb, metabolism, tool, and neural effects;
- four special evolutions backed by four RedCrow metapods;
- a targeted `FilthRate = 0` compatibility rule for hive insects.

The elements remain unavailable through pheromone unlocking:
`unlockable=false` and `selectionWeight=0`.

## Installed-mod audit

The exact personal resource used by the annual abilities is:

```text
GeneDef: VRE_InsectJellyDependency
Runtime class: VanillaRacesExpandedInsector.Gene_Resource_InsectJelly
Base API: RimWorld.Gene_Resource
Internal range: 0.0-1.0
Displayed range: 0-100
Successful annual-ability cost: 1.0 internal / 100 displayed
```

The active HSK Insector representation is:

```text
Race/ThingDef: Human
XenotypeDef: VRE_Insector
Scenario PawnKindDef: VRE_TribalInsector
```

`CatInHead` and `Bipolar` are resolved at runtime from HSK More Content.
Corpse Memory is disabled with a translated reason when that package or
either trait is unavailable. There are no hard XML references to those
optional traits.

The four biological tools were matched to the installed Survival Tools Lite
definitions. Their strongest non-combat work factors are applied, without
copying melee damage or armor penetration:

| Biological mutation | Installed bronze-tool basis | Effective work bonus |
| --- | --- | --- |
| `RC_Mutation_BiologicalSickle` | bronze sickle | Plant work `+30%` |
| `RC_Mutation_BiologicalDiggingTools` | bronze hoe and pickaxe | Plant work `+30%`, Mining `+30%` |
| `RC_Mutation_BiologicalHandaxe` | bronze handaxe | Plant work `+30%`, Pruning `+30%` |
| `RC_Mutation_BiologicalHammer` | bronze hammer | Construction `+35%`, Smithing `+30%` |

When a physical tool provides a stronger factor, the strongest result is
used rather than adding both bonuses.

## Hunger formula

All RedCrow hunger effects are accumulated into one factor before that
factor is applied to the game's current food-fall result:

```text
RedCrow factor =
    product of RedCrow multipliers
    + sum of RedCrow additive modifiers
```

Examples:

- efficient metabolism alone: `0.5`;
- three additional limb sets: `1.0 + 0.2 + 0.4 + 0.6 = 2.2`;
- efficient metabolism plus dorsal manipulators: `0.5 + 0.4 = 0.9`.

The jelly abdomen uses an independent spawner: 50 `InsectJelly` every
60,000 ticks. It does not replace or conflict with the original Jelly Sacks.

## Metapod architecture

`RC_MetapodBase` owns the contained pawn through a `ThingOwner`, saves the
occupant and transformation data, and advances at normal speed without fuel
or fivefold speed while fueled. Fuel exhaustion does not reset progress.
The remaining-time estimate reflects the current speed.

| ThingDef | Base duration | Fuel/day | Full accelerated fuel |
| --- | ---: | ---: | ---: |
| `RC_Metapod_Usurpation` | 30 days | 25 | 150 |
| `RC_Metapod_CorpseMemory` | 60 days | 25 | 300 |
| `RC_Metapod_LarvalRebirth` | 30 days | 20 | 120 |
| `RC_Metapod_PerfectImago` | 120 days | 50 | 1200 |

The original VRE metapod definitions, duration, fuel, sickness, and
transformation classes are not patched.

Usurpation snapshots the source race, compatible PawnKind, faction,
ideology, xenotype metadata, and every ordinary gene with its
endogene/xenogene origin. Geneline genes and the selected Geneline are
excluded. At completion the target keeps the same pawn identity, age,
skills, traits, relations, memories, records, equipment, and inventory.

Race changes first verify artificial-part mapping by stable body-part path,
`BodyPartDef`, and body-part groups. Remaining part-bound Hediffs are
remapped, race-specific ThingComps are rebuilt, and dynamic pawn components
are refreshed. An unsafe transformation is rejected.

## Health renewal

Health cleanup is targeted; the complete `HediffSet` is never cleared.
It removes injuries, natural missing-part records not protected by an
artificial replacement, addictions, withdrawal, ordinary disease,
infection, parasites, and chronic age conditions.

All `Hediff_Implant` instances are preserved, which includes installed
prostheses, bionics, artificial organs, and other implants. Natural
missing-part markers beneath a directly installed artificial part are also
preserved. `RC_SolarStuporCondition` is explicitly retained.

Larval Rebirth and Perfect Imago keep the same pawn, genotype, Geneline,
identity, gear, and chronological age. Their biological ages become zero
and twenty respectively.

## Hive-insect filth compatibility

The active VFE Insectoids 2 and HSK compatibility definitions were traced
from `CompProperties_InsectSpawner`, `CompProperties_Hive`, and their
`InsectGenelineDef` lists. The patch returns zero only for `FilthRate` on
these 26 explicit `PawnKindDef` names:

```text
Megascarab
Spelopede
Megaspider
VFEI2_Megapede
VFEI2_Queen
VFEI2_Swarmling
VFEI2_Boomtick
VFEI2_Hellbeetle
VFEI2_Fuelmite
VFEI2_Macrofly
VFEI2_Megawasp
VFEI2_Gigalocust
VFEI2_Megathrips
VFEI2_Venomite
VFEI2_Acidspitter
VFEI2_Durapod
VFEI2_Tankroach
VFEI2_Ironclad
AA_MammothWorm
AA_MegaLouse
AA_Ravager
AA_BlackScarab
AA_BlackSpelopede
AA_BlackSpider
VFEI2_BlackQueen
VFEI2_BlackSwarmling
```

Every listed kind uses an identically named `Race/ThingDef`. The eight
Black Hive entries are harmless dormant names when Alpha Animals is absent.
`Human` and `VRE_TribalInsector` are intentionally excluded.

This changes only the regular animal-filth chance derived from `FilthRate`.
It does not skip the pawn's cell-entry method, so carried terrain filth can
still be picked up and dropped. Blood, death effects, hatch effects, slime,
and metapod filth are untouched.

The vanilla `Megascarab`, `Spelopede`, and `Megaspider` PawnKinds are used
both by VFE hives and ordinary infestations. RimWorld does not retain hive
spawn provenance on those pawns, so the explicit PawnKind rule applies to
that species wherever it was spawned. It still does not affect unrelated
animals or humanlike Insectors.

## Automated checks

The branch must pass:

- `validate-stage1.ps1`;
- `validate-stage2.ps1`;
- `validate-stage3.ps1`;
- `validate-stage4.ps1`;
- `validate-organs-metapods.ps1`;
- parsing of every XML file;
- Release / AnyCPU compilation for .NET Framework 4.7.2;
- assembly metadata inspection;
- `git diff --check`.

## Deferred in-game checks

Static checks do not claim the required runtime scenarios as verified.
The following remain for RimWorld testing:

- every hunger-factor combination before and after save/load;
- all three limb graphics and their simultaneous work/movement effects;
- each biological tool with no tool, a weaker tool, and a stronger tool;
- independent 50/day and combined 56/day jelly production;
- full usurpation incubation, coma, race/genotype transfer, cooldown, and
  save/load at every stage;
- Corpse Memory with fresh and invalid corpses, missing organs, mandatory
  traits, solar stupor, and HSK More Content absent;
- Larval Rebirth and Perfect Imago health renewal, ages, artificial parts,
  Geneline retention, fuel consumption, and save/load;
- destruction and deconstruction safety for occupied metapods;
- no `Filth_AnimalFilth` from every supported hive insect while blood,
  carried terrain filth, and ordinary-animal filth remain unchanged.
