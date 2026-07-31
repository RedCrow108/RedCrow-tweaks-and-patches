#!/usr/bin/env python3
"""Synchronize Geneline XML balance with PherocoreBalanceIntegration.cs."""

from __future__ import annotations

import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

MOD_ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path(__file__).with_name("PherocoreBalanceIntegration.cs")
GENE_DIR = MOD_ROOT / "1.5" / "Defs" / "GeneDefs"

ENTRY_RE = re.compile(
    r'new BalanceEntry\("([^"]+)",\s*(true|false),\s*(\d+),\s*(\d+)\)'
)
BLOCK_RE = re.compile(
    r'<VanillaRacesExpandedInsector\.GenelineGeneDef\b[^>]*>.*?'
    r'</VanillaRacesExpandedInsector\.GenelineGeneDef>',
    re.S,
)
EXPECTED_EXTERNAL = {
    "VRE_HypothermicHibernation",
    "VRE_JellySacks",
    "VRE_LowGreyMatter",
    "VRE_LowOctopamine",
    "VRE_OcelliEyes",
    "VRE_Parthenogenesis",
    "VRE_PorousSkin",
    "VRE_ProteinDenaturation",
    "VRE_SensitiveBrainGoop",
    "VRE_SpawningSack",
    "VRE_VestigialTubules",
    "VRE_VocalChitters",
}


def read_entries() -> dict[str, dict[str, object]]:
    text = SOURCE.read_text(encoding="utf-8")
    entries: dict[str, dict[str, object]] = {}
    for match in ENTRY_RE.finditer(text):
        def_name, mutation, points, tier = match.groups()
        if def_name in entries:
            raise RuntimeError(f"Duplicate balance entry: {def_name}")
        entries[def_name] = {
            "is_mutation": mutation == "true",
            "points": int(points),
            "tier": int(tier),
        }
    if len(entries) != 100:
        raise RuntimeError(f"Expected 100 balance entries, found {len(entries)}")
    return entries


def set_or_insert(block: str, field: str, value: str, indent: str) -> str:
    pattern = rf'(?m)^([ \t]*)<{field}>[^<]*</{field}>[ \t]*$'
    if re.search(pattern, block):
        return re.sub(
            pattern,
            rf'\1<{field}>{value}</{field}>',
            block,
            count=1,
        )
    return re.sub(
        r'(?m)^([ \t]*<defName>[^<]+</defName>[ \t]*\r?$)',
        rf'\1\n{indent}<{field}>{value}</{field}>',
        block,
        count=1,
    )


def update_block(block: str, entry: dict[str, object]) -> str:
    is_mutation = bool(entry["is_mutation"])
    target = "mutation" if is_mutation else "evolution"
    opposite = "evolution" if is_mutation else "mutation"
    points = str(entry["points"])
    unlockable = "true" if int(entry["tier"]) > 0 else "false"

    indent_match = re.search(r'\n([ \t]*)<defName>', block)
    indent = indent_match.group(1) if indent_match else "    "

    block = re.sub(
        rf'(?m)^[ \t]*<{opposite}>[^<]*</{opposite}>[ \t]*\r?\n?',
        "",
        block,
    )
    block = set_or_insert(block, target, points, indent)

    unlock_pattern = r'(?m)^([ \t]*)<unlockable>[^<]*</unlockable>[ \t]*$'
    if re.search(unlock_pattern, block):
        block = re.sub(
            unlock_pattern,
            rf'\1<unlockable>{unlockable}</unlockable>',
            block,
            count=1,
        )
    else:
        block = re.sub(
            rf'(?m)^([ \t]*<{target}>[^<]*</{target}>[ \t]*\r?$)',
            rf'\1\n{indent}<unlockable>{unlockable}</unlockable>',
            block,
            count=1,
        )
    return block


