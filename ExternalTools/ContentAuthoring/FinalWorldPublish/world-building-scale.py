"""Author the approved 2x world buildings/furniture and 12x8 / 15x12 fields.

Keeps town centres, political ranges, final perimeter, residents and asset IDs. Repacking
uses real rectangles and the published terrain raster, not a game simulation.
Run before FinalWorldPublish; current source scale markers prevent compounding.
"""
import copy
import json
import math
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PACKAGE = ROOT/'Content/BaseGame'
WORLD = PACKAGE/'Authoring/ContinuousSurface/Worlds/main_world.worldcomposition.json'
VERSION = '0.0.43-world-scale-and-crop-cycles'
KINDS = {'room', 'bed', 'storageRoom', 'recoverySpot', 'prisonDropOff', 'cushion',
         'chessBoard', 'alchemyFurnace', 'smithingFurnace', 'controlCore'}
NATURAL = {'treeS', 'treeM', 'treeL', 'ore'}
FIXED = {'wall', 'wallSegment', 'rock', 'cave', 'waystone', 'spring', 'spiritGate'}


class Grid:
    def __init__(self,w,h,filled=False):
        self.width,self.height=w,h
        self.rows=[(1<<w)-1 if filled else 0 for _ in range(h)]

    def any(self,x,y,w,h):
        if x<0 or y<0 or x+w>self.width or y+h>self.height:return True
        mask=((1<<w)-1)<<x
        return any(self.rows[row]&mask for row in range(y,y+h))


def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))


