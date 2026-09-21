using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.World
{
    /// <summary>Party focus and legacy Site/Outdoor LocalMap activation helpers.</summary>
    public static class WorldTravelService
    {
        public static void SyncPartyFocus(SimulationWorld world)
        {
            if (world == null)
                return;

            // Domain authority: once PlayerPartyWorldMotion has a position, never let NPC AtSite
            // presence rewrite PartyWorld (that revived departure Site after ordinary Hex TravelComplete).
            var travel = world.PlayerPartyTravel;
            if (travel != null && travel.HasPosition)
                return;

            string bestLivingWithMap = null;
            string bestLiving = null;
            string bestAnyWithMap = null;
            string bestAny = null;

            foreach (var kv in world.WorldPresence.All)
            {
                var p = kv.Value;
                if (p == null)
                    continue;

                var id = new EntityId(kv.Key);
                if (id.IsNone || !world.Entities.TryGet(id, out var ent) || ent == null)
                    continue;
                if ((ent.Tags & EntityTag.Npc) != 0)
                    continue;

                string siteId = null;
                if (p.Mode == PartyWorldPresenceMode.AtSite && !string.IsNullOrEmpty(p.SiteId))
                    siteId = p.SiteId;
                else if (!string.IsNullOrEmpty(p.SiteId) &&
                         world.Strategic.Sites.TryGet(p.SiteId, out var nodeAsSite) &&
                         nodeAsSite != null)
                    siteId = nodeAsSite.SiteId;

                if (string.IsNullOrEmpty(siteId))
                    continue;

                bestAny = siteId;
                var hasMap = world.Strategic.Sites.TryGet(siteId, out var site) &&
                             site != null &&
                             !string.IsNullOrWhiteSpace(ResolveWorldSiteLocalMapId(site));
                if (hasMap)
                    bestAnyWithMap = siteId;

                var living = true;
                if (ent.TryGet<LifecycleComponent>(out var life) && life != null)
                    living = !life.IsIncapacitated && !life.IsDead && !life.IsRemoved;
                if (!living)
                    continue;

                bestLiving = siteId;
                if (hasMap)
                    bestLivingWithMap = siteId;
            }

            var focusSiteId = bestLivingWithMap ?? bestLiving ?? bestAnyWithMap ?? bestAny ??
                              world.PartyWorld.SiteId;
            if (string.IsNullOrEmpty(focusSiteId) ||
                !world.Strategic.Sites.TryGet(focusSiteId, out var focusSite) ||
                focusSite == null)
                return;

            world.PartyWorld.SiteId = focusSiteId;
            world.PartyWorld.LocalMapId = ResolveWorldSiteLocalMapId(focusSite);
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtSite;
        }

        public static void ApplyLocalMapSessionFromFocus(SimulationWorld world)
        {
            LoadedDestinationArrivalMaterializer.ReleaseEligibleOccupantsOnLocalMapUnload(world, null);
            var presence = world.PartyWorld;
            world.LocalMap.ClearOccupants();
            world.LocalMap.ReturnLocationId = string.Empty;
            if (string.IsNullOrWhiteSpace(presence.LocalMapId))
            {
                world.LocalMap.ActiveMapLayoutId = string.Empty;
                world.LocalMap.OverworldMapLayoutId = string.Empty;
            }
            else
            {
                world.LocalMap.ActiveMapLayoutId = presence.LocalMapId;
                world.LocalMap.OverworldMapLayoutId = presence.LocalMapId;
            }
        }

        /// <summary>Legacy WorldSite LocalMap compatibility activation.</summary>
        public static Result EnterWorldSiteScene(
            SimulationWorld world,
            string siteId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");

            var access = StrategicWorldSiteAccessService.CanEnterWorldSiteLocalMap(
                world, siteId);
            if (access.IsFailure)
                return access;

            if (!world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
                return Result.Failure(ErrorCode.NotFound, "WorldSite missing.", siteId);

            var localMapId = ResolveWorldSiteLocalMapId(site);
            if (string.IsNullOrWhiteSpace(localMapId))
            {
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "WorldSite \u672a\u914d\u7f6e LocalMap\uff0c\u65e0\u6cd5\u8fdb\u5165\u3002",
                    siteId);
            }

            world.PartyWorld.ClearSiteFocus();
            world.PartyWorld.SiteId = siteId;
            world.PartyWorld.LocalMapId = localMapId;
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtSite;
            world.PartyWorld.EncounterId = string.Empty;
            ApplyLocalMapSessionFromFocus(world);
            return Result.Success();
        }

        /// <summary>
        /// PlayerParty transition PREPARE verified this Site already. This method only performs
        /// deterministic PartyWorld and LocalMap activation; it never re-checks presence access.
        /// </summary>
        public static Result ActivatePreparedWorldSiteScene(
            SimulationWorld world,
            WorldSite site,
            string preparedLocalMapId)
        {
            if (world == null || site == null || string.IsNullOrWhiteSpace(preparedLocalMapId))
                return Result.Failure(ErrorCode.InvalidArgument, "Prepared WorldSite scene args invalid.");
            world.PartyWorld.ClearSiteFocus();
            world.PartyWorld.SiteId = site.SiteId;
            world.PartyWorld.LocalMapId = preparedLocalMapId.Trim();
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtSite;
            world.PartyWorld.EncounterId = string.Empty;
            ApplyLocalMapSessionFromFocus(world);
            return Result.Success();
        }
        public static string ResolveWorldSiteLocalMapId(WorldSite site)
        {
            if (site == null || string.IsNullOrWhiteSpace(site.LocalMapId))
                return string.Empty;
            return site.LocalMapId.Trim();
        }

        /// <summary>
        /// Compatibility-only Outdoor LocalMap activation for old Hex content and saves.
        /// Normal Continuous Outdoor must retain SurfaceId + exact WorldPosition authority.
        /// </summary>
        public static Result EnterLegacyWildernessLocalMap(
            SimulationWorld world,
            HexCoord wildernessHex,
            string localMapId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (string.IsNullOrWhiteSpace(localMapId))
                return Result.Failure(ErrorCode.InvalidArgument, "Wilderness LocalMapId required.");
            if (!world.HexWorld.HasGrid || !world.HexWorld.Contains(wildernessHex))
                return Result.Failure(ErrorCode.InvalidArgument, "Wilderness hex out of bounds.");

            world.PartyWorld.ClearSiteFocus();
            world.PartyWorld.SiteId = string.Empty;
            world.PartyWorld.LocalMapId = localMapId.Trim();
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtHex;
            world.PartyWorld.EncounterId = string.Empty;
            ApplyLocalMapSessionFromFocus(world);
            return Result.Success();
        }

    }
}
