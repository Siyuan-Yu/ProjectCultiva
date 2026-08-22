using System;
using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 残留战场再入队伍收集与锚点解析（Core 真源；Host 只负责点击与菜单）。
    /// </summary>
    public static class LingeringBattlefieldPartyService
    {
        public static bool IsIncapacitated(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone)
                return false;
            if (!world.Entities.TryGet(id, out var ent) || ent == null)
                return false;
            return ent.TryGet<LifecycleComponent>(out var life) && life.IsIncapacitated;
        }

        /// <summary>可见尸体（未腐烂）：与弥留同属「倒下可交互」——可选中／进残留，不可下令。</summary>
        public static bool IsVisibleCorpse(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone)
                return false;
            if (!world.Entities.TryGet(id, out var ent) || ent == null)
                return false;
            return CombatLifeStateService.HasVisibleCorpse(ent);
        }

        /// <summary>弥留或可见尸体：残留战场交互（点选／右键进入／探望）同一套。</summary>
        public static bool IsLingeringDowned(SimulationWorld world, EntityId id) =>
            IsIncapacitated(world, id) || IsVisibleCorpse(world, id);

        public static bool IsLivingForMacroOrder(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone || !world.Entities.TryGet(id, out var ent) || ent == null)
                return false;
            if (!ent.TryGet<LifecycleComponent>(out var life) || life == null)
                return true;
            return !life.IsIncapacitated && !life.IsDead && !life.IsRemoved;
        }

        /// <summary>
        /// 残留战场再入：半径内我方弥留／尸体 + 当前行动决定人（mandatoryLiving）强制纳入；
        /// 其余支援半径内活人由接战窗 Optional 名单勾选，不在此强制。
        /// </summary>
        public static bool CollectViewParty(
            SimulationWorld world,
            IReadOnlyList<EntityId> roster,
            EntityId focusIncap,
            List<EntityId> into,
            IReadOnlyList<EntityId> mandatoryLiving = null)
        {
            into.Clear();
            if (world?.Strategic == null || roster == null || into == null)
                return false;
            if (!BattleOfferService.HasLingeringBattlefield(world))
                return false;

            if (!TryResolveBattleAnchor(world, focusIncap, out var anchorNode, out var anchorRoute, out var anchorProgress))
            {
                if (!focusIncap.IsNone && IsLingeringDowned(world, focusIncap))
                {
                    into.Add(focusIncap);
                    return true;
                }

                return false;
            }

            AppendMandatoryLivingInRange(
                world, mandatoryLiving, anchorNode, anchorRoute, anchorProgress, into);
            // 支援范围内我方弥留／可见尸体：全部强制进场
            AppendIncapacitatedInRange(world, roster, anchorNode, anchorRoute, anchorProgress, into);
            EnsureFocusIncapInParty(world, focusIncap, into);

            return into.Count > 0;
        }

        static void AppendMandatoryLivingInRange(
            SimulationWorld world,
            IReadOnlyList<EntityId> mandatoryLiving,
            string anchorNode,
            string anchorRoute,
            float anchorProgress,
            List<EntityId> into)
        {
            if (world == null || mandatoryLiving == null || into == null)
                return;
            for (var i = 0; i < mandatoryLiving.Count; i++)
            {
                var id = mandatoryLiving[i];
                if (id.IsNone || !IsLivingForMacroOrder(world, id))
                    continue;
                if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    continue;
                if (!ReinforcementRangeService.IsWithinReinforcementRange(
                        world, wp, anchorNode, anchorRoute, anchorProgress))
                    continue;
                for (var j = 0; j < into.Count; j++)
                {
                    if (into[j] == id)
                        goto nextMandatory;
                }

                into.Add(id);
                nextMandatory: ;
            }
        }

        static void AppendIncapacitatedInRange(
            SimulationWorld world,
            IReadOnlyList<EntityId> roster,
            string anchorNode,
            string anchorRoute,
            float anchorProgress,
            List<EntityId> into)
        {
            if (world == null || roster == null || into == null)
                return;
            for (var i = 0; i < roster.Count; i++)
            {
                var id = roster[i];
                if (id.IsNone || !IsLingeringDowned(world, id))
                    continue;
                if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    continue;
                if (!ReinforcementRangeService.IsWithinReinforcementRange(
                        world, wp, anchorNode, anchorRoute, anchorProgress))
                    continue;
                var exists = false;
                for (var j = 0; j < into.Count; j++)
                {
                    if (into[j] == id)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    into.Add(id);
            }
        }

        static void EnsureFocusIncapInParty(
            SimulationWorld world,
            EntityId focusIncap,
            List<EntityId> into)
        {
            if (into == null || focusIncap.IsNone || !IsLingeringDowned(world, focusIncap))
                return;
            for (var i = 0; i < into.Count; i++)
            {
                if (into[i] == focusIncap)
                    return;
            }

            // 用户从该倒下头像进入：本人始终纳入进场名单
            into.Add(focusIncap);
        }

        public static bool CanEnterLingeringBattlefield(
            SimulationWorld world,
            IReadOnlyList<EntityId> roster,
            EntityId focusIncap,
            List<EntityId> scratch,
            IReadOnlyList<EntityId> mandatoryLiving = null)
        {
            if (scratch == null)
                return false;
            return CollectViewParty(world, roster, focusIncap, scratch, mandatoryLiving) &&
                   scratch.Count > 0;
        }

        public static bool TryResolveBattleAnchor(
            SimulationWorld world,
            EntityId focusIncap,
            out string anchorNode,
            out string anchorRoute,
            out float anchorProgress)
        {
            anchorNode = string.Empty;
            anchorRoute = string.Empty;
            anchorProgress = -1f;

            if (TryResolveBattleAnchorFromParticipants(world, out anchorNode, out anchorRoute, out anchorProgress))
                return true;

            if (focusIncap.IsNone ||
                !world.WorldPresence.TryGet(focusIncap, out var wp) ||
                wp == null)
                return false;

            return TryResolveBattleAnchorFromPresence(wp, out anchorNode, out anchorRoute, out anchorProgress);
        }

        static bool TryResolveBattleAnchorFromParticipants(
            SimulationWorld world,
            out string anchorNode,
            out string anchorRoute,
            out float anchorProgress)
        {
            anchorNode = string.Empty;
            anchorRoute = string.Empty;
            anchorProgress = -1f;

            var snap = world?.Strategic?.Participants;
            if (snap == null ||
                (string.IsNullOrEmpty(snap.BattleAnchorNodeId) &&
                 string.IsNullOrEmpty(snap.BattleAnchorRouteId)))
                return false;

            anchorNode = snap.BattleAnchorNodeId ?? string.Empty;
            anchorRoute = snap.BattleAnchorRouteId ?? string.Empty;
            anchorProgress = snap.BattleAnchorProgress;
            return true;
        }

        static bool TryResolveBattleAnchorFromPresence(
            WorldAgentPresence wp,
            out string anchorNode,
            out string anchorRoute,
            out float anchorProgress)
        {
            anchorNode = string.Empty;
            anchorRoute = string.Empty;
            anchorProgress = -1f;
            if (wp == null)
                return false;

            if (wp.HasRoutePresentation && !string.IsNullOrEmpty(wp.RouteId))
            {
                anchorNode = wp.NodeId ?? string.Empty;
                anchorRoute = wp.RouteId;
                anchorProgress = wp.Mode == PartyWorldPresenceMode.RouteAnchored
                    ? Clamp01(wp.RouteAnchorProgress)
                    : Clamp01(wp.TravelProgress);
                return true;
            }

            if (!string.IsNullOrEmpty(wp.NodeId))
            {
                anchorNode = wp.NodeId;
                return true;
            }

            return false;
        }

        static float Clamp01(float v) => Math.Max(0f, Math.Min(1f, v));
    }
}
