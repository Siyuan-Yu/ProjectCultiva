using System;
using System.Collections.Generic;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
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
                var have = world.Inventory.GetCount(total.ItemId);
                if (have < total.Count)
                {
                    missing = new ConstructionMaterialCost
                        { ItemId = total.ItemId, Count = total.Count - have };
                    return false;
                }
            }
            return true;
        }

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

            var totals = SumCosts(spec.Costs);
            var removed = new List<ConstructionMaterialCost>();
            for (var i = 0; i < totals.Count; i++)
            {
                var cost = totals[i];
                if (!world.Inventory.TryRemoveAll(cost.ItemId, cost.Count))
                {
                    RestoreRemoved(world, removed);
                    return Result.Failure(ErrorCode.InvalidOperation, "建造材料扣除失败，事务已回滚。", cost.ItemId);
                }
                removed.Add(cost);
            }

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
                world.Strategic.FactionFlags.Register(flag);
                StrategicTerritoryCoverageResolver.Rebuild(world);
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

        static void RestoreRemoved(SimulationWorld world, IReadOnlyList<ConstructionMaterialCost> removed)
        {
            for (var i = 0; i < removed.Count; i++)
                world.Inventory.TryAddAll(removed[i].ItemId, removed[i].Count);
        }
    }
}
