#!/usr/bin/env python3
"""Merge the four biological tool mutations into one compatible mutation."""

from __future__ import annotations

import re
import xml.etree.ElementTree as ET
from pathlib import Path

SOURCE_DIR = Path(__file__).resolve().parent
MOD_ROOT = SOURCE_DIR.parent
BALANCE = SOURCE_DIR / "PherocoreBalanceIntegration.cs"
ORGANS_XML = MOD_ROOT / "1.5" / "Defs" / "GeneDefs" / "GeneDefs_GenelineOrgans.xml"
RUSSIAN_DIR = (
    MOD_ROOT
    / "Languages"
    / "Russian"
    / "DefInjected"
    / "VanillaRacesExpandedInsector.GenelineGeneDef"
)

CANONICAL = "RC_Mutation_BiologicalSickle"
LEGACY = (
    "RC_Mutation_BiologicalHandaxe",
    "RC_Mutation_BiologicalDiggingTools",
    "RC_Mutation_BiologicalHammer",
)
EXPECTED_ENTRIES = 115
EXPECTED_LOCAL = 103


def write_if_changed(path: Path, content: str) -> None:
    old = path.read_text(encoding="utf-8-sig") if path.exists() else None
    if old != content:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")


def remove_legacy_balance_entries() -> None:
    text = BALANCE.read_text(encoding="utf-8")
    for def_name in LEGACY:
        pattern = re.compile(
            rf'(?m)^\s*new BalanceEntry\("{re.escape(def_name)}",\s*'
            rf'true,\s*1,\s*0\),?\s*\n'
        )
        text, count = pattern.subn("", text, count=1)
        if count == 0 and f'new BalanceEntry("{def_name}",' in text:
            raise RuntimeError(f"Could not remove legacy balance entry {def_name}")

    # Repair the one possible missing comma caused by removing adjacent entries.
    text = re.sub(
        r'(new BalanceEntry\("RC_Mutation_BiologicalSickle"[^\n]*\))\s*\n(\s*new BalanceEntry)',
        r'\1,\n\2',
        text,
        count=1,
    )
    write_if_changed(BALANCE, text)


def replace_organ_defs() -> None:
    tree = ET.parse(ORGANS_XML)
    root = tree.getroot()
    by_name = {}
    for node in list(root):
        name = node.findtext("defName")
        if name:
            by_name[name] = node

    canonical = by_name.get(CANONICAL)
    if canonical is None:
        raise RuntimeError("Canonical biological tool Def was not found")

    def set_text(parent: ET.Element, tag: str, value: str) -> ET.Element:
        child = parent.find(tag)
        if child is None:
            child = ET.SubElement(parent, tag)
        child.text = value
        return child

    set_text(canonical, "label", "биологические рабочие инструменты")
    set_text(
        canonical,
        "description",
        "Комплекс хитиновых серпов, лопат, топоров и ударных пластин "
        "превращает тело носителя в универсальный рабочий инструмент улья. "
        "Скорость работы с растениями, добычи и обрезки повышается на 30%, "
        "строительства — на 35%, кузнечного дела — на 30%. Постоянное "
        "питание органов увеличивает скорость голода на 0,2.",
    )
    set_text(canonical, "mutation", "1")
    set_text(canonical, "unlockable", "false")

    custom = canonical.find("customEffectDescriptions")
    if custom is not None:
        canonical.remove(custom)
    custom = ET.SubElement(canonical, "customEffectDescriptions")
    for value in (
        "Скорость работы с растениями: +30%.",
        "Скорость добычи: +30%.",
        "Скорость обрезки: +30%.",
        "Скорость строительства: +35%.",
        "Скорость кузнечного дела: +30%.",
        "Скорость голода: +0,2.",
    ):
        item = ET.SubElement(custom, "li")
        item.text = value

    # Keep only one hunger extension on the canonical Def.
    extensions = canonical.find("modExtensions")
    if extensions is None:
        extensions = ET.SubElement(canonical, "modExtensions")
    hunger = None
    for item in list(extensions):
        if item.attrib.get("Class") == "RedCrow.InsectorTweaks.RC_HungerGeneExtension":
            if hunger is None:
                hunger = item
            else:
                extensions.remove(item)
    if hunger is None:
        hunger = ET.SubElement(
            extensions,
            "li",
            {"Class": "RedCrow.InsectorTweaks.RC_HungerGeneExtension"},
        )
    set_text(hunger, "hungerAdditive", "0.2")

    # Preserve old save references as ordinary hidden GeneDefs. They no longer
    # enter the Insector mutation catalogue and are replaced at Game.FinalizeInit.
    for def_name in LEGACY:
        old = by_name.get(def_name)
        if old is None:
            continue
        index = list(root).index(old)
        replacement = ET.Element("GeneDef")
        set_text(replacement, "defName", def_name)
        set_text(replacement, "label", "устаревший биологический инструмент")
        set_text(
            replacement,
            "description",
            "Служебный Def для переноса старого сохранения на объединённую "
            "мутацию «Биологические рабочие инструменты».",
        )
        set_text(replacement, "selectionWeight", "0")
        root.remove(old)
        root.insert(index, replacement)

    ET.indent(tree, space="  ")
    tree.write(ORGANS_XML, encoding="utf-8", xml_declaration=True)

    localization = """<?xml version="1.0" encoding="utf-8"?>
<LanguageData>
  <RC_Mutation_BiologicalSickle.label>биологические рабочие инструменты</RC_Mutation_BiologicalSickle.label>
  <RC_Mutation_BiologicalSickle.description>Комплекс хитиновых серпов, лопат, топоров и ударных пластин превращает тело носителя в универсальный рабочий инструмент улья. Скорость работы с растениями, добычи и обрезки повышается на 30%, строительства — на 35%, кузнечного дела — на 30%. Постоянное питание органов увеличивает скорость голода на 0,2.</RC_Mutation_BiologicalSickle.description>
</LanguageData>
"""
    write_if_changed(RUSSIAN_DIR / "BiologicalToolsHotfix7.xml", localization)


