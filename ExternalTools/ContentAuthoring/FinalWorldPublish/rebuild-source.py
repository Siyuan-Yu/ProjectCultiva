"""Rebuild the approved WORLD-REF-020 layout; leaves publication to WorldComposer.

Run from the repository root. All coordinates come from the reviewed CSV/polygon
sources. Local furniture dimensions and stable retained IDs are never scaled.
Requires numpy; this is an authoring transform, not a runtime generator or test.
"""
import copy
import csv
import json
import math
import runpy
import sys
from pathlib import Path
import numpy as np

ROOT = Path(__file__).resolve().parents[3]
PACKAGE = ROOT / 'Content/BaseGame'
SOURCE = PACKAGE / 'Authoring/ContinuousSurface'
WORLD = SOURCE / 'Worlds/main_world.worldcomposition.json'

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

world = read(WORLD)
geo = read(SOURCE / 'Worlds/final_world_geography_v1.json')
with (SOURCE / 'Worlds/final_world_site_plan_v1.csv').open(encoding='utf-8-sig', newline='') as f:
    plan = list(csv.DictReader(f))
for row in plan:
    for key in ('level', 'mapX', 'mapY'):
        row[key] = int(row[key])
SCALE = geo['cellsPerMapPixel']
compression_config = read(SOURCE / 'Worlds/final_world_compaction_v1.json')
Compaction = runpy.run_path(str(Path(__file__).with_name('world-compaction.py')))['Compaction']
compression = Compaction(geo, plan, compression_config)
W, H = compression.width, compression.height
# Approved additions use the frozen 60-town mapping, never a new global solve.
plan.extend(copy.deepcopy(compression_config.get('additionalGateSites',[])))
for row in plan:
    if row['oldSiteId'] in compression_config.get('siteOwnerOverrides',{}):
        row['ownerFactionId']=compression_config['siteOwnerOverrides'][row['oldSiteId']]
    if row['entryRegion'] in compression_config.get('closedBasins',[]):
        row['entryRegion']=''
if len({r['oldSiteId'] for r in plan})!=len(plan):
    raise ValueError('Duplicate authoring site ID')
beast=next(r for r in geo['regions'] if r['id']=='beast')
if compression_config.get('additionalGateSites'):
    beast['wall']=True
    # Keep the river untouched. The west gate stands on the east bank, with its
    # full reserve clear of the water; this local enclosing edge follows it.
    beast['points']=copy.deepcopy(compression_config['beastPerimeter'])
for region in geo['regions']:
    region.update(copy.deepcopy(compression_config.get('regionOverrides',{}).get(region['id'],{})))
if '--plan-only' in sys.argv:
    print(json.dumps(dict(width=W, height=H, axes=[dict(length=a.length, freeScale=a.scale,
        protected=a.intervals) for a in compression.axes]), ensure_ascii=False, indent=2))
    sys.exit(0)

# Preserve the original reference coordinates on disk; split at every warp knot so
# polygon containment and shared river/ridge geometry survive the remap exactly.
for r in geo['regions']+[geo['openingBasin']]:
    r['points']=compression.split_path(r['points'], closed=True)
geo['ridges']=[compression.split_path(p) for p in geo['ridges']]
for river in geo['rivers']:
    river['points']=compression.split_path(river['points'])

def point(p):
    x,y=compression.point(p)
    return (geo['offsetCellsX'] + compression.left_padding + x * SCALE,
            geo['offsetCellsY'] + compression.bottom_padding + (compression.axes[1].length - y) * SCALE)

def center(row):
    original=(geo['offsetCellsX']+row['mapX']*SCALE,
              geo['offsetCellsY']+(geo['mapHeight']-row['mapY'])*SCALE)
    moved=point((row['mapX'], row['mapY']))
    result=tuple(round(a)+round(b-a) for a,b in zip(original,moved))
    relocation=compression_config.get('siteRelocations',{}).get(row['oldSiteId'],{})
    if 'mapPoint' in relocation:
        result=tuple(round(v) for v in point(relocation['mapPoint']))
    return tuple(v+d for v,d in zip(result,relocation.get('offsetCells',(0,0))))

def region_point(region,p):
    offset=compression_config.get('regionCellOffsets',{}).get(region['id'],(0,0))
    return tuple(v+d for v,d in zip(point(p),offset))

def region_polygon(region):
    return [region_point(region,p) for p in region['points']]

def size(row):
    return 90 + row['level'] * 90

