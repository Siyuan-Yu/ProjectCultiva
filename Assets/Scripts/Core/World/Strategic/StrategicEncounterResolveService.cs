using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>PostBattle → EncounterResolved：恢复 Presence、清运行时、出队或解冻。</summary>
    public static class StrategicEncounterResolveService
    {
        public static Result ResolveAndEnd(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");

            var snap = world.Strategic.Participants;
            RestoreParticipantsAfterBattle(world, snap);

            var rt = world.Strategic.Encounter;
            StrategicEncounterSpawner.ClearSpawned(world);
            rt.ClearEngagedParty();
            rt.FieldCleared = false;
            rt.ArmyStackId = string.Empty;
            rt.EncounterLinkId = string.Empty;
            rt.SpawnOnNextMapLoad = false;
            world.PartyWorld.EncounterId = string.Empty;

            world.Strategic.ClearBattleOffer();
            WorldTravelService.SyncPartyFocus(world);

            // 出队下一场或 EndFreeze（Host 再恢复 presentation）
            BattleOfferService.FinishOfferResolution(world);
            return Result.Success();
        }

        public static void EnterPostBattleIfCleared(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;
            if (!StrategicEncounterSpawner.IsFieldCleared(world))
                return;
            if (!StrategicClockFreezeService.IsWorldTickFrozen(world))
                StrategicClockFreezeService.BeginOrPromote(
                    world, StrategicClockFreezeReason.PostBattle);
            else
                StrategicClockFreezeService.BeginOrPromote(
                    world, StrategicClockFreezeReason.PostBattle);

            if (string.IsNullOrEmpty(world.Strategic.Participants.LastBattleSummary))
                world.Strategic.Participants.LastBattleSummary =
                    "敌军已清空。可继续补刀／交互；点「结束战斗」才结算。";
            world.Strategic.Participants.PlayerWon = true;
        }

        public static void RestoreParticipantsAfterBattle(
            SimulationWorld world,
            BattleParticipantSnapshot snap)
        {
            if (world == null || snap == null)
                return;

            for (var i = 0; i < snap.Records.Count; i++)
            {
                var rec = snap.Records[i];
                if (rec.EntityId.IsNone || rec.PreBattle == null)
                    continue;
                if (!world.WorldPresence.TryGet(rec.EntityId, out var wp) || wp == null)
                    continue;
                if (!world.Entities.TryGet(rec.EntityId, out var ent) || ent == null)
                    continue;

                // 死亡／移除：不恢复宏观移动态
                if (ent.TryGet<LifecycleComponent>(out var life) &&
                    (life.IsDead || life.IsRemoved))
                {
                    wp.Mode = PartyWorldPresenceMode.AtNode;
                    wp.ClearFollow();
                    wp.ClearCombatPursuit();
                    continue;
                }

                if (life != null && life.IsIncapacitated)
                {
                    // 弥留：落到 BattleAnchor 节点／路锚，不瞬移回支援出发点
                    PlaceAtBattleAnchor(wp, snap);
                    wp.ClearFollow();
                    wp.ClearCombatPursuit();
                    continue;
                }

                if (rec.Kind == BattleParticipantKind.OptionalFriendly)
                {
                    // 可选支援：必须回 PreBattle（禁止靠参战瞬移）
                    rec.PreBattle.ApplyTo(wp);
                    wp.ClearCombatPursuit();
                    continue;
                }

                if (rec.Kind == BattleParticipantKind.MandatoryFriendly)
                {
                    // Primary：落在 BattleAnchor（或已有路锚快照）
                    if (rec.PreBattle.Mode == PartyWorldPresenceMode.Traveling ||
                        rec.PreBattle.Mode == PartyWorldPresenceMode.RouteAnchored ||
                        !string.IsNullOrEmpty(rec.PreBattle.RouteId))
                    {
                        rec.PreBattle.ApplyTo(wp);
                        if (wp.Mode == PartyWorldPresenceMode.InEncounter)
                            wp.Mode = string.IsNullOrEmpty(wp.RouteId)
                                ? PartyWorldPresenceMode.AtNode
                                : PartyWorldPresenceMode.RouteAnchored;
                    }
                    else
                        PlaceAtBattleAnchor(wp, snap);

                    wp.ClearCombatPursuit();
                }
            }

            // 清理仍挂 InEncounter 但未在快照里的人
            var engaged = new List<EntityId>(rtEngaged(world));
            for (var i = 0; i < engaged.Count; i++)
            {
                var id = engaged[i];
                if (snap.FindByEntity(id) != null)
                    continue;
                StrategicEncounterSpawner.ReleaseEngagedForMacroTravel(world, id);
            }
        }

        static List<EntityId> rtEngaged(SimulationWorld world)
        {
            var list = new List<EntityId>();
            var rt = world.Strategic?.Encounter;
            if (rt == null)
                return list;
            for (var i = 0; i < rt.EngagedPartyIds.Count; i++)
                list.Add(new EntityId(rt.EngagedPartyIds[i]));
            return list;
        }

        static void PlaceAtBattleAnchor(WorldAgentPresence wp, BattleParticipantSnapshot snap)
        {
            if (wp == null || snap == null)
                return;
            if (!string.IsNullOrEmpty(snap.BattleAnchorRouteId) &&
                snap.BattleAnchorProgress >= 0f)
            {
                wp.Mode = PartyWorldPresenceMode.RouteAnchored;
                wp.RouteId = snap.BattleAnchorRouteId;
                wp.NodeId = snap.BattleAnchorNodeId ?? string.Empty;
                wp.DestNodeId = string.Empty;
                wp.RouteAnchorProgress = snap.BattleAnchorProgress;
                wp.RemainingTravelTicks = 0;
                wp.TravelTotalTicks = 0;
                wp.ClearRouteSegment();
                // Dest from route
                return;
            }

            wp.Mode = PartyWorldPresenceMode.AtNode;
            wp.NodeId = snap.BattleAnchorNodeId ?? wp.NodeId ?? string.Empty;
            wp.RouteId = string.Empty;
            wp.DestNodeId = string.Empty;
            wp.RouteAnchorProgress = -1f;
            wp.RemainingTravelTicks = 0;
            wp.TravelTotalTicks = 0;
            wp.ClearRouteSegment();
        }
    }
}
