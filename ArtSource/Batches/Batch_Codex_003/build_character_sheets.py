"""Rebuild identity-preserving four-direction character sheets from separated strips.

Generated source strips remain intact in Originals.  No Unity/Content data is read.
"""
from pathlib import Path
from PIL import Image
import json
import hashlib

ROOT = Path(__file__).resolve().parent
ORIGINALS = ROOT / "Originals"
SHEETS = ROOT / "Sheets"
FRAMES = ROOT / "Frames"
PREVIEWS = ROOT / "Previews"
SHEETS.mkdir(exist_ok=True)
FRAMES.mkdir(exist_ok=True)
PREVIEWS.mkdir(exist_ok=True)

SOURCES = {
    "protagonist": "ART-CHAR-002",
    "companion_a": "ART-CHAR-002",
    "companion_b": "ART-CHAR-002",
}
DIRECTIONS = ("north", "south", "west", "east")
W, H = 560, 440
FOOT_Y = 392
BODY_H = 310
records = []

def runs(values):
    output = []
    start = None
    for i, value in enumerate(values):
        if value and start is None:
            start = i
        if not value and start is not None:
            output.append((start, i))
            start = None
    if start is not None:
        output.append((start, len(values)))
    return output

def extract_four(source):
    alpha = source.getchannel("A")
    pix = alpha.load()
    counts = [sum(pix[x, y] > 32 for y in range(source.height))
              for x in range(source.width)]
    spans = [span for span in runs([n >= 3 for n in counts])
             if span[1] - span[0] > 40]
    if len(spans) != 4:
        raise ValueError(f"Expected four isolated poses, found {spans}")
    poses = []
    for index, (left, right) in enumerate(spans):
        # Alpha bbox is local to each isolated pose; transparent gaps stay excluded.
        crop = source.crop((left, 0, right, source.height))
        mask = crop.getchannel("A").point(lambda a: 255 if a > 32 else 0)
        bbox = mask.getbbox()
        if bbox is None:
            raise ValueError(f"Pose {index} is empty")
        poses.append((crop.crop(bbox), [left + bbox[0], bbox[1], left + bbox[2], bbox[3]]))
    return poses

