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

            // Authored and player-built rooms are already normalized onto the same physical board.
            // Derive this compatibility lookup once from that authority to avoid double-registering
            // authored storage rooms.
            foreach (var asset in world.OutdoorConstructedAssets.Assets.Values)
            {
                if (!string.Equals(asset.Kind, OutdoorConstructedAssetSemantics.StorageRoomKind,
                        StringComparison.Ordinal)) continue;
                var registered = Register(world, asset.StableAssetId, asset.BoundWorldSiteId, asset.SurfaceId,
                    "储藏室", asset.WorldX, asset.WorldY, asset.WorldWidth, asset.WorldHeight);
                if (registered.IsFailure) return registered;
            }
            return Result.Success();
        }

        static Result Register(SimulationWorld world, string roomId, string siteId, string surfaceId,
            string displayName, float footprintWorldX, float footprintWorldY,
            float worldWidth, float worldHeight)
        {
            if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(siteId) ||
                !world.Strategic.Sites.TryGet(siteId, out _) ||
                !world.SiteStorageRooms.TryRegister(new WorldSiteStorageRoomState {
                    StorageRoomId = roomId, SiteId = siteId, SurfaceId = surfaceId,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? "储藏室" : displayName,
                    WorldX = footprintWorldX + worldWidth * .5f,
                    WorldY = footprintWorldY + worldHeight * .5f,
                    FootprintWorldX = footprintWorldX, FootprintWorldY = footprintWorldY,
                    WorldWidth = worldWidth, WorldHeight = worldHeight
                }))
                return Result.Failure(ErrorCode.ContentLoadFailed,
                    "Storage room binding is invalid or duplicated.", roomId ?? string.Empty);
            return Result.Success();
        }
    }
}
