"""Export alpha bounds plus explicit anatomical calibration; never changes PNGs or GUIDs."""
from collections import deque
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import uuid

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / "Assets/Art/Characters/Resources"
CALIBRATION_PATH = ROOT / "ArtSource/BatchInfo/human-common-rig-calibration.json"


def normalized_point(point, piece, label):
    """Source atlas top-left pixels -> rect-relative bottom-left coordinates."""
    if not isinstance(point, list) or len(point) != 2:
        raise ValueError(f"{label}: expected [source x, source y]")
    x = (point[0] - piece["x"]) / piece["width"]
    y = 1 - (point[1] - piece["y"]) / piece["height"]
    if not 0 <= x <= 1 or not 0 <= y <= 1:
        raise ValueError(f"{label}: pixel {point} falls outside the alpha-bounded rect")
    return dict(x=round(x, 7), y=round(y, 7))


def triangulate_polygon(vertices, label):
    """Ear-clip a small explicitly authored visible torso outline, retaining its concavity."""
    points = [(v["x"], v["y"]) for v in vertices]
    if len(points) < 3:
        raise ValueError(f"{label}: visible torso mesh needs at least three vertices")

    def cross(a, b, c):
        return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])

    area = sum(points[i][0] * points[(i + 1) % len(points)][1] -
               points[(i + 1) % len(points)][0] * points[i][1] for i in range(len(points)))
    if abs(area) < 1e-8:
        raise ValueError(f"{label}: visible torso outline has no area")
    remaining = list(range(len(points)))
    if area < 0:
        remaining.reverse()
    triangles = []
    while len(remaining) > 3:
        for j in range(len(remaining)):
            a, b, c = remaining[j - 1], remaining[j], remaining[(j + 1) % len(remaining)]
            if cross(points[a], points[b], points[c]) <= 1e-9:
                continue
            if any(cross(points[a], points[b], points[k]) >= -1e-9 and
                   cross(points[b], points[c], points[k]) >= -1e-9 and
                   cross(points[c], points[a], points[k]) >= -1e-9
                   for k in remaining if k not in (a, b, c)):
                continue
            triangles.extend((a, b, c))
            del remaining[j]
            break
        else:
            raise ValueError(f"{label}: cannot triangulate the torso outline; inspect crossing edges")
    triangles.extend(remaining)
    return triangles


def apply_calibration(name, pieces, entry):
    overrides = entry["pieces"]
    copy_sources = {}
    for slot, values in overrides.items():
        index = int(slot)
        if not 0 <= index < len(pieces):
            raise ValueError(f"{name}: calibration references nonexistent part {index}")
        if "copyFromPart" in values:
            source = values["copyFromPart"]
            if type(source) is not int or not 0 <= source < len(pieces):
                raise ValueError(f"{name} part {index}: invalid copyFromPart {source!r}")
            if source == index:
                raise ValueError(f"{name} part {index}: copyFromPart cannot reference itself")
            extra = set(values) - {"copyFromPart", "mirrorX"}
            if extra:
                raise ValueError(f"{name} part {index}: register {sorted(extra)} on the source part, not its copy")
            copy_sources[index] = source

    def registered_source(index, chain=()):
        if index in chain:
            raise ValueError(f"{name}: cyclic copyFromPart chain {chain + (index,)}")
        if index in copy_sources:
            return registered_source(copy_sources[index], chain + (index,))
        return index

    # Resolve every reference before applying any calibration, including optional slots.
    sources = {index: registered_source(index) for index in range(len(pieces))}
    if "_parts_" in name:
        for index in (0, 6, 8):
            for socket in ("neckSocketPx", "leftShoulderSocketPx", "rightShoulderSocketPx"):
                if socket not in overrides.get(str(sources[index]), {}):
                    raise ValueError(f"{name} part {index}: missing anatomical {socket}")
        for index in (1, 2, 3, 7):
            if "pivotPx" not in overrides.get(str(sources[index]), {}):
                raise ValueError(f"{name} part {index}: missing anatomical pivot")
        for index in (2, 3):
            if "gripPx" not in overrides.get(str(sources[index]), {}):
                raise ValueError(f"{name} arm {index}: missing palm grip")
    elif len(overrides) != len(pieces):
        raise ValueError(f"{name}: every side head needs an explicit neck-base pivot")
    for slot, values in overrides.items():
        index = int(slot)
        if index in copy_sources:
            continue
        piece = pieces[index]
        label = f"{name} part {index}"
        for source_field, prefix in (("pivotPx", "pivot"), ("gripPx", "grip")):
            if source_field in values:
                point = normalized_point(values[source_field], piece, label + " " + source_field)
                piece[prefix + "X"], piece[prefix + "Y"] = point["x"], point["y"]
        if "neckSocketPx" in values:
            piece["hasSockets"] = True
            for socket in ("neckSocket", "leftShoulderSocket", "rightShoulderSocket"):
                piece[socket] = normalized_point(values[socket + "Px"], piece, label + " " + socket)
        if "mirrorX" in values:
            piece["mirrorX"] = values["mirrorX"]
        if "meshPolygonPx" in values:
            vertices = [normalized_point(point, piece, label + " visible outline")
                        for point in values["meshPolygonPx"]]
            piece["meshVertices"] = vertices
            piece["meshTriangles"] = triangulate_polygon(vertices, label)

    copied = set()

    def copy_calibrated_piece(index):
        if index not in copy_sources or index in copied:
            return
        source = copy_sources[index]
        copy_calibrated_piece(source)
        piece = deepcopy(pieces[source])
        # The copied crop, pivot and grip all describe the same physical source piece.
        # Mirror is a view setting; the Host reflects both rendering and grip together.
        piece["sourcePart"] = piece.get("sourcePart", source)
        if "mirrorX" in overrides[str(index)]:
            piece["mirrorX"] = overrides[str(index)]["mirrorX"]
        pieces[index] = piece
        copied.add(index)

    for index in copy_sources:
        copy_calibrated_piece(index)


