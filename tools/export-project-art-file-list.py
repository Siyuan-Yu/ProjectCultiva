#!/usr/bin/env python3
"""Export a lightweight, project-wide index of files relevant to art production.

The index contains paths and metadata only; it does not embed image binaries or
claim that a file is accepted for runtime. The 273 TSV remains the asset-family
status authority, while current-art-inventory.tsv remains the detailed art-file
hash inventory.
"""

from __future__ import annotations

import csv
import re
from collections import Counter
from datetime import datetime
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
OUTPUT = ROOT / "ArtSource/Reports/project-wide-art-file-list.md"
INVENTORY = ROOT / "ArtSource/Reports/current-art-inventory.tsv"
ASSET_ID = re.compile(r"ART-(?:CHAR|ANIM|ROOM|GROUND|UI|FX|REF)-\d{3}")
IMAGE_EXT = {".png", ".jpg", ".jpeg", ".webp", ".gif", ".psd", ".tga", ".tif", ".tiff", ".svg", ".ase"}
ART_DOC_PREFIXES = (
    "46-demo-v0.1-art-assets", "47-demo-v0.1-ai-art-batches",
    "48-demo-v0.1-minimum-art-integration", "133-manual-art-editor-and-cleanup",
    "273-current-art-asset-register", "274-art-preparation-scope-spec-and-handoff",
    "275-current-art-integration-report", "276-character-art-sleep-and-source-audit",
    "276-art-tech-route-spine-frame-static", "277-art-second-code-review",
    "277-artsource-identical-images", "277-huangcun-art-coverage",
    "277-roster-art-coverage", "278-human-common-rig-spine-sample-inputs",
    "279-human-common-rig-roster-migration", "280-animation-binding-audit-and-fix",
    "281-project-experience-and-art-gap-audit", "282-character-locomotion-and-tool-action-polish",
    "283-character-directional-gait-and-nonblocking-overlap", "284-character-actual-driver-and-unified-work-binding",
    "285-human-rig-joint-registration-and-pose-repair", "286-character-head-pairing-and-ground-vitals",
    "287-character-head-body-proportion-polish", "288-character-approach-spacing-and-soft-avoidance",
    "289-character-rest-pose-bed-layering-and-eye-policy", "290-bed-occupant-visibility-and-rest-selection",
    "291-character-art-eight-step-status-audit", "292-character-closed-eye-head-variants",
    "293-character-eight-direction-movement", "294-eight-direction-head-neck-registration-repair",
    "295-side-facing-arm-presentation", "296-right-click-repath-position-continuity",
    "297-art-rig-and-source-cleanup-current-handoff",
)
ART_TOOLS = {
    "audit-art-presentation.py", "export-art-inventory.py", "export-art-register.mjs",
    "organize-art-inbox.py", "rebuild-codex001-previews.py",
    "retire-artsource-slimming-2026-10-17.py", "export-project-art-file-list.py",
    "export-human-rig-layouts.py", "export-human-rig-diagonal-layouts.py",
    "export-human-rig-side-arms.py",
}
VISUAL_CODE = re.compile(
    r"SpriteRenderer|Sprite\.Create|Texture2D|Resources\.Load<\s*(?:Sprite|Texture2D)|"
    r"HumanCommonRig|HostStepAArt|HostArtDepth|sortingOrder|MeshRenderer|LineRenderer"
)
EXTRA_CODE = {
    "HostMoveController.cs", "MapLayoutPrefabResolver.cs", "MapLayoutPrefabCatalog.cs",
    "MapLayoutPresentationSync.cs", "PlayableHostCameraRig.cs",
    "HostFarmFieldLabor.cs", "HostDestructibleAssault.cs",
}


