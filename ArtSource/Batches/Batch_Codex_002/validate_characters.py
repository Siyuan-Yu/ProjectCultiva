"""Technical animation QA and state-switch previews; not Unity/visual acceptance."""
from pathlib import Path
from hashlib import sha256
import json
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
DIRECTIONS = ("north", "south", "west", "east")


def frame(asset, action, direction, i):
    return Image.open(HERE / "Frames" / asset / action /
                      f"{asset}_{action}_{direction}_{i:02d}_v02.png").convert("RGBA")


def switch_preview(asset, direction, action):
    sequence = [("idle", 1)] + [("walk", i) for i in range(1, 5)] + \
               [(action, i) for i in range(1, 5)] + [("idle", 1)]
    cards = []
    for label, i in sequence:
        im = frame(asset, label, direction, i).resize((280, 200), Image.Resampling.LANCZOS)
        card = Image.new("RGBA", (280, 226), (234, 230, 219, 255))
        card.alpha_composite(im, (0, 0))
        ImageDraw.Draw(card).text((8, 204), f"{asset} {direction} {label} {i}", fill=(30, 30, 30))
        cards.append(card)
    out = HERE / "Previews" / f"{asset}_{direction}_idle_walk_{action}_switch_v02.gif"
    cards[0].save(out, save_all=True, append_images=cards[1:], duration=190, loop=0, disposal=2)
    return out.relative_to(ROOT).as_posix()


def main():
    manifest = json.loads((HERE / "character_manifest.json").read_text(encoding="utf-8"))
    results = []
    for entry in manifest["entries"]:
        frames = entry["frames"]
        sizes = set()
        hashes = []
        nonempty = True
        alpha = True
        edge_hits = []
        for record in frames:
            im = Image.open(ROOT / record["file"]).convert("RGBA")
            sizes.add(im.size)
            a = im.getchannel("A")
            nonempty &= a.getbbox() is not None
            alpha &= a.getextrema()[0] == 0 and a.getextrema()[1] > 0
            if record["touches_canvas_edge"]:
                edge_hits.append(record["file"])
            hashes.append(sha256(im.tobytes()).hexdigest())
        duplicates = len(hashes) - len(set(hashes))
        results.append({"asset_id": entry["asset_id"], "action": entry["action"],
                        "frame_count": len(frames), "expected": 4 * entry["frames_per_direction"],
                        "uniform_size": len(sizes) == 1, "nonempty": nonempty,
                        "transparent_alpha": alpha, "canvas_edge_hits": edge_hits,
                        "exact_duplicate_frames": duplicates,
                        "review": "Pixel uniqueness is not motion validity; inspect switch GIF for foot/garment/equipment jump."})
    previews = []
    for asset, last in (("ART-CHAR-001", "work"), ("ART-CHAR-004", "attack")):
        sheet = Image.new("RGBA", (10 * 160, 4 * 155), (232, 229, 220, 255))
        for direction in DIRECTIONS:
            previews.append(switch_preview(asset, direction, last))
            row = DIRECTIONS.index(direction)
            sequence = [("idle", 1)] + [("walk", i) for i in range(1, 5)] + \
                       [(last, i) for i in range(1, 5)] + [("idle", 1)]
            for col, (action, i) in enumerate(sequence):
                im = frame(asset, action, direction, i).resize((160, 135), Image.Resampling.LANCZOS)
                sheet.alpha_composite(im, (col * 160, row * 155))
                ImageDraw.Draw(sheet).text((col * 160 + 3, row * 155 + 137),
                                           f"{direction[0]} {action} {i}", fill=(25, 25, 25))
        sheet.save(HERE / "Previews" / f"{asset}_action_switch_contact_v02.png")
    for direction in DIRECTIONS:
        previews.append(switch_preview("ART-CHAR-001", direction, "attack"))
    report = {"checks": results, "switch_previews": previews,
              "all_technical_checks_pass": all(x["frame_count"] == x["expected"] and
                 x["uniform_size"] and x["nonempty"] and x["transparent_alpha"] and
                 not x["canvas_edge_hits"] and x["exact_duplicate_frames"] == 0 for x in results),
              "limitations": ["No Unity PPU/pivot/render or human playtest", "Motion quality requires visual review"]}
    (HERE / "character_qa.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"pass": report["all_technical_checks_pass"], "checks": results}, ensure_ascii=False))


if __name__ == "__main__":
    main()
