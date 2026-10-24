#!/usr/bin/env python3
"""Export a file-level art inventory and the delta against its previous snapshot.

This is an evidence report, not a replacement for the 273 asset-family register.
Generated reports are excluded from their own input set to avoid self-reference.
"""

from __future__ import annotations

import csv
import hashlib
import json
import re
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
ARTSOURCE = ROOT / "ArtSource"
ASSETS_ART = ROOT / "Assets" / "Art"
REPORTS = ARTSOURCE / "Reports"
INVENTORY = REPORTS / "current-art-inventory.tsv"
CHANGE_REPORT = REPORTS / "latest-art-change-report.md"
REGISTER = ROOT / "docs/40-process/273-current-art-asset-register.tsv"
ID_RE = re.compile(r"ART-(?:CHAR|ANIM|ROOM|GROUND|UI|FX|REF)-\d{3}")
VERSION_RE = re.compile(r"(?:^|[_-])(v\d{2,})(?:[_.-]|$)", re.IGNORECASE)
IMAGE_SUFFIXES = {".png", ".jpg", ".jpeg", ".webp", ".gif", ".psd", ".tif", ".tiff"}
SPINE_SUFFIXES = {".spine", ".skel", ".atlas", ".atlas.txt"}
FIELDS = [
    "AssetID", "SubAsset/子件", "文件路径", "文件类型", "文件大小(字节)", "SHA-256",
    "生成批次和版本", "技术路线", "所属角色Skin/共用动作", "文件角色", "制作状态",
    "Unity导入路径", "实际运行引用", "被替代版本", "清理分类", "是否可安全清理", "问题及待确认事项",
]