def components(image):
    width, height = image.size
    alpha = image.getchannel("A").tobytes()
    seen = bytearray(width * height)
    found = []
    for seed, value in enumerate(alpha):
        if value < 32 or seen[seed]:
            continue
        seen[seed] = 1
        queue = deque([seed])
        left = right = seed % width
        top = bottom = seed // width
        count = 0
        while queue:
            pos = queue.popleft()
            x, y = pos % width, pos // width
            count += 1
            left, right, top, bottom = min(left, x), max(right, x), min(top, y), max(bottom, y)
            neighbors = []
            if x: neighbors.append(pos - 1)
            if x + 1 < width: neighbors.append(pos + 1)
            if y: neighbors.append(pos - width)
            if y + 1 < height: neighbors.append(pos + width)
            for other in neighbors:
                if not seen[other] and alpha[other] >= 32:
                    seen[other] = 1
                    queue.append(other)
        if count >= 64:
            found.append((left, top, right + 1, bottom + 1, count))
    return found


def export(source, calibration, calibration_sha256):
    image = Image.open(source).convert("RGBA")
    width, height = image.size
    name = source.stem
    is_parts = "_parts_" in name
    is_tools = "_tools_" in name
    source_sha256 = hashlib.sha256(source.read_bytes()).hexdigest()
    registration = calibration["atlases"].get(name)
    if not is_tools:
        if registration is None:
            raise ValueError(f"{name}: no anatomical source-image registration; do not guess pivots")
        if registration.get("sourceSha256") != source_sha256:
            raise ValueError(f"{name}: source image changed; re-register joints before exporting")
    columns = 3 if is_parts or is_tools or "001_002_004" in name else 1 if "_sidehead_" in name else 4
    rows = 3 if is_parts else 2 if is_tools else 1
    buckets = [[] for _ in range(columns * rows)]
    for box in components(image):
        cx, cy = (box[0] + box[2]) / 2, (box[1] + box[3]) / 2
        col = min(columns - 1, int(cx * columns / width))
        row = (0 if cy < height * .578 else 1) if is_tools else min(rows - 1, int(cy * rows / height))
        buckets[row * columns + col].append(box)
    pieces = []
    for index, boxes in enumerate(buckets):
        if not boxes:
            raise ValueError(f"No alpha component for {name}, slot {index}")
        left = max(0, min(b[0] for b in boxes) - 2)
        top = max(0, min(b[1] for b in boxes) - 2)
        right = min(width, max(b[2] for b in boxes) + 2)
        bottom = min(height, max(b[3] for b in boxes) + 2)
        pivot, world_height, grip = (.5, .08), .74, (.5, .08)
        if is_parts:
            if index in (0, 6, 8): pivot, world_height = (.5, .5), .86
            elif index in (2, 3): pivot, world_height = (.5, .94), .82
            elif index in (4, 5): pivot, world_height = (.5, .94), .76
        if is_tools:
            world_height = (2.0, .95, 1.55, 1.35, 1.1, .75)[index]
            pivot = ((.5, .4), (.5, .24), (.8, .22), (.5, .22), (.6, .18), (.5, .5))[index]
        pieces.append(dict(x=left, y=top, width=right-left, height=bottom-top,
                           pivotX=pivot[0], pivotY=pivot[1], worldHeight=world_height,
                           gripX=grip[0], gripY=grip[1]))
    if registration is not None:
        apply_calibration(name, pieces, registration)
    layout = dict(source=source.relative_to(ROOT).as_posix(), sourceSha256=source_sha256, parts=pieces)
    if registration is not None:
        layout["calibrationSource"] = CALIBRATION_PATH.relative_to(ROOT).as_posix()
        layout["calibrationSha256"] = calibration_sha256
        layout["calibrationSchemaVersion"] = calibration["schemaVersion"]
    content = json.dumps(layout, ensure_ascii=False, indent=2) + "\n"
    archive = source.parent.parent / "Layouts" / (name + "_layout.json")
    archive.parent.mkdir(parents=True, exist_ok=True)
    archive.write_text(content, encoding="utf-8")
    runtime = DEST / archive.name
    runtime.write_text(content, encoding="utf-8")
    meta = Path(str(runtime) + ".meta")
    if not meta.exists():
        meta.write_text("fileFormatVersion: 2\nguid: " + uuid.uuid4().hex +
                        "\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n"
                        "  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")
    print(f"{name}: {len(pieces)} alpha-bounded pieces" +
          (" with source-pixel joint registration" if registration is not None else ""))


