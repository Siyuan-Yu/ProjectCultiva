"""Approved Huangcun-only 1.5 physical furniture scale and layout authoring transform.

Run once before FinalWorldPublish. Stable IDs, residents and other cities are retained.
This edits authoring data; it does not launch or simulate the game.
"""
import copy
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PACKAGE = ROOT / 'Content/BaseGame'
WORLD = PACKAGE / 'Authoring/ContinuousSurface/Worlds/main_world.worldcomposition.json'
BLUEPRINT = PACKAGE / 'Authoring/ContinuousSurface/Blueprints/huangcun.worldsiteblueprint.json'
VERSION = '0.0.41-huangcun-furniture-scale'
SCALE = 1.5
KINDS = {'room', 'bed', 'storageRoom', 'cushion', 'chessBoard',
         'alchemyFurnace', 'smithingFurnace', 'controlCore', 'recoverySpot', 'prisonDropOff'}


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def write(path, obj):
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def rect(o):
    return tuple(o[k] for k in ('localSurfaceX', 'localSurfaceY', 'widthCells', 'heightCells'))


def overlap(a, b, gap=0):
    return a[0] < b[0]+b[2]+gap-1e-7 and b[0] < a[0]+a[2]+gap-1e-7 and \
        a[1] < b[1]+b[3]+gap-1e-7 and b[1] < a[1]+a[3]+gap-1e-7


def inside(o, room):
    x, y, w, h = rect(o)
    rx, ry, rw, rh = rect(room)
    return x >= rx+1 and y >= ry+1 and x+w <= rx+rw-1 and y+h <= ry+rh-1


