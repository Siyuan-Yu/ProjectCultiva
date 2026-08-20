using NUnit.Framework;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Random;
using XianXia.Core.Simulation;

namespace XianXia.Tests
{
    public sealed class CombatLifeStateTests
    {
        static SimulationWorld CreateWorld(out Entity defender, out Entity attacker)
        {
            var world = new SimulationWorld(random: new DeterministicRandom(11));
            var factory = new ModifierIdFactory();
            defender = new Entity(
                new EntityId(1001),
                new DefinitionId("test", "fighter"),
                EntityTag.Npc,
                "测试者");
            defender.AddComponent(new AttributesComponent(factory));
            defender.AddComponent(new LifecycleComponent(LifecycleState.Alive));
            defender.AddComponent(new CultivationComponent());
            defender.AddComponent(new CombatVitalsComponent());
            defender.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 20);
            defender.Get<AttributesComponent>().SetBase(AttributeId.Defense, 0);
            CombatDamageRules.EnsureVitals(defender);
            Assert.IsTrue(world.Entities.AddExisting(defender).IsSuccess);

            attacker = new Entity(
                new EntityId(1002),
                new DefinitionId("test", "attacker"),
                EntityTag.Npc,
                "攻");
            attacker.AddComponent(new AttributesComponent(factory));
            attacker.AddComponent(new LifecycleComponent(LifecycleState.Alive));
            attacker.AddComponent(new CombatVitalsComponent());
            attacker.Get<AttributesComponent>().SetBase(AttributeId.Attack, 20);
            CombatDamageRules.EnsureVitals(attacker);
            Assert.IsTrue(world.Entities.AddExisting(attacker).IsSuccess);
            return world;
        }

        [Test]
        public void ZeroHp_EntersIncapacitated_NotDead()
        {
            var world = CreateWorld(out var entity, out var attacker);
            var melee = new MeleeCombatService();
            var hit = melee.ApplyStrike(
                world,
                attacker.Id,
                entity.Id,
                out _,
                out var down);
            Assert.IsTrue(hit.IsSuccess);
            Assert.IsTrue(down);
            Assert.IsTrue(entity.TryGet<LifecycleComponent>(out var life));
            Assert.IsTrue(life.IsIncapacitated);
            Assert.IsFalse(life.IsDead);
        }

        [Test]
        public void Incapacitated_SecondStrike_ConfirmsDeathAndCorpse()
        {
            var world = CreateWorld(out var entity, out var attacker);
            CombatLifeStateService.TryEnterIncapacitated(world, entity);

            var melee = new MeleeCombatService();
            var hit = melee.ApplyStrike(
                world,
                attacker.Id,
                entity.Id,
                out _,
                out var dead);
            Assert.IsTrue(hit.IsSuccess);
            Assert.IsTrue(dead);
            Assert.IsTrue(entity.TryGet<LifecycleComponent>(out var life));
            Assert.IsTrue(life.IsDead);
            Assert.IsTrue(entity.TryGet<CorpseComponent>(out var corpse));
            Assert.Greater(corpse.RemoveAfterTick, world.Tick.Value);
        }

        [Test]
        public void Corpse_DecaysToRemoved_AfterLifetime()
        {
            var world = CreateWorld(out var entity, out _);
            CombatLifeStateService.TryEnterIncapacitated(world, entity);
            CombatLifeStateService.TryConfirmDeath(world, EntityId.None, entity, out _);
            Assert.IsTrue(entity.TryGet<CorpseComponent>(out var corpse));
            world.Tick = new WorldTick(corpse.RemoveAfterTick);
            CombatLifeStateService.TickCorpseDecay(world);
            Assert.IsTrue(entity.TryGet<LifecycleComponent>(out var life));
            Assert.IsTrue(life.IsRemoved);
        }
    }
}
