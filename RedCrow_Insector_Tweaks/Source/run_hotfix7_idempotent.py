#!/usr/bin/env python3
"""Run Hotfix 7 only when the branch has not already been synchronized."""

from __future__ import annotations

import runpy
from pathlib import Path

SOURCE_DIR = Path(__file__).resolve().parent
BALANCE = SOURCE_DIR / "PherocoreBalanceIntegration.cs"


def main() -> int:
    text = BALANCE.read_text(encoding="utf-8")
    count = text.count("new BalanceEntry")

    if count == 115:
        required = (
            'new BalanceEntry("RC_Evolution_ScoutStride", false, 1, 0)',
            'new BalanceEntry("RC_Evolution_HiveAnimaResonance", false, 1, 1)',
            'new BalanceEntry("RC_Evolution_ChlorophyllMetabolism", false, 3, 2)',
            'new BalanceEntry("RC_Mutation_SolarDeath", true, 10, 3)',
            'new BalanceEntry("RC_Evolution_HiveSynapticNode", false, 3, 4)',
            'new BalanceEntry("RC_Mutation_AlienHiveVisage", true, 10, 1)',
        )
        missing = [fragment for fragment in required if fragment not in text]
        if missing:
            raise RuntimeError(
                "Synchronized Hotfix 7 balance is missing: " + ", ".join(missing)
            )
        print("Hotfix 7 is already synchronized; base transformation skipped.")
        return 0

    if count != 100:
        raise RuntimeError(
            f"Unexpected balance state before Hotfix 7: {count} entries"
        )

    runpy.run_path(
        str(SOURCE_DIR / "apply_hotfix7.py"),
        run_name="__main__",
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
