#!/usr/bin/env python3
"""Fix corrupted UTF-8 strings in scenarios.json (missing final char + closing quote)."""
from pathlib import Path

path = Path(__file__).resolve().parents[1] / "Content/BaseGame/Data/Scenarios/scenarios.json"
data = path.read_bytes()

replacements = [
    (b'"displayName": "\xe6\x9d\x91\xe5\x86\x85\xe5\x8f\xaf\xe6\x8b\x9b\xe8\x80?,', b'"displayName": "\xe6\x9d\x91\xe5\x86\x85\xe5\x8f\xaf\xe6\x8b\x9b\xe8\x80\x85",'),
    (b'"displayName": "\xe5\xb7\xa1\xe5\x8d\xab\xe7\x94?,', b'"displayName": "\xe5\xb7\xa1\xe5\x8d\xab\xe7\x94\xb2",'),
    (b'"displayName": "\xe5\xb7\xa1\xe5\x8d\xab\xe4\xb9?,', b'"displayName": "\xe5\xb7\xa1\xe5\x8d\xab\xe4\xb9\x99",'),
    (b'"displayName": "\xe5\xb7\xa1\xe5\x8d\xab\xe4\xb8?,', b'"displayName": "\xe5\xb7\xa1\xe5\x8d\xab\xe4\xb8\x99",'),
    (b'"displayName": "\xe5\xb0\x86\xe8\x80?,', b'"displayName": "\xe5\xb0\x86\xe8\x80\x81",'),
    (b'"name": "\xe7\xac\xac\xe4\xb8\x80\xe7\xab\xa0\xe7\x94\x9f\xe4\xba?Harness",', b'"name": "\xe7\xac\xac\xe4\xb8\x80\xe7\xab\xa0\xe7\x94\x9f\xe5\xad\x98 Harness",'),
]

for old, new in replacements:
    count = data.count(old)
    if count:
        print(f"replace {count}x: {old!r} -> {new!r}")
        data = data.replace(old, new)

path.write_bytes(data)

import json
parsed = json.loads(path.read_text(encoding="utf-8"))
print(f"OK: {len(parsed['definitions'])} scenario definitions")
