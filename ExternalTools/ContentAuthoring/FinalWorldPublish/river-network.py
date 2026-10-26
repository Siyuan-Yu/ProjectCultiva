"""Approved river authoring, using runtime-world points on the frozen surface."""
import json
import math

def smooth(points):
    result=[]
    for i in range(len(points)-1):
        a,b,c,d=points[max(0,i-1)],points[i],points[i+1],points[min(len(points)-1,i+2)]
        for j in range(8):
            t=j/8
            result.append(tuple(.5*(2*b[k]+(-a[k]+c[k])*t+(2*a[k]-5*b[k]+4*c[k]-d[k])*t*t+(-a[k]+3*b[k]-3*c[k]+d[k])*t*t*t) for k in (0,1)))
    return result+[tuple(points[-1])]

def apply(world,source,plan,center,write):
    config=json.loads((source/'Worlds/final_world_river_network_v1.json').read_text(encoding='utf-8'))
    surface=world['runtimeSurface']
    origin=(surface['originWorldX'],surface['originWorldY'])
    def cell(p):return tuple((v-o)/surface['surfaceCellWorldSize'] for v,o in zip(p,origin))
    mountains={(p['editorCellX'],p['editorCellY']) for p in world['coarseTerrainSources']}
    for river in config['rivers']:
        points=[cell(p) for p in river['points']];line=smooth(points)
        # Full five-level city squares stay dry, including river half-width.
        for row in plan:
            cx,cy=center(row);margin=270+river['width']/2+3
            for a,b in zip(line,line[1:]):
                steps=max(1,math.ceil(math.dist(a,b)/10))
                if any(abs(a[0]+(b[0]-a[0])*i/steps-cx)<margin and abs(a[1]+(b[1]-a[1])*i/steps-cy)<margin for i in range(steps+1)):
                    raise ValueError('River touches town reserve: '+river['id']+' / '+row['displayName'])
        rid='base:river_'+river['id']
        world['rivers'].append(dict(pathId=rid,widthCells=river['width'],controlPoints=[dict(x=x,y=y) for x,y in points]))
        for i,requested in enumerate(river['bridges']):
            target=cell(requested);best=None
            for a,b in zip(line,line[1:]):
                dx,dy=b[0]-a[0],b[1]-a[1];den=dx*dx+dy*dy
                t=max(0,min(1,((target[0]-a[0])*dx+(target[1]-a[1])*dy)/den)) if den else 0
                p=(a[0]+dx*t,a[1]+dy*t);candidate=(math.dist(p,target),p,abs(dx)>abs(dy))
                if best is None or candidate[0]<best[0]:best=candidate
            _,(x,y),vertical=best
            length=river['width']*2+60;w,h=(24,length) if vertical else (length,24)
            if any((bx//10,by//10) in mountains
                   for by in range(round(y-h/2)-3,round(y+h/2)+3)
                   for bx in range(round(x-w/2)-3,round(x+w/2)+3)):
                raise ValueError('Bridge would cut mountain wall: '+river['id']+' / '+str(i))
            road='base:road_'+river['id']+'_bridge_'+str(i)
            world['roads'].append(dict(pathId=road,roadClass='Road',controlPoints=[dict(x=x-(0 if vertical else length),y=y-(length if vertical else 0)),dict(x=x+(0 if vertical else length),y=y+(length if vertical else 0))]))
            world['crossings'].append(dict(riverPathId=rid,roadPathId=road,x=x,y=y,resolution='Bridge'))
            world['worldObjectPlacements'].append(dict(placementId=road.replace('base:road_', 'base:bridge_'),kindId='bridge',worldSurfaceX=round(x-w/2),worldSurfaceY=round(y-h/2),widthCells=w,heightCells=h,rotationQuarterTurns=0,metadata=dict(riverPathId=rid,roadPathId=road)))
    for i,p in enumerate(config['lakes']):
        x,y=map(round,cell(p));rx,ry=125,85;key='final_river_lake_'+str(i)
        fine=[dict(surfaceCellX=dx+rx,surfaceCellY=dy+ry,baseTerrainOverride='Water',featureOverride='None',featureDensity=0) for dy in range(-ry,ry) for dx in range(-rx,rx) if (dx/rx)**2+(dy/ry)**2<=1]
        path=source/'DetailPatches'/(key+'.detailpatch.json')
        write(path,dict(schemaVersion=3,patchId=key,displayName='河源湖',widthCells=rx*2,heightCells=ry*2,fineTerrainOverrides=fine))
        world['detailPatchPlacements'].append(dict(placementId=key,patchId=key,sourcePath='../DetailPatches/'+path.name,surfaceCellX=x-rx,surfaceCellY=y-ry))
