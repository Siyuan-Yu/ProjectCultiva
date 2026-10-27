"""Approved 15x12 grain fields and removal of the solitary herb field below Huangcun housing.
Edits authoring data only; keep other physical assets and IDs in place.
"""
import copy
import runpy
from pathlib import Path
h = runpy.run_path(str(Path(__file__).with_name('world-building-scale.py')))
read, write, rect, mark, free_origin, terrain_grid = (h[k] for k in ('read','write','rect','mark','free_origin','terrain_grid'))
PACKAGE, WORLD = h['PACKAGE'], h['WORLD']
VERSION = '0.0.47-patrol-routines-fields'

def main():
    world = read(WORLD)
    geo = read(PACKAGE/'Data/Worlds/w2a_surface_geography_baked_v1.json')['definitions'][0]
    staged = []
    count = 0
    removed = 0
    for placed in world['worldSiteBlueprintPlacements']:
        path = (WORLD.parent/placed['sourcePath']).resolve()
        bp = read(path)
        huangcun = bp['blueprintId'] == 'base:site_huangcun'
        if huangcun:
            before = len(bp['objectPlacements'])
            bp['objectPlacements'] = [o for o in bp['objectPlacements'] if o['placementId'] != 'base:site_huangcun:place_herbField_234905155']
            removed += before-len(bp['objectPlacements'])
        grains = [o for o in bp['objectPlacements'] if o['kindId']=='grainField']
        if not grains: continue
        x0,y0,w,hh = (430,430,180,180) if huangcun else (0,0,bp['widthCells'],bp['heightCells'])
        grid = terrain_grid(geo, placed, (x0,y0,w,hh))
        for r in [(0,0,w,3),(0,hh-3,w,3),(0,0,3,hh),(w-3,0,3,hh)]: mark(grid,r)
        if not huangcun:
            mark(grid,(w/2-4,0,8,hh)); mark(grid,(0,hh/2-4,w,8))
        for o in bp['objectPlacements']:
            if o['kindId']=='grainField': continue
            x,y,a,b=rect(o)
            pad=1 if o['kindId'] not in ['wall','wallSegment'] else 0
            mark(grid,(x-x0-pad,y-y0-pad,a+pad*2,b+pad*2))
        for i,o in enumerate(grains):
            preferred = (470+i%3*17,566+i//3*14) if huangcun else (o['localSurfaceX'],o['localSurfaceY'])
            point = free_origin(grid,17,14,(preferred[0]-x0-1,preferred[1]-y0-1))
            if point is None: raise ValueError('No space for grain field: '+o['placementId'])
            o.update(localSurfaceX=point[0]+x0+1,localSurfaceY=point[1]+y0+1,widthCells=15,heightCells=12)
            o.setdefault('metadata',{}).update(sourceCellsW='15',sourceCellsH='12',legacyGeometryState='edited')
            mark(grid,(point[0],point[1],17,14))
            count += 1
        for place in world['runtimeLayout']['sitePlaces']:
            if place['siteId'] != bp['blueprintId']: continue
            fields = [o for o in bp['objectPlacements'] if o['kindId'] in ['grainField','herbField'] and o.get('metadata',{}).get('boundLocationId')==place['locationId']]
            if fields:
                place['surfaceCellX']=placed['surfaceCellX']+(min(o['localSurfaceX'] for o in fields)+max(o['localSurfaceX']+o['widthCells'] for o in fields))/2
                place['surfaceCellY']=placed['surfaceCellY']+(min(o['localSurfaceY'] for o in fields)+max(o['localSurfaceY']+o['heightCells'] for o in fields))/2
        staged.append((path,bp))
    buildings=read(PACKAGE/'Data/Buildings/buildings.json')
    for b in buildings['definitions']:
        if b['id']=='base:building_grain_field': b.update(footprintCellsW=15,footprintCellsH=12)
    for p,d in staged+[(WORLD,world),(PACKAGE/'Data/Buildings/buildings.json',buildings)]: write(p,d)
    for p,key in [(PACKAGE/'manifest.json','version'),(WORLD.parent/'final_world_compaction_v1.json','contentVersion')]:
        d=read(p);d[key]=VERSION;write(p,d)
    print('Authored grain fields:',count,'15x12; removed Huangcun herb fields:',removed)
if __name__=='__main__': main()
