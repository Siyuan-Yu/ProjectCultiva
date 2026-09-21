using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Legacy travel-fixture focus synchronization; modern Gameplay has no caller.</summary>
    public static class LegacyPartyFocusCompatibility
    {
        /// <summary>
        /// Retained for PlayerPartyContinuousWorldPhase2CTests ARRIVAL_02, ARRIVAL_07 and
        /// ARRIVAL_11. The null and HasPosition guards intentionally preserve the original
        /// no-op behavior once authoritative party motion exists.
        /// </summary>
        public static void SyncPartyFocus(SimulationWorld world)
        {
            if (world == null)
                return;

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
                             !string.IsNullOrWhiteSpace(
                                 WorldTravelService.ResolveWorldSiteLocalMapId(site));
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
            world.PartyWorld.LocalMapId =
                WorldTravelService.ResolveWorldSiteLocalMapId(focusSite);
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtSite;
        }
    }
}
