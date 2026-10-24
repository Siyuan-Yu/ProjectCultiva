using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    public sealed partial class HostMoveController
    {
        bool IsOpeningMoveAllowed(EntityId id, Vector3 point)
        {
            var world = bootstrap?.Session?.World;
            if (!HuangcunOpeningAccess.IsLocked(world)) return true;
            // Tactical/interior coordinates are not outdoor canonical coordinates.
            if (world.LocalMap.IsInInterior || CharacterEncounterService.OwnsParticipantSpatialState(world, id))
                return true;
            if (!HuangcunOpeningAccess.IsSimulated(world, id)) return false;
            var runtime = bootstrap.ContinuousOutdoorSurfaceRuntime;
            if (runtime?.Mapper == null || !runtime.IsActive) return false;
            runtime.Mapper.PresentationToWorld(point.x, point.y, out var x, out var y);
            return HuangcunOpeningAccess.AllowsPosition(world, runtime.ActiveSurfaceId, x, y);
        }
    }
}
