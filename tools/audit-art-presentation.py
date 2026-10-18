"""Audit runtime wiring, independently of ArtSource's source-image inventory."""
import collections
import csv
import hashlib
import json
from pathlib import Path
import re
import struct

ROOT = Path(__file__).resolve().parents[1]
REPORTS = ROOT / "docs/40-process"


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_tsv(name, columns, rows):
    with (REPORTS / name).open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.writer(stream, delimiter="\t")
        writer.writerow(columns)
        writer.writerows(rows)


def audit():
    art = (ROOT / "Assets/Scripts/Unity/Host/HostStepAArt.cs").read_text(encoding="utf-8-sig")
    resources = {path.stem: path for path in (ROOT / "Assets/Art").rglob("Resources/*.png")}
    for name in set(re.findall(r'"(ART-[^"\n]+_v\d+)"', art)):
        if name.endswith("_protagonist") or name.endswith("_companion_a") or name.endswith("_companion_b"):
            continue
        assert name in resources, name
    for name, path in resources.items():
        assert path.read_bytes().startswith(b"\x89PNG\r\n\x1a\n"), str(path)
    for identity in ("protagonist", "companion_a", "companion_b"):
        for action, width in (("idle", 560), ("walk", 2240)):
            path = resources[f"ART-CHAR-002_{identity}_{action}_v03"]
            assert struct.unpack(">II", path.read_bytes()[16:24]) == (width, 1760), str(path)

    catalog = (ROOT / "Assets/Resources/MapLayoutPrefabCatalog.asset").read_text(encoding="utf-8")
    source = (ROOT / "Assets/Scripts/Unity/Host/MapKindCatalog.cs").read_text(encoding="utf-8")
    paths = set(re.findall(r'"(Assets/Prefabs/[^"\n]+\.prefab)"', source))
    entries = re.findall(r'  - path: (.+)\n    prefab: \{fileID: (\d+), guid: (\w+), type: 3\}', catalog)
    assert {path for path, _, _ in entries} == paths
    for path, file_id, guid in entries:
        assert re.search(r"guid: (\w+)", (ROOT / (path + ".meta")).read_text())[1] == guid
        prefab = (ROOT / path).read_text(encoding="utf-8-sig")
        assert f"--- !u!1 &{file_id}\n" in prefab, path
        root_transform = next(block for block in re.split(r"(?=--- !u!)", prefab)
                              if block.startswith("--- !u!4 ") and "m_Father: {fileID: 0}" in block)
        assert f"m_GameObject: {{fileID: {file_id}}}" in root_transform, path

    objects = read_json(ROOT / "Content/BaseGame/Authoring/ContinuousSurface/Blueprints/huangcun.worldsiteblueprint.json")["objectPlacements"]
    assert len(objects) == 98
    new_art = {
        "treeM": "ART-GROUND-007_tree_medium_v02", "treeL": "ART-GROUND-007_tree_large_v02",
        "ore": "ART-GROUND-009_ore_base_v02", "bed": "ART-ROOM-006_bed_residential_v02",
        "controlCore": "ART-ROOM-008_core_isolated_v03", "storageRoom": "ART-ROOM-007_storage_crates_v03",
        "cave": "ART-ROOM-010_cave_entrance_v02",
    }
    notes = {
        "room": "旧程序房间地板与墙体；未接入新房屋模块",
        "grainField": "旧地块与颜色阶段刷新；新植物图层待合格",
        "herbField": "旧地块与颜色阶段刷新；新植物图层待合格",
        "zoneMine": "旧区域覆盖层", "spring": "旧区域覆盖层", "cushion": "旧蒲团 Prefab",
        "bed": "用途由真实父房间决定；住宅/医疗/牢床三图均在 Resources",
        "cave": "已接线；未发现的洞口按既有规则隐藏，非资源缺失",
    }
    rows = []
    by_id = {obj["placementId"]: obj for obj in objects}
    for obj in objects:
        kind = obj["kindId"]
        resource = new_art.get(kind, "")
        if kind == "bed":
            parent = by_id[obj["metadata"]["parentRoomId"]]
            usage = parent["metadata"]["roomUsage"].lower()
            resource = f"ART-ROOM-006_bed_{usage}_v02"
        status = "新美术已接入待视觉验收" if resource in resources else "旧表现保留"
        assert kind in new_art or kind in notes, kind
        if resource:
            assert resource in resources
        rows.append((obj["placementId"], kind, status,
                     str(resources[resource].relative_to(ROOT)).replace("\\", "/") if resource else "",
                     notes.get(kind, "正式 Resources + HostStepAArt.MapSprite 接线"),
                     "0", "新植物图层待合格" if kind in ("grainField", "herbField") else "无已知接线缺陷；视觉待验"))
    write_tsv("277-huangcun-art-coverage.tsv", ("stableId", "kind", "status", "runtimeAsset", "notes", "missingRuntimeAsset", "pending"), rows)

    mappings = {}
    pending = []
    for line in art.splitlines():
        match = re.search(r'case "(base:character_[^"]+)":', line)
        if match:
            pending.append(match[1])
        profile = re.search(r'return Profile\.(\w+);', line)
        if profile and pending:
            for definition in pending:
                mappings[definition] = profile[1]
            pending.clear()
    roster = read_json(ROOT / "Content/BaseGame/Data/Rosters/level_tester_roster.json")["definitions"][0]["entries"]
    assert len(roster) == 25
    roster_rows = [(entry["definitionId"], mappings.get(entry["definitionId"], "None"),
                    "已映射，本人动作或本人静态回退" if entry["definitionId"] in mappings else "未映射，保留旧显示")
                   for entry in roster]
    write_tsv("277-roster-art-coverage.tsv", ("definitionId", "profile", "status"), roster_rows)
    mapped = sum(entry["definitionId"] in mappings for entry in roster)
    assert mapped == 12, mapped

    source_refs = 0
    for path in (ROOT / "ArtSource").rglob("*.json"):
        raw = path.read_text(encoding="utf-8-sig")
        for reference in re.findall(r'"(ArtSource/[^"\n]+)"', raw):
            source_refs += 1
            assert (ROOT / reference).exists(), f"{path}: {reference}"
    duplicates = collections.defaultdict(list)
    for path in (ROOT / "ArtSource").rglob("*.png"):
        duplicates[hashlib.sha256(path.read_bytes()).hexdigest()].append(str(path.relative_to(ROOT)))
    duplicate_rows = [(digest, " | ".join(paths)) for digest, paths in duplicates.items() if len(paths) > 1]
    write_tsv("277-artsource-identical-images.tsv", ("sha256", "paths"), duplicate_rows)
    print(f"PASS: {len(resources)} runtime PNGs; 6 directional sheets; {len(entries)} Build prefab references")
    print(f"PASS: 98 objects: {dict(collections.Counter(row[2] for row in rows))}")
    print(f"PASS: roster={len(roster)} mapped={mapped} fallback={len(roster)-mapped}")
    print(f"PASS: {source_refs} ArtSource JSON source references; {len(duplicate_rows)} identical-image groups retained")


if __name__ == "__main__":
    audit()
