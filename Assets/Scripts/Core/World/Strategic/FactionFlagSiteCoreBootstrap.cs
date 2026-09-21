using System;
using System.Collections.Generic;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Promotes explicitly authored precise FactionFlags into the same runtime Site-Core model used
    /// by player-built flags. This service never derives a precise position from AnchorHex.
    /// </summary>
    public static class FactionFlagSiteCoreBootstrap
    {
        public static Result EnsureAuthoredSiteCores(
            SimulationWorld world,
            bool allowCreateMissing,
            ErrorCode errorCode)
        {
            if (world?.Strategic?.SpatialRules == null)
                return Result.Failure(errorCode, "Authored FactionFlag Site-Core bootstrap requires spatial rules.");

            var flags = new List<FactionFlagState>();
            foreach (var pair in world.Strategic.FactionFlags.Flags)
                if (pair.Value != null && pair.Value.IsAuthoredSiteCore)
                    flags.Add(pair.Value);
            flags.Sort(Compare);

            for (var i = 0; i < flags.Count; i++)
            {
                var flag = flags[i];
                var siteId = FactionFlagService.SiteIdForCoreFlag(flag.FlagId);
                if (!flag.HasWorldPosition || string.IsNullOrWhiteSpace(flag.SurfaceId) ||
                    !IsFinite(flag.WorldX) || !IsFinite(flag.WorldY) ||
                    flag.AuthoredCoreLevel < 1 || string.IsNullOrWhiteSpace(siteId))
                    return Result.Failure(errorCode,
                        "Authored FactionFlag Site-Core metadata is invalid.", flag.FlagId);

                ResolvedWorldSpatialRange range;
                try
                {
                    range = world.Strategic.SpatialRules.ResolveLevel(
                        world, flag.AuthoredCoreLevel, flag.SurfaceId);
                }
                catch (Exception ex)
                {
                    return Result.Failure(errorCode,
                        "Authored FactionFlag Site-Core range cannot be resolved.",
                        flag.FlagId + ": " + ex.Message);
                }

                if (world.Strategic.Sites.TryGet(siteId, out var existing) && existing != null)
                {
                    var matches = existing.IsRuntimeCreated && existing.HasContinuousCore &&
                                  string.Equals(existing.CoreAssetId, flag.FlagId, StringComparison.Ordinal) &&
                                  string.Equals(existing.CoreSurfaceId, flag.SurfaceId, StringComparison.Ordinal) &&
                                  Math.Abs(existing.CoreWorldX - flag.WorldX) <= .001f &&
                                  Math.Abs(existing.CoreWorldY - flag.WorldY) <= .001f &&
                                  existing.CoreLevel == flag.AuthoredCoreLevel;
                    if (!matches)
                        return Result.Failure(errorCode,
                            "Authored FactionFlag collides with a mismatched runtime Site.", flag.FlagId);
                    flag.SiteId = siteId;
                    flag.IsSiteCore = true;
                    flag.FactionId = existing.OwnerFactionId;
                    if (!FactionFlagSiteCoreQuery.TryResolveFlagForSite(world, existing, out var resolved) ||
                        !ReferenceEquals(resolved, flag))
                        return Result.Failure(errorCode,
                            "Authored FactionFlag runtime Site identity is inconsistent.", flag.FlagId);
                    continue;
                }

                if (!allowCreateMissing)
                    return Result.Failure(errorCode,
                        "Authoritative snapshot is missing an authored FactionFlag runtime Site.", flag.FlagId);

                var site = new WorldSite
                {
                    SiteId = siteId,
                    DisplayName = string.IsNullOrWhiteSpace(flag.AuthoredSiteDisplayName)
                        ? "势力旗据点" : flag.AuthoredSiteDisplayName.Trim(),
                    SiteType = string.IsNullOrWhiteSpace(flag.AuthoredSiteType)
                        ? "Outpost" : flag.AuthoredSiteType.Trim(),
                    OwnerFactionId = flag.FactionId,
                    ControlEstablishedOrder = flag.EstablishedOrder,
                    UsesContinuousOutdoorSurface = true,
                    IsRuntimeCreated = true,
                    CoreAssetId = flag.FlagId,
                    CoreSurfaceId = flag.SurfaceId,
                    HasCoreWorldPosition = true,
                    CoreWorldX = flag.WorldX,
                    CoreWorldY = flag.WorldY,
                    CoreLevel = flag.AuthoredCoreLevel,
                    CoreRangeWidth = range.WidthWorld,
                    CoreRangeHeight = range.HeightWorld,
                    IsCoreActive = true,
                    CoreIsRemovable = true,
                    LegacyAnchorHex = flag.AnchorHex,
                    LegacyPresenceHex = flag.AnchorHex,
                    LocalMapId = string.Empty
                };
                site.SetLegacyHexFootprint(new[] { flag.AnchorHex });
                try
                {
                    world.Strategic.Sites.Register(site);
                }
                catch (Exception ex)
                {
                    return Result.Failure(errorCode,
                        "Authored FactionFlag runtime Site registration failed.",
                        flag.FlagId + ": " + ex.Message);
                }
                flag.SiteId = siteId;
                flag.IsSiteCore = true;
                if (!FactionFlagSiteCoreQuery.TryResolveFlagForSite(world, site, out var createdFlag) ||
                    !ReferenceEquals(createdFlag, flag))
                    return Result.Failure(errorCode,
                        "Authored FactionFlag runtime Site identity is inconsistent.", flag.FlagId);
            }
            return Result.Success();
        }

        static int Compare(FactionFlagState a, FactionFlagState b)
        {
            var order = a.EstablishedOrder.CompareTo(b.EstablishedOrder);
            return order != 0 ? order : string.CompareOrdinal(a.FlagId, b.FlagId);
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