def synchronize_defs(entries: dict[str, dict[str, object]]) -> set[str]:
    found: set[str] = set()
    for path in sorted(GENE_DIR.glob("*.xml")):
        original = path.read_text(encoding="utf-8-sig")

        def replace(match: re.Match[str]) -> str:
            block = match.group(0)
            def_match = re.search(r'<defName>([^<]+)</defName>', block)
            if not def_match:
                return block
            def_name = def_match.group(1)
            entry = entries.get(def_name)
            if entry is None:
                return block
            found.add(def_name)
            return update_block(block, entry)

        updated = BLOCK_RE.sub(replace, original)
        if updated != original:
            path.write_text(updated, encoding="utf-8")
    return found


def update_player_text() -> None:
    localization = (
        MOD_ROOT
        / "Languages"
        / "Russian"
        / "DefInjected"
        / "VanillaRacesExpandedInsector.GenelineGeneDef"
        / "ConsumptionAndAffinity.xml"
    )
    text = localization.read_text(encoding="utf-8-sig")
    text = re.sub(
        r'(<RC_Evolution_HiveRegeneratorCells\.description>).*?'
        r'(</RC_Evolution_HiveRegeneratorCells\.description>)',
        r'\1Спящие регенеративные личинки время от времени поглощают шрамы, '
        r'старые раны и хронические заболевания.\2',
        text,
    )
    localization.write_text(text, encoding="utf-8")

    metapods = GENE_DIR / "GeneDefs_RedCrowMetapods.xml"
    text = metapods.read_text(encoding="utf-8-sig")
    text = text.replace(
        "Consumed by the hive removes mood and doubles base food and "
        "personal-jelly consumption.",
        "Consumed by the hive removes mood and doubles base food consumption.",
    )
    metapods.write_text(text, encoding="utf-8")


def verify(entries: dict[str, dict[str, object]], found: set[str]) -> None:
    missing = set(entries) - found
    if missing != EXPECTED_EXTERNAL:
        raise RuntimeError(
            "Unexpected local Def coverage. Missing: " + ", ".join(sorted(missing))
        )

    verified = 0
    for path in sorted(GENE_DIR.glob("*.xml")):
        text = path.read_text(encoding="utf-8-sig")
        for match in BLOCK_RE.finditer(text):
            block = match.group(0)
            def_match = re.search(r'<defName>([^<]+)</defName>', block)
            if not def_match or def_match.group(1) not in entries:
                continue
            def_name = def_match.group(1)
            entry = entries[def_name]
            target = "mutation" if entry["is_mutation"] else "evolution"
            opposite = "evolution" if entry["is_mutation"] else "mutation"
            expected_unlock = "true" if int(entry["tier"]) > 0 else "false"
            target_match = re.search(rf'<{target}>([^<]+)</{target}>', block)
            opposite_match = re.search(rf'<{opposite}>([^<]+)</{opposite}>', block)
            unlock_match = re.search(r'<unlockable>([^<]+)</unlockable>', block)
            if (
                target_match is None
                or target_match.group(1).strip() != str(entry["points"])
                or opposite_match is not None
                or unlock_match is None
                or unlock_match.group(1).strip().lower() != expected_unlock
            ):
                raise RuntimeError(f"Balance verification failed for {def_name}")
            verified += 1

    if verified != 88:
        raise RuntimeError(f"Expected 88 local Defs, verified {verified}")

    for xml_path in MOD_ROOT.rglob("*.xml"):
        ET.parse(xml_path)

    all_text = "\n".join(
        path.read_text(encoding="utf-8-sig", errors="replace")
        for path in MOD_ROOT.rglob("*.xml")
    )
    if "2010 tokens truncated" in all_text:
        raise RuntimeError("Damaged '2010 tokens truncated' text found in mod XML")
    if "personal-jelly consumption" in all_text:
        raise RuntimeError("Removed RC_SwarmConsumed jelly effect is still described")

    print(
        f"Balance synchronized: {verified} local Defs; "
        f"{len(EXPECTED_EXTERNAL)} external VRE Defs remain patch-managed."
    )


def main() -> int:
    try:
        entries = read_entries()
        found = synchronize_defs(entries)
        update_player_text()
        verify(entries, found)
        return 0
    except Exception as exc:  # noqa: BLE001 - build validation entry point
        print(f"Balance synchronization failed: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
