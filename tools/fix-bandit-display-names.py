#!/usr/bin/env python3
from pathlib import Path

path = Path(r"F:\ProjectCultiva\ProjectCultiva\Assets\Scripts\Core\World\Strategic\ArmyStackAdapter.cs")
text = path.read_text(encoding="utf-8", errors="replace")
replacements = [
    ('BanditPatrolStackId, "????"', 'BanditPatrolStackId, "荒村山匪"'),
    ('BanditWeakPatrolStackId, "??????????"', 'BanditWeakPatrolStackId, "试炼弱匪（自动必胜）"'),
    ('BanditScoutStackId, "????"', 'BanditScoutStackId, "山匪斥候"'),
]
for old, new in replacements:
    count = text.count(old)
    if count:
        print(f"replace {count}x: {old} -> {new}")
        text = text.replace(old, new)
path.write_text(text, encoding="utf-8", newline="\n")
print("done")
