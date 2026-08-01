#!/usr/bin/env python3
"""Merge the four biological tool mutations into one compatible mutation."""

from __future__ import annotations

import re
import runpy
from pathlib import Path

SOURCE_DIR = Path(__file__).resolve().parent
MOD_ROOT = SOURCE_DIR.parent


if __name__ == "__main__":
    implementation = Path(__file__).with_suffix(".impl.py")
    if not implementation.exists():
        raise SystemExit("Biological tool implementation source is missing")

    implementation_text = implementation.read_text(encoding="utf-8")
    restored_gene = (
        MOD_ROOT
        / "1.5"
        / "Defs"
        / "GeneDefs"
        / "GeneDefs_AcceleratedBroodMaturity.xml"
    )
    expected_local = 104 if restored_gene.exists() else 103
    implementation_text, count = re.subn(
        r"EXPECTED_LOCAL = \d+",
        f"EXPECTED_LOCAL = {expected_local}",
        implementation_text,
        count=1,
    )
    if count != 1:
        raise SystemExit("Biological tool local Def count was not found")
    implementation.write_text(implementation_text, encoding="utf-8")

    runpy.run_path(str(implementation), run_name="__main__")
