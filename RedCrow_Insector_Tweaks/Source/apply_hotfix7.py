#!/usr/bin/env python3
"""Apply the user-confirmed Hotfix 7 balance and integration corrections.

The script is intentionally idempotent because GitHub Actions may execute it on
both the user commit and the synchronization commit made by the workflow bot.
"""

from __future__ import annotations

import re
import xml.etree.ElementTree as ET
from pathlib import Path

SOURCE_DIR = Path(__file__).resolve().parent
MOD_ROOT = SOURCE_DIR.parent
BALANCE_SOURCE = SOURCE_DIR / "PherocoreBalanceIntegration.cs"
GENE_DIR = MOD_ROOT / "1.5" / "Defs" / "GeneDefs"
PATCH_DIR = MOD_ROOT / "1.5" / "Patches"
RUSSIAN_GENE_DIR = (
    MOD_ROOT
    / "Languages"
    / "Russian"
    / "DefInjected"
    / "VanillaRacesExpandedInsector.GenelineGeneDef"
)

# New entries were previously defined in XML but were not connected to a
# pherocore pool. Points are preserved from XML unless the user explicitly
# supplied a new value.
NEW_BALANCE_ENTRIES = (
    ("RC_Mutation_AlienHiveVisage", True, 10, 1),
    ("RC_Evolution_ChlorophyllMetabolism", False, 3, 2),
    ("RC_Evolution_PheromoneUnity", False, 2, 2),
    ("RC_Evolution_MatriarchWail", False, 3, 2),
    ("RC_Evolution_PsiMimicry", False, 4, 3),
    ("RC_Evolution_InsanityPulse", False, 4, 4),
    ("RC_Evolution_UnconstrainedCarapace", False, 2, 2),
    ("RC_Evolution_MatriarchCalmAura", False, 4, 5),
    ("RC_Evolution_CoagulatingSecretion", False, 4, 3),
    ("RC_Mutation_LightStride", True, 3, 3),
    ("RC_Mutation_TwilightStride", True, 3, 3),
    ("RC_Mutation_HiveElectroOrgan", True, 3, 3),
    ("RC_Mutation_ThreatMark", True, 3, 3),
    ("RC_Mutation_DoomOmen", True, 5, 5),
    ("RC_Mutation_SolarOverdrive", True, 4, 3),
    ("RC_Mutation_SolarStupor", True, 4, 4),
    ("RC_Mutation_SolarDeath", True, 10, 3),
    ("RC_Evolution_HiveSynapticNode", False, 3, 4),
)

EXPECTED_BALANCE_ENTRIES = 118
EXPECTED_LOCAL_DEFS = 106


def write_if_changed(path: Path, content: str) -> None:
    current = path.read_text(encoding="utf-8-sig") if path.exists() else None
    if current != content:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")


def update_existing_balance_entry(
    text: str,
    def_name: str,
    *,
    points: int | None = None,
    tier: int | None = None,
) -> str:
    pattern = re.compile(
        rf'new BalanceEntry\("{re.escape(def_name)}",\s*'
        rf'(true|false),\s*(\d+),\s*(\d+)\)'
    )
    match = pattern.search(text)
    if match is None:
        raise RuntimeError(f"Existing balance entry was not found: {def_name}")
    mutation, old_points, old_tier = match.groups()
    replacement = (
        f'new BalanceEntry("{def_name}", {mutation}, '
        f'{points if points is not None else old_points}, '
        f'{tier if tier is not None else old_tier})'
    )
    return text[: match.start()] + replacement + text[match.end() :]


def add_missing_balance_entries(text: str) -> str:
    missing = [
        entry
        for entry in NEW_BALANCE_ENTRIES
        if f'new BalanceEntry("{entry[0]}",' not in text
    ]
    if not missing:
        return text

    anchor = re.search(
        r'(?m)^(\s*)new BalanceEntry\('
        r'"RC_Mutation_DuplicateCerebellum",\s*true,\s*1,\s*5\)\s*$'
        ,
        text,
    )
    if anchor is None:
        raise RuntimeError("Could not find the final balance-entry anchor")

    indent = anchor.group(1)
    original = anchor.group(0).rstrip()
    lines = [original + ","]
    for index, (def_name, is_mutation, points, tier) in enumerate(missing):
        comma = "," if index < len(missing) - 1 else ""
        lines.append(
            f'{indent}new BalanceEntry("{def_name}", '
            f'{str(is_mutation).lower()}, {points}, {tier}){comma}'
        )
    return text[: anchor.start()] + "\n".join(lines) + text[anchor.end() :]


