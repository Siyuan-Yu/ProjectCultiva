"""Offline authored/published geometry and village connectivity checks (no Unity required)."""
import collections
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CONTENT = ROOT / "Content/BaseGame"


def read(relative):
    return json.loads((CONTENT / relative).read_text(encoding="utf-8-sig"))


def definition(relative):
    return read(relative)["definitions"][0]


def check(condition, message):
    if not condition:
        raise AssertionError(message)
    print("PASS", message)


world = read("Authoring/ContinuousSurface/Worlds/main_world.worldcomposition.json")
blueprint = read("Authoring/ContinuousSurface/Blueprints/huangcun.worldsiteblueprint.json")
surface = definition("Data/Worlds/main_wilderness_surface_v1.json")
geography = definition("Data/Worlds/w2a_surface_geography_baked_v1.json")
terrain = definition("Data/Worlds/main_world_surface_map_v1.json")
check((world["surfaceWidthCells"], world["surfaceHeightCells"]) == (3800, 1700), "author world 3800x1700 / area 6,460,000")
check((geography["width"], geography["height"]) == (3800, 1700), "navigation bake dimensions")
check((terrain["widthCells"], terrain["heightCells"]) == (3800, 1700), "terrain cache dimensions")
check(surface["cellSize"] == 0.028 and surface["movementScale"] == 1, "original CellSize and movement scale")
expected = {(x, y) for x in range(76) for y in range(34)}
for name, chunks in [("Surface", surface["chunks"]), ("geography", geography["coverageChunks"])]:
    check(len(chunks) == 2584 and {(p["x"], p["y"]) for p in chunks} == expected, name + " complete unique Chunk coverage")
for chunk in geography["chunkRows"]:
    x, y = chunk["x"] * 50, chunk["y"] * 50
    check_rows = [geography["rows"][y + row][x:x + 50] for row in range(50)]
    assert chunk["rows"] == check_rows, (x, y)
check(len(geography["chunkRows"]) == 2584, "all Chunk/global rows identical")
for key in ["baseTerrainRows", "forestRows"]:
    check(len(terrain[key]) == 1700 and all(len(row) == 3800 for row in terrain[key]), key + " complete world coverage")
counts = collections.Counter("".join(terrain["baseTerrainRows"]))
check(all(counts[c] > 0 for c in "PMW"), "Plain / Mountain / Water retained")
for x0, y0 in [(1900, 0), (0, 850), (1900, 850)]:
    rows = terrain["baseTerrainRows"][y0:y0 + 850]
    forest = terrain["forestRows"][y0:y0 + 850]
    check(any("M" in row[x0:x0 + 1900] for row in rows) and any(set(row[x0:x0 + 1900]) - {"0"} for row in forest),
          f"expanded quadrant {x0},{y0} contains mountains and forest")
check(len(surface["siteRegions"]) == 6 and {s["siteId"] for s in surface["siteRegions"]} ==
      {"base:site_" + name for name in ["huangcun", "chengzhen", "guanai", "zhuangyuan", "lingdi", "linjian"]}, "six original stable WorldSites")
for collection, key in [("sitePlaces", "locationId"), ("sitePlacements", "stableId"), ("openingEntityAnchors", "spawnKey")]:
    entries = surface[collection]
    check(len({o[key] for o in entries}) == len(entries), collection + " unique identities")

objects = blueprint["objectPlacements"]
rooms = {o["placementId"]: o for o in objects if o["kindId"] == "room"}
beds = [o for o in objects if o["kindId"] == "bed"]
check(blueprint["widthCells"] >= 120 and blueprint["heightCells"] >= 100, "village source at least 120x100")
check(len(rooms) == 22 and len(beds) == 27, "22 rooms / 27 beds")
check(collections.Counter(o["metadata"]["roomUsage"] for o in rooms.values()) == {"Residential": 20, "Medical": 1, "Prison": 1}, "original room usage counts")
assigned = {o["metadata"]["initialAssigneeSpawnKey"] for o in beds if o["metadata"].get("initialAssigneeSpawnKey")}
check(len(assigned) == 20, "20 unique residential SpawnKey bindings")
residential = [o for o in rooms.values() if o["metadata"]["roomUsage"] == "Residential"]
fields = [o for o in objects if o["kindId"] in {"grainField", "herbField"}]
gap = min(o["localSurfaceY"] for o in residential) - max(o["localSurfaceY"] + o["heightCells"] for o in fields)
check(12 <= gap <= 18, f"residential / production edge gap {gap:g} cells")
check(all((o["widthCells"], o["heightCells"]) in {(4, 6), (8, 12), (16, 24)} for o in rooms.values()) and
      all((o["widthCells"], o["heightCells"]) == (1, 2) for o in beds), "room/bed template sizes preserved")
