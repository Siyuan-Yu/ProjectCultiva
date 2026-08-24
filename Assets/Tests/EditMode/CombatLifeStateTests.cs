using NUnit.Framework;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Random;
using XianXia.Core.Simulation;
using XianXia.Core.World;

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
                "测试�");
            defender.AddComponent(new AttributesComponent(factory));
            defender.AddComponent(new LifecycleComponent(LifecycleState.Alive));
            defender.AddComponent(new CultivationComponent());
            defender.AddComponent(new CombatVitalsComponent());
            defender.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 20);
            defender.Get<AttributesComponent>().SetBase(AttributeId.Defense, 0);
            CombatDamageRules.EnsureVitals(defender);
            Assert.IsTrue(world.Entities.AddExisting(defender).IsSuccess);
            world.WorldPresence.SetAtSite(defender.Id, "node:test");

            attacker = new Entity(
                new EntityId(1002),
                new DefinitionId("test", "attacker"),
                EntityTag.Npc,
                "�");
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
            Assert.Greater(life.BleedOutAfterTick, world.Tick.Value);
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
            Assert.AreEqual(0UL, life.BleedOutAfterTick);
            Assert.IsTrue(entity.TryGet<CorpseComponent>(out var corpse));
            Assert.AreEqual(
                world.Tick.Value + CombatLifeStateService.CorpseLifetimeTicks,
                corpse.RemoveAfterTick);
        }

        [Test]
        public void Incapacitated_BleedOut_ConfirmsDeathWithoutAttack()
        {
            var world = CreateWorld(out var entity, out _);
            CombatLifeStateService.TryEnterIncapacitated(world, entity);
            Assert.IsTrue(entity.TryGet<LifecycleComponent>(out var life));
            world.Tick = new WorldTick(life.BleedOutAfterTick);
            CombatLifeStateService.TickLifeStateDecay(world);
            Assert.IsTrue(life.IsDead);
            Assert.IsTrue(entity.TryGet<CorpseComponent>(out _));
        }

        [Test]
        public void Corpse_DecaysToRemoved_AndClearsWorldAndLocalPresence()
        {
            var world = CreateWorld(out var entity, out _);
            CombatLifeStateService.TryEnterIncapacitated(world, entity);
            CombatLifeStateService.TryConfirmDeath(world, EntityId.None, entity, out _);
            Assert.IsTrue(entity.TryGet<CorpseComponent>(out var corpse));
            Assert.IsTrue(world.WorldPresence.TryGet(entity.Id, out _));

            var loc = new EntityLocationComponent();
            loc.LocationId = "loc:test";
            loc.SetPresentationOverride(1.5f, 2.5f);
            Assert.IsTrue(entity.AddComponent(loc).IsSuccess);

            world.Tick = new WorldTick(corpse.RemoveAfterTick);
            CombatLifeStateService.TickLifeStateDecay(world);

            Assert.IsTrue(entity.TryGet<LifecycleComponent>(out var life));
            Assert.IsTrue(life.IsRemoved);
            Assert.IsFalse(world.WorldPresence.TryGet(entity.Id, out _));
            Assert.IsTrue(entity.TryGet<EntityLocationComponent>(out var cleared));
            Assert.IsFalse(cleared.HasLocation);
            Assert.IsFalse(cleared.HasPresentationOverride);
            Assert.IsTrue(CombatLifeStateService.ShouldHideFromSpawn(entity));
        }

        [Test]
        public void LifeStateCountdown_IncapAndCorpse()
        {
            var world = CreateWorld(out var entity, out _);
            CombatLifeStateService.TryEnterIncapacitated(world, entity);
            Assert.IsTrue(
                CombatLifeStateService.TryGetLifeStateCountdown(
                    world, entity, out var label, out var sec));
            Assert.AreEqual("弥留", label);
            Assert.AreEqual((int)CombatLifeStateService.BleedOutDurationTicks, sec);
            Assert.AreEqual(
                "弥留 " + sec + "s",
                CombatLifeStateService.FormatLifeStateWithCountdown(world, entity));

            CombatLifeStateService.TryConfirmDeath(world, EntityId.None, entity, out _);
            Assert.IsTrue(
                CombatLifeStateService.TryGetLifeStateCountdown(
                    world, entity, out label, out sec));
            Assert.AreEqual("尸体", label);
            Assert.AreEqual((int)CombatLifeStateService.CorpseLifetimeTicks, sec);
        }

        [Test]
        public void Downed_CannotFight_OrTravel()
        {
            var world = CreateWorld(out var entity, out _);
            Assert.IsTrue(CombatLifeStateService.CanFight(entity));
            CombatLifeStateService.TryEnterIncapacitated(world, entity);
            Assert.IsFalse(CombatLifeStateService.CanFight(entity));
            Assert.IsFalse(WorldTravelService.CanReceiveTravelOrder(world, entity.Id));

            CombatLifeStateService.TryConfirmDeath(world, EntityId.None, entity, out _);
            Assert.IsFalse(CombatLifeStateService.CanFight(entity));
            Assert.IsFalse(CombatLifeStateService.CanBeAttacked(entity));
            Assert.IsFalse(WorldTravelService.CanReceiveTravelOrder(world, entity.Id));
        }
    }
}
