using NUnit.Framework;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Tests
{
    public sealed class MeleeCombatAlphaTests
    {
        [Test]
        public void Strike_Damages_And_Art_Increases_Damage()
        {
            var world = new SimulationWorld();
            world.RegisterCombatArt(new CombatArtSpec
            {
                Id = new DefinitionId("base", "art_spirit_strike"),
                Name = "灵力灌注",
                AttackBonusPercent = 0.5,
                DamageFlat = 0
            });

            var atk = world.Entities.CreateCharacter(
                new DefinitionId("base", "character_atk"), "攻").Value;
            var def = world.Entities.CreateNpc(
                new DefinitionId("base", "character_def"), "守").Value;
            atk.Get<AttributesComponent>().SetBase(AttributeId.Attack, 20);
            atk.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 100);
            def.Get<AttributesComponent>().SetBase(AttributeId.Defense, 0);
            def.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 100);
            CombatDamageRules.EnsureVitals(atk);
            CombatDamageRules.EnsureVitals(def);

            var melee = new MeleeCombatService();
            var baseDmg = melee.ComputeStrikeDamage(world, atk, def);
            Assert.AreEqual(20, baseDmg);

            atk.Get<CombatArtsComponent>().TryLearn(new DefinitionId("base", "art_spirit_strike"));
            var buffed = melee.ComputeStrikeDamage(world, atk, def);
            Assert.AreEqual(30, buffed);
        }

        [Test]
        public void Defeat_Npc_Sets_Encounter_Flag()
        {
            var world = new SimulationWorld();
            var atk = world.Entities.CreateCharacter(
                new DefinitionId("base", "character_atk"), "攻").Value;
            var def = world.Entities.CreateNpc(
                new DefinitionId("base", "character_def"), "守").Value;
            atk.Get<AttributesComponent>().SetBase(AttributeId.Attack, 50);
            atk.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 100);
            def.Get<AttributesComponent>().SetBase(AttributeId.Defense, 0);
            def.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 10);
            def.AddComponent(new EncounterLinkComponent { EncounterId = "cave_ch01_shade" });
            CombatDamageRules.EnsureVitals(atk);
            CombatDamageRules.EnsureVitals(def);

            var melee = new MeleeCombatService();
            Assert.IsTrue(melee.ApplyStrike(world, atk.Id, def.Id, out _, out var defeated).IsSuccess);
            Assert.IsTrue(defeated);
            Assert.IsTrue(def.Get<LifecycleComponent>().IsDead);
            Assert.IsTrue(world.Flags.Has(ContentConditionEvaluator.EncounterFlag("cave_ch01_shade")));
        }

        [Test]
        public void Active_Art_Liezhao_Three_Hits_At_200_Percent()
        {
            var world = new SimulationWorld();
            var artId = new DefinitionId("base", "art_liezhao_claw");
            world.RegisterCombatArt(new CombatArtSpec
            {
                Id = artId,
                Name = "裂爪击",
                Grade = "黄阶中级",
                DamageAttackMult = 2.0,
                HitCount = 3
            });
            var atk = world.Entities.CreateCharacter(
                new DefinitionId("base", "character_atk"), "攻").Value;
            var def = world.Entities.CreateNpc(
                new DefinitionId("base", "character_def"), "守").Value;
            atk.Get<AttributesComponent>().SetBase(AttributeId.Attack, 10);
            atk.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 100);
            def.Get<AttributesComponent>().SetBase(AttributeId.Defense, 0);
            def.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 1000);
            CombatDamageRules.EnsureVitals(atk);
            CombatDamageRules.EnsureVitals(def);
            atk.Get<CombatArtsComponent>().TryLearn(artId);

            var melee = new MeleeCombatService();
            Assert.IsTrue(melee.CastEquippedArt(
                world, atk.Id, def.Id, 0, out var total, out var hits, out var defeated).IsSuccess);
            Assert.AreEqual(3, hits);
            Assert.AreEqual(60, total); // 10*2 * 3
            Assert.IsFalse(defeated);
            Assert.AreEqual(940, def.Get<CombatVitalsComponent>().CurrentHp);
        }

        [Test]
        public void Active_Art_Kaishan_One_Hit_At_500_Percent()
        {
            var world = new SimulationWorld();
            var artId = new DefinitionId("base", "art_kaishan_fist");
            world.RegisterCombatArt(new CombatArtSpec
            {
                Id = artId,
                Name = "开山拳",
                DamageAttackMult = 5.0,
                HitCount = 1
            });
            var atk = world.Entities.CreateCharacter(
                new DefinitionId("base", "character_atk"), "攻").Value;
            var def = world.Entities.CreateNpc(
                new DefinitionId("base", "character_def"), "守").Value;
            atk.Get<AttributesComponent>().SetBase(AttributeId.Attack, 10);
            atk.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 100);
            def.Get<AttributesComponent>().SetBase(AttributeId.Defense, 0);
            def.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 200);
            CombatDamageRules.EnsureVitals(atk);
            CombatDamageRules.EnsureVitals(def);
            atk.Get<CombatArtsComponent>().TryLearn(artId);

            Assert.IsTrue(new MeleeCombatService().CastEquippedArt(
                world, atk.Id, def.Id, 0, out var total, out var hits, out _).IsSuccess);
            Assert.AreEqual(1, hits);
            Assert.AreEqual(50, total);
        }

        [Test]
        public void EnsureVitals_Does_Not_Refill_Empty_Spirit_Shield()
        {
            var world = new SimulationWorld();
            var ent = world.Entities.CreateNpc(
                new DefinitionId("base", "character_shade"), "残影").Value;
            ent.Get<AttributesComponent>().SetBase(AttributeId.MaxHp, 80);
            ent.Get<AttributesComponent>().SetBase(AttributeId.SpiritPower, 20);
            ent.Get<AttributesComponent>().SetBase(AttributeId.Defense, 0);
            ent.Get<XianXia.Core.Cultivation.CultivationComponent>().Realm =
                XianXia.Core.Cultivation.RealmStage.QiRefining;
            CombatDamageRules.EnsureVitals(ent);
            var vitals = ent.Get<CombatVitalsComponent>();
            Assert.AreEqual(80, vitals.CurrentHp);
            Assert.AreEqual(20, vitals.CurrentSpiritPower);

            vitals.CurrentSpiritPower = 0;
            vitals.CurrentHp = 70;
            CombatDamageRules.EnsureVitals(ent);
            Assert.AreEqual(0, vitals.CurrentSpiritPower);
            Assert.AreEqual(70, vitals.CurrentHp);
        }

        [Test]
        public void Equip_Slots_Max_Six()
        {
            var world = new SimulationWorld();
            var artsComp = world.Entities.CreateCharacter(
                new DefinitionId("base", "c"), "c").Value.Get<CombatArtsComponent>();
            for (var i = 0; i < 6; i++)
            {
                var id = new DefinitionId("base", "art_" + i);
                world.RegisterCombatArt(new CombatArtSpec { Id = id, Name = "a" + i });
                Assert.IsTrue(artsComp.TryLearn(id));
            }

            var seventh = new DefinitionId("base", "art_7");
            world.RegisterCombatArt(new CombatArtSpec { Id = seventh, Name = "a7" });
            Assert.IsTrue(artsComp.TryLearn(seventh)); // 可学
            Assert.IsFalse(artsComp.TryEquipFirstEmpty(seventh)); // 栏满装不上
            Assert.IsTrue(artsComp.GetEquipped(0).HasValue);
            Assert.IsTrue(artsComp.GetEquipped(5).HasValue);
        }
    }
}
