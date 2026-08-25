using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.World;

namespace XianXia.Tests
{
    public sealed class PlayerPartyRuntimeTests
    {
        [Test]
        public void PlayerParty_MaxSixMembers()
        {
            var party = new PlayerPartyRuntime();
            party.TryInitialize(new EntityId(1), out _);
            for (ulong i = 2; i <= 6; i++)
                Assert.IsTrue(party.TryAddMember(null, Roster(1, 2, 3, 4, 5, 6), new EntityId(i), out _));
            Assert.AreEqual(6, party.Count);
        }

        [Test]
        public void PlayerParty_CannotAddSeventh()
        {
            var party = new PlayerPartyRuntime();
            party.TryInitialize(new EntityId(1), out _);
            for (ulong i = 2; i <= 6; i++)
                party.TryAddMember(null, Roster(1, 2, 3, 4, 5, 6), new EntityId(i), out _);
            Assert.IsFalse(party.TryAddMember(null, Roster(1, 2, 3, 4, 5, 6, 7), new EntityId(7), out var err));
            Assert.IsNotEmpty(err);
        }

        [Test]
        public void PlayerParty_AlwaysOneActive()
        {
            var party = new PlayerPartyRuntime();
            party.TryInitialize(new EntityId(10), out _);
            party.TryAddMember(null, Roster(10, 20), new EntityId(20), out _);
            Assert.IsTrue(party.IsActive(new EntityId(10)));
            Assert.IsFalse(party.IsActive(new EntityId(20)));
            Assert.IsTrue(party.TrySetActive(null, new EntityId(20), out _));
            Assert.IsTrue(party.IsActive(new EntityId(20)));
            Assert.IsTrue(party.IsFollower(new EntityId(10)));
        }

        [Test]
        public void PlayerParty_NonMemberCannotSetActive()
        {
            var party = new PlayerPartyRuntime();
            party.TryInitialize(new EntityId(1), out _);
            Assert.IsFalse(party.TrySetActive(null, new EntityId(99), out var err));
            Assert.IsNotEmpty(err);
        }

        [Test]
        public void PlayerParty_DyingCannotJoin()
        {
            var world = MakeWorld(new EntityId(2), LifecycleState.Incapacitated);
            world.LocalMap.AddOccupant(new EntityId(1));
            var party = new PlayerPartyRuntime();
            party.TryInitialize(new EntityId(1), out _);
            Assert.IsFalse(party.TryAddMember(world, Roster(1, 2), new EntityId(2), out _));
        }

        [Test]
        public void PlayerParty_DeadCannotJoin()
        {
            var world = MakeWorld(new EntityId(2), LifecycleState.Dead);
            world.LocalMap.AddOccupant(new EntityId(1));
            var party = new PlayerPartyRuntime();
            party.TryInitialize(new EntityId(1), out _);
            Assert.IsFalse(party.TryAddMember(world, Roster(1, 2), new EntityId(2), out _));
        }

        [Test]
        public void PlayerParty_DyingCannotSetActive()
        {
            var world = MakeWorld(new EntityId(2), LifecycleState.Alive);
            world.LocalMap.AddOccupant(new EntityId(1));
            var party = new PlayerPartyRuntime();
            party.TryInitialize(new EntityId(1), out _);
            party.TryAddMember(world, Roster(1, 2), new EntityId(2), out _);
            world.Entities.TryGet(new EntityId(2), out var ent);
            ent.Get<LifecycleComponent>().State = LifecycleState.Incapacitated;
            Assert.IsFalse(party.TrySetActive(world, new EntityId(2), out _));
        }

