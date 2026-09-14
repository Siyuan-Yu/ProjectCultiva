using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    public enum StrategicControlSourceKind { WorldSite, FactionFlag }

    public readonly struct StrategicControlSource
    {
        public readonly string FactionId;
        public readonly StrategicControlSourceKind Kind;
        public readonly string SourceId;
        public StrategicControlSource(string factionId, StrategicControlSourceKind kind, string sourceId)
        { FactionId = factionId; Kind = kind; SourceId = sourceId; }
    }

    /// <summary>
    /// Exact-position administrative authority → derived strategic Hex/Region projection.
    /// Hex and TerritoryRegion are never persistence or physical-control authority.
    /// </summary>
    public static class StrategicTerritoryCoverageResolver
    {
        sealed class LegacyFlagProjection
        {
            public long Order;
            public string Id;
            public string Faction;
            public List<HexCoord> Nominal;
        }

        sealed class ResolutionCache
        {
            public readonly Dictionary<HexCoord, StrategicControlSource> Sources =
                new Dictionary<HexCoord, StrategicControlSource>();
        }

        static readonly ConditionalWeakTable<SimulationWorld, ResolutionCache> CacheByWorld =
            new ConditionalWeakTable<SimulationWorld, ResolutionCache>();

        public static bool TryGetSource(SimulationWorld world, HexCoord hex, out StrategicControlSource source)
        {
            source = default;
            return world != null && CacheByWorld.TryGetValue(world, out var cache) &&
                   cache.Sources.TryGetValue(hex, out source);
        }

        public static void Rebuild(SimulationWorld world)
        {
            if (world?.HexWorld == null || world.Strategic == null) return;
            var sources = CacheByWorld.GetOrCreateValue(world).Sources;
            sources.Clear();
            var flags = BuildLegacyFlags(world);
            var surfaces = CollectClaimSurfaces(world);

            for (var r = 0; r < world.HexWorld.Height; r++)
            for (var q = 0; q < world.HexWorld.Width; q++)
            {
                var hex = new HexCoord(q, r);
                if (!world.HexWorld.TryGetCell(hex, out var cell) || cell == null) continue;
                cell.ControlFactionId = string.Empty;
                HexMath.ToWorldPosition(hex, world.HexWorld.HexSize, out var x, out var y);

                if (TryResolveSiteAtHexCenter(world, surfaces, x, y, out var site, out _))
                {
                    var faction = site.OwnerFactionId ?? string.Empty;
                    cell.ControlFactionId = faction;
                    sources[hex] = new StrategicControlSource(
                        faction, StrategicControlSourceKind.WorldSite, site.SiteId);
                    continue;
                }

                // Legacy non-Site FactionFlag is a compatibility fallback only where no Site manages the point.
                for (var i = 0; i < flags.Count; i++)
                {
                    var flag = flags[i];
                    if (!flag.Nominal.Contains(hex)) continue;
                    cell.ControlFactionId = flag.Faction;
                    sources[hex] = new StrategicControlSource(
                        flag.Faction, StrategicControlSourceKind.FactionFlag, flag.Id);
                    break;
                }
            }

            var effectiveByRegion = new Dictionary<string, IReadOnlyList<HexCoord>>(StringComparer.Ordinal);
            foreach (var pair in world.Strategic.TerritoryRegions.Regions)
            {
                var region = pair.Value;
                if (region == null || !world.Strategic.Sites.TryGet(
                        region.PrimaryWorldSiteId, out var site) || site == null) continue;
                var effective = new List<HexCoord>();
                foreach (var item in sources)
                    if (item.Value.Kind == StrategicControlSourceKind.WorldSite &&
                        string.Equals(item.Value.SourceId, site.SiteId, StringComparison.Ordinal))
                        effective.Add(item.Key);
                effectiveByRegion[region.RegionId] = effective;
            }
            world.Strategic.TerritoryRegions.ReplaceHexesAtomically(effectiveByRegion);
            foreach (var pair in world.Strategic.TerritoryRegions.Regions)
            {
                var region = pair.Value;
                if (region == null || !world.Strategic.Sites.TryGet(
                        region.PrimaryWorldSiteId, out var site) || site == null) continue;
                region.ControlFactionId = site.OwnerFactionId ?? string.Empty;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TerritoryClaimInvariantValidator.AssertValid(world);
#endif
        }

        static bool TryResolveSiteAtHexCenter(
            SimulationWorld world, IReadOnlyList<string> surfaces, float x, float y,
            out WorldSite site, out TerritoryClaimState claim)
        {
            site = null;
            claim = null;
            if (!world.Strategic.TerritoryClaims.HasAuthority)
                return WorldSiteCoreCoverageResolver.TryResolve(
                    world, ResolveLegacySurface(world, x, y), x, y, out site);
            for (var i = 0; i < surfaces.Count; i++)
            {
                if (!WorldSiteAdministrativeControlResolver.TryResolve(
                        world, surfaces[i], x, y, out var candidate, out var candidateClaim)) continue;
                if (claim == null || candidateClaim.AcquiredOrder < claim.AcquiredOrder ||
                    (candidateClaim.AcquiredOrder == claim.AcquiredOrder &&
                     string.CompareOrdinal(candidateClaim.ClaimId, claim.ClaimId) < 0))
                {
                    site = candidate;
                    claim = candidateClaim;
                }
            }
            return site != null;
        }

        static string ResolveLegacySurface(SimulationWorld world, float x, float y)
        {
            foreach (var pair in world.Strategic.Sites.Sites)
                if (pair.Value != null && pair.Value.HasContinuousCore &&
                    WorldSiteCoreCoverageResolver.Contains(
                        pair.Value, pair.Value.CoreSurfaceId, x, y))
                    return pair.Value.CoreSurfaceId;
            return string.Empty;
        }

        static List<string> CollectClaimSurfaces(SimulationWorld world)
        {
            var result = new List<string>();
            var claims = world.Strategic.TerritoryClaims.Claims;
            for (var i = 0; i < claims.Count; i++)
                if (!result.Contains(claims[i].SurfaceId)) result.Add(claims[i].SurfaceId);
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        static List<LegacyFlagProjection> BuildLegacyFlags(SimulationWorld world)
        {
            var result = new List<LegacyFlagProjection>();
            foreach (var pair in world.Strategic.FactionFlags.Flags)
            {
                var flag = pair.Value;
                if (flag == null || flag.IsSiteCore || string.IsNullOrEmpty(flag.FactionId)) continue;
                result.Add(new LegacyFlagProjection
                {
                    Order = flag.EstablishedOrder,
                    Id = flag.FlagId,
                    Faction = flag.FactionId,
                    Nominal = ExpandOneRing(new[] { flag.AnchorHex })
                });
            }
            result.Sort((a, b) =>
            {
                var order = a.Order.CompareTo(b.Order);
                return order != 0 ? order : string.CompareOrdinal(a.Id, b.Id);
            });
            return result;
        }

        public static List<HexCoord> ExpandOneRing(IEnumerable<HexCoord> bases)
        {
            var set = new HashSet<HexCoord>();
            if (bases != null)
                foreach (var hex in bases)
                {
                    set.Add(hex);
                    for (var d = 0; d < 6; d++) set.Add(HexMath.Neighbor(hex, d));
                }
            return new List<HexCoord>(set);
        }
    }

    public static class TerritoryClaimInvariantValidator
    {
        public static List<string> Validate(SimulationWorld world)
        {
            var errors = new List<string>();
            if (world?.Strategic?.TerritoryClaims == null ||
                !world.Strategic.TerritoryClaims.HasAuthority) return errors;
            var claims = world.Strategic.TerritoryClaims.Claims;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var orders = new HashSet<long>();
            for (var i = 0; i < claims.Count; i++)
            {
                var claim = claims[i];
                if (claim == null || !ids.Add(claim?.ClaimId ?? string.Empty) ||
                    !orders.Add(claim?.AcquiredOrder ?? 0) ||
                    !world.Strategic.Sites.TryGet(claim?.SiteId ?? string.Empty, out _))
                    errors.Add("Invalid TerritoryClaim at index " + i + ".");
            }
            foreach (var pair in world.Strategic.Sites.Sites)
            {
                var site = pair.Value;
                if (site == null || !site.IsCoreActive || !site.HasContinuousCore) continue;
                var found = false;
                foreach (var unused in world.Strategic.TerritoryClaims.EnumerateForSite(site.SiteId))
                    { found = true; break; }
                if (!found) errors.Add("Active continuous Site has no TerritoryClaim: " + site.SiteId);
            }
            foreach (var claim in claims)
            {
                if (!world.Strategic.Sites.TryGet(claim.SiteId, out var site) || site == null ||
                    site.IsCoreActive) continue;
                if (WorldSiteAdministrativeControlResolver.TryResolve(
                        world, claim.SurfaceId, claim.CenterX, claim.CenterY,
                        out var inactiveWinner, out _) && inactiveWinner == site)
                    errors.Add("Inactive Site won administrative control: " + site.SiteId);
            }
            for (var r = 0; r < world.HexWorld.Height; r++)
            for (var q = 0; q < world.HexWorld.Width; q++)
            {
                var hex = new HexCoord(q, r);
                if (!StrategicTerritoryCoverageResolver.TryGetSource(world, hex, out var source) ||
                    source.Kind != StrategicControlSourceKind.WorldSite) continue;
                if (!world.Strategic.Sites.TryGet(source.SourceId, out var site) || site == null)
                {
                    errors.Add("Hex projection references missing Site: " + source.SourceId);
                    continue;
                }
                HexMath.ToWorldPosition(hex, world.HexWorld.HexSize, out var x, out var y);
                if (!WorldSiteAdministrativeControlResolver.TryResolve(
                        world, site.CoreSurfaceId, x, y, out var resolved, out _) ||
                    !string.Equals(resolved.SiteId, site.SiteId, StringComparison.Ordinal))
                    errors.Add("Hex projection does not match exact administration: " + hex);
                if (world.HexWorld.TryGetCell(hex, out var cell) && cell != null &&
                    !string.Equals(cell.ControlFactionId ?? string.Empty,
                        site.OwnerFactionId ?? string.Empty, StringComparison.Ordinal))
                    errors.Add("Hex controller does not match Site Owner: " + hex);
            }
            errors.AddRange(TerritoryInvariantValidator.Validate(world));
            return errors;
        }

        public static void AssertValid(SimulationWorld world)
        {
            var errors = Validate(world);
            for (var i = 0; i < errors.Count; i++)
                System.Diagnostics.Debug.Fail("[TerritoryClaimInvariant] " + errors[i]);
        }
    }
}
