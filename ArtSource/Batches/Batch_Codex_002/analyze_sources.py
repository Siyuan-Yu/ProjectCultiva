from pathlib import Path
from PIL import Image
import numpy as np
from collections import deque

ROOT = Path(__file__).resolve().parents[2]
for path in sorted((ROOT / "Batches/Batch_Codex_001/Originals").glob("ART-CHAR*.png")):
    a = np.asarray(Image.open(path).convert("RGBA"))[:, :, 3] > 30
    print(path.name, a.shape)
    for label, proj in (("x", a.sum(axis=0)), ("y", a.sum(axis=1))):
        empty = np.flatnonzero(proj == 0)
        runs = []
        for idx in empty:
            if runs and idx == runs[-1][-1] + 1:
                runs[-1].append(int(idx))
            else:
                runs.append([int(idx)])
        print(label, [(run[0], run[-1]) for run in runs if len(run) >= 3])
    if "idle" in path.name:
        continue
    h, w = a.shape
    visited = bytearray(h * w)
    comps = []
    for sy in range(h):
        for sx in range(w):
            seed = sy * w + sx
            if visited[seed] or not a[sy, sx]:
                continue
            visited[seed] = 1
            todo = deque([seed])
            count = 0
            minx = maxx = sx
            miny = maxy = sy
            while todo:
                pos = todo.popleft()
                y, x = divmod(pos, w)
                count += 1
                minx, maxx = min(minx, x), max(maxx, x)
                miny, maxy = min(miny, y), max(maxy, y)
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if 0 <= nx < w and 0 <= ny < h:
                        np0 = ny * w + nx
                        if not visited[np0] and a[ny, nx]:
                            visited[np0] = 1
                            todo.append(np0)
            if count >= 100:
                comps.append((count, (minx, miny, maxx + 1, maxy + 1)))
    print("components >=100:", len(comps), "largest:", sorted(comps, reverse=True)[:25])
