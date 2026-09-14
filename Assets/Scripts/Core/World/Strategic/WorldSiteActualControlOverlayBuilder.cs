using System;
using System.Collections.Generic;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>One exact world-space rectangle owned by a Site after Claim priority resolution.</summary>
    public sealed class ActualControlRect
    {
        public float MinX { get; internal set; }
        public float MinY { get; internal set; }
        public float MaxX { get; internal set; }
        public float MaxY { get; internal set; }
    }

    public sealed class ActualControlBoundarySegment
    {
        public float X0 { get; internal set; }
        public float Y0 { get; internal set; }
        public float X1 { get; internal set; }
        public float Y1 { get; internal set; }
    }

    /// <summary>Read-only presentation geometry derived from actual administrative authority.</summary>
    public sealed class WorldSiteActualControlOverlay
    {
        readonly List<ActualControlRect> _pieces = new List<ActualControlRect>();
        readonly List<ActualControlBoundarySegment> _boundarySegments =
            new List<ActualControlBoundarySegment>();

        public string SiteId { get; internal set; } = string.Empty;
        public string SurfaceId { get; internal set; } = string.Empty;
        public string FactionId { get; internal set; } = string.Empty;
        public IReadOnlyList<ActualControlRect> Pieces => _pieces;
        public IReadOnlyList<ActualControlBoundarySegment> BoundarySegments => _boundarySegments;
        internal List<ActualControlRect> MutablePieces => _pieces;
        internal List<ActualControlBoundarySegment> MutableBoundarySegments => _boundarySegments;
        public float MinX { get; internal set; }
        public float MinY { get; internal set; }
        public float MaxX { get; internal set; }
        public float MaxY { get; internal set; }
    }

    /// <summary>
    /// Converts Claim authority into exact axis-aligned drawing pieces. Claim edges define the
    /// finite partition; ownership of every partition cell is queried exclusively through
    /// WorldSiteAdministrativeControlResolver. This is geometry conversion, not another
    /// territory rule or a Hex approximation.
    /// </summary>
    public static class WorldSiteActualControlOverlayBuilder
    {
        const float Epsilon = .00001f;

        public static List<WorldSiteActualControlOverlay> Build(SimulationWorld world)
        {
            var result = new List<WorldSiteActualControlOverlay>();
            var claims = world?.Strategic?.TerritoryClaims?.Claims;
            if (claims == null || !world.Strategic.TerritoryClaims.HasAuthority)
                return result;

            var surfaces = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < claims.Count; i++)
                if (IsEligible(world, claims[i])) surfaces.Add(claims[i].SurfaceId);
            var orderedSurfaces = new List<string>(surfaces);
            orderedSurfaces.Sort(StringComparer.Ordinal);
            for (var i = 0; i < orderedSurfaces.Count; i++)
                BuildSurface(world, claims, orderedSurfaces[i], result);
            result.Sort((a, b) =>
            {
                var surface = string.CompareOrdinal(a.SurfaceId, b.SurfaceId);
                return surface != 0 ? surface : string.CompareOrdinal(a.SiteId, b.SiteId);
            });
            return result;
        }

        static void BuildSurface(
            SimulationWorld world,
            IReadOnlyList<TerritoryClaimState> claims,
            string surfaceId,
            List<WorldSiteActualControlOverlay> output)
        {
            var xs = new List<float>();
            var ys = new List<float>();
            for (var i = 0; i < claims.Count; i++)
            {
                var claim = claims[i];
                if (!IsEligible(world, claim) ||
                    !string.Equals(claim.SurfaceId, surfaceId, StringComparison.Ordinal) ||
                    !world.Strategic.Sites.TryGet(claim.SiteId, out var site)) continue;
                var minX = Math.Max(claim.CenterX - claim.Width * .5f,
                    site.CoreWorldX - site.CoreRangeWidth * .5f);
                var maxX = Math.Min(claim.CenterX + claim.Width * .5f,
                    site.CoreWorldX + site.CoreRangeWidth * .5f);
                var minY = Math.Max(claim.CenterY - claim.Height * .5f,
                    site.CoreWorldY - site.CoreRangeHeight * .5f);
                var maxY = Math.Min(claim.CenterY + claim.Height * .5f,
                    site.CoreWorldY + site.CoreRangeHeight * .5f);
                if (maxX - minX <= Epsilon || maxY - minY <= Epsilon) continue;
                xs.Add(minX); xs.Add(maxX); ys.Add(minY); ys.Add(maxY);
            }
            SortAndDeduplicate(xs);
            SortAndDeduplicate(ys);
            if (xs.Count < 2 || ys.Count < 2) return;

            var bySite = new Dictionary<string, WorldSiteActualControlOverlay>(StringComparer.Ordinal);
            var owners = new string[xs.Count - 1, ys.Count - 1];
            for (var yi = 0; yi + 1 < ys.Count; yi++)
            {
                var minY = ys[yi];
                var maxY = ys[yi + 1];
                for (var xi = 0; xi + 1 < xs.Count; xi++)
                {
                    var minX = xs[xi];
                    var maxX = xs[xi + 1];
                    var hasOwner = WorldSiteAdministrativeControlResolver.TryResolve(
                        world, surfaceId, (minX + maxX) * .5f, (minY + maxY) * .5f,
                        out var site, out _);
                    owners[xi, yi] = hasOwner ? site.SiteId : null;
                }
            }

            for (var yi = 0; yi + 1 < ys.Count; yi++)
            {
                var minY = ys[yi];
                var maxY = ys[yi + 1];
                string runSiteId = null;
                float runMinX = 0f;
                for (var xi = 0; xi + 1 < xs.Count; xi++)
                {
                    var minX = xs[xi];
                    var maxX = xs[xi + 1];
                    var siteId = owners[xi, yi];
                    if (!string.Equals(runSiteId, siteId, StringComparison.Ordinal))
                    {
                        if (runSiteId != null)
                            AddOrMerge(bySite, world, surfaceId, runSiteId, runMinX, minX, minY, maxY);
                        runSiteId = siteId;
                        runMinX = minX;
                    }
                    if (xi + 2 == xs.Count && runSiteId != null)
                        AddOrMerge(bySite, world, surfaceId, runSiteId, runMinX, maxX, minY, maxY);
                }
            }

            for (var yi = 0; yi + 1 < ys.Count; yi++)
            for (var xi = 0; xi + 1 < xs.Count; xi++)
            {
                var siteId = owners[xi, yi];
                if (siteId == null) continue;
                var overlay = EnsureOverlay(bySite, world, surfaceId, siteId);
                if (xi == 0 || !string.Equals(owners[xi - 1, yi], siteId, StringComparison.Ordinal))
                    AddBoundary(overlay, xs[xi], ys[yi], xs[xi], ys[yi + 1]);
                if (xi + 1 == xs.Count - 1 || !string.Equals(owners[xi + 1, yi], siteId, StringComparison.Ordinal))
                    AddBoundary(overlay, xs[xi + 1], ys[yi], xs[xi + 1], ys[yi + 1]);
                if (yi == 0 || !string.Equals(owners[xi, yi - 1], siteId, StringComparison.Ordinal))
                    AddBoundary(overlay, xs[xi], ys[yi], xs[xi + 1], ys[yi]);
                if (yi + 1 == ys.Count - 1 || !string.Equals(owners[xi, yi + 1], siteId, StringComparison.Ordinal))
                    AddBoundary(overlay, xs[xi], ys[yi + 1], xs[xi + 1], ys[yi + 1]);
            }

            foreach (var pair in bySite)
            {
                ComputeBounds(pair.Value);
                output.Add(pair.Value);
            }
        }

        static void AddOrMerge(
            Dictionary<string, WorldSiteActualControlOverlay> bySite,
            SimulationWorld world,
            string surfaceId,
            string siteId,
            float minX,
            float maxX,
            float minY,
            float maxY)
        {
            if (maxX - minX <= Epsilon || maxY - minY <= Epsilon) return;
            var overlay = EnsureOverlay(bySite, world, surfaceId, siteId);
            // Horizontal runs were already merged. Coalesce identical runs in consecutive Y bands.
            for (var i = overlay.MutablePieces.Count - 1; i >= 0; i--)
            {
                var prior = overlay.MutablePieces[i];
                if (prior.MaxY < minY - Epsilon) break;
                if (Nearly(prior.MinX, minX) && Nearly(prior.MaxX, maxX) && Nearly(prior.MaxY, minY))
                {
                    prior.MaxY = maxY;
                    return;
                }
            }
            overlay.MutablePieces.Add(new ActualControlRect
                { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY });
        }

        static WorldSiteActualControlOverlay EnsureOverlay(
            Dictionary<string, WorldSiteActualControlOverlay> bySite,
            SimulationWorld world,
            string surfaceId,
            string siteId)
        {
            if (bySite.TryGetValue(siteId, out var overlay)) return overlay;
            world.Strategic.Sites.TryGet(siteId, out var site);
            overlay = new WorldSiteActualControlOverlay
            {
                SiteId = siteId,
                SurfaceId = surfaceId,
                FactionId = site?.OwnerFactionId ?? string.Empty
            };
            bySite.Add(siteId, overlay);
            return overlay;
        }

        static void AddBoundary(
            WorldSiteActualControlOverlay overlay, float x0, float y0, float x1, float y1) =>
            overlay.MutableBoundarySegments.Add(new ActualControlBoundarySegment
                { X0 = x0, Y0 = y0, X1 = x1, Y1 = y1 });

        static void ComputeBounds(WorldSiteActualControlOverlay overlay)
        {
            overlay.MinX = overlay.MinY = float.MaxValue;
            overlay.MaxX = overlay.MaxY = float.MinValue;
            for (var i = 0; i < overlay.Pieces.Count; i++)
            {
                var piece = overlay.Pieces[i];
                overlay.MinX = Math.Min(overlay.MinX, piece.MinX);
                overlay.MinY = Math.Min(overlay.MinY, piece.MinY);
                overlay.MaxX = Math.Max(overlay.MaxX, piece.MaxX);
                overlay.MaxY = Math.Max(overlay.MaxY, piece.MaxY);
            }
        }

        static bool IsEligible(SimulationWorld world, TerritoryClaimState claim) =>
            claim != null && claim.Width > 0f && claim.Height > 0f &&
            !string.IsNullOrWhiteSpace(claim.SurfaceId) &&
            world.Strategic.Sites.TryGet(claim.SiteId, out var site) && site != null &&
            site.IsCoreActive && site.HasContinuousCore;

        static void SortAndDeduplicate(List<float> values)
        {
            values.Sort();
            for (var i = values.Count - 1; i > 0; i--)
                if (Nearly(values[i], values[i - 1])) values.RemoveAt(i);
        }

        static bool Nearly(float a, float b) => Math.Abs(a - b) <= Epsilon;
    }
}