def relative(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def read_inventory() -> dict[str, str]:
    if not INVENTORY.exists():
        return {}
    with INVENTORY.open(encoding="utf-8-sig", newline="") as stream:
        return {row["文件路径"]: row["AssetID"] for row in csv.DictReader(stream, delimiter="\t")}


def select_file(path: Path) -> tuple[str, str] | None:
    rel = relative(path)
    name = path.name
    if path == OUTPUT or "__pycache__" in path.parts or path.suffix == ".pyc":
        return None
    if rel.startswith("ArtSource/"):
        if rel.startswith("ArtSource/Reports/"):
            return "美术清单与审查记录", "文件级盘点/变更记录；不是273资产族状态真源"
        if "/Originals/" in rel or rel.startswith("ArtSource/References/"):
            return "美术原图与参考", "绘画参考或生成原图；是否可作运行部件须看批次记录"
        if path.suffix.lower() in IMAGE_EXT:
            return "美术派生图与预览", "图集、帧、切片或预览；旧批次可能仅供迁移参考"
        return "美术批次与生产资料", "manifest、提示词、加工脚本或历史生产记录"
    if rel.startswith("Assets/Art/") or rel == "Assets/Art.meta":
        if name.endswith(".meta"):
            return "Unity美术导入元数据", "保留GUID和导入设置；不是绘画内容"
        return "Unity美术资源", "项目内图片、材质或资源目录；具体接入状态见273/文件清单"
    if rel.startswith("Assets/Prefabs/") or rel == "Assets/Prefabs.meta":
        return "Unity视觉预制体", "人物、场景物件、地块或选中圈的显示装配"
    if rel.startswith("Assets/Scenes/") or rel == "Assets/Scenes.meta":
        return "Unity场景", "画面/布局参考；LevelTester是当前人工主验收场景"
    if rel.startswith("Assets/Resources/MapLayoutPrefabCatalog.asset"):
        return "Unity美术装配目录", "地图物件预制体映射"
    if rel.startswith("Assets/LevelTester/Maps/") or rel == "Assets/LevelTester/Maps.meta":
        return "场景制作说明", "LevelTester地图素材/布局说明"
    if rel == "Assets/Prefabs/Environment/Tiles/MissingPrefabPlaceholder.png":
        return "Unity美术资源", "缺失预制体的显示占位"
    if rel.startswith("Assets/Editor/DemoPrototypeBuilder.cs"):
        return "Unity视觉装配代码", "历史Demo构建工具；不代表当前正式画风"
    if rel.startswith("Assets/Scripts/") and (name.endswith(".cs") or name.endswith(".cs.meta")):
        source = path if name.endswith(".cs") else Path(str(path)[:-5])
        if not source.exists():
            return None
        if not ("/Unity/" in rel or "/Runtime/Presentation/" in rel or "/Runtime/UI/" in rel or "/Runtime/World/" in rel):
            return None
        code = source.read_text(encoding="utf-8-sig", errors="ignore")
        if source.name in EXTRA_CODE or VISUAL_CODE.search(code):
            label = "Unity视觉接线代码" if "/Unity/" in rel else "历史Demo视觉代码"
            return label, "用于理解Sprite/部件、排序、场景或界面显示；不能当作待绘图片"
    if rel.startswith("Assets/Tests/") and ("ArtPresentation" in name or "ArtSource" in name):
        return "美术相关验证代码", "历史/现有静态或自动验证文件；不代表人工视觉验收"
    if rel.startswith("docs/40-process/") and (name.startswith(ART_DOC_PREFIXES) or name == "42-devlog.md"):
        return "美术规划与审查文档", "273 TSV 是资产族真源；276/279/280是当前人物技术、接线与修复说明"
    if rel.startswith("tools/") and name in ART_TOOLS:
        return "美术清单与加工工具", "本地导出、整理、审查或切片脚本"
    return None


def main() -> None:
    inventory_ids = read_inventory()
    selected: list[tuple[str, str, str, int, str]] = []
    for folder in ("ArtSource", "Assets", "docs", "tools"):
        for path in (ROOT / folder).rglob("*"):
            if not path.is_file():
                continue
            picked = select_file(path)
            if picked is None:
                continue
            category, note = picked
            rel = relative(path)
            ids = inventory_ids.get(rel) or ",".join(sorted(set(ASSET_ID.findall(rel)))) or "—"
            selected.append((category, rel, ids, path.stat().st_size, note))
    selected.sort(key=lambda row: (row[0], row[1].casefold()))
    counts = Counter(category for category, *_ in selected)
    image_count = sum(Path(rel).suffix.lower() in IMAGE_EXT for _, rel, *_ in selected)
    lines = [
        "# PJCultiva 全项目美术相关文件列表", "",
        f"> 生成日期：{datetime.now().astimezone():%Y-%m-%d}。共 {len(selected)} 个文件，其中图像 {image_count} 个。全部为项目相对路径；不包含图像二进制。",
        "",
        "这份列表供绘画与美术交接使用，覆盖 ArtSource、Unity 美术资源、预制体/场景、表现接线代码及美术规划文档。目录元数据 `.meta` 也列出，以便定位 Unity 资源绑定。`Library`、`Temp`、缓存、日志、普通玩法代码和无关设计文档不列入。",
        "",
        "**阅读顺序**：先看 `docs/40-process/273-current-art-asset-register.tsv`（55个资产族真源）、`docs/40-process/276-art-tech-route-spine-frame-static-v1-2026-10-16.md`（当前人物路线）、`docs/40-process/278-human-common-rig-spine-sample-inputs-2026-10-16.md`（三Skin样机）、`ArtSource/Batches/Batch_ModularRig_001/manifest.json`，再按 AssetID 找原图与运行图。旧高精人物PNG是设定参考，旧四方向逐帧是运行回退；它们不等同于合格的新部件。Spine不是当前必需路线。",
        "",
        "制作/接入/验收状态以273为准；文件大小和路径仅证明文件存在，不表示已经在Unity运行或通过人工视觉验收。逐个美术文件的SHA-256、来源和引用详情仍见 `ArtSource/Reports/current-art-inventory.tsv`。",
        "", "## 分类数量", "",
        "| 类别 | 文件数 |", "| --- | ---: |",
    ]
    lines += [f"| {category} | {count} |" for category, count in sorted(counts.items())]
    lines += ["", "## 文件列表", "", "| 类别 | AssetID | 项目相对路径 | 大小（字节） | 用途提示 |", "| --- | --- | --- | ---: | --- |"]
    for category, rel, ids, size, note in selected:
        lines.append(f"| {category} | {ids} | `{rel}` | {size} | {note} |")
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"Wrote {relative(OUTPUT)}: {len(selected)} files, {image_count} images")


if __name__ == "__main__":
    main()
