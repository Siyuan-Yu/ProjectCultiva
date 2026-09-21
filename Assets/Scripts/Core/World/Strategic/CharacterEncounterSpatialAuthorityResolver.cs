using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Navigation;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    public enum EncounterSpatialOwnerKind
    {
        Personal = 0,
        PlayerParty = 1,
        LegacyFormalArmy = 2,
        Squad = 3
    }

    /// <summary>Read-only encounter origin resolution; organization authority is never rewritten.</summary>
    public static class CharacterEncounterSpatialAuthorityResolver
    {
        public static bool TryResolveEncounterWorldPosition(SimulationWorld world, EntityId id,
            string sourceSurfaceId, out WorldVec2 position, out EncounterSpatialOwnerKind owner,
            out string ownerId, out string failure)
        {
            var resolved = ContinuousCharacterSpatialAuthorityResolver.TryResolveWorldPosition(
                world, id, sourceSurfaceId, out position, out var spatialOwner,
                out ownerId, out failure);
            owner = (EncounterSpatialOwnerKind)spatialOwner;
            return resolved;
        }

        public static string DescribeFailure(SimulationWorld world, EntityId id, string squadId,
            string requestedSurface, EncounterSpatialOwnerKind owner, string failure)
        {
            WorldAgentPresence personal = null;
            if (world != null) world.WorldPresence.TryGet(id, out personal);
            SquadState squad = null;
            SquadWorldMotionState motion = null;
            if (world != null && world.Strategic.Squads.TryGetForCharacter(id, out squad))
                world.Strategic.SquadWorldMotions.TryGet(squad.SquadId, out motion);
            var party = world?.Strategic?.PlayerPartyContext;
            var partyMotion = world?.PlayerPartyTravel;
            var partySurface = "";
            if (partyMotion?.HasPosition == true &&
                world.SurfaceGround.TryResolveContaining(partyMotion.WorldPosition,
                    out var partyNavigation))
                partySurface = partyNavigation.SurfaceId;
            return failure + " CharacterId=" + id.Value + " SquadId=" + (squadId ?? "") +
                   " SpatialOwnerKind=" + owner + " OwnerSquadId=" + (squad?.SquadId ?? "") +
                   " HasPersonalPosition=" + (personal?.HasContinuousWorldPosition == true) +
                   " PersonalSurfaceId=" + (personal?.PersonalSurfaceId ?? "") +
                   " SquadSurfaceId=" + (motion?.SurfaceId ?? "") +
                   " SquadWorldPosition=" + (motion?.HasPosition == true
                       ? motion.WorldPosition.ToString() : "none") +
                   " IsPlayerPartyMember=" + (party?.IsMember(id) == true) +
                   " IsActiveCharacter=" + (party?.IsActive(id) == true) +
                   " PartyHasPosition=" + (partyMotion?.HasPosition == true) +
                   " PartyLocationKind=" + (partyMotion?.LocationKind.ToString() ?? "none") +
                   " PartySiteId=" + (partyMotion?.SiteId ?? "") +
                   " PartyWorldPosition=" + (partyMotion?.HasPosition == true
                       ? partyMotion.WorldPosition.ToString() : "none") +
                   " PartySurfaceId=" + partySurface +
                   " RequestedSurface=" + (requestedSurface ?? "");
        }
    }

    /// <summary>
    /// Resolves independent-field Tactical placement without changing ordinary-world Origin authority.
    /// All connectivity checks run against the final prepared encounter grid.
    /// </summary>
    public static class EncounterInitialTacticalPlacementResolver
    {
        static readonly int[] StepX = { 1, 0, -1, 0 };
        static readonly int[] StepY = { 0, 1, 0, -1 };

        public static bool TryResolve(
            WalkGrid grid,
            WorldVec2 origin,
            bool hasPreferred,
            WorldVec2 preferred,
            string preferredSource,
            Func<WorldVec2, WorldVec2> worldToGrid,
            Func<WorldVec2, WorldVec2> gridToWorld,
            Func<WorldVec2, bool> containsWorld,
            HashSet<long> occupiedCells,
            int maxRadius,
            out WorldVec2 tactical,
            out string source,
            out int cellX,
            out int cellY,
            out string failure)
        {
            tactical = default;
            source = string.Empty;
            cellX = cellY = -1;
            failure = string.Empty;
            if (grid == null || worldToGrid == null || gridToWorld == null || containsWorld == null ||
                occupiedCells == null || maxRadius < 1)
            { failure = "InvalidPlacementContext"; return false; }

            if (hasPreferred && TryAcceptExact(grid, preferred, worldToGrid, containsWorld,
                    occupiedCells, out cellX, out cellY))
            {
                tactical = preferred;
                source = string.IsNullOrEmpty(preferredSource) ? "CurrentView" : preferredSource;
                return true;
            }
            if (TryAcceptExact(grid, origin, worldToGrid, containsWorld,
                    occupiedCells, out cellX, out cellY))
            {
                tactical = origin;
                source = "Origin";
                return true;
            }

            var originGrid = worldToGrid(origin);
            if (!grid.TryWorldToCell(originGrid.X, originGrid.Y, out var originX, out var originY))
            { failure = "OriginOutsidePreparedGrid"; return false; }

            var seeds = new List<int>(4);
            if (grid.IsWalkable(originX, originY)) seeds.Add(Index(grid, originX, originY));
            else
                for (var i = 0; i < StepX.Length; i++)
                {
                    var x = originX + StepX[i];
                    var y = originY + StepY[i];
                    if (grid.IsWalkable(x, y)) seeds.Add(Index(grid, x, y));
                }
            if (seeds.Count == 0)
            { failure = "NoAdjacentConnectedWalkableSide"; return false; }

            var reachable = CollectReachable(grid, seeds[0], originX, originY, maxRadius);
            for (var i = 1; i < seeds.Count; i++)
                if (!reachable.Contains(seeds[i]))
                { failure = "BlockedOriginHasDisconnectedSides"; return false; }

            var queue = new Queue<int>();
            var visited = new HashSet<int>();
            queue.Enqueue(seeds[0]);
            visited.Add(seeds[0]);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var x = current % grid.Width;
                var y = current / grid.Width;
                grid.CellToWorldCenter(x, y, out var gx, out var gy);
                var candidate = gridToWorld(new WorldVec2(gx, gy));
                var key = CellKey(x, y);
                if (containsWorld(candidate) && !occupiedCells.Contains(key))
                {
                    occupiedCells.Add(key);
                    tactical = candidate;
                    source = "BoundedConnectedCorrection";
                    cellX = x;
                    cellY = y;
                    return true;
                }
                for (var i = 0; i < StepX.Length; i++)
                {
                    var nx = x + StepX[i];
                    var ny = y + StepY[i];
                    if (Math.Abs(nx - originX) > maxRadius || Math.Abs(ny - originY) > maxRadius ||
                        !grid.IsWalkable(nx, ny)) continue;
                    var next = Index(grid, nx, ny);
                    if (visited.Add(next)) queue.Enqueue(next);
                }
            }
            failure = "NoDistinctConnectedTacticalCellWithinRadius";
            return false;
        }

        static bool TryAcceptExact(WalkGrid grid, WorldVec2 point,
            Func<WorldVec2, WorldVec2> worldToGrid, Func<WorldVec2, bool> containsWorld,
            HashSet<long> occupied, out int x, out int y)
        {
            x = y = -1;
            if (!containsWorld(point)) return false;
            var projected = worldToGrid(point);
            if (!grid.TryWorldToCell(projected.X, projected.Y, out x, out y) ||
                !grid.IsWalkable(x, y) || occupied.Contains(CellKey(x, y))) return false;
            occupied.Add(CellKey(x, y));
            return true;
        }

        static HashSet<int> CollectReachable(WalkGrid grid, int seed,
            int originX, int originY, int maxRadius)
        {
            var result = new HashSet<int> { seed };
            var queue = new Queue<int>();
            queue.Enqueue(seed);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var x = current % grid.Width;
                var y = current / grid.Width;
                for (var i = 0; i < StepX.Length; i++)
                {
                    var nx = x + StepX[i];
                    var ny = y + StepY[i];
                    if (Math.Abs(nx - originX) > maxRadius || Math.Abs(ny - originY) > maxRadius ||
                        !grid.IsWalkable(nx, ny)) continue;
                    var next = Index(grid, nx, ny);
                    if (result.Add(next)) queue.Enqueue(next);
                }
            }
            return result;
        }

        static int Index(WalkGrid grid, int x, int y) => y * grid.Width + x;
        static long CellKey(int x, int y) => ((long)x << 32) ^ (uint)y;
    }
}
