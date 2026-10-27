"""Author the approved doubled resources, retaining IDs and fixed town geometry."""
import copy
import importlib.util
import math
from pathlib import Path

spec=importlib.util.spec_from_file_location('layout',Path(__file__).with_name('world-building-scale.py'))
layout=importlib.util.module_from_spec(spec);spec.loader.exec_module(layout)
read,write,mark,free_origin=layout.read,layout.write,layout.mark,layout.free_origin
ROOT=layout.ROOT;PACKAGE=layout.PACKAGE;WORLD=layout.WORLD
VERSION='0.0.44-thin-walls-resources-movement'
NATURE={'treeS','treeM','treeL','ore'}


def terrain(geo,px,py,w,h):
    grid=layout.Grid(w,h,filled=True)
    for y in range(h):
        row=py+y
        if row<0 or row>=geo['height']:continue
        position=0
        for run in geo['rows'][row].split(';'):
            if not run:continue
            end=position+int(run[1:],16)
            a=max(position,px);b=min(end,px+w)
            if b>a:mark(grid,(a-px,y,b-a,1),run[0] in ('#','~','=','B'))
            position=end
            if position>=px+w:break
    return grid


def main():
    if read(PACKAGE/'manifest.json')['version']==VERSION:
        print('Already authored:',VERSION);return
    world=read(WORLD);geo=read(PACKAGE/'Data/Worlds/w2a_surface_geography_baked_v1.json')['definitions'][0]
    if any(o.get('metadata',{}).get('resourcePhysicalScale')=='2'
           for p in world['worldSiteBlueprintPlacements']
           for o in read((WORLD.parent/p['sourcePath']).resolve())['objectPlacements']):
        print('Resource doubling already authored; preserve subsequent manual layouts.');return
    staged=[];reserved=[];resources=[];count=0
    for placed in world['worldSiteBlueprintPlacements']:
        path=(WORLD.parent/placed['sourcePath']).resolve();bp=read(path)
        if placed.get('rotationQuarterTurns',0):raise ValueError('Resource reflow requires unrotated source')
        # Blueprint canvas is an author envelope, independent from fixed city walls
        # and RuntimeLayout Site control ranges. Shift all local sources equally.
        margin=0 if bp['blueprintId']=='base:site_huangcun' else 80
        placed['surfaceCellX']-=margin;placed['surfaceCellY']-=margin
        bp['widthCells']+=margin*2;bp['heightCells']+=margin*2
        for cell in bp['fineTerrainSources']:
            cell['surfaceCellX']+=margin;cell['surfaceCellY']+=margin
        for o in bp['objectPlacements']:
            o['localSurfaceX']+=margin;o['localSurfaceY']+=margin
            if o['kindId'] in NATURE:
                oldw,oldh=o['widthCells'],o['heightCells']
                o['widthCells']*=2;o['heightCells']*=2
                o['localSurfaceX']=round(o['localSurfaceX']-oldw/2)
                o['localSurfaceY']=round(o['localSurfaceY']-oldh/2)
                o.setdefault('metadata',{}).update(resourcePhysicalScale='2',legacyGeometryState='edited',
                    sourceCellsW=str(o['widthCells']),sourceCellsH=str(o['heightCells']))
                resources.append((placed,bp,o));count+=1
            else:
                x,y,w,h=layout.rect(o)
                reserved.append((placed['surfaceCellX']+x-3,placed['surfaceCellY']+y-3,w+6,h+6))
        walls=[o for o in bp['objectPlacements'] if o['kindId'] in ('wall','wallSegment')]
        if walls:
            left=min(o['localSurfaceX'] for o in walls);bottom=min(o['localSurfaceY'] for o in walls)
            right=max(o['localSurfaceX']+o['widthCells'] for o in walls)
            top=max(o['localSurfaceY']+o['heightCells'] for o in walls)
            reserved.append((placed['surfaceCellX']+left-4,placed['surfaceCellY']+bottom-4,right-left+8,top-bottom+8))
        staged.append((path,bp,placed))
    for anchor in world['runtimeLayout']['openingEntityAnchors']:
        reserved.append((anchor['surfaceCellX']-3,anchor['surfaceCellY']-3,6,6))
    placed_nature=[]
    for path,bp,p in staged:
        px,py=p['surfaceCellX'],p['surfaceCellY'];w,h=bp['widthCells'],bp['heightCells']
        grid=terrain(geo,px,py,w,h)
        for x,y,rw,rh in reserved+placed_nature:
            mark(grid,(x-px,y-py,rw,rh))
        objects=sorted([o for o in bp['objectPlacements'] if o['kindId'] in NATURE],
                       key=lambda o:(-o['widthCells']*o['heightCells'],o['placementId']))
        for o in objects:
            x,y,rw,rh=layout.rect(o);gap=3 if o['kindId']!='ore' else 2
            point=free_origin(grid,rw+gap*2,rh+gap*2,(x-gap,y-gap))
            if point is None:raise ValueError('No terrain-safe spaced resource location: '+o['placementId'])
            x,y=point[0]+gap,point[1]+gap
            o['localSurfaceX'],o['localSurfaceY']=x,y
            reserve=(x-gap,y-gap,rw+gap*2,rh+gap*2)
            mark(grid,reserve);placed_nature.append((reserve[0]+px,reserve[1]+py,reserve[2],reserve[3]))
        for place in world['runtimeLayout']['sitePlaces']:
            if place['siteId']!=bp['blueprintId']:continue
            bound=[o for o in bp['objectPlacements'] if o.get('metadata',{}).get('boundLocationId')==place['locationId']]
            if bound:
                place['surfaceCellX']=px+(min(o['localSurfaceX'] for o in bound)+max(o['localSurfaceX']+o['widthCells'] for o in bound))/2
                place['surfaceCellY']=py+(min(o['localSurfaceY'] for o in bound)+max(o['localSurfaceY']+o['heightCells'] for o in bound))/2
    # All layouts complete before writing any source.
    for path,bp,p in staged:write(path,bp)
    write(WORLD,world)
    for path in [PACKAGE/'manifest.json',WORLD.parent/'final_world_compaction_v1.json']:
        key='version' if path.name=='manifest.json' else 'contentVersion'
        path.write_text(path.read_text(encoding='utf-8-sig').replace(read(path)[key],VERSION),encoding='utf-8')
    print('Authored spaced doubled resources:',count,'towns:',len(staged),VERSION)


if __name__=='__main__':main()