core = next(o for o in objects if o["kindId"] == "controlCore")
center_x = core["localSurfaceX"] + core["widthCells"] / 2
center_y = core["localSurfaceY"] + core["heightCells"] / 2
check(center_x - 75 <= 0 and center_y - 75 <= 0 and center_x + 75 >= blueprint["widthCells"] and
      center_y + 75 >= blueprint["heightCells"], "entire village inside unchanged 150x150 administrative range")

# Reconstruct the actual room perimeter, south-facing two-cell door, furniture and
# solid placement raster. Flood fill tests physical topology independently of NPC AI.
width, height = blueprint["widthCells"], blueprint["heightCells"]
blocked = set()


def block_rect(x, y, w, h):
    for cy in range(max(0, math.floor(y)), min(height, math.ceil(y + h))):
        for cx in range(max(0, math.floor(x)), min(width, math.ceil(x + w))):
            if x <= cx + .5 < x + w and y <= cy + .5 < y + h:
                blocked.add((cx, cy))


for obj in objects:
    if obj["metadata"].get("blocksMovement") != "true":
        continue
    x, y, w, h = [obj[key] for key in ("localSurfaceX", "localSurfaceY", "widthCells", "heightCells")]
    if obj["kindId"] == "room":
        for gy in range(int(h)):
            for gx in range(int(w)):
                if gx in {0, w - 1} or gy in {0, h - 1}:
                    if gy == 0 and (w - 2) // 2 <= gx < (w - 2) // 2 + 2:
                        continue
                    block_rect(x + gx, y + gy, 1, 1)
    else:
        block_rect(x, y, w, h)
start = (64, 50)
seen = {start}
queue = collections.deque([start])
while queue:
    x, y = queue.popleft()
    for dx, dy in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
        point = (x + dx, y + dy)
        if 0 <= point[0] < width and 0 <= point[1] < height and point not in blocked and point not in seen:
            seen.add(point)
            queue.append(point)
for bed in beds:
    access = (math.floor(bed["localSurfaceX"] + .5), math.floor(bed["localSurfaceY"] - .5))
    assert access in seen, "Unreachable bed: " + bed["placementId"]
check(True, "all 27 bed access cells connected through doors to village roads")
for field in fields:
    point = (math.floor(field["localSurfaceX"] + field["widthCells"] / 2), math.floor(field["localSurfaceY"] + field["heightCells"] / 2))
    assert point in seen, "Unreachable production field: " + field["placementId"]
check(True, "all grain/herb plots connected to residential beds")
placement = world["worldSiteBlueprintPlacements"][0]
for spawn in surface["openingEntityAnchors"]:
    if spawn["siteId"] != "base:site_huangcun":
        continue
    x = (spawn["worldX"] - surface["originWorldX"]) / surface["cellSize"] - placement["surfaceCellX"]
    y = (spawn["worldY"] - surface["originWorldY"]) / surface["cellSize"] - placement["surfaceCellY"]
    assert (math.floor(x), math.floor(y)) in seen, "Blocked/unmoved opening anchor: " + spawn["spawnKey"]
check(True, "all 20 village opening anchors at reachable new positions")
for obj in world["worldObjectPlacements"]:
    if obj["kindId"] != "bridge":
        continue
    x, y, w, h = [obj[key] for key in ("worldSurfaceX", "worldSurfaceY", "widthCells", "heightCells")]
    bridge_rows = [geography["rows"][yy][x:x + w] for yy in range(y, y + h)]
    assert all(set(row) == {"B"} for row in bridge_rows)
    assert any("W" in terrain["baseTerrainRows"][yy][x:x + w] for yy in range(y, y + h))
    assert any(geography["rows"][yy][x - 1] == "=" for yy in range(y, y + h))
    assert any(geography["rows"][yy][x + w] == "=" for yy in range(y, y + h))
check(True, "bridge overlays river with roads connected to both ends")
print("WORLD-EXPAND-04 data/topology checks complete. Terrain counts:", dict(counts))
