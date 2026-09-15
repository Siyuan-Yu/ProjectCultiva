using System;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Resolves an explicitly registered administrative asset through its canonical anchor and the one administrative
    /// authority. A true result with null managingSite is the valid unmanaged state.
    /// </summary>
    public static class WorldAdministrativeAssetControlResolver
    {
        public static bool TryResolve(
            SimulationWorld world,
            string stableAssetId,
            out OutdoorAdministrativeAssetAnchor anchor,
            out WorldSite managingSite,
            out TerritoryClaimState winningClaim)
        {
            anchor = null;
            managingSite = null;
            winningClaim = null;
            if (world?.OutdoorAdministrativeAssetAnchors == null ||
                !world.OutdoorAdministrativeAssetAnchors.TryGet(stableAssetId, out anchor))
                return false;
            WorldSiteAdministrativeControlResolver.TryResolve(
                world, anchor.SurfaceId, anchor.WorldX, anchor.WorldY,
                out managingSite, out winningClaim);
            return true;
        }
    }

    public enum AdministrativeAssetAuthorizationStatus
    {
        Invalid = 0,
        Allowed = 1,
        Unmanaged = 2,
        ManagedByOtherFaction = 3,
        NotAdministrativeAsset = 4
    }

    /// <summary>Read-only result for an organized action requested by one faction.</summary>
    public sealed class AdministrativeAssetAuthorization
    {
        internal AdministrativeAssetAuthorization(
            AdministrativeAssetAuthorizationStatus status,
            string actingFactionId,
            OutdoorAdministrativeAssetAnchor anchor,
            WorldSite managingSite,
            TerritoryClaimState winningClaim)
        {
            Status = status;
            ActingFactionId = actingFactionId ?? string.Empty;
            Anchor = anchor;
            ManagingSite = managingSite;
            WinningClaim = winningClaim;
        }

        public AdministrativeAssetAuthorizationStatus Status { get; }
        public bool IsAllowed => Status == AdministrativeAssetAuthorizationStatus.Allowed;
        public string ActingFactionId { get; }
        public OutdoorAdministrativeAssetAnchor Anchor { get; }
        public WorldSite ManagingSite { get; }
        public string ManagingFactionId => ManagingSite?.OwnerFactionId ?? string.Empty;
        public TerritoryClaimState WinningClaim { get; }
    }

    /// <summary>
    /// Faction authorization for organized work on an explicitly registered administrative asset.
    /// Site succession is resolved on every call; no manager or claim winner is cached here.
    /// </summary>
    public static class WorldAdministrativeAssetAuthorizationService
    {
        public static AdministrativeAssetAuthorization ResolveForFaction(
            SimulationWorld world,
            string stableAssetId,
            string actingFactionId)
        {
            if (world == null || string.IsNullOrWhiteSpace(stableAssetId) ||
                string.IsNullOrWhiteSpace(actingFactionId))
                return Result(AdministrativeAssetAuthorizationStatus.Invalid, actingFactionId);

            if (!WorldAdministrativeAssetControlResolver.TryResolve(
                    world, stableAssetId, out var anchor, out var managingSite, out var winningClaim))
                return Result(AdministrativeAssetAuthorizationStatus.NotAdministrativeAsset,
                    actingFactionId);

            if (managingSite == null)
                return Result(AdministrativeAssetAuthorizationStatus.Unmanaged,
                    actingFactionId, anchor, null, winningClaim);

            var status = string.Equals(
                managingSite.OwnerFactionId, actingFactionId, StringComparison.Ordinal)
                ? AdministrativeAssetAuthorizationStatus.Allowed
                : AdministrativeAssetAuthorizationStatus.ManagedByOtherFaction;
            return Result(status, actingFactionId, anchor, managingSite, winningClaim);
        }

        static AdministrativeAssetAuthorization Result(
            AdministrativeAssetAuthorizationStatus status,
            string actingFactionId,
            OutdoorAdministrativeAssetAnchor anchor = null,
            WorldSite managingSite = null,
            TerritoryClaimState winningClaim = null) =>
            new AdministrativeAssetAuthorization(
                status, actingFactionId, anchor, managingSite, winningClaim);
    }

    /// <summary>Resolves whether a faction may use at least one real farm cell in a WorkArea location.</summary>
    public static class WorldAdministrativeFarmWorkAreaAuthorizationService
    {
        public static bool HasAllowedFarmCell(SimulationWorld world, string locationId, string actingFactionId)
        {
            if (world?.OutdoorAdministrativeAssetAnchors == null || string.IsNullOrWhiteSpace(locationId) ||
                string.IsNullOrWhiteSpace(actingFactionId) ||
                !world.OutdoorAdministrativeAssetAnchors.TryGetByLocation(locationId, out var anchors)) return false;
            for (var i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor == null || !OutdoorStatefulObjectSemantics.IsFarmPlotKind(anchor.Kind)) continue;
                if (WorldAdministrativeAssetAuthorizationService.ResolveForFaction(
                        world, anchor.StableAssetId, actingFactionId).IsAllowed) return true;
            }
            return false;
        }
    }
}
