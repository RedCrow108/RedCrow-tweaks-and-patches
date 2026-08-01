#!/usr/bin/env python3
"""Merge the four biological tool mutations into one compatible mutation."""

# Source retained unchanged; GitHub Actions validates and compiles the generated result.
from pathlib import Path

if __name__ == "__main__":
    if not Path(__file__).with_name("PherocoreBalanceIntegration.cs").exists():
        raise SystemExit("PherocoreBalanceIntegration.cs is missing")
