using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>Builds farm administrative anchors directly from checked-in Surface content.</summary>
    public static class OutdoorAdministrativeAssetAnchorBootstrap
    {
        public static Result Rehydrate(SimulationWorld world, DefinitionRegistry registry)
        {
            if (world?.OutdoorAdministrativeAssetAnchors == null || registry == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Outdoor administrative asset anchor bootstrap args null.");
            var board = world.OutdoorAdministrativeAssetAnchors;
            board.Clear();
            foreach (var pair in registry.OutdoorSurfaces)
            {
                var surface = pair.Value;
                if (surface == null || surface.AcceptanceOnly || surface.SitePlacements == null)
                    continue;
                for (var i = 0; i < surface.SitePlacements.Count; i++)
                {
                    var placement = surface.SitePlacements[i];
                    if (placement == null ||
                        !OutdoorAdministrativeAssetSemantics.IsAdministrativeAssetKind(placement.Kind))
                        continue;
                    if (string.IsNullOrWhiteSpace(placement.StableId) ||
                        placement.WorldWidth <= 0f || placement.WorldHeight <= 0f)
                        return Invalid(placement, "invalid stable identity or world bounds");
                    if (!OutdoorStatefulObjectSemantics.UsesPerCellIdentity(placement.Kind))
                    {
                        if (!Register(board, placement.StableId, surface.SurfaceId, placement.Kind,
                                placement.WorldX + placement.WorldWidth * .5f,
                                placement.WorldY + placement.WorldHeight * .5f,
                                placement.BoundLocationId))
                            return Invalid(placement, "duplicate or invalid centered asset anchor");
                        continue;
                    }

                    if (placement.SourceCellsW <= 0 || placement.SourceCellsH <= 0)
                        return Invalid(placement, "invalid per-cell dimensions");
                    var cellWidth = placement.WorldWidth / placement.SourceCellsW;
                    var cellHeight = placement.WorldHeight / placement.SourceCellsH;
                    for (var y = 0; y < placement.SourceCellsH; y++)
                    for (var x = 0; x < placement.SourceCellsW; x++)
                    {
                        var stableId = OutdoorStatefulObjectId.ForCell(placement.StableId, x, y);
                        if (!Register(board, stableId, surface.SurfaceId, placement.Kind,
                                placement.WorldX + (x + .5f) * cellWidth,
                                placement.WorldY + (y + .5f) * cellHeight,
                                placement.BoundLocationId))
                            return Invalid(placement, "duplicate or invalid per-cell asset anchor: " + stableId);
                    }
                }
            }
            foreach (var asset in world.OutdoorConstructedAssets.Assets.Values)
            {
                if (!OutdoorAdministrativeAssetSemantics.IsAdministrativeAssetKind(asset.Kind))
                    continue;
                var physical = XianXia.Core.Construction.OutdoorFactionConstructionAuthorizationService.ValidatePhysicalPlacement(world, asset);
                if (physical.IsFailure) return physical;
                foreach (var anchor in asset.AdministrativeCellAnchors())
                    if (!board.TryRegister(anchor))
                        return Result.Failure(ErrorCode.ContentLoadFailed, "Runtime farm anchor collision.", anchor.StableAssetId);
            }
            return Result.Success();
        }

        static bool Register(
            OutdoorAdministrativeAssetAnchorBoard board,
            string stableId,
            string surfaceId,
            string kind,
            float worldX,
            float worldY,
            string boundLocationId) =>
            board.TryRegister(new OutdoorAdministrativeAssetAnchor(
                stableId, surfaceId, worldX, worldY, kind, boundLocationId));

        static Result Invalid(OutdoorSurfacePlacementDefinition placement, string reason) =>
            Result.Failure(
                ErrorCode.ContentLoadFailed,
                "Outdoor administrative asset anchor bootstrap failed.",
                "Placement=" + (placement?.StableId ?? string.Empty) + " Reason=" + reason);
    }
}
