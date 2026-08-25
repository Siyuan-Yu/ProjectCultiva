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
