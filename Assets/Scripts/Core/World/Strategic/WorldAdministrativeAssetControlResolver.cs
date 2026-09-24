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
    /// Strict faction management authorization for an explicitly registered administrative asset.
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

    public enum AdministrativeAssetWorkAuthorizationStatus
    {
        Invalid = 0,
        AllowedAsManager = 1,
        AllowedAsVassalWorker = 2,
        Unmanaged = 3,
        ManagedByOtherFaction = 4,
        NotAdministrativeAsset = 5
    }

    /// <summary>
    /// Read-only permission for labor/use on an administrative asset. This does not grant management,
    /// construction, storage, housing, schedule, or control authority.
    /// </summary>
    public sealed class AdministrativeAssetWorkAuthorization
    {
        internal AdministrativeAssetWorkAuthorization(
            AdministrativeAssetWorkAuthorizationStatus status,
            AdministrativeAssetAuthorization managementAuthorization)
        {
            Status = status;
            ManagementAuthorization = managementAuthorization;
        }

        public AdministrativeAssetWorkAuthorizationStatus Status { get; }
        public bool IsAllowed =>
            Status == AdministrativeAssetWorkAuthorizationStatus.AllowedAsManager ||
            Status == AdministrativeAssetWorkAuthorizationStatus.AllowedAsVassalWorker;
        public bool IsAllowedAsVassalWorker =>
            Status == AdministrativeAssetWorkAuthorizationStatus.AllowedAsVassalWorker;
        public AdministrativeAssetAuthorization ManagementAuthorization { get; }
        public string ActingFactionId => ManagementAuthorization?.ActingFactionId ?? string.Empty;
        public OutdoorAdministrativeAssetAnchor Anchor => ManagementAuthorization?.Anchor;
        public WorldSite ManagingSite => ManagementAuthorization?.ManagingSite;
        public string ManagingFactionId => ManagementAuthorization?.ManagingFactionId ?? string.Empty;
        public TerritoryClaimState WinningClaim => ManagementAuthorization?.WinningClaim;
    }

    /// <summary>
    /// Labor/use authorization layered on top of strict administrative management authorization.
    /// A direct vassal may work its overlord's asset, while the actual manager remains unchanged.
    /// </summary>
    public static class WorldAdministrativeAssetWorkAuthorizationService
    {
        public static AdministrativeAssetWorkAuthorization ResolveForFaction(
            SimulationWorld world,
            string stableAssetId,
            string actingFactionId)
        {
            var management = WorldAdministrativeAssetAuthorizationService.ResolveForFaction(
                world, stableAssetId, actingFactionId);
            switch (management.Status)
            {
                case AdministrativeAssetAuthorizationStatus.Allowed:
                    return Result(AdministrativeAssetWorkAuthorizationStatus.AllowedAsManager, management);
                case AdministrativeAssetAuthorizationStatus.ManagedByOtherFaction:
                    if (FactionDiplomacyRelationQuery.GetRelation(
                            world, actingFactionId, management.ManagingFactionId) ==
                        FactionDiplomacyRelation.Overlord)
                        return Result(
                            AdministrativeAssetWorkAuthorizationStatus.AllowedAsVassalWorker,
                            management);
                    return Result(
                        AdministrativeAssetWorkAuthorizationStatus.ManagedByOtherFaction,
                        management);
                case AdministrativeAssetAuthorizationStatus.Unmanaged:
                    return Result(AdministrativeAssetWorkAuthorizationStatus.Unmanaged, management);
                case AdministrativeAssetAuthorizationStatus.NotAdministrativeAsset:
                    return Result(
                        AdministrativeAssetWorkAuthorizationStatus.NotAdministrativeAsset,
                        management);
                default:
                    return Result(AdministrativeAssetWorkAuthorizationStatus.Invalid, management);
            }
        }

        static AdministrativeAssetWorkAuthorization Result(
            AdministrativeAssetWorkAuthorizationStatus status,
            AdministrativeAssetAuthorization managementAuthorization) =>
            new AdministrativeAssetWorkAuthorization(status, managementAuthorization);
    }

    /// <summary>Resolves whether a faction may work at least one real farm cell in a WorkArea location.</summary>
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
                if (WorldAdministrativeAssetWorkAuthorizationService.ResolveForFaction(
                        world, anchor.StableAssetId, actingFactionId).IsAllowed) return true;
            }
            return false;
        }
    }
}
