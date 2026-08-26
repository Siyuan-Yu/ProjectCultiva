import re
from pathlib import Path

path = Path(r"F:\ProjectCultiva\ProjectCultiva\Assets\Scripts\Core\World\Strategic\StrategicEncounterResolveService.cs")
text = path.read_text(encoding="utf-8", errors="replace")
in_string = False
escape = False
for i, line in enumerate(text.splitlines(), 1):
    for j, ch in enumerate(line):
        if in_string:
            if escape:
                escape = False
            elif ch == "\\":
                escape = True
            elif ch == '"':
                in_string = False
        else:
            if ch == '"':
                in_string = True
    if in_string:
        print(f"UNCLOSED at line {i}: {line[:100]}")
if in_string:
    print("FILE ENDS inside string")
