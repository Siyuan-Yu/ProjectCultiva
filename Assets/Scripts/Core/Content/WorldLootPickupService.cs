using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Content
{
    /// <summary>地表／洞内可拾取物：一次拾取进背包，flag＝loot:{spotId}。</summary>
    public sealed class WorldLootPickupService
    {
        public static string FlagId(string lootSpotId) =>
            string.IsNullOrWhiteSpace(lootSpotId) ? string.Empty : "loot:" + lootSpotId.Trim();

        public static bool IsTaken(SimulationWorld world, string lootSpotId) =>
            world != null && StoryFlagService.Has(world, FlagId(lootSpotId));

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
            if (!world.Entities.TryGet(subject, out _))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.", subject.ToString());

            var flag = FlagId(lootSpotId);
            if (StoryFlagService.Has(world, flag))
                return Result.Failure(ErrorCode.InvalidOperation, "Loot already taken.", lootSpotId);

            if (!world.InventoryCatalog.TryGet(itemId.Trim(), out _))
                return Result.Failure(ErrorCode.NotFound, "Item not in catalog.", itemId);

            var added = world.Inventory.TryAdd(itemId.Trim(), 1);
            if (added <= 0)
                return Result.Failure(ErrorCode.InvalidOperation, "Inventory full or rejected.", itemId);

            StoryFlagService.Set(world, flag, subject);
            world.Events.Publish(
                EventType.SettlementStockChanged,
                world.Tick,
                target: subject,
                payload: "bag:" + itemId.Trim() + ":+" + added + ";loot:" + lootSpotId.Trim());
            return Result.Success();
        }
    }
}
