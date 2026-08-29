using System;
using System.Collections.Generic;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// WorldSite 战略路由唯一真源 —— PlayerParty / FormalArmy 共享。
    /// 基础 Policy：
    ///  - 所有非目标 WorldSite footprint 默认 blocked（不能把普通 Site 当道路穿越）；
    ///  - 目标 Site（destinationSiteId）footprint / ingress 可进入，不阻塞。
    /// Dynamic MandatoryTransit（Phase 5D-B2）：正常 A→B NoRoute 时，用反事实
    /// permeability probe 找出"单独放开后能真正连通 A/B"的那个非目标 Site（无论预配置
    /// 类型），作为本次路线的 MandatoryTransitSite。Transit 是路径中的动态关系，不是
    /// Site 固有属性 —— 不依赖 TransitMode / DisplayName / SiteType 预配置。
    /// </summary>
    public static class WorldSiteTransitPolicy
    {
        /// <summary>
        /// 收集"不可作为非目标中转"的 footprint hex 集合：目标 Site 不在集合内，
        /// 其余所有非目标 WorldSite（普通城市 / 关隘 / 营地）全部 blocked。
        /// </summary>
        public static HashSet<HexCoord> BuildBlockedFootprintHexes(
            SimulationWorld world,
            string destinationSiteId)
        {
            var blocked = new HashSet<HexCoord>();
            if (world?.Strategic?.Sites == null)
                return blocked;

            foreach (var kv in world.Strategic.Sites.Sites)
            {
                var site = kv.Value;
                if (site == null)
                    continue;
                if (string.Equals(site.SiteId, destinationSiteId, StringComparison.Ordinal))
                    continue; // 目标 Site：允许到达其正式 ingress。
                foreach (var hex in site.EnumerateFootprintHexes())
                    blocked.Add(hex);
            }

            return blocked;
        }

        /// <summary>
        /// Dynamic MandatoryTransitSite resolver（战略层公共，非 UI 专属）。
        /// 正常 A→B NoRoute 后调用：对每个【非目标】WorldSite S，在【仅移除 S footprint、
        /// 其余非目标 Site 全 blocked】的基线下用同一 HexPathfinder 重算 A→B；仅当
        /// ProbeRouteSuccess 且测试路径真实经过 S 的至少一个 footprint 格，才把 S 视为
        /// MandatoryTransit candidate（证明"允许穿过 S 后 A/B 被真正连通"）。多候选按假设
        /// 直通路径的实际 cost 选最小。输出只截取 A→S 段（到首个进入 S footprint 的入口格），
        /// 不写入任何 Travel —— 仅为战略可达性证明，由调用方决定后续行为。
        /// </summary>
        public static bool TryResolveMandatoryTransitSite(
            SimulationWorld world,
            HexCoord startHex,
            HexCoord goalHex,
            string destinationSiteId,
            HexTravelMode mode,
            string fromSiteId,
            List<HexCoord> into,
            out string transitSiteId,
            out HexCoord transitApproachHex)
        {
            into.Clear();
            transitSiteId = string.Empty;
            transitApproachHex = default;
            if (world?.Strategic?.Sites == null || world.HexWorld == null)
                return false;

            // 基线：所有非目标 Site blocked；出发 Site footprint exempt（departure 段合法路径）。
            var baselineBlocked = BuildBlockedFootprintHexes(world, destinationSiteId);
            ExemptDepartureSiteFootprint(world, fromSiteId, baselineBlocked);

            PlayerPartyWorldLocationDebug.Sink?.Invoke(
                "[GatewayProbe] FinalTarget=" + goalHex + " Baseline=NoRoute");

            var probePath = new List<HexCoord>(128);
            var probeBlocked = new HashSet<HexCoord>();
            var bestCost = float.MaxValue;
            var found = false;

            foreach (var kv in world.Strategic.Sites.Sites)
            {
                var site = kv.Value;
                if (site == null)
                    continue;
                if (string.Equals(site.SiteId, destinationSiteId, StringComparison.Ordinal))
                    continue; // 目标本身：直接以它为终点，不走中间节点语义。
                if (site.OccupiesHex(startHex) || site.OccupiesHex(goalHex))
                    continue; // 起点/终点已在 Site footprint 内：当前 leg 应直接以它为终点。

                // 反事实 permeability：临时仅移除候选 S 的 footprint，其余 blocked 不变。
                probeBlocked.Clear();
                probeBlocked.UnionWith(baselineBlocked);
                foreach (var hex in site.EnumerateFootprintHexes())
                    probeBlocked.Remove(hex);

                probePath.Clear();
                var probeOk = HexPathfinder.TryFindPath(
                    world.HexWorld, startHex, goalHex, probePath, mode, probeBlocked);

                // 测试路径必须真实经过 S 至少一个 footprint 格；首个进入格 = A 侧真实入口。
                var crosses = false;
                var entryIndex = -1;
                for (var i = 0; i < probePath.Count; i++)
                {
                    if (site.OccupiesHex(probePath[i]))
                    {
                        crosses = true;
                        entryIndex = i;
                        break;
                    }
                }

                var probeCost = probeOk ? PathCost(world, probePath) : float.MaxValue;
                PlayerPartyWorldLocationDebug.Sink?.Invoke(
                    "[GatewayProbe] Candidate=" + site.SiteId +
                    " ProbeRouteSuccess=" + probeOk +
                    " ProbePathCrossesGateway=" + crosses +
                    " ProbeCost=" + (probeOk ? probeCost.ToString("0.##") : "n/a"));

                if (!probeOk || !crosses || probeCost >= bestCost)
                    continue;
                bestCost = probeCost;
                transitSiteId = site.SiteId;
                transitApproachHex = probePath[entryIndex];
                into.Clear();
                for (var i = 0; i <= entryIndex; i++)
                    into.Add(probePath[i]); // 只输出 A→S 段（到入口格），不输出 S→B 段。
                found = true;
            }

            PlayerPartyWorldLocationDebug.Sink?.Invoke(
                "[GatewayProbe] SelectedGateway=" + (found ? transitSiteId : "none"));
            return found;
        }

        /// <summary>出发 Site 自身的 footprint 是 departure 段合法路径，从 blocked 中移除。</summary>
        static void ExemptDepartureSiteFootprint(
            SimulationWorld world,
            string siteId,
            HashSet<HexCoord> blocked)
        {
            if (string.IsNullOrEmpty(siteId) || blocked == null || world?.Strategic?.Sites == null)
                return;
            if (world.Strategic.Sites.TryGet(siteId, out var site) && site != null)
            {
                foreach (var hex in site.EnumerateFootprintHexes())
                    blocked.Remove(hex);
            }
        }

        /// <summary>路径总代价（逐格 ResolveMovementCost；缺 tile 按 1）。</summary>
        static float PathCost(SimulationWorld world, IReadOnlyList<HexCoord> path)
        {
            if (path == null)
                return 0f;
            var total = 0f;
            if (world?.HexWorld != null)
            {
                for (var i = 0; i < path.Count; i++)
                {
                    if (world.HexWorld.TryGetTile(path[i], out var tile) && tile != null)
                    {
                        var c = tile.ResolveMovementCost();
                        total += float.IsFinite(c) ? c : 1f;
                        continue;
                    }

                    total += 1f;
                }
            }
            else
            {
                total = path.Count;
            }

            return total;
        }
    }
}
