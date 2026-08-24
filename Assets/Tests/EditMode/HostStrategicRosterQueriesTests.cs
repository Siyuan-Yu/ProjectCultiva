using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    /// <summary>Host 战略层角色／军队列表只读查询（非 IMGUI）�?/summary>
    public sealed class HostStrategicRosterQueriesTests
    {
        const string FactionA = "test:faction_a";
        const string NodeA = "test:node_a";

        [Test]
        public void Roster_CollectPlayerCharacters_IncludesPartyAndFactionMembers()
        {
            var world = BootstrapNodeWithCharacters(out var leader, out var recruit);
            var rows = new List<StrategicCharacterRosterRow>();
            var party = new List<EntityId> { leader };
            HostStrategicRosterQueries.CollectPlayerCharacters(world, FactionA, party, rows);
            Assert.GreaterOrEqual(rows.Count, 2);
            Assert.IsTrue(ContainsCharacter(rows, leader));
            Assert.IsTrue(ContainsCharacter(rows, recruit));
        }

        [Test]
        public void Roster_CollectPlayerArmies_EmptyWhenNoArmy()
        {
            var world = BootstrapNodeWithCharacters(out _, out _);
            var rows = new List<StrategicArmyRosterRow>();
            HostStrategicRosterQueries.CollectPlayerArmies(world, FactionA, rows);
            Assert.AreEqual(0, rows.Count);
        }

        [Test]
        public void Roster_CollectPlayerArmies_ListsCreatedArmy()
        {
            var world = BootstrapNodeWithCharacters(out var leader, out var recruit);
            var create = ArmyUiCommands.TryCreateArmy(world, NodeA, FactionA, new[] { leader, recruit });
            Assert.IsTrue(create.IsSuccess);

            var rows = new List<StrategicArmyRosterRow>();
            HostStrategicRosterQueries.CollectPlayerArmies(world, FactionA, rows);
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(create.Value.ArmyId, rows[0].ArmyId);
            Assert.AreEqual(2, rows[0].MemberCount);
            Assert.Greater(rows[0].CombatPower, 0);
        }

        [Test]
        public void Roster_CollectUngroupedCharactersAtSite_ExcludesGrouped()
        {
            var world = BootstrapNodeWithCharacters(out var leader, out var recruit);
            var create = ArmyUiCommands.TryCreateArmy(world, NodeA, FactionA, new[] { leader });
            Assert.IsTrue(create.IsSuccess);

            var ungrouped = new List<EntityId>();
            HostStrategicRosterQueries.CollectUngroupedCharactersAtSite(
                world, NodeA, FactionA, new[] { leader, recruit }, ungrouped);
            Assert.AreEqual(1, ungrouped.Count);
            Assert.AreEqual(recruit, ungrouped[0]);
        }

        [Test]
        public void Roster_CollectUngroupedPlayerCharacters_ExcludesGrouped()
        {
            var world = BootstrapNodeWithCharacters(out var leader, out var recruit);
            var create = ArmyUiCommands.TryCreateArmy(world, NodeA, FactionA, new[] { leader });
            Assert.IsTrue(create.IsSuccess);

            var ungrouped = new List<EntityId>();
            HostStrategicRosterQueries.CollectUngroupedPlayerCharacters(
                world, FactionA, new[] { leader, recruit }, ungrouped);
            Assert.AreEqual(1, ungrouped.Count);
            Assert.AreEqual(recruit, ungrouped[0]);
        }

        [Test]
        public void Roster_CollectPlayerCharacters_ExcludesOverlordFactionMembers()
        {
            var world = BootstrapNodeWithCharacters(out var leader, out var recruit);
            world.Strategic.PlayerFactionId = StrategicFactionCatalog.PlayerFactionId;
            world.Entities.TryGet(leader, out var leaderEnt);
            world.Entities.TryGet(recruit, out var recruitEnt);
            leaderEnt.Get<FactionMembershipComponent>().Assign(StrategicFactionCatalog.PlayerFactionId, FactionRoleKind.Member);
            recruitEnt.Get<FactionMembershipComponent>().Assign(StrategicFactionCatalog.HuangcunLaborId, FactionRoleKind.LaborDisciple);

            var rows = new List<StrategicCharacterRosterRow>();
            HostStrategicRosterQueries.CollectPlayerCharacters(
                world, StrategicFactionCatalog.PlayerFactionId, new[] { leader }, rows);
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(leader, rows[0].CharacterId);
            Assert.AreEqual(StrategicFactionCatalog.PlayerFactionId, rows[0].FactionId);
        }

        [Test]
        public void Roster_CollectPlayerCharacters_IncludesIncapacitatedAndCorpse_ButNotSelectable()
        {
            var world = BootstrapNodeWithCharacters(out var leader, out var recruit);
            Assert.IsTrue(world.Entities.TryGet(recruit, out var recruitEnt));
            Assert.IsTrue(XianXia.Core.Combat.CombatLifeStateService.TryEnterIncapacitated(world, recruitEnt));

            var rows = new List<StrategicCharacterRosterRow>();
            HostStrategicRosterQueries.CollectPlayerCharacters(
                world, FactionA, new[] { leader }, rows);
            Assert.IsTrue(ContainsCharacter(rows, recruit));
            var recruitRow = rows.Find(r => r.CharacterId == recruit);
            Assert.NotNull(recruitRow);
            Assert.AreEqual("弥留", recruitRow.LifeStateLabel);
            Assert.IsFalse(recruitRow.CanSelectForArmyCreation);

            var ungrouped = new List<EntityId>();
            HostStrategicRosterQueries.CollectUngroupedPlayerCharacters(
                world, FactionA, new[] { leader }, ungrouped);
            Assert.IsFalse(ungrouped.Contains(recruit));
        }

        static bool ContainsCharacter(List<StrategicCharacterRosterRow> rows, EntityId id)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].CharacterId == id)
                    return true;
            }

            return false;
        }

        static SimulationWorld BootstrapNodeWithCharacters(out EntityId leader, out EntityId recruit)
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = FactionA;
            var l = world.Entities.CreateCharacter(new DefinitionId("test", "leader"), "Leader");
            var r = world.Entities.CreateCharacter(new DefinitionId("test", "recruit"), "Recruit");
            Assert.IsTrue(l.IsSuccess);
            Assert.IsTrue(r.IsSuccess);
            leader = l.Value.Id;
            recruit = r.Value.Id;
            l.Value.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            r.Value.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(leader, NodeA);
            world.WorldPresence.SetAtSite(recruit, NodeA);
            return world;
        }
    }
}
