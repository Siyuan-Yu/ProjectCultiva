using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Belonging 只读解释（Trace 专用；不改变判定逻辑）。
    /// </summary>
    public static class LoadedLocalMapBelongingExplain
    {
        public static void ExplainTryResolveLoadedLocalMap(
            SimulationWorld world,
            out bool resolved,
            out string reason)
        {
            resolved = LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap(world, out var ctx);
            if (!resolved)
            {
                reason = BuildResolveFailureReason(world);
                return;
            }

            reason = "LoadedMapKind=" + ctx.Kind +
                     " ActiveMapLayoutId=" + ctx.ActiveMapLayoutId +
                     " LoadedWildernessHex=" + ctx.WildernessHex +
                     " LoadedSiteId=" + (ctx.Site?.SiteId ?? string.Empty) +
                     " PartySiteId=" + (world.PartyWorld?.SiteId ?? string.Empty) +
                     " PartyLocalMapId=" + (world.PartyWorld?.LocalMapId ?? string.Empty) +
                     " IsWildernessLocalExpand=" +
                     PlayerPartyLocalMapMaterializationService.IsWildernessLocalExpand(world);
        }

        public static void ExplainDoesBelong(
            SimulationWorld world,
            EntityId characterId,
            HexCoord enteredHexForNotify,
            out bool belongs,
            out string reason)
        {
            belongs = LoadedLocalMapBelongingQuery.DoesWorldLocationBelongToLoadedLocalMap(
                world,
                characterId,
                out var ctx);
            if (!LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap(world, out var loaded))
            {
                reason = "TryResolveLoadedLocalMap=false;" + BuildResolveFailureReason(world);
                return;
            }

            if (!world.WorldPresence.TryGet(characterId, out var presence) || presence == null)
            {
                reason = "CharacterPresenceMissing";
                return;
            }

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var derived = presence.HasContinuousWorldPosition
                ? HexMath.WorldToHex(presence.WorldPosX, presence.WorldPosY, hexSize)
                : default;

            reason = "LoadedMapKind=" + loaded.Kind +
                     " LoadedWildernessHex=" + loaded.WildernessHex +
                     " EnteredHexForNotify=" + enteredHexForNotify +
                     " CharacterMode=" + presence.Mode +
                     " CharacterDerivedHex=" + derived +
                     " PartyCurrentHex=" + (world.PlayerPartyTravel?.HasPosition == true
                         ? world.PlayerPartyTravel.CurrentHex.ToString()
                         : "None") +
                     " NotifyEnteredEqualsLoaded=" + enteredHexForNotify.Equals(loaded.WildernessHex) +
                     " BelongsToLoadedMap=" + belongs +
                     " BelongingKind=" + ctx.Kind;
        }

        static string BuildResolveFailureReason(SimulationWorld world)
        {
            if (world?.LocalMap == null)
                return "LocalMapNull";
            if (world.PartyWorld == null)
                return "PartyWorldNull";
            if (string.IsNullOrEmpty(world.LocalMap.ActiveMapLayoutId))
                return "ActiveMapLayoutIdEmpty";
            if (world.LocalMap.IsInInterior)
                return "IsInInterior";
            if (!PlayerPartyLocalMapMaterializationService.IsWildernessLocalExpand(world))
            {
                return "NotWildernessLocalExpand" +
                       " PartySiteId=" + (world.PartyWorld.SiteId ?? string.Empty) +
                       " PartyLocalMapId=" + (world.PartyWorld.LocalMapId ?? string.Empty);
            }

            if (string.IsNullOrEmpty(world.PartyWorld.LocalMapId))
                return "PartyLocalMapIdEmpty";
            if (!string.Equals(
                    world.LocalMap.ActiveMapLayoutId.Trim(),
                    world.PartyWorld.LocalMapId.Trim(),
                    System.StringComparison.Ordinal))
                return "ActiveMapMismatchPartyLocalMapId";
            if (world.PlayerPartyTravel == null || !world.PlayerPartyTravel.HasPosition)
                return "PlayerPartyTravelMissingPosition";
            return "UnknownResolveFailure";
        }
    }
}
