using System;
using XianXia.Core.Construction;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>Rebuilds the derived physical StorageRoom lookup; it never owns PublicStock.</summary>
    public static class WorldSiteStorageRoomBootstrap
    {
        public static Result Rehydrate(SimulationWorld world, DefinitionRegistry registry)
        {
            if (world?.SiteStorageRooms == null || registry == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Storage room bootstrap args null.");
            world.SiteStorageRooms.Clear();

            foreach (var pair in registry.OutdoorSurfaces)
            {
                var surface = pair.Value;
                if (surface == null || surface.AcceptanceOnly || surface.SitePlacements == null) continue;
                for (var i = 0; i < surface.SitePlacements.Count; i++)
                {
                    var placement = surface.SitePlacements[i];
                    if (placement == null || !string.Equals(placement.Kind,
                            OutdoorConstructedAssetSemantics.StorageRoomKind, StringComparison.Ordinal)) continue;
                    var registered = Register(world, placement.StableId, placement.SiteId, surface.SurfaceId,
                        placement.Label, placement.WorldX + placement.WorldWidth * .5f,
                        placement.WorldY + placement.WorldHeight * .5f);
                    if (registered.IsFailure) return registered;
                }
            }

            foreach (var asset in world.OutdoorConstructedAssets.Assets.Values)
            {
                if (!string.Equals(asset.Kind, OutdoorConstructedAssetSemantics.StorageRoomKind,
                        StringComparison.Ordinal)) continue;
                var registered = Register(world, asset.StableAssetId, asset.BoundWorldSiteId, asset.SurfaceId,
                    "储藏室", asset.WorldX + asset.WorldWidth * .5f, asset.WorldY + asset.WorldHeight * .5f);
                if (registered.IsFailure) return registered;
            }
            return Result.Success();
        }

        static Result Register(SimulationWorld world, string roomId, string siteId, string surfaceId,
            string displayName, float worldX, float worldY)
        {
            if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(siteId) ||
                !world.Strategic.Sites.TryGet(siteId, out _) ||
                !world.SiteStorageRooms.TryRegister(new WorldSiteStorageRoomState {
                    StorageRoomId = roomId, SiteId = siteId, SurfaceId = surfaceId,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? "储藏室" : displayName,
                    WorldX = worldX, WorldY = worldY
                }))
                return Result.Failure(ErrorCode.ContentLoadFailed,
                    "Storage room binding is invalid or duplicated.", roomId ?? string.Empty);
            return Result.Success();
        }
    }
}
