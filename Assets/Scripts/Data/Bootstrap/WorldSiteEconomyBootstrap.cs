using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    public static class WorldSiteEconomyBootstrap
    {
        public static Result ApplyNewGame(SimulationWorld world, DefinitionRegistry registry) =>
            ApplyDefaults(world, registry, requireNoSnapshotAuthority: false);

        public static Result ApplyLegacySaveFallback(SimulationWorld world, DefinitionRegistry registry) =>
            ApplyDefaults(world, registry, requireNoSnapshotAuthority: true);

        static Result ApplyDefaults(SimulationWorld world, DefinitionRegistry registry, bool requireNoSnapshotAuthority)
        {
            if (world?.Strategic?.SitePublicStocks == null || registry == null)
                return Result.Failure(ErrorCode.InvalidArgument, "WorldSite economy bootstrap requires world and registry.");
            var board = world.Strategic.SitePublicStocks;
            if (board.DefaultsInitialized || (requireNoSnapshotAuthority && board.HasSnapshotAuthority))
                return Result.Success();
            foreach (var site in world.Strategic.Sites.Sites)
                board.GetOrCreate(site.Key);
            foreach (var pair in registry.WorldSiteEconomies)
            {
                var economy = pair.Value;
                if (economy == null || string.IsNullOrWhiteSpace(economy.SiteId) ||
                    !world.Strategic.Sites.TryGet(economy.SiteId, out _))
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "WorldSite economy target is not present in runtime world.", economy?.SiteId ?? pair.Key.ToString());
                for (var i = 0; i < economy.InitialPublicStock.Count; i++)
                {
                    var entry = economy.InitialPublicStock[i];
                    var set = WorldSitePublicStockService.SetInitial(
                        world, economy.SiteId, entry.ResourceId, entry.Amount);
                    if (set.IsFailure) return set;
                }
            }
            board.MarkDefaultsInitialized();
            return Result.Success();
        }
    }
}
