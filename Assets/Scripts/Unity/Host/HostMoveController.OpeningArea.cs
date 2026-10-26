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
            if (!HuangcunOpeningAccess.AllowsPosition(world, runtime.ActiveSurfaceId, x, y)) return false;
            if (world.PlayerPartyTravel != null && world.PlayerPartyTravel.HasPosition &&
                world.Strategic.PlayerPartyContext?.ActiveCharacterId == id)
                return HuangcunOpeningAccess.AllowsPlayerMove(world, runtime.ActiveSurfaceId,
                    world.PlayerPartyTravel.WorldPosition.X, world.PlayerPartyTravel.WorldPosition.Y, x, y);
            return true;
        }

        string OpeningMoveBlockReason(EntityId id, Vector3 point)
        {
            var world = bootstrap?.Session?.World;
            var runtime = bootstrap?.ContinuousOutdoorSurfaceRuntime;
            if (world?.PlayerPartyTravel == null || runtime?.Mapper == null ||
                world.Strategic.PlayerPartyContext?.ActiveCharacterId != id)
                return HuangcunOpeningAccess.BlockedMessage;
            runtime.Mapper.PresentationToWorld(point.x, point.y, out var x, out var y);
            return HuangcunOpeningAccess.AllowsPosition(world, runtime.ActiveSurfaceId, x, y) &&
                !HuangcunOpeningAccess.AllowsPlayerMove(world, runtime.ActiveSurfaceId,
                    world.PlayerPartyTravel.WorldPosition.X,
                    world.PlayerPartyTravel.WorldPosition.Y, x, y)
                ? HuangcunOpeningAccess.PatrolBlockedMessage
                : HuangcunOpeningAccess.BlockedMessage;
        }
    }
}
