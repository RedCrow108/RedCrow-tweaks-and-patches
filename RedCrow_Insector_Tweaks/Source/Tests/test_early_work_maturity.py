#!/usr/bin/env python3
"""Static validation for the restored Early Work Maturity evolution."""

from pathlib import Path
import re
import xml.etree.ElementTree as ET

MOD_ROOT = Path(__file__).resolve().parents[2]
DEF_PATH = (
    MOD_ROOT
    / "1.5"
    / "Defs"
    / "GeneDefs"
    / "GeneDefs_AcceleratedBroodMaturity.xml"
)
SOURCE_PATH = MOD_ROOT / "Source" / "EarlyWorkMaturity.cs"
PROJECT_PATH = MOD_ROOT / "Source" / "RedCrow.InsectorTweaks.csproj"


def main() -> int:
    root = ET.parse(DEF_PATH).getroot()
    gene = root.find("VanillaRacesExpandedInsector.GenelineGeneDef")
    assert gene is not None
    assert gene.findtext("defName") == "RC_Evolution_AcceleratedBroodMaturity"
    assert gene.findtext("evolution") == "3"
    assert gene.findtext("unlockable") == "true"

    source = SOURCE_PATH.read_text(encoding="utf-8")
    assert '"RC_Evolution_AcceleratedBroodMaturity"' in source
    assert "LifeStageWorkSettings" in source
    assert "candidateAge < requiredAge" in source
    assert re.search(r"previousUnlockAge\s*>\s*0", source)
    assert "pawn.DevelopmentalStage.Baby()" in source

    project = PROJECT_PATH.read_text(encoding="utf-8")
    assert '<Compile Include="EarlyWorkMaturity.cs" />' in project

    print("Early Work Maturity definition and runtime patch are present.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
