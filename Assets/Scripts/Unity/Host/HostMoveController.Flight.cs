using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;

namespace XianXia.Unity.Host
{
    public sealed partial class HostMoveController
    {
        bool IsFlying(EntityId id) => FlightService.IsAirborne(bootstrap?.Session?.World, id);

        public bool IsAirPointAllowed(EntityId id, Vector3 point)
        {
            var world = bootstrap?.Session?.World;
            var surface = bootstrap?.ContinuousOutdoorSurfaceRuntime;
            if (world == null || surface?.Mapper == null || !surface.IsActive || world.LocalMap.IsInInterior ||
                float.IsNaN(point.x) || float.IsInfinity(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.y) ||
                _walkGrid == null || !_walkGrid.TryWorldToCell(point.x, point.y, out _, out _)) return false;
            surface.Mapper.PresentationToWorld(point.x, point.y, out var x, out var y);
            var encounter = world.Strategic.CharacterEncounter;
            return FlightService.AllowsAirPosition(world, surface.ActiveSurfaceId,
                new XianXia.Core.World.WorldVec2(x, y)) && (encounter == null || encounter.Contains(x, y));
        }

        bool TryBuildCharacterPath(EntityId id, Vector3 from, Vector3 to, List<Vector3> path, bool exactGoal)
        {
            if (!IsFlying(id)) return TryBuildWorldPath(from, to, path, exactGoal);
            path.Clear();
            if (!IsAirPointAllowed(id, from) || !IsAirPointAllowed(id, to)) return false;
            path.Add(to);
            return true;
        }

        public bool IsLandingPointClear(EntityId id, Vector3 point)
        {
            if (!IsAirPointAllowed(id, point) || !_walkGrid.TryWorldToCell(point.x, point.y, out var x, out var y) ||
                !_walkGrid.IsWalkable(x, y)) return false;
            if (viewSpawner == null) return false;
            foreach (var other in viewSpawner.Registry.All)
                if (other != null && other.EntityId != id && !IsFlying(other.EntityId) && IsCharacterOccupant(other) &&
                    FeetOverlap(point - other.transform.position, StandSpacing(other))) return false;
            return true;
        }

        public bool TryFindLandingPoint(EntityId id, Vector3 from, out Vector3 point)
        {
            point = from;
            if (IsLandingPointClear(id, point)) return true;
            if (_walkGrid == null || !_walkGrid.TryWorldToCell(from.x, from.y, out var cx, out var cy)) return false;
            // Bounded emergency descent; never teleport through a scene or encounter boundary.
            for (var radius = 1; radius <= 24; radius++)
                for (var y = -radius; y <= radius; y++)
                    for (var x = -radius; x <= radius; x++)
                    {
                        if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius) continue;
                        if (!_walkGrid.IsWalkable(cx + x, cy + y)) continue;
                        _walkGrid.CellToWorldCenter(cx + x, cy + y, out var px, out var py);
                        var candidate = new Vector3(px, py, HostPresentationSpace.EntityZ);
                        if (IsLandingPointClear(id, candidate)) { point = candidate; return true; }
                    }
            return false;
        }
    }
}