if __name__ == "__main__":
    calibration = json.loads(CALIBRATION_PATH.read_text(encoding="utf-8"))
    if calibration.get("schemaVersion") != 1:
        raise ValueError("Unsupported HumanCommonRig source calibration schema")
    calibration_sha256 = hashlib.sha256(CALIBRATION_PATH.read_bytes()).hexdigest()
    for batch in ("Batch_ModularRig_001", "Batch_ModularRig_002"):
        batch_root = ROOT / "ArtSource/Batches" / batch
        for source in sorted((batch_root / "Originals").glob("*HumanCommonRig*.png")):
            export(source, calibration, calibration_sha256)
        manifest_path = batch_root / "manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
        manifest["layout_generator"] = "tools/export-human-rig-layouts.py"
        manifest["layout_status"] = "Alpha-derived bounds with explicit per-image anatomical joints/sockets, visible body/head meshes and mirrored part aliases; Unity visual acceptance pending"
        manifest["calibration_source"] = CALIBRATION_PATH.relative_to(ROOT).as_posix()
        manifest["calibration_sha256"] = calibration_sha256
        manifest["calibration_schema_version"] = calibration["schemaVersion"]
        if "parts_order" in manifest:
            manifest["parts_order"] = ["front_torso", "front_head", "screen_right_arm", "screen_left_arm",
                                       "left_leg", "right_leg", "back_torso", "back_head", "side_torso"]
        for entry in manifest.get("entries", []):
            layout_name = Path(entry["file"]).stem + "_layout.json"
            archived = batch_root / "Layouts" / layout_name
            if archived.exists():
                if "_parts_" in Path(entry["file"]).name:
                    entry["atlas_layout"] = "3x3: front torso/head/screen-right arm; screen-left arm/left leg/right leg; back torso/head/side torso; alpha-bound rectangles in layout JSON"
                entry["layout_file"] = archived.relative_to(ROOT).as_posix()
                entry["unity_layout_path"] = (DEST / layout_name).relative_to(ROOT).as_posix()
                entry["layout_sha256"] = hashlib.sha256(archived.read_bytes()).hexdigest()
        manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
