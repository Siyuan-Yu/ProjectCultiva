using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Content
{
    /// <summary>背包秘籍：消耗 1 本，指定角色 <see cref="CultivationService.LearnManual"/>。</summary>
    public sealed class ManualItemLearnService
    {
        readonly CultivationService _cultivation = new CultivationService();

        public Result TryLearnFromItem(SimulationWorld world, EntityId learner, string itemId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World null.");
            if (learner.IsNone)
                return Result.Failure(ErrorCode.InvalidArgument, "Learner required.");
            if (string.IsNullOrEmpty(itemId))
                return Result.Failure(ErrorCode.InvalidArgument, "Item id required.");

            var manualIdText = world.InventoryCatalog.GetTeachesManualId(itemId);
            if (string.IsNullOrEmpty(manualIdText))
                return Result.Failure(ErrorCode.InvalidOperation, "Item is not a manual tome.", itemId);

            if (!DefinitionId.TryParse(manualIdText, out var manualId))
                return Result.Failure(ErrorCode.InvalidDefinitionId, "teachesManualId invalid.", manualIdText);
            if (!world.TryGetManual(manualId, out var manual) || manual == null)
                return Result.Failure(ErrorCode.NotFound, "Manual missing.", manualIdText);

            if (world.Inventory.GetCount(itemId) < 1)
                return Result.Failure(ErrorCode.InvalidOperation, "No tome in bag.", itemId);

            var learned = _cultivation.LearnManual(world, learner, manual);
            if (learned.IsFailure)
                return learned;

            if (!world.Inventory.TryRemoveAll(itemId, 1))
                return Result.Failure(ErrorCode.InvalidOperation, "Failed to consume tome after learn.", itemId);

            world.Events.Publish(
                EventType.SettlementStockChanged,
                world.Tick,
                target: learner,
                payload: "bag:" + itemId + ":-1:learnManual");
            QuestProgressRefresh.AfterWorldChange(world, learner);
            return Result.Success();
        }
    }
}
