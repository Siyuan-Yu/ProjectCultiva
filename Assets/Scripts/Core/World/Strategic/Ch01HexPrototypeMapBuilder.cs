using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Ch01 Hex 战略地图：第一版 100×50 正式验收世界 + WorldGraph 内容导入。</summary>
    public static class Ch01HexPrototypeMapBuilder
    {
        public const string MapId = "base:hex_ch01_prototype";
        public const string SiteHuangcun = "base:site_huangcun";
        public const string SiteQingyunLu = "base:site_qingyun_lu";

        public static readonly HexCoord HuangcunHex = new HexCoord(20, 25);
        public static readonly HexCoord QingyunLuHex = new HexCoord(38, 22);

        const string HuangcunNodeId = "base:node_huangcun";

        public static void Build(SimulationWorld world)
        {
            if (world == null)
                return;

            if (world.WorldGraph.TryGetNode(HuangcunNodeId, out _))
                BuildFullFromWorldGraph(world);
            else
                BuildMinimalTwoSitePrototype(world);
        }

        public static void BuildMinimalTwoSitePrototype(SimulationWorld world)
        {
            if (world == null)
                return;

            BuildPlayableRectangle(world, MapId, "Ch01 Hex Prototype (minimal)");
            RegisterSite(world, SiteHuangcun, "青石荒村", "Village", HuangcunHex,
                "base:node_huangcun", "base:map_huangcun");
            RegisterSite(world, SiteQingyunLu, "青石路", "Road", QingyunLuHex,
                "base:node_qingyun_lu", "base:map_qingyun_lu");
            ApplyTerrainForSite(world, HuangcunHex, "Village");
            ApplyTerrainForSite(world, QingyunLuHex, "Road");
            PaintRoadPath(world.HexWorld, HuangcunHex, QingyunLuHex);
            LinkLegacyNodes(world);
        }

        public static void BuildFullFromWorldGraph(SimulationWorld world)
        {
            if (world?.WorldGraph == null)
                return;

            BuildPlayableRectangle(world, MapId, "Ch01 Hex Strategic");

            var rawHex = new System.Collections.Generic.Dictionary<string, HexCoord>(64);
            var minQx = float.MaxValue;
            var maxQx = float.MinValue;
            var minQy = float.MaxValue;
            var maxQy = float.MinValue;

            foreach (var kv in world.WorldGraph.Nodes)
            {
                var node = kv.Value;
                if (node == null || string.IsNullOrEmpty(node.Id))
                    continue;
                rawHex[node.Id] = WorldGraphToHex(node.WorldX, node.WorldY);
                minQx = System.Math.Min(minQx, node.WorldX);
                maxQx = System.Math.Max(maxQx, node.WorldX);
                minQy = System.Math.Min(minQy, node.WorldY);
                maxQy = System.Math.Max(maxQy, node.WorldY);
            }

            if (rawHex.Count == 0)
                return;

            var spanX = System.Math.Max(1f, maxQx - minQx);
            var spanY = System.Math.Max(1f, maxQy - minQy);
            var placeW = HexWorldScale.PlayableV1Width - HexWorldScale.PlayableOriginQ * 2;
            var placeH = HexWorldScale.PlayableV1Height - HexWorldScale.PlayableOriginR * 2;

            var nodeHex = new System.Collections.Generic.Dictionary<string, HexCoord>(rawHex.Count);
            foreach (var kv in world.WorldGraph.Nodes)
            {
                var node = kv.Value;
                if (node == null || string.IsNullOrEmpty(node.Id))
                    continue;
                var tq = (int)System.Math.Round((node.WorldX - minQx) / spanX * (placeW - 1));
                var tr = (int)System.Math.Round((node.WorldY - minQy) / spanY * (placeH - 1));
                nodeHex[node.Id] = new HexCoord(
                    HexWorldScale.PlayableOriginQ + tq,
                    HexWorldScale.PlayableOriginR + tr);
            }

            foreach (var kv in nodeHex)
            {
                var nodeId = kv.Key;
                if (!world.WorldGraph.TryGetNode(nodeId, out var node) || node == null)
                    continue;
                var anchor = kv.Value;
                var siteId = "base:site_" + nodeId.Substring(nodeId.LastIndexOf('_') + 1);
                RegisterSite(
                    world,
                    siteId,
                    string.IsNullOrEmpty(node.Name) ? nodeId : node.Name,
                    node.Kind ?? "Site",
                    anchor,
                    nodeId,
                    node.LocalMapId ?? string.Empty);
                ApplyTerrainForSite(world, anchor, node.Kind);
            }

            foreach (var kv in world.WorldGraph.Routes)
            {
                var route = kv.Value;
                if (route == null)
                    continue;
                if (!nodeHex.TryGetValue(route.FromNodeId ?? string.Empty, out var from) ||
                    !nodeHex.TryGetValue(route.ToNodeId ?? string.Empty, out var to))
                    continue;
                PaintRoadPath(world.HexWorld, from, to);
            }

            LinkLegacyNodes(world);
        }

        static void BuildPlayableRectangle(SimulationWorld world, string mapId, string mapName)
        {
            var grid = world.HexWorld;
            grid.Clear();
            grid.MapId = mapId;
            grid.MapName = mapName;
            grid.HexSize = HexWorldScale.DefaultHexOuterRadius;
            grid.FillRectangle(HexWorldScale.PlayableV1Width, HexWorldScale.PlayableV1Height, HexTerrainType.Plain);
            PaintAllRockTerrain(grid);
        }

        static void PaintAllRockTerrain(HexWorld grid)
        {
            if (!grid.UsesCompactStorage)
                return;

            for (var r = 0; r < grid.Height; r++)
            {
                for (var q = 0; q < grid.Width; q++)
                {
                    if (!grid.TryGetCell(new HexCoord(q, r), out var cell) || cell == null)
                        continue;
                    cell.Terrain = HexTerrainType.Mountain;
                    cell.IsRoad = false;
                    cell.IsPassable = false;
                }
            }
        }

        static void RegisterSite(
            SimulationWorld world,
            string siteId,
            string displayName,
            string kind,
            HexCoord anchor,
            string legacyNodeId,
            string localMapId)
        {
            var site = new WorldSite
            {
                SiteId = siteId,
                DisplayName = displayName,
                SiteType = kind,
                AnchorHex = anchor,
                LocalMapId = localMapId,
                LegacyNodeId = legacyNodeId,
            };
            site.SetFootprint(BuildFootprintForKind(world.HexWorld, anchor, kind));

            if (world.WorldGraph.TryGetNode(legacyNodeId, out var node) && node != null)
            {
                site.OwnerFactionId = node.OwnerId ?? string.Empty;
                if (!string.IsNullOrEmpty(node.LocalMapId))
                    site.LocalMapId = node.LocalMapId;
                WorldSiteRegistrationService.LinkLegacyNodeToHex(node, anchor);
            }

            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);
        }

        static System.Collections.Generic.IEnumerable<HexCoord> BuildFootprintForKind(HexWorld grid, HexCoord anchor, string kind)
        {
            var footprint = new System.Collections.Generic.List<HexCoord>(6) { anchor };
            var extra = FootprintExtraHexCount(kind);
            if (extra <= 0)
                return footprint;

            for (var d = 0; d < 6 && footprint.Count < 1 + extra; d++)
            {
                var neighbor = HexMath.Neighbor(anchor, d);
                if (grid != null && grid.IsInBounds(neighbor) && !grid.Contains(neighbor))
                    grid.GetOrCreate(neighbor);
                if (!footprint.Contains(neighbor))
                    footprint.Add(neighbor);
            }

            return footprint;
        }

        static int FootprintExtraHexCount(string kind)
        {
            if (string.IsNullOrEmpty(kind))
                return 0;
            switch (kind)
            {
                case "City":
                case "Town":
                    return 2;
                case "Sect":
                case "Fortress":
                case "Mine":
                    return 1;
                default:
                    return 0;
            }
        }

        static void LinkLegacyNodes(SimulationWorld world)
        {
            foreach (var kv in world.Strategic.Sites.Sites)
            {
                var site = kv.Value;
                if (site == null || string.IsNullOrEmpty(site.LegacyNodeId))
                    continue;
                if (!world.WorldGraph.TryGetNode(site.LegacyNodeId, out var node) || node == null)
                    continue;
                WorldSiteRegistrationService.LinkLegacyNodeToHex(node, site.AnchorHex);
            }
        }

        public static HexCoord WorldGraphToHex(float worldX, float worldY) =>
            new HexCoord(
                (int)System.Math.Round(worldX * HexWorldScale.WorldGraphHexStepsPerUnit),
                -(int)System.Math.Round(worldY * HexWorldScale.WorldGraphHexStepsPerUnit));

        static void PaintRoadPath(HexWorld grid, HexCoord from, HexCoord to)
        {
            var path = new System.Collections.Generic.List<HexCoord>(64);
            CollectHexLine(from, to, path);
            for (var i = 0; i < path.Count; i++)
            {
                if (!grid.IsInBounds(path[i]))
                    continue;
                PaintRoadTile(grid, path[i]);
            }
        }

        static void PaintRoadTile(HexWorld grid, HexCoord hex)
        {
            var tile = grid.GetOrCreate(hex);
            tile.Terrain = HexTerrainType.Road;
            tile.IsRoad = true;
            tile.IsPassable = true;
        }

        static void CollectHexLine(HexCoord from, HexCoord to, System.Collections.Generic.List<HexCoord> pathOut)
        {
            pathOut.Clear();
            var steps = HexMath.Distance(from, to);
            if (steps <= 0)
            {
                pathOut.Add(from);
                return;
            }

            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)steps;
                var q = from.Q + (to.Q - from.Q) * t;
                var r = from.R + (to.R - from.R) * t;
                var s = from.S + (to.S - to.S) * t;
                pathOut.Add(CubeRound(q, r, s));
            }
        }

        static HexCoord CubeRound(float q, float r, float s)
        {
            var rq = System.Math.Round(q);
            var rr = System.Math.Round(r);
            var rs = System.Math.Round(s);

            var dq = System.Math.Abs(rq - q);
            var dr = System.Math.Abs(rr - r);
            var ds = System.Math.Abs(rs - s);

            if (dq > dr && dq > ds)
                rq = -rr - rs;
            else if (dr > ds)
                rr = -rq - rs;
            else
                rs = -rq - rr;

            return new HexCoord((int)rq, (int)rr);
        }

        static void ApplyTerrainForSite(SimulationWorld world, HexCoord hex, string kind)
        {
            if (world?.HexWorld == null)
                return;

            foreach (var pad in BuildFootprintForKind(world.HexWorld, hex, kind))
                PaintRoadTile(world.HexWorld, pad);
        }

        /// <summary>Prototype 山匪巡逻 Hex：相对荒村锚点外圈 7～8 格，避开 Site 占地。</summary>
        public static HexCoord ResolvePrototypeBanditPatrolHex(SimulationWorld world, int preferredDistance = 8)
        {
            if (world?.HexWorld == null || !world.HexWorld.HasGrid)
                return QingyunLuHex;

            var origin = ResolveHuangcunAnchorHex(world);
            var grid = world.HexWorld;
            var minDistance = System.Math.Max(1, preferredDistance - 1);
            for (var ring = preferredDistance; ring >= minDistance; ring--)
            {
                if (TryPickPatrolOnRing(world, grid, origin, ring, out var picked))
                    return picked;
            }

            return grid.Contains(QingyunLuHex) ? QingyunLuHex : origin;
        }

        /// <summary>
        /// Prototype 测试山匪 Hex（对照手操红框）：
        /// strong = 荒村正南路廊；weak = 荒村正东横路。
        /// Content 荒村 (80,52) → strong (82,56)、weak (86,52)。
        /// </summary>
        public static void ResolvePrototypeTestBanditHexesBelowHuangcun(
            SimulationWorld world,
            out HexCoord strongPatrolHex,
            out HexCoord weakPatrolHex)
        {
            var origin = ResolveHuangcunAnchorHex(world);
            // 屏幕下方 = R+；屏幕右方 = Q+（Odd-R + GUI Y 翻转）
            strongPatrolHex = new HexCoord(origin.Q + 2, origin.R + 4);
            weakPatrolHex = new HexCoord(origin.Q + 6, origin.R);

            if (world?.HexWorld == null || !world.HexWorld.HasGrid)
                return;

            if (!TryResolveStationaryTestHex(world, origin, strongPatrolHex, preferHigherR: true, out strongPatrolHex))
                strongPatrolHex = new HexCoord(origin.Q + 2, origin.R + 4);
            if (!TryResolveStationaryTestHex(world, origin, weakPatrolHex, preferHigherR: false, out weakPatrolHex))
                weakPatrolHex = new HexCoord(origin.Q + 6, origin.R);
        }

        static bool TryResolveStationaryTestHex(
            SimulationWorld world,
            HexCoord origin,
            HexCoord preferred,
            bool preferHigherR,
            out HexCoord picked)
        {
            picked = preferred;
            if (IsStationaryTestBanditCandidate(world, world.HexWorld, origin, preferred))
                return true;

            HexCoord? best = null;
            var bestScore = int.MaxValue;
            for (var dr = -2; dr <= 2; dr++)
            {
                for (var dq = -2; dq <= 2; dq++)
                {
                    var hex = new HexCoord(preferred.Q + dq, preferred.R + dr);
                    if (!IsStationaryTestBanditCandidate(world, world.HexWorld, origin, hex))
                        continue;
                    if (preferHigherR && hex.R <= origin.R)
                        continue;
                    if (!preferHigherR && hex.Q <= origin.Q)
                        continue;

                    var score = HexMath.Distance(preferred, hex) * 10 + HexMath.Distance(origin, hex);
                    if (score >= bestScore)
                        continue;
                    bestScore = score;
                    best = hex;
                }
            }

            if (!best.HasValue)
                return false;
            picked = best.Value;
            return true;
        }

        static bool IsStationaryTestBanditCandidate(
            SimulationWorld world,
            HexWorld grid,
            HexCoord origin,
            HexCoord hex)
        {
            if (grid == null || !grid.Contains(hex))
                return false;
            if (hex.Equals(origin))
                return false;
            if (HexMath.Distance(origin, hex) > 10)
                return false;
            if (world.Strategic.Sites.TryGetAtHex(hex, out _))
                return false;
            return true;
        }

        /// <summary>测试山匪驻点：若 Content 未画路，则临时开格保证可放置／接战。</summary>
        public static void EnsurePrototypeTestBanditHexPassable(SimulationWorld world, HexCoord hex)
        {
            if (world?.HexWorld == null || !world.HexWorld.HasGrid || hex.Equals(default))
                return;

            if (!world.HexWorld.IsInBounds(hex))
                return;

            PaintRoadTile(world.HexWorld, hex);
        }

        static HexCoord ResolveHuangcunAnchorHex(SimulationWorld world)
        {
            if (world.Strategic.Sites.TryGet(SiteHuangcun, out var site) &&
                site != null &&
                !site.AnchorHex.Equals(default))
                return site.AnchorHex;

            if (ArmyHexBattleAnchorService.TryResolveHexForNode(world, HuangcunNodeId, out var hex))
                return hex;

            return HuangcunHex;
        }

        static bool TryPickPatrolOnRing(
            SimulationWorld world,
            HexWorld grid,
            HexCoord origin,
            int ring,
            out HexCoord picked)
        {
            picked = default;
            if (ring <= 0)
                return false;

            var ringHexes = new System.Collections.Generic.List<HexCoord>(ring * 6);
            CollectRing(origin, ring, ringHexes);

            HexCoord? best = null;
            var bestScore = int.MinValue;
            for (var i = 0; i < ringHexes.Count; i++)
            {
                var hex = ringHexes[i];
                if (!IsPatrolCandidate(world, grid, origin, hex))
                    continue;

                var score = hex.Q * 3 - hex.R;
                if (score <= bestScore)
                    continue;
                bestScore = score;
                best = hex;
            }

            if (!best.HasValue)
                return false;

            picked = best.Value;
            return true;
        }

        static bool IsPatrolCandidate(SimulationWorld world, HexWorld grid, HexCoord origin, HexCoord hex)
        {
            if (!grid.Contains(hex))
                return false;
            if (HexMath.Distance(origin, hex) < 7)
                return false;
            if (!grid.TryGetCell(hex, out var cell) || cell == null || !cell.IsPassable)
                return false;
            if (world.Strategic.Sites.TryGetAtHex(hex, out _))
                return false;
            return true;
        }

        static void CollectRing(HexCoord center, int radius, System.Collections.Generic.List<HexCoord> results)
        {
            results.Clear();
            if (radius <= 0)
                return;

            var startDir = HexMath.AxialDirections[4];
            var hex = new HexCoord(
                center.Q + startDir.Q * radius,
                center.R + startDir.R * radius);
            for (var side = 0; side < 6; side++)
            {
                for (var step = 0; step < radius; step++)
                {
                    results.Add(hex);
                    hex = HexMath.Neighbor(hex, side);
                }
            }
        }
    }
}
