using System;
using System.Collections.Generic;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Ordinary Hex Exit Zone 同边 overlap 消解：优先 shrink span，必要时沿边最小位移。
    /// </summary>
    static class SurfaceExitZoneOverlapResolver
    {
        enum PerimeterEdge
        {
            Right = 0,
            Left = 1,
            Top = 2,
            Bottom = 3,
        }

        struct ZoneDraft
        {
            public SurfaceExitConnection Connection;
            public PerimeterEdge Edge;
            public float OriginalAlongCoord;
            public float EdgeLength;
            public float ResolvedAlongCoord;
            public float ResolvedSpan;
        }

        static readonly List<ZoneDraft> EdgeScratch = new List<ZoneDraft>(4);

        public static void ResolveOrdinaryHexOverlaps(
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float exitTriggerDepth,
            IList<SurfaceExitConnection> connections) =>
            ResolveOverlaps(bounds, exitTriggerDepth, connections);

        public static void ResolveOverlaps(
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float exitTriggerDepth,
            IList<SurfaceExitConnection> connections)
        {
            if (connections == null || connections.Count == 0)
                return;

            var drafts = new ZoneDraft[connections.Count];
            for (var i = 0; i < connections.Count; i++)
                drafts[i] = ToDraft(connections[i], bounds);

            for (var edge = 0; edge < 4; edge++)
            {
                EdgeScratch.Clear();
                for (var i = 0; i < drafts.Length; i++)
                {
                    if ((int)drafts[i].Edge != edge)
                        continue;
                    EdgeScratch.Add(drafts[i]);
                }

                if (EdgeScratch.Count == 0)
                    continue;

                if (EdgeScratch.Count == 1)
                {
                    var single = EdgeScratch[0];
                    single.ResolvedSpan = DefaultSpanForSingle(single.EdgeLength);
                    single.ResolvedAlongCoord = single.OriginalAlongCoord;
                    WriteBack(ref drafts, single);
                    continue;
                }

                ResolveEdgeGroup(EdgeScratch, bounds);
                for (var i = 0; i < EdgeScratch.Count; i++)
                    WriteBack(ref drafts, EdgeScratch[i]);
            }

            for (var i = 0; i < connections.Count; i++)
                connections[i] = RebuildConnection(drafts[i], bounds, exitTriggerDepth);
        }

        static void ResolveEdgeGroup(
            List<ZoneDraft> group,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds)
        {
            group.Sort(CompareDrafts);
            var edgeLen = group[0].EdgeLength;
            var minSpan = SurfaceExitZoneCalculator.MinSlotSpanFraction * edgeLen;
            var maxSpan = SurfaceExitZoneCalculator.MaxSlotSpanFraction * edgeLen;
            var defaultSpan = SurfaceExitZoneCalculator.DefaultSlotSpanFraction * edgeLen;

            var minGap = float.MaxValue;
            for (var i = 1; i < group.Count; i++)
            {
                var gap = group[i].OriginalAlongCoord - group[i - 1].OriginalAlongCoord;
                if (gap < minGap)
                    minGap = gap;
            }

            var spanWithoutShift = Math.Min(defaultSpan, Math.Min(maxSpan, minGap));
            var needsShift = spanWithoutShift + 0.0001f < minSpan;

            if (!needsShift)
            {
                var frac = QuantizeSpanDown(spanWithoutShift / edgeLen);
                var span = Math.Min(frac * edgeLen, minGap);
                span = Math.Max(minSpan, Math.Min(maxSpan, span));
                if (span <= minGap + 0.001f)
                {
                    for (var i = 0; i < group.Count; i++)
                    {
                        var d = group[i];
                        d.ResolvedSpan = span;
                        d.ResolvedAlongCoord = d.OriginalAlongCoord;
                        group[i] = d;
                    }

                    return;
                }
            }

            var resolvedSpan = minSpan;
            var half = resolvedSpan * 0.5f;
            GetEdgeAlongLimits(group[0].Edge, bounds, half, out var limitMin, out var limitMax);
            var resolved = new float[group.Count];
            for (var i = 0; i < group.Count; i++)
                resolved[i] = group[i].OriginalAlongCoord;

            for (var i = 1; i < group.Count; i++)
                resolved[i] = Math.Max(resolved[i], resolved[i - 1] + resolvedSpan);

            if (resolved[group.Count - 1] > limitMax)
            {
                resolved[group.Count - 1] = limitMax;
                for (var i = group.Count - 2; i >= 0; i--)
                    resolved[i] = Math.Min(resolved[i], resolved[i + 1] - resolvedSpan);
            }

            for (var i = 1; i < group.Count; i++)
                resolved[i] = Math.Max(resolved[i], resolved[i - 1] + resolvedSpan);

            if (resolved[0] < limitMin)
            {
                var delta = limitMin - resolved[0];
                for (var i = 0; i < group.Count; i++)
                    resolved[i] += delta;
            }

            if (resolved[group.Count - 1] > limitMax)
            {
                var delta = resolved[group.Count - 1] - limitMax;
                for (var i = 0; i < group.Count; i++)
                    resolved[i] -= delta;
            }

            for (var i = 0; i < group.Count; i++)
            {
                var d = group[i];
                d.ResolvedSpan = resolvedSpan;
                d.ResolvedAlongCoord = resolved[i];
                group[i] = d;
            }
        }

        static float DefaultSpanForSingle(float edgeLength) =>
            Math.Min(
                SurfaceExitZoneCalculator.DefaultSlotSpanFraction * edgeLength,
                SurfaceExitZoneCalculator.MaxSlotSpanFraction * edgeLength);

        static float QuantizeSpanDown(float fraction)
        {
            var steps = new[]
            {
                SurfaceExitZoneCalculator.DefaultSlotSpanFraction,
                0.25f,
                0.20f,
                SurfaceExitZoneCalculator.MinSlotSpanFraction,
            };
            var best = SurfaceExitZoneCalculator.MinSlotSpanFraction;
            for (var i = 0; i < steps.Length; i++)
            {
                if (steps[i] <= fraction + 0.0001f)
                    best = steps[i];
            }

            return best;
        }

        static int CompareDrafts(ZoneDraft a, ZoneDraft b)
        {
            var cmp = a.OriginalAlongCoord.CompareTo(b.OriginalAlongCoord);
            if (cmp != 0)
                return cmp;
            cmp = a.Connection.DestinationHex.Q.CompareTo(b.Connection.DestinationHex.Q);
            return cmp != 0 ? cmp : a.Connection.DestinationHex.R.CompareTo(b.Connection.DestinationHex.R);
        }

        static ZoneDraft ToDraft(
            SurfaceExitConnection connection,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds)
        {
            ClassifyEdge(
                connection.LocalDirectionX,
                connection.LocalDirectionY,
                connection.ExitCenterLocalX,
                connection.ExitCenterLocalY,
                bounds,
                out var edge,
                out var edgeLen,
                out var along);
            return new ZoneDraft
            {
                Connection = connection,
                Edge = edge,
                OriginalAlongCoord = along,
                EdgeLength = edgeLen,
                ResolvedAlongCoord = along,
                ResolvedSpan = DefaultSpanForSingle(edgeLen),
            };
        }

        static void ClassifyEdge(
            float localDirX,
            float localDirY,
            float centerX,
            float centerY,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            out PerimeterEdge edge,
            out float edgeLength,
            out float alongCoord)
        {
            if (Math.Abs(localDirX) >= Math.Abs(localDirY))
            {
                edgeLength = bounds.MaxY - bounds.MinY;
                alongCoord = centerY;
                edge = localDirX > 0f ? PerimeterEdge.Right : PerimeterEdge.Left;
            }
            else
            {
                edgeLength = bounds.MaxX - bounds.MinX;
                alongCoord = centerX;
                edge = localDirY > 0f ? PerimeterEdge.Top : PerimeterEdge.Bottom;
            }
        }

        static void WriteBack(ref ZoneDraft[] all, ZoneDraft updated)
        {
            var dest = updated.Connection.DestinationHex;
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].Connection.DestinationHex != dest)
                    continue;
                all[i] = updated;
                return;
            }
        }

        static void GetEdgeAlongLimits(
            PerimeterEdge edge,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float halfSpan,
            out float limitMin,
            out float limitMax)
        {
            if (edge == PerimeterEdge.Right || edge == PerimeterEdge.Left)
            {
                limitMin = bounds.MinY + halfSpan;
                limitMax = bounds.MaxY - halfSpan;
            }
            else
            {
                limitMin = bounds.MinX + halfSpan;
                limitMax = bounds.MaxX - halfSpan;
            }
        }

        static SurfaceExitConnection RebuildConnection(
            ZoneDraft draft,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float exitTriggerDepth)
        {
            var c = draft.Connection;
            var cx = c.ExitCenterLocalX;
            var cy = c.ExitCenterLocalY;
            if (draft.Edge == PerimeterEdge.Right || draft.Edge == PerimeterEdge.Left)
                cy = draft.ResolvedAlongCoord;
            else
                cx = draft.ResolvedAlongCoord;

            LocalMapHexDirectionProjection.TryBuildSlotRectAtSpan(
                bounds,
                exitTriggerDepth,
                draft.ResolvedSpan,
                cx,
                cy,
                c.LocalDirectionX,
                c.LocalDirectionY,
                out var slot);

            return new SurfaceExitConnection(
                c.SourceHex,
                c.DestinationHex,
                c.DirectionIndex,
                c.DestinationKind,
                c.DestinationSiteId,
                c.LocalDirectionX,
                c.LocalDirectionY,
                cx,
                cy,
                slot,
                c.BoundaryContactWorldX,
                c.BoundaryContactWorldY);
        }
    }
}
