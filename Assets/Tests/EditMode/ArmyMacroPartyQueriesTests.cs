using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class ArmyMacroPartyQueriesTests
    {
        const string TestFactionA = "test:faction_a";
        const string TestNodeA = "test:node_a";

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            Ch01HexPrototypeMapBuilder.Build(world);
            world.Strategic.PlayerFactionId = TestFactionA;return world;
        }

        static EntityId SpawnCharacter(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            var entity = created.Value;
            entity.Get<FactionMembershipComponent>().Assign(TestFactionA, FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(entity.Id, TestNodeA);
            return entity.Id;
        }

        [Test]
        public void LingeringOffer_ActingArmyMembersAreMandatory_NotOptional()
        {
            var world = CreateWorld();
            var a = SpawnCharacter(world, "A");
            var b = SpawnCharacter(world, "B");
            var c = SpawnCharacter(world, "C");
            Assert.IsTrue(ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a, b, c }).IsSuccess);

            var enemy = new ArmyStack
            {
                Id = "enemy:stack",
                FactionId = "enemy:faction",
                SiteId = TestNodeA,
                IsBattlefieldRemnant = true,
                IncapacitatedMemberCount = 1
            };
            world.Strategic.Armies.Register(enemy);
            world.Strategic.Encounter.ArmyStackId = enemy.Id;
            world.Strategic.Encounter.BattlefieldLingering = true;

            var mandatory = new List<EntityId> { a };
            ArmyMacroPartyQueries.ExpandMandatoryLivingToFormalArmies(world, mandatory);
            Assert.AreEqual(3, mandatory.Count);

            Assert.IsTrue(BattleOfferService.TryBuildOfferForLingeringBattlefield(
                world,
                new[] { a, b, c },
                a,
                "残留战场",
                mandatory));

            var snap = world.Strategic.Participants;
            Assert.AreEqual(BattleParticipantKind.MandatoryFriendly, snap.FindByEntity(a).Kind);
            Assert.AreEqual(BattleParticipantKind.MandatoryFriendly, snap.FindByEntity(b).Kind);
            Assert.AreEqual(BattleParticipantKind.MandatoryFriendly, snap.FindByEntity(c).Kind);
            for (var i = 0; i < snap.Records.Count; i++)
            {
                var rec = snap.Records[i];
                if (rec.EntityId == a || rec.EntityId == b || rec.EntityId == c)
                    Assert.AreNotEqual(BattleParticipantKind.OptionalFriendly, rec.Kind);
            }
        }
    }
}
