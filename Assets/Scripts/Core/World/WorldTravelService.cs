using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.World
{
    /// <summary>Hex 战略：LocalMap 入口与队伍焦点；legacy Route/Node 旅行已移除�?/summary>
    public static class WorldTravelService
    {
        public static Result AdvanceTravel(
            SimulationWorld world,
            int ticks = 1,
            List<EntityId> arrivedOut = null)
        {
            arrivedOut?.Clear();
            return Result.Success();
        }

        public static bool CanReceiveTravelOrder(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone || !world.WorldPresence.TryGet(id, out var p) || p == null)
                return false;
            if (world.Entities.TryGet(id, out var ent) &&
                ent.TryGet<LifecycleComponent>(out var life) &&
                (life.IsIncapacitated || life.IsDead || life.IsRemoved))
                return false;
            if (StrategicClockFreezeService.IsModalEncounter(world))
                return false;
            if (p.Mode == PartyWorldPresenceMode.AtSite)
                return true;
            if (p.Mode != PartyWorldPresenceMode.InEncounter)
                return false;
            if (BattleOfferService.HasActiveManualEncounter(world))
                return StrategicEncounterSpawner.IsFieldCleared(world);
            return true;
        }

        /// <summary>
        /// Phase D Legacy Exit：玩家宏观移动令仅通过 FormalArmy 下达�?
        /// </summary>
        public static bool CanReceivePlayerMacroTravelOrder(SimulationWorld world, EntityId id)
        {
            if (!CanReceiveTravelOrder(world, id))
                return false;
            if (!IsPlayerAgent(world, id))
                return true;
            return false;
        }

        static bool IsPlayerAgent(SimulationWorld world, EntityId id)
        {
            if (id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                return false;
            return (entity.Tags & EntityTag.Npc) == 0;
        }

        public static bool BlocksFormalArmyMemberIndependentTravel(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone || !world.WorldPresence.TryGet(id, out var presence) || presence == null)
                return false;
            return BlocksFormalArmyIndependentTravel(world, id, presence);
        }

        static bool BlocksFormalArmyIndependentTravel(
            SimulationWorld world,
            EntityId id,
            WorldAgentPresence presence)
        {
            if (world == null || id.IsNone || presence == null || !IsPlayerAgent(world, id))
                return false;
            if (!ArmyService.TryGetArmyForCharacter(world, id, out var army) || army == null)
                return false;
            if (army.State == FormalArmyState.Moving)
                return true;
            if (presence.IsFollowingStack)
                return false;
            if (presence.IsCombatPursuing)
                return false;
            return true;
        }

        public static void SyncPartyFocus(SimulationWorld world)
        {
            if (world == null)
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
            world.PartyWorld.LocalMapId = BattleOfferService.HasActiveManualEncounter(world)
                ? BattleOfferService.ResolveActiveEncounterLocalMapId(world)
                : ResolveWorldSiteLocalMapId(focusSite);
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtSite;
        }

        public static void ApplyLocalMapSessionFromFocus(SimulationWorld world)
        {
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

        /// <summary>�?WorldSite 进入 LocalMap（真�?= SiteId + FormalArmy 足迹）�?/summary>
        public static Result EnterWorldSiteScene(
            SimulationWorld world,
            string siteId,
            string formalArmyId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");

            var access = StrategicWorldSiteAccessService.CanEnterWorldSiteLocalMap(
                world, siteId, formalArmyId);
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
            world.PartyWorld.FocusFormalArmyId = formalArmyId ?? string.Empty;
            world.PartyWorld.LocalMapId = localMapId;
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
    }
}
