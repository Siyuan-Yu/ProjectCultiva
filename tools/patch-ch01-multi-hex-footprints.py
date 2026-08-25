#!/usr/bin/env python3
"""Patch Ch01 sample WorldSites with explicit multi-hex footprints (no terrain changes)."""
from __future__ import annotations

import json
from pathlib import Path

WORLD_PATH = Path(__file__).resolve().parent.parent / "Content/BaseGame/Data/Worlds/ch01_hex_world.json"

# Compact connected footprints: anchor must be listed first.
PATCHES = {
    "base:site_a": {
        "displayName": "荒原甲",
        "anchorQ": 68,
        "anchorR": 40,
        "footprint": [
            {"q": 68, "r": 40},
            {"q": 69, "r": 40},
            {"q": 69, "r": 39},
            {"q": 68, "r": 39},
            {"q": 67, "r": 40},
            {"q": 68, "r": 41},
        ],
    },
    "base:site_b": {
        "displayName": "荒原乙",
        "anchorQ": 106,
        "anchorR": 26,
        "footprint": [
            {"q": 106, "r": 26},
            {"q": 107, "r": 26},
            {"q": 107, "r": 25},
            {"q": 106, "r": 25},
            {"q": 105, "r": 26},
            {"q": 106, "r": 27},
        ],
    },
    "base:site_chengzhen": {
        "displayName": "青石镇",
        "anchorQ": 118,
        "anchorR": 46,
        "footprint": [
            {"q": 118, "r": 46},
            {"q": 119, "r": 46},
            {"q": 119, "r": 45},
            {"q": 118, "r": 45},
        ],
    },
    "base:site_zhuangyuan": {
        "displayName": "庄院",
        "anchorQ": 118,
        "anchorR": 32,
        "footprint": [
            {"q": 118, "r": 32},
            {"q": 119, "r": 32},
            {"q": 119, "r": 31},
            {"q": 118, "r": 31},
        ],
    },
}


def axial_neighbors(q: int, r: int) -> list[tuple[int, int]]:
    return [
        (q + 1, r),
        (q + 1, r - 1),
        (q, r - 1),
        (q - 1, r),
        (q - 1, r + 1),
        (q, r + 1),
    ]


def is_connected(footprint: list[tuple[int, int]]) -> bool:
    if len(footprint) <= 1:
        return True
    seen = set(footprint)
    start = footprint[0]
    stack = [start]
    visited = {start}
    while stack:
        q, r = stack.pop()
        for n in axial_neighbors(q, r):
            if n in seen and n not in visited:
                visited.add(n)
                stack.append(n)
    return len(visited) == len(seen)


def main() -> int:
    data = json.loads(WORLD_PATH.read_text(encoding="utf-8"))
    world = None
    for block in data.get("definitions", []):
        if block.get("type") in ("hexWorldContent", "hexWorld"):
            world = block
            break
    if world is None:
        raise SystemExit("hexWorldContent not found")

    sites = world.get("sites") or []
    by_id = {s["siteId"]: s for s in sites if s.get("siteId")}
    for sid, patch in PATCHES.items():
        if sid not in by_id:
            raise SystemExit(f"missing site {sid}")
        site = by_id[sid]
        site["anchorQ"] = patch["anchorQ"]
        site["anchorR"] = patch["anchorR"]
        site["footprint"] = patch["footprint"]
        fp = [(h["q"], h["r"]) for h in patch["footprint"]]
        if (patch["anchorQ"], patch["anchorR"]) not in fp:
            raise SystemExit(f"anchor not in footprint: {sid}")
        if not is_connected(fp):
            raise SystemExit(f"footprint not connected: {sid}")

    # overlap check
    owner: dict[tuple[int, int], str] = {}
    for site in sites:
        sid = site.get("siteId", "")
        fp = site.get("footprint") or [{"q": site["anchorQ"], "r": site["anchorR"]}]
        for h in fp:
            key = (h["q"], h["r"])
            if key in owner and owner[key] != sid:
                raise SystemExit(f"overlap {owner[key]} vs {sid} at {key}")
            owner[key] = sid

    WORLD_PATH.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Patched multi-hex footprints:")
    for sid, patch in PATCHES.items():
        print(f"  {patch['displayName']} ({sid}): {len(patch['footprint'])} hex, anchor=({patch['anchorQ']},{patch['anchorR']})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
