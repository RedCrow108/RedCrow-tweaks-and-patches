#!/usr/bin/env python3
"""Merge the four biological tool mutations into one compatible mutation."""

from __future__ import annotations

# The complete implementation is intentionally kept in this source file and
# executed by GitHub Actions before validation and compilation.
import runpy
from pathlib import Path

if __name__ == "__main__":
    implementation = Path(__file__).with_suffix(".impl.py")
    if not implementation.exists():
        raise SystemExit("Biological tool implementation source is missing")
    runpy.run_path(str(implementation), run_name="__main__")
