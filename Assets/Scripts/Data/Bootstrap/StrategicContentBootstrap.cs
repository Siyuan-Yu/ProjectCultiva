using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>Playable startup from the authored continuous Surface.</summary>
    public static class StrategicContentBootstrap
    {
        public static Result ApplyCh01Defaults(
            SimulationWorld world,
            DefinitionRegistry registry,
            OpeningScenarioDefinition scenario,
            GameStartLookup openingLookup = null)
        {
            world.Strategic.Ch01FormationScenarioCompat = true;
            Ch01ScenarioProgressionHooks.Register(world);
            var opening = StrategicOpeningContentBootstrap.Apply(world, registry, scenario);
            if (opening.IsFailure)
                return opening;
            var surface = ApplySurfaceSites(world, registry, scenario);
            if (surface.IsFailure)
                return surface;
            var armies = FormalArmyContentBootstrap.Apply(world, registry, scenario, openingLookup);
            if (armies.IsFailure)
                return armies;
            return Result.Success();
        }

        public static Result ApplySurfaceSites(
            SimulationWorld world, DefinitionRegistry registry, OpeningScenarioDefinition scenario)
        {
            if (registry == null || scenario == null ||
                !XianXia.Core.Domain.Ids.DefinitionId.TryParse(scenario.OpeningSurfaceId, out var id) ||
                !registry.TryGetOutdoorSurface(id, out var surface) || surface == null || surface.AcceptanceOnly)
                return Result.Failure(ErrorCode.ContentLoadFailed,
                    "Opening Surface definition missing.", scenario?.OpeningSurfaceId ?? string.Empty);
            world.Strategic.Sites.Clear();
            world.Strategic.TerritoryRegions.Clear();
            world.Strategic.FactionFlags.Clear();
            TerritoryClaimService.ResetForContentBootstrap(world);
            if (surface.SiteRegions == null || surface.SiteRegions.Count == 0)
                return Result.Failure(ErrorCode.ContentLoadFailed, "Opening Surface has no SiteRegions.");
            foreach (var region in surface.SiteRegions)
            {
                if (region == null || string.IsNullOrWhiteSpace(region.SiteId) ||
                    !string.Equals(region.SurfaceId, surface.SurfaceId, System.StringComparison.Ordinal))
                    return Result.Failure(ErrorCode.ContentLoadFailed, "Invalid opening Surface SiteRegion.");
                var site = new WorldSite
                {
                    SiteId = region.SiteId,
                    DisplayName = string.IsNullOrWhiteSpace(region.DisplayName) ? region.SiteId : region.DisplayName,
                    SiteType = string.IsNullOrWhiteSpace(region.SiteType) ? "Site" : region.SiteType,
                    OwnerFactionId = region.OwnerFactionId ?? string.Empty,
                    TerritoryRegionId = region.TerritoryRegionId ?? string.Empty,
                    UsesContinuousOutdoorSurface = true
                };
                world.Strategic.Sites.Register(site);
                if (!string.IsNullOrWhiteSpace(region.TerritoryRegionId))
                    world.Strategic.TerritoryRegions.Register(new TerritoryRegion
                    {
                        RegionId = region.TerritoryRegionId,
                        PrimaryWorldSiteId = region.SiteId,
                        ControlFactionId = region.OwnerFactionId ?? string.Empty
                    });
            }
            if (surface.FactionFlags != null)
                foreach (var flag in surface.FactionFlags)
                {
                    if (flag == null || string.IsNullOrWhiteSpace(flag.FlagId) ||
                        string.IsNullOrWhiteSpace(flag.FactionId) ||
                        !world.Strategic.FactionFlags.Register(new FactionFlagState
                        {
                            FlagId = flag.FlagId,
                            FactionId = flag.FactionId,
                            EstablishedOrder = flag.EstablishedOrder,
                            HasWorldPosition = true,
                            SurfaceId = surface.SurfaceId,
                            WorldX = flag.WorldX,
                            WorldY = flag.WorldY,
                            IsAuthoredSiteCore = flag.CreatesWorldSite,
                            AuthoredSiteDisplayName = flag.SiteDisplayName ?? string.Empty,
                            AuthoredSiteType = flag.SiteType ?? string.Empty,
                            AuthoredCoreLevel = flag.CoreLevel
                        }))
                        return Result.Failure(ErrorCode.ContentLoadFailed,
                            "Duplicate or invalid Surface FactionFlag.", flag?.FlagId ?? string.Empty);
                }
            return Result.Success();
        }
    }
}
