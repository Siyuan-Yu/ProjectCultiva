#!/usr/bin/env python3
"""Fix unclosed C# string literals corrupted during encoding migration."""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "Assets"


def fix_content(text: str) -> str:
    text = re.sub(r'"([^"\r\n]*)\?,\s*(_body|_title|_avatarLabel)', r'"\1", \2', text)
    text = re.sub(
        r'"([^"\r\n]*)\?,\s*\r?\n(\s*)(_body|_title)',
        lambda m: f'"{m.group(1)}",\n{m.group(2)}{m.group(3)}',
        text,
    )
    text = re.sub(r'"([^"\r\n]*)\?\)', r'"\1")', text)
    text = re.sub(r'"([^"\r\n]*)\?,', r'"\1",', text)
    text = re.sub(r'"([^"\r\n]*)\?\s*:', r'"\1" :', text)
    text = re.sub(
        r'"([^"\r\n]*)\?,\s*\r?\n(\s*)(this\))',
        lambda m: f'"{m.group(1)}",\n{m.group(2)}{m.group(3)}',
        text,
    )
    text = re.sub(r'"([^"\r\n]*)\?\s*\};', r'"\1" };', text)
    text = re.sub(r'"([^"\r\n]*)\?\s*;', r'"\1";', text)
    text = text.replace('+ "?· ', '+ "\\u4eba \\u00b7 "')
    return text


def main():
    fixed = []
    for path in ROOT.rglob("*.cs"):
        original = path.read_text(encoding="utf-8", errors="replace")
        updated = fix_content(original)
        if updated != original:
            path.write_text(updated, encoding="utf-8", newline="\n")
            fixed.append(str(path.relative_to(ROOT.parent)))
    print(f"Fixed {len(fixed)} files:")
    for f in fixed:
        print(f"  {f}")


if __name__ == "__main__":
    main()
