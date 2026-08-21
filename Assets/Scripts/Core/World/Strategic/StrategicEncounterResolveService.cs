using System;
using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 结束 Modal／结算：参战者落在 BattleAnchor（禁止瞬移回家）；
    /// 场上仍有弥留则保留遭遇战场，否则销毁。
    /// </summary>
    public static class StrategicEncounterResolveService
    {
        public static Result ResolveAndEnd(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");

            var snap = world.Strategic.Participants;
            RestoreParticipantsAfterBattle(world, snap);

            var linger = HasLingeringIncapacitated(world);
            if (linger)
            {
                ParkLingeringBattlefield(world, snap);
                world.Strategic.ClearBattleOffer();
                if (snap != null)
                    snap.IsAutoSettlement = false;
                WorldTravelService.SyncPartyFocus(world);
                BattleOfferService.FinishOfferResolution(world);
                return Result.Success();
            }

            DestroyBattlefieldCompletely(world);
            world.Strategic.ClearBattleOffer();
            WorldTravelService.SyncPartyFocus(world);
            BattleOfferService.FinishOfferResolution(world);
            return Result.Success();
        }

        /// <summary>场上已无弥留时销毁残留战场（补刀／清场后调用）。</summary>
        public static Result TryDestroyIfNoIncapacitated(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return Result.Failure(ErrorCode.InvalidArgument, "null");
            if (HasLingeringIncapacitated(world))
                return Result.Success();
            DestroyBattlefieldCompletely(world);
            WorldTravelService.SyncPartyFocus(world);
            return Result.Success();
        }

        public static bool HasLingeringIncapacitated(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return false;

            var snap = world.Strategic.Participants;
            if (snap != null)
            {
                for (var i = 0; i < snap.Records.Count; i++)
                {
                    var rec = snap.Records[i];
                    if (rec.EntityId.IsNone)
                        continue;
                    if (!world.Entities.TryGet(rec.EntityId, out var ent) || ent == null)
                        continue;
                    if (ent.TryGet<LifecycleComponent>(out var life) && life.IsIncapacitated)
                        return true;
                }
            }

            var rt = world.Strategic.Encounter;
            if (rt != null)
            {
                for (var i = 0; i < rt.SpawnedEntityIds.Count; i++)
                {
                    var id = new EntityId(rt.SpawnedEntityIds[i]);
                    if (!world.Entities.TryGet(id, out var ent) || ent == null)
                        continue;
                    if (ent.TryGet<LifecycleComponent>(out var life) && life.IsIncapacitated)
                        return true;
                }
            }

            if (rt != null &&
                !string.IsNullOrEmpty(rt.ArmyStackId) &&
                world.Strategic.Armies.TryGet(rt.ArmyStackId, out var stack) &&
                stack != null &&
                stack.HasIncapacitatedRemnant)
                return true;

            // 自动战后尚未绑到 Encounter.ArmyStackId 时，看快照主敌栈
            var primary = world.Strategic.Participants?.PrimaryEnemyStackId;
            if (!string.IsNullOrEmpty(primary) &&
                world.Strategic.Armies.TryGet(primary, out var primaryStack) &&
                primaryStack != null &&
                primaryStack.HasIncapacitatedRemnant)
                return true;

            return false;
        }

        public static void EnterPostBattleIfCleared(SimulationWorld world) =>
            TryEnterPostBattleFromManual(world);

        /// <summary>敌军清空或我方全倒 → PostBattle（仍冻结，可点结束战斗）。</summary>
        public static void TryEnterPostBattleFromManual(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;
            if (world.Strategic.ClockFreeze.Reason != StrategicClockFreezeReason.ManualEncounter)
                return;

            var fieldCleared = StrategicEncounterSpawner.IsFieldCleared(world);
            var friendliesDown = AreAllEngagedFriendliesDown(world);
            if (!fieldCleared && !friendliesDown)
                return;

            StrategicClockFreezeService.BeginOrPromote(
                world, StrategicClockFreezeReason.PostBattle);

            if (string.IsNullOrEmpty(world.Strategic.Participants.LastBattleSummary))
            {
                world.Strategic.Participants.LastBattleSummary = fieldCleared
                    ? "敌军已清空。可继续补刀／交互；点「结束战斗」退出 Modal（有弥留则战场仍留在接战点）。"
                    : "我方已全部倒下。点「结束战斗」退出；弥留者仍留在接战点，可再派人查看。";
            }

            world.Strategic.Participants.PlayerWon = fieldCleared;
        }

        public static bool AreAllEngagedFriendliesDown(SimulationWorld world)
        {
            var rt = world?.Strategic?.Encounter;
            if (rt == null || !rt.HasEngagedParty)
                return false;
            var any = false;
            for (var i = 0; i < rt.EngagedPartyIds.Count; i++)
            {
                var id = new EntityId(rt.EngagedPartyIds[i]);
                if (id.IsNone || !world.Entities.TryGet(id, out var ent) || ent == null)
                    continue;
                any = true;
                if (CombatLifeStateService.CanFight(ent))
                    return false;
            }

            return any;
        }

        /// <summary>
        /// 参战／勾选支援者一律落到 BattleAnchor；禁止 Apply PreBattle 瞬移回家。
        /// 未参战、未勾选者不改位置。
        /// </summary>
        public static void RestoreParticipantsAfterBattle(
            SimulationWorld world,
            BattleParticipantSnapshot snap)
        {
            if (world == null || snap == null)
                return;

            for (var i = 0; i < snap.Records.Count; i++)
            {
                var rec = snap.Records[i];
                if (rec.EntityId.IsNone)
                    continue;
                if (rec.Kind != BattleParticipantKind.MandatoryFriendly &&
                    !(rec.Kind == BattleParticipantKind.OptionalFriendly && rec.Selected))
                    continue;
                if (!world.WorldPresence.TryGet(rec.EntityId, out var wp) || wp == null)
                    continue;
                if (!world.Entities.TryGet(rec.EntityId, out var ent) || ent == null)
                    continue;

                // 强制参战、已上场／已 Engaged 的支援 → BattleAnchor；
                // 仅勾选、未上场的远处支援 → 留 PreBattle（禁止瞬移到接战点，也禁止把路上人送回家）
                var mustAnchor =
                    rec.Kind == BattleParticipantKind.MandatoryFriendly ||
                    world.Strategic.Encounter.IsEngaged(rec.EntityId) ||
                    wp.Mode == PartyWorldPresenceMode.InEncounter ||
                    (rec.PreBattle != null &&
                     (rec.PreBattle.Mode == PartyWorldPresenceMode.Traveling ||
                      rec.PreBattle.Mode == PartyWorldPresenceMode.RouteAnchored ||
                      rec.PreBattle.Mode == PartyWorldPresenceMode.InEncounter));

                if (mustAnchor)
                    PlaceAtBattleAnchor(world, wp, snap);
                else if (rec.PreBattle != null)
                    rec.PreBattle.ApplyTo(wp);

                wp.ClearFollow();
                wp.ClearCombatPursuit();
            }

            var engaged = new List<EntityId>(CollectEngaged(world));
            for (var i = 0; i < engaged.Count; i++)
            {
                var id = engaged[i];
                if (snap.FindByEntity(id) != null)
                    continue;
                if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    continue;
                PlaceAtBattleAnchor(world, wp, snap);
                wp.ClearFollow();
                wp.ClearCombatPursuit();
            }
        }

        static void ParkLingeringBattlefield(SimulationWorld world, BattleParticipantSnapshot snap)
        {
            var rt = world.Strategic.Encounter;
            rt.BattlefieldLingering = true;
            rt.FieldCleared = true;
            rt.SpawnOnNextMapLoad = false;
            if (snap != null && !string.IsNullOrEmpty(snap.EncounterLocalMapId))
                rt.LingeringLocalMapId = snap.EncounterLocalMapId;
            else if (string.IsNullOrEmpty(rt.LingeringLocalMapId))
                rt.LingeringLocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;

            // 自动战未进过 LocalMap：把主敌栈绑到 Encounter，否则再进／残留丢失
            if (string.IsNullOrEmpty(rt.ArmyStackId) &&
                snap != null &&
                !string.IsNullOrEmpty(snap.PrimaryEnemyStackId))
                rt.ArmyStackId = snap.PrimaryEnemyStackId;

            // 退出 Modal：人不再 InEncounter，但遭遇数据保留
            rt.ClearEngagedParty();
            world.PartyWorld.EncounterId = string.Empty;

            // 钉住敌军栈在接战点，标为战场残留（可再攻击进入）
            if (!string.IsNullOrEmpty(rt.ArmyStackId) &&
                world.Strategic.Armies.TryGet(rt.ArmyStackId, out var stack) &&
                stack != null)
            {
                ParkStackAtBattleAnchor(world, stack, snap);
                var incapSpawns = CountIncapacitatedSpawns(world);
                if (stack.HasIncapacitatedRemnant || incapSpawns > 0)
                {
                    stack.IsBattlefieldRemnant = true;
                    if (stack.IncapacitatedMemberCount <= 0)
                        stack.IncapacitatedMemberCount = Math.Max(1, incapSpawns);
                    if (stack.MemberCount < stack.IncapacitatedMemberCount)
                        stack.MemberCount = stack.IncapacitatedMemberCount;
                }
            }

            // 给弥留敌军补 WorldPresence，大地图能画头像
            EnsureEnemyIncapWorldPresence(world, snap);

            // 卸掉 ActiveMap 遭遇会话标记：LocalMap 切回焦点节点图由 Host 处理
            if (!string.IsNullOrEmpty(world.PartyWorld.NodeId) &&
                world.WorldGraph.TryGetNode(world.PartyWorld.NodeId, out var focus) &&
                focus != null &&
                !string.IsNullOrEmpty(focus.LocalMapId))
                world.PartyWorld.LocalMapId = focus.LocalMapId;
        }

        static void DestroyBattlefieldCompletely(SimulationWorld world)
        {
            var rt = world.Strategic.Encounter;
            StrategicEncounterSpawner.ClearSpawned(world);
            if (!string.IsNullOrEmpty(rt.ArmyStackId) &&
                world.Strategic.Armies.TryGet(rt.ArmyStackId, out var stack) &&
                stack != null &&
                stack.IsBattlefieldRemnant)
                world.Strategic.Armies.Remove(stack.Id);

            rt.ClearEngagedParty();
            rt.FieldCleared = false;
            rt.BattlefieldLingering = false;
            rt.ArmyStackId = string.Empty;
            rt.EncounterLinkId = string.Empty;
            rt.SpawnOnNextMapLoad = false;
            rt.LingeringLocalMapId = string.Empty;
            world.PartyWorld.EncounterId = string.Empty;
            world.Strategic.Participants.Clear();
        }

        static void ParkStackAtBattleAnchor(
            SimulationWorld world,
            ArmyStack stack,
            BattleParticipantSnapshot snap)
        {
            if (stack == null || snap == null)
                return;
            stack.RemainingTravelTicks = 0;
            stack.TravelTotalTicks = 0;
            if (!string.IsNullOrEmpty(snap.BattleAnchorRouteId) &&
                snap.BattleAnchorProgress >= 0f)
            {
                stack.RouteId = snap.BattleAnchorRouteId;
                stack.RouteAnchorProgress = snap.BattleAnchorProgress;
                stack.NodeId = snap.BattleAnchorNodeId ?? string.Empty;
                stack.DestNodeId = ResolveAnchorDest(world, snap);
            }
            else
            {
                stack.RouteId = string.Empty;
                stack.RouteAnchorProgress = -1f;
                stack.NodeId = snap.BattleAnchorNodeId ?? stack.NodeId ?? string.Empty;
                stack.DestNodeId = string.Empty;
            }
        }

        static void EnsureEnemyIncapWorldPresence(
            SimulationWorld world,
            BattleParticipantSnapshot snap)
        {
            var rt = world.Strategic.Encounter;
            if (rt == null || snap == null)
                return;
            var slot = 0;
            for (var i = 0; i < rt.SpawnedEntityIds.Count; i++)
            {
                var id = new EntityId(rt.SpawnedEntityIds[i]);
                if (!world.Entities.TryGet(id, out var ent) || ent == null)
                    continue;
                if (!ent.TryGet<LifecycleComponent>(out var life) || !life.IsIncapacitated)
                    continue;

                if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    wp = world.WorldPresence.GetOrCreate(id);

                PlaceAtBattleAnchor(world, wp, snap);
                // 微偏进度避免完全重叠
                if (wp.Mode == PartyWorldPresenceMode.RouteAnchored)
                {
                    var bias = (slot % 5) * 0.008f;
                    wp.RouteAnchorProgress = Clamp01(wp.RouteAnchorProgress + bias);
                    slot++;
                }
            }
        }

        public static void PlaceAtBattleAnchor(
            SimulationWorld world,
            WorldAgentPresence wp,
            BattleParticipantSnapshot snap)
        {
            if (wp == null || snap == null)
                return;
            if (!string.IsNullOrEmpty(snap.BattleAnchorRouteId) &&
                snap.BattleAnchorProgress >= 0f)
            {
                wp.Mode = PartyWorldPresenceMode.RouteAnchored;
                wp.RouteId = snap.BattleAnchorRouteId;
                wp.NodeId = snap.BattleAnchorNodeId ?? string.Empty;
                wp.DestNodeId = ResolveAnchorDest(world, snap);
                // Dest 为空时从路网补齐，否则 HasRoutePresentation=false，改点路上目标会走挂路瞬移分支
                if ((string.IsNullOrEmpty(wp.DestNodeId) ||
                     string.Equals(wp.NodeId, wp.DestNodeId, System.StringComparison.Ordinal)) &&
                    world?.WorldGraph != null &&
                    world.WorldGraph.TryGetRoute(wp.RouteId, out var route) &&
                    route != null)
                {
                    if (string.Equals(route.FromNodeId, wp.NodeId, System.StringComparison.Ordinal))
                        wp.DestNodeId = route.ToNodeId ?? string.Empty;
                    else if (string.Equals(route.ToNodeId, wp.NodeId, System.StringComparison.Ordinal))
                        wp.DestNodeId = route.FromNodeId ?? string.Empty;
                    else
                    {
                        wp.NodeId = route.FromNodeId ?? wp.NodeId;
                        wp.DestNodeId = route.ToNodeId ?? string.Empty;
                    }
                }

                wp.RouteAnchorProgress = Clamp01(snap.BattleAnchorProgress);
                wp.RemainingTravelTicks = 0;
                wp.TravelTotalTicks = 0;
                wp.ClearRouteSegment();
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

        static string ResolveAnchorDest(SimulationWorld world, BattleParticipantSnapshot snap)
        {
            if (!string.IsNullOrEmpty(snap.BattleAnchorDestNodeId))
                return snap.BattleAnchorDestNodeId;
            if (world?.WorldGraph != null &&
                !string.IsNullOrEmpty(snap.BattleAnchorRouteId) &&
                world.WorldGraph.TryGetRoute(snap.BattleAnchorRouteId, out var route) &&
                route != null)
            {
                if (string.Equals(route.FromNodeId, snap.BattleAnchorNodeId, System.StringComparison.Ordinal))
                    return route.ToNodeId ?? string.Empty;
                return route.FromNodeId ?? string.Empty;
            }

            return string.Empty;
        }

        static int CountIncapacitatedSpawns(SimulationWorld world)
        {
            var rt = world?.Strategic?.Encounter;
            if (rt == null)
                return 0;
            var n = 0;
            for (var i = 0; i < rt.SpawnedEntityIds.Count; i++)
            {
                var id = new EntityId(rt.SpawnedEntityIds[i]);
                if (!world.Entities.TryGet(id, out var ent) || ent == null)
                    continue;
                if (ent.TryGet<LifecycleComponent>(out var life) && life.IsIncapacitated)
                    n++;
            }

            return n;
        }

        static List<EntityId> CollectEngaged(SimulationWorld world)
        {
            var list = new List<EntityId>();
            var rt = world.Strategic?.Encounter;
            if (rt == null)
                return list;
            for (var i = 0; i < rt.EngagedPartyIds.Count; i++)
                list.Add(new EntityId(rt.EngagedPartyIds[i]));
            return list;
        }

        static float Clamp01(float v)
        {
            if (v < 0f)
                return 0f;
            if (v > 1f)
                return 1f;
            return v;
        }
    }
}
