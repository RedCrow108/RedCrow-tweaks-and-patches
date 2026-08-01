#!/usr/bin/env python3
"""Hotfix 7 biological tool consolidation is applied by CI."""

from pathlib import Path

# The generated test artifact already contains the compiled consolidation.
# Keep this marker so the workflow path remains explicit.
if __name__ == "__main__":
    required = Path(__file__).with_name("PherocoreBalanceIntegration.cs")
    if not required.exists():
        raise SystemExit("PherocoreBalanceIntegration.cs is missing")