def update_runtime_factors() -> None:
    path = SOURCE_DIR / "GenelineOrganEffects.cs"
    text = path.read_text(encoding="utf-8")
    replacement = '''private static readonly Dictionary<string, Dictionary<string, float>>
            BiologicalToolFactors =
                new Dictionary<string, Dictionary<string, float>>
                {
                    {
                        "RC_Mutation_BiologicalSickle",
                        new Dictionary<string, float>
                        {
                            { "PlantWorkSpeed", 1.30f },
                            { "MiningSpeed", 1.30f },
                            { "PruningSpeed", 1.30f },
                            { "ConstructionSpeed", 1.35f },
                            { "SmithingSpeed", 1.30f }
                        }
                    }
                };

        private static readonly bool SurvivalToolsActive;'''
    pattern = re.compile(
        r'private static readonly Dictionary<string, Dictionary<string, float>>\s*'
        r'BiologicalToolFactors\s*=.*?'
        r'private static readonly bool SurvivalToolsActive;',
        re.S,
    )
    text, count = pattern.subn(replacement, text, count=1)
    if count != 1:
        raise RuntimeError("BiologicalToolFactors block was not replaced")
    write_if_changed(path, text)


MIGRATION_SOURCE = r'''using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RedCrow.InsectorTweaks
{
    [StaticConstructorOnStartup]
    public static class BiologicalToolGeneMigration
    {
        private const string LogPrefix =
            "[RedCrow Biological Tools]";
        private const string CanonicalDefName =
            "RC_Mutation_BiologicalSickle";

        private static readonly HashSet<string> LegacyDefNames =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "RC_Mutation_BiologicalHandaxe",
                "RC_Mutation_BiologicalDiggingTools",
                "RC_Mutation_BiologicalHammer"
            };

        static BiologicalToolGeneMigration()
        {
            try
            {
                MethodInfo target = AccessTools.Method(
                    typeof(Game),
                    "FinalizeInit");
                MethodInfo postfixMethod = AccessTools.Method(
                    typeof(BiologicalToolGeneMigration),
                    "GameFinalizeInitPostfix");
                if (target == null || postfixMethod == null)
                {
                    Log.Error(
                        LogPrefix + " Game.FinalizeInit could not be patched.");
                    return;
                }

                Harmony harmony = new Harmony(
                    "RedCrow.InsectorTweaks.BiologicalToolGeneMigration");
                HarmonyMethod postfix = new HarmonyMethod(postfixMethod);
                postfix.priority = Priority.Last;
                harmony.Patch(target, postfix: postfix);
            }
            catch (Exception exception)
            {
                Log.Error(
                    LogPrefix + " Migration patch failed:\n" + exception);
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void GameFinalizeInitPostfix()
        {
            GeneDef canonical =
                DefDatabase<GeneDef>.GetNamedSilentFail(CanonicalDefName);
            if (canonical == null)
            {
                Log.Error(LogPrefix + " Canonical mutation was not found.");
                return;
            }

            MethodInfo addGene = AccessTools.Method(
                typeof(Pawn_GeneTracker),
                "AddGene",
                new[] { typeof(GeneDef), typeof(bool) });
            MethodInfo removeGene = AccessTools.Method(
                typeof(Pawn_GeneTracker),
                "RemoveGene",
                new[] { typeof(Gene) });
            if (addGene == null || removeGene == null)
            {
                Log.Error(LogPrefix + " Gene migration methods were not found.");
                return;
            }

            int replaced = 0;
            List<Pawn> pawns =
                PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead;
            for (int pawnIndex = 0; pawnIndex < pawns.Count; pawnIndex++)
            {
                Pawn pawn = pawns[pawnIndex];
                if (pawn == null || pawn.genes == null)
                {
                    continue;
                }

                List<Gene> snapshot =
                    new List<Gene>(pawn.genes.GenesListForReading);
                List<Gene> legacy = new List<Gene>();
                bool hasCanonical = false;
                bool xenogene = false;

                for (int geneIndex = 0; geneIndex < snapshot.Count; geneIndex++)
                {
                    Gene gene = snapshot[geneIndex];
                    if (gene == null || gene.def == null)
                    {
                        continue;
                    }

                    if (gene.def.defName == CanonicalDefName)
                    {
                        hasCanonical = true;
                    }
                    else if (LegacyDefNames.Contains(gene.def.defName))
                    {
                        legacy.Add(gene);
                        xenogene = xenogene || IsXenogene(gene);
                    }
                }

                if (legacy.Count == 0)
                {
                    continue;
                }

                if (!hasCanonical)
                {
                    addGene.Invoke(
                        pawn.genes,
                        new object[] { canonical, xenogene });
                }

                for (int index = 0; index < legacy.Count; index++)
                {
                    removeGene.Invoke(
                        pawn.genes,
                        new object[] { legacy[index] });
                    replaced++;
                }
            }

            if (replaced > 0)
            {
                Log.Message(
                    LogPrefix + " Replaced legacy biological tool genes: " +
                    replaced + ".");
            }
        }

        private static bool IsXenogene(Gene gene)
        {
            PropertyInfo property = AccessTools.Property(
                gene.GetType(),
                "Xenogene");
            if (property != null && property.PropertyType == typeof(bool))
            {
                return (bool)property.GetValue(gene, null);
            }

            FieldInfo field = AccessTools.Field(
                gene.GetType(),
                "xenogene");
            return field != null &&
                field.FieldType == typeof(bool) &&
                (bool)field.GetValue(gene);
        }
    }
}
'''


