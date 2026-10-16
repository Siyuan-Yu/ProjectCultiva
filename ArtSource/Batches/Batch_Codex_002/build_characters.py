"""Extract the six existing character source sheets by connected figure, never by W/4/H/4."""
from collections import deque
from hashlib import sha256
from pathlib import Path
import json

import numpy as np
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
SOURCE = ROOT / "ArtSource/Batches/Batch_Codex_001/Originals"
DIRECTIONS = ("north", "south", "west", "east")
CANVAS = (560, 440)
PIVOT = (280, 392)
SPECS = (("ART-CHAR-001", "idle", 1), ("ART-CHAR-001", "walk", 4),
         ("ART-CHAR-001", "work", 4), ("ART-CHAR-001", "attack", 4),
         ("ART-CHAR-004", "idle", 1),
         ("ART-CHAR-004", "walk", 4), ("ART-CHAR-004", "attack", 4))


def components(image):
    rgba = np.asarray(image.convert("RGBA"))
    alpha = rgba[:, :, 3] > 30
    height, width = alpha.shape
    visited = bytearray(height * width)
    found = []
    for sy in range(height):
        for sx in range(width):
            seed = sy * width + sx
            if visited[seed] or not alpha[sy, sx]:
                continue
            visited[seed] = 1
            queue = deque([seed])
            pixels = []
            minx = maxx = sx
            miny = maxy = sy
            while queue:
                pos = queue.popleft()
                y, x = divmod(pos, width)
                pixels.append((x, y))
                minx, maxx = min(minx, x), max(maxx, x)
                miny, maxy = min(miny, y), max(maxy, y)
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if 0 <= nx < width and 0 <= ny < height:
                        neighbor = ny * width + nx
                        if not visited[neighbor] and alpha[ny, nx]:
                            visited[neighbor] = 1
                            queue.append(neighbor)
            if len(pixels) > 1000:
                found.append({"pixels": pixels, "bbox": [minx, miny, maxx + 1, maxy + 1], "area": len(pixels)})
    return found


def extract(image, component):
    bbox = component["bbox"]
    # Copy exact source RGB/alpha in the figure's component, plus a 1px AA rim.
    mask = Image.new("L", image.size)
    ma = np.zeros((image.height, image.width), dtype=np.uint8)
    for x, y in component["pixels"]:
        ma[y, x] = 255
    core = Image.fromarray(ma)
    from PIL import ImageFilter
    mask = core.filter(ImageFilter.MaxFilter(3))
    frame = image.crop(tuple(bbox))
    alpha = image.getchannel("A").crop(tuple(bbox))
    rim = mask.crop(tuple(bbox))
    alpha = Image.fromarray(np.minimum(np.asarray(alpha), np.asarray(rim)))
    frame.putalpha(alpha)
    return frame


def body_anchor(frame):
    alpha = np.asarray(frame.getchannel("A")) > 50
    h, w = alpha.shape
    torso = alpha[max(0, int(h * .28)):max(1, int(h * .68)), :]
    profile = torso.sum(axis=0)
    width = min(65, w)
    scores = np.convolve(profile, np.ones(width), mode="valid")
    left = int(scores.argmax()) if len(scores) else 0
    cx = left + width // 2
    lo, hi = max(0, cx - 65), min(w, cx + 66)
    rows = alpha[int(h * .65):, lo:hi].sum(axis=1)
    valid = np.flatnonzero(rows > 15)
    foot = int(h * .65) + int(valid[-1]) if len(valid) else h - 1
    return cx, foot