for character, asset_id in SOURCES.items():
    walk = Image.new("RGBA", (W * 4, H * 4))
    idle = Image.new("RGBA", (W, H * 4))
    for row, direction in enumerate(DIRECTIONS):
        source_path = ORIGINALS / f"{character}_walk_{direction}_v03.png"
        source = Image.open(source_path).convert("RGBA")
        poses = extract_four(source)
        frame_images = []
        for col, (pose, bbox) in enumerate(poses):
            # One scale for the four-frame action, based on body height only.
            # Source pose width never independently determines size.
            scale = BODY_H / max(p.height for p, _ in poses)
            size = (round(pose.width * scale), round(pose.height * scale))
            if size[0] > W - 32 or size[1] > FOOT_Y - 16:
                raise ValueError(f"Unexpected pose size: {character}/{direction}/{col}: {size}")
            resized = pose.resize(size, Image.Resampling.LANCZOS)
            frame = Image.new("RGBA", (W, H))
            frame.alpha_composite(resized, ((W - size[0]) // 2, FOOT_Y - size[1]))
            frame_images.append(frame)
            walk.alpha_composite(frame, (col * W, row * H))
            frame_path = FRAMES / f"{asset_id}_{character}_walk_{direction}_{col + 1:02d}_v03.png"
            frame.save(frame_path)
            records.append({"path": str(frame_path.relative_to(ROOT)).replace('\\', '/'),
                            "source": str(source_path.relative_to(ROOT)).replace('\\', '/'),
                            "source_sha256": hashlib.sha256(source_path.read_bytes()).hexdigest(),
                            "source_bbox": bbox, "pivot_top": [W // 2, FOOT_Y],
                            "direction": direction, "frame": col + 1})
        # The near-contact walk pose is a deliberate temporary idle reuse for
        # side/back directions; south keeps its separately authored idle cutout.
        idle_frame = frame_images[1]
        if direction == "south":
            reference = Path(__file__).resolve().parents[1] / "Batch_Codex_002" / "Static" / f"{asset_id}_{character}_idle_south_v02.png"
            if reference.exists():
                cutout = Image.open(reference).convert("RGBA")
                bbox = cutout.getchannel("A").getbbox()
                if bbox:
                    cutout = cutout.crop(bbox)
                    factor = BODY_H / cutout.height
                    cutout = cutout.resize((round(cutout.width * factor), BODY_H), Image.Resampling.LANCZOS)
                    idle_frame = Image.new("RGBA", (W, H))
                    idle_frame.alpha_composite(cutout, ((W - cutout.width) // 2, FOOT_Y - BODY_H))
        idle.alpha_composite(idle_frame, (0, row * H))
        idle_path = FRAMES / f"{asset_id}_{character}_idle_{direction}_01_v03.png"
        idle_frame.save(idle_path)
        records.append({"path": str(idle_path.relative_to(ROOT)).replace('\\', '/'),
                        "source": "Batch_Codex_002/Static" if direction == "south" else f"{character}_walk_{direction}_v03 pose 2",
                        "pivot_top": [W // 2, FOOT_Y], "direction": direction, "frame": 1,
                        "note": "temporary near-contact pose reuse" if direction != "south" else "authored south idle"})
        preview = []
        for frame in [idle_frame, idle_frame] + frame_images:
            ground = Image.new("RGBA", (W, H), (224, 217, 204, 255))
            ground.alpha_composite(frame)
            preview.append(ground.convert("RGB"))
        preview[0].save(PREVIEWS / f"{character}_idle_walk_{direction}_v03.gif",
                        save_all=True, append_images=preview[1:], duration=[400, 400, 120, 120, 120, 120],
                        loop=0, disposal=2)
    walk.save(SHEETS / f"{asset_id}_{character}_walk_v03.png")
    idle.save(SHEETS / f"{asset_id}_{character}_idle_v03.png")

SLEEP_ONLY = [("villager", "ART-CHAR-001"), ("guard", "ART-CHAR-004"),
              ("supervisor", "ART-CHAR-003"), ("resident_hoe", "ART-CHAR-005"),
              ("resident_apron", "ART-CHAR-005"), ("resident_basket", "ART-CHAR-005")]
for character, asset_id in list(SOURCES.items()) + SLEEP_ONLY:
    sleep_src = ORIGINALS / f"{character}_sleep_v03.png"
    if not sleep_src.exists():
        continue
    source = Image.open(sleep_src).convert("RGBA")
    mask = source.getchannel("A").point(lambda a: 255 if a > 32 else 0)
    box = mask.getbbox()
    if not box:
        raise ValueError(f"Empty sleep source {sleep_src}")
    image = source.crop(box)
    # Sized for the room bed rendering, independent of map/bed gameplay size.
    target_width = 390
    image = image.resize((target_width, round(image.height * target_width / image.width)), Image.Resampling.LANCZOS)
    image.save(FRAMES / f"{asset_id}_{character}_sleep_v03.png")
    records.append({"path": f"Frames/{asset_id}_{character}_sleep_v03.png",
                    "source": str(sleep_src.relative_to(ROOT)).replace('\\', '/'),
                    "source_sha256": hashlib.sha256(sleep_src.read_bytes()).hexdigest(),
                    "source_bbox": list(box), "pose": "lying, head left"})

(ROOT / "manifest.json").write_text(json.dumps({"version": 3, "canvas": [W, H],
    "direction_order": DIRECTIONS, "foot_anchor_top": [W // 2, FOOT_Y],
    "frame_records": records}, ensure_ascii=False, indent=2), encoding="utf-8")
