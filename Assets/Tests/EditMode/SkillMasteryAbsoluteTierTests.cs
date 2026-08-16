using NUnit.Framework;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Tests
{
    public sealed class SkillMasteryAbsoluteTierTests
    {
        [Test]
        public void Manual_Speed_Uses_Absolute_Tier_Not_Multiply()
        {
            var world = new SimulationWorld();
            var manual = new CultivationManualSpec
            {
                Id = new DefinitionId("base", "cultivation_test"),
                Name = "测功法",
                RequiredRealm = "炼气",
                CultivationSpeed = 8,
                BreakthroughProgress = 100,
                Mastery = new SkillMasteryProfile
                {
                    Tiers =
                    {
                        new SkillMasteryTierSpec { Tier = SkillMasteryTier.Entry, CultivationSpeed = 8 },
                        new SkillMasteryTierSpec { Tier = SkillMasteryTier.Minor, CultivationSpeed = 10 }
                    },
                    Breakthroughs =
                    {
                        SkillMasteryLookup.DefaultEntryToMinorBreakthrough()
                    }
                }
            };
            world.RegisterManual(manual);

            var ent = world.Entities.CreateCharacter(
                new DefinitionId("base", "character_t"), "测").Value;
            ent.Get<CultivationComponent>().Realm = RealmStage.QiRefining;
            ent.Get<CultivationComponent>().MinorStage = 1;

            Assert.IsTrue(new CultivationService().LearnManual(world, ent.Id, manual).IsSuccess);
            var cult = ent.Get<CultivationComponent>();
            Assert.AreEqual(8, cult.CultivationSpeed);
            Assert.AreEqual(SkillMasteryTier.Entry, cult.ManualMastery.Tier);

            cult.ManualMastery.Tier = SkillMasteryTier.Minor;
            Assert.IsTrue(new CultivationService().ReapplyManualModifiers(world, ent.Id).IsSuccess);
            Assert.AreEqual(10, cult.CultivationSpeed);
            // 不是 8*1.15
            Assert.AreNotEqual(9, cult.CultivationSpeed);
        }

        [Test]
        public void Art_Damage_Uses_Absolute_Tier()
        {
            var world = new SimulationWorld();
            var artId = new DefinitionId("base", "art_test_claw");
            var art = new CombatArtSpec
            {
                Id = artId,
                Name = "测爪",
                DamageAttackMult = 2.0,
                HitCount = 1,
                Mastery = new SkillMasteryProfile
                {
                    Tiers =
                    {
                        new SkillMasteryTierSpec { Tier = SkillMasteryTier.Entry, DamageAttackMult = 2.0 },
                        new SkillMasteryTierSpec { Tier = SkillMasteryTier.Minor, DamageAttackMult = 2.2 }
                    },
                    Breakthroughs =
                    {
                        SkillMasteryLookup.DefaultEntryToMinorBreakthrough()
                    }
                }
            };
            world.RegisterCombatArt(art);

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

            var arts = atk.Get<CombatArtsComponent>();
            arts.TryLearn(artId);
            arts.SetMastery(artId, SkillMasteryState.CreateEntry(art.Mastery));

            var melee = new MeleeCombatService();
            Assert.IsTrue(melee.CastEquippedArt(
                world, atk.Id, def.Id, 0, out var totalEntry, out _, out _).IsSuccess);
            Assert.AreEqual(20, totalEntry);

            arts.GetMastery(artId).Tier = SkillMasteryTier.Minor;
            CombatDamageRules.EnsureVitals(def);
            def.Get<CombatVitalsComponent>().CurrentHp = 1000;
            Assert.IsTrue(melee.CastEquippedArt(
                world, atk.Id, def.Id, 0, out var totalMinor, out _, out _).IsSuccess);
            Assert.AreEqual(22, totalMinor);
        }

        [Test]
        public void Content_Package_Loads_CombatArt_And_Mastery()
        {
            var root = FindBaseGamePackageRoot();
            if (root == null)
            {
                Assert.Ignore("BaseGame content path not found in this environment.");
                return;
            }

            var loaded = new XianXia.Data.Content.ContentPackageLoader().Load(new[] { root });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : "");
            Assert.IsTrue(loaded.Value.Registry.CombatArts.Count >= 2);
            Assert.IsTrue(loaded.Value.Registry.TryGetCultivation(
                new DefinitionId("base", "cultivation_jiang_lao_legacy"), out var cult));
            Assert.IsNotNull(cult.Mastery);
            Assert.Greater(cult.Mastery.Tiers.Count, 0);
            Assert.Greater(cult.Mastery.Breakthroughs.Count, 0);
        }

        static string FindBaseGamePackageRoot()
        {
            var candidates = new[]
            {
                System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Content", "BaseGame")),
                System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "Content", "BaseGame")),
                @"D:\UnityProjects\XianXia\Content\BaseGame"
            };
            for (var i = 0; i < candidates.Length; i++)
            {
                if (System.IO.Directory.Exists(candidates[i]) &&
                    System.IO.File.Exists(System.IO.Path.Combine(candidates[i], "manifest.json")))
                    return candidates[i];
            }

            return null;
        }
    }
}
