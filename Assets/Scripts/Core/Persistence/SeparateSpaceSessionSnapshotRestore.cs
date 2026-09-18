using System.Collections.Generic;
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
                    "SeparateSpace snapshot missing ActiveMapLayoutId.");

            if (snap.HasOutdoorReturn)
            {
                if (string.IsNullOrWhiteSpace(snap.ReturnSurfaceId) ||
                    !IsFinite(snap.ReturnWorldX) || !IsFinite(snap.ReturnWorldY))
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "SeparateSpace outdoor return authority incomplete.");
            }

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
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtHex;
            return Result.Success();
        }

        /// <summary>
        /// 旧档：LocalMapId／placements 指向 Cave 等 Separate Map，但没有 SeparateSpace DTO。
        /// 能重建 return authority 则迁移；否则明确失败，不默默送回荒村。
        /// </summary>
        public static Result TryMigrateLegacySeparateSpace(SimulationWorld world, StrategicSnapshotDto dto)
        {
            if (world?.LocalMap == null || dto == null)
                return Result.Success();

            var mapId = InferLegacySeparateMapId(dto);
            if (string.IsNullOrEmpty(mapId))
                return Result.Success();

            var travel = dto.PlayerPartyTravel;
            if (travel == null || !travel.HasPosition ||
                !IsFinite(travel.WorldX) || !IsFinite(travel.WorldY))
            {
                return Result.Failure(ErrorCode.SnapshotInvalid,
                    "Legacy Separate Space save lacks outdoor return WorldPosition; cannot migrate safely.",
                    mapId);
            }

            var surfaceId = InferReturnSurfaceId(world, travel);
            if (string.IsNullOrEmpty(surfaceId))
            {
                return Result.Failure(ErrorCode.SnapshotInvalid,
                    "Legacy Separate Space save lacks resolvable ReturnSurfaceId.",
                    mapId);
            }

            var session = world.LocalMap;
            session.Clear();
            session.EstablishSeparateSpace(
                mapId,
                string.Empty,
                SeparateSpaceResolver.ParseSpaceKind(
                    mapId.IndexOf("cave", System.StringComparison.OrdinalIgnoreCase) >= 0
                        ? "cave"
                        : "separateMap"),
                InferLegacyEntryLocationId(dto, mapId),
                InferLegacyEntryLocationId(dto, mapId),
                "legacy-migrate");
            session.HasOutdoorReturn = true;
            session.ReturnSurfaceId = surfaceId;
            session.ReturnWorldX = travel.WorldX;
            session.ReturnWorldY = travel.WorldY;

            if (dto.LoadedLocalMapCharacterPlacements != null)
            {
                for (var i = 0; i < dto.LoadedLocalMapCharacterPlacements.Count; i++)
                {
                    var p = dto.LoadedLocalMapCharacterPlacements[i];
                    if (p == null || p.CharacterId == 0)
                        continue;
                    if (!string.Equals(p.LocalMapId, mapId, System.StringComparison.Ordinal))
                        continue;
                    session.AddOccupant(new EntityId(p.CharacterId));
                }
            }

            world.PartyWorld.LocalMapId = mapId;
            world.PartyWorld.SiteId = string.Empty;
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtHex;
            return Result.Success();
        }

        static string InferLegacySeparateMapId(StrategicSnapshotDto dto)
        {
            if (dto.LoadedLocalMapCharacterPlacements != null)
            {
                for (var i = 0; i < dto.LoadedLocalMapCharacterPlacements.Count; i++)
                {
                    var p = dto.LoadedLocalMapCharacterPlacements[i];
                    if (p == null || string.IsNullOrWhiteSpace(p.LocalMapId))
                        continue;
                    var id = p.LocalMapId.Trim();
                    if (LooksLikeSeparateSpaceMap(id))
                        return id;
                }
            }

            return string.Empty;
        }

        static bool LooksLikeSeparateSpaceMap(string mapId) =>
            !string.IsNullOrEmpty(mapId) &&
            (mapId.IndexOf("cave", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             mapId.IndexOf("dungeon", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             mapId.IndexOf("interior", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             mapId.IndexOf("arena", System.StringComparison.OrdinalIgnoreCase) >= 0);

        static string InferLegacyEntryLocationId(StrategicSnapshotDto dto, string mapId)
        {
            // Best-effort：洞口 identity；缺省留空，Leave 仍可走 Continuous return。
            if (mapId != null &&
                mapId.IndexOf("cave", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "base:loc_ref_cave";
            return string.Empty;
        }

        static string InferReturnSurfaceId(SimulationWorld world, PlayerPartyTravelSnapshotDto travel)
        {
            if (world?.SurfaceGround == null || travel == null)
                return string.Empty;
            if (world.SurfaceGround.TryResolveContaining(
                    new WorldVec2(travel.WorldX, travel.WorldY), out var nav) &&
                nav != null)
                return nav.SurfaceId ?? string.Empty;
            var active = world.SurfaceGround.Active;
            return active != null ? active.SurfaceId ?? string.Empty : string.Empty;
        }

        static bool IsFinite(float v) =>
            !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
