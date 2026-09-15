using System;
using System.Collections.Generic;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Inventory
{
    public enum StrategicResourceSourceKind
    {
        WorldSitePublicStock = 0,
        PartyInventory = 1
    }

    public sealed class StrategicResourceWithdrawal
    {
        public StrategicResourceSourceKind SourceKind { get; set; }
        public string SiteId { get; set; } = string.Empty;
        public int Amount { get; set; }
    }

    /// <summary>Rollback receipt for one atomic resource withdrawal.</summary>
    public sealed class StrategicResourceWithdrawalReceipt
    {
        readonly List<StrategicResourceWithdrawal> _withdrawals = new List<StrategicResourceWithdrawal>();
        public string ResourceId { get; internal set; } = string.Empty;
        public IReadOnlyList<StrategicResourceWithdrawal> Withdrawals => _withdrawals;
        internal List<StrategicResourceWithdrawal> MutableWithdrawals => _withdrawals;
        internal bool IsApplied { get; set; }
    }

    /// <summary>
    /// Read-through aggregation over PartyInventory and eligible WorldSite public stocks.
    /// It owns no inventory and persists no derived total.
    /// </summary>
    public static class PlayerStrategicResourceService
    {
        public static bool CanAccessSiteStorageNetwork(SimulationWorld world) =>
            TryResolveCurrentManagingSite(world, out _);

        public static bool TryResolveCurrentManagingSite(SimulationWorld world, out WorldSite site)
        {
            site = null;
            var motion = world?.PlayerPartyTravel;
            var sites = world?.Strategic?.Sites;
            var playerFactionId = world?.Strategic?.PlayerFactionId ?? string.Empty;
            if (motion == null || sites == null || string.IsNullOrEmpty(playerFactionId)) return false;

            var currentOutdoorSiteId = motion.CurrentOutdoorWorldSiteId ?? string.Empty;
            if (!string.IsNullOrEmpty(currentOutdoorSiteId))
                return IsActivePlayerSite(sites, currentOutdoorSiteId, playerFactionId, out site);

            if (motion.LocationKind != PlayerPartyLocationKind.AtWorldSite || string.IsNullOrEmpty(motion.SiteId))
                return false;
            return IsActivePlayerSite(sites, motion.SiteId, playerFactionId, out site);
        }

        public static int GetAvailableCount(SimulationWorld world, string resourceId)
        {
            if (!IsResource(world, resourceId)) return 0;
            long total = world.Inventory.GetCount(resourceId);
            if (!TryResolveCurrentManagingSite(world, out _)) return (int)total;
            foreach (var siteId in EligibleStorageSiteIds(world))
                total += WorldSitePublicStockService.GetCount(world, siteId, resourceId);
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        public static Result TryConsume(
            SimulationWorld world, string resourceId, int amount,
            out StrategicResourceWithdrawalReceipt receipt)
        {
            receipt = new StrategicResourceWithdrawalReceipt { ResourceId = resourceId ?? string.Empty };
            if (world?.Inventory == null || amount <= 0 || !IsResource(world, resourceId))
                return Result.Failure(ErrorCode.InvalidArgument, "Strategic resource withdrawal requires a resource and positive amount.");
            if (GetAvailableCount(world, resourceId) < amount)
                return Result.Failure(ErrorCode.InvalidOperation, "可用战略物资不足。", resourceId);

            var plan = receipt.MutableWithdrawals;
            var remaining = amount;
            if (TryResolveCurrentManagingSite(world, out var current))
            {
                if (world.SiteStorageRooms.HasActiveStorageForSite(world, current.SiteId))
                    AddSiteWithdrawal(world, current.SiteId, resourceId, ref remaining, plan);
                var siteIds = EligibleStorageSiteIds(world);
                for (var i = 0; i < siteIds.Count && remaining > 0; i++)
                    if (!string.Equals(siteIds[i], current.SiteId, StringComparison.Ordinal))
                        AddSiteWithdrawal(world, siteIds[i], resourceId, ref remaining, plan);
            }
            var bagTake = Math.Min(remaining, world.Inventory.GetCount(resourceId));
            if (bagTake > 0)
            {
                plan.Add(new StrategicResourceWithdrawal
                    { SourceKind = StrategicResourceSourceKind.PartyInventory, Amount = bagTake });
                remaining -= bagTake;
            }
            if (remaining != 0)
                return Result.Failure(ErrorCode.InvalidOperation, "战略物资消费计划不完整。", resourceId);

            var applied = 0;
            for (; applied < plan.Count; applied++)
            {
                var row = plan[applied];
                var ok = row.SourceKind == StrategicResourceSourceKind.PartyInventory
                    ? world.Inventory.TryRemoveAll(resourceId, row.Amount)
                    : WorldSitePublicStockService.TryRemove(world, row.SiteId, resourceId, row.Amount).IsSuccess;
                if (ok) continue;
                RollbackApplied(world, receipt, applied);
                receipt = new StrategicResourceWithdrawalReceipt { ResourceId = resourceId };
                return Result.Failure(ErrorCode.InvalidOperation, "战略物资扣除失败，事务已回滚。", resourceId);
            }
            receipt.IsApplied = true;
            return Result.Success();
        }

        public static Result Rollback(SimulationWorld world, StrategicResourceWithdrawalReceipt receipt)
        {
            if (world == null || receipt == null || !receipt.IsApplied || !IsResource(world, receipt.ResourceId))
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid strategic resource rollback receipt.");
            var result = RollbackApplied(world, receipt, receipt.Withdrawals.Count);
            if (result.IsSuccess) receipt.IsApplied = false;
            return result;
        }

        static Result RollbackApplied(SimulationWorld world, StrategicResourceWithdrawalReceipt receipt, int count)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                var row = receipt.Withdrawals[i];
                var ok = row.SourceKind == StrategicResourceSourceKind.PartyInventory
                    ? world.Inventory.TryAddAll(receipt.ResourceId, row.Amount)
                    : WorldSitePublicStockService.TryAdd(world, row.SiteId, receipt.ResourceId, row.Amount).IsSuccess;
                if (!ok) return Result.Failure(ErrorCode.InvalidOperation, "战略物资事务回滚失败。", receipt.ResourceId);
            }
            return Result.Success();
        }

        static void AddSiteWithdrawal(SimulationWorld world, string siteId, string resourceId,
            ref int remaining, List<StrategicResourceWithdrawal> plan)
        {
            var take = Math.Min(remaining, WorldSitePublicStockService.GetCount(world, siteId, resourceId));
            if (take <= 0) return;
            plan.Add(new StrategicResourceWithdrawal {
                SourceKind = StrategicResourceSourceKind.WorldSitePublicStock,
                SiteId = siteId,
                Amount = take
            });
            remaining -= take;
        }

        static List<string> EligibleStorageSiteIds(SimulationWorld world)
        {
            var ids = new List<string>();
            var playerFactionId = world?.Strategic?.PlayerFactionId ?? string.Empty;
            if (string.IsNullOrEmpty(playerFactionId)) return ids;
            foreach (var pair in world.Strategic.Sites.Sites)
            {
                var site = pair.Value;
                if (site != null && site.IsCoreActive &&
                    string.Equals(site.OwnerFactionId, playerFactionId, StringComparison.Ordinal) &&
                    world.SiteStorageRooms.TryGetBySite(site.SiteId, out _)) ids.Add(site.SiteId);
            }
            ids.Sort(StringComparer.Ordinal);
            return ids;
        }

        static bool IsResource(SimulationWorld world, string resourceId) =>
            world?.InventoryCatalog != null && world.InventoryCatalog.HasTag(resourceId, "resource");

        static bool IsActivePlayerSite(WorldSiteBoard sites, string siteId, string playerFactionId,
            out WorldSite site)
        {
            site = null;
            return sites.TryGet(siteId, out site) && site != null && site.IsCoreActive &&
                   string.Equals(site.OwnerFactionId, playerFactionId, StringComparison.Ordinal);
        }
    }
}
