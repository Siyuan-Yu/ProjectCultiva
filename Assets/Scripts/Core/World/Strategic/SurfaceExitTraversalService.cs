using System;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Surface Exit 的唯一结构性预检：零领域 mutation，可供 Host 显示与各类执行器共用。</summary>
    public readonly struct PreparedSurfaceExitTraversal
    {
        public PreparedSurfaceExitTraversal(
            HexCoord destinationHex,
            string destinationLocalMapId,
            WorldSite destinationSite,
            SurfaceExitConnection destinationIngress)
        {
            DestinationHex = destinationHex;
            DestinationLocalMapId = destinationLocalMapId ?? string.Empty;
            DestinationSite = destinationSite;
            DestinationIngress = destinationIngress;
        }

        public HexCoord DestinationHex { get; }
        public string DestinationLocalMapId { get; }
        public WorldSite DestinationSite { get; }
        public SurfaceExitConnection DestinationIngress { get; }
        public bool EntersWorldSite => DestinationSite != null;
    }

    public static class SurfaceExitTraversalService
    {
        public static Result TryPrepareTraversal(
            SimulationWorld world,
            PlayerPartyRuntime party,
            SurfaceExitConnection connection,
            out PreparedSurfaceExitTraversal prepared)
        {
            prepared = default;
            var motion = world?.PlayerPartyTravel;
            if (world == null || party == null || !party.HasActive || motion == null || !motion.HasPosition)
                return Result.Failure(ErrorCode.InvalidArgument, "Surface exit traversal requires world, party and motion.");

            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldPosition)
            {
                if (!connection.SourceHex.Equals(motion.CurrentHex))
                    return Result.Failure(ErrorCode.InvalidOperation, "Exit source is not current wilderness hex.");
            }
            else if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                     !string.IsNullOrEmpty(motion.SiteId))
            {
                if (world.Strategic?.Sites == null ||
                    !world.Strategic.Sites.TryGet(motion.SiteId, out var sourceSite) || sourceSite == null ||
                    !sourceSite.OccupiesHex(connection.SourceHex) ||
                    sourceSite.OccupiesHex(connection.DestinationHex))
                    return Result.Failure(ErrorCode.InvalidOperation, "Exit does not leave current WorldSite.");
            }
            else
            {
                return Result.Failure(ErrorCode.InvalidOperation, "Current context cannot traverse a surface exit.");
            }

            if (world.HexWorld == null ||
                !world.HexWorld.TryGetTile(connection.DestinationHex, out var tile) || tile == null ||
                !tile.IsPassable || tile.Terrain == HexTerrainType.Water)
                return Result.Failure(ErrorCode.InvalidOperation, "Exit destination hex is impassable.");

            WorldSite destinationSite = null;
            var hasDestinationSite = world.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGetAtHex(connection.DestinationHex, out destinationSite) &&
                destinationSite != null;
            if (hasDestinationSite !=
                (connection.DestinationKind == SurfaceExitDestinationKind.WorldSite))
                return Result.Failure(ErrorCode.InvalidOperation, "Exit destination kind disagrees with topology.");

            if (hasDestinationSite)
            {
                if (!string.IsNullOrEmpty(connection.DestinationSiteId) &&
                    !string.Equals(connection.DestinationSiteId, destinationSite.SiteId, StringComparison.Ordinal))
                    return Result.Failure(ErrorCode.InvalidOperation, "Exit destination SiteId disagrees with topology.");
                var admission = StrategicWorldSiteAccessService.CanTransitionPlayerPartyIntoWorldSite(
                    world, destinationSite.SiteId);
                if (admission.IsFailure)
                    return admission;
                var mapId = WorldTravelService.ResolveWorldSiteLocalMapId(destinationSite);
                var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
                if (!WorldSiteFootprintExitConnectionResolver.TryResolveFormalIngressConnection(
                        world,
                        destinationSite,
                        connection.DestinationHex,
                        connection.SourceHex,
                        hexSize,
                        out var ingress))
                    return Result.Failure(ErrorCode.InvalidOperation, "Destination exact ingress edge cannot be resolved.");
                prepared = new PreparedSurfaceExitTraversal(
                    connection.DestinationHex, mapId, destinationSite, ingress);
                return Result.Success();
            }

            if (!WildernessLocalMapFallback.TryResolve(
                    world, connection.DestinationHex, out var wildernessMap) ||
                string.IsNullOrEmpty(wildernessMap))
                return Result.Failure(ErrorCode.InvalidOperation, "No wilderness fallback LocalMap for exit hex.");
            prepared = new PreparedSurfaceExitTraversal(
                connection.DestinationHex, wildernessMap, null, default);
            return Result.Success();
        }
    }
}