def rel(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_register() -> dict[str, dict[str, str]]:
    with REGISTER.open("r", encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.DictReader(handle, delimiter="\t"))
    by_id = {row["AssetID"]: row for row in rows}
    original_ids = {f"ART-{family}-{index:03d}"
                    for family, count in (("CHAR", 7), ("ANIM", 5), ("ROOM", 10),
                                          ("GROUND", 14), ("UI", 10), ("FX", 6), ("REF", 3))
                    for index in range(1, count + 1)}
    if len(by_id) != len(rows) or not original_ids.issubset(by_id) or any(
            ID_RE.fullmatch(asset_id) is None for asset_id in by_id):
        raise ValueError("273 must preserve its original 55 AssetIDs and contain unique valid IDs")
    return by_id


def manifest_mappings() -> tuple[dict[str, set[str]], set[str]]:
    mapped: dict[str, set[str]] = defaultdict(set)
    references: set[str] = set()

    def walk(value: object, inherited_ids: set[str]) -> None:
        if isinstance(value, list):
            for child in value:
                walk(child, inherited_ids)
            return
        if not isinstance(value, dict):
            if isinstance(value, str) and value.startswith(("ArtSource/", "Assets/Art/")):
                references.add(value.replace("\\", "/"))
            return
        ids = set(inherited_ids)
        for key in ("asset_id", "assetId", "primary_asset_id"):
            if isinstance(value.get(key), str):
                ids.update(ID_RE.findall(value[key]))
        for key in ("asset_ids", "assetIds"):
            if isinstance(value.get(key), list):
                for item in value[key]:
                    if isinstance(item, str):
                        ids.update(ID_RE.findall(item))
        for key, child in value.items():
            if isinstance(child, str) and child.startswith(("ArtSource/", "Assets/Art/")):
                path = child.replace("\\", "/")
                references.add(path)
                mapped[path].update(ids)
            elif key not in ("asset_ids", "assetIds"):
                walk(child, ids)

    for path in ARTSOURCE.rglob("*.json"):
        if REPORTS in path.parents:
            continue
        try:
            walk(json.loads(path.read_text(encoding="utf-8-sig")), set())
        except (OSError, UnicodeError, json.JSONDecodeError):
            continue
    return mapped, references


def unity_guid_references() -> dict[str, list[str]]:
    """Find serialized GUID users; this proves a reference, not an actual play session."""
    consumers = []
    for suffix in ("*.prefab", "*.unity", "*.asset", "*.mat"):
        consumers.extend((ROOT / "Assets").rglob(suffix))
    guid_uses: dict[str, list[str]] = defaultdict(list)
    for path in consumers:
        try:
            content = path.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        for guid in set(re.findall(r"\bguid:\s*([0-9a-f]{32})", content)):
            guid_uses[guid].append(rel(path))
    return guid_uses


def file_role(path: str) -> str:
    if path.endswith(".meta"):
        return "Unity导入元数据"
    if path.startswith("Assets/Art/"):
        return "运行资源目录文件（具体引用另列）"
    if "/Layouts/" in path and path.endswith(".json"):
        return "派生布局元数据（无图像二进制）"
    if "/Originals/" in path or path.startswith("ArtSource/Inbox/"):
        return "原图"
    if "/References/" in path:
        return "参考图"
    if "/Previews/" in path or "contact_sheet" in path or "contact-sheet" in path:
        return "预览"
    if "/Frames/" in path or "/Sheets/" in path or "/Static/" in path or "/Candidates/" in path:
        return "派生图"
    return "辅助记录/未知"


def batch_version(path: str) -> str:
    parts = path.split("/")
    batch = parts[2] if len(parts) > 3 and parts[0:2] == ["ArtSource", "Batches"] else "未知"
    version = VERSION_RE.search(Path(path).name)
    return batch + ("/" + version.group(1).lower() if version else "")


def action_or_skin(path: str, ids: list[str]) -> str:
    if not any(x.startswith(("ART-CHAR-", "ART-ANIM-")) for x in ids):
        return "未知"
    name = Path(path).stem.lower()
    if "_humancommonrig_" in name:
        if "sidehead" in name:
            return "SharedSkinSideHeads;East/West(side mirrored)"
        if "tools" in name:
            return "SharedEquipment;Work/Attack;Spear/Axe/Hoe/Pick/Sickle/Basket"
        skin = name.split("_humancommonrig_")[0].replace("art-char-", "")
        return skin + ";South/North/East/West(side mirrored);Torso/Head/LeftArm/RightArm/LeftLeg/RightLeg"
    tokens = [x for x in ("protagonist", "companion_a", "companion_b", "guard", "villager", "resident_hoe", "resident_apron", "resident_basket", "idle", "walk", "work", "attack", "sleep", "carry") if x in name]
    return ",".join(tokens) if tokens else "未知（文件未标明Skin/动作）"


def load_old() -> dict[str, dict[str, str]] | None:
    if not INVENTORY.exists():
        return None
    with INVENTORY.open("r", encoding="utf-8-sig", newline="") as handle:
        return {row["文件路径"]: row for row in csv.DictReader(handle, delimiter="\t")}


def main() -> None:
    register = read_register()
    old = load_old()
    mapped, manifest_refs = manifest_mappings()
    guid_uses = unity_guid_references()
    host_source = (ROOT / "Assets/Scripts/Unity/Host/HostStepAArt.cs").read_text(encoding="utf-8")
    rig_source = (ROOT / "Assets/Scripts/Unity/Host/HumanCommonRigView.cs").read_text(encoding="utf-8")
    files = sorted(
        (path for folder in (ARTSOURCE, ASSETS_ART) for path in folder.rglob("*")
         if path.is_file() and REPORTS not in path.parents),
        key=lambda path: rel(path).casefold(),
    )
    rows: list[dict[str, str]] = []
    duplicates: dict[str, list[str]] = defaultdict(list)
    total_bytes = 0
    for path in files:
        location = rel(path)
        size = path.stat().st_size
        digest = sha256(path)
        total_bytes += size
        if path.suffix.lower() in IMAGE_SUFFIXES:
            duplicates[digest].append(location)
        ids = set(ID_RE.findall(location)) | mapped.get(location, set())
        if not ids:
            ids = {aid for aid, family in register.items() if location in family.get("当前文件路径", "")}
        valid_ids = sorted(ids & register.keys())
        unknown_ids = sorted(ids - register.keys())
        role = file_role(location)
        issues = []
        if unknown_ids:
            issues.append("文件名/manifest AssetID 未在273中登记: " + ",".join(unknown_ids))
        if not valid_ids:
            issues.append("AssetID归属未知")
        imported = (location[:-5] if location.endswith(".meta") else location) if location.startswith("Assets/Art/") else "无（ArtSource工程外）"
        runtime = "无（ArtSource工程外）" if location.startswith("ArtSource/") else "未知"
        if location.startswith(("ArtSource/Batches/Batch_ModularRig_001/Originals/",
                                "ArtSource/Batches/Batch_ModularRig_002/Originals/")):
            imported = "Assets/Art/Characters/Resources/" + Path(location).name
            runtime = "原图；同SHA运行副本由HumanCommonRigView.Resources代码引用（Unity运行待验）"
        if "/Layouts/" in location and path.name.endswith("_layout.json"):
            imported = "Assets/Art/Characters/Resources/" + path.name
            runtime = "派生布局归档；同SHA运行TextAsset由HumanCommonRigView读取（Unity待验）"
        if location.startswith("Assets/Art/"):
            if "_HumanCommonRig_" in path.name and path.name.endswith("_layout.json"):
                source_png = path.with_name(path.stem.removesuffix("_layout") + ".png")
                if source_png.exists():
                    runtime = "HumanCommonRigView.Resources TextAsset布局；PNG透明边界/关节/握点（Unity待验）"
            meta = path if path.suffix == ".meta" else Path(str(path) + ".meta")
            if meta.exists():
                match = re.search(r"^guid:\s*([0-9a-f]{32})", meta.read_text(encoding="utf-8", errors="ignore"), re.MULTILINE)
                if match and guid_uses.get(match.group(1)):
                    runtime = "序列化GUID引用: " + ",".join(sorted(guid_uses[match.group(1)])[:3])
            if runtime == "未知" and "/Resources/" in location and path.suffix.lower() == ".png":
                if "_HumanCommonRig_" in path.stem and (
                    path.stem in rig_source or
                    (path.stem.startswith("ART-CHAR-") and "_HumanCommonRig_parts_v01" in rig_source)
                ):
                    runtime = "HumanCommonRigView.Resources代码引用；ModularRig开关接线（Unity运行待验）"
                elif path.stem in host_source:
                    runtime = "HostStepAArt代码明文资源名（未做Unity运行验收）"
                elif valid_ids and any(x.startswith("ART-CHAR-") for x in valid_ids):
                    runtime = "HostStepAArt动态角色资源路径候选（具体身份运行待验）"
        status = "未知"
        if valid_ids:
            states = {register[aid].get("制作进度", "未知") for aid in valid_ids}
            status = "文件级未知；273资产族=" + ",".join(sorted(states))
        safe = "否（保留）" if location.startswith("Assets/Art/") or "/Originals/" in location else "未知（待引用核）"
        if location in manifest_refs:
            safe = "否（manifest引用）"
        route = ",".join(register[aid].get("推荐技术路线", "未知") for aid in valid_ids) or "未知"
        subasset = Path(location).stem
        if valid_ids:
            subasset = ID_RE.sub("", subasset, count=1).strip("_- ") or "未知"
        if "_HumanCommonRig_" in location and path.suffix.lower() == ".png":
            subasset = "parts atlas: torso/head/left arm/right arm/left leg/right leg" if "_parts_" in location else "east side head atlas" if "_sidehead" in location else "equipment atlas: spear/axe/hoe/pick/sickle/basket"
        if location.startswith("Assets/Art/Characters/") and "_HumanCommonRig_" in location:
            cleanup_class = "保留样机资源（Unity人工验收待完成）"
        elif location.startswith("Assets/Art/Characters/") and runtime != "未知":
            cleanup_class = "未来逐步迁移后可退役（当前运行保留）"
        elif location.startswith("Assets/Art/"):
            cleanup_class = "保留正式使用" if runtime != "未知" else "引用不明暂缓"
        elif "/Originals/" in location or location.startswith("ArtSource/References/"):
            cleanup_class = "保留迁移参考"
        elif "/Batch_Codex_001/Frames/" in location:
            cleanup_class = "旧版可退役（待来源/引用迁移）"
        elif "/Previews/" in location:
            cleanup_class = "可重建预览（引用待核）"
        else:
            cleanup_class = "保留迁移参考" if role in ("原图", "派生图") else "引用不明暂缓"
        rows.append({
            "AssetID": ",".join(valid_ids) or "未知", "SubAsset/子件": subasset,
            "文件路径": location, "文件类型": path.suffix.lower() or "无扩展名",
            "文件大小(字节)": str(size), "SHA-256": digest,
            "生成批次和版本": batch_version(location), "技术路线": route,
            "所属角色Skin/共用动作": action_or_skin(location, valid_ids), "文件角色": role,
            "制作状态": status, "Unity导入路径": imported, "实际运行引用": runtime,
            "被替代版本": "未知", "清理分类": cleanup_class, "是否可安全清理": safe,
            "问题及待确认事项": "；".join(issues) or "无已知；视觉/运行状态仍待核",
        })
    for row in rows:
        location = row["文件路径"]
        if len(duplicates[row["SHA-256"]]) > 1 and (location.startswith("ArtSource/Inbox/") or "/Candidates/" in location):
            row["清理分类"] = "确定重复（待manifest/273路径迁移）"
    REPORTS.mkdir(parents=True, exist_ok=True)
    with INVENTORY.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=FIELDS, delimiter="\t", lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)

    current = {row["文件路径"]: row for row in rows}
    if old is None:
        added = changed = removed = []
        delta = "首次建档，无上一份快照；历史新增/修改/删除/迁移均记未知，不能把当前全部文件当成本轮新增。"
    else:
        added = sorted(current.keys() - old.keys())
        removed = sorted(old.keys() - current.keys())
        changed = sorted(path for path in current.keys() & old.keys() if current[path]["SHA-256"] != old[path]["SHA-256"])
        delta = f"相对上一快照：新增 {len(added)}、修改 {len(changed)}、删除 {len(removed)}。仅哈希相同的路径搬移可进一步判为迁移。"
    duplicate_groups = [paths for paths in duplicates.values() if len(paths) > 1]
    duplicate_files = sum(len(paths) - 1 for paths in duplicate_groups)
    duplicate_waste = sum((len(paths) - 1) * int(current[paths[0]]["文件大小(字节)"]) for paths in duplicate_groups)
    originals = sum(1 for row in rows if row["文件角色"] == "原图" and row["文件类型"] in IMAGE_SUFFIXES)
    frames = sum(1 for row in rows if "/Frames/" in row["文件路径"] and row["文件类型"] in IMAGE_SUFFIXES)
    spine = sum(1 for row in rows if row["文件类型"] in SPINE_SUFFIXES or row["文件路径"].endswith(".atlas.txt"))
    asset_ids = {aid for row in rows for aid in row["AssetID"].split(",") if aid != "未知"}
    integrated = sum(1 for aid in asset_ids if register[aid].get("制作进度", "").startswith("已接入"))
    registered_integrated = sum(1 for family in register.values() if family.get("制作进度", "").startswith("已接入"))
    lines = [
        "# 本次美术文件变更报告", "",
        f"> 生成时间：{datetime.now(timezone.utc).isoformat()}（UTC）。273 TSV 是资产族进度真源；本报告只做文件级盘点。", "",
        delta, "",
        "## 当前汇总", "",
        f"- 扫描范围：ArtSource（不含自动生成的 Reports）及 Assets/Art；文件 {len(rows)}，{total_bytes} 字节。",
        f"- 原图 {originals}；Frames 目录图像 {frames}；Spine 工程/导出文件 {spine}。",
        f"- 273 全部{len(register)}个资产族：登记已接入待验 {registered_integrated}，其余 {len(register)-registered_integrated}；其中本次文件可关联 {len(asset_ids)} 个AssetID（已接入待验 {integrated}、其余 {len(asset_ids)-integrated}）。这些数不是文件级人工验收。",
        f"- 同 SHA-256 图片 {len(duplicate_groups)} 组，额外副本 {duplicate_files} 个；理论重复占用 {duplicate_waste} 字节。重复不等于可删除。",
        "- 当前可直接删除的图像候选：0；manifest、原图来源、Unity引用及加工脚本均可能依赖旧路径。",
        "- HumanCommonRig：001/002 两批基础部件共12套Skin、25身份；003闭眼、004斜向头身、005侧臂见各批manifest及docs/40-process/297-art-rig-and-source-cleanup-current-handoff-2026-10-18.md。Unity人工验收待完成，Spine不是依赖。",
        "", "## 本次差异", "",
    ]
    for title, paths in (("新增", added), ("修改", changed), ("删除", removed)):
        lines.append(f"- {title}：" + ("、".join(paths[:30]) + ("…" if len(paths) > 30 else "") if paths else ("未知（首次建档）" if old is None else "无")))
    lines += ["- 迁移/接入：静态代码引用仅证明已接线，Unity/LevelTester 人工验收仍待完成。", "- 待返工：以273与各批 manifest 的候选问题为准；文件清单本身不代表视觉验收。", "- 受影响 AssetID：" + ("、".join(sorted({aid for path in added+changed+removed for aid in (current.get(path) or old.get(path) or {}).get("AssetID", "").split(",") if aid and aid != "未知"})) or "无已确认的图像变更"), ""]
    lines += ["## HumanCommonRig 文件映射", "",
              "001/002基础透明部件/附件的运行副本与原图 SHA-256 相同；003～005 另补闭眼、斜向头身和侧臂。旧 Unity 逐帧资源仅作回退。以下仅证明接线，Unity 视觉/Build/性能待制作人验收。", ""]
    for batch in ("Batch_ModularRig_001", "Batch_ModularRig_002"):
        rig_manifest = ARTSOURCE / "Batches" / batch / "manifest.json"
        if not rig_manifest.exists():
            continue
        rig = json.loads(rig_manifest.read_text(encoding="utf-8-sig"))
        for entry in rig.get("entries", []):
            ids = entry.get("asset_ids") or [entry.get("asset_id", "未知")]
            lines.append("- " + ",".join(ids) + " / " + entry.get("skin_or_usage", "未知") +
                         "：`" + entry["file"] + "` → `" + entry["unity_import_path"] +
                         "`；" + entry.get("atlas_layout", rig.get("parts_layout", "未知")) +
                         "；SHA-256 `" + entry["sha256"] + "`。")
    lines += ["- 正背保留基础手臂；纯侧向与四斜向切换对应侧臂，左视角整体镜像；八向头身与闭眼差分分别见004/003批次。",
              "- 静态代码引用：`HumanCommonRigView.Resources` 在默认 ModularRig 驱动下读取运行图；Unity 实际加载与缩放仍待人工确认。", ""]
    if added or changed or removed or not CHANGE_REPORT.exists():
        CHANGE_REPORT.write_text("\n".join(lines), encoding="utf-8")
    print(f"Wrote {rel(INVENTORY)} ({len(rows)} rows) and {rel(CHANGE_REPORT)}; previous snapshot: {'yes' if old is not None else 'no'}")


if __name__ == "__main__":
    main()
