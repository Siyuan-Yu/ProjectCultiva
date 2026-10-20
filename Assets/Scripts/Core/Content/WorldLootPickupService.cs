using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Content
{
    /// <summary>地表／洞内可拾取物：一次拾取进背包，flag＝loot:{spotId}。</summary>
    public sealed class WorldLootPickupService
    {
        const string LootFlagPrefix = "loot:";

        public static string FlagId(string lootSpotId) =>
            string.IsNullOrWhiteSpace(lootSpotId) ? string.Empty : LootFlagPrefix + lootSpotId.Trim();

        /// <summary>LocalMap identity is layout-scoped; Outdoor callers pass an empty layout id and retain StableId.</summary>
        public static string StableSpotId(string mapLayoutId, string placementId)
        {
            var placement = placementId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(placement))
                return string.Empty;
            var layout = mapLayoutId?.Trim() ?? string.Empty;
            return string.IsNullOrEmpty(layout) ? placement : layout + "::" + placement;
        }

        public static bool IsTaken(SimulationWorld world, string lootSpotId) =>
            world != null && StoryFlagService.Has(world, FlagId(lootSpotId));

        public static void CaptureTakenSpotIds(SimulationWorld world, List<string> destination)
        {
            if (world?.Flags == null || destination == null)
                return;
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var flag in world.Flags.All)
                if (!string.IsNullOrEmpty(flag) &&
                    flag.StartsWith(LootFlagPrefix, StringComparison.Ordinal) &&
                    flag.Length > LootFlagPrefix.Length)
                    unique.Add(flag.Substring(LootFlagPrefix.Length));
            destination.AddRange(unique);
            destination.Sort(StringComparer.Ordinal);
        }

        /// <summary>Snapshot restore path: no StoryFlagChanged or pickup/inventory event.</summary>
        public static void RestoreTakenSpotIds(SimulationWorld world, IEnumerable<string> spotIds)
        {
            if (world?.Flags == null)
                return;
            var existing = new List<string>();
            foreach (var flag in world.Flags.All)
                if (!string.IsNullOrEmpty(flag) && flag.StartsWith(LootFlagPrefix, StringComparison.Ordinal))
                    existing.Add(flag);
            for (var i = 0; i < existing.Count; i++)
                world.Flags.Clear(existing[i]);
            if (spotIds == null)
                return;
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var raw in spotIds)
            {
                var spotId = raw?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(spotId) && unique.Add(spotId))
                    world.Flags.Set(FlagId(spotId));
            }
        }

        public Result TryPickup(
            SimulationWorld world,
            EntityId subject,
            string lootSpotId,
            string itemId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World is null.");
            if (string.IsNullOrWhiteSpace(lootSpotId))
                return Result.Failure(ErrorCode.InvalidArgument, "Loot spot id required.");
            if (string.IsNullOrWhiteSpace(itemId))
                return Result.Failure(ErrorCode.InvalidArgument, "Item id required.");
            if (!world.Entities.TryGet(subject, out var actor))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.", subject.ToString());
            if (!XianXia.Core.Combat.CombatLifeStateService.CanFight(actor))
                return Result.Failure(ErrorCode.InvalidOperation, "失能人物不能拾取物品。");

            var flag = FlagId(lootSpotId);
            if (StoryFlagService.Has(world, flag))
                return Result.Failure(ErrorCode.InvalidOperation, "Loot already taken.", lootSpotId);

            if (!world.InventoryCatalog.TryGet(itemId.Trim(), out _))
                return Result.Failure(ErrorCode.NotFound, "Item not in catalog.", itemId);

            world.GroundLoot.TryGetValue(lootSpotId, out var drop);
            if (drop != null)
            {
                if (drop.ItemId != itemId ||
                    !XianXia.Core.World.Strategic.CharacterWorldPresenceQuery.TryResolve(world, subject, out var presence) ||
                    !presence.HasWorldPosition || presence.SurfaceId != drop.SurfaceId ||
                    !world.SurfaceSpatial.TryGet(drop.SurfaceId, out var metric))
                    return Result.Failure(ErrorCode.InvalidOperation, "掉落物与人物不在同一可拾取空间。");
                var dx = presence.WorldPosition.X - drop.WorldX;
                var dy = presence.WorldPosition.Y - drop.WorldY;
                if (dx * dx + dy * dy > metric.CellSize * metric.CellSize * 9f)
                    return Result.Failure(ErrorCode.InvalidOperation, "请先走近掉落物。");
            }
            var added = world.Inventory.TryAdd(itemId.Trim(), drop?.Count ?? 1);
            if (added <= 0)
                return Result.Failure(ErrorCode.InvalidOperation, "Inventory full or rejected.", itemId);

            if (drop != null) drop.Count -= added;
            if (drop == null || drop.Count == 0)
            {
                world.GroundLoot.Remove(lootSpotId);
                StoryFlagService.Set(world, flag, subject);
            }
            world.Events.Publish(
                EventType.PartyInventoryChanged,
                world.Tick,
                target: subject,
                payload: "bag:" + itemId.Trim() + ":+" + added + ";loot:" + lootSpotId.Trim());
            return Result.Success();
        }
    }
}
