#!/usr/bin/env python3
"""整理 ArtSource/Inbox；不移动、删除或改写收件箱原图。"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import os
import re
import shutil
import sys
import textwrap
import uuid
from datetime import datetime
from pathlib import Path
from typing import Any

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError as exc:
    raise SystemExit("缺少 Pillow，未创建批次。请安装 Pillow 后重试。") from exc

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")


EXTENSIONS = {".png", ".jpg", ".jpeg", ".webp"}
ASSET_ID_RE = re.compile(r"ART-[A-Z0-9]+-[0-9]+", re.IGNORECASE)
BATCH_ID_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="批量整理美术收件箱中的新增图片。")
    parser.add_argument(
        "--repo-root", type=Path, default=Path(__file__).resolve().parents[1]
    )
    parser.add_argument("--batch-id")
    parser.add_argument(
        "--source-manifest",
        type=Path,
        help="可选的批次来源清单；提供后以其中的文件、AssetID、子件和备注为准。",
    )
    parser.add_argument("--dry-run", action="store_true")
    return parser.parse_args()


def digest(path: Path) -> str:
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            value.update(chunk)
    return value.hexdigest()


def pixels(image: Image.Image) -> list[int]:
    if hasattr(image, "get_flattened_data"):
        return list(image.get_flattened_data())
    return list(image.getdata())


def registry_ids(path: Path) -> set[str]:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream, delimiter="\t")
        if not reader.fieldnames or "AssetID" not in reader.fieldnames:
            raise RuntimeError("273 TSV 缺少 AssetID 字段。")
        result: set[str] = set()
        for line, row in enumerate(reader, 2):
            asset_id = (row.get("AssetID") or "").strip().upper()
            if not asset_id:
                raise RuntimeError(f"273 TSV 第 {line} 行 AssetID 为空。")
            if asset_id in result:
                raise RuntimeError(f"273 TSV 存在重复 AssetID：{asset_id}")
            result.add(asset_id)
        return result


def archived_hashes(batches: Path) -> dict[str, str]:
    result: dict[str, str] = {}
    for path in sorted(batches.glob("*/manifest.json")) if batches.exists() else []:
        try:
            manifest = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as exc:
            raise RuntimeError(f"无法读取既有批次记录 {path}：{exc}") from exc
        for item in manifest.get("files", []):
            if isinstance(item.get("sha256"), str):
                result.setdefault(item["sha256"], item.get("originalPath", str(path)))
    return result


def source_manifest_data(
    path: Path, root: Path, inbox: Path, known_ids: set[str]
) -> tuple[dict[str, Any], dict[Path, dict[str, Any]]]:
    resolved = path if path.is_absolute() else root / path
    resolved = resolved.resolve()
    try:
        data = json.loads(resolved.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as exc:
        raise RuntimeError(f"无法读取来源 manifest：{resolved}（{exc}）") from exc
    entries = data.get("entries")
    if not isinstance(entries, list) or not entries:
        raise RuntimeError("来源 manifest 缺少非空 entries。")
    if data.get("images_count") != len(entries):
        raise RuntimeError("来源 manifest 的 images_count 与 entries 数量不一致。")
    by_path: dict[Path, dict[str, Any]] = {}
    ordinals: set[int] = set()
    for entry in entries:
        if not isinstance(entry, dict):
            raise RuntimeError("来源 manifest entries 必须是对象。")
        relative_value = entry.get("relative_path")
        if not isinstance(relative_value, str):
            raise RuntimeError("来源 manifest 条目缺少 relative_path。")
        source_path = (root / relative_value).resolve()
        try:
            source_path.relative_to(inbox.resolve())
        except ValueError as exc:
            raise RuntimeError(f"来源图片不在 ArtSource/Inbox：{relative_value}") from exc
        if source_path.name != entry.get("filename"):
            raise RuntimeError(f"来源 manifest 文件名与路径不一致：{relative_value}")
        if source_path in by_path:
            raise RuntimeError(f"来源 manifest 重复登记图片：{relative_value}")
        asset_ids = entry.get("asset_ids")
        if not isinstance(asset_ids, list) or not asset_ids:
            raise RuntimeError(f"来源 manifest 条目缺少 asset_ids：{relative_value}")
        unknown = [value for value in asset_ids if str(value).upper() not in known_ids]
        if unknown:
            raise RuntimeError(
                f"来源 manifest 使用了 273 中不存在的 AssetID：{', '.join(unknown)}"
            )
        ordinal = entry.get("ordinal")
        if not isinstance(ordinal, int) or ordinal < 1 or ordinal in ordinals:
            raise RuntimeError(f"来源 manifest ordinal 非法或重复：{relative_value}")
        ordinals.add(ordinal)
        by_path[source_path] = entry
    data["_resolvedPath"] = resolved
    return data, by_path


def images_in(inbox: Path) -> list[Path]:
    if not inbox.exists():
        return []
    return sorted(
        (
            p
            for p in inbox.rglob("*")
            if p.is_file() and not p.is_symlink() and p.suffix.lower() in EXTENSIONS
        ),
        key=lambda p: p.relative_to(inbox).as_posix().casefold(),
    )


def inspect_image(path: Path) -> tuple[dict[str, Any], list[dict[str, str]]]:
    issues: list[dict[str, str]] = []
    try:
        with Image.open(path) as check:
            check.verify()
        with Image.open(path) as source:
            source.load()
            width, height = source.size
            has_alpha = "A" in source.getbands() or (
                source.mode == "P" and "transparency" in source.info
            )
            alpha_min = alpha_max = 255
            transparent = fully_transparent = 0
            if has_alpha:
                alpha = source.convert("RGBA").getchannel("A")
                alpha_min, alpha_max = alpha.getextrema()
                histogram = alpha.histogram()
                transparent = sum(histogram[:255])
                fully_transparent = histogram[0]
                edges = (
                    pixels(alpha.crop((0, 0, width, 1)))
                    + pixels(alpha.crop((0, height - 1, width, height)))
                    + pixels(alpha.crop((0, 0, 1, height)))
                    + pixels(alpha.crop((width - 1, 0, width, height)))
                )
                if any(0 < value < 255 for value in edges):
                    issues.append(issue("transparent_edge_review", "画布边缘存在半透明像素，需检查留白、光效或裁切。"))
                if transparent and any(value > 8 for value in edges):
                    issues.append(issue("possible_crop_review", "非透明内容接触画布边缘，可能被裁切，也可能是预期贴边模块。"))
            actual_transparency = transparent > 0
            if width < 64 or height < 64:
                issues.append(issue("low_resolution_review", "至少一个边小于 64 px，运行用途需要复核。"))
            if width > 8192 or height > 8192:
                issues.append(issue("large_resolution_review", "至少一个边超过 8192 px，处理成本需要复核。"))
            if max(width, height) / max(1, min(width, height)) > 6:
                issues.append(issue("extreme_aspect_ratio_review", "长宽比超过 6:1，需确认是否为预期图集或模块。"))
            if has_alpha and not actual_transparency:
                issues.append(issue("alpha_without_transparency", "存在 Alpha 通道，但所有像素均不透明。"))
            if not actual_transparency:
                issues.append(issue("opaque_background_review", "未检测到真实透明像素；棋盘格、黑底或白底仍按实色处理。"))
                issues.append(issue("crop_requires_visual_review", "全画布不透明，无法仅凭 Alpha 判断裁切。"))
            return (
                {
                    "format": source.format or path.suffix.lstrip(".").upper(),
                    "width": width,
                    "height": height,
                    "mode": source.mode,
                    "hasAlphaChannel": has_alpha,
                    "hasTransparentPixels": actual_transparency,
                    "transparentPixelCount": transparent,
                    "fullyTransparentPixelCount": fully_transparent,
                    "alphaMin": alpha_min if has_alpha else None,
                    "alphaMax": alpha_max if has_alpha else None,
                },
                issues,
            )
    except Exception as exc:
        raise RuntimeError(f"图片无法读取或已损坏：{path}（{exc}）") from exc


def issue(code: str, message: str) -> dict[str, str]:
    return {"code": code, "message": message}


def filename_mapping(name: str, known: set[str]) -> tuple[list[dict[str, Any]], str, list[dict[str, str]]]:
    tokens = list(dict.fromkeys(value.upper() for value in ASSET_ID_RE.findall(name)))
    valid = [value for value in tokens if value in known]
    invalid = [value for value in tokens if value not in known]
    problems = []
    if invalid:
        problems.append(issue("unknown_asset_id_in_filename", "文件名包含 273 中不存在的 AssetID：" + ", ".join(invalid)))
    mappings = [
        {
            "assetId": value,
            "subpartName": None,
            "status": "filename_validated",
            "note": "仅按文件名校验，仍需结合图片内容复核。",
        }
        for value in valid
    ]
    if not mappings:
        mappings.append(
            {
                "assetId": None,
                "subpartName": None,
                "status": "pending_confirmation",
                "note": "文件名未提供可验证的 AssetID，等待结合图片内容与 273 建立候选映射。",
            }
        )
    status = "filename_matched_review_required" if valid and not invalid else "pending_confirmation"
    return mappings, status, problems


def font(size: int) -> ImageFont.ImageFont:
    roots = [
        Path(os.environ.get("WINDIR", r"C:\Windows")) / "Fonts/msyh.ttc",
        Path(os.environ.get("WINDIR", r"C:\Windows")) / "Fonts/simhei.ttf",
        Path("/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc"),
    ]
    for candidate in roots:
        if candidate.is_file():
            try:
                return ImageFont.truetype(str(candidate), size)
            except OSError:
                pass
    return ImageFont.load_default()


def on_background(source: Image.Image, size: tuple[int, int], color: tuple[int, int, int]) -> Image.Image:
    canvas = Image.new("RGB", size, color)
    image = source.convert("RGBA")
    image.thumbnail(size, Image.Resampling.LANCZOS)
    position = ((size[0] - image.width) // 2, (size[1] - image.height) // 2)
    canvas.paste(image, position, image)
    return canvas


def item_preview(source_path: Path, output: Path, transparent: bool) -> None:
    with Image.open(source_path) as source:
        source.load()
        if transparent:
            left = on_background(source, (360, 472), (238, 238, 238))
            right = on_background(source, (360, 472), (42, 46, 52))
            canvas = Image.new("RGB", (720, 520), "white")
            canvas.paste(left, (0, 48))
            canvas.paste(right, (360, 48))
        else:
            body = on_background(source, (720, 472), (214, 216, 220))
            canvas = Image.new("RGB", (720, 520), "white")
            canvas.paste(body, (0, 48))
    draw = ImageDraw.Draw(canvas)
    draw.rectangle((0, 0, 720, 48), fill=(31, 38, 48))
    draw.text((14, 11), "浅底 / 深底透明检查" if transparent else "不透明图检查", font=font(20), fill="white")
    canvas.save(output, "PNG")


def contact_sheet(batch_id: str, items: list[dict[str, Any]], originals: dict[int, Path], output: Path) -> None:
    columns, tile_w, tile_h, header = 4, 380, 330, 96
    rows = math.ceil(len(items) / columns)
    canvas = Image.new("RGB", (columns * tile_w, header + rows * tile_h), (225, 227, 231))
    draw = ImageDraw.Draw(canvas)
    draw.rectangle((0, 0, canvas.width, header), fill=(28, 35, 45))
    draw.text((24, 14), f"美术收件批次 {batch_id}", font=font(28), fill="white")
    draw.text((24, 57), f"新增 {len(items)} 项 · 编号仅用于预览 · 技术检查不等于视觉验收", font=font(18), fill=(208, 215, 224))
    for index, item in enumerate(items):
        row, column = divmod(index, columns)
        left, top = column * tile_w, header + row * tile_h
        draw.rectangle((left + 8, top + 8, left + tile_w - 8, top + tile_h - 8), fill=(248, 248, 248), outline=(176, 180, 187))
        with Image.open(originals[item["number"]]) as source:
            source.load()
            if item["image"]["hasTransparentPixels"]:
                first = on_background(source, (174, 230), (240, 240, 240))
                second = on_background(source, (174, 230), (45, 48, 54))
                canvas.paste(first, (left + 16, top + 16))
                canvas.paste(second, (left + 190, top + 16))
            else:
                canvas.paste(on_background(source, (348, 230), (214, 216, 220)), (left + 16, top + 16))
        draw.text((left + 16, top + 254), f"#{item['number']:03d}", font=font(18), fill=(24, 29, 36))
        lines = textwrap.wrap(item["sourceFileName"], width=28, break_long_words=True, break_on_hyphens=False)[:2]
        for line_no, line in enumerate(lines):
            draw.text((left + 78, top + 254 + line_no * 21), line, font=font(15), fill=(24, 29, 36))
        draw.text((left + 16, top + 304), f"{item['image']['width']}×{item['image']['height']} · 待复核 {len(item['issues'])}", font=font(14), fill=(90, 95, 104))
    canvas.save(output, "PNG")


def organize(
    root: Path,
    requested_id: str | None,
    dry_run: bool,
    source_manifest: Path | None = None,
) -> dict[str, Any]:
    root = root.resolve()
    inbox = root / "ArtSource/Inbox"
    batches = root / "ArtSource/Batches"
    known_ids = registry_ids(root / "docs/40-process/273-current-art-asset-register.tsv")
    known_hashes = archived_hashes(batches)
    discovered = images_in(inbox)
    source_data: dict[str, Any] | None = None
    source_entries: dict[Path, dict[str, Any]] = {}
    if source_manifest:
        source_data, source_entries = source_manifest_data(
            source_manifest, root, inbox, known_ids
        )
        expected = set(source_entries)
        actual = {path.resolve() for path in discovered}
        missing = sorted(str(path) for path in expected - actual)
        extra = sorted(str(path) for path in actual - expected)
        if missing or extra:
            details = []
            if missing:
                details.append("清单缺图：" + ", ".join(missing))
            if extra:
                details.append("Inbox 有清单外图片：" + ", ".join(extra))
            raise RuntimeError("；".join(details))
        discovered.sort(key=lambda path: source_entries[path.resolve()]["ordinal"])
    seen = dict(known_hashes)
    new: list[tuple[Path, str]] = []
    duplicates: list[dict[str, str]] = []
    for source in discovered:
        value = digest(source)
        relative = source.relative_to(inbox).as_posix()
        if value in seen:
            duplicates.append({"sourceInboxPath": relative, "sha256": value, "duplicateOf": seen[value]})
        else:
            new.append((source, value))
            seen[value] = f"本次扫描：{relative}"
    result = {
        "discovered": len(discovered),
        "new": len(new),
        "duplicates": len(duplicates),
        "duplicateItems": duplicates,
        "pendingMappings": 0,
        "filesWithIssues": 0,
        "batchId": None,
        "manifest": None,
        "preview": None,
    }
    if dry_run or not new:
        return result

    manifest_batch_id = source_data.get("batch_id") if source_data else None
    if requested_id and manifest_batch_id and requested_id != manifest_batch_id:
        raise RuntimeError("--batch-id 与来源 manifest 的 batch_id 不一致。")
    batch_id = requested_id or manifest_batch_id or datetime.now().strftime("ART-%Y%m%d-%H%M%S")
    if not BATCH_ID_RE.fullmatch(batch_id):
        raise RuntimeError("批次 ID 只能包含字母、数字、点、下划线和连字符。")
    final = batches / batch_id
    if final.exists():
        raise RuntimeError(f"批次目录已经存在：{batch_id}")
    staging = batches / f".{batch_id}.staging-{uuid.uuid4().hex}"
    originals_dir, candidates_dir, previews_dir = staging / "Originals", staging / "Candidates", staging / "Previews"
    try:
        originals_dir.mkdir(parents=True)
        candidates_dir.mkdir()
        (candidates_dir / ".gitkeep").write_text("", encoding="utf-8")
        previews_dir.mkdir()
        items, originals = [], {}
        for number, (source, value) in enumerate(new, 1):
            relative = source.relative_to(inbox)
            copied = originals_dir / relative
            copied.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, copied)
            if digest(copied) != value:
                raise RuntimeError(f"复制校验失败：{source}")
            image_info, problems = inspect_image(copied)
            source_entry = source_entries.get(source.resolve())
            if source_entry:
                mappings = [
                    {
                        "assetId": str(asset_id).upper(),
                        "subpartName": None,
                        "status": "source_manifest_declared",
                        "note": "由批次来源 manifest 明确提供，未按文件名推断。",
                    }
                    for asset_id in source_entry["asset_ids"]
                ]
                mapping_status = "source_manifest_declared_review_required"
                mapping_problems = []
            else:
                mappings, mapping_status, mapping_problems = filename_mapping(
                    source.name, known_ids
                )
            problems.extend(mapping_problems)
            if source_entry:
                expected_hash = str(source_entry.get("sha256", "")).lower()
                if expected_hash != value:
                    raise RuntimeError(f"图片 SHA-256 与来源 manifest 不一致：{source.name}")
                if [image_info["width"], image_info["height"]] != [
                    source_entry.get("width_px"),
                    source_entry.get("height_px"),
                ]:
                    raise RuntimeError(f"图片尺寸与来源 manifest 不一致：{source.name}")
                if image_info["hasAlphaChannel"] != source_entry.get(
                    "has_alpha_channel"
                ) or image_info["hasTransparentPixels"] != source_entry.get(
                    "has_transparent_pixels"
                ):
                    raise RuntimeError(f"图片 Alpha 结果与来源 manifest 不一致：{source.name}")
            preview_name = f"{number:03d}.png"
            item_preview(copied, previews_dir / preview_name, image_info["hasTransparentPixels"])
            item = {
                "number": number,
                "sourceFileName": source.name,
                "sourceInboxPath": (Path("ArtSource/Inbox") / relative).as_posix(),
                "sha256": value,
                "originalPath": (Path("ArtSource/Batches") / batch_id / "Originals" / relative).as_posix(),
                "fileSizeBytes": source.stat().st_size,
                "image": image_info,
                "suggestedMappings": mappings,
                "mappingStatus": mapping_status,
                "subparts": [
                    {
                        "name": part,
                        "assetIds": [
                            str(asset_id).upper()
                            for asset_id in (source_entry or {}).get("asset_ids", [])
                        ],
                        "status": "manifest_declared_unprocessed",
                    }
                    for part in (source_entry or {}).get("parts", [])
                ],
                "sourceLabel": (source_entry or {}).get("label"),
                "sourceNotes": (source_entry or {}).get("notes"),
                "sourceStatus": (source_entry or {}).get("status"),
                "primaryAssetId": (source_entry or {}).get("primary_asset_id"),
                "originalGeneratedFilename": (source_entry or {}).get(
                    "original_generated_filename"
                ),
                "previewPath": (Path("ArtSource/Batches") / batch_id / "Previews" / preview_name).as_posix(),
                "issues": problems,
                "processing": {"cropRegions": [], "scale": None, "versions": []},
                "technicalCheckStatus": "recorded_review_required",
                "visualAcceptance": "not_performed",
            }
            items.append(item)
            originals[number] = copied
        contact_sheet(batch_id, items, originals, previews_dir / "contact-sheet.png")
        style_reference = None
        style_reference_value = (
            source_data.get("style_reference_relative_path") if source_data else None
        )
        if style_reference_value:
            style_path = (root / style_reference_value).resolve()
            try:
                style_path.relative_to((root / "ArtSource/References").resolve())
            except ValueError as exc:
                raise RuntimeError("风格参考图必须位于 ArtSource/References。") from exc
            if not style_path.is_file():
                raise RuntimeError(f"来源 manifest 指定的风格参考图不存在：{style_reference_value}")
            style_info, style_issues = inspect_image(style_path)
            style_reference = {
                "assetId": "ART-REF-001",
                "path": style_path.relative_to(root).as_posix(),
                "sha256": digest(style_path),
                **style_info,
                "issues": style_issues,
                "status": "reference_only_not_runtime_integrated_or_accepted",
            }
        manifest = {
            "schemaVersion": 1,
            "batchId": batch_id,
            "createdAt": datetime.now().astimezone().isoformat(timespec="seconds"),
            "sourceInbox": "ArtSource/Inbox",
            "sourceManifest": (
                Path(source_data["_resolvedPath"]).relative_to(root).as_posix()
                if source_data
                else None
            ),
            "sourceManifestSha256": (
                digest(Path(source_data["_resolvedPath"])) if source_data else None
            ),
            "globalRegister": "docs/40-process/273-current-art-asset-register.tsv",
            "authorityNote": "这里只记录文件追溯和候选映射；全局制作、接入与验收进度以 273 TSV 为准。",
            "overviewPreviewPath": (Path("ArtSource/Batches") / batch_id / "Previews/contact-sheet.png").as_posix(),
            "styleReference": style_reference,
            "files": items,
            "skippedDuplicates": duplicates,
        }
        (staging / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        staging.rename(final)
    except Exception:
        if staging.exists():
            shutil.rmtree(staging)
        raise
    result.update(
        {
            "pendingMappings": sum(
                item["mappingStatus"] == "pending_confirmation" for item in items
            ),
            "filesWithIssues": sum(bool(item["issues"]) for item in items),
            "batchId": batch_id,
            "manifest": (final / "manifest.json").relative_to(root).as_posix(),
            "preview": (final / "Previews/contact-sheet.png").relative_to(root).as_posix(),
        }
    )
    return result


def main() -> int:
    args = arguments()
    try:
        result = organize(
            args.repo_root, args.batch_id, args.dry_run, args.source_manifest
        )
    except (OSError, RuntimeError) as exc:
        print(f"错误：{exc}", file=sys.stderr)
        return 1
    print("美术收件箱只读扫描完成。" if args.dry_run else "美术收件箱整理完成。")
    print(f"发现支持图片：{result['discovered']}")
    print(f"新增：{result['new']}")
    print(f"重复并跳过：{result['duplicates']}")
    for duplicate in result["duplicateItems"]:
        print(f"  重复：{duplicate['sourceInboxPath']} -> {duplicate['duplicateOf']}")
    if result["batchId"]:
        print(f"待确认映射：{result['pendingMappings']}")
        print(f"存在待复核问题的文件：{result['filesWithIssues']}")
        print(f"批次：{result['batchId']}")
        print(f"记录：{result['manifest']}")
        print(f"预览：{result['preview']}")
    elif result["new"] == 0:
        print("没有新增图片，未创建批次目录。")
    else:
        print("dry-run 未创建批次目录。")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
