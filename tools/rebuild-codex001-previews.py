#!/usr/bin/env python3
"""Rebuild retired Batch_Codex_001 single-image light/dark review previews.

Run only when a historical single-image preview is needed. The contact sheet is
preserved separately. These previews are visual aids, not source or runtime art.
"""

from pathlib import Path
from PIL import Image


ROOT = Path(__file__).resolve().parent.parent
BATCH = ROOT / "ArtSource/Batches/Batch_Codex_001"
ORIGINALS = BATCH / "Originals"
PREVIEWS = BATCH / "Previews"


def main() -> None:
    PREVIEWS.mkdir(exist_ok=True)
    for source in sorted(ORIGINALS.glob("*.png")):
        with Image.open(source) as opened:
            image = opened.convert("RGBA")
        for label, color in (("light", (238, 239, 232, 255)), ("dark", (43, 49, 53, 255))):
            background = Image.new("RGBA", image.size, color)
            background.alpha_composite(image)
            output = PREVIEWS / f"{source.stem}_{label}.png"
            background.convert("RGB").save(output)
            print(output.relative_to(ROOT).as_posix())


if __name__ == "__main__":
    main()
