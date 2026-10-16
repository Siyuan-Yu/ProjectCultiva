"""Assemble the immutable batch evidence and mechanically update the 273 TSV truth."""
from pathlib import Path
from hashlib import sha256
import csv
import io
import json

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
REGISTER = ROOT / "docs/40-process/273-current-art-asset-register.tsv"
REL = "ArtSource/Batches/Batch_Codex_002"


def digest(path):
    return sha256(path.read_bytes()).hexdigest()


def rel(path):
    return path.relative_to(ROOT).as_posix()


def make_manifest():
    character = json.loads((HERE / "character_manifest.json").read_text(encoding="utf-8"))
    qa = json.loads((HERE / "character_qa.json").read_text(encoding="utf-8"))
    static = json.loads((HERE / "static_manifest.json").read_text(encoding="utf-8"))
    composites = json.loads((HERE / "composites_manifest.json").read_text(encoding="utf-8"))
    behavior = json.loads((HERE / "behavior_samples_manifest.json").read_text(encoding="utf-8"))
    generated = []
    for path in sorted((HERE / "Originals").glob("*.png")):
        generated.append({"file": rel(path), "sha256": digest(path),
                          "kind": "built-in image_gen original/edit, not accepted or Unity integrated"})
    all_frames = list((HERE / "Frames").rglob("*.png"))
    static_cutouts = [ROOT / x["file"] for x in static["cutouts"]]
    fields = [ROOT / layer["file"] for field in composites["fields"] for layer in field["layers"]]
    manifest = {
        "schema_version": 2, "batch_id": "Batch_Codex_002",
        "canonical_register": "docs/40-process/273-current-art-asset-register.tsv",
        "style_reference": "ArtSource/References/ART-REF-001_huangcun_style_reference.png",
        "generator": "Codex built-in image_gen, medium-detail prompting; no medium quality parameter",
        "generation_prompts": f"{REL}/generation_prompts.md",
        "scope": "art preparation only; no Unity Assets/Content/Prefab/meta/GUID changes",
        "generated_originals": generated,
        "counts": {"new_generated_originals": len(generated),
                   "processed_animation_sheets": len(character["entries"]),
                   "animation_single_frames": len(all_frames),
                   "static_cutouts_in_manifest": len(static_cutouts),
                   "experimental_field_plant_layers": len(fields)},
        "animation_manifest": f"{REL}/character_manifest.json",
        "animation_qa": f"{REL}/character_qa.json",
        "animation_technical_checks_pass": qa["all_technical_checks_pass"],
        "static_manifest": f"{REL}/static_manifest.json",
        "composites_manifest": f"{REL}/composites_manifest.json",
        "behavior_samples_manifest": f"{REL}/behavior_samples_manifest.json",
        "appearance_action_matrix": f"{REL}/appearance_action_matrix.md",
        "corrected_core": {"file": f"{REL}/Static/ART-ROOM-008_core_isolated_v03.png",
                           "sha256": digest(HERE / "Static/ART-ROOM-008_core_isolated_v03.png"),
                           "source": "ArtSource/Batches/Batch_Codex_001/Originals/ART-ROOM-008_flat_core_flags_sheet_v01.png",
                           "status": "transparent edited candidate; identity/size not Unity checked"},
        "remaining_p0": [
            "ART-CHAR-002/003/005: only south static identity candidates for distinct outfits/hair; four-direction action coverage absent",
            "ART-ANIM-003: actual carried appearance sample exists but mount/occlusion not validated",
            "ART-ROOM-001/002/003: schematic assembly only; real grid dimensions, door opening and seamless floor not verified",
            "ART-GROUND-002: road repeat preview shows a visible seam",
            "ART-GROUND-010/011: separated plant overlays are experimental, with holes/soil color remnants; not ready",
            "ART-UI-001: generated frame only; 9-slice bounds and inventory/character panel layouts not validated",
            "ART-UI-002/003: 32px preview exists, final HUD legibility and item mapping not Unity validated",
            "ART-UI-007: marker and flag cutouts exist; scale/occlusion and world anchoring not validated"
        ],
        "status": "mixed prepared/failed candidates; none Unity integrated or accepted"
    }
    (HERE / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    return manifest


UPDATES = {
    "ART-CHAR-001": ("character_manifest.json", "Idle/Walk/Work 36 帧按完整人物重提，另补徒手 Attack 16 帧；技术检查通过，尚待人工/Unity 验证。"),
    "ART-CHAR-002": ("Static/ART-CHAR-002_protagonist_idle_south_v02.png", "三主角南向静态差分已切；其他方向和动作保持缺口，不是完整动画。"),
    "ART-CHAR-003": ("Static/ART-CHAR-003_supervisor_idle_south_v02.png", "主管南向官帽外观已切；其他方向和动作未完成。"),
    "ART-CHAR-004": ("Sheets/ART-CHAR-004_attack_4dir_4f_sheet_v02.png", "36 帧按完整人物/长枪重提；技术检查通过；动作切换预览在 Batch_Codex_002，尚待人工/Unity 验证。"),
    "ART-CHAR-005": ("Previews/static_cutouts_contact_v02.jpg", "背篓/围裙/锄具三种真实南向差分已切；后两者多方向动作缺。"),
    "ART-ANIM-001": ("character_manifest.json", "复用 ART-CHAR-001/004 的 Idle/Walk 结构，不重复计为新动画；不同外观帧覆盖未齐。"),
    "ART-ANIM-002": ("Sheets/ART-CHAR-001_work_4dir_4f_sheet_v02.png", "通用 Work 16 帧基础候选；工具/角色外观差分未齐，不重复生成一套 ANIM。"),
    "ART-ANIM-003": ("Previews/ART-ANIM-003_carry_actual_appearance_mount_sample_v02.png", "实际被搬运者外观组合样本已出；挂点/遮挡未验证，非运行资产。"),
    "ART-ANIM-004": ("Sheets/ART-CHAR-001_attack_4dir_4f_sheet_v02.png", "共用徒手 Attack 与巡卫长枪 Attack 各 16 帧；其他身份外观覆盖未齐。"),
    "ART-ANIM-005": ("Previews/ART-ANIM-005_static_state_transform_sample_v02.png", "同一人物睡眠/弥留/尸体静态变换样本；床位对齐未验证。"),
    "ART-ROOM-001": ("Static/ART-ROOM-001_floor_interior_v02.png", "地板独立切件及开面房拼接示意已出；地板边缘/真实格距未通过。"),
    "ART-ROOM-002": ("Static/ART-ROOM-002_wall_straight_v02.png", "墙直段与左右墙角独立切件；拼接示意非真实格距验证。"),
    "ART-ROOM-003": ("Static/ART-ROOM-003_door_open_v02.png", "门洞独立切件；真实通行格宽度未验证。"),
    "ART-ROOM-006": ("Static/ART-ROOM-006_bed_residential_v02.png", "住宅/医疗/囚禁三床同向切件；用途色、枕端、占地一致性仍待运行尺寸核验。"),
    "ART-ROOM-007": ("Static/ART-ROOM-007_storage_sacks_v03.png", "旧图货物/标识拆件候选；摆位与库存 UI 仍待核。"),
    "ART-ROOM-008": ("Static/ART-ROOM-008_core_isolated_v03.png", "核心 v02 有渐变背景，v03 AI 透明修版；三色旗独立切件，阵营映射未核。"),
    "ART-ROOM-010": ("Static/ART-ROOM-010_cave_entrance_v02.png", "旧图洞口/山壁子件已切，碰撞/接近点未核。"),
    "ART-GROUND-001": ("Static/ART-GROUND-001_grass_tile_seam_candidate_v02.png", "新草地原图经边缘融合；2×2 接缝预览可见，实际地表缩放未验。"),
    "ART-GROUND-002": ("Previews/ART-GROUND-002_road_repeat_seam_v02.png", "道路六子件已拆；直段拼接有明显接缝，未通过。"),
    "ART-GROUND-007": ("Static/ART-GROUND-007_tree_medium_v02.png", "树 S/M/L 从现有原图独立分离；占地/遮挡待核。"),
    "ART-GROUND-009": ("Static/ART-GROUND-009_ore_base_v02.png", "矿堆独立切件；采集状态与占地待核。"),
    "ART-GROUND-010": ("Previews/ART-GROUND-010_shared_soil_plant_overlay_v02.png", "农田共用底土与植株色度分离实验；残土/叶片漏洞，未通过。"),
    "ART-GROUND-011": ("Previews/ART-GROUND-011_shared_soil_plant_overlay_v02.png", "药田共用底土与植株色度分离实验；幼苗/叶片漏洞，未通过。"),
    "ART-UI-001": ("Originals/ART-UI-001_hud_frame_v01.png", "新正式 HUD 九宫格框候选；文字数值留空，背包/人物面板尺寸未验。"),
    "ART-UI-002": ("Previews/ART-UI-002_003_32px_readability_v02.png", "饥饿/精力/弥留/睡眠/押送五图标已拆，32px 预览待制作人确认。"),
    "ART-UI-003": ("Previews/ART-UI-002_003_32px_readability_v02.png", "按当前 5 个 Item 定义制作图标并拆件；实际 ItemId 映射未接。"),
    "ART-UI-007": ("Static/ART-UI-007_interact_rune_v01.png", "交互符文与地点石牌新候选；旗帜复用 ROOM-008，动态文字不烘焙。"),
}


def update_register():
    raw = REGISTER.read_bytes()
    if not raw.startswith(b"\xef\xbb\xbf"):
        raise ValueError("273 TSV must have UTF-8 BOM")
    text = raw[3:].decode("utf-8")
    rows = list(csv.DictReader(io.StringIO(text, newline=""), delimiter="\t"))
    headers = list(rows[0])
    seen = set()
    for row in rows:
        asset = row["AssetID"]
        if asset in UPDATES:
            if asset == "ART-CHAR-001":
                row["备注与替换要求"] = row["备注与替换要求"].replace(
                    "；Batch_Codex_002：36 帧按完整人物重提；技术检查通过；动作切换预览在 Batch_Codex_002，尚待人工/Unity 验证。", "")
            if asset == "ART-ANIM-004":
                row["备注与替换要求"] = row["备注与替换要求"].replace(
                    "；Batch_Codex_002：巡卫长枪 Attack 16 帧样本；其他外观攻击覆盖未齐。", "")
            short_path, note = UPDATES[asset]
            full_path = f"{REL}/{short_path}"
            if full_path not in row["当前文件路径"]:
                old = row["当前文件路径"].strip()
                row["当前文件路径"] = (old + "；" if old else "") + full_path
            if "Batch_Codex_002" not in row["来源与授权"]:
                row["来源与授权"] += "；Batch_Codex_002 内置生图/原图加工候选，授权待制作人核"
            if note not in row["备注与替换要求"]:
                row["备注与替换要求"] += "；Batch_Codex_002：" + note
            if asset.startswith("ART-UI-00") or asset == "ART-GROUND-001":
                row["品质阶段"] = "AI首版"
            seen.add(asset)
    if seen != set(UPDATES):
        raise ValueError(f"missing AssetIDs: {set(UPDATES)-seen}")
    output = io.StringIO(newline="")
    writer = csv.DictWriter(output, fieldnames=headers, delimiter="\t", lineterminator="\n")
    writer.writeheader()
    writer.writerows(rows)
    REGISTER.write_bytes(b"\xef\xbb\xbf" + output.getvalue().encode("utf-8"))
    return len(rows), len(seen)


if __name__ == "__main__":
    manifest = make_manifest()
    total, edited = update_register()
    print(f"generated={manifest['counts']['new_generated_originals']}, sheets={manifest['counts']['processed_animation_sheets']}, animation_frames={manifest['counts']['animation_single_frames']}, static_cutouts={manifest['counts']['static_cutouts_in_manifest']}; register rows={total}, updated={edited}")
