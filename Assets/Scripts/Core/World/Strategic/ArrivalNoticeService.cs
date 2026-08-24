using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Hex 战略：到站提示（legacy Route 旅行已移除）。</summary>
    public static class ArrivalNoticeService
    {
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
                if (StrategicPursuitService.IsCombatPursuitTraveler(world, id))
                    continue;
                if (!world.WorldPresence.TryGet(id, out var presence) || presence == null)
                    continue;
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
            return p.Mode == PartyWorldPresenceMode.AtSite ||
                   p.Mode == PartyWorldPresenceMode.AtNode;
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
                        firstFocus = p.SiteId ?? p.NodeId ?? string.Empty;
                }

                list.Add(id);
            }

            if (byPlace.Count == 0)
                return;

            var sb = new System.Text.StringBuilder(128);
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
            if (p.Mode == PartyWorldPresenceMode.AtSite && !string.IsNullOrEmpty(p.SiteId))
                return "site:" + p.SiteId;
            return "node:" + (p.NodeId ?? string.Empty);
        }

        static string ResolvePlaceLabel(SimulationWorld world, WorldAgentPresence p)
        {
            if (p.Mode == PartyWorldPresenceMode.AtSite &&
                !string.IsNullOrEmpty(p.SiteId) &&
                world.Strategic.Sites.TryGet(p.SiteId, out var site) &&
                site != null &&
                !string.IsNullOrWhiteSpace(site.DisplayName))
                return site.DisplayName;

            return PlaceSiteName(world, p.SiteId ?? p.NodeId);
        }

        static string PlaceSiteName(SimulationWorld world, string siteId)
        {
            if (string.IsNullOrEmpty(siteId))
                return "未知地点";
            if (world.Strategic.Sites.TryGet(siteId, out var site) &&
                site != null &&
                !string.IsNullOrWhiteSpace(site.DisplayName))
                return site.DisplayName;
            return siteId;
        }

        static string FormatPartyNames(SimulationWorld world, IReadOnlyList<EntityId> party)
        {
            if (party == null || party.Count == 0)
                return "我方";
            var sb = new System.Text.StringBuilder(32);
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