def main():
    entries = []
    frame_count = 0
    for asset_id, action, per_direction in SPECS:
        name = f"{asset_id}_{action}_4dir_{per_direction}f_sheet_v01.png"
        source_path = (HERE / "Originals" / name) if (asset_id == "ART-CHAR-001" and action == "attack") else SOURCE / name
        source = Image.open(source_path).convert("RGBA")
        pieces = components(source)
        expected = 4 * per_direction
        if len(pieces) != expected:
            raise ValueError(f"{name}: found {len(pieces)} figures, expected {expected}")
        pieces.sort(key=lambda c: (c["bbox"][1] + c["bbox"][3]) / 2)
        ordered = []
        for row in range(4):
            group = pieces[row * per_direction:(row + 1) * per_direction]
            group.sort(key=lambda c: c["bbox"][0])
            ordered.extend(group)
        out_dir = HERE / "Frames" / asset_id / action
        out_dir.mkdir(parents=True, exist_ok=True)
        raw = [extract(source, p) for p in ordered]
        # Idle source uses roughly twice the walk character scale. One scale per
        # *entire action*, never one scale per weapon-inclusive frame.
        scale = (.5 if (asset_id == "ART-CHAR-001" and action == "idle") else
                 .42 if action == "idle" else 1.16 if action == "work" else 1.)
        sheet = Image.new("RGBA", (CANVAS[0] * per_direction, CANVAS[1] * 4))
        records = []
        all_frames = []
        for i, (piece, frame) in enumerate(zip(ordered, raw)):
            if scale != 1:
                frame = frame.resize((round(frame.width * scale), round(frame.height * scale)), Image.Resampling.LANCZOS)
            cx, foot = body_anchor(frame)
            target = Image.new("RGBA", CANVAS)
            x, y = PIVOT[0] - cx, PIVOT[1] - foot
            target.alpha_composite(frame, (x, y))
            alpha = target.getchannel("A")
            bounds = alpha.getbbox()
            edge = bounds is None or bounds[0] == 0 or bounds[1] == 0 or bounds[2] == CANVAS[0] or bounds[3] == CANVAS[1]
            direction = DIRECTIONS[i // per_direction]
            number = i % per_direction + 1
            filename = f"{asset_id}_{action}_{direction}_{number:02d}_v02.png"
            target.save(out_dir / filename)
            sheet.alpha_composite(target, ((i % per_direction) * CANVAS[0], (i // per_direction) * CANVAS[1]))
            all_frames.append(target)
            records.append({"file": (out_dir / filename).relative_to(ROOT).as_posix(),
                            "source_bbox_xyxy": piece["bbox"], "source_component_area": piece["area"],
                            "body_anchor_in_extracted_frame": [cx, foot], "canvas_pivot_px": list(PIVOT),
                            "alpha_bounds_xyxy": bounds, "touches_canvas_edge": edge,
                            "sha256": sha256((out_dir / filename).read_bytes()).hexdigest()})
            frame_count += 1
        sheet_path = HERE / "Sheets" / name.replace("v01", "v02")
        sheet_path.parent.mkdir(parents=True, exist_ok=True)
        sheet.save(sheet_path)
        preview = HERE / "Previews" / f"{asset_id}_{action}_4dir_v02.gif"
        preview.parent.mkdir(parents=True, exist_ok=True)
        small = [f.resize((220, 200), Image.Resampling.LANCZOS) for f in all_frames]
        times = per_direction * 4
        # Direction-by-direction loop, looping back to its first pose.
        small[0].save(preview, save_all=True, append_images=small[1:], duration=160, loop=0,
                      disposal=2, transparency=0)
        entries.append({"asset_id": asset_id, "action": action,
                        "source": source_path.relative_to(ROOT).as_posix(),
                        "source_sha256": sha256(source_path.read_bytes()).hexdigest(),
                        "sheet": sheet_path.relative_to(ROOT).as_posix(),
                        "sheet_sha256": sha256(sheet_path.read_bytes()).hexdigest(),
                        "preview": preview.relative_to(ROOT).as_posix(),
                        "direction_order": list(DIRECTIONS), "frames_per_direction": per_direction,
                        "frame_order": "row major", "canvas_px": list(CANVAS), "pivot_px": list(PIVOT),
                        "uniform_action_scale": scale, "frames": records})
    (HERE / "character_manifest.json").write_text(json.dumps({"entries": entries,
        "exported_frames": frame_count, "note": "Candidate extraction only; no Unity/visual acceptance."}, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"exported {frame_count} frames in {len(entries)} sheets")


if __name__ == "__main__":
    main()