def update_balance_source() -> None:
    text = BALANCE_SOURCE.read_text(encoding="utf-8")

    # Scout stride is a starting evolution and must never consume Sorne.
    text = update_existing_balance_entry(
        text,
        "RC_Evolution_ScoutStride",
        tier=0,
    )

    # The anima resonance now belongs to the first pherocore tier.
    text = update_existing_balance_entry(
        text,
        "RC_Evolution_HiveAnimaResonance",
        tier=1,
    )

    text = add_missing_balance_entries(text)
    write_if_changed(BALANCE_SOURCE, text)


def update_validation_counts() -> None:
    finalize = SOURCE_DIR / "finalize_balance_source.py"
    text = finalize.read_text(encoding="utf-8")
    text = re.sub(
        r'final\.count\("new BalanceEntry"\) != \d+',
        f'final.count("new BalanceEntry") != {EXPECTED_BALANCE_ENTRIES}',
        text,
    )
    text = text.replace(
        'raise RuntimeError("Balance entry count changed unexpectedly")',
        'raise RuntimeError("Balance entry count changed unexpectedly")',
    )
    write_if_changed(finalize, text)

    sync = SOURCE_DIR / "sync_balance.py"
    text = sync.read_text(encoding="utf-8")
    text = re.sub(
        r'if len\(entries\) != \d+:',
        f'if len(entries) != {EXPECTED_BALANCE_ENTRIES}:',
        text,
        count=1,
    )
    text = re.sub(
        r'Expected \d+ balance entries, found',
        f'Expected {EXPECTED_BALANCE_ENTRIES} balance entries, found',
        text,
        count=1,
    )
    text = re.sub(
        r'if verified != \d+:',
        f'if verified != {EXPECTED_LOCAL_DEFS}:',
        text,
        count=1,
    )
    text = re.sub(
        r'Expected \d+ local Defs, verified',
        f'Expected {EXPECTED_LOCAL_DEFS} local Defs, verified',
        text,
        count=1,
    )
    write_if_changed(sync, text)


def update_heavy_caste_label() -> None:
    path = GENE_DIR / "GeneDefs_GenelineStage1.xml"
    text = path.read_text(encoding="utf-8-sig")
    text = text.replace(
        "<label>тяжёлая поступь касты</label>",
        "<label>наросты на панцирях</label>",
    )
    write_if_changed(path, text)

    localization = """<?xml version="1.0" encoding="utf-8"?>
<LanguageData>
  <RC_Mutation_HeavyCasteStride.label>наросты на панцирях</RC_Mutation_HeavyCasteStride.label>
</LanguageData>
"""
    write_if_changed(RUSSIAN_GENE_DIR / "Hotfix7.xml", localization)


def disable_duplicate_removal_patch() -> None:
    # Hotfix 6 removed the only Scout Stride Def present in the tested HSK
    # package. Keep the local compatibility fallback; runtime integration will
    # prefer an upstream original when one genuinely exists.
    write_if_changed(
        PATCH_DIR / "RemoveDuplicateScoutStride.xml",
        '<?xml version="1.0" encoding="utf-8"?>\n<Patch />\n',
    )


