using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    /// <summary>152 Phase A �?Formal Army Domain + ArmyMembership�?/summary>
    public sealed class ArmyDomainTests
    {
        const string TestFactionA = "test:faction_a";
        const string TestFactionB = "test:faction_b";
        const string TestNodeA = "test:node_a";
        const string TestNodeB = "test:node_b";

        static SimulationWorld CreateFixtureWorld()
        {
            var world = new SimulationWorld();
            Ch01HexPrototypeMapBuilder.Build(world);
            return world;
        }

        static EntityId CreateCharacter(
            SimulationWorld world,
            string displayName,
            string factionId,
            string nodeId)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", displayName), displayName);
            Assert.IsTrue(created.IsSuccess, created.IsFailure ? created.Error.ToString() : "");
            var entity = created.Value;
            entity.Get<FactionMembershipComponent>().Assign(factionId, FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(entity.Id, nodeId);
            return entity.Id;
        }

        static void AssertMembershipSync(SimulationWorld world)
        {
            var sync = ArmyInvariants.AssertMembershipSync(world);
            Assert.IsTrue(sync.IsSuccess, sync.IsFailure ? sync.Error.ToString() : "");
        }

        [Test]
        public void Army_Create_TwoMembers_SameFaction_Success()
        {
            var world = CreateFixtureWorld();
            var a = CreateCharacter(world, "A", TestFactionA, TestNodeA);
            var b = CreateCharacter(world, "B", TestFactionA, TestNodeA);

            var created = ArmyService.CreateArmy(
                world,
                TestFactionA,
                TestNodeA,
                new[] { a, b });
            Assert.IsTrue(created.IsSuccess, created.IsFailure ? created.Error.ToString() : "");

            var army = created.Value;
            Assert.AreEqual(TestFactionA, army.FactionId);
            Assert.AreEqual(a, army.LeaderCharacterId);
            Assert.AreEqual(2, army.MemberCharacterIds.Count);
            Assert.IsTrue(army.TryGetFormationSiteId(world, out var formationSiteId));
            Assert.AreEqual(TestNodeA, formationSiteId);
            Assert.AreEqual(FormalArmyState.Idle, army.State);

            Assert.IsTrue(ArmyService.TryGetArmyForCharacter(world, a, out var armyForA));
            Assert.AreEqual(army.ArmyId, armyForA.ArmyId);
            AssertMembershipSync(world);
        }

        [Test]
        public void Army_Create_CrossFaction_Fails()
        {
            var world = CreateFixtureWorld();
            var a = CreateCharacter(world, "A", TestFactionA, TestNodeA);
            var b = CreateCharacter(world, "B", TestFactionA, TestNodeA);
            var x = CreateCharacter(world, "X", TestFactionB, TestNodeA);

            var created = ArmyService.CreateArmy(
                world,
                TestFactionA,
                TestNodeA,
                new[] { a, b, x });
            Assert.IsTrue(created.IsFailure);
            StringAssert.Contains("Cross-faction", created.Error.Message);
            Assert.AreEqual(0, world.Strategic.FormalArmies.Armies.Count);
            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, a, out _));
            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, b, out _));
            AssertMembershipSync(world);
        }

        [Test]
        public void Army_Create_FailedValidation_LeavesNoPartialState()
        {
            var world = CreateFixtureWorld();
            var a = CreateCharacter(world, "A", TestFactionA, TestNodeA);
            var b = CreateCharacter(world, "B", TestFactionA, TestNodeA);
            var x = CreateCharacter(world, "X", TestFactionB, TestNodeA);

            Assert.AreEqual(0, world.Strategic.FormalArmies.Armies.Count);

            var created = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a, b, x });
            Assert.IsTrue(created.IsFailure);
            Assert.AreEqual(0, world.Strategic.FormalArmies.Armies.Count);
            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, a, out _));
            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, b, out _));
            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, x, out _));
            AssertMembershipSync(world);
        }

        [Test]
        public void Army_Membership_OneArmyPerCharacter()
        {
            var world = CreateFixtureWorld();
            var a = CreateCharacter(world, "A", TestFactionA, TestNodeA);
            var b = CreateCharacter(world, "B", TestFactionA, TestNodeA);
            var c = CreateCharacter(world, "C", TestFactionA, TestNodeA);

            var army1 = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a, b });
            Assert.IsTrue(army1.IsSuccess, army1.IsFailure ? army1.Error.ToString() : "");

            var army2 = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a, c });
            Assert.IsTrue(army2.IsFailure);
            StringAssert.Contains("already in an army", army2.Error.Message);
        }

        [Test]
        public void Army_Form_OnlyOnFriendlyNode()
        {
            var world = CreateFixtureWorld();
            var a = CreateCharacter(world, "A", TestFactionA, TestNodeA);
            var b = CreateCharacter(world, "B", TestFactionA, TestNodeA);

            var created = ArmyService.CreateArmy(world, TestFactionA, TestNodeB, new[] { a, b });
            Assert.IsTrue(created.IsFailure);
            StringAssert.Contains("friendly node", created.Error.Message);
        }

        [Test]
        public void Army_Disband_ClearsMembership()
        {
            var world = CreateFixtureWorld();
            var a = CreateCharacter(world, "A", TestFactionA, TestNodeA);
            var b = CreateCharacter(world, "B", TestFactionA, TestNodeA);

            var created = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a, b });
            Assert.IsTrue(created.IsSuccess, created.IsFailure ? created.Error.ToString() : "");

            var disband = ArmyService.DisbandArmy(world, created.Value.ArmyId);
            Assert.IsTrue(disband.IsSuccess, disband.IsFailure ? disband.Error.ToString() : "");
            Assert.IsFalse(world.Strategic.FormalArmies.TryGet(created.Value.ArmyId, out _));
            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, a, out _));
            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, b, out _));
            AssertMembershipSync(world);
        }

        [Test]
        public void Army_Garrisoned_DoesNotDisband()
        {
            var world = CreateFixtureWorld();
            var a = CreateCharacter(world, "A", TestFactionA, TestNodeA);
            var b = CreateCharacter(world, "B", TestFactionA, TestNodeA);

            var created = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a, b });
            Assert.IsTrue(created.IsSuccess, created.IsFailure ? created.Error.ToString() : "");

            var garrison = ArmyService.GarrisonArmy(world, created.Value.ArmyId);
            Assert.IsTrue(garrison.IsSuccess, garrison.IsFailure ? garrison.Error.ToString() : "");
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(created.Value.ArmyId, out var army));
            Assert.AreEqual(FormalArmyState.Garrisoned, army.State);
            Assert.AreEqual(2, army.MemberCharacterIds.Count);
            Assert.IsTrue(ArmyService.TryGetArmyForCharacter(world, a, out _));
            Assert.IsTrue(ArmyService.TryGetArmyForCharacter(world, b, out _));
            AssertMembershipSync(world);
        }

        [Test]
        public void Army_LeaderFallback_OnLeaderInvalid()
        {
            var world = CreateFixtureWorld();
            var a = CreateCharacter(world, "A", TestFactionA, TestNodeA);
            var b = CreateCharacter(world, "B", TestFactionA, TestNodeA);

            var created = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a, b });
            Assert.IsTrue(created.IsSuccess, created.IsFailure ? created.Error.ToString() : "");
            var armyId = created.Value.ArmyId;

            Assert.IsTrue(world.Entities.TryGet(a, out var leaderEntity));
            leaderEntity.Get<LifecycleComponent>().State = LifecycleState.Dead;

            var refresh = ArmyService.RefreshLeader(world, armyId);
            Assert.IsTrue(refresh.IsSuccess, refresh.IsFailure ? refresh.Error.ToString() : "");
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out var army));
            Assert.AreEqual(b, army.LeaderCharacterId);
            Assert.AreEqual(2, army.MemberCharacterIds.Count);
            AssertMembershipSync(world);
        }

        [Test]
        public void Army_RefreshLeader_ForceDisbands_WhenNoValidLeader_EvenIfNodeHostile()
        {
            var world = CreateFixtureWorld();
            var a = CreateCharacter(world, "A", TestFactionA, TestNodeA);

            var created = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a });
            Assert.IsTrue(created.IsSuccess, created.IsFailure ? created.Error.ToString() : "");
            var armyId = created.Value.ArmyId;

            Assert.IsTrue(world.Entities.TryGet(a, out var leaderEntity));
            leaderEntity.Get<LifecycleComponent>().State = LifecycleState.Dead;

            Assert.IsTrue(world.Strategic.Sites.TryGet(TestNodeA, out var node));
            node.OwnerFactionId = TestFactionB;

            var refresh = ArmyService.RefreshLeader(world, armyId);
            Assert.IsTrue(refresh.IsSuccess, refresh.IsFailure ? refresh.Error.ToString() : "");
            Assert.IsFalse(world.Strategic.FormalArmies.TryGet(armyId, out _));
            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, a, out _));
            AssertMembershipSync(world);
        }

        [Test]
        public void NodeOwner_TestFixture_NotClearedByBootstrap()
        {
            var world = CreateFixtureWorld();
            Assert.IsTrue(world.Strategic.Sites.TryGet(TestNodeA, out var nodeBefore));
            Assert.AreEqual(TestFactionA, nodeBefore.OwnerFactionId);

            StrategicBootstrap.ApplyCh01Defaults(world);

            Assert.IsTrue(world.Strategic.Sites.TryGet(TestNodeA, out var nodeAfter));
            Assert.AreEqual(TestFactionA, nodeAfter.OwnerFactionId, "Bootstrap must not clear established OwnerFactionId.");
            Assert.IsTrue(world.Strategic.Sites.TryGet(Ch01HexPrototypeMapBuilder.SiteHuangcun, out var huangcun));
            Assert.IsTrue(string.IsNullOrEmpty(huangcun.OwnerFactionId), "Ch01 prototype sites remain ownerless.");
        }

        [Test]
        public void Army_MembershipReverseIndex_CannotDriftFromMembers()
        {
            var world = CreateFixtureWorld();
            var a = CreateCharacter(world, "A", TestFactionA, TestNodeA);
            var b = CreateCharacter(world, "B", TestFactionA, TestNodeA);
            var c = CreateCharacter(world, "C", TestFactionA, TestNodeA);

            var ab = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a, b });
            Assert.IsTrue(ab.IsSuccess, ab.IsFailure ? ab.Error.ToString() : "");
            AssertMembershipSync(world);

            ArmyService.GarrisonArmy(world, ab.Value.ArmyId);
            AssertMembershipSync(world);

            var bc = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { c });
            Assert.IsTrue(bc.IsSuccess, bc.IsFailure ? bc.Error.ToString() : "");
            AssertMembershipSync(world);

            ArmyService.DisbandArmy(world, bc.Value.ArmyId);
            AssertMembershipSync(world);

            ArmyService.DisbandArmy(world, ab.Value.ArmyId);
            AssertMembershipSync(world);
        }
    }
}