        [Test]
        public void PlayerParty_DeadCannotSetActive()
        {
            var world = MakeWorld(new EntityId(2), LifecycleState.Alive);
            world.LocalMap.AddOccupant(new EntityId(1));
            var party = new PlayerPartyRuntime();
            party.TryInitialize(new EntityId(1), out _);
            party.TryAddMember(world, Roster(1, 2), new EntityId(2), out _);
            world.Entities.TryGet(new EntityId(2), out var ent);
            ent.Get<LifecycleComponent>().State = LifecycleState.Dead;
            Assert.IsFalse(party.TrySetActive(world, new EntityId(2), out _));
        }

        [Test]
        public void PlayerParty_RemoveFollowerKeepsCharacter()
        {
            var party = new PlayerPartyRuntime();
            party.TryInitialize(new EntityId(1), out _);
            party.TryAddMember(null, Roster(1, 2), new EntityId(2), out _);
            Assert.IsTrue(party.TryRemoveMember(new EntityId(2), out _));
            Assert.IsFalse(party.IsMember(new EntityId(2)));
            Assert.AreEqual(1, party.Count);
        }

        [Test]
        public void PlayerParty_ActiveCannotRemoveUntilSwitch()
        {
            var party = new PlayerPartyRuntime();
            party.TryInitialize(new EntityId(1), out _);
            party.TryAddMember(null, Roster(1, 2), new EntityId(2), out _);
            Assert.IsFalse(party.TryRemoveMember(new EntityId(1), out var err));
            Assert.IsNotEmpty(err);
        }

        [Test]
        public void PlayerParty_SetActiveSwapsFollowerRole()
        {
            var party = new PlayerPartyRuntime();
            party.TryInitialize(new EntityId(1), out _);
            party.TryAddMember(null, Roster(1, 2), new EntityId(2), out _);
            Assert.IsTrue(party.TrySetActive(null, new EntityId(2), out _));
            Assert.IsTrue(party.IsActive(new EntityId(2)));
            Assert.IsTrue(party.IsFollower(new EntityId(1)));
        }

        [Test]
        public void PlayerParty_SameActiveMapLayoutWithoutOccupantRegistry()
        {
            var world = new XianXia.Core.Simulation.SimulationWorld();
            world.LocalMap.ActiveMapLayoutId = "base:map_qinghe_village";
            world.LocalMap.EnsureOverworld("base:map_qinghe_village");

            AddCharacterAtLocation(world, new EntityId(1), "loc_spawn_a");
            AddCharacterAtLocation(world, new EntityId(2), "loc_spawn_b");

            var party = new PlayerPartyRuntime();
            party.TryInitialize(new EntityId(1), out _);
            Assert.IsTrue(
                party.TryAddMember(world, Roster(1, 2), new EntityId(2), out var err),
                err);
        }

        static void AddCharacterAtLocation(
            XianXia.Core.Simulation.SimulationWorld world,
            EntityId id,
            string locationId)
        {
            var entity = new Entity(id, new DefinitionId("test", "char"), EntityTag.Character, "test");
            entity.AddComponent(new LifecycleComponent(LifecycleState.Alive));
            entity.AddComponent(new ActionStateComponent());
            var loc = new EntityLocationComponent { LocationId = locationId };
            entity.AddComponent(loc);
            world.Entities.AddExisting(entity);

            world.WorldRegion.Register(new WorldLocationState
            {
                Id = locationId,
                LocalMapId = string.Empty
            });
        }

        static EntityId[] Roster(params ulong[] ids)
        {
            var arr = new EntityId[ids.Length];
            for (var i = 0; i < ids.Length; i++)
                arr[i] = new EntityId(ids[i]);
            return arr;
        }

        static XianXia.Core.Simulation.SimulationWorld MakeWorld(EntityId id, LifecycleState state)
        {
            var world = new XianXia.Core.Simulation.SimulationWorld();
            var entity = new Entity(id, new DefinitionId("test", "char"), EntityTag.Character, "test");
            entity.AddComponent(new LifecycleComponent(state));
            entity.AddComponent(new ActionStateComponent());
            world.Entities.AddExisting(entity);
            world.LocalMap.AddOccupant(id);
            return world;
        }
    }
}
