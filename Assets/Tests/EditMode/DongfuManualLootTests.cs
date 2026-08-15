using NUnit.Framework;
using XianXia.Core.Attributes;
using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Data.Cultivation;

namespace XianXia.Tests
{
    public sealed class DongfuManualLootTests
    {
        [Test]
        public void Pickup_Adds_Manual_Tome_Once()
        {
            var world = new SimulationWorld();
            world.InventoryCatalog.Register(
                "base:item_manual_dongfu_secret",
                "洞府秘诀（秘籍）",
                1,
                new[] { "manual_tome" },
                "base:cultivation_dongfu_secret");

            var subject = world.Entities.CreateCharacter(
                new DefinitionId("base", "character_protagonist"), "主角").Value;

            var svc = new WorldLootPickupService();
            Assert.IsTrue(svc.TryPickup(
                world, subject.Id, "loot_cave_dongfu_manual", "base:item_manual_dongfu_secret").IsSuccess);
            Assert.AreEqual(1, world.Inventory.GetCount("base:item_manual_dongfu_secret"));
            Assert.IsTrue(WorldLootPickupService.IsTaken(world, "loot_cave_dongfu_manual"));
            Assert.IsTrue(svc.TryPickup(
                world, subject.Id, "loot_cave_dongfu_manual", "base:item_manual_dongfu_secret").IsFailure);
        }

        [Test]
        public void Dongfu_Manual_Grants_Attack_Percentage()
        {
            var world = new SimulationWorld();
            var def = new XianXia.Data.Content.CultivationDefinition
            {
                Id = new DefinitionId("base", "cultivation_dongfu_secret"),
                Name = "洞府秘诀",
                RequiredRealm = "炼气",
                Grade = "黄阶中级",
                EffectSummary = "修炼后被动：攻击力 +6%。",
                CultivationSpeed = 7,
                BreakthroughProgress = 100
            };
            def.GrantedModifiers.Add(new XianXia.Data.Content.ModifierGrantDefinition
            {
                TargetAttribute = "Attack",
                Operation = "Percentage",
                Value = 0.06,
                StackingKey = "dongfu_secret_atk_pct"
            });
            var mapped = CultivationManualMapper.ToManualSpec(def);
            Assert.IsTrue(mapped.IsSuccess);

            var subject = world.Entities.CreateCharacter(
                new DefinitionId("base", "character_protagonist"), "主角").Value;
            subject.Get<CultivationComponent>().Realm = RealmStage.QiRefining;
            subject.Get<AttributesComponent>().SetBase(AttributeId.Attack, 100);

            Assert.IsTrue(new CultivationService().LearnManual(world, subject.Id, mapped.Value).IsSuccess);
            Assert.AreEqual(106, subject.Get<AttributesComponent>().GetFinal(AttributeId.Attack));
        }
    }
}
