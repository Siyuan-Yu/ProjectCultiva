using NUnit.Framework;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Tests
{
    public sealed class SpiritVeilTests
    {
        [Test]
        public void Activate_Requires_Foundation_And_Deducts_Fixed_Cost()
        {
            var world = new SimulationWorld();
            var e = world.Entities.CreateCharacter(
                new DefinitionId("base", "character_veil"), "修士").Value;
            e.Get<CultivationComponent>().Realm = RealmStage.QiRefining;
            e.Get<AttributesComponent>().SetBase(AttributeId.SpiritPower, 180);
            e.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 100);
            CombatDamageRules.EnsureVitals(e);
            e.Get<CombatVitalsComponent>().CurrentSpiritPower = 180;

            var svc = new SpiritVeilService();
            Assert.IsTrue(svc.TryActivate(world, e.Id).IsFailure);
            Assert.AreEqual(SpiritVeilRules.MeleeEngageRange, svc.ResolveEngageRange(e), 0.01f);

            e.Get<CultivationComponent>().Realm = RealmStage.Foundation;
            Assert.IsTrue(svc.TryActivate(world, e.Id).IsSuccess);
            Assert.IsTrue(SpiritVeilService.IsActive(e));
            Assert.AreEqual(180 - SpiritVeilRules.FoundationActivateSpiritCost,
                e.Get<CombatVitalsComponent>().CurrentSpiritPower);
            Assert.AreEqual(SpiritVeilRules.FoundationRangedEngageRange, svc.ResolveEngageRange(e), 0.01f);
        }

        [Test]
        public void NonPlayer_Foundation_Auto_Activates_Player_Does_Not()
        {
            var world = new SimulationWorld();
            var npc = world.Entities.CreateNpc(
                new DefinitionId("base", "npc_veil"), "主管").Value;
            npc.Get<CultivationComponent>().Realm = RealmStage.Foundation;
            npc.Get<AttributesComponent>().SetBase(AttributeId.SpiritPower, 180);
            npc.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 100);
            CombatDamageRules.EnsureVitals(npc);
            npc.Get<CombatVitalsComponent>().CurrentSpiritPower = 180;

            var player = MakeFoundationWithSpirit(world, 180);
            var svc = new SpiritVeilService();

            Assert.IsTrue(svc.TryAutoActivateForNonPlayer(world, npc.Id).IsSuccess);
            Assert.IsTrue(SpiritVeilService.IsActive(npc));
            Assert.IsTrue(svc.TryAutoActivateForNonPlayer(world, player.Id).IsFailure);
            Assert.IsFalse(SpiritVeilService.IsActive(player));
        }

        [Test]
        public void Spirit_Empty_Auto_Dismisses_Veil()
        {
            var world = new SimulationWorld();
            var e = MakeFoundationWithSpirit(world, 180);
            var svc = new SpiritVeilService();
            Assert.IsTrue(svc.TryActivate(world, e.Id).IsSuccess);

            e.Get<CombatVitalsComponent>().CurrentSpiritPower = 0;
            Assert.IsTrue(svc.DeactivateIfSpiritEmpty(e));
            Assert.IsFalse(SpiritVeilService.IsActive(e));
            Assert.AreEqual(SpiritVeilRules.MeleeEngageRange, svc.ResolveEngageRange(e), 0.01f);
        }

        [Test]
        public void Combat_End_Dismisses_Both_Sides()
        {
            var world = new SimulationWorld();
            var a = MakeFoundationWithSpirit(world, 180);
            var b = MakeFoundationWithSpirit(world, 180);
            var svc = new SpiritVeilService();
            Assert.IsTrue(svc.TryActivate(world, a.Id).IsSuccess);
            Assert.IsTrue(svc.TryActivate(world, b.Id).IsSuccess);

            svc.DeactivateOnCombatEnd(a, b);
            Assert.IsFalse(SpiritVeilService.IsActive(a));
            Assert.IsFalse(SpiritVeilService.IsActive(b));
        }

        [Test]
        public void Veil_Does_Not_Change_Strike_Damage()
        {
            var world = new SimulationWorld();
            var atk = MakeFoundationWithSpirit(world, 180);
            var def = world.Entities.CreateNpc(
                new DefinitionId("base", "character_def"), "守").Value;
            atk.Get<AttributesComponent>().SetBase(AttributeId.Attack, 24);
            def.Get<AttributesComponent>().SetBase(AttributeId.Defense, 0);
            def.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 100);
            CombatDamageRules.EnsureVitals(def);

            var melee = new MeleeCombatService();
            var before = melee.ComputeStrikeDamage(world, atk, def);
            Assert.IsTrue(new SpiritVeilService().TryActivate(world, atk.Id).IsSuccess);
            var after = melee.ComputeStrikeDamage(world, atk, def);
            Assert.AreEqual(before, after);
        }

        static Entity MakeFoundationWithSpirit(SimulationWorld world, int spirit)
        {
            var e = world.Entities.CreateCharacter(
                new DefinitionId("base", "character_veil_" + spirit), "筑基").Value;
            e.Get<CultivationComponent>().Realm = RealmStage.Foundation;
            e.Get<AttributesComponent>().SetBase(AttributeId.SpiritPower, spirit);
            e.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 100);
            CombatDamageRules.EnsureVitals(e);
            e.Get<CombatVitalsComponent>().CurrentSpiritPower = spirit;
            return e;
        }
    }
}
