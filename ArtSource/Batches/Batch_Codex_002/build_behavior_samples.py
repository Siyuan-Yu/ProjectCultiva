"""Visual samples only: real carried appearance and static rest/down/death transforms."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageEnhance
import json

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
OUT = HERE / "Previews"


def main():
    carrier_path = HERE / "Frames/ART-CHAR-001/walk/ART-CHAR-001_walk_south_01_v02.png"
    carried_path = HERE / "Static/ART-CHAR-002_companion_a_idle_south_v02.png"
    carrier = Image.open(carrier_path).convert("RGBA")
    carried = Image.open(carried_path).convert("RGBA")
    carried.thumbnail((150, 200), Image.Resampling.LANCZOS)
    carried = carried.rotate(55, expand=True, resample=Image.Resampling.BICUBIC)
    carry = Image.new("RGBA", (560, 440), (225, 226, 217, 255))
    carry.alpha_composite(carried, (245, 90))
    carry.alpha_composite(carrier)
    ImageDraw.Draw(carry).text((10, 10), "mount sample: carried actor = companion_a original", fill=(20, 24, 23))
    carry_path = OUT / "ART-ANIM-003_carry_actual_appearance_mount_sample_v02.png"
    carry.save(carry_path)

    actor = Image.open(carried_path).convert("RGBA")
    actor.thumbnail((230, 290), Image.Resampling.LANCZOS)
    lying = actor.rotate(90, expand=True, resample=Image.Resampling.BICUBIC)
    preview = Image.new("RGBA", (930, 380), (225, 226, 217, 255))
    for i, label in enumerate(("sleep", "dying", "corpse")):
        sprite = lying.copy()
        if i == 1:
            sprite = ImageEnhance.Color(sprite).enhance(.55)
        if i == 2:
            sprite = ImageEnhance.Color(sprite).enhance(.15)
        x = 30 + i * 300
        preview.alpha_composite(sprite, (x, 115))
        ImageDraw.Draw(preview).text((x + 70, 40), label, fill=(20, 24, 23))
    states_path = OUT / "ART-ANIM-005_static_state_transform_sample_v02.png"
    preview.save(states_path)
    (HERE / "behavior_samples_manifest.json").write_text(json.dumps({
        "carry_preview": carry_path.relative_to(ROOT).as_posix(),
        "static_states_preview": states_path.relative_to(ROOT).as_posix(),
        "carrier_source": carrier_path.relative_to(ROOT).as_posix(),
        "carried_source": carried_path.relative_to(ROOT).as_posix(),
        "status": "visual candidate only; no Host mount/socket/ordering or play test",
        "issues": ["mount position is schematic", "lying transforms have no bed fit or collision validation"]
    }, ensure_ascii=False, indent=2), encoding="utf-8")
    print("wrote carry and static-state visual samples")


if __name__ == "__main__":
    main()
