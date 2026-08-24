#!/usr/bin/env python3
"""Restore corrupted WorldSite displayName entries in ch01_hex_world.json from git."""
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
JSON_PATH = ROOT / "Content/BaseGame/Data/Worlds/ch01_hex_world.json"
GIT_REV = "1e89a7b"


def extract_sites(doc):
    for d in doc.get("definitions") or []:
        if d.get("type") == "hexWorld" and d.get("sites"):
            return d["sites"]
    raise SystemExit("hexWorld sites not found")


def main():
    git_raw = subprocess.check_output(
        ["git", "-C", str(ROOT), "show", f"{GIT_REV}:Content/BaseGame/Data/Worlds/ch01_hex_world.json"]
    )
    git_sites = extract_sites(json.loads(git_raw.decode("utf-8")))
    git_by_id = {
        (s.get("id") or s.get("siteId")): s.get("displayName", "")
        for s in git_sites
        if s.get("id") or s.get("siteId")
    }

    doc = json.loads(JSON_PATH.read_text(encoding="utf-8", errors="replace"))
    sites = extract_sites(doc)
    fixed = 0
    for s in sites:
        sid = s.get("id") or s.get("siteId")
        if not sid:
            continue
        name = s.get("displayName", "")
        ref = git_by_id.get(sid, "")
        if not ref or name == ref:
            continue
        if "\ufffd" in name or name.endswith("�") or (len(ref) > len(name) and name in ref):
            s["displayName"] = ref
            fixed += 1

    if fixed:
        JSON_PATH.write_text(json.dumps(doc, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"fixed {fixed} site display names")


if __name__ == "__main__":
    main()
