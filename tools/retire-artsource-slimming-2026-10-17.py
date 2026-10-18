#!/usr/bin/env python3
"""Apply the reviewed ArtSource slimming ledger once, with hash/archive guards."""

import csv
import hashlib
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
ART = ROOT / "ArtSource"
LEDGER = ART / "Reports/artsource-slimming-2026-10-17.tsv"


def sha256(path: Path) -> str:
    result = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(chunk)
    return result.hexdigest()


def main() -> None:
    with LEDGER.open(encoding="utf-8", newline="") as stream:
        rows = list(csv.DictReader(stream, delimiter="\t"))
    if len(rows) != 122 or len({row["删除路径"] for row in rows}) != 122:
        raise RuntimeError("Unexpected slimming ledger")
    allowed = (
        "ArtSource/Batches/Batch_Codex_001/Candidates/",
        "ArtSource/Batches/Batch_Codex_001/Frames/",
        "ArtSource/Batches/Batch_Codex_001/Previews/",
        "ArtSource/Batches/Batch_Codex_002/Static/ART-ROOM-008_core_isolated_v03.png",
        "ArtSource/Inbox/",
    )
    targets = []
    for row in rows:
        relative = row["删除路径"]
        if not relative.startswith(allowed) or ".." in Path(relative).parts:
            raise RuntimeError(f"Unexpected path: {relative}")
        path = (ROOT / relative).resolve()
        if not path.is_relative_to(ART.resolve()):
            raise RuntimeError(f"Outside ArtSource: {path}")
        retained = row["保留或替代路径"]
        if retained.startswith("ArtSource/"):
            kept = (ROOT / retained).resolve()
            if not kept.is_relative_to(ART.resolve()) or not kept.is_file():
                raise RuntimeError(f"Missing archive: {kept}")
            if sha256(kept) != row["SHA-256"]:
                raise RuntimeError(f"Archive hash differs: {kept}")
        if path.exists():
            if not path.is_file() or path.stat().st_size != int(row["字节数"]):
                raise RuntimeError(f"Target differs: {path}")
            if sha256(path) != row["SHA-256"]:
                raise RuntimeError(f"Target hash differs: {path}")
            targets.append(path)
    for path in targets:
        path.unlink()
    print(f"Removed {len(targets)} remaining ledger files; {len(rows) - len(targets)} already absent")


if __name__ == "__main__":
    main()
