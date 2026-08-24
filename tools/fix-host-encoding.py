#!/usr/bin/env python3
"""Repair encoding corruption in Unity Host .cs files after Pure Hex migration."""
from __future__ import annotations

import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
HOST = ROOT / "Assets/Scripts/Unity/Host"
GIT = ["git", "-C", str(ROOT)]


def git_show(rev: str, relpath: str) -> str | None:
    try:
        data = subprocess.check_output(GIT + ["show", f"{rev}:{relpath}"])
        return data.decode("utf-8")
    except (subprocess.CalledProcessError, UnicodeDecodeError):
        return None


def normalize_line_key(line: str) -> str:
    """Match lines that differ only by corrupted string literals."""
    s = re.sub(r'"[^"]*"', '""', line)
    s = s.replace("\ufffd", "")
    return s.strip()


def graft_strings_from_git(current: str, git_text: str) -> str:
    git_lines = git_text.splitlines()
    git_by_key = {}
    for gl in git_lines:
        if "\ufffd" in gl:
            continue
        key = normalize_line_key(gl)
        if key and ('"' in gl or "//" in gl or "///" in gl):
            git_by_key[key] = gl

    out = []
    replaced = 0
    for line in current.splitlines():
        if "\ufffd" not in line:
            out.append(line)
            continue
        key = normalize_line_key(line)
        donor = git_by_key.get(key)
        if donor and "\ufffd" not in donor:
            out.append(donor)
            replaced += 1
        else:
            out.append(line)
    return "\n".join(out) + ("\n" if current.endswith("\n") else ""), replaced


def apply_common_replacements(text: str) -> str:
    rules = [
        ('\ufffd\uff1a', '：'),
        ('\ufffd?', ''),
        ('\ufffd', ''),
        ('"?· ', '人 · '),
        ('"?·', '人·'),
        ('+ "人 · " + " 战力"', '+ "人 · 战力"'),  # no-op safety
    ]
    for old, new in rules:
        text = text.replace(old, new)
    # Fix doubled artifacts
    text = re.sub(r'Leader：：', 'Leader：', text)
    text = re.sub(r'Faction：：', 'Faction：', text)
    text = re.sub(r'State：：', 'State：', text)
    text = re.sub(r'Location：：', 'Location：', text)
    return text


def fix_file(path: Path) -> tuple[int, int]:
    relpath = path.relative_to(ROOT).as_posix()
    raw = path.read_bytes()
    try:
        current = raw.decode("utf-8")
    except UnicodeDecodeError:
        current = raw.decode("utf-8", errors="replace")

    before = current.count("\ufffd")
    git_text = git_show("1e89a7b", relpath)
    if git_text:
        current, _ = graft_strings_from_git(current, git_text)
    current = apply_common_replacements(current)
    after = current.count("\ufffd")
    if current != raw.decode("utf-8", errors="replace"):
        path.write_text(current, encoding="utf-8", newline="\n")
    return before, after


def main():
    total_before = total_after = 0
    for path in sorted(HOST.rglob("*.cs")):
        before, after = fix_file(path)
        if before:
            print(f"{path.name}: {before} -> {after} remaining")
            total_before += before
            total_after += after
    print(f"Total replacement chars: {total_before} -> {total_after}")


if __name__ == "__main__":
    main()
