"""Author-only monotone remap. Protected intervals translate without scaling.

No runtime dependency: the publisher consumes the resulting normal composition.
"""
import math


class Axis:
    def __init__(self, length, intervals, target, minimum_free_scale):
        merged = []
        for a, b in sorted((max(0, a), min(length, b)) for a, b in intervals):
            if b <= a:
                continue
            if merged and a <= merged[-1][1]:
                merged[-1][1] = max(b, merged[-1][1])
            else:
                merged.append([a, b])
        protected = sum(b-a for a, b in merged)
        free = length-protected
        self.scale = max(minimum_free_scale, min(1, (length*target-protected)/max(free, 1e-9)))
        self.intervals = merged
        self.knots = sorted({0, length, *(v for pair in merged for v in pair)})
        self.length = self.map(length)

    def map(self, value):
        protected = sum(max(0, min(value, b)-a) for a, b in self.intervals if value > a)
        return value*self.scale + protected*(1-self.scale)


class Compaction:
    def __init__(self, geo, plan, config):
        scale = geo['cellsPerMapPixel']
        intervals = [[], []]
        regions = {r['id']: r for r in geo['regions']}
        regions['huangcun'] = geo['openingBasin']
        for name in config['rigidRegions']:
            region = regions[name]
            points = list(region['points'])
            points += [region[k] for k in ('innerGate', 'outerGate') if k in region]
            for axis in range(2):
                intervals[axis].append((min(p[axis] for p in points)-4, max(p[axis] for p in points)+4))
        # Keep the existing physical street/cliff junction around every entry city.
        for row in plan:
            if not row['entryRegion']:
                continue
            radius = ((90+row['level']*90)/2 + config['gateBufferCells'])/scale
            for axis, key in enumerate(('mapX', 'mapY')):
                intervals[axis].append((row[key]-radius, row[key]+radius))
        # Reserve full five-level city envelopes, including the existing closest gap.
        # Protect the separating axis of pairs which uniform target scaling would crowd.
        separation = config['townReserveCells'] + config['minimumTownGapCells']
        target = config['targetLinearScale']
        for i, a in enumerate(plan):
            for b in plan[i+1:]:
                delta = [abs(a[k]-b[k]) for k in ('mapX', 'mapY')]
                axis = 0 if delta[0] >= delta[1] else 1
                if delta[axis]*scale < separation/target:
                    key = ('mapX', 'mapY')[axis]
                    intervals[axis].append((min(a[key], b[key]), max(a[key], b[key])))
        # Free strips may compress more than the global target. Add constraints until
        # every actual five-level square has its required gap; never shrink buildings.
        for _ in range(len(plan)*len(plan)):
            self.axes = [Axis(length, spans, target, config['minimumFreeScale'])
                         for length, spans in zip((geo['mapWidth'], geo['mapHeight']), intervals)]
            conflict = None
            for i, a in enumerate(plan):
                pa = self.point((a['mapX'], a['mapY']))
                for b in plan[i+1:]:
                    pb = self.point((b['mapX'], b['mapY']))
                    if max(abs(pa[j]-pb[j]) for j in (0, 1))*scale < separation-1e-6:
                        conflict = (a, b)
                        break
                if conflict:
                    break
            if not conflict:
                break
            a, b = conflict
            axis = 0 if abs(a['mapX']-b['mapX']) >= abs(a['mapY']-b['mapY']) else 1
            key = ('mapX', 'mapY')[axis]
            lo, hi = sorted((a[key], b[key]))
            if (hi-lo)*scale < separation-1e-6:
                raise ValueError('Original town reserve already overlaps: '+a['displayName']+'/'+b['displayName'])
            intervals[axis].append((lo, hi))
        else:
            raise ValueError('Cannot satisfy town reserve constraints')
        # Compatibility publication requires 50-cell dimensions. Round outwards;
        # the extra margin never moves terrain, gate geometry or town centers.
        self.width = math.ceil((geo['offsetCellsX']*2+self.axes[0].length*scale)/50)*50
        self.height = math.ceil((geo['offsetCellsY']*2+self.axes[1].length*scale)/50)*50
        centers = [(geo['offsetCellsX']+self.axes[0].map(r['mapX'])*scale,
                    geo['offsetCellsY']+(self.axes[1].length-self.axes[1].map(r['mapY']))*scale) for r in plan]
        reserve = config['townReserveCells']/2 + 1 # integer center rounding allowance
        self.left_padding = max(0, math.ceil((reserve-min(p[0] for p in centers))/50)*50)
        self.bottom_padding = max(0, math.ceil((reserve-min(p[1] for p in centers))/50)*50)
        right = max(0, math.ceil((max(p[0] for p in centers)+reserve-self.width)/50)*50)
        top = max(0, math.ceil((max(p[1] for p in centers)+reserve-self.height)/50)*50)
        self.width += self.left_padding + right
        self.height += self.bottom_padding + top

    def point(self, p):
        return tuple(axis.map(v) for axis, v in zip(self.axes, p))

    def split_path(self, points, closed=False):
        result = []
        pairs = zip(points, points[1:]+points[:1] if closed else points[1:])
        for a, b in pairs:
            ts = {0.0}
            for axis in (0, 1):
                d = b[axis]-a[axis]
                if d:
                    ts.update((k-a[axis])/d for k in self.axes[axis].knots if 0 < (k-a[axis])/d < 1)
            result.extend([a[0]+t*(b[0]-a[0]), a[1]+t*(b[1]-a[1])] for t in sorted(ts))
        if not closed:
            result.append(points[-1])
        return result
