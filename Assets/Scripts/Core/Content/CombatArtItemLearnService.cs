using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Content
{
    /// <summary>背包斗技秘本：不消耗；可学多门，默认装备第一门。</summary>
    public sealed class CombatArtItemLearnService
    {
        public Result TryLearnFromItem(SimulationWorld world, EntityId learner, string itemId)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World null.");
            if (learner.IsNone)
                return Result.Failure(ErrorCode.InvalidArgument, "Learner required.");
            if (string.IsNullOrEmpty(itemId))
                return Result.Failure(ErrorCode.InvalidArgument, "Item id required.");

            var artIdText = world.InventoryCatalog.GetTeachesArtId(itemId);
            if (string.IsNullOrEmpty(artIdText))
                return Result.Failure(ErrorCode.InvalidOperation, "Item is not a combat-art tome.", itemId);
            if (!DefinitionId.TryParse(artIdText, out var artId))
                return Result.Failure(ErrorCode.InvalidDefinitionId, "teachesArtId invalid.", artIdText);
            if (!world.TryGetCombatArt(artId, out var art) || art == null)
                return Result.Failure(ErrorCode.NotFound, "Combat art missing.", artIdText);
            if (world.Inventory.GetCount(itemId) < 1)
                return Result.Failure(ErrorCode.InvalidOperation, "No tome in bag.", itemId);
            if (!world.Entities.TryGet(learner, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Learner missing.");
            if (!entity.TryGet<CombatArtsComponent>(out var arts))
            {
                arts = new CombatArtsComponent();
                var added = entity.AddComponent(arts);
                if (added.IsFailure)
                    return Result.Failure(added.Error);
            }

            if (arts.Knows(artId))
                return Result.Success();

            arts.TryLearn(artId);
            world.Events.Publish(
                EventType.SettlementStockChanged,
                world.Tick,
                target: learner,
                payload: "bag:" + itemId + ":0:learnArt:" + artId);
            QuestProgressRefresh.AfterWorldChange(world, learner);
            return Result.Success();
        }
    }
}