def write(p, x):
    p.write_text(json.dumps(x, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


def rect(o):
    return tuple(o[k] for k in ('localSurfaceX', 'localSurfaceY', 'widthCells', 'heightCells'))


def contains(r, o):
    x,y,w,h=rect(r); a,b,c,d=rect(o)
    return a>=x+1-1e-7 and b>=y+1-1e-7 and a+c<=x+w-1+1e-7 and b+d<=y+h-1+1e-7


def collides(a,b):
    return a[0]<b[0]+b[2]-1e-7 and b[0]<a[0]+a[2]-1e-7 and a[1]<b[1]+b[3]-1e-7 and b[1]<a[1]+a[3]-1e-7


def mark(grid, r, value=True):
    x,y,w,h=r
    x0=max(0,math.floor(x+1e-6)); y0=max(0,math.floor(y+1e-6))
    x1=min(grid.width,math.ceil(x+w-1e-6)); y1=min(grid.height,math.ceil(y+h-1e-6))
    if x1>x0 and y1>y0:
        mask=((1<<(x1-x0))-1)<<x0
        for row in range(y0,y1):
            if value:grid.rows[row]|=mask
            else:grid.rows[row]&=~mask


def scale(o):
    old=float(o.get('metadata',{}).get('physicalScale','1'))
    if o['kindId'] in KINDS:
        w=round(o['widthCells']/old); h=round(o['heightCells']/old)
        o['widthCells'],o['heightCells']=w*2,h*2
        o.setdefault('metadata',{}).update(physicalScale='2',sourceCellsW=str(w),sourceCellsH=str(h),legacyGeometryState='edited')
    elif o['kindId'] in ('grainField','herbField'):
        w,h=(12,8) if o['kindId']=='grainField' else (15,12)
        o['widthCells'],o['heightCells']=w,h
        o.setdefault('metadata',{}).update(sourceCellsW=str(w),sourceCellsH=str(h),legacyGeometryState='edited')


def group_objects(old, current):
    rooms=[o for o in old['objectPlacements'] if o['kindId']=='room']
    groups={r['placementId']:[r['placementId']] for r in rooms}
    owned=set(groups)
    for o in old['objectPlacements']:
        if o['kindId']=='room': continue
        parent=o.get('metadata',{}).get('parentRoomId')
        if parent not in groups and o['kindId'] in KINDS:
            parent=next((r['placementId'] for r in rooms if contains(r,o)),None)
        if parent in groups:
            groups[parent].append(o['placementId']); owned.add(o['placementId'])
    by={o['placementId']:o for o in current['objectPlacements']}
    before={o['placementId']:o for o in old['objectPlacements']}
    for r in rooms:
        old_scale=float(r.get('metadata',{}).get('physicalScale','1'))
        for child_id in groups[r['placementId']][1:]:
            child=by[child_id]; orig=before[child_id]
            child['localSurfaceX']=round((orig['localSurfaceX']-r['localSurfaceX'])/old_scale*2)
            child['localSurfaceY']=round((orig['localSurfaceY']-r['localSurfaceY'])/old_scale*2)
        by[r['placementId']]['localSurfaceX']=by[r['placementId']]['localSurfaceY']=0
    return groups, owned


def move_group(by, group, x, y):
    for i in group:
        o=by[i]; o['localSurfaceX']+=x; o['localSurfaceY']+=y
        o.setdefault('metadata',{})['legacyGeometryState']='edited'


def footprint(group, by):
    x=min(by[i]['localSurfaceX'] for i in group); y=min(by[i]['localSurfaceY'] for i in group)
    return x,y,max(by[i]['localSurfaceX']+by[i]['widthCells'] for i in group)-x,max(by[i]['localSurfaceY']+by[i]['heightCells'] for i in group)-y


def free_origin(grid,w,h,preferred):
    w,h=math.ceil(w),math.ceil(h)
    if w>grid.width or h>grid.height:return None
    limit=(1<<(grid.width-w+1))-1
    preferred_x=max(0,min(grid.width-w,round(preferred[0])))
    best=None;best_distance=float('inf')
    for y in sorted(range(grid.height-h+1),key=lambda yy:(abs(yy-preferred[1]),yy)):
        dy=(y-preferred[1])**2
        if dy>best_distance:break
        blocked=0
        for row in range(y,y+h):blocked|=grid.rows[row]
        span=1
        while span<w:
            shift=min(span,w-span);blocked|=blocked>>shift;span+=shift
        valid=(~blocked)&limit
        if not valid:continue
        left=valid&((1<<(preferred_x+1))-1);right=valid>>preferred_x
        xs=[]
        if left:xs.append(left.bit_length()-1)
        if right:xs.append(preferred_x+(right&-right).bit_length()-1)
        for x in xs:
            distance=dy+(x-preferred[0])**2
            if distance<best_distance or distance==best_distance and (best is None or (y,x)<(best[1],best[0])):
                best,best_distance=(x,y),distance
    return best


def terrain_grid(geo,placed,region):
    x0,y0,w,h=region; grid=Grid(w,h,filled=True)
    gx0=round(placed['surfaceCellX']+x0); gy0=round(placed['surfaceCellY']+y0)
    for row in range(h):
        gy=gy0+row
        if not 0<=gy<len(geo['rows']):continue
        pos=0
        for run in geo['rows'][gy].split(';'):
            if not run:continue
            n=int(run[1:],16); end=pos+n
            if end>gx0 and pos<gx0+w:
                a=max(0,pos-gx0); b=min(w,end-gx0)
                mark(grid,(a,row,b-a,1),run[0] in ('#','~'))
            pos=end
            if pos>=gx0+w:break
    return grid


def repack(bp,old,placed,geo):
    by={o['placementId']:o for o in bp['objectPlacements']}
    groups,owned=group_objects(old,bp)
    before={o['placementId']:o for o in old['objectPlacements']}
    huangcun=bp['blueprintId']=='base:site_huangcun'
    x0,y0,n,m=(430,430,180,180) if huangcun else (0,0,bp['widthCells'],bp['heightCells'])
    grid=terrain_grid(geo,placed,(x0,y0,n,m))
    for r in [(0,0,n,4),(0,m-4,n,4),(0,0,4,m),(n-4,0,4,m)]:mark(grid,r)
    cx,cy=n/2,m/2
    lane=24 if huangcun else 8
    mark(grid,(cx-lane/2,0,lane,m));mark(grid,(0,cy-lane/2,n,lane))
    for o in bp['objectPlacements']:
        if o['kindId'] in FIXED:
            r=rect(o);mark(grid,(r[0]-x0,r[1]-y0,r[2],r[3]))
    core=next(o for o in bp['objectPlacements'] if o['kindId']=='controlCore')
    original=before[core['placementId']]
    core['localSurfaceX']=round(original['localSurfaceX']+original['widthCells']/2-core['widthCells']/2)
    core['localSurfaceY']=round(original['localSurfaceY']+original['heightCells']/2-core['heightCells']/2)
    mark(grid,(core['localSurfaceX']-x0-3,core['localSurfaceY']-y0-3,core['widthCells']+6,core['heightCells']+6))
    singles=[[o['placementId']] for o in bp['objectPlacements'] if o['placementId'] not in owned and
             o['kindId'] in (KINDS|{'grainField','herbField'})-{'controlCore'}]
    for g in singles:
        by[g[0]]['localSurfaceX']=by[g[0]]['localSurfaceY']=0
    all_groups=list(groups.values())+singles
    if huangcun:
        homes=[r for r in old['objectPlacements'] if r['kindId']=='room' and ':room_res_' in r['placementId'] and
               not r['placementId'].endswith(('room_res_10','room_res_11'))]
        targets={r['placementId']:(434+i%3*12,550+i//3*15) for i,r in enumerate(homes)}
        for name,xy in {'room_supervisor':(534,480),'room_res_10':(534,448),'room_res_11':(544,448),
                        'practice_house':(556,480),'manual_hall':(556,448),'combat_hall':(580,448),
                        'room_prison':(580,480),'room_medical':(534,580),
                        'alchemy_room':(556,580),'smithy_room':(578,580)}.items():
            targets['base:site_huangcun:'+name]=xy
        for kind,points in [('grainField',[(x,y) for y in (566,576,586,596) for x in (470,485)]),
                            ('herbField',[(434,534),(470,534),(488,534),(470,550),(488,550)]),
                            ('chessBoard',[(536+i*10,552) for i in range(5)]),
                            ('storageRoom',[(536,538)])]:
            for o,xy in zip([o for o in bp['objectPlacements'] if o['kindId']==kind],points):targets[o['placementId']]=xy
        spring_pads=[o for o in bp['objectPlacements'] if o['kindId']=='cushion' and ':place_cushion_' in o['placementId']]
        for o,xy in zip(spring_pads,[(582,554),(586,554),(582,558),(586,558)]):targets[o['placementId']]=xy
        for group in all_groups:
            key=group[0]
            if key not in targets:raise ValueError('Unplanned Huangcun facility: '+key)
            x,y=targets[key]; move_group(by,group,x,y)
            r=footprint(group,by);pad=4 if by[key]['kindId']=='room' else 1
            # Terrain geometry is checked below; corridors may contain the planned nonblocking furniture.
            mark(grid,(r[0]-x0-2,r[1]-y0-pad,r[2]+4,r[3]+pad+2))
    else:
        def size_key(g):
            r=footprint(g,by);return (-max(r[2],r[3]),-r[2]*r[3],g[0])
        all_groups.sort(key=size_key)
        for group in all_groups:
            key=group[0];r=footprint(group,by);isroom=by[key]['kindId']=='room'
            side=0;front=2;back=0
            preferred=(0,0)
            point=free_origin(grid,r[2]+side*2,r[3]+front+back,preferred)
            if point is None:raise ValueError('Town cannot fit enlarged group: '+bp['displayName']+' '+key)
            x,y=point[0]+side+x0-r[0],point[1]+front+y0-r[1]
            move_group(by,group,x,y)
            mark(grid,(point[0],point[1],r[2]+side*2,r[3]+front+back))
    # Preserve every natural resource; move only those occupying a building, field or reserved lane.
    props=[o for o in bp['objectPlacements'] if o['kindId'] in NATURAL and
           x0<=o['localSurfaceX']<x0+n and y0<=o['localSurfaceY']<y0+m]
    props.sort(key=lambda o:(-o['widthCells']*o['heightCells'],o['placementId']))
    for o in props:
        x,y,w,h=rect(o);ix,iy=round(x-x0),round(y-y0)
        if ix<1 or iy<1 or ix+w>=n-1 or iy+h>=m-1 or grid.any(ix,iy,math.ceil(w),math.ceil(h)):
            point=free_origin(grid,w,h,(ix,iy))
            if point is None:raise ValueError('No free resource location in '+bp['displayName']+': '+o['placementId']+
                ' free='+str(n*m-sum(r.bit_count() for r in grid.rows))+' remaining='+str(len(props)-props.index(o)))
            ix,iy=point
            o['localSurfaceX'],o['localSurfaceY']=ix+x0,iy+y0
            o.setdefault('metadata',{})['legacyGeometryState']='edited'
        mark(grid,(ix,iy,w,h))
    # Minimum author geometry integrity, without running gameplay or a behavioural test suite.
    room_list=[o for o in bp['objectPlacements'] if o['kindId']=='room']
    for i,r in enumerate(room_list):
        if any(collides(rect(r),rect(other)) for other in room_list[i+1:]):raise ValueError('Room overlap: '+r['placementId'])
        for child_id in groups[r['placementId']][1:]:
            if not contains(r,by[child_id]):raise ValueError('Furniture escaped parent: '+child_id)
    for group in all_groups:
        r=footprint(group,by)
        if r[0]<x0+2 or r[1]<y0+2 or r[0]+r[2]>x0+n-2 or r[1]+r[3]>y0+m-2:
            raise ValueError('Facility outside town boundary: '+group[0])
    return groups


def safe_spawn(bp,room,preferred):
    scale=float(room.get('metadata',{}).get('physicalScale','1'))
    x,y,w,h=rect(room)
    blocked=[o for o in bp['objectPlacements'] if o['kindId'] in ('bed','storageRoom','alchemyFurnace','smithingFurnace')]
    candidates=[(xx+.5,yy+.5) for yy in range(math.ceil(y+scale),math.floor(y+h-scale))
                for xx in range(math.ceil(x+scale),math.floor(x+w-scale))]
    candidates.sort(key=lambda p:((p[0]-preferred[0])**2+(p[1]-preferred[1])**2,p[1],p[0]))
    for p in candidates:
        if not any(collides((p[0]-.5,p[1]-.5,1,1),rect(o)) for o in blocked):return p
    raise ValueError('No full standing cell inside '+room['placementId'])


def update_layout(world,bp,old,placed,groups):
    sid=bp['blueprintId'];px,py=placed['surfaceCellX'],placed['surfaceCellY'];layout=world['runtimeLayout']
    by={o['placementId']:o for o in bp['objectPlacements']};before={o['placementId']:o for o in old['objectPlacements']}
    for place in layout['sitePlaces']:
        if place['siteId']!=sid:continue
        bound=[o for o in bp['objectPlacements'] if o.get('metadata',{}).get('boundLocationId')==place['locationId']]
        if bound:
            place['surfaceCellX']=px+(min(o['localSurfaceX'] for o in bound)+max(o['localSurfaceX']+o['widthCells'] for o in bound))/2
            place['surfaceCellY']=py+(min(o['localSurfaceY'] for o in bound)+max(o['localSurfaceY']+o['heightCells'] for o in bound))/2
        elif 'store' in place['locationId']:
            store=next((o for o in bp['objectPlacements'] if o['kindId']=='storageRoom'),None)
            if store:place['surfaceCellX'],place['surfaceCellY']=px+store['localSurfaceX']+store['widthCells']/2,py+store['localSurfaceY']-1
    beds={o.get('metadata',{}).get('initialAssigneeSpawnKey'):o for o in bp['objectPlacements']
          if o['kindId']=='bed' and o.get('metadata',{}).get('initialAssigneeSpawnKey')}
    for anchor in layout['openingEntityAnchors']:
        if anchor['siteId']!=sid:continue
        bed=beds.get(anchor['spawnKey'])
        if bed:
            room=by[bed['metadata']['parentRoomId']];x,y,w,h=rect(bed)
            preferred=(x+w/2,y-.5)
        else:
            ax,ay=anchor['surfaceCellX']-px,anchor['surfaceCellY']-py
            original=next((o for o in old['objectPlacements'] if o['kindId']=='room' and
                           o['localSurfaceX']<=ax<=o['localSurfaceX']+o['widthCells'] and
                           o['localSurfaceY']<=ay<=o['localSurfaceY']+o['heightCells']),None)
            if original is None:
                room=next((o for o in bp['objectPlacements'] if o['kindId']=='room' and
                           o.get('metadata',{}).get('boundLocationId')==anchor.get('sourceLocationId')),None)
                if room is None:continue
                preferred=(room['localSurfaceX']+room['widthCells']/2,room['localSurfaceY']+room['heightCells']/2)
            else:
                room=by[original['placementId']];factor=2/float(original.get('metadata',{}).get('physicalScale','1'))
                preferred=(room['localSurfaceX']+(ax-original['localSurfaceX'])*factor,
                           room['localSurfaceY']+(ay-original['localSurfaceY'])*factor)
        sx,sy=safe_spawn(bp,room,preferred)
        anchor['surfaceCellX'],anchor['surfaceCellY']=px+sx,py+sy


def main():
    if read(PACKAGE/'manifest.json')['version']==VERSION or read(PACKAGE/'manifest.json')['version'].startswith('0.0.44-'):
        print('Already authored:',VERSION)
        return
    world=read(WORLD);geo=read(PACKAGE/'Data/Worlds/w2a_surface_geography_baked_v1.json')['definitions'][0]
    if any(o.get('metadata',{}).get('physicalScale')=='2'
           for p in world['worldSiteBlueprintPlacements']
           for o in read((WORLD.parent/p['sourcePath']).resolve())['objectPlacements']):
        print('Building doubling already authored; preserve subsequent manual layouts.');return
    staged=[];counts={'towns':0,'rooms':0,'beds':0,'grainField':0,'herbField':0}
    for placed in world['worldSiteBlueprintPlacements']:
        path=(WORLD.parent/placed['sourcePath']).resolve();bp=read(path);old=copy.deepcopy(bp)
        # The final perimeter is authoritative. Superseded scattered perimeter
        # segments inside towns must not partition the repacked neighbourhoods.
        if any(':final_wall:' in o['placementId'] for o in bp['objectPlacements']):
            bp['objectPlacements']=[o for o in bp['objectPlacements'] if
                o['kindId'] not in ('wall','wallSegment') or ':final_wall:' in o['placementId']]
            old=copy.deepcopy(bp)
        for o in bp['objectPlacements']:scale(o)
        groups=repack(bp,old,placed,geo);update_layout(world,bp,old,placed,groups)
        staged.append((path,bp));counts['towns']+=1
        for k in ('rooms','beds','grainField','herbField'):
            kind={'rooms':'room','beds':'bed'}.get(k,k);counts[k]+=sum(o['kindId']==kind for o in bp['objectPlacements'])
    buildings_path=PACKAGE/'Data/Buildings/buildings.json';buildings=read(buildings_path)
    for b in buildings['definitions']:
        if b['id']=='base:building_grain_field':b['footprintCellsW'],b['footprintCellsH']=12,8
        if b.get('outdoorKind') in ('room','bed'):
            s=b.get('description','')
            for a,z in [('24×36','32×48'),('12×18','16×24'),('6×9','8×12'),('1.5×3','2×4')]:s=s.replace(a,z)
            b['description']=s
    # Mutate files only after every town has a complete valid packing.
    for p,obj in staged+[(WORLD,world),(buildings_path,buildings)]:write(p,obj)
    for path in [PACKAGE/'manifest.json',WORLD.parent/'final_world_compaction_v1.json']:
        key='version' if path.name=='manifest.json' else 'contentVersion'
        path.write_text(path.read_text(encoding='utf-8-sig').replace(read(path)[key],VERSION),encoding='utf-8')
    print('Authored world 2x building scale:',counts,VERSION)


if __name__=='__main__':main()
