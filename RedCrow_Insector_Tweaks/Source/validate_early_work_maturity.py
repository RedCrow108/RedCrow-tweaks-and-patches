#!/usr/bin/env python3
"""Validate the restored Early Work Maturity definition and runtime patch."""

from pathlib import Path
import xml.etree.ElementTree as ET

SOURCE_DIR = Path(__file__).resolve().parent
MOD_ROOT = SOURCE_DIR.parent
DEF_PATH = (
    MOD_ROOT
    / "1.5"
    / "Defs"
    / "GeneDefs"
    / "GeneDefs_AcceleratedBroodMaturity.xml"
)
SOURCE_PATH = SOURCE_DIR / "EarlyWorkMaturity.cs"
PROJECT_PATH = SOURCE_DIR / "RedCrow.InsectorTweaks.csproj"


def main() -> int:
    root = ET.parse(DEF_PATH).getroot()
    gene = root.find("VanillaRacesExpandedInsector.GenelineGeneDef")
    if gene is None:
        raise RuntimeError("Accelerated Brood Maturity GeneDef is missing")
    if gene.findtext("defName") != "RC_Evolution_AcceleratedBroodMaturity":
        raise RuntimeError("Unexpected Early Work Maturity defName")
    if gene.findtext("evolution") != "3":
        raise RuntimeError("Early Work Maturity must cost 3 evolution points")
    if gene.findtext("unlockable") != "true":
        raise RuntimeError("Early Work Maturity must be pherocore-unlockable")

    source = SOURCE_PATH.read_text(encoding="utf-8")
    required_source_fragments = (
        '"RC_Evolution_AcceleratedBroodMaturity"',
        "typeof(LifeStageWorkSettings)",
        "candidateAge < requiredAge",
        "previousUnlockAge > 0",
        "pawn.DevelopmentalStage.Baby()",
    )
    missing = [fragment for fragment in required_source_fragments if fragment not in source]
    if missing:
        raise RuntimeError("Early Work Maturity source is incomplete: " + ", ".join(missing))

    project = PROJECT_PATH.read_text(encoding="utf-8")
    if '<Compile Include="EarlyWorkMaturity.cs" />' not in project:
        raise RuntimeError("EarlyWorkMaturity.cs is not included in the project")

    print("Early Work Maturity definition and runtime patch are valid.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
