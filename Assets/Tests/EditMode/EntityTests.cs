using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;

namespace XianXia.Tests
{
    public sealed class EntityTests
    {
        [Test]
        public void CreateCharacter_HasWhitelistComponents()
        {
            var store = new EntityStore();
            var created = store.CreateCharacter(new DefinitionId("base", "character_labor_disciple"), "甲");
            Assert.IsTrue(created.IsSuccess);
            var e = created.Value;
            Assert.IsTrue(e.TryGet<IdentityComponent>(out _));
            Assert.IsTrue(e.TryGet<AttributesComponent>(out _));
            Assert.IsTrue(e.TryGet<LifecycleComponent>(out var life));
            Assert.IsTrue(e.TryGet<ActionStateComponent>(out _));
            Assert.IsTrue(e.TryGet<XianXia.Core.Cultivation.CultivationComponent>(out var cultivation));
            Assert.AreEqual(XianXia.Core.Cultivation.RealmStage.Mortal, cultivation.Realm);
            Assert.AreEqual(LifecycleState.Alive, life.State);
            Assert.AreEqual(EntityTag.Character, e.Tags);
        }

        [Test]
        public void Incapacitated_IsNotDead_And_Removed_IsNotDead()
        {
            var life = new LifecycleComponent(LifecycleState.Incapacitated);
            Assert.IsTrue(life.IsIncapacitated);
            Assert.IsFalse(life.IsDead);
            Assert.IsFalse(life.IsRemoved);

            life.State = LifecycleState.Removed;
            Assert.IsTrue(life.IsRemoved);
            Assert.IsFalse(life.IsDead);
        }

        [Test]
        public void Rejects_NonWhitelistedComponent()
        {
            var store = new EntityStore();
            var e = store.CreateCharacter(new DefinitionId("base", "character_labor_disciple")).Value;
            var result = e.AddComponent(new RogueComponent());
            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual(ErrorCode.InvalidOperation, result.Error.Code);
        }

        [Test]
        public void Query_FiltersByTagAndLifecycle()
        {
            var store = new EntityStore();
            store.CreateCharacter(new DefinitionId("base", "a"));
            var b = store.CreateCharacter(new DefinitionId("base", "b")).Value;
            b.Get<LifecycleComponent>().State = LifecycleState.Dead;

            var query = new EntityQuery(store);
            Assert.AreEqual(2, query.WithTag(EntityTag.Character).Count);
            Assert.AreEqual(1, query.WithLifecycle(LifecycleState.Dead).Count);
            Assert.AreEqual(1, query.WithLifecycle(LifecycleState.Alive).Count);
        }

        sealed class RogueComponent : IComponent
        {
        }
    }
}