SCOUT_STRIDE_SOURCE = r'''using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class OriginalScoutStrideIntegration
    {
        private const string LogPrefix =
            "[RedCrow Scout Stride]";
        private const string FallbackDefName =
            "RC_Evolution_ScoutStride";
        private const string ComponentTypeName =
            "VanillaRacesExpandedInsector.GameComponent_UnlockedGenes";
        private const string GeneTypeName =
            "VanillaRacesExpandedInsector.GenelineGeneDef";

        static OriginalScoutStrideIntegration()
        {
            try
            {
                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.OriginalScoutStrideIntegration");
                MethodInfo target = AccessTools.Method(
                    typeof(Game),
                    "FinalizeInit");
                MethodInfo postfixMethod = AccessTools.Method(
                    typeof(OriginalScoutStrideIntegration),
                    "GameFinalizeInitPostfix");
                if (target == null || postfixMethod == null)
                {
                    Log.Error(
                        LogPrefix + " Game.FinalizeInit could not be patched.");
                    return;
                }

                HarmonyMethod postfix = new HarmonyMethod(postfixMethod);
                postfix.priority = Priority.Last;
                postfix.after = new[]
                {
                    "RedCrow.InsectorTweaks.PherocoreGameComponentHotfix",
                    "RedCrow.InsectorTweaks.PherocoreInteractionAndSynapticHotfix"
                };
                harmony.Patch(target, postfix: postfix);
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Patch installation failed:\n" +
                    exception);
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void GameFinalizeInitPostfix()
        {
            try
            {
                GeneDef original = FindOriginalScoutStride();
                GeneDef fallback =
                    DefDatabase<GeneDef>.GetNamedSilentFail(FallbackDefName);
                GeneDef available = original ?? fallback;
                if (available == null)
                {
                    Log.Error(
                        LogPrefix + " Neither an upstream original nor the " +
                        "compatibility fallback was found.");
                    return;
                }

                SetStartingAvailability(available);
                if (fallback != null)
                {
                    SetStartingAvailability(fallback);
                }

                int removed = RemoveFromSornePool(original, fallback);
                ClearGeneListCache();

                string source = available.modContentPack == null
                    ? "<no-source>"
                    : available.modContentPack.PackageId;
                Log.Message(
                    LogPrefix + " Available from the start: " +
                    available.defName + " from " + source +
                    "; removed from Sorne=" + removed +
                    "; upstream original=" + (original != null) + ".");
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Integration failed:\n" + exception);
            }
        }

        private static void SetStartingAvailability(GeneDef gene)
        {
            Type geneType = AccessTools.TypeByName(GeneTypeName);
            FieldInfo unlockableField = geneType == null
                ? null
                : AccessTools.Field(geneType, "unlockable");
            if (unlockableField != null &&
                geneType.IsInstanceOfType(gene))
            {
                unlockableField.SetValue(gene, false);
            }
        }

        private static int RemoveFromSornePool(
            GeneDef original,
            GeneDef fallback)
        {
            Type componentType = AccessTools.TypeByName(ComponentTypeName);
            FieldInfo instanceField = componentType == null
                ? null
                : AccessTools.Field(componentType, "Instance");
            object component = instanceField == null
                ? null
                : instanceField.GetValue(null);
            FieldInfo poolField = componentType == null
                ? null
                : AccessTools.Field(
                    componentType,
                    "sorne_pherocore_genes");
            FieldInfo allField = componentType == null
                ? null
                : AccessTools.Field(
                    componentType,
                    "allSorneGenesUnlocked");
            IDictionary pool = component == null || poolField == null
                ? null
                : poolField.GetValue(component) as IDictionary;
            if (pool == null)
            {
                Log.Warning(
                    LogPrefix + " Sorne pherocore pool was unavailable.");
                return 0;
            }

            List<object> staleKeys = new List<object>();
            foreach (DictionaryEntry pair in pool)
            {
                GeneDef candidate = pair.Key as GeneDef;
                if (candidate == null)
                {
                    continue;
                }

                if (candidate == original ||
                    candidate == fallback ||
                    candidate.defName == FallbackDefName ||
                    IsScoutStrideLabel(candidate.label) ||
                    IsScoutStrideLabel(candidate.LabelCap.ToString()))
                {
                    staleKeys.Add(pair.Key);
                }
            }

            for (int index = 0; index < staleKeys.Count; index++)
            {
                pool.Remove(staleKeys[index]);
            }

            bool allUnlocked = pool.Count > 0;
            foreach (DictionaryEntry pair in pool)
            {
                if (!(pair.Value is bool) || !(bool)pair.Value)
                {
                    allUnlocked = false;
                    break;
                }
            }
            if (allField != null)
            {
                allField.SetValue(component, allUnlocked);
            }

            return staleKeys.Count;
        }

        private static GeneDef FindOriginalScoutStride()
        {
            Type geneType = AccessTools.TypeByName(GeneTypeName);
            if (geneType == null)
            {
                return null;
            }

            List<GeneDef> genes =
                DefDatabase<GeneDef>.AllDefsListForReading;
            for (int index = 0; index < genes.Count; index++)
            {
                GeneDef gene = genes[index];
                if (gene == null ||
                    gene.defName == FallbackDefName ||
                    !geneType.IsInstanceOfType(gene) ||
                    IsRedCrowDef(gene))
                {
                    continue;
                }

                if (IsScoutStrideLabel(gene.label) ||
                    IsScoutStrideLabel(gene.LabelCap.ToString()))
                {
                    return gene;
                }
            }

            return null;
        }

        private static bool IsRedCrowDef(Def def)
        {
            return def.modContentPack != null &&
                string.Equals(
                    def.modContentPack.PackageId,
                    "redcrow.insectortweaks",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsScoutStrideLabel(string value)
        {
            string normalized =
                (value ?? string.Empty).Trim().ToLowerInvariant();
            return normalized == "бег дозорного" ||
                normalized.Contains("scout stride") ||
                normalized.Contains("scout run");
        }

        private static void ClearGeneListCache()
        {
            Type utilsType = AccessTools.TypeByName(
                "VanillaRacesExpandedInsector.Utils");
            FieldInfo cacheField = utilsType == null
                ? null
                : AccessTools.Field(utilsType, "cachedGeneDefsInOrder");
            if (cacheField != null &&
                cacheField.IsStatic &&
                !cacheField.IsInitOnly)
            {
                cacheField.SetValue(null, null);
            }
        }
    }
}
'''