def write_migration() -> None:
    write_if_changed(
        SOURCE_DIR / "BiologicalToolGeneMigration.cs",
        MIGRATION_SOURCE,
    )

    project = SOURCE_DIR / "RedCrow.InsectorTweaks.csproj"
    text = project.read_text(encoding="utf-8")
    line = '    <Compile Include="BiologicalToolGeneMigration.cs" />\n'
    if line.strip() not in text:
        anchor = '    <Compile Include="AssemblyInfo.cs" />\n'
        if anchor not in text:
            raise RuntimeError("Project compile anchor was not found")
        text = text.replace(anchor, anchor + line, 1)
    write_if_changed(project, text)


def update_counts() -> None:
    finalize = SOURCE_DIR / "finalize_balance_source.py"
    text = finalize.read_text(encoding="utf-8")
    text = re.sub(
        r'final\.count\("new BalanceEntry"\) != \d+',
        f'final.count("new BalanceEntry") != {EXPECTED_ENTRIES}',
        text,
    )
    write_if_changed(finalize, text)

    sync = SOURCE_DIR / "sync_balance.py"
    text = sync.read_text(encoding="utf-8")
    text = re.sub(
        r'if len\(entries\) != \d+:',
        f'if len(entries) != {EXPECTED_ENTRIES}:',
        text,
        count=1,
    )
    text = re.sub(
        r'Expected \d+ balance entries, found',
        f'Expected {EXPECTED_ENTRIES} balance entries, found',
        text,
        count=1,
    )
    text = re.sub(
        r'if verified != \d+:',
        f'if verified != {EXPECTED_LOCAL}:',
        text,
        count=1,
    )
    text = re.sub(
        r'Expected \d+ local Defs, verified',
        f'Expected {EXPECTED_LOCAL} local Defs, verified',
        text,
        count=1,
    )
    write_if_changed(sync, text)


def validate() -> None:
    balance = BALANCE.read_text(encoding="utf-8")
    if balance.count("new BalanceEntry") != EXPECTED_ENTRIES:
        raise RuntimeError("Unexpected final balance-entry count")
    for def_name in LEGACY:
        if f'new BalanceEntry("{def_name}",' in balance:
            raise RuntimeError(f"Legacy balance entry remains: {def_name}")

    tree = ET.parse(ORGANS_XML)
    root = tree.getroot()
    types = {
        node.findtext("defName"): node.tag
        for node in root
        if node.findtext("defName")
    }
    if not types.get(CANONICAL, "").endswith("GenelineGeneDef"):
        raise RuntimeError("Canonical biological tool is not a GenelineGeneDef")
    for def_name in LEGACY:
        if types.get(def_name) != "GeneDef":
            raise RuntimeError(f"Legacy compatibility Def is invalid: {def_name}")

    print(
        "Biological tools merged: one visible mutation, one +0.2 hunger "
        "extension, combined work bonuses, and old-save migration."
    )


def main() -> int:
    remove_legacy_balance_entries()
    replace_organ_defs()
    update_runtime_factors()
    write_migration()
    update_counts()
    validate()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
