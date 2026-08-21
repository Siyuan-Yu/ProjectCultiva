using System;
using System.Collections.Generic;
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

        public static bool IsLivingForMacroOrder(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone || !world.Entities.TryGet(id, out var ent) || ent == null)
                return false;
            if (!ent.TryGet<LifecycleComponent>(out var life) || life == null)
                return true;
            return !life.IsIncapacitated && !life.IsDead && !life.IsRemoved;
        }

        /// <summary>
        /// 残留战场再入：优先接战锚点半径内存活者；若无则锚点上弥留 solo。
        /// </summary>
        public static bool CollectViewParty(
            SimulationWorld world,
            IReadOnlyList<EntityId> roster,
            EntityId focusIncap,
            List<EntityId> into)
        {
            into.Clear();
            if (world?.Strategic == null || roster == null || into == null)
                return false;
            if (!BattleOfferService.HasLingeringBattlefield(world))
                return false;

            if (!TryResolveBattleAnchor(world, focusIncap, out var anchorNode, out var anchorRoute, out var anchorProgress))
            {
                if (!focusIncap.IsNone && IsIncapacitated(world, focusIncap))
                {
                    into.Add(focusIncap);
                    return true;
                }

                return false;
            }

            for (var i = 0; i < roster.Count; i++)
            {
                var id = roster[i];
                if (id.IsNone || !IsLivingForMacroOrder(world, id))
                    continue;
                if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    continue;
                if (!WorldTravelService.CanReceiveTravelOrder(world, id))
                    continue;
                if (!ReinforcementRangeService.IsWithinReinforcementRange(
                        world, wp, anchorNode, anchorRoute, anchorProgress))
                    continue;
                into.Add(id);
            }

            if (into.Count > 0)
                return true;

            for (var i = 0; i < roster.Count; i++)
            {
                var id = roster[i];
                if (id.IsNone || !IsIncapacitated(world, id))
                    continue;
                if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    continue;
                if (!ReinforcementRangeService.IsWithinReinforcementRange(
                        world, wp, anchorNode, anchorRoute, anchorProgress))
                    continue;
                into.Add(id);
            }

            if (into.Count == 0 &&
                !focusIncap.IsNone &&
                IsIncapacitated(world, focusIncap) &&
                world.WorldPresence.TryGet(focusIncap, out var focusWp) &&
                focusWp != null &&
                ReinforcementRangeService.IsWithinReinforcementRange(
                    world, focusWp, anchorNode, anchorRoute, anchorProgress))
            {
                into.Add(focusIncap);
            }

            return into.Count > 0;
        }

        public static bool CanEnterLingeringBattlefield(
            SimulationWorld world,
            IReadOnlyList<EntityId> roster,
            EntityId focusIncap,
            List<EntityId> scratch)
        {
            if (scratch == null)
                return false;
            return CollectViewParty(world, roster, focusIncap, scratch) && scratch.Count > 0;
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