OWNERSHIP_SOURCE = r'''using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class UpstreamInsectorOwnershipEarlyFix
    {
        private const string LogPrefix =
            "[RedCrow Upstream Ownership]";
        private const string UpstreamPackageId =
            "CarbineAction.HSK.VRE.Insector";

        private static readonly string[] OriginalGeneDefNames =
        {
            "VRE_SwarmSynapse",
            "VRE_RoyalJellyInjector",
            "VRE_Microsized",
            "VRE_Colossal",
            "VRE_PyroResistantChitin",
            "VRE_FlameGlands",
            "VRE_ChemfuelSacks",
            "VRE_Pyrophiliac",
            "VRE_LocustWings",
            "VRE_InsectRostrum",
            "VRE_InsectVolatile",
            "VRE_EcdysoneOverdrive",
            "VRE_AcidGlands",
            "VRE_InfraredSensors",
            "VRE_AcidBurstSack",
            "VRE_SolidGreyMatter",
            "VRE_MineralRichInsectskin",
            "VRE_ChargerClaws",
            "VRE_HardLockedJoints",
            "VRE_PassiveInsect"
        };

        static UpstreamInsectorOwnershipEarlyFix()
        {
            try
            {
                AssignOwnership("static startup");
                LongEventHandler.ExecuteWhenFinished(
                    delegate { AssignOwnership("long-event completion"); });

                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.UpstreamInsectorOwnershipEarlyFix");
                MethodInfo target = AccessTools.Method(
                    typeof(Game),
                    "FinalizeInit");
                MethodInfo postfixMethod = AccessTools.Method(
                    typeof(UpstreamInsectorOwnershipEarlyFix),
                    "GameFinalizeInitPostfix");
                if (target != null && postfixMethod != null)
                {
                    HarmonyMethod postfix = new HarmonyMethod(postfixMethod);
                    postfix.priority = Priority.First;
                    harmony.Patch(target, postfix: postfix);
                }
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Installation failed:\n" + exception);
            }
        }

        [HarmonyPriority(Priority.First)]
        public static void GameFinalizeInitPostfix()
        {
            AssignOwnership("Game.FinalizeInit");
        }

        private static void AssignOwnership(string source)
        {
            ModContentPack upstream =
                LoadedModManager.RunningModsListForReading.FirstOrDefault(
                    pack => string.Equals(
                        pack.PackageId,
                        UpstreamPackageId,
                        StringComparison.OrdinalIgnoreCase));
            if (upstream == null)
            {
                Log.Warning(
                    LogPrefix + " Upstream package was not found at " +
                    source + ".");
                return;
            }

            int found = 0;
            int changed = 0;
            for (int index = 0;
                index < OriginalGeneDefNames.Length;
                index++)
            {
                GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail(
                    OriginalGeneDefNames[index]);
                if (gene == null)
                {
                    continue;
                }

                found++;
                if (gene.modContentPack != upstream)
                {
                    gene.modContentPack = upstream;
                    changed++;
                }
            }

            ClearGeneListCache();
            Log.Message(
                LogPrefix + " Ownership synchronized at " + source +
                ": found=" + found + "/" +
                OriginalGeneDefNames.Length + ", changed=" + changed +
                ", source=" + upstream.PackageId + ".");
        }

        private static void ClearGeneListCache()
        {
            Type utilsType = AccessTools.TypeByName(
                "VanillaRacesExpandedInsector.Utils");
            FieldInfo cacheField = utilsType == null
                ? null
                : AccessTools.Field(utilsType, "cachedGeneDefsInOrder");
            if (cacheField != null &&
                cacheField.IsStatic &&
                !cacheField.IsInitOnly)
            {
                cacheField.SetValue(null, null);
            }
        }
    }
}
'''