# Local basin sizing is applied AFTER deriving the global compaction, so changing
# this enclave cannot shift other towns, rivers or the world's protected strips.
basin_size=compression_config.get('openingBasinSizeCells')
if basin_size is not None:
    if basin_size < 960:raise ValueError('Round basin must preserve the approved five-level reserve and margin')
    row=next(r for r in plan if r['oldSiteId']=='base:site_huangcun')
    bx,by=center(row); px,py=point((row['mapX'],row['mapY']))
    refx=row['mapX']+(bx-px)/SCALE; refy=row['mapY']-(by-py)/SCALE
    def basin_ref(dx,dy):
        p=[refx+dx/SCALE,refy-dy/SCALE]
        mapped=point(p)
        if abs(mapped[0]-bx-dx)>1e-6 or abs(mapped[1]-by-dy)>1e-6:
            raise ValueError('Basin override extends outside its rigid protected region')
        return p
    half=basin_size/2
    angles=[i*math.tau/64 for i in range(64)]
    radii=[half+6*math.sin(3*a)*math.sin(2*a) for a in angles]
    geo['openingBasin']['points']=[basin_ref(r*math.cos(a),r*math.sin(a)) for a,r in zip(angles,radii)]
    geo['openingBasin']['innerGate']=basin_ref(half-40,0)
    geo['openingBasin']['outerGate']=basin_ref(half+120,0)
    # Slightly uneven circular mountain band lies outside the usable enclave.
    geo['openingBasin']['mountainOuter']=[basin_ref((r+90+8*math.sin(7*a))*math.cos(a),
        (r+90+8*math.sin(7*a))*math.sin(a)) for a,r in zip(angles,radii)]

def publish_squad_deployments():
    """Map both ends of remote deployment offsets; moving only the town is wrong."""
    sources={d['id']:d for d in compression_config.get('squadDeployments',[])}
    sites={r['oldSiteId']:r for r in plan}
    found=set()
    updates=[]
    for path in (PACKAGE/'Data/Armies').glob('*.json'):
        data=read(path);changed=False
        for definition in data.get('definitions',[]):
            source=sources.get(definition['id'])
            if source is None:continue
            if definition['id'] in found:raise ValueError('Duplicate squad deployment: '+definition['id'])
            found.add(definition['id'])
            deployment=definition.get('initialSurfaceDeployment')
            if deployment is None or deployment['anchorSiteId']!=source['anchorSiteId']:
                raise ValueError('Squad deployment anchor differs from authoring: '+definition['id'])
            cx,cy=center(sites[source['anchorSiteId']])
            x,y=point((source['mapX'],source['mapY']))
            deployment['offsetCellsX']=round(x)-cx
            deployment['offsetCellsY']=round(y)-cy
            changed=True
        if changed:updates.append((path,data))
    if found!=set(sources):raise ValueError('Unresolved authored squad deployments: '+str(set(sources)-found))
    for path,data in updates:write(path,data)
    print(f'Published {len(found)} authored squad deployment offsets.',flush=True)

if '--deployments-only' in sys.argv:
    # No terrain/blueprint rewrite or multi-minute raster bake is needed for a
    # deployment-only repair against the already published compressed world.
    if (world['surfaceWidthCells'],world['surfaceHeightCells'])!=(W,H):
        raise ValueError('Publish the matching world composition before squad-only repair')
    for row in plan:
        placed=next(s for s in world['runtimeLayout']['siteRegions'] if s['siteId']==row['oldSiteId'])
        if (placed['surfaceCellX'],placed['surfaceCellY'])!=center(row):
            raise ValueError('Published authoring town differs from compaction: '+row['displayName'])
    publish_squad_deployments()
    sys.exit(0)

# Authoring guards run before any file is rewritten. Full upgrade space, not just
# today's small town wall, must remain inside the world and separate from peers.
reserve=compression_config['townReserveCells']
def segment_touches_box(a,b,c,half):
    lo,hi=0.0,1.0
    for axis in (0,1):
        d=b[axis]-a[axis]
        if abs(d)<1e-9:
            if abs(a[axis]-c[axis])>half:return False
        else:
            x,y=sorted(((c[axis]-half-a[axis])/d,(c[axis]+half-a[axis])/d))
            lo=max(lo,x);hi=min(hi,y)
    return lo<=hi

for enclosed in (r for r in geo['regions'] if r['id'] in ('beast','thunder') and r.get('wall')):
    perimeter=region_polygon(enclosed)
    for row in plan:
        if row['entryRegion']==enclosed['id']:continue
        if any(segment_touches_box(a,b,center(row),reserve/2+40)
               for a,b in zip(perimeter,perimeter[1:]+perimeter[:1])):
            raise ValueError('New enclosing mountain band touches existing upgrade reserve: '+row['displayName'])
for i,row in enumerate(plan):
    cx,cy=center(row)
    if min(cx,cy,W-cx,H-cy) < reserve/2:
        raise ValueError('Five-level reserve outside world: '+row['displayName'])
    for other in plan[i+1:]:
        ox,oy=center(other)
        if max(abs(cx-ox),abs(cy-oy))-reserve < math.ceil(compression_config['minimumTownGapCells']):
            raise ValueError('Five-level reserves too close: '+row['displayName']+'/'+other['displayName'])

def replace_strings(value, replacements):
    if isinstance(value, str):
        for old, new in sorted(replacements.items(), key=lambda p: -len(p[0])):
            value = value.replace(old, new)
        return value
    if isinstance(value, list):
        return [replace_strings(v, replacements) for v in value]
    if isinstance(value, dict):
        return {k: replace_strings(v, replacements) for k, v in value.items()}
    return value

layout = world['runtimeLayout']
old_sites = {r['siteId']: copy.deepcopy(r) for r in layout['siteRegions']}
old_placements = {p['blueprintId']: p for p in world['worldSiteBlueprintPlacements']}
active_ids = {r['oldSiteId'] for r in plan}
retired_ids = set(old_sites) - active_ids
for key in ('siteRegions', 'sitePlaces', 'sitePlacements', 'openingEntityAnchors'):
    layout[key] = [r for r in layout[key] if r.get('siteId') not in retired_ids]
