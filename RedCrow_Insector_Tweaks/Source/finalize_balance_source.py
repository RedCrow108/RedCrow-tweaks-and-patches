#!/usr/bin/env python3
"""Make XML the balance source of truth and keep C# as a validator/pool bridge."""

from __future__ import annotations

import sys
from pathlib import Path

SOURCE = Path(__file__).with_name("PherocoreBalanceIntegration.cs")

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
    forbidden = (
        "mutationField.SetValue",
        "evolutionField.SetValue",
        "unlockableField.SetValue",
    )
    present = [token for token in forbidden if token in final]
    if present:
        raise RuntimeError("C# still overrides XML balance: " + ", ".join(present))
    if final.count("new BalanceEntry") != 100:
        raise RuntimeError("Balance entry count changed unexpectedly")

    print("Pherocore C# now validates XML balance without overriding it.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:  # noqa: BLE001 - build validation entry point
        print(f"Balance source finalization failed: {exc}", file=sys.stderr)
        raise SystemExit(1)