def write_runtime_fixes() -> None:
    write_if_changed(
        SOURCE_DIR / "OriginalScoutStrideIntegration.cs",
        SCOUT_STRIDE_SOURCE,
    )
    write_if_changed(
        SOURCE_DIR / "UpstreamInsectorOwnershipEarlyFix.cs",
        OWNERSHIP_SOURCE,
    )

    legacy_project = SOURCE_DIR / "RedCrow.InsectorTweaks.csproj"
    text = legacy_project.read_text(encoding="utf-8")
    anchor = '    <Compile Include="PherocoreBalanceIntegration.cs" />'
    additions = (
        '    <Compile Include="OriginalScoutStrideIntegration.cs" />\n'
        '    <Compile Include="PherocoreBalanceIntegration.cs" />\n'
        '    <Compile Include="UpstreamInsectorOwnershipEarlyFix.cs" />'
    )
    if 'Compile Include="UpstreamInsectorOwnershipEarlyFix.cs"' not in text:
        if anchor not in text:
            raise RuntimeError("Legacy project compile anchor was not found")
        text = text.replace(anchor, additions, 1)
    write_if_changed(legacy_project, text)


def validate() -> None:
    balance = BALANCE_SOURCE.read_text(encoding="utf-8")
    count = balance.count("new BalanceEntry")
    if count != EXPECTED_BALANCE_ENTRIES:
        raise RuntimeError(
            f"Expected {EXPECTED_BALANCE_ENTRIES} balance entries, found {count}"
        )

    expected_fragments = (
        'new BalanceEntry("RC_Evolution_ScoutStride", false, 1, 0)',
        'new BalanceEntry("RC_Evolution_HiveAnimaResonance", false, 1, 1)',
        'new BalanceEntry("RC_Evolution_ChlorophyllMetabolism", false, 3, 2)',
        'new BalanceEntry("RC_Mutation_SolarDeath", true, 10, 3)',
        'new BalanceEntry("RC_Evolution_HiveSynapticNode", false, 3, 4)',
        'new BalanceEntry("RC_Mutation_AlienHiveVisage", true, 10, 1)',
    )
    missing = [fragment for fragment in expected_fragments if fragment not in balance]
    if missing:
        raise RuntimeError("Missing Hotfix 7 balance fragments: " + ", ".join(missing))

    removal_patch = (
        PATCH_DIR / "RemoveDuplicateScoutStride.xml"
    ).read_text(encoding="utf-8-sig")
    if "PatchOperationRemove" in removal_patch:
        raise RuntimeError("Scout Stride is still removed by XML")

    for path in MOD_ROOT.rglob("*.xml"):
        ET.parse(path)

    print(
        "Hotfix 7 prepared: tier corrections, starting Scout Stride, "
        "heavy-caste rename, and early upstream ownership assignment."
    )


def main() -> int:
    update_balance_source()
    update_validation_counts()
    update_heavy_caste_label()
    disable_duplicate_removal_patch()
    write_runtime_fixes()
    validate()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
