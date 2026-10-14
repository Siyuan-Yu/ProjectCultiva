using System;
using System.Collections.Generic;
using XianXia.Core.Construction;
using XianXia.Core.Npc;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>Projects authored rescue/drop-off objects into the stable Core physical-point board.</summary>
    public static class AuthoredTransportPointBootstrap
    {
        public static Result ApplyNewGame(SimulationWorld world, DefinitionRegistry registry) => Apply(world, registry, false);
        public static Result ValidateRestored(SimulationWorld world, DefinitionRegistry registry) => Apply(world, registry, true);

        public static Result ApplyInitialBedAssignments(SimulationWorld world)
        {
            if (world == null) return Result.Failure(ErrorCode.InvalidArgument, "World is required.");
            var assignedCharacters = new HashSet<ulong>();
            var assignmentCount = 0;
            foreach (var asset in world.OutdoorConstructedAssets.Assets.Values)
            {
                if (!RoomBedGeometry.IsBed(asset) || string.IsNullOrWhiteSpace(asset.InitialAssigneeSpawnKey)) continue;
                if (!world.OpeningSpawnIdentities.TryGetEntity(asset.InitialAssigneeSpawnKey, out var assignee))
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "Authored bed assignee SpawnKey is missing.", asset.InitialAssigneeSpawnKey);
                if (!RoomBedFacilityService.BedHasUsage(world, asset, RoomUsage.Residential))
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "Only residential beds may define an initial assignee.", asset.StableAssetId);
                if (!assignedCharacters.Add(assignee.Value))
                    return Result.Failure(ErrorCode.ContentLoadFailed,
                        "Authored bed assignee is duplicated.", asset.InitialAssigneeSpawnKey);
                asset.AssignedCharacterId = assignee;
                assignmentCount++;
                if (world.Entities.TryGet(assignee, out var entity) &&
                    entity.TryGet<ActivityTendencyComponent>(out var tendency))
                    tendency.HomeWorkAreaId = string.Empty;
                if (world.Civilians.TryGet(assignee, out var civilian))
                    civilian.ResidenceWorkAreaId = string.Empty;
            }
            if (assignmentCount != 20)
                return Result.Failure(ErrorCode.ContentLoadFailed,
                    "Huangcun must resolve exactly 20 authored residential bed assignments.", assignmentCount.ToString());
            return Result.Success();
        }

        static Result Apply(SimulationWorld world, DefinitionRegistry registry, bool validateOnly)
        {
            if (world == null || registry == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Authored transport point bootstrap args null.");
            foreach (var pair in registry.OutdoorSurfaces)
            {
                var surface = pair.Value;
                if (surface == null || surface.AcceptanceOnly || surface.SitePlacements == null) continue;
                for (var i = 0; i < surface.SitePlacements.Count; i++)
                {
                    var p = surface.SitePlacements[i];
                    if (p == null || !OutdoorConstructedAssetSemantics.IsAuthoredRuntimeAssetKind(p.Kind)) continue;
                    if (!Enum.TryParse(p.RoomUsage ?? string.Empty, true, out RoomUsage roomUsage))
                        roomUsage = RoomUsage.Residential;
                    var expected = new OutdoorConstructedAssetState {
                        StableAssetId = p.StableId, BuildingId = string.IsNullOrWhiteSpace(p.BuildingId) ? "base:authored:" + p.Kind : p.BuildingId,
                        Kind = p.Kind, SurfaceId = surface.SurfaceId,
                        WorldX = p.WorldX, WorldY = p.WorldY, WorldWidth = p.WorldWidth, WorldHeight = p.WorldHeight,
                        CellsW = Math.Max(1, p.SourceCellsW), CellsH = Math.Max(1, p.SourceCellsH),
                        BoundLocationId = string.IsNullOrWhiteSpace(p.BoundLocationId)
                            ? "location:authored:" + p.StableId
                            : p.BoundLocationId,
                        BoundWorldSiteId = p.SiteId ?? string.Empty,
                        QuarterTurns = p.QuarterTurns,
                        ParentRoomId = p.ParentRoomId ?? string.Empty,
                        RoomUsage = roomUsage,
                        InitialAssigneeSpawnKey = p.InitialAssigneeSpawnKey ?? string.Empty,
                        StructureDurability = p.StructureDurability
                    };
                    if (!expected.IsValid)
                        return Result.Failure(ErrorCode.ContentLoadFailed, "Authored transport point is invalid.", p.StableId);
                    if (!validateOnly)
                    {
                        var geometry = RoomBedGeometry.ValidatePlacement(world.OutdoorConstructedAssets, expected);
                        if (geometry.IsFailure)
                            return Result.Failure(ErrorCode.ContentLoadFailed,
                                "Authored room or bed geometry is invalid: " + geometry.Error.Message, p.StableId);
                        if (!world.OutdoorConstructedAssets.TryRegister(expected))
                            return Result.Failure(ErrorCode.ContentLoadFailed, "Authored transport point is duplicated or overlaps another asset.", p.StableId);
                        continue;
                    }
                    if (!world.OutdoorConstructedAssets.Assets.TryGetValue(p.StableId, out var actual) ||
                        actual.BuildingId != expected.BuildingId || actual.Kind != expected.Kind ||
                        actual.SurfaceId != expected.SurfaceId ||
                        actual.WorldX != expected.WorldX || actual.WorldY != expected.WorldY ||
                        actual.WorldWidth != expected.WorldWidth || actual.WorldHeight != expected.WorldHeight ||
                        actual.CellsW != expected.CellsW || actual.CellsH != expected.CellsH ||
                        actual.BoundLocationId != expected.BoundLocationId || actual.BoundWorldSiteId != expected.BoundWorldSiteId ||
                        actual.QuarterTurns != expected.QuarterTurns || actual.ParentRoomId != expected.ParentRoomId ||
                        actual.InitialAssigneeSpawnKey != expected.InitialAssigneeSpawnKey)
                        return Result.Failure(ErrorCode.SnapshotInvalid, "Authored transport point was not restored exactly.", p.StableId);
                }
            }
            return Result.Success();
        }
    }
}
