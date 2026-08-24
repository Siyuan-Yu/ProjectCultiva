#!/usr/bin/env python3
from pathlib import Path

root = Path(__file__).resolve().parents[1] / "Assets/Scripts/Unity/Host"
for p in sorted(root.rglob("*.cs")):
    data = p.read_bytes()
    try:
        data.decode("utf-8")
    except UnicodeDecodeError as e:
        print(p.name, e)
