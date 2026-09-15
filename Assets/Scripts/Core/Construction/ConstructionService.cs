using System;
using System.Collections.Generic;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Inventory;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Construction
{
    public static class ConstructionService
    {
        public const string FactionControlPostBuildingId = "base:building_faction_control_post";

        public static bool HasRequiredMaterials(
            SimulationWorld world,
            BuildingConstructionSpec spec,
            out ConstructionMaterialCost missing)
        {
            missing = null;
            if (world == null || spec == null)
                return false;
            foreach (var total in SumCosts(spec.Costs))
            {
                var have = GetAvailableMaterialCount(world, total.ItemId);
                if (have < total.Count)
                {
                    missing = new ConstructionMaterialCost
                        { ItemId = total.ItemId, Count = total.Count - have };
                    return false;
                }
            }
            return true;
        }

        public static int GetAvailableMaterialCount(SimulationWorld world, string itemId) =>
            world?.InventoryCatalog?.HasTag(itemId, "resource") == true
                ? PlayerStrategicResourceService.GetAvailableCount(world, itemId)
                : world?.Inventory?.GetCount(itemId) ?? 0;

        public static Result TryConstructFactionFlag(
            SimulationWorld world,
            string buildingId,
            string playerFactionId,
            HexCoord anchor,
            float localX,
            float localZ,
            out string flagId)
        {
            flagId = string.Empty;
            var resolved = ResolveFactionFlagSpec(world, buildingId, out var spec);
            if (resolved.IsFailure)
                return resolved;
            if (!spec.UnlockedByDefault)
                return Result.Failure(ErrorCode.InvalidOperation, "此建筑尚未解锁。");
            if (!HasRequiredMaterials(world, spec, out var missing))
                return Result.Failure(ErrorCode.InvalidOperation, "建造材料不足。", missing?.ItemId);

            var placement = FactionFlagService.ValidatePlacement(world, playerFactionId, anchor, out _);
            if (placement.IsFailure)
                return placement;

            if (!TrySpendMaterials(world, spec, out var removed))
                return Result.Failure(ErrorCode.InvalidOperation, "建造材料扣除失败，事务已回滚。");

            flagId = FactionFlagService.NextRuntimeFlagId(world, playerFactionId, anchor);
            var placed = FactionFlagService.TryPlace(
                world, flagId, playerFactionId, anchor,
                FactionFlagService.NextEstablishedOrder(world), localX, localZ, true);
            if (placed.IsFailure)
            {
                RestoreRemoved(world, removed);
                flagId = string.Empty;
                return placed;
            }
            return Result.Success();
        }

        public static Result TryConstructFactionFlagSite(
            SimulationWorld world,
            string buildingId,
            string playerFactionId,
            FactionFlagSitePlacementRequest request,
            out string flagId,
            out string siteId)
        {
            flagId = siteId = string.Empty;
            var resolved = ResolveFactionFlagSpec(world, buildingId, out var spec);
            if (resolved.IsFailure)
                return resolved;
            if (!spec.UnlockedByDefault || !spec.CreatesWorldSite)
                return Result.Failure(ErrorCode.InvalidOperation, "此建筑未配置为新据点核心。");
            if (!HasRequiredMaterials(world, spec, out var missing))
                return Result.Failure(ErrorCode.InvalidOperation, "建造材料不足。", missing?.ItemId);

            var valid = FactionFlagService.ValidateSiteCorePlacement(
                world, playerFactionId, request, spec.InitialSiteLevel, out _);
            if (valid.IsFailure)
                return valid;

            flagId = FactionFlagService.NextRuntimeFlagId(world, playerFactionId, request.StrategicAnchor);
            if (!TrySpendMaterials(world, spec, out var removed))
            {
                flagId = string.Empty;
                return Result.Failure(ErrorCode.InvalidOperation, "建造材料扣除失败，事务已回滚。");
            }

            var placed = FactionFlagService.TryPlaceSiteCore(
                world, flagId, playerFactionId, request,
                FactionFlagService.NextEstablishedOrder(world), spec.CreatedSiteName,
                spec.CreatedSiteType, spec.InitialSiteLevel, out siteId);
            if (placed.IsFailure)
            {
                RestoreRemoved(world, removed);
                flagId = siteId = string.Empty;
                return placed;
            }
            return Result.Success();
        }

        public static Result TryConstructFarmField(
            SimulationWorld world, string buildingId, string actingFactionId,
            string surfaceId, float worldX, float worldY, out string assetId)
            => TryConstructOutdoorAsset(world, buildingId, actingFactionId, surfaceId, worldX, worldY,
                ConstructionPlacementKind.FarmField, "农田", "farm", true, out assetId);

        public static Result TryConstructRecoverySpot(
            SimulationWorld world, string buildingId, string actingFactionId,
            string surfaceId, float worldX, float worldY, out string assetId)
            => TryConstructOutdoorAsset(world, buildingId, actingFactionId, surfaceId, worldX, worldY,
                ConstructionPlacementKind.RecoverySpot, "恢复处", "recovery", false, out assetId);

        public static Result TryConstructStorageRoom(
            SimulationWorld world, string buildingId, string actingFactionId,
            string surfaceId, float worldX, float worldY, out string assetId)
            => TryConstructOutdoorAsset(world, buildingId, actingFactionId, surfaceId, worldX, worldY,
                ConstructionPlacementKind.StorageRoom, "储藏室", "storage", false, out assetId);

        static Result TryConstructOutdoorAsset(
            SimulationWorld world, string buildingId, string actingFactionId,
            string surfaceId, float worldX, float worldY, ConstructionPlacementKind placementKind,
            string displayName, string idKind, bool createsAdministrativeAnchors, out string assetId)
        {
            assetId = string.Empty;
            if (world == null || !world.ConstructionCatalog.TryGet(buildingId, out var spec) ||
                spec.PlacementKind != placementKind || !spec.UnlockedByDefault || spec.CreatesWorldSite ||
                (placementKind == ConstructionPlacementKind.RecoverySpot &&
                 (spec.FootprintCellsW != 2 || spec.FootprintCellsH != 2 ||
                  !string.Equals(spec.OutdoorKind, OutdoorConstructedAssetSemantics.RecoverySpotKind, StringComparison.Ordinal))) ||
                (placementKind == ConstructionPlacementKind.StorageRoom &&
                 (spec.FootprintCellsW != 3 || spec.FootprintCellsH != 3 ||
                  !string.Equals(spec.OutdoorKind, OutdoorConstructedAssetSemantics.StorageRoomKind, StringComparison.Ordinal))))
                return Result.Failure(ErrorCode.InvalidArgument, displayName + "建筑定义无效或未解锁。");
            if (world.LocalMap.IsInInterior || world.Strategic.ClockFreeze.Reason != StrategicClockFreezeReason.None)
                return Result.Failure(ErrorCode.InvalidOperation, "当前空间或战斗阶段不允许建造" + displayName + "。");
            if (!HasRequiredMaterials(world, spec, out _))
                return Result.Failure(ErrorCode.InvalidOperation, "建造材料不足。");
            if (!world.SurfaceSpatial.TryGet(surfaceId, out var metric) || world.OutdoorConstructedAssets.NextSequence >= long.MaxValue - 1)
                return Result.Failure(ErrorCode.InvalidArgument, "Surface 或资产序列无效。");
            var boundSiteId = string.Empty;
            if (placementKind == ConstructionPlacementKind.StorageRoom)
            {
                var storageSite = WorldSiteStorageRoomPlacementService.ResolveSiteForFootprint(world, actingFactionId, surfaceId,
                    worldX, worldY, spec.FootprintCellsW, spec.FootprintCellsH, metric.CellSize, out boundSiteId);
                if (storageSite.IsFailure) return storageSite;
            }
            var nextId = world.OutdoorConstructedAssets.NextIdForKind(spec.OutdoorKind);
            var asset = new OutdoorConstructedAssetState {
                StableAssetId = nextId, BuildingId = buildingId,
                Kind = spec.OutdoorKind, SurfaceId = surfaceId, WorldX = worldX, WorldY = worldY,
                WorldWidth = spec.FootprintCellsW * metric.CellSize, WorldHeight = spec.FootprintCellsH * metric.CellSize,
                CellsW = spec.FootprintCellsW, CellsH = spec.FootprintCellsH,
                BoundLocationId = "location:runtime:" + idKind + ":" + nextId,
                BoundWorldSiteId = boundSiteId
            };
            var allowed = OutdoorFactionConstructionAuthorizationService.Validate(world, actingFactionId, asset);
            if (allowed.IsFailure) return allowed;
            foreach (var existing in world.OutdoorConstructedAssets.Assets.Values)
                if (OutdoorConstructedAssetBoard.Overlaps(existing, asset))
                    return Result.Failure(ErrorCode.InvalidOperation, "此处已有室外建筑或世界对象。");
            if (createsAdministrativeAnchors)
                foreach (var anchor in world.OutdoorAdministrativeAssetAnchors.Anchors.Values)
                    if (anchor.SurfaceId == surfaceId && anchor.WorldX > worldX && anchor.WorldX < worldX + asset.WorldWidth &&
                        anchor.WorldY > worldY && anchor.WorldY < worldY + asset.WorldHeight)
                        return Result.Failure(ErrorCode.InvalidOperation, "此处已有农田。");
            var anchors = createsAdministrativeAnchors
                ? new List<XianXia.Core.Exploration.OutdoorAdministrativeAssetAnchor>(asset.AdministrativeCellAnchors())
                : new List<XianXia.Core.Exploration.OutdoorAdministrativeAssetAnchor>();
            foreach (var anchor in anchors)
                if (world.OutdoorAdministrativeAssetAnchors.TryGet(anchor.StableAssetId, out _))
                    return Result.Failure(ErrorCode.InvalidOperation, "室外建筑身份已存在。");
            if (!TrySpendMaterials(world, spec, out var removed))
                return Result.Failure(ErrorCode.InvalidOperation, "材料扣除失败，已回滚。");
            if (!world.OutdoorConstructedAssets.TryRegister(asset))
            { RestoreRemoved(world, removed); return Result.Failure(ErrorCode.InvalidOperation, displayName + "注册失败，已回滚。"); }
            if (placementKind == ConstructionPlacementKind.StorageRoom &&
                !world.SiteStorageRooms.TryRegister(new WorldSiteStorageRoomState {
                    StorageRoomId = asset.StableAssetId,
                    SiteId = asset.BoundWorldSiteId,
                    SurfaceId = asset.SurfaceId,
                    DisplayName = displayName,
                    WorldX = asset.WorldX + asset.WorldWidth * .5f,
                    WorldY = asset.WorldY + asset.WorldHeight * .5f
                }))
            {
                world.OutdoorConstructedAssets.Remove(asset.StableAssetId);
                RestoreRemoved(world, removed);
                return Result.Failure(ErrorCode.InvalidOperation, "该据点已有储藏室，材料已回滚。");
            }
            var registered = new List<string>();
            foreach (var anchor in anchors)
            {
                if (!world.OutdoorAdministrativeAssetAnchors.TryRegister(anchor))
                {
                    foreach (var id in registered) world.OutdoorAdministrativeAssetAnchors.Remove(id);
                    world.SiteStorageRooms.Remove(asset.StableAssetId);
                    world.OutdoorConstructedAssets.Remove(asset.StableAssetId);
                    RestoreRemoved(world, removed);
                    return Result.Failure(ErrorCode.InvalidOperation, "室外建筑锚点注册失败，已回滚。");
                }
                registered.Add(anchor.StableAssetId);
            }
            world.OutdoorConstructedAssets.AdvanceSequence();
            assetId = asset.StableAssetId;
            return Result.Success();
        }

        public static List<ConstructionMaterialCost> CalculateDismantleRefunds(BuildingConstructionSpec spec)
        {
            var refunds = new List<ConstructionMaterialCost>();
            if (spec == null)
                return refunds;
            foreach (var cost in SumCosts(spec.Costs))
            {
                var count = (int)Math.Floor(cost.Count * spec.DismantleRefundRate);
                if (count > 0)
                    refunds.Add(new ConstructionMaterialCost { ItemId = cost.ItemId, Count = count });
            }
            return refunds;
        }

        public static Result TryDismantleFactionFlag(
            SimulationWorld world,
            string buildingId,
            string playerFactionId,
            string flagId,
            out List<ConstructionMaterialCost> refunds)
        {
            refunds = new List<ConstructionMaterialCost>();
            var resolved = ResolveFactionFlagSpec(world, buildingId, out var spec);
            if (resolved.IsFailure)
                return resolved;
            if (world?.Strategic == null ||
                !world.Strategic.FactionFlags.Flags.TryGetValue(flagId ?? string.Empty, out var flag) || flag == null)
                return Result.Failure(ErrorCode.NotFound, "势力控制建筑不存在。");
            if (!string.Equals(flag.FactionId, playerFactionId, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "只能拆除己方势力控制建筑。");

            refunds = CalculateDismantleRefunds(spec);
            if (!CanAddRefundsWithoutMutation(world, refunds))
                return Result.Failure(ErrorCode.InvalidOperation, "背包空间不足，无法容纳拆除返还材料。");

            var destroyed = FactionFlagService.TryDestroy(world, flagId);
            if (destroyed.IsFailure)
                return destroyed;

            var before = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < refunds.Count; i++)
                before[refunds[i].ItemId] = world.Inventory.GetCount(refunds[i].ItemId);
            for (var i = 0; i < refunds.Count; i++)
            {
                var refund = refunds[i];
                if (world.Inventory.TryAddAll(refund.ItemId, refund.Count))
                    continue;

                foreach (var pair in before)
                {
                    var delta = world.Inventory.GetCount(pair.Key) - pair.Value;
                    if (delta > 0)
                        world.Inventory.TryRemoveAll(pair.Key, delta);
                }
                FactionFlagService.TryRestoreRemovedCore(world, flag);
                refunds.Clear();
                return Result.Failure(ErrorCode.InvalidOperation, "拆除返料失败，事务已回滚。", refund.ItemId);
            }
            return Result.Success();
        }

        static Result ResolveFactionFlagSpec(
            SimulationWorld world, string buildingId, out BuildingConstructionSpec spec)
        {
            spec = null;
            if (world == null || !world.ConstructionCatalog.TryGet(buildingId, out spec) || spec == null)
                return Result.Failure(ErrorCode.NotFound, "建筑定义不存在。", buildingId);
            if (spec.PlacementKind != ConstructionPlacementKind.FactionFlag)
                return Result.Failure(ErrorCode.InvalidOperation, "建筑放置类型不是 FactionFlag。", buildingId);
            return Result.Success();
        }

        static List<ConstructionMaterialCost> SumCosts(IEnumerable<ConstructionMaterialCost> costs)
        {
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            if (costs != null)
            {
                foreach (var cost in costs)
                {
                    if (cost == null || string.IsNullOrEmpty(cost.ItemId) || cost.Count <= 0)
                        continue;
                    totals.TryGetValue(cost.ItemId, out var count);
                    totals[cost.ItemId] = count + cost.Count;
                }
            }
            var result = new List<ConstructionMaterialCost>();
            foreach (var pair in totals)
                result.Add(new ConstructionMaterialCost { ItemId = pair.Key, Count = pair.Value });
            return result;
        }

        static bool CanAddRefundsWithoutMutation(
            SimulationWorld world, IReadOnlyList<ConstructionMaterialCost> refunds)
        {
            if (world == null)
                return false;
            var emptySlots = world.Inventory.SlotCapacity - world.Inventory.UsedSlotCount;
            var requiredEmptySlots = 0;
            for (var i = 0; i < refunds.Count; i++)
            {
                var refund = refunds[i];
                var capacityInExistingStacks = 0;
                var maxStack = world.InventoryCatalog.GetMaxStack(refund.ItemId);
                for (var s = 0; s < world.Inventory.Slots.Count; s++)
                {
                    var slot = world.Inventory.Slots[s];
                    if (!slot.IsEmpty && string.Equals(slot.ItemId, refund.ItemId, StringComparison.Ordinal))
                        capacityInExistingStacks += Math.Max(0, maxStack - slot.Count);
                }
                var remaining = Math.Max(0, refund.Count - capacityInExistingStacks);
                requiredEmptySlots += (remaining + maxStack - 1) / maxStack;
            }
            return requiredEmptySlots <= emptySlots;
        }

        sealed class MaterialWithdrawal
        {
            public ConstructionMaterialCost Cost;
            public StrategicResourceWithdrawalReceipt StrategicReceipt;
        }

        static bool TrySpendMaterials(SimulationWorld world, BuildingConstructionSpec spec,
            out List<MaterialWithdrawal> removed)
        {
            removed = new List<MaterialWithdrawal>();
            if (!HasRequiredMaterials(world, spec, out _)) return false;
            foreach (var cost in SumCosts(spec.Costs))
            {
                var row = new MaterialWithdrawal { Cost = cost };
                var success = world.InventoryCatalog.HasTag(cost.ItemId, "resource")
                    ? PlayerStrategicResourceService.TryConsume(
                        world, cost.ItemId, cost.Count, out row.StrategicReceipt).IsSuccess
                    : world.Inventory.TryRemoveAll(cost.ItemId, cost.Count);
                if (!success) { RestoreRemoved(world, removed); return false; }
                removed.Add(row);
            }
            return true;
        }

        static void RestoreRemoved(SimulationWorld world, IReadOnlyList<MaterialWithdrawal> removed)
        {
            for (var i = removed.Count - 1; i >= 0; i--)
            {
                var row = removed[i];
                if (row.StrategicReceipt != null)
                    PlayerStrategicResourceService.Rollback(world, row.StrategicReceipt);
                else
                    world.Inventory.TryAddAll(row.Cost.ItemId, row.Cost.Count);
            }
        }
    }
}
