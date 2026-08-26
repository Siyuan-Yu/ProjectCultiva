from pathlib import Path

path = Path(r"F:\ProjectCultiva\ProjectCultiva\Assets\Scripts\Core\World\Strategic\StrategicEncounterResolveService.cs")
text = path.read_text(encoding="utf-8", errors="replace")
depth = 0
in_string = False
escape = False
in_line_comment = False
in_block_comment = False
for i, line in enumerate(text.splitlines(), 1):
    j = 0
    while j < len(line):
        c = line[j]
        c2 = line[j:j+2]
        if in_line_comment:
            break
        if in_block_comment:
            if c2 == "*/":
                in_block_comment = False
                j += 2
                continue
            j += 1
            continue
        if in_string:
            if escape:
                escape = False
            elif c == "\\":
                escape = True
            elif c == '"':
                in_string = False
            j += 1
            continue
        if c2 == "//":
            in_line_comment = True
            break
        if c2 == "/*":
            in_block_comment = True
            j += 2
            continue
        if c == '"':
            in_string = True
            j += 1
            continue
        if c == '{':
            depth += 1
        elif c == '}':
            depth -= 1
            if depth < 0:
                print(f"extra }} at {i}")
        j += 1
    in_line_comment = False
    if i in (690, 691, 692):
        print(f"line {i} depth={depth}: {line.rstrip()[:80]}")
print(f"final depth={depth}")
