using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Persistence
{
    /// <summary>SPACE-01 Separate Space Session 进 Snapshot（additive）。</summary>
    public static class SeparateSpaceSessionSnapshotRestore
    {
        public static void Capture(SimulationWorld world, StrategicSnapshotDto dto, PlayerPartyRuntime party)
        {
            if (world?.LocalMap == null || dto == null)
                return;

            var session = world.LocalMap;
            if (!session.IsActive)
            {
                dto.SeparateSpace = null;
                return;
            }

            var snap = new SeparateSpaceSessionSnapshotDto
            {
                IsInSeparateSpace = true,
                SpaceKind = (int)session.SpaceKind,
                ActiveMapLayoutId = session.ActiveMapLayoutId ?? string.Empty,
                ActiveLocalPlaceSetId = session.ActiveLocalPlaceSetId ?? string.Empty,
                EntryLocationId = session.EntryLocationId ?? string.Empty,
                ReturnLocationId = session.ReturnLocationId ?? string.Empty,
                ReturnSurfaceId = session.ReturnSurfaceId ?? string.Empty,
                ReturnWorldX = session.ReturnWorldX,
                ReturnWorldY = session.ReturnWorldY,
                HasOutdoorReturn = session.HasOutdoorReturn,
                ActiveCharacterId = party != null && party.HasActive ? party.ActiveCharacterId.Value : 0UL,
                EntryReason = session.EntryReason ?? string.Empty
            };

            for (var i = 0; i < session.OccupantIds.Count; i++)
            {
                var id = session.OccupantIds[i];
                if (!id.IsNone)
                    snap.OccupantIds.Add(id.Value);
            }

            dto.SeparateSpace = snap;
        }

        public static Result Restore(SimulationWorld world, StrategicSnapshotDto dto)
        {
            if (world?.LocalMap == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World LocalMap missing.");

            var snap = dto?.SeparateSpace;
            if (snap == null || !snap.IsInSeparateSpace)
                return Result.Success();

            if (string.IsNullOrWhiteSpace(snap.ActiveMapLayoutId))
                return Result.Failure(ErrorCode.SnapshotInvalid,
                    "SeparateSpace snapshot lacks current ActiveMapLayoutId authority and requires offline conversion.");

            if (snap.HasOutdoorReturn)
            {
                if (string.IsNullOrWhiteSpace(snap.ReturnSurfaceId) ||
                    !IsFinite(snap.ReturnWorldX) || !IsFinite(snap.ReturnWorldY))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "SeparateSpace outdoor return authority is incomplete and requires offline conversion.");
            }
            else if (string.IsNullOrWhiteSpace(snap.ReturnLocationId))
                return Result.Failure(ErrorCode.SnapshotInvalid,
                    "SeparateSpace snapshot lacks current return authority and requires offline conversion.");

            var session = world.LocalMap;
            session.Clear();
            session.EstablishSeparateSpace(
                snap.ActiveMapLayoutId.Trim(),
                snap.ActiveLocalPlaceSetId ?? string.Empty,
                (SeparateSpaceKind)snap.SpaceKind,
                snap.EntryLocationId ?? string.Empty,
                snap.ReturnLocationId ?? string.Empty,
                snap.EntryReason ?? "restore");
            session.HasOutdoorReturn = snap.HasOutdoorReturn;
            session.ReturnSurfaceId = snap.ReturnSurfaceId ?? string.Empty;
            session.ReturnWorldX = snap.ReturnWorldX;
            session.ReturnWorldY = snap.ReturnWorldY;

            session.ClearOccupants();
            if (snap.OccupantIds != null)
            {
                for (var i = 0; i < snap.OccupantIds.Count; i++)
                {
                    var id = new EntityId(snap.OccupantIds[i]);
                    if (!id.IsNone)
                        session.AddOccupant(id);
                }
            }

            world.PartyWorld.LocalMapId = session.ActiveMapLayoutId;
            world.PartyWorld.SiteId = string.Empty;
            world.PartyWorld.Mode = PartyWorldPresenceMode.InSeparateSpace;
            SeparateSpaceTransitionService.ReconcileActiveSeparateSpaceWorldPresence(world);
            return Result.Success();
        }

        static bool IsFinite(float v) =>
            !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
