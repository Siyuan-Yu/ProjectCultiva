"""Export authored side-facing sleeves with shoulder and hand registration."""

from __future__ import annotations

import hashlib
import json
import shutil
import uuid
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "ArtSource/Batches/Batch_ModularRig_005/Originals"
TARGET = ROOT / "Assets/Art/Characters/Resources/SideArms"
SKINS = (
    "ART-CHAR-001", "ART-CHAR-002", "ART-CHAR-004",
    "ART-CHAR-002_companion_a", "ART-CHAR-002_companion_b",
    "ART-CHAR-003_supervisor", "ART-CHAR-005_hoe", "ART-CHAR-005_apron",
    "ART-CHAR-005_basket", "ART-CHAR-006_merchant",
    "ART-CHAR-007_other", "ART-CHAR-007_jiang_lao",
)


def write_meta_once(path: Path, template: str) -> None:
    if path.exists():
        return
    path.write_text("\n".join(
        "guid: " + uuid.uuid4().hex if line.startswith("guid: ") else line
        for line in template.splitlines()
    ) + "\n", encoding="utf-8")


def main() -> None:
    TARGET.mkdir(parents=True, exist_ok=True)
    resources = TARGET.parent
    write_meta_once(TARGET.with_name(TARGET.name + ".meta"),
                    (resources / "Diagonal.meta").read_text(encoding="utf-8"))
    texture_meta = (resources / "ART-CHAR-002_HumanCommonRig_parts_v01.png.meta").read_text(encoding="utf-8")
    layout_meta = (resources / "ART-CHAR-002_HumanCommonRig_parts_v01_layout.json.meta").read_text(encoding="utf-8")
    records = []
    for skin in SKINS:
        name = skin + "_HumanCommonRig_sidearm_v01"
        src = SOURCE / (name + ".png")
        dst = TARGET / (name + ".png")
        with Image.open(src) as image:
            if image.mode != "RGBA":
                raise ValueError(f"Expected RGBA side arm: {src}")
            width, height = image.size
            visible = image.getchannel("A").point(lambda alpha: 255 if alpha >= 16 else 0)
            bbox = visible.getbbox()
            if bbox is None:
                raise ValueError(f"No visible side arm in {src}")
            x, top, right, bottom = bbox
            x, top = max(0, x - 2), max(0, top - 2)
            right, bottom = min(width, right + 2), min(height, bottom + 2)
            crop_width, crop_height = right - x, bottom - top

            def occupied_center(y0: int, y1: int) -> float:
                counts = [sum(visible.getpixel((xx, yy)) != 0 for yy in range(y0, y1))
                          for xx in range(x, right)]
                count = sum(counts)
                return (x + sum(xx * n for xx, n in enumerate(counts)) / count) if count else (x + right) / 2

            # Unity sprite coordinates start at the bottom of the rectangle.
            shoulder_x = occupied_center(top, min(bottom, top + max(1, crop_height // 10)))
            palm_x = occupied_center(max(top, bottom - max(1, crop_height // 10)), bottom)
            pivot_x = max(.1, min(.9, (shoulder_x - x) / crop_width))
            grip_x = max(0., min(1., (palm_x - x) / crop_width))
            layout = {
                "source": src.relative_to(ROOT).as_posix(),
                "sourceSha256": hashlib.sha256(src.read_bytes()).hexdigest(),
                "parts": [{
                    "x": x, "y": height - bottom, "width": crop_width, "height": crop_height,
                    "pivotX": round(pivot_x, 4), "pivotY": .94,
                    "worldHeight": .82, "gripX": round(grip_x, 4), "gripY": .06,
                }],
            }
        shutil.copyfile(src, dst)
        (TARGET / (name + "_layout.json")).write_text(json.dumps(layout, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        write_meta_once(TARGET / (name + ".png.meta"), texture_meta)
        write_meta_once(TARGET / (name + "_layout.json.meta"), layout_meta)
        records.append({"skin": skin, "source": layout["source"], "sha256": layout["sourceSha256"],
                        "pixelSize": [width, height]})
    manifest = {"batch": "Batch_ModularRig_005", "purpose": "side and diagonal shoulder-pivoted arm art",
                "resources": records}
    SOURCE.parent.joinpath("manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Exported {len(records)} side-facing arms")


if __name__ == "__main__":
    main()
