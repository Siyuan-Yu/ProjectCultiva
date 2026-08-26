using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 只读：Character WorldLocation 是否落在当前 Host 已 Loaded 的 LocalMap。
    /// </summary>
    public static class LoadedLocalMapBelongingQuery
    {
        public enum LoadedLocalMapKind
        {
            None = 0,
            WorldSite = 1,
            WildernessHex = 2,
        }

        public readonly struct LoadedLocalMapContext
        {
            public LoadedLocalMapContext(
                LoadedLocalMapKind kind,
                string activeMapLayoutId,
                WorldSite site,
                HexCoord wildernessHex)
            {
                Kind = kind;
                ActiveMapLayoutId = activeMapLayoutId ?? string.Empty;
                Site = site;
                WildernessHex = wildernessHex;
            }

            public LoadedLocalMapKind Kind { get; }
            public string ActiveMapLayoutId { get; }
            public WorldSite Site { get; }
            public HexCoord WildernessHex { get; }
        }

        public static bool TryResolveLoadedLocalMap(
            SimulationWorld world,
            out LoadedLocalMapContext context)
        {
            context = default;
            if (world?.LocalMap == null || world.PartyWorld == null)
                return false;

            var activeMap = world.LocalMap.ActiveMapLayoutId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(activeMap) || world.LocalMap.IsInInterior)
                return false;

            if (StrategicWorldSitePopulationService.TryResolvePartyFocusSite(world, out var focusSite) &&
                focusSite != null)
            {
                var siteMap = WorldTravelService.ResolveWorldSiteLocalMapId(focusSite);
                if (!string.IsNullOrEmpty(siteMap) &&
                    string.Equals(activeMap, siteMap, System.StringComparison.Ordinal) &&
                    string.Equals(
                        world.PartyWorld.SiteId,
                        focusSite.SiteId,
                        System.StringComparison.Ordinal) &&
                    !PlayerPartyLocalMapMaterializationService.IsWildernessLocalExpand(world))
                {
                    context = new LoadedLocalMapContext(
                        LoadedLocalMapKind.WorldSite,
                        activeMap,
                        focusSite,
                        default);
                    return true;
                }
            }

            if (!PlayerPartyLocalMapMaterializationService.IsWildernessLocalExpand(world))
                return false;

            var focusMap = world.PartyWorld.LocalMapId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(focusMap) ||
                !string.Equals(activeMap, focusMap, System.StringComparison.Ordinal))
                return false;

            var motion = world.PlayerPartyTravel;
            if (motion == null || !motion.HasPosition)
                return false;

            context = new LoadedLocalMapContext(
                LoadedLocalMapKind.WildernessHex,
                activeMap,
                null,
                motion.CurrentHex);
            return true;
        }

        public static bool DoesWorldLocationBelongToLoadedLocalMap(
            SimulationWorld world,
            EntityId characterId,
            out LoadedLocalMapContext loadedContext)
        {
            loadedContext = default;
            if (world == null || characterId.IsNone)
                return false;
            if (!TryResolveLoadedLocalMap(world, out loadedContext))
                return false;
            if (!world.WorldPresence.TryGet(characterId, out var presence) || presence == null)
                return false;

            switch (loadedContext.Kind)
            {
                case LoadedLocalMapKind.WorldSite:
                    return presence.Mode == PartyWorldPresenceMode.AtSite &&
                           !string.IsNullOrEmpty(presence.SiteId) &&
                           loadedContext.Site != null &&
                           string.Equals(
                               presence.SiteId,
                               loadedContext.Site.SiteId,
                               System.StringComparison.Ordinal);

                case LoadedLocalMapKind.WildernessHex:
                    if (presence.Mode != PartyWorldPresenceMode.AtWorldPosition ||
                        !presence.HasContinuousWorldPosition)
                        return false;

                    var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
                    var derived = HexMath.WorldToHex(
                        presence.WorldPosX,
                        presence.WorldPosY,
                        hexSize);
                    return derived.Equals(loadedContext.WildernessHex);

                default:
                    return false;
            }
        }
    }
}
