"""Apply the producer's housing/nearby-fields/staff-facility revision to Huangcun only."""
import copy
import json
import math
import runpy
from pathlib import Path

helpers = runpy.run_path(str(Path(__file__).with_name('huangcun-furniture-scale.py')))
ROOT, PACKAGE, WORLD, BLUEPRINT = (helpers[k] for k in ('ROOT', 'PACKAGE', 'WORLD', 'BLUEPRINT'))
read, write, rect, overlap, inside = (helpers[k] for k in ('read', 'write', 'rect', 'overlap', 'inside'))
VERSION = '0.0.42-huangcun-neighborhood'


def main():
    bp, world = read(BLUEPRINT), read(WORLD)
    if any(o['placementId'] == 'base:site_huangcun:combat_hall' for o in bp['objectPlacements']):
        print('Refreshing the existing Huangcun neighborhood without adding duplicate facilities.')
    if not any(o.get('metadata', {}).get('physicalScale') == '1.5' for o in bp['objectPlacements']):
        raise ValueError('Apply huangcun-furniture-scale.py first.')
    old = copy.deepcopy(bp)
    rooms = [o for o in old['objectPlacements'] if o['kindId'] == 'room']
    children = {r['placementId']: [] for r in rooms}
    for o in old['objectPlacements']:
        if o['kindId'] == 'room':
            continue
        parent = o.get('metadata', {}).get('parentRoomId')
        if parent not in children:
            parent = next((r['placementId'] for r in rooms if inside(o, r)), None)
        if parent:
            children[parent].append(o['placementId'])
    occupied_homes = {o['metadata']['parentRoomId'] for o in old['objectPlacements']
                      if o['kindId'] == 'bed' and o.get('metadata', {}).get('initialAssigneeSpawnKey')}
    ordinary = [r for r in rooms if ':room_res_' in r['placementId'] and
                not r['placementId'].endswith(('room_res_10', 'room_res_11'))]
    occupied = [r for r in ordinary if r['placementId'] in occupied_homes]
    spare = [r for r in ordinary if r['placementId'] not in occupied_homes][:2]
    if len(occupied) != 10:
        raise ValueError('Expected ten ordinary resident homes including the captive companion.')
    kept_homes = {r['placementId'] for r in occupied+spare}
    retired = [r for r in ordinary if r['placementId'] not in kept_homes]
    retired_ids = {i for r in retired for i in [r['placementId']]+children[r['placementId']]}
    bp['objectPlacements'] = [o for o in bp['objectPlacements'] if o['placementId'] not in retired_ids]
    by_id = {o['placementId']: o for o in bp['objectPlacements']}
    targets = {r['placementId']: (444+i % 4*11, 558+i//4*14)
               for i, r in enumerate(occupied+spare)}
    for key, xy in {'room_supervisor': (558, 486), 'room_res_10': (542, 496),
                    'room_res_11': (542, 482), 'practice_house': (576, 482),
                    'manual_hall': (558, 460), 'room_prison': (590, 480)}.items():
        targets['base:site_huangcun:'+key] = xy
    for room in rooms:
        key = room['placementId']
        if key not in targets:
            continue
        x, y = targets[key]
        dx, dy = x-room['localSurfaceX'], y-room['localSurfaceY']
        for child_id in [key]+children[key]:
            if child_id not in by_id:
                continue
            o = by_id[child_id]
            o['localSurfaceX'] += dx
            o['localSurfaceY'] += dy
            o.setdefault('metadata', {})['legacyGeometryState'] = 'edited'

    hall = copy.deepcopy(by_id['base:site_huangcun:manual_hall'])
    hall.update(placementId='base:site_huangcun:combat_hall', contentRef='base:building_combat_hall_low',
                localSurfaceX=576, localSurfaceY=460)
    hall['metadata'].update(buildingId='base:building_combat_hall_low', label='低级斗技坊',
                            boundLocationId='base:loc_huangcun_combat_hall')
    if hall['placementId'] not in by_id:
        bp['objectPlacements'].append(hall)
    template = by_id['base:site_huangcun:manual_cushion_0']
    for i in range(3):
        cushion = copy.deepcopy(template)
        cushion.update(placementId=f'base:site_huangcun:combat_cushion_{i}',
                       localSurfaceX=578+i*3, localSurfaceY=467.5)
        cushion['metadata'].update(label='斗技坊练功席', boundLocationId='base:loc_huangcun_combat_hall')
        if cushion['placementId'] not in by_id:
            bp['objectPlacements'].append(cushion)

    for kind, xs, ys, w, h in [('grainField', [488, 499], [552, 562, 572, 582], 8, 6),
                               ('herbField', [444, 456, 468, 480, 492], [540], 10, 8)]:
        fields = [o for o in bp['objectPlacements'] if o['kindId'] == kind]
        for i, o in enumerate(fields):
            o.update(localSurfaceX=xs[i % len(xs)], localSurfaceY=ys[i//len(xs)], widthCells=w, heightCells=h)
            o['metadata'].update(legacyGeometryState='edited', sourceCellsW=str(w), sourceCellsH=str(h))
    boards = [o for o in bp['objectPlacements'] if o['kindId'] == 'chessBoard']
    for o, xy in zip(boards, [(484, 552), (496, 598), (541, 542), (586, 538), (550, 470)]):
        o['localSurfaceX'], o['localSurfaceY'] = xy
    spring = next(o for o in bp['objectPlacements'] if o['kindId'] == 'spring' and
                  431 <= o['localSurfaceX'] < 609 and 431 <= o['localSurfaceY'] < 609)
    dx, dy = 580-spring['localSurfaceX'], 552-spring['localSurfaceY']
    spring['localSurfaceX'], spring['localSurfaceY'] = 580, 552
    spring['metadata']['legacyGeometryState'] = 'edited'
    for o in bp['objectPlacements']:
        if o['kindId'] == 'cushion' and ':place_cushion_' in o['placementId']:
            o['localSurfaceX'] += dx
            o['localSurfaceY'] += dy
            o['metadata']['boundLocationId'] = 'base:loc_ref_spring'

    new_rooms = [o for o in bp['objectPlacements'] if o['kindId'] == 'room']
    fixed = [o for o in bp['objectPlacements'] if o['kindId'] in
             ('room', 'controlCore', 'storageRoom', 'grainField', 'herbField', 'chessBoard', 'spring', 'cushion')]
    protected = [(508, 432, 24, 176), (432, 508, 176, 24)]
    for o in fixed:
        x, y, w, h = rect(o)
        protected.append((x-2, y-4 if o['kindId'] == 'room' else y-1, w+4, h+6 if o['kindId'] == 'room' else h+2))
    accepted = []
    natural = [o for o in bp['objectPlacements'] if o['kindId'] in ('treeM', 'treeL', 'ore') and
               431 <= o['localSurfaceX'] < 609 and 431 <= o['localSurfaceY'] < 609]
    for o in natural:
        x, y, w, h = rect(o)
        if any(overlap((x, y, w, h), r, 1) for r in protected+accepted):
            candidates = [(xx, yy) for yy in range(434, 605, 3) for xx in range(434, 605, 3)
                          if xx+w <= 607 and yy+h <= 607]
            candidates.sort(key=lambda p: ((p[0]-x)**2+(p[1]-y)**2, p[1], p[0]))
            point = next((p for p in candidates if not any(overlap((*p, w, h), r, 1)
                                                          for r in protected+accepted)), None)
            if point is None:
                raise ValueError('No clear resource position: '+o['placementId'])
            o['localSurfaceX'], o['localSurfaceY'] = point
            o['metadata']['legacyGeometryState'] = 'edited'
        accepted.append(rect(o))
    for i, room in enumerate(new_rooms):
        if any(overlap(rect(room), rect(other), 2) for other in new_rooms[i+1:]):
            raise ValueError('Room overlap: '+room['placementId'])

    placement = next(p for p in world['worldSiteBlueprintPlacements'] if p['blueprintId'] == bp['blueprintId'])
    px, py = placement['surfaceCellX'], placement['surfaceCellY']
    layout = world['runtimeLayout']
    sample = copy.deepcopy(next(p for p in layout['sitePlaces'] if p['locationId'] == 'base:loc_huangcun_manual_hall'))
    sample.update(locationId='base:loc_huangcun_combat_hall', name='斗技坊')
    if not any(p['locationId'] == sample['locationId'] for p in layout['sitePlaces']):
        layout['sitePlaces'].append(sample)
    for place in layout['sitePlaces']:
        if place['siteId'] != bp['blueprintId']:
            continue
        group = [o for o in bp['objectPlacements'] if o.get('metadata', {}).get('boundLocationId') == place['locationId']]
        if group:
            place['surfaceCellX'] = px+(min(o['localSurfaceX'] for o in group)+max(o['localSurfaceX']+o['widthCells'] for o in group))/2
            place['surfaceCellY'] = py+(min(o['localSurfaceY'] for o in group)+max(o['localSurfaceY']+o['heightCells'] for o in group))/2
    for bed in (o for o in bp['objectPlacements'] if o['kindId'] == 'bed'):
        key = bed.get('metadata', {}).get('initialAssigneeSpawnKey')
        if not key:
            continue
        x, y, w, h = rect(bed)
        for a in layout['openingEntityAnchors']:
            if a['siteId'] == bp['blueprintId'] and a['spawnKey'] == key:
                a['surfaceCellX'], a['surfaceCellY'] = px+math.floor(x+w/2)+.5, py+math.floor(y)-.5
    buildings_path = PACKAGE/'Data/Buildings/buildings.json'
    buildings = read(buildings_path)
    spec = copy.deepcopy(next(d for d in buildings['definitions'] if d['id'] == 'base:building_manual_hall_low'))
    spec.update(id='base:building_combat_hall_low', name='低级斗技坊',
                description='低级木制，12×18格。斗技研习与练功空间，沿用现有秘籍学习与蒲团规则；不额外产生熟练或修为加成。')
    if not any(d['id'] == spec['id'] for d in buildings['definitions']):
        buildings['definitions'].append(spec)
    areas_path = PACKAGE/'Data/WorkAreas/work_areas.json'
    areas = read(areas_path)
    area = copy.deepcopy(next(d for d in areas['definitions'] if d['id'] == 'base:workarea_huangcun_manual_hall'))
    area.update(id='base:workarea_huangcun_combat_hall', name='斗技坊练功席',
                locationId='base:loc_huangcun_combat_hall', capacity=3)
    if not any(d['id'] == area['id'] for d in areas['definitions']):
        areas['definitions'].append(area)
    chars_path = PACKAGE/'Data/Characters/ch01_reference_characters.json'
    chars = read(chars_path)
    staff = {'base:character_ch01_ref_supervisor', 'base:character_ch01_ref_guard_a', 'base:character_ch01_ref_guard_b'}
    for d in chars['definitions']:
        if d['id'] in staff:
            d['preferredWorkAreaIds'] = ['base:workarea_huangcun_practice' if x == 'base:workarea_spring_cultivate' else x
                                         for x in d.get('preferredWorkAreaIds', [])]
    for path in [PACKAGE/'manifest.json', WORLD.parent/'final_world_compaction_v1.json']:
        key = 'version' if path.name == 'manifest.json' else 'contentVersion'
        path.write_text(path.read_text(encoding='utf-8-sig').replace(read(path)[key], VERSION), encoding='utf-8')
    for path, obj in [(BLUEPRINT, bp), (WORLD, world), (buildings_path, buildings), (areas_path, areas), (chars_path, chars)]:
        write(path, obj)
    print(f'Huangcun: {len(occupied)} occupied ordinary homes + {len(spare)} spare; {len(retired)} empty homes retired; {len(new_rooms)} rooms total; {VERSION}')


if __name__ == '__main__':
    main()
