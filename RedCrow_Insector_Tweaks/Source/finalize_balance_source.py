#!/usr/bin/env python3
"""Make XML the balance source of truth and keep C# as a validator/pool bridge."""

from __future__ import annotations

import sys
from pathlib import Path

SOURCE = Path(__file__).with_name("PherocoreBalanceIntegration.cs")
MOD_ROOT = Path(__file__).resolve().parents[1]

VALIDATION_METHOD = r'''        private static void ValidateBalanceAndUnlockability()
        {
            Type genelineType = AccessTools.TypeByName(
                GenelineGeneDefTypeName);
            if (genelineType == null)
            {
                Log.Error(
                    LogPrefix + " GenelineGeneDef type was not found.");
                return;
            }

            FieldInfo mutationField =
                AccessTools.Field(genelineType, "mutation");
            FieldInfo evolutionField =
                AccessTools.Field(genelineType, "evolution");
            FieldInfo unlockableField =
                AccessTools.Field(genelineType, "unlockable");

            if (mutationField == null ||
                evolutionField == null ||
                unlockableField == null)
            {
                Log.Error(
                    LogPrefix + " Required Geneline fields were not found.");
                return;
            }

            int validated = 0;
            List<string> missing = new List<string>();
            List<string> mismatches = new List<string>();

            for (int index = 0;
                index < BalanceEntries.Length;
                index++)
            {
                BalanceEntry entry = BalanceEntries[index];
                GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail(
                    entry.DefName);
                if (gene == null ||
                    !genelineType.IsInstanceOfType(gene))
                {
                    missing.Add(entry.DefName);
                    continue;
                }

                int actualMutation =
                    (int)mutationField.GetValue(gene);
                int actualEvolution =
                    (int)evolutionField.GetValue(gene);
                bool actualUnlockable =
                    (bool)unlockableField.GetValue(gene);

                int expectedMutation =
                    entry.IsMutation ? entry.Points : 0;
                int expectedEvolution =
                    entry.IsMutation ? 0 : entry.Points;
                bool expectedUnlockable = entry.Tier > 0;

                if (actualMutation != expectedMutation ||
                    actualEvolution != expectedEvolution ||
                    actualUnlockable != expectedUnlockable)
                {
                    mismatches.Add(
                        entry.DefName +
                        " expected M=" + expectedMutation +
                        ", E=" + expectedEvolution +
                        ", unlockable=" + expectedUnlockable +
                        "; actual M=" + actualMutation +
                        ", E=" + actualEvolution +
                        ", unlockable=" + actualUnlockable);
                    continue;
                }

                validated++;
            }

            Log.Message(
                LogPrefix + " Validated points and unlockability for " +
                validated + " Geneline defs. Tier 0 is available by " +
                "default; tiers 1-5 require pherocores.");

            if (missing.Count > 0)
            {
                Log.Warning(
                    LogPrefix + " Optional or missing defs skipped: " +
                    string.Join(", ", missing.ToArray()));
            }

            if (mismatches.Count > 0)
            {
                Log.Error(
                    LogPrefix + " XML balance validation failed:\n" +
                    string.Join("\n", mismatches.ToArray()));
            }
        }

'''

DUPLICATED_UPSTREAM_PATHS = (
    "1.5/Compat/VFEInsectoids/Defs/GeneDefs/GeneDefs_Evolutions_VFEInsectoids.xml",
    "1.5/Compat/VFEInsectoids/Defs/GeneDefs/GeneDefs_Mutations_VFEInsectoids.xml",
    "1.5/Compat/VFEInsectoids/Defs/AbilityDefs/Abilities_VFEInsectoids.xml",
    "1.5/Compat/VFEInsectoids/Defs/JobDefs/Jobs_VFEInsectoids.xml",
    "1.5/Compat/VFEInsectoids/Defs/FurDefs/FurDefs_VFEInsectoids.xml",
    "1.5/Compat/VFEInsectoids/Defs/HediffDefs/Hediffs_Hunger_VFEInsectoids.xml",
    "1.5/Compat/VFEInsectoids/Defs/HediffDefs/Hediffs_Attacks_VFEInsectoids.xml",
    "1.5/Compat/VFEInsectoids/Defs/HediffDefs/Hediffs_Misc_VFEInsectoids.xml",
    "1.5/Compat/VFEInsectoids/Patches/PherocorePatch.xml",
    "1.5/Compat/VFEInsectoids/Patches/GlobalWorkSpeedStatPart.xml",
    "1.5/Compat/VFEInsectoids/Patches/DarknessPatch.xml",
    "1.5/Compat/VFEInsectoids/Patches/HeavyWeaponsPatches.xml",
)


def validate_narrow_runtime_correction(final: str) -> None:
    forbidden = (
        "mutationField.SetValue",
        "evolutionField.SetValue",
    )
    present = [token for token in forbidden if token in final]
    if present:
        raise RuntimeError("C# still overrides XML balance: " + ", ".join(present))

    unlock_write = "unlockableField.SetValue"
    if final.count(unlock_write) != 1:
        raise RuntimeError(
            "Expected exactly one runtime unlockability correction for "
            "the original Insectoids 2 genes"
        )

    method_start = final.find(
        "        private static int EnsureOriginalPherocoreUnlockability()"
    )
    method_end = final.find(
        "        private static int EnsurePool(",
        method_start,
    )
    write_index = final.find(unlock_write)
    if (
        method_start < 0
        or method_end < 0
        or not method_start <= write_index < method_end
    ):
        raise RuntimeError(
            "Runtime unlockability write escaped the original-gene repair method"
        )


def validate_no_upstream_duplicates() -> None:
    duplicates = [
        relative
        for relative in DUPLICATED_UPSTREAM_PATHS
        if (MOD_ROOT / relative).exists()
    ]
    if duplicates:
        raise RuntimeError(
            "Duplicated HSK Insector compatibility files remain: "
            + ", ".join(duplicates)
        )


def main() -> int:
    text = SOURCE.read_text(encoding="utf-8")
    if "ApplyBalanceAndUnlockability();" in text:
        text = text.replace(
            "ApplyBalanceAndUnlockability();",
            "ValidateBalanceAndUnlockability();",
            1,
        )
        start = text.index(
            "        private static void ApplyBalanceAndUnlockability()"
        )
        end = text.index("        private static void InstallPatches()", start)
        text = text[:start] + VALIDATION_METHOD + text[end:]
        SOURCE.write_text(text, encoding="utf-8")
    elif "ValidateBalanceAndUnlockability();" not in text:
        raise RuntimeError("Balance initialization method was not recognized")

    final = SOURCE.read_text(encoding="utf-8")
    validate_narrow_runtime_correction(final)
    validate_no_upstream_duplicates()

    if final.count("new BalanceEntry") != 100:
        raise RuntimeError("Balance entry count changed unexpectedly")

    print(
        "Pherocore C# validates XML balance, repairs only original "
        "Insectoids 2 unlockability, and contains no duplicated upstream files."
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:  # noqa: BLE001 - build validation entry point
        print(f"Balance source finalization failed: {exc}", file=sys.stderr)
        raise SystemExit(1)