def main():
    bp, world = read(BLUEPRINT), read(WORLD)
    if any(o.get('metadata', {}).get('physicalScale') in ('1.5', '2') for o in bp['objectPlacements']):
        print('Huangcun furniture scale already authored; no second enlargement.')
        return
    old = copy.deepcopy(bp)
    old_by_id = {o['placementId']: o for o in old['objectPlacements']}
    by_id = {o['placementId']: o for o in bp['objectPlacements']}
    rooms = [o for o in old['objectPlacements'] if o['kindId'] == 'room']
    children = {o['placementId']: [] for o in rooms}
    child_ids = set()
    for o in old['objectPlacements']:
        if o['kindId'] == 'room':
            continue
        parent = o.get('metadata', {}).get('parentRoomId')
        if parent not in children:
            parent = next((r['placementId'] for r in rooms if inside(o, r)), None)
        if parent:
            children[parent].append(o['placementId'])
            child_ids.add(o['placementId'])

    def scaled(o):
        original = old_by_id[o['placementId']]
        if o['kindId'] in KINDS:
            m = o.setdefault('metadata', {})
            m.update(physicalScale='1.5', legacyGeometryState='edited',
                     sourceCellsW=str(round(original['widthCells'])),
                     sourceCellsH=str(round(original['heightCells'])))
            o['widthCells'] = original['widthCells'] * SCALE
            o['heightCells'] = original['heightCells'] * SCALE

    targets = {}
    residences = [r for r in rooms if ':room_res_' in r['placementId'] and
                  not r['placementId'].endswith(('room_res_10', 'room_res_11'))]
    for i, r in enumerate(residences):
        targets[r['placementId']] = (444 + i % 6 * 11, 560 + i // 6 * 14)
    named = {'room_res_10': (546, 496), 'room_res_11': (557, 496),
             'room_supervisor': (566, 478), 'room_prison': (568, 448),
             'room_medical': (532, 578), 'practice_house': (576, 552),
             'manual_hall': (554, 552), 'alchemy_room': (554, 578),
             'smithy_room': (576, 578)}
    for name, target in named.items():
        targets['base:site_huangcun:' + name] = target
    occupied = []
    # Door approaches and the main streets stay empty, independently of artwork size.
    protected = [(508, 432, 24, 176), (432, 508, 176, 24)]
    for room in rooms:
        target = by_id[room['placementId']]
        x, y = targets[room['placementId']]
        scaled(target)
        target['localSurfaceX'], target['localSurfaceY'] = x, y
        for child_id in children[room['placementId']]:
            child, previous = by_id[child_id], old_by_id[child_id]
            scaled(child)
            child['localSurfaceX'] = x + (previous['localSurfaceX']-room['localSurfaceX']) * SCALE
            child['localSurfaceY'] = y + (previous['localSurfaceY']-room['localSurfaceY']) * SCALE
            child.setdefault('metadata', {})['legacyGeometryState'] = 'edited'
        occupied.append(rect(target))
        protected.append((x-2, y-4, target['widthCells']+4, target['heightCells']+6))

    core = next(o for o in bp['objectPlacements'] if o['kindId'] == 'controlCore')
    scaled(core)
    core['localSurfaceX'] = core['localSurfaceY'] = 514
    occupied.append(rect(core))
    protected.append((510, 510, 20, 20))
    remaining = [o for o in bp['objectPlacements'] if o['kindId'] in KINDS and
                 o['kindId'] not in ('room', 'controlCore') and o['placementId'] not in child_ids]
    board_index = 0
    for o in remaining:
        scaled(o)
        if o['kindId'] == 'storageRoom':
            xy = (536, 538)
        elif o['kindId'] == 'chessBoard':
            xy = [(489, 542), (499, 542), (541, 542), (586, 538), (550, 470)][board_index]
            board_index += 1
        else:
            # Old standalone meditation cushions remain in the eastern spring precinct.
            original = old_by_id[o['placementId']]
            xy = (582+(original['localSurfaceX']-582)*SCALE,
                  480+(original['localSurfaceY']-480)*SCALE)
        o['localSurfaceX'], o['localSurfaceY'] = xy
        occupied.append(rect(o))
        protected.append((xy[0]-2, xy[1]-2, o['widthCells']+4, o['heightCells']+4))

    for kind, xs, ys in [('grainField', [444, 452, 460, 468], [462, 470]),
                         ('herbField', [444, 456, 468], [488, 497])]:
        fields = [o for o in bp['objectPlacements'] if o['kindId'] == kind]
        for i, o in enumerate(fields):
            o['localSurfaceX'], o['localSurfaceY'] = xs[i % len(xs)], ys[i // len(xs)]
            o.setdefault('metadata', {})['legacyGeometryState'] = 'edited'
            occupied.append(rect(o))
            protected.append(rect(o))

    natural = [o for o in bp['objectPlacements'] if o['kindId'] in ('treeM', 'treeL', 'ore') and
               431 <= o['localSurfaceX'] < 609 and 431 <= o['localSurfaceY'] < 609]
    # Relocate only conflicting natural props. Identity and resource quantities remain intact.
    for o in natural:
        original = rect(o)
        if not any(overlap(original, r, 1) for r in protected+occupied):
            occupied.append(original)
            continue
        candidates = [(x, y) for y in range(434, 605, 3) for x in range(434, 605, 3)
                      if x+o['widthCells'] <= 607 and y+o['heightCells'] <= 607]
        candidates.sort(key=lambda p: ((p[0]-original[0])**2+(p[1]-original[1])**2, p[1], p[0]))
        found = next(((x, y) for x, y in candidates
                      if not any(overlap((x, y, original[2], original[3]), r, 1)
                                 for r in protected+occupied)), None)
        if found is None:
            raise ValueError('No clear authored location for ' + o['placementId'])
        o['localSurfaceX'], o['localSurfaceY'] = found
        o.setdefault('metadata', {})['legacyGeometryState'] = 'edited'
        occupied.append(rect(o))

    # Source rectangle integrity: every scaled child stays in its original parent and no rooms overlap.
    new_rooms = [by_id[r['placementId']] for r in rooms]
    for i, room in enumerate(new_rooms):
        if any(overlap(rect(room), rect(other), 2) for other in new_rooms[i+1:]):
            raise ValueError('Room envelopes overlap: ' + room['placementId'])
        for child_id in children[room['placementId']]:
            if not inside(by_id[child_id], room):
                raise ValueError('Furniture outside room: ' + child_id)
    for o in bp['objectPlacements']:
        if o['kindId'] in KINDS and o['placementId'] not in child_ids:
            x, y, w, h = rect(o)
            if x < 432 or y < 432 or x+w > 608 or y+h > 608:
                raise ValueError('Facility outside village: ' + o['placementId'])

    placement = next(p for p in world['worldSiteBlueprintPlacements'] if p['blueprintId'] == bp['blueprintId'])
    px, py = placement['surfaceCellX'], placement['surfaceCellY']
    layout = world['runtimeLayout']
    for place in layout['sitePlaces']:
        if place['siteId'] != bp['blueprintId']:
            continue
        bound = [o for o in bp['objectPlacements'] if o.get('metadata', {}).get('boundLocationId') == place['locationId']]
        if not bound:
            continue
        place['surfaceCellX'] = px+(min(o['localSurfaceX'] for o in bound)+max(o['localSurfaceX']+o['widthCells'] for o in bound))/2
        place['surfaceCellY'] = py+(min(o['localSurfaceY'] for o in bound)+max(o['localSurfaceY']+o['heightCells'] for o in bound))/2
    # The public warehouse has a separately authored approach place, not a bound location on its asset.
    store = next(o for o in remaining if o['kindId'] == 'storageRoom')
    for p in layout['sitePlaces']:
        if p['locationId'] == 'base:loc_huangcun_store':
            p['surfaceCellX'], p['surfaceCellY'] = px+store['localSurfaceX']+store['widthCells']/2, py+store['localSurfaceY']-1
    for bed in (o for o in bp['objectPlacements'] if o['kindId'] == 'bed'):
        key = bed.get('metadata', {}).get('initialAssigneeSpawnKey')
        if not key:
            continue
        x, y, w, h = rect(bed)
        # Full Surface-cell standing point below the larger bed, inside the larger room.
        spawn = (math.floor(x+w/2)+.5, math.floor(y)-.5)
        for a in layout['openingEntityAnchors']:
            if a['siteId'] == bp['blueprintId'] and a['spawnKey'] == key:
                a['surfaceCellX'], a['surfaceCellY'] = px+spawn[0], py+spawn[1]
    for path in [PACKAGE/'manifest.json', WORLD.parent/'final_world_compaction_v1.json']:
        key = 'version' if path.name == 'manifest.json' else 'contentVersion'
        old_version = read(path)[key]
        path.write_text(path.read_text(encoding='utf-8-sig').replace(old_version, VERSION), encoding='utf-8')
    write(BLUEPRINT, bp)
    write(WORLD, world)
    print(f'Huangcun authored: {len(new_rooms)} rooms, {len(child_ids)} attached objects, {len(natural)} natural props retained; {VERSION}')


if __name__ == '__main__':
    main()
