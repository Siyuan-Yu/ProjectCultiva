#!/usr/bin/env python3
"""Fix UTF-8 replacement-char corruption in Unity Host UI strings."""
import re
from pathlib import Path

HOST = Path(__file__).resolve().parents[1] / "Assets/Scripts/Unity/Host"

# Exact replacements (order matters for longer patterns first)
REPLACEMENTS = [
    # HostArmyFormPanel labels
    ('"Leader\ufffd\uff1a"', '"Leader："'),
    ('"Faction\ufffd\uff1a"', '"Faction："'),
    ('"State\ufffd\uff1a"', '"State："'),
    ('"Location\ufffd\uff1a"', '"Location："'),
    ('"Members\ufffd"', '"Members："'),
    ('" ? "', '" → "'),
    ('"无可用势力身份，无法组军\ufffd"', '"无可用势力身份，无法组军。"'),
    ('"选择未编组角\ufffd"', '"选择未编组角色"'),
    ('"当前没有可组建军队的角色\ufffd"', '"当前没有可组建军队的角色。"'),
    ('"请至少选择一名角\ufffd。"', '"请至少选择一名角色。"'),
    ('"已创\ufffd?"', '"已创建 "'),
    ('"已更\ufffd?Leader"', '"已更新 Leader"'),
    ('"已移除成\ufffd"', '"已移除成员"'),
    ('"已驻\ufffd\uff1a"', '"已驻扎。"'),
    ('"已解\ufffd"', '"已解散。"'),
    ('"已添\ufffd?"', '"已添加 "'),
    ('" 名成\ufffd。"', '" 名成员。"'),
    # HostWorldMapPanel army labels
    ('+ "人·尸\ufffd"', '+ "人·尸体"'),
    ('+ "人·弥\ufffd"', '+ "人·弥留"'),
    ('+ "\ufffd?· "', '+ "人 · "'),
    ('+ stack.MemberCount + "\ufffd?· "', '+ stack.MemberCount + "人 · "'),
    ('"残留战场\ufffd\uff1a"', '"残留战场："'),
    ('"下令攻击\ufffd\uff1a"', '"下令攻击："'),
    ('"人数\ufffd\uff1a"', '"人数："'),
    ('"弥留残留\ufffd\uff1a"', '"弥留残留："'),
    ('"尸体残留\ufffd\uff1a"', '"尸体残留："'),
    ('"解除驻\ufffd?Mobilize\ufffd。"', '"解除驻扎 Mobilize」。"'),
    ('"解除驻\ufffd?Mobilize\ufffd。"', '"解除驻扎 Mobilize」。"'),
    ('"已选到\ufffd?"', '"已选到达 "'),
    ('"已选军\ufffd?"', '"已选军团 "'),
    ('"Debug: 单人自动战必弥\ufffd\ufffd\ufffd\ufffd。"', '"Debug: 单人自动战必弥留。"'),
    ('" ] 调倍\ufffd\ufffd\ufffd\ufffd"', '" ] 调倍速"'),
    # Generic: replacement char before fullwidth colon in quoted UI strings
]

GENERIC_PATTERNS = [
    (re.compile(r'"([^"\r\n]*?)\ufffd\uff1a"'), r'"\1："'),
    (re.compile(r'"([^"\r\n]*?)\ufffd"'), r'"\1"'),
    (re.compile(r'"([^"\r\n]*?)\ufffd\?([^"\r\n]*?)"'), r'"\1\2"'),
    (re.compile(r'(\+ [^+]+ \+ )"\ufffd\?· "'), r'\1"人 · "'),
    (re.compile(r'(\+ [^+]+ \+ )"\?· "'), r'\1"人 · "'),
]


def fix_file(path: Path) -> int:
    original = path.read_text(encoding="utf-8")
    text = original
    for old, new in REPLACEMENTS:
        text = text.replace(old, new)
    for pattern, repl in GENERIC_PATTERNS:
        text = pattern.sub(repl, text)
    if text != original:
        path.write_text(text, encoding="utf-8", newline="\n")
        return original.count("\ufffd") - text.count("\ufffd")
    return 0


def main():
    total = 0
    for path in sorted(HOST.rglob("*.cs")):
        if "\ufffd" not in path.read_text(encoding="utf-8"):
            continue
        removed = fix_file(path)
        if removed > 0:
            remaining = path.read_text(encoding="utf-8").count("\ufffd")
            print(f"{path.name}: removed {removed} replacement chars, {remaining} remain")
            total += removed
    print(f"Done. Total replacement chars removed: {total}")


if __name__ == "__main__":
    main()
