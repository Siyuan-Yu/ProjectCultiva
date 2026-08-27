using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 2C：Wilderness LocalMap 内移动同步、边缘跨 Hex、WorldSite 出站。
    /// </summary>
    public static class PlayerPartyWildernessTransitionService
    {
        /// <summary>
        /// 仅 Surface World Location LocalMap 启用 Hex 边界跨格；Interior（洞窟／室内）禁用。
        /// </summary>
        public static bool IsSurfaceHexEdgeTransitionEnabled(SimulationWorld world)
        {
            if (world?.LocalMap == null || world.PlayerPartyTravel == null)
                return false;
            if (world.LocalMap.IsInInterior)
                return false;

            var motion = world.PlayerPartyTravel;
            if (!motion.HasPosition || motion.IsMoving)
                return false;

            return motion.LocationKind == PlayerPartyLocationKind.AtWorldSite ||
                   motion.LocationKind == PlayerPartyLocationKind.AtWorldPosition;
        }

        /// <summary>
        /// 根据 LocationKind 尝试 Site Exit 或 Wilderness Cross；单次 Debug（非每帧）。
        /// </summary>
        public static Result TryAttemptSurfaceEdgeTransition(
            SimulationWorld world,
            PlayerPartyRuntime party,
            int directionIndex)
        {
            if (!IsSurfaceHexEdgeTransitionEnabled(world))
                return Result.Failure(ErrorCode.InvalidOperation, "Surface hex edge transition disabled.");

            var motion = world.PlayerPartyTravel;
            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                TryFindSiteConnectionByDirection(world, directionIndex, out var siteConnection))
                return TryAttemptSurfaceEdgeTransition(world, party, siteConnection);

            return TryAttemptSurfaceEdgeTransitionInternal(
                world, party, directionIndex, default, false);
        }

        public static Result TryAttemptSurfaceEdgeTransition(
            SimulationWorld world,
            PlayerPartyRuntime party,
            SurfaceExitConnection connection)
        {
            if (!IsSurfaceHexEdgeTransitionEnabled(world))
                return Result.Failure(ErrorCode.InvalidOperation, "Surface hex edge transition disabled.");

            return TryAttemptSurfaceEdgeTransitionInternal(
                world,
                party,
                connection.DirectionIndex,
                connection,
                true);
        }

        static Result TryAttemptSurfaceEdgeTransitionInternal(
            SimulationWorld world,
            PlayerPartyRuntime party,
            int directionIndex,
            SurfaceExitConnection connection,
            bool hasConnection)
        {
            var motion = world.PlayerPartyTravel;
            var gate = motion.SurfaceEdgeGate;
            if (gate != null && !gate.CanAttemptEdgeTransition)
                return Result.Failure(ErrorCode.InvalidOperation, "Edge transition gated (in progress or disarmed).");

            var dir = NormalizeDirection(directionIndex);
            ProbeNeighbor(world, motion, dir, out var sourceHex, out var neighbor, out var passable, out var terrain);

            if (hasConnection)
            {
                sourceHex = connection.SourceHex;
                neighbor = connection.DestinationHex;
                passable = IsGroundPassable(world.HexWorld, neighbor);
                gate?.BeginTransition(
                    connection.DirectionIndex,
                    connection.DestinationHex,
                    connection.SourceHex,
                    hasBoundaryContext: motion.LocationKind == PlayerPartyLocationKind.AtWorldSite);
            }
            else
            {
                gate?.BeginTransition(dir);
            }

            Result result;
            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite)
            {
                result = hasConnection
                    ? TryExitWorldSiteByConnection(world, party, connection)
                    : TryExitWorldSiteByDirection(world, party, dir);
            }
            else
            {
                result = hasConnection
                    ? TryCrossWildernessEdge(world, party, connection.DestinationHex)
                    : TryCrossWildernessEdge(world, party, dir);
            }

            if (result.IsFailure)
                gate?.ClearEdgeState();

            LogEdgeAttempt(
                world,
                party,
                dir,
                sourceHex,
                neighbor,
                terrain,
                passable,
                result);
            return result;
        }

        /// <summary>
        /// Expand/Materialize 完成后：用目的 LocalMap bounds + Entry 边完成 Gate（Disarm）。
        /// </summary>
        public static void CompleteEdgeTransitionPresentation(
            SimulationWorld world,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float spawnLocalX,
            float spawnLocalY)
        {
            var motion = world?.PlayerPartyTravel;
            var gate = motion?.SurfaceEdgeGate;
            if (gate == null)
                return;
            var exitDir = gate.LastExitDirection >= 0 ? gate.LastExitDirection : 0;
            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;
            var depth = SurfaceExitZoneCalculator.ResolveDepthFromSession(world, bounds);
            var currentHex = motion.CurrentHex;
            HexCoord cameFromHex;
            if (gate.HasExitBoundaryContext)
                cameFromHex = gate.LastExitSourceFootprintHex;
            else
                cameFromHex = HexMath.Neighbor(
                    currentHex,
                    WildernessLocalWorldProjection.OppositeDirection(exitDir));
            if (!WildernessLocalWorldProjection.IsInSafeInterior(spawnLocalX, spawnLocalY, bounds))
            {
                WildernessLocalWorldProjection.GetLocalPositionNearEdge(
                    bounds,
                    currentHex,
                    cameFromHex,
                    hexSize,
                    depth,
                    out spawnLocalX,
                    out spawnLocalY);
            }

            gate.CompleteTransition(exitDir, spawnLocalX, spawnLocalY);
        }

        public static Result TrySyncLocalMovementToWorldPosition(
            SimulationWorld world,
            float localX,
            float localY,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds)
        {
            if (world?.PlayerPartyTravel == null)
                return Result.Failure(ErrorCode.InvalidArgument, "No party travel state.");
            var motion = world.PlayerPartyTravel;
            if (!motion.HasPosition ||
                motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition)
                return Result.Success();

            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;
            if (!WildernessLocalWorldProjection.TryProjectLocalToWorld(
                    motion.CurrentHex,
                    localX,
                    localY,
                    bounds,
                    hexSize,
                    out var worldPos))
                return Result.Failure(ErrorCode.InvalidOperation, "Local to world projection failed.");

            var derived = HexMath.WorldToHex(worldPos.X, worldPos.Y, hexSize);
            motion.SetWorldPositionInternal(worldPos, derived);
            ApplyTravelingMembersAtHex(world, derived);
            return Result.Success();
        }

        public static Result TryCrossWildernessEdge(
            SimulationWorld world,
            PlayerPartyRuntime party,
            int directionIndex)
        {
            directionIndex = NormalizeDirection(directionIndex);
            var motion = world?.PlayerPartyTravel;
            if (motion == null || !motion.HasPosition)
                return Result.Failure(ErrorCode.InvalidOperation, "Party has no world position.");
            var neighbor = HexMath.Neighbor(motion.CurrentHex, directionIndex);
            return TryCrossWildernessEdge(world, party, neighbor);
        }

        public static Result TryCrossWildernessEdge(
            SimulationWorld world,
            PlayerPartyRuntime party,
            HexCoord destinationHex)
        {
            if (world == null || party == null || !party.HasActive)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid wilderness edge args.");
            var motion = world.PlayerPartyTravel;
            if (motion == null || !motion.HasPosition)
                return Result.Failure(ErrorCode.InvalidOperation, "Party has no world position.");
            if (motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition)
                return Result.Failure(ErrorCode.InvalidOperation, "Not in continuous wilderness position.");
            if (motion.IsMoving)
                return Result.Failure(ErrorCode.InvalidOperation, "Stop travel before crossing hex edge.");

            var currentHex = motion.CurrentHex;
            if (!IsNeighborHex(currentHex, destinationHex))
                return Result.Failure(ErrorCode.InvalidOperation, "Destination hex is not a neighbor.");
            if (!IsGroundPassable(world.HexWorld, destinationHex))
                return Result.Failure(ErrorCode.InvalidOperation, "Neighbor hex is impassable.");

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var newWorldPos = WildernessLocalWorldProjection.ComputeCrossEdgeWorldPosition(
                currentHex,
                destinationHex,
                motion.WorldPosition,
                hexSize);
            var derived = HexMath.WorldToHex(newWorldPos.X, newWorldPos.Y, hexSize);

            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
            PlayerPartyTransitionMembership.LogPartyTransition(
                world,
                party,
                "CrossWildernessEdge.BeforeApply",
                destinationHex,
                world.PartyWorld?.LocalMapId);

            motion.SetAtWorldPosition(newWorldPos, derived);
            ApplyTravelingMembersAtHex(world, derived);

            if (world.Strategic.Sites.TryGetAtHex(destinationHex, out var site) && site != null)
                return PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, site);

            if (!WildernessLocalMapFallback.TryResolve(world, destinationHex, out var mapId) ||
                string.IsNullOrEmpty(mapId))
                return Result.Failure(ErrorCode.InvalidOperation, "No wilderness fallback LocalMap for neighbor.");

            return WorldTravelService.EnterWildernessLocalMap(world, destinationHex, mapId);
        }

        public static Result TryExitWorldSiteByDirection(
            SimulationWorld world,
            PlayerPartyRuntime party,
            int directionIndex)
        {
            if (!TryFindSiteConnectionByDirection(world, directionIndex, out var connection))
                return Result.Failure(ErrorCode.InvalidOperation, "No site exit for that direction.");
            return TryExitWorldSiteByConnection(world, party, connection);
        }

        public static Result TryExitWorldSiteByDestinationHex(
            SimulationWorld world,
            PlayerPartyRuntime party,
            HexCoord destinationHex)
        {
            if (!TryFindSiteConnectionByDestination(world, destinationHex, out var connection))
                return Result.Failure(ErrorCode.InvalidOperation, "No site exit for destination hex.");
            return TryExitWorldSiteByConnection(world, party, connection);
        }

        public static Result TryExitWorldSiteByConnection(
            SimulationWorld world,
            PlayerPartyRuntime party,
            SurfaceExitConnection connection)
        {
            if (world == null || party == null || !party.HasActive)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid site exit args.");
            var motion = world.PlayerPartyTravel;
            if (motion == null || !motion.HasPosition)
                return Result.Failure(ErrorCode.InvalidOperation, "Party has no world position.");
            if (motion.LocationKind != PlayerPartyLocationKind.AtWorldSite ||
                string.IsNullOrEmpty(motion.SiteId))
                return Result.Failure(ErrorCode.InvalidOperation, "Party is not at a WorldSite.");
            if (motion.IsMoving)
                return Result.Failure(ErrorCode.InvalidOperation, "Stop travel before leaving site.");

            if (!world.Strategic.Sites.TryGet(motion.SiteId, out var site) || site == null)
                return Result.Failure(ErrorCode.NotFound, "WorldSite missing.", motion.SiteId);

            var sourceFootprint = connection.SourceHex;
            var external = connection.DestinationHex;
            if (!site.OccupiesHex(sourceFootprint))
                return Result.Failure(ErrorCode.InvalidOperation, "Exit source is not in site footprint.");
            if (site.OccupiesHex(external))
                return Result.Failure(ErrorCode.InvalidOperation, "No external neighbor outside footprint.");
            if (!IsGroundPassable(world.HexWorld, external))
                return Result.Failure(ErrorCode.InvalidOperation, "External hex is impassable.");

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var worldPos = WildernessLocalWorldProjection.ComputeCrossEdgeWorldPosition(
                sourceFootprint,
                external,
                motion.WorldPosition,
                hexSize);
            var derived = HexMath.WorldToHex(worldPos.X, worldPos.Y, hexSize);

            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
            PlayerPartyTransitionMembership.LogPartyTransition(
                world,
                party,
                "ExitWorldSite.BeforeApply",
                external,
                world.PartyWorld?.LocalMapId);

            motion.SetAtWorldPosition(worldPos, derived);
            ApplyTravelingMembersAtHex(world, derived);

            if (world.Strategic.Sites.TryGetAtHex(external, out var destSite) && destSite != null)
                return PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, destSite);

            if (!WildernessLocalMapFallback.TryResolve(world, external, out var mapId) ||
                string.IsNullOrEmpty(mapId))
                return Result.Failure(ErrorCode.InvalidOperation, "No wilderness fallback LocalMap for exit hex.");

            return WorldTravelService.EnterWildernessLocalMap(world, external, mapId);
        }

        public static bool TryEvaluateSurfaceExitLegality(
            SimulationWorld world,
            int directionIndex,
            out HexCoord neighborHex,
            out bool passable)
        {
            neighborHex = default;
            passable = false;
            if (world?.PlayerPartyTravel == null || !world.PlayerPartyTravel.HasPosition)
                return false;
            if (world.LocalMap != null && world.LocalMap.IsInInterior)
                return false;

            var motion = world.PlayerPartyTravel;
            var dir = NormalizeDirection(directionIndex);
            ProbeNeighbor(world, motion, dir, out _, out neighborHex, out passable, out _);

            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite)
            {
                if (string.IsNullOrEmpty(motion.SiteId) ||
                    !world.Strategic.Sites.TryGet(motion.SiteId, out var site) ||
                    site == null)
                    return false;

                if (TryFindSiteConnectionByDirection(world, dir, out var connection))
                {
                    neighborHex = connection.DestinationHex;
                    passable = IsGroundPassable(world.HexWorld, neighborHex);
                    return true;
                }

                passable = false;
                return true;
            }

            if (motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition)
                return false;

            passable = IsGroundPassable(world.HexWorld, neighborHex);
            return true;
        }

        /// <summary>WorldSite 某方向的 Footprint 最外缘 Hex（供旧 API 兼容；新逻辑请用 Connection）。</summary>
        public static bool TryResolveSiteExitSourceHex(
            SimulationWorld world,
            string siteId,
            int directionIndex,
            out HexCoord outerHex)
        {
            outerHex = default;
            if (TryFindSiteConnectionByDirection(world, directionIndex, out var connection))
            {
                outerHex = connection.SourceHex;
                return true;
            }

            if (world?.Strategic?.Sites == null ||
                string.IsNullOrEmpty(siteId) ||
                !world.Strategic.Sites.TryGet(siteId, out var site) ||
                site == null)
                return false;
            return TryPickOutermostFootprintHex(site, directionIndex, out outerHex);
        }

        public static bool TryFindSiteConnectionByDestination(
            SimulationWorld world,
            HexCoord destinationHex,
            out SurfaceExitConnection connection)
        {
            connection = default;
            var motion = world?.PlayerPartyTravel;
            if (motion == null ||
                motion.LocationKind != PlayerPartyLocationKind.AtWorldSite ||
                string.IsNullOrEmpty(motion.SiteId))
                return false;

            var bounds = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                0f, 0f, 1f, 16, 16);
            var depth = SurfaceExitZoneCalculator.DefaultExitTriggerDepth;
            var list = new System.Collections.Generic.List<SurfaceExitConnection>(12);
            SurfaceExitZoneCalculator.CollectConnections(world, bounds, depth, list);
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].DestinationHex != destinationHex)
                    continue;
                connection = list[i];
                return true;
            }

            return false;
        }

        static bool TryFindSiteConnectionByDirection(
            SimulationWorld world,
            int directionIndex,
            out SurfaceExitConnection connection)
        {
            connection = default;
            var motion = world?.PlayerPartyTravel;
            if (motion == null ||
                motion.LocationKind != PlayerPartyLocationKind.AtWorldSite ||
                string.IsNullOrEmpty(motion.SiteId))
                return false;

            var dir = NormalizeDirection(directionIndex);
            var bounds = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                0f, 0f, 1f, 16, 16);
            var depth = SurfaceExitZoneCalculator.DefaultExitTriggerDepth;
            var list = new System.Collections.Generic.List<SurfaceExitConnection>(12);
            SurfaceExitZoneCalculator.CollectConnections(world, bounds, depth, list);
            SurfaceExitConnection? best = null;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].DirectionIndex != dir)
                    continue;
                if (!best.HasValue)
                {
                    best = list[i];
                    continue;
                }

                // 同方向多连接：取 Destination 排序第一（确定性，非随机合并）。
                if (CompareHex(list[i].DestinationHex, best.Value.DestinationHex) < 0)
                    best = list[i];
            }

            if (!best.HasValue)
                return false;
            connection = best.Value;
            return true;
        }

        static int CompareHex(HexCoord a, HexCoord b)
        {
            var cmp = a.Q.CompareTo(b.Q);
            return cmp != 0 ? cmp : a.R.CompareTo(b.R);
        }

        static bool IsNeighborHex(HexCoord a, HexCoord b)
        {
            for (var d = 0; d < 6; d++)
            {
                if (HexMath.Neighbor(a, d).Equals(b))
                    return true;
            }

            return false;
        }

        static void ProbeNeighbor(
            SimulationWorld world,
            PlayerPartyWorldMotion motion,
            int directionIndex,
            out HexCoord sourceHex,
            out HexCoord neighbor,
            out bool passable,
            out HexTerrainType terrain)
        {
            sourceHex = motion.CurrentHex;
            neighbor = default;
            passable = false;
            terrain = HexTerrainType.Plain;

            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(motion.SiteId) &&
                TryFindSiteConnectionByDirection(world, directionIndex, out var siteConnection))
            {
                sourceHex = siteConnection.SourceHex;
                neighbor = siteConnection.DestinationHex;
            }
            else if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(motion.SiteId) &&
                world.Strategic.Sites.TryGet(motion.SiteId, out var site) &&
                site != null &&
                TryPickOutermostFootprintHex(site, directionIndex, out var outer))
            {
                sourceHex = outer;
                neighbor = HexMath.Neighbor(outer, directionIndex);
            }
            else
            {
                neighbor = HexMath.Neighbor(sourceHex, directionIndex);
            }

            if (world.HexWorld != null &&
                world.HexWorld.TryGetTile(neighbor, out var tile) &&
                tile != null)
            {
                terrain = tile.Terrain;
                passable = IsGroundPassable(world.HexWorld, neighbor);
            }
        }

        static string _lastEdgeLogKey = string.Empty;

        static void LogEdgeAttempt(
            SimulationWorld world,
            PlayerPartyRuntime party,
            int directionIndex,
            HexCoord sourceHex,
            HexCoord neighbor,
            HexTerrainType terrain,
            bool passable,
            Result result)
        {
            if (PlayerPartyWorldLocationDebug.Sink == null)
                return;

            var active = party != null && party.HasActive
                ? party.ActiveCharacterId.Value.ToString()
                : "none";
            var motion = world?.PlayerPartyTravel;
            var msg =
                "[PlayerPartyEdgeTransition] attempt" +
                " active=" + active +
                " exitDir=" + directionIndex +
                " sourceKind=" + (motion != null ? motion.LocationKind.ToString() : "?") +
                " sourceSite=" + (motion?.SiteId ?? "") +
                " sourceHex=" + sourceHex +
                " neighbor=" + neighbor +
                " terrain=" + terrain +
                " passable=" + passable +
                " result=" + (result.IsSuccess ? "OK" : FormatResultError(result)) +
                " afterKind=" + (motion != null ? motion.LocationKind.ToString() : "?") +
                " afterHex=" + (motion != null ? motion.CurrentHex.ToString() : "?");
            if (msg == _lastEdgeLogKey)
                return;
            _lastEdgeLogKey = msg;
            PlayerPartyWorldLocationDebug.Sink(msg);
        }

        static string FormatResultError(Result result)
        {
            if (result.IsSuccess)
                return "OK";
            try
            {
                return result.Error.ToString();
            }
            catch
            {
                return "FAIL";
            }
        }

        static bool TryPickOutermostFootprintHex(WorldSite site, int directionIndex, out HexCoord outerHex)
        {
            outerHex = default;
            if (site == null)
                return false;

            var dir = HexMath.AxialDirections[NormalizeDirection(directionIndex)];
            var found = false;
            var bestScore = int.MinValue;
            foreach (var hex in site.EnumerateFootprintHexes())
            {
                var neighbor = HexMath.Neighbor(hex, directionIndex);
                if (site.OccupiesHex(neighbor))
                    continue;

                var score = hex.Q * dir.Q + hex.R * dir.R;
                if (!found ||
                    score > bestScore ||
                    (score == bestScore &&
                     (hex.Q < outerHex.Q || (hex.Q == outerHex.Q && hex.R < outerHex.R))))
                {
                    found = true;
                    bestScore = score;
                    outerHex = hex;
                }
            }

            return found;
        }

        static bool IsGroundPassable(HexWorld grid, HexCoord coord)
        {
            if (grid == null || !grid.TryGetTile(coord, out var tile) || tile == null)
                return false;
            if (tile.Terrain == HexTerrainType.Water)
                return false;
            if (!tile.IsPassable)
                return false;
            return true;
        }

        static int NormalizeDirection(int directionIndex)
        {
            var d = directionIndex % 6;
            if (d < 0)
                d += 6;
            return d;
        }

        static void ApplyTravelingMembersAtHex(SimulationWorld world, HexCoord hex)
        {
            if (world?.WorldPresence == null || world.PlayerPartyTravel == null)
                return;
            var members = world.PlayerPartyTravel.TravelingMembers;
            for (var i = 0; i < members.Count; i++)
            {
                var id = members[i];
                if (id.IsNone)
                    continue;
                world.WorldPresence.SetAtHex(id, hex);
            }
        }
    }
}
