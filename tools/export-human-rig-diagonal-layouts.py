"""Export the approved diagonal four-part atlases as Unity Resources/layouts.

The PNGs are copied unchanged. Alpha is read only to locate each authored cell;
no pixels are generated or retouched by this exporter.
"""

from __future__ import annotations

import hashlib
import json
import shutil
import uuid
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "ArtSource/Batches/Batch_ModularRig_004/Originals"
TARGET = ROOT / "Assets/Art/Characters/Resources/Diagonal"
SKINS = (
    "ART-CHAR-001", "ART-CHAR-002", "ART-CHAR-004",
    "ART-CHAR-002_companion_a", "ART-CHAR-002_companion_b",
    "ART-CHAR-003_supervisor", "ART-CHAR-005_hoe", "ART-CHAR-005_apron",
    "ART-CHAR-005_basket", "ART-CHAR-006_merchant",
    "ART-CHAR-007_other", "ART-CHAR-007_jiang_lao",
)


def guid_meta(template: str) -> str:
    lines = template.splitlines()
    return "\n".join(
        "guid: " + uuid.uuid4().hex if line.startswith("guid: ") else line
        for line in lines
    ) + "\n"


def write_meta_once(path: Path, template: str) -> None:
    if not path.exists():
        path.write_text(guid_meta(template), encoding="utf-8")


def part_for(image: Image.Image, column: int, row: int, skin: str) -> dict:
    width, height = image.size
    left, top = column * width // 2, row * height // 2
    right, bottom = (column + 1) * width // 2, (row + 1) * height // 2
    # Generated sheets contain 1-alpha dust far from the painted pieces. A
    # plain alpha getbbox made the back head occupy only ~68% of its Sprite
    # rectangle, shrinking it and moving its neck far above the torso socket.
    visible = image.crop((left, top, right, bottom)).getchannel("A").point(
        lambda alpha: 255 if alpha >= 16 else 0
    )
    bbox = visible.getbbox()
    if bbox is None:
        raise ValueError(f"Missing visible piece: {skin} column={column} row={row}")
    x = max(left, left + bbox[0] - 2)
    y = max(top, top + bbox[1] - 2)
    x2 = min(right, left + bbox[2] + 2)
    y2 = min(bottom, top + bbox[3] + 2)
    torso = column == 0
    pivot_x = 0.5
    if not torso:
        # The neck/chin is the painted shape at the bottom of each head. Its
        # horizontal centre varies with hair, age and three-quarter facing.
        neck = visible.crop((bbox[0], bbox[1] + round((bbox[3] - bbox[1]) * .9),
                             bbox[2], bbox[3]))
        # Image projection is clearer than a fixed .54: sum occupied alpha
        # pixels in the lowest 10% of the visible head.
        neck_width, neck_height = neck.size
        counts = [sum(1 for yy in range(neck_height) if neck.getpixel((xx, yy)))
                  for xx in range(neck_width)]
        total = sum(counts)
        neck_center = bbox[0] + sum(xx * count for xx, count in enumerate(counts)) / total if total else (bbox[0] + bbox[2]) / 2
        pivot_x = round(max(.35, min(.65, (neck_center + left - x) / (x2 - x))), 4)
    part = {
        "x": x, "y": y, "width": x2 - x, "height": y2 - y,
        "pivotX": pivot_x,
        "pivotY": 0.5 if torso else 0.06,
        "worldHeight": 0.86 if torso else 0.74,
        "gripX": 0.5, "gripY": 0.08,
    }
    if torso:
        # Crop the painted sleeve stubs from the torso's visible mesh. The
        # shared bone arms draw those sleeves and hands at their real sockets.
        contour = ((0.36, 1.0), (0.64, 1.0), (0.75, 0.82),
                   (0.71, 0.60), (0.85, 0.05), (0.50, 0.0),
                   (0.15, 0.05), (0.29, 0.60), (0.25, 0.82))
        vertices = ((0.50, 0.50),) + contour
        triangles = []
        for index in range(1, len(vertices)):
            triangles.extend((0, index, 1 if index == len(vertices) - 1 else index + 1))
        part.update({
            "hasSockets": True,
            "neckSocket": {"x": 0.52, "y": 0.94},
            "leftShoulderSocket": {"x": 0.22, "y": 0.79},
            "rightShoulderSocket": {"x": 0.78, "y": 0.79},
            "meshVertices": [{"x": x, "y": y} for x, y in vertices],
            "meshTriangles": triangles,
        })
    # The hoe worker's front quarter view was authored looking left. Normalize
    # its head and torso together before the full west-facing rig flip.
    if skin == "ART-CHAR-005_hoe" and row == 0:
        part["mirrorX"] = True
    return part


def main() -> None:
    TARGET.mkdir(parents=True, exist_ok=True)
    folder_meta = (ROOT / "Assets/Art/Characters/Resources/ClosedEyes.meta").read_text(encoding="utf-8")
    write_meta_once(TARGET.with_name(TARGET.name + ".meta"), folder_meta)
    texture_meta = (ROOT / "Assets/Art/Characters/Resources/ART-CHAR-002_HumanCommonRig_parts_v01.png.meta").read_text(encoding="utf-8")
    layout_meta = (ROOT / "Assets/Art/Characters/Resources/ART-CHAR-002_HumanCommonRig_parts_v01_layout.json.meta").read_text(encoding="utf-8")
    records = []
    for skin in SKINS:
        name = skin + "_HumanCommonRig_diagonal_v01"
        src = SOURCE / (name + ".png")
        dst = TARGET / (name + ".png")
        shutil.copyfile(src, dst)
        with Image.open(src) as image:
            if image.mode != "RGBA":
                raise ValueError(f"Expected RGBA atlas: {src}")
            # LayoutSprites consumes front torso, front head, back torso, back head.
            parts = [part_for(image, col, row, skin) for row in range(2) for col in range(2)]
            dimensions = image.size
        digest = hashlib.sha256(src.read_bytes()).hexdigest()
        layout = {
            "source": src.relative_to(ROOT).as_posix(),
            "sourceSha256": digest,
            "parts": parts,
        }
        (TARGET / (name + "_layout.json")).write_text(json.dumps(layout, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        write_meta_once(TARGET / (name + ".png.meta"), texture_meta)
        write_meta_once(TARGET / (name + "_layout.json.meta"), layout_meta)
        records.append({"skin": skin, "source": layout["source"], "sha256": digest, "size": dimensions})
    manifest = {"batch": "Batch_ModularRig_004", "purpose": "eight-direction diagonal head/torso views", "resources": records,
                "partOrder": ["southeastTorso", "southeastHead", "northeastTorso", "northeastHead"]}
    (SOURCE.parent / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Exported {len(records)} four-part diagonal atlases")


if __name__ == "__main__":
    main()