world['worldSiteBlueprintPlacements'] = [p for p in world['worldSiteBlueprintPlacements'] if p['blueprintId'] in active_ids]

def obj(id_, kind, x, y, w, h, label, **metadata):
    return dict(placementId=id_, kindId=kind, contentRef=None, assetRef=None,
                localSurfaceX=x, localSurfaceY=y, widthCells=w, heightCells=h,
                rotationQuarterTurns=0,
                metadata=dict(label=label, blocksMovement=str(kind in ('room','wall','controlCore')).lower(),
                              sourceCellsW=str(w), sourceCellsH=str(h), **metadata))

def new_blueprint(row):
    sid, name, n = row['oldSiteId'], row['displayName'], size(row)
    bp = dict(schemaVersion=4, blueprintId=sid, displayName=name, widthCells=n, heightCells=n,
              fineTerrainSources=[], objectPlacements=[])
    bp['objectPlacements'].append(obj(sid+':core', 'controlCore', n//2-4, n//2-4, 8, 8,
        name+'议政厅', boundLocationId=sid+':core_place', localPlaceName=name+'议政厅',
        localPlaceKind='Settlement', localPlaceTags='["site","controlCore"]', siteId=sid))
    for i, (x,y) in enumerate(((n//2-30,n//2+16),(n//2+24,n//2+16),(n//2-30,n//2-30),(n//2+24,n//2-30))):
        rid, lid = sid+':home:'+str(i), sid+':home_place:'+str(i)
        bp['objectPlacements'].append(obj(rid,'room',x,y,4,6,name+'住宅',
            buildingId='base:room_normal_wood', structureDurability='100', roomUsage='Residential',
            boundLocationId=lid, localPlaceName=name+'住宅', localPlaceTags='["settlement","hideMapLabel"]'))
        bp['objectPlacements'].append(obj(rid+':bed','bed',x+1,y+2,1,2,name+'床位',
            buildingId='base:furniture_bed', structureDurability='60', parentRoomId=rid, boundLocationId=lid))
    return bp

def fit_town(bp, row):
    """Keep fitting buildings; relocate only groups outside the new actual-level wall."""
    n = size(row)
    core = next(o for o in bp['objectPlacements'] if o['kindId']=='controlCore')
    dx = n/2 - core['localSurfaceX'] - core['widthCells']/2
    dy = n/2 - core['localSurfaceY'] - core['heightCells']/2
    objects = [o for o in bp['objectPlacements'] if o['kindId'] != 'wall']
    for o in objects:
        o['localSurfaceX'] = round(o['localSurfaceX'] + dx)
        o['localSurfaceY'] = round(o['localSurfaceY'] + dy)
    rooms = {o['placementId']: [o] for o in objects if o['kindId']=='room'}
    groups = []
    for o in objects:
        if o['kindId']=='room': continue
        parent = o.get('metadata',{}).get('parentRoomId')
        if parent in rooms: rooms[parent].append(o)
        else:
            # Cushions/chess and other furniture physically inside a room move with it.
            parent = next((key for key, group in rooms.items()
                if o['localSurfaceX'] >= group[0]['localSurfaceX']+1 and
                   o['localSurfaceY'] >= group[0]['localSurfaceY']+1 and
                   o['localSurfaceX']+o['widthCells'] <= group[0]['localSurfaceX']+group[0]['widthCells']-1 and
                   o['localSurfaceY']+o['heightCells'] <= group[0]['localSurfaceY']+group[0]['heightCells']-1),None)
            if parent: rooms[parent].append(o)
            else: groups.append([o])
    groups.extend(rooms.values())
    occupied = np.zeros((n,n), dtype=bool)
    # Preserve cardinal gate-to-town streets when relocating edge buildings.
    occupied[:,n//2-6:n//2+6] = True
    occupied[n//2-6:n//2+6,:] = True
    def bounds(g):
        return (min(o['localSurfaceX'] for o in g), min(o['localSurfaceY'] for o in g),
                max(o['localSurfaceX']+o['widthCells'] for o in g), max(o['localSurfaceY']+o['heightCells'] for o in g))
    moving = []
    for group in groups:
        x,y,x1,y1 = bounds(group)
        if min(x,y) >= 10 and max(x1,y1) <= n-10:
            occupied[max(0,y-1):min(n,y1+1),max(0,x-1):min(n,x1+1)] = True
        else: moving.append(group)
    for group in sorted(moving,key=lambda g:-(bounds(g)[2]-bounds(g)[0])*(bounds(g)[3]-bounds(g)[1])):
        x,y,x1,y1 = bounds(group); w,h=x1-x,y1-y
        spots = ((xx,yy) for yy in range(10,n-h-9,2) for xx in range(10,n-w-9,2)
                 if not occupied[yy-1:yy+h+1,xx-1:xx+w+1].any())
        spot = next(spots, None)
        if spot is None: raise ValueError('Cannot fit town objects: '+row['displayName'])
        xx,yy=spot
        for o in group: o['localSurfaceX']+=xx-x; o['localSurfaceY']+=yy-y
        occupied[yy-1:yy+h+1,xx-1:xx+w+1] = True
    bp['widthCells']=bp['heightCells']=n
    bp['fineTerrainSources']=[]
    bp['objectPlacements']=objects
    # Stone wall segments retain physical individual gate openings; no wall crosses a road.
    gap=6
    for edge in range(4):
        for start,end in ((0,n//2-gap),(n//2+gap,n)):
            for at in range(start,end,50):
                length=min(50,end-at)
                x,y,w,h = ((at,0,length,1),(at,n-1,length,1),(0,at,1,length),(n-1,at,1,length))[edge]
                bp['objectPlacements'].append(obj(row['oldSiteId']+f':final_wall:{edge}:{at}','wall',x,y,w,h,
                    row['displayName']+'城墙', buildingId='base:wall_segment_stone',structureDurability='300'))
    return (n//2,n//2)

def bed_spawn_point(bp, bed):
    """A standing 1x1 footprint beside the assigned bed, never inside its collider."""
    parent=bed.get('metadata',{}).get('parentRoomId')
    room=next((o for o in bp['objectPlacements'] if o['placementId']==parent and o['kindId']=='room'),None)
    if room is None:raise ValueError('Assigned bed has no authored room: '+bed['placementId'])
    x,y,w,h=(bed[k] for k in ('localSurfaceX','localSurfaceY','widthCells','heightCells'))
    inset = float(room.get('metadata',{}).get('physicalScale','1'))
    candidates=((x+w/2,y-.5),(x+w+.5,y+h/2),(x-.5,y+h/2),(x+w/2,y+h+.5))
    for cx,cy in candidates:
        cx,cy=math.floor(cx)+.5,math.floor(cy)+.5
        if not (cx-.5>=room['localSurfaceX']+inset and cy-.5>=room['localSurfaceY']+inset and
                cx+.5<=room['localSurfaceX']+room['widthCells']-inset and cy+.5<=room['localSurfaceY']+room['heightCells']-inset):continue
        blocked=False
        for o in bp['objectPlacements']:
            if o is room:continue
            if o['kindId'] not in ('bed','room','wall','controlCore','storageRoom') and o.get('metadata',{}).get('blocksMovement')!='true':continue
            ox,oy,ow,oh=(o[k] for k in ('localSurfaceX','localSurfaceY','widthCells','heightCells'))
            if cx-.5<ox+ow and ox<cx+.5 and cy-.5<oy+oh and oy<cy+.5:
                blocked=True;break
        if not blocked:return cx,cy
    raise ValueError('Assigned bed has no clear standing spawn cell: '+bed['placementId'])

regions=copy.deepcopy(geo['regions'])
for region in regions:
    region['gates']=[r['displayName'] for r in plan if r['entryRegion']==region['id']]
regions.append(geo['openingBasin'])
by_name={r['displayName']:r for r in plan}
blueprints={}
for row in plan:
    sid,name=row['oldSiteId'],row['displayName']; cx,cy=center(row)
    if sid in old_placements:
        placed=old_placements[sid]
        path=(WORLD.parent/placed['sourcePath']).resolve()
        bp=read(path)
        previous=old_sites[sid]
        old_core=next(o for o in bp['objectPlacements'] if o['kindId']=='controlCore')
        old_center=(old_core['localSurfaceX']+old_core['widthCells']/2,old_core['localSurfaceY']+old_core['heightCells']/2)
        bp=replace_strings(bp,{previous['displayName']:name})
    else:
        bp=new_blueprint(row)
        fit_town(bp,row) # New cities need physical walls; existing towns are never refitted.
        path=SOURCE/'Blueprints/Settlements'/('final_'+sid.split(':')[-1]+'.worldsiteblueprint.json')
        placed=dict(placementId='final_'+sid.split(':')[-1],blueprintId=sid,
                    sourcePath='../Blueprints/Settlements/'+path.name,surfaceCellX=0,surfaceCellY=0,rotationQuarterTurns=0)
        world['worldSiteBlueprintPlacements'].append(placed)
        previous=dict(siteId=sid,surfaceId=world['runtimeSurface']['surfaceId'],siteType='Town',
                      surfaceCellX=cx,surfaceCellY=cy,controlEstablishedOrder=200+len(layout['siteRegions']))
        layout['siteRegions'].append(copy.deepcopy(previous))
        old_center=(size(row)//2,size(row)//2)
    if sid=='base:site_huangcun':
        local_center=old_center
        # Extend the linked blueprint to encompass the traced basin and both gates.
        border=[point(p) for p in geo['openingBasin']['points']]+[point(geo['openingBasin']['outerGate'])]
        origin=(math.floor(min(p[0] for p in border))-40,math.floor(min(p[1] for p in border))-40)
        local_center=(cx-origin[0],cy-origin[1])
        shift=(local_center[0]-old_center[0],local_center[1]-old_center[1])
        for o in bp['objectPlacements']:
            snap = 2 if o.get('metadata',{}).get('physicalScale') == '1.5' else 1
            o['localSurfaceX']=round((o['localSurfaceX']+shift[0])*snap)/snap;o['localSurfaceY']=round((o['localSurfaceY']+shift[1])*snap)/snap
            if o['kindId']=='spiritGate':
                key='outerGate' if o['placementId'].endswith('_outer') else 'innerGate'
                gx,gy=point(geo['openingBasin'][key]);o['localSurfaceX']=round(gx-origin[0]-2);o['localSurfaceY']=round(gy-origin[1]-2)
        bp['fineTerrainSources']=[]
        bp['widthCells']=max(math.ceil(max(p[0] for p in border)-origin[0])+40,math.ceil(max(o['localSurfaceX']+o['widthCells'] for o in bp['objectPlacements'])))
        bp['heightCells']=max(math.ceil(max(p[1] for p in border)-origin[1])+40,math.ceil(max(o['localSurfaceY']+o['heightCells'] for o in bp['objectPlacements'])))
        if basin_size is not None:
            for o in bp['objectPlacements']:
                if o['kindId']=='spiritGate':continue # The return gate intentionally lies outside.
                for dx in (0,o['widthCells']):
                    for dy in (0,o['heightCells']):
                        rx=o['localSurfaceX']+dx-local_center[0]
                        ry=o['localSurfaceY']+dy-local_center[1]
                        if math.hypot(rx,ry)>basin_size/2-8:
                            raise ValueError('Enclave object outside resized basin: '+o['placementId'])
    else:
        # Compaction may translate a town, never refit its existing buildings/walls.
        if bp['widthCells'] != size(row) or bp['heightCells'] != size(row):
            raise ValueError('Town size differs from approved layout: '+name)
        local_center=old_center
    placed['surfaceCellX']=round(cx-local_center[0]);placed['surfaceCellY']=round(cy-local_center[1])
    bp['displayName']=name
    write(path,bp);blueprints[sid]=(bp,placed)
    delta=(cx-previous['surfaceCellX'],cy-previous['surfaceCellY'])
    for key in ('siteRegions','sitePlaces','openingEntityAnchors','sitePlacements'):
        for item in layout[key]:
            if item.get('siteId') != sid: continue
            item['surfaceCellX']+=delta[0];item['surfaceCellY']+=delta[1]
            for labelkey in ('name','displayName','label'):
                if labelkey in item and 'displayName' in previous:
                    item[labelkey]=item[labelkey].replace(previous['displayName'],name)
            if key=='siteRegions':
                item.update(displayName=name,coreLevel=row['level'],surfaceCellX=cx,surfaceCellY=cy)
                if row['ownerFactionId']:item['ownerFactionId']=row['ownerFactionId']
                else:item.pop('ownerFactionId',None)
    # Keep real resident spawn anchors with their assigned beds after edge-building relocation.
    bed_spawns=set()
    for o in bp['objectPlacements']:
        spawn=o.get('metadata',{}).get('initialAssigneeSpawnKey')
        if spawn:
            bed_spawns.add(spawn)
            spawn_x,spawn_y=bed_spawn_point(bp,o)
            for anchor in layout['openingEntityAnchors']:
                if anchor['siteId']==sid and anchor['spawnKey']==spawn:
                    anchor['surfaceCellX']=placed['surfaceCellX']+spawn_x
                    anchor['surfaceCellY']=placed['surfaceCellY']+spawn_y
    # Source place anchors follow the same physically moved bound object groups.
    for place in (p for p in layout['sitePlaces'] if p['siteId']==sid):
        group=[o for o in bp['objectPlacements'] if o.get('metadata',{}).get('boundLocationId')==place['locationId']]
        if group:
            old_place=(place['surfaceCellX'],place['surfaceCellY'])
            place['surfaceCellX']=placed['surfaceCellX']+(min(o['localSurfaceX'] for o in group)+max(o['localSurfaceX']+o['widthCells'] for o in group))/2
            place['surfaceCellY']=placed['surfaceCellY']+(min(o['localSurfaceY'] for o in group)+max(o['localSurfaceY']+o['heightCells'] for o in group))/2
            for anchor in layout['openingEntityAnchors']:
                if anchor['siteId']!=sid or anchor['sourceLocationId']!=place['locationId'] or anchor['spawnKey'] in bed_spawns:continue
                # Unassigned shop staff start inside the relocated physical service hall.
                room=next((o for o in group if o['kindId']=='room'),None)
                if room and anchor['spawnKey'].endswith(('_shop','_auction')):
                    anchor['surfaceCellX']=placed['surfaceCellX']+room['localSurfaceX']+room['widthCells']/2
                    anchor['surfaceCellY']=placed['surfaceCellY']+room['localSurfaceY']+room['heightCells']/2
                else:
                    anchor['surfaceCellX']+=place['surfaceCellX']-old_place[0]
                    anchor['surfaceCellY']+=place['surfaceCellY']-old_place[1]

# Every authored fixed core needs one real WorkArea binding, including the four added towns.
areas_path=PACKAGE/'Data/WorkAreas/work_areas.json'
areas=read(areas_path)
areas['definitions']=[a for a in areas['definitions'] if a.get('locationId')!='base:loc_sect_zhuxing_node_4_core']
template=next(a for a in areas['definitions'] if a.get('isControlCore'))
for row in plan:
    bp,_=blueprints[row['oldSiteId']]
    core=next(o for o in bp['objectPlacements'] if o['kindId']=='controlCore')
    location=core['metadata']['boundLocationId']
    matches=[a for a in areas['definitions'] if a.get('isControlCore') and a.get('locationId')==location]
    if not matches:
        area=copy.deepcopy(template)
        area.update(id='base:workarea_core_'+row['oldSiteId'].split(':')[-1],locationId=location)
        areas['definitions'].append(area);matches=[area]
    if len(matches)!=1:raise ValueError('Duplicate core WorkArea: '+location)
    matches[0]['name']=row['displayName']+'·议政厅'
write(areas_path,areas)

# Runtime source placements are regenerated from linked blueprints by the publisher.
layout['sitePlacements']=[]
layout['openingBasin']=dict(boundary=[v for p in geo['openingBasin']['points'] for v in point(p)],
    innerGateId='base:site_huangcun:enclave_spirit_gate',outerGateId='base:site_huangcun:enclave_spirit_gate_outer')
world['surfaceWidthCells'],world['surfaceHeightCells']=W,H
world['worldMapRegions']=[dict(id=r['id'],label=r.get('label',''),colorRgb=(r['color'][0]<<16)+(r['color'][1]<<8)+r['color'][2],
    points=[dict(x=x,y=y) for x,y in region_polygon(r)],
    labelPoint=dict(zip(('x','y'),region_point(r,r.get('labelPoint',[0,0]))))) for r in regions]
world['worldMapRegions'].extend([
    dict(id='southern_peaks',label='南麓峰行',colorRgb=0xc1cfa0,points=[dict(x=0,y=0)]*3,labelPoint=dict(zip(('x','y'),point([820,580])))),
    dict(id='star_rift',label='镇星裂谷',colorRgb=0xc1cfa0,points=[dict(x=0,y=0)]*3,labelPoint=dict(zip(('x','y'),point([1000,468]))))])

# Macro terrain is authored at 10 cells, exactly as WorldComposer expects.
gw,gh=W//10,H//10
terrain=np.zeros((gh,gw),dtype=np.uint8)
features=np.zeros((gh,gw),dtype=np.uint8)

def segment(a,b,width,value=1):
    ax,ay=a;bx,by=b;radius=width/2
    x0=max(0,int((min(ax,bx)-radius)//10));x1=min(gw,int((max(ax,bx)+radius)//10)+1)
    y0=max(0,int((min(ay,by)-radius)//10));y1=min(gh,int((max(ay,by)+radius)//10)+1)
    xx=(np.arange(x0,x1)[None,:]+.5)*10;yy=(np.arange(y0,y1)[:,None]+.5)*10
    length=(bx-ax)**2+(by-ay)**2
    t=np.clip(((xx-ax)*(bx-ax)+(yy-ay)*(by-ay))/max(1e-9,length),0,1)
    mask=(xx-ax-t*(bx-ax))**2+(yy-ay-t*(by-ay))**2 <= radius**2
    terrain[y0:y1,x0:x1][mask]=value

def polygon_mask(points):
    xx=(np.arange(gw)[None,:]+.5)*10;yy=(np.arange(gh)[:,None]+.5)*10
    mask=np.zeros((gh,gw),dtype=bool)
    for a,b in zip(points,points[1:]+points[:1]):
        if a[1]==b[1]:continue
        mask^=((a[1]>yy)!=(b[1]>yy)) & (xx < (b[0]-a[0])*(yy-a[1])/(b[1]-a[1])+a[0])
    return mask

def nearest_on_boundary(p,poly):
    candidates=[]
    for a,b in zip(poly,poly[1:]+poly[:1]):
        d=(b[0]-a[0],b[1]-a[1]);t=max(0,min(1,((p[0]-a[0])*d[0]+(p[1]-a[1])*d[1])/max(1e-9,d[0]**2+d[1]**2)))
        q=(a[0]+t*d[0],a[1]+t*d[1]);candidates.append(q)
    return min(candidates,key=lambda q:(q[0]-p[0])**2+(q[1]-p[1])**2)

for r in regions:
    poly=region_polygon(r)
    if 'forest' in r:features[polygon_mask(poly)]=round(r['forest']*100)
    if r.get('wall'):
        if 'mountainOuter' in r:
            inner=polygon_mask(poly)
            terrain[polygon_mask(list(map(point,r['mountainOuter']))) & ~inner]=1
        else:
            for a,b in zip(poly,poly[1:]+poly[:1]):segment(a,b,70)
for ridge in geo['ridges']:
    poly=list(map(point,ridge))
    for a,b in zip(poly,poly[1:]):segment(a,b,35)

gate_notes=[]
uncut_terrain=terrain.copy()
gate_seams=[]
world['roads']=[]
for r in regions:
    poly=region_polygon(r)
    if r['id'] in compression_config.get('closedBasins',[]):continue
    gates=[(name,center(by_name[name]),size(by_name[name])) for name in r.get('gates',[])]
    gates += [(r['id']+':trail:'+str(i),point(p),0) for i,p in enumerate(r.get('trailGates',[]))]
    for name,p,n in gates:
        q=nearest_on_boundary(p,poly)
        # Match each city's cardinal street with the nearest cliff face, rather than an unrelated gap.
        def approach_clearance(vertical):
            ends=[(p[0],p[1]+sign*(n/2+1)) if vertical else (p[0]+sign*(n/2+1),p[1]) for sign in (-1,1)]
            return min(math.dist(e,nearest_on_boundary(e,poly)) for e in ends)
        vertical=max((False,True),key=approach_clearance)
        if n and 'gateAxis' in by_name[name]:vertical=by_name[name]['gateAxis']=='vertical'
        reach=max(n/2+100,math.dist(p,q)+110)
        a=(p[0],p[1]-reach) if vertical else (p[0]-reach,p[1])
        b=(p[0],p[1]+reach) if vertical else (p[0]+reach,p[1])
        # A city's footprint is the only opening. Never cut a parallel/diagonal
        # route outside its walls. Interior wilderness trails retain their rule.
        if not n:segment(a,b,60,0)
        else:gate_seams.append((by_name[name],p,n,vertical))
        world['roads'].append(dict(pathId='base:final_gate:'+name,roadClass='Road',controlPoints=[dict(x=max(0,min(W,a[0])),y=max(0,min(H,a[1]))),dict(x=max(0,min(W,b[0])),y=max(0,min(H,b[1])))]))
        gate_notes.append(dict(region=r['id'],gate=name,cellX=round(p[0],2),cellY=round(p[1],2)))

# Clear only actual town footprints, not five-level reserve boxes. No other cliff gaps are introduced.
for row in plan:
    # Keep the cliff touching the gate city's outer wall; a grass margin would let walkers bypass it.
    cx,cy=center(row);half=size(row)/2+(0 if row['entryRegion'] else 8)
    terrain[max(0,int((cy-half)//10)):min(gh,math.ceil((cy+half)/10)),max(0,int((cx-half)//10)):min(gw,math.ceil((cx+half)/10))]=0
    features[max(0,int((cy-half)//10)):min(gh,math.ceil((cy+half)/10)),max(0,int((cx-half)//10)):min(gw,math.ceil((cx+half)/10))]=0
world['coarseTerrainSources']=[dict(editorCellX=int(x),editorCellY=int(y),baseTerrain='Mountain') for y,x in zip(*np.nonzero(terrain))]
world['coarseFeatureSources']=[dict(editorCellX=int(x),editorCellY=int(y),feature='Forest',density=float(features[y,x])/100) for y,x in zip(*np.nonzero(features))]
world['detailPatchPlacements']=[]
# Pin the 12-cell band on both sides of each city wall to exact terrain.
# Coarse clearing rounds outwards and the compositor adds two cells of noise;
# neither may create a walkable sliver between a mountain and a city wall.
for row,(cx,cy),n,vertical in gate_seams:
    bp,placed=blueprints[row['oldSiteId']]
    left,bottom=placed['surfaceCellX'],placed['surfaceCellY']
    right,top=left+n,bottom+n
    margin=12;patch_margin=180;ox,oy=left-patch_margin,bottom-patch_margin;fine={}
    for y in range(bottom-margin,top+margin):
        for x in range(left-margin,right+margin):
            if left+margin<=x<right-margin and bottom+margin<=y<top-margin:continue
            ex,ey=x//10,y//10
            if not uncut_terrain[max(0,ey-1):min(gh,ey+2),max(0,ex-1):min(gw,ex+2)].any():continue
            inside=left<=x<right and bottom<=y<top
            fine[(x,y)]=dict(surfaceCellX=x-ox,surfaceCellY=y-oy,
                baseTerrainOverride='Plain' if inside or not uncut_terrain[ey,ex] else 'Mountain',
                featureOverride='None',featureDensity=0)
    # At a convex corner an axial door can meet the mountain. Its short approach
    # has solid flanks attached to the city wall, not a free-standing cut in the ridge.
    for sign in (-1,1):
        def approach_cell(distance,side):
            along=(top+distance if sign>0 else bottom-1-distance) if vertical else (right+distance if sign>0 else left-1-distance)
            return (cx+side,along) if vertical else (along,cy+side)
        blocked=[d for d in range(patch_margin-12) if any(uncut_terrain[y//10,x//10]
            for x,y in (approach_cell(d,s) for s in range(-6,6)))]
        if not blocked:continue
        length=max(blocked)+12
        if length>=patch_margin-1:raise ValueError('Gate corner approach exceeds local reserve: '+row['displayName'])
        if length>reserve/2-n/2:raise ValueError('Gate approach leaves five-level reserve: '+row['displayName'])
        for distance in range(length):
            for side in range(-18,18):
                x,y=approach_cell(distance,side)
                fine[(x,y)]=dict(surfaceCellX=x-ox,surfaceCellY=y-oy,
                    baseTerrainOverride='Plain' if -6<=side<6 else 'Mountain',featureOverride='None',featureDensity=0)
    key=row['oldSiteId'].replace(':','_')+'_seam'
    path=SOURCE/'DetailPatches'/(key+'.detailpatch.json')
    path.parent.mkdir(parents=True,exist_ok=True)
    write(path,dict(schemaVersion=3,patchId=key,displayName=row['displayName']+'山墙接缝',
        widthCells=n+2*patch_margin,heightCells=n+2*patch_margin,fineTerrainOverrides=list(fine.values())))
    world['detailPatchPlacements'].append(dict(placementId=key,patchId=key,
        sourcePath='../DetailPatches/'+path.name,surfaceCellX=ox,surfaceCellY=oy))
world['rivers']=[dict(pathId='base:river_'+r['id'],widthCells=r['widthCells'],controlPoints=[dict(x=x,y=y) for x,y in map(point,r['points'])]) for r in geo['rivers']]
world['crossings']=[];world['worldObjectPlacements']=[]
for name,p in zip(('north','south'),geo['bridges']):
    x,y=point(p);road='base:road_west_river_bridge_'+name;river=world['rivers'][0]['pathId']
    world['roads'].append(dict(pathId=road,roadClass='Road',controlPoints=[dict(x=x-110,y=y),dict(x=x+110,y=y)]))
    world['crossings'].append(dict(riverPathId=river,roadPathId=road,x=x,y=y,resolution='Bridge'))
    world['worldObjectPlacements'].append(dict(placementId='base:bridge_west_river_'+name,kindId='bridge',worldSurfaceX=round(x-90),worldSurfaceY=round(y-12),widthCells=180,heightCells=24,rotationQuarterTurns=0,metadata=dict(riverPathId=river,roadPathId=road)))

# West gate approach crosses the existing river, not the enclosing mountain line.
west=next((r for r in plan if r['oldSiteId']=='base:site_beast_west_gate'),None)
if west:
    cx,cy=center(west)
    river=world['rivers'][0]
    line=[(p['x'],p['y']) for p in river['controlPoints']]
    hits=[a[0]+(cy-a[1])*(b[0]-a[0])/(b[1]-a[1]) for a,b in zip(line,line[1:])
          if min(a[1],b[1])<=cy<max(a[1],b[1])]
    if len(hits)!=1:raise ValueError('West gate approach must cross the river exactly once')
    x=hits[0];road='base:road_beast_west_approach'
    if cx-x < reserve/2+river['widthCells']/2+50:raise ValueError('West gate upgrade reserve touches river')
    world['roads'].append(dict(pathId=road,roadClass='Road',controlPoints=[dict(x=x-150,y=cy),dict(x=cx,y=cy)]))
    world['crossings'].append(dict(riverPathId=river['pathId'],roadPathId=road,x=x,y=cy,resolution='Bridge'))
    world['worldObjectPlacements'].append(dict(placementId='base:bridge_beast_west_approach',kindId='bridge',
        worldSurfaceX=round(x-100),worldSurfaceY=cy-12,widthCells=200,heightCells=24,rotationQuarterTurns=0,
        metadata=dict(riverPathId=river['pathId'],roadPathId=road)))

# Remote deployment offsets use two mapped endpoints, not unchanged old distances.
runpy.run_path(str(Path(__file__).with_name('river-network.py')))['apply'](world, SOURCE, plan, center, write)
publish_squad_deployments()
# Other absolute initial positions retain their existing site-relative migration.
for path in (PACKAGE/'Data/Armies').glob('*.json'):
    if 'acceptance' in path.name: continue
    data=read(path);changed=False
    for d in data.get('definitions',[]):
        sid=d.get('assemblySiteId',d.get('defendedSiteId'))
        if sid in old_sites and sid in active_ids and 'initialSurfacePosition' in d:
            row=next(r for r in plan if r['oldSiteId']==sid);cx,cy=center(row);previous=old_sites[sid]
            d['initialSurfacePosition']['worldX']+=(cx-previous['surfaceCellX'])*world['runtimeSurface']['surfaceCellWorldSize']
            d['initialSurfacePosition']['worldY']+=(cy-previous['surfaceCellY'])*world['runtimeSurface']['surfaceCellWorldSize'];changed=True
    if changed:write(path,data)
write(WORLD,world)
manifest=read(PACKAGE/'manifest.json');manifest['version']=compression_config['contentVersion'];write(PACKAGE/'manifest.json',manifest)
audit_path=ROOT/'docs/40-process/379-final-world-site-disposition-audit.tsv'
with audit_path.open(encoding='utf-8-sig',newline='') as f:
    old_audit=list(csv.DictReader(f,delimiter='\t'))
rows={r['oldSiteId']:r for r in old_audit}
for sid in retired_ids:
    if sid in rows:rows[sid].update(disposition='retire',newName='',ownerFactionId='',startLevel='',newCellX='',newCellY='',sitePlaces='0',openingAnchors='0')
for p in plan:
    sid=p['oldSiteId'];cx,cy=center(p)
    row=rows.setdefault(sid,dict(oldSiteId=sid,oldName='',disposition='new'))
    places={x['locationId'] for x in layout['sitePlaces'] if x['siteId']==sid}
    places.update(o['metadata']['boundLocationId'] for o in blueprints[sid][0]['objectPlacements'] if o.get('metadata',{}).get('boundLocationId'))
    row.update(newName=p['displayName'],ownerFactionId=p['ownerFactionId'],startLevel=p['level'],newCellX=cx,newCellY=cy,
        sitePlaces=len(places),openingAnchors=sum(x['siteId']==sid for x in layout['openingEntityAnchors']))
with audit_path.open('w',encoding='utf-8',newline='') as f:
    out=csv.DictWriter(f,fieldnames=list(old_audit[0]),delimiter='\t',lineterminator='\n');out.writeheader();out.writerows(rows.values())
write(ROOT/'local/final-world-geography/final-world-gates.json',gate_notes)
print(f'Authored {len(plan)} sites, {len(world["coarseTerrainSources"])} mountain cells, {W}x{H}; publication required.',flush=True)
