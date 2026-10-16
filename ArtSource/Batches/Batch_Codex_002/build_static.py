"""Deterministic source-preserving cutouts and candidate previews outside Unity Assets."""
from pathlib import Path
from hashlib import sha256
import json

import numpy as np
from PIL import Image, ImageDraw
from build_characters import components, extract

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
OLD1 = ROOT / "ArtSource/Batches/Batch_20261016_01/Originals"
OLD2 = ROOT / "ArtSource/Inbox"
CODEX1 = ROOT / "ArtSource/Batches/Batch_Codex_001/Originals"
NEW = HERE / "Originals"
OUT = HERE / "Static"
PREVIEW = HERE / "Previews"

SOURCES = [
    (OLD1 / "ART-CHAR-001_common_villagers_3up_v01.png", [
        ("ART-CHAR-005_resident_basket_idle_south_v02", (0, 0, 480, 1086)),
        ("ART-CHAR-005_resident_apron_idle_south_v02", (480, 0, 970, 1086)),
        ("ART-CHAR-005_resident_hoe_idle_south_v02", (970, 0, 1448, 1086))]),
    (OLD1 / "ART-CHAR-002_protagonist_trio_3up_v01.png", [
        ("ART-CHAR-002_protagonist_idle_south_v02", (0, 0, 480, 1086)),
        ("ART-CHAR-002_companion_a_idle_south_v02", (480, 0, 970, 1086)),
        ("ART-CHAR-002_companion_b_idle_south_v02", (970, 0, 1448, 1086))]),
    (OLD1 / "ART-CHAR-003_supervisor_v01.png", [
        ("ART-CHAR-003_supervisor_idle_south_v02", None)]),
    (OLD1 / "ART-ROOM-001_wood_room_floor_wall_door_sheet_v01.png", [
        ("ART-ROOM-001_floor_interior_v02", (105, 145, 650, 665)),
        ("ART-ROOM-002_wall_straight_v02", (775, 90, 1395, 275)),
        ("ART-ROOM-002_corner_left_v02", (755, 370, 1050, 705)),
        ("ART-ROOM-002_corner_right_v02", (1110, 370, 1410, 705)),
        ("ART-ROOM-003_door_open_v02", (430, 785, 990, 1020))]),
    (OLD1 / "ART-ROOM-006_beds_residential_medical_prison_3up_v01.png", [
        ("ART-ROOM-006_bed_residential_v02", (35, 155, 465, 920)),
        ("ART-ROOM-006_bed_medical_v02", (510, 155, 930, 920)),
        ("ART-ROOM-006_bed_prison_v02", (990, 155, 1430, 920))]),
    (CODEX1 / "ART-GROUND-007_trees_s_m_l_sheet_v01.png", [
        ("ART-GROUND-007_tree_small_v02", (0, 0, 455, 887)),
        ("ART-GROUND-007_tree_medium_v02", (455, 0, 1030, 887)),
        ("ART-GROUND-007_tree_large_v02", (1030, 0, 1774, 887))]),
    (OLD1 / "ART-GROUND-009_spirit_ore_blue_crystal_v01.png", [
        ("ART-GROUND-009_ore_base_v02", None)]),
    (OLD2 / "ART-ROOM-007_storage_goods_markers_sheet_v01.png", [
        ("ART-ROOM-007_storage_sacks_v03", (0, 0, 350, 370)),
        ("ART-ROOM-007_storage_crates_v03", (330, 0, 675, 370)),
        ("ART-ROOM-007_storage_jars_v03", (675, 0, 1000, 370)),
        ("ART-ROOM-007_storage_bales_v03", (990, 0, 1220, 370)),
        ("ART-ROOM-007_storage_sign_v03", (1190, 0, 1448, 370))]),
    (OLD2 / "ART-ROOM-010_cave_entrance_cliff_modules_sheet_v01.png", [
        ("ART-ROOM-010_cave_entrance_v02", (0, 0, 725, 640)),
        ("ART-ROOM-010_cliff_top_v02", (725, 0, 1448, 365))]),
    (CODEX1 / "ART-ROOM-008_flat_core_flags_sheet_v01.png", [
        ("ART-ROOM-008_core_v02", (0, 0, 720, 768)),
        ("ART-ROOM-008_flag_blue_v02", (720, 0, 1110, 768)),
        ("ART-ROOM-008_flag_red_v02", (1160, 0, 1550, 768)),
        ("ART-ROOM-008_flag_green_v02", (1620, 0, 2048, 768))]),
    (CODEX1 / "ART-GROUND-002_road_modules_sheet_v01.png", [
        (f"ART-GROUND-002_road_{name}_v02", ((i % 3) * 512, (i // 3) * 512,
             (i % 3 + 1) * 512, (i // 3 + 1) * 512))
        for i, name in enumerate(("vertical", "horizontal", "turn", "tee", "cross", "end"))]),
    (NEW / "ART-UI-002_status_icons_v01.png", [
        (f"ART-UI-002_{name}_v01", (i * 2172 // 5, 0, (i+1) * 2172 // 5, 724))
        for i, name in enumerate(("hunger", "stamina", "dying", "sleep", "escorted"))]),
    (NEW / "ART-UI-003_item_icons_v01.png", [
        (f"ART-UI-003_{name}_v01", (i * 2156 // 5, 0, (i+1) * 2156 // 5, 729))
        for i, name in enumerate(("rough_wood", "jiang_manual", "dongfu_manual", "liezhao_art", "kaishan_art"))]),
    (NEW / "ART-UI-007_interaction_markers_v01.png", [
        ("ART-UI-007_site_marker_v01", (0, 0, 1024, 832)),
        ("ART-UI-007_interact_rune_v01", (1024, 0, 2048, 832))]),
]


def save_cut(src, name, rect):
    image = Image.open(src).convert("RGBA")
    if rect is None:
        rect = (0, 0, image.width, image.height)
    x0, y0, x1, y1 = rect
    rect = (max(0, x0), max(0, y0), min(image.width, x1), min(image.height, y1))
    cut = image.crop(rect)
    if not name.startswith(("ART-ROOM-001", "ART-ROOM-002", "ART-ROOM-003", "ART-UI-002", "ART-UI-007_interact")):
        figures = components(cut)
        if figures:
            figure = max(figures, key=lambda item: item["area"])
            isolated = extract(cut, figure)
            cut = isolated
    bbox = cut.getchannel("A").getbbox()
    if bbox is None:
        raise ValueError(f"empty: {name}")
    cut = cut.crop(bbox)
    out = OUT / f"{name}.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    cut.save(out)
    alpha = np.asarray(cut.getchannel("A"))
    return {"name": name, "source": src.relative_to(ROOT).as_posix(),
            "source_sha256": sha256(src.read_bytes()).hexdigest(),
            "source_region_xyxy": list(rect), "tight_bounds_in_region": list(bbox),
            "file": out.relative_to(ROOT).as_posix(), "sha256": sha256(out.read_bytes()).hexdigest(),
            "size_px": list(cut.size), "nonempty_alpha": bool(np.any(alpha)),
            "has_transparency": bool(np.any(alpha == 0)),
            "review": "cutout candidate; alignment, clean component separation and Unity dimensions unverified"}


def seamless_grass():
    src = NEW / "ART-GROUND-001_grass_tile_v01.png"
    image = Image.open(src).convert("RGB")
    a = np.asarray(image).copy().astype(np.float32)
    h, w, _ = a.shape
    # Blend matching opposite edges. No crop of the original and no claim that
    # the AI source itself was seamless.
    band = 48
    for d in range(band):
        t = (band - d) / band * .5
        left, right = a[:, d].copy(), a[:, w - 1 - d].copy()
        a[:, d] = left * (1 - t) + right * t
        a[:, w - 1 - d] = right * (1 - t) + left * t
    for d in range(band):
        t = (band - d) / band * .5
        top, bottom = a[d, :].copy(), a[h - 1 - d, :].copy()
        a[d, :] = top * (1 - t) + bottom * t
        a[h - 1 - d, :] = bottom * (1 - t) + top * t
    tile = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGB")
    path = OUT / "ART-GROUND-001_grass_tile_seam_candidate_v02.png"
    tile.save(path)
    preview = Image.new("RGB", (w * 2, h * 2))
    for y in (0, h):
        for x in (0, w):
            preview.paste(tile, (x, y))
    pp = PREVIEW / "ART-GROUND-001_2x2_seam_v02.jpg"
    preview.resize((1000, 1000), Image.Resampling.LANCZOS).save(pp, quality=88)
    q = np.asarray(tile).astype(int)
    edge_x = float(np.abs(q[:, 0] - q[:, -1]).mean())
    edge_y = float(np.abs(q[0] - q[-1]).mean())
    return {"source": src.relative_to(ROOT).as_posix(), "file": path.relative_to(ROOT).as_posix(),
            "preview": pp.relative_to(ROOT).as_posix(), "opposite_edge_mae_x": edge_x,
            "opposite_edge_mae_y": edge_y, "opaque": True,
            "review": "2x2 seam candidate only; generated source is not inherently tileable"}


def contact(records):
    cards = []
    for record in records:
        img = Image.open(ROOT / record["file"]).convert("RGBA")
        img.thumbnail((240, 200), Image.Resampling.LANCZOS)
        card = Image.new("RGB", (270, 240), (226, 225, 215))
        checker = Image.new("RGBA", (240, 200), (205, 205, 200, 255))
        d = ImageDraw.Draw(checker)
        for y in range(0, 200, 20):
            for x in range(0, 240, 20):
                if (x // 20 + y // 20) % 2:
                    d.rectangle((x, y, x + 19, y + 19), fill=(235, 235, 230, 255))
        checker.alpha_composite(img, ((240-img.width)//2, (200-img.height)//2))
        card.paste(checker.convert("RGB"), (15, 8))
        ImageDraw.Draw(card).text((8, 215), record["name"][:34], fill=(25, 25, 25))
        cards.append(card)
    cols = 4
    rows = (len(cards) + cols - 1)//cols
    overview = Image.new("RGB", (cols*270, rows*240), (240,240,236))
    for i, card in enumerate(cards):
        overview.paste(card, ((i%cols)*270, (i//cols)*240))
    path = PREVIEW / "static_cutouts_contact_v02.jpg"
    overview.save(path, quality=90)
    return path


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    PREVIEW.mkdir(parents=True, exist_ok=True)
    records = [save_cut(source, name, rect) for source, specs in SOURCES for name, rect in specs]
    grass = seamless_grass()
    overview = contact(records)
    (HERE / "static_manifest.json").write_text(json.dumps({"cutouts": records,
         "grass": grass, "contact": overview.relative_to(ROOT).as_posix()}, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"static cutouts={len(records)}; grass edge MAE x={grass['opposite_edge_mae_x']:.2f}, y={grass['opposite_edge_mae_y']:.2f}")


if __name__ == "__main__":
    main()
