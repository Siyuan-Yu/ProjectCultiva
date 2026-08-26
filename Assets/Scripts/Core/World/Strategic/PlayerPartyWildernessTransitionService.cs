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
            var gate = motion.SurfaceEdgeGate;
            if (gate != null && !gate.CanAttemptEdgeTransition)
                return Result.Failure(ErrorCode.InvalidOperation, "Edge transition gated (in progress or disarmed).");

            var dir = NormalizeDirection(directionIndex);
            ProbeNeighbor(world, motion, dir, out var sourceHex, out var neighbor, out var passable, out var terrain);

            gate?.BeginTransition(dir);

            Result result;
            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite)
                result = TryExitWorldSiteByDirection(world, party, dir);
            else
                result = TryCrossWildernessEdge(world, party, dir);

            if (result.IsFailure)
            {
                // 失败：恢复为可再试（保持 Armed，清 InProgress）
                gate?.ClearEdgeState();
            }

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
            // 若 spawn 仍在近缘，强制推到 Entry Interior Inset。
            var entryDir = WildernessLocalWorldProjection.OppositeDirection(exitDir);
            if (!WildernessLocalWorldProjection.IsInSafeInterior(spawnLocalX, spawnLocalY, bounds))
            {
                WildernessLocalWorldProjection.GetLocalPositionNearEdge(
                    bounds, entryDir, out spawnLocalX, out spawnLocalY);
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
            if (world == null || party == null || !party.HasActive)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid wilderness edge args.");
            var motion = world.PlayerPartyTravel;
            if (motion == null || !motion.HasPosition)
                return Result.Failure(ErrorCode.InvalidOperation, "Party has no world position.");
            if (motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition)
                return Result.Failure(ErrorCode.InvalidOperation, "Not in continuous wilderness position.");
            if (motion.IsMoving)
                return Result.Failure(ErrorCode.InvalidOperation, "Stop travel before crossing hex edge.");

            directionIndex = NormalizeDirection(directionIndex);
            var currentHex = motion.CurrentHex;
            var neighbor = HexMath.Neighbor(currentHex, directionIndex);
            if (!IsGroundPassable(world.HexWorld, neighbor))
                return Result.Failure(ErrorCode.InvalidOperation, "Neighbor hex is impassable.");

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var newWorldPos = WildernessLocalWorldProjection.ComputeCrossEdgeWorldPosition(
                currentHex,
                neighbor,
                motion.WorldPosition,
                hexSize);
            var derived = HexMath.WorldToHex(newWorldPos.X, newWorldPos.Y, hexSize);

            motion.SetAtWorldPosition(newWorldPos, derived);
            ApplyTravelingMembersAtHex(world, derived);

            if (world.Strategic.Sites.TryGetAtHex(neighbor, out var site) && site != null)
                return PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, site);

            if (!WildernessLocalMapFallback.TryResolve(world, neighbor, out var mapId) ||
                string.IsNullOrEmpty(mapId))
                return Result.Failure(ErrorCode.InvalidOperation, "No wilderness fallback LocalMap for neighbor.");

            return WorldTravelService.EnterWildernessLocalMap(world, neighbor, mapId);
        }

        public static Result TryExitWorldSiteByDirection(
            SimulationWorld world,
            PlayerPartyRuntime party,
            int directionIndex)
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

            directionIndex = NormalizeDirection(directionIndex);
            if (!TryPickOutermostFootprintHex(site, directionIndex, out var outerHex))
                return Result.Failure(ErrorCode.InvalidOperation, "No site edge in that direction.");

            var external = HexMath.Neighbor(outerHex, directionIndex);
            if (site.OccupiesHex(external))
                return Result.Failure(ErrorCode.InvalidOperation, "No external neighbor outside footprint.");
            if (!IsGroundPassable(world.HexWorld, external))
                return Result.Failure(ErrorCode.InvalidOperation, "External hex is impassable.");

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var worldPos = WildernessLocalWorldProjection.ComputeCrossEdgeWorldPosition(
                outerHex,
                external,
                motion.WorldPosition,
                hexSize);
            var derived = HexMath.WorldToHex(worldPos.X, worldPos.Y, hexSize);

            motion.SetAtWorldPosition(worldPos, derived);
            ApplyTravelingMembersAtHex(world, derived);

            if (!WildernessLocalMapFallback.TryResolve(world, external, out var mapId) ||
                string.IsNullOrEmpty(mapId))
                return Result.Failure(ErrorCode.InvalidOperation, "No wilderness fallback LocalMap for exit hex.");

            return WorldTravelService.EnterWildernessLocalMap(world, external, mapId);
        }

        /// <summary>
        /// 只读评估某方向出口是否合法（不触发 Transition、不改 Gate）。
        /// Presentation 与 Detection 共用。
        /// </summary>
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
                if (!TryPickOutermostFootprintHex(site, dir, out var outer))
                    return false;
                var external = HexMath.Neighbor(outer, dir);
                if (site.OccupiesHex(external))
                {
                    passable = false;
                    return true;
                }

                neighborHex = external;
                passable = IsGroundPassable(world.HexWorld, external);
                return true;
            }

            if (motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition)
                return false;

            passable = IsGroundPassable(world.HexWorld, neighborHex);
            return true;
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
