using NUnit.Framework;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;

namespace XianXia.Tests
{
    public sealed class QuestStockProgressRefreshTests
    {
        const string HerbId = "base:resource_spirit_herb";
        const string QuestId = "base:quest_test_herb_stock";

        [Test]
        public void InventoryAdd_RefreshesStockAtLeastProgress()
        {
            var world = new SimulationWorld();
            var spec = new QuestSpec
            {
                Id = QuestId,
                Name = "test"
            };
            spec.CompleteConditions.Add(new ContentCondition
            {
                Kind = "stockAtLeast",
                Id = HerbId,
                Amount = 100
            });
            world.Quests.Register(spec);

            var subject = EntityId.None;
            Assert.IsTrue(new QuestService().TryStart(world, QuestId, subject).IsSuccess);
            Assert.IsTrue(world.Quests.TryGet(QuestId, out var runtime));
            Assert.AreEqual(0, runtime.ProgressCount);

            world.Inventory.TryAdd(HerbId, 4);
            QuestProgressRefresh.AfterWorldChange(world, subject);

            Assert.IsTrue(world.Quests.TryGet(QuestId, out runtime));
            Assert.AreEqual(4, runtime.ProgressCount);
            Assert.AreEqual(100, runtime.ProgressMax);
        }
    }
}
