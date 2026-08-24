#!/usr/bin/env python3
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[1] / "Content"
pattern = re.compile(rb'"[^"\n]*\?,\s*\r?\n')

for path in sorted(root.rglob("*.json")):
    data = path.read_bytes()
    hits = pattern.findall(data)
    if hits:
        print(f"BROKEN {path.relative_to(root.parents[0])}")
        for h in hits[:5]:
            print(f"  {h!r}")
    try:
        json.loads(data.decode("utf-8"))
    except Exception as exc:
        print(f"PARSE_FAIL {path}: {exc}")
