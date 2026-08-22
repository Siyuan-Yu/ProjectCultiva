using System;
using System.Collections.Generic;
using System.Text;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>我方最终目的地到站提示（遇敌接战优先，不叠弹）。</summary>
    public static class ArrivalNoticeService
    {
        /// <summary>接战弹窗已覆盖本次抵达：参战者不再弹「是否查看」。</summary>
        public static void SuppressForParty(SimulationWorld world, IReadOnlyList<EntityId> party)
        {
            if (world?.WorldPresence == null || party == null)
                return;
            for (var i = 0; i < party.Count; i++)
            {
                var id = party[i];
                if (id.IsNone || !world.WorldPresence.TryGet(id, out var p) || p == null)
                    continue;
                p.SuppressArrivalNotice = true;
            }

            world.Strategic?.ClearArrivalNotice();
        }

        public static void AfterTravelTick(SimulationWorld world, IReadOnlyList<EntityId> arrivedThisTick)
        {
            if (world?.Strategic == null || arrivedThisTick == null || arrivedThisTick.Count == 0)
                return;
            if (world.Strategic.HasBlockingInterrupt)
                return;

            var final = new List<EntityId>(arrivedThisTick.Count);
            for (var i = 0; i < arrivedThisTick.Count; i++)
            {
                var id = arrivedThisTick[i];
                // 攻击／追击中的人：到站只走接战，绝不弹「是否查看」
                if (StrategicPursuitService.IsCombatPursuitTraveler(world, id))
                    continue;
                if (!world.WorldPresence.TryGet(id, out var presence) || presence == null)
                    continue;
                // 已弹过接战（含撤退）：同一趟抵达不再弹到站查看
                if (presence.SuppressArrivalNotice)
                    continue;
                if (!IsFinalPlayerArrival(world, id))
                    continue;
                final.Add(id);
            }

            if (final.Count == 0)
                return;

            TryBuildNotice(world, final);
        }

        static bool IsFinalPlayerArrival(SimulationWorld world, EntityId id)
        {
            if (id.IsNone || !IsPlayerAgent(world, id))
                return false;
            if (!world.WorldPresence.TryGet(id, out var p) || p == null)
                return false;
            if (p.Mode == PartyWorldPresenceMode.Traveling)
                return false;
            if (WorldTravelPathService.HasPendingLegs(id))
                return false;
            return p.Mode == PartyWorldPresenceMode.AtNode ||
                   p.Mode == PartyWorldPresenceMode.RouteAnchored;
        }

        static void TryBuildNotice(SimulationWorld world, IReadOnlyList<EntityId> arrived)
        {
            var byPlace = new Dictionary<string, List<EntityId>>(StringComparer.Ordinal);
            var placeLabels = new Dictionary<string, string>(StringComparer.Ordinal);
            string firstFocus = null;

            for (var i = 0; i < arrived.Count; i++)
            {
                var id = arrived[i];
                if (!world.WorldPresence.TryGet(id, out var p) || p == null)
                    continue;
                var placeKey = ResolvePlaceKey(p);
                var label = ResolvePlaceLabel(world, p);
                if (!byPlace.TryGetValue(placeKey, out var list))
                {
                    list = new List<EntityId>(4);
                    byPlace[placeKey] = list;
                    placeLabels[placeKey] = label;
                    if (firstFocus == null)
                        firstFocus = p.NodeId ?? string.Empty;
                }

                list.Add(id);
            }

            if (byPlace.Count == 0)
                return;

            var sb = new StringBuilder(128);
            var all = new List<EntityId>(arrived.Count);
            var firstPlaceLabel = string.Empty;
            foreach (var kv in byPlace)
            {
                var names = FormatPartyNames(world, kv.Value);
                var place = placeLabels[kv.Key];
                if (string.IsNullOrEmpty(firstPlaceLabel))
                    firstPlaceLabel = place;
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append(names).Append(" 抵达「").Append(place).Append("」");
                for (var i = 0; i < kv.Value.Count; i++)
                    all.Add(kv.Value[i]);
            }

            var notice = world.Strategic.ArrivalNotice;
            notice.Resolved = false;
            notice.NoticeId = "arrive:" + world.Tick.Value;
            notice.Summary = sb.ToString();
            notice.PlaceLabel = firstPlaceLabel;
            notice.FocusNodeId = firstFocus ?? string.Empty;
            notice.SetArrived(all);
        }

        static string ResolvePlaceKey(WorldAgentPresence p)
        {
            if (p.Mode == PartyWorldPresenceMode.RouteAnchored && !string.IsNullOrEmpty(p.RouteId))
                return "route:" + p.RouteId + ":" + p.RouteAnchorProgress.ToString("0.###");
            return "node:" + (p.NodeId ?? string.Empty);
        }

        static string ResolvePlaceLabel(SimulationWorld world, WorldAgentPresence p)
        {
            if (p.Mode == PartyWorldPresenceMode.RouteAnchored &&
                !string.IsNullOrEmpty(p.RouteId) &&
                world.WorldGraph.TryGetRoute(p.RouteId, out var route) &&
                route != null)
            {
                var from = PlaceNodeName(world, route.FromNodeId);
                var to = PlaceNodeName(world, route.ToNodeId);
                return from + "—" + to + " 路上";
            }

            return PlaceNodeName(world, p.NodeId);
        }

        static string PlaceNodeName(SimulationWorld world, string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                return "未知地点";
            if (world.WorldGraph.TryGetNode(nodeId, out var node) &&
                node != null &&
                !string.IsNullOrWhiteSpace(node.Name))
                return node.Name;
            return nodeId;
        }

        static string FormatPartyNames(SimulationWorld world, IReadOnlyList<EntityId> party)
        {
            if (party == null || party.Count == 0)
                return "我方";
            var sb = new StringBuilder(32);
            var n = Math.Min(party.Count, 3);
            for (var i = 0; i < n; i++)
            {
                if (i > 0)
                    sb.Append('、');
                sb.Append(EntityName(world, party[i]));
            }

            if (party.Count > 3)
                sb.Append(" 等").Append(party.Count).Append("人");
            return sb.ToString();
        }

        static string EntityName(SimulationWorld world, EntityId id)
        {
            if (!world.Entities.TryGet(id, out var e) || e == null)
                return "同伴";
            if (!string.IsNullOrWhiteSpace(e.DisplayName))
                return e.DisplayName;
            return "同伴";
        }

        static bool IsPlayerAgent(SimulationWorld world, EntityId id)
        {
            if (id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                return false;
            return (entity.Tags & EntityTag.Npc) == 0;
        }
    }
}
