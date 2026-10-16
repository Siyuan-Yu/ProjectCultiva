"""Representative room/road previews and experimental soil/plant layer separation."""
from pathlib import Path
from hashlib import sha256
import json
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
STATIC = HERE / "Static"
PREVIEW = HERE / "Previews"
CODEX1 = ROOT / "ArtSource/Batches/Batch_Codex_001/Originals"


def room_preview():
    room = Image.new("RGBA", (760, 720), (203, 208, 196, 255))
    parts = (("ART-ROOM-001_floor_interior_v02", (120, 135, 520, 460)),
             ("ART-ROOM-002_wall_straight_v02", (120, 110, 520, 95)),
             ("ART-ROOM-002_corner_left_v02", (100, 110, 140, 245)),
             ("ART-ROOM-002_corner_right_v02", (520, 110, 140, 245)),
             ("ART-ROOM-003_door_open_v02", (250, 550, 270, 120)),
             ("ART-ROOM-006_bed_residential_v02", (320, 300, 100, 210)))
    for name, (x, y, w, h) in parts:
        part = Image.open(STATIC / f"{name}.png").convert("RGBA").resize((w, h), Image.Resampling.LANCZOS)
        room.alpha_composite(part, (x, y))
    ImageDraw.Draw(room).text((15, 15), "SCHEMATIC ONLY - not collision/grid validated", fill=(24, 31, 29))
    path = PREVIEW / "ART-ROOM-001_002_003_006_assembly_schematic_v02.png"
    room.save(path)
    return path.relative_to(ROOT).as_posix()


def road_preview():
    preview = Image.new("RGBA", (900, 550), (203, 211, 188, 255))
    for name, positions in (("vertical", ((80, 45), (80, 285))),
                             ("horizontal", ((410, 95), (650, 95)))):
        part = Image.open(STATIC / f"ART-GROUND-002_road_{name}_v02.png").convert("RGBA")
        part = part.resize((240, 240), Image.Resampling.LANCZOS)
        for pos in positions:
            preview.alpha_composite(part, pos)
    path = PREVIEW / "ART-GROUND-002_road_repeat_seam_v02.png"
    preview.save(path)
    return path.relative_to(ROOT).as_posix()


def field_layers(asset, stem):
    source = CODEX1 / f"{asset}_{stem}_4stage_sheet_v01.png"
    image = Image.open(source).convert("RGBA")
    cellw = image.width // 4
    stage_names = ("empty", "sprout", "growing", "mature")
    region = (64, 116, 490, 558)
    # Soil is always the same empty-stage pixels. Other source soil is discarded.
    soil = image.crop(region)
    soil_path = STATIC / f"{asset}_soil_base_v02.png"
    soil.save(soil_path)
    records = []
    strip = Image.new("RGBA", (4 * soil.width, soil.height), (202, 208, 195, 255))
    for stage in range(4):
        crop = image.crop((stage * cellw + region[0], region[1],
                           stage * cellw + region[2], region[3]))
        pix = np.asarray(crop).copy()
        rgb = pix[:, :, :3].astype(np.float32)
        r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
        if asset.endswith("010"):
            green = (g > r * 1.08) & (g > b * 1.15) & (g > 72)
            gold = (r > 172) & (g > 135) & (b < 110) & (r > g * 1.10)
            mask = (green | (gold if stage >= 2 else False)) if stage else np.zeros(g.shape, dtype=bool)
        else:
            teal = (g > r * .97) & (b > r * .78) & (g > 95) & (b > 65)
            violet = (b > r * 1.12) & (b > 115)
            mask = (teal | violet) if stage else np.zeros(g.shape, dtype=bool)
        mask &= pix[:, :, 3] > 30
        # Keep source strokes adjacent to the chromatic core, but do not include
        # the other stage's soil. This is a candidate, not a clean matte.
        plant_mask = Image.fromarray((mask.astype(np.uint8) * 255)).filter(ImageFilter.MaxFilter(5))
        alpha = np.minimum(np.asarray(plant_mask), pix[:, :, 3])
        pix[:, :, 3] = alpha
        overlay = Image.fromarray(pix, "RGBA")
        layer_path = STATIC / f"{asset}_plant_{stage_names[stage]}_v02.png"
        overlay.save(layer_path)
        composed = soil.copy()
        composed.alpha_composite(overlay)
        strip.alpha_composite(composed, (stage * soil.width, 0))
        records.append({"stage": stage_names[stage], "file": layer_path.relative_to(ROOT).as_posix(),
                        "nonempty": bool(alpha.max()), "source_region_xyxy": [stage * cellw + region[0],
                          region[1], stage * cellw + region[2], region[3]],
                        "sha256": sha256(layer_path.read_bytes()).hexdigest()})
    preview_path = PREVIEW / f"{asset}_shared_soil_plant_overlay_v02.png"
    strip.save(preview_path)
    return {"asset_id": asset, "source": source.relative_to(ROOT).as_posix(),
            "source_sha256": sha256(source.read_bytes()).hexdigest(),
            "soil_base": soil_path.relative_to(ROOT).as_posix(),
            "preview": preview_path.relative_to(ROOT).as_posix(), "layers": records,
            "status": "experimental chroma isolation, not clean art/Unity approved",
            "issue": "Source stages have different soil; chroma matte needs visual repair and crop bounds QA."}


def ui_small_preview():
    names = ["ART-UI-002_hunger_v01", "ART-UI-002_stamina_v01", "ART-UI-002_dying_v01",
             "ART-UI-002_sleep_v01", "ART-UI-002_escorted_v01", "ART-UI-003_rough_wood_v01",
             "ART-UI-003_jiang_manual_v01", "ART-UI-003_dongfu_manual_v01",
             "ART-UI-003_liezhao_art_v01", "ART-UI-003_kaishan_art_v01"]
    preview = Image.new("RGBA", (800, 240), (33, 42, 48, 255))
    d = ImageDraw.Draw(preview)
    for i, name in enumerate(names):
        source = Image.open(STATIC / f"{name}.png").convert("RGBA")
        icon = source.resize((32, 32), Image.Resampling.LANCZOS)
        x = (i % 5) * 160 + 60
        y = (i // 5) * 115 + 30
        preview.alpha_composite(icon, (x, y))
        d.text((x - 30, y + 40), name.split("_")[-2][:14], fill=(238, 234, 217))
    path = PREVIEW / "ART-UI-002_003_32px_readability_v02.png"
    preview.save(path)
    return path.relative_to(ROOT).as_posix()


def main():
    info = {"room": room_preview(), "road": road_preview(), "ui_32px": ui_small_preview(),
            "fields": [field_layers("ART-GROUND-010", "grain"),
                       field_layers("ART-GROUND-011", "herb")]}
    (HERE / "composites_manifest.json").write_text(json.dumps(info, ensure_ascii=False, indent=2), encoding="utf-8")
    print("wrote room, road, grain, herb previews")


if __name__ == "__main__":
    main()
