using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Surface;

namespace XianXia.Tests
{
    public sealed class PlayerPartyLocalCoPresenceTests
    {
        const string SurfaceId = "surface:test";
        const string InteriorMap = "test:map_cave";
        static readonly EntityId ActiveId = new EntityId(1);
        static readonly EntityId FollowerId = new EntityId(2);
        static readonly EntityId[] Roster = { ActiveId, FollowerId };

        [Test]
        public void ContinuousOutdoorWithoutActiveSeparateSpaceAllowsJoin()
        {
            var world = BuildContinuousWorld();
            var party = new PlayerPartyRuntime();
            party.BindWorld(world);
            Assert.IsTrue(party.TryInitialize(ActiveId, out var initError), initError);

            Assert.IsTrue(party.TryAddMember(world, Roster, FollowerId, out var joinError), joinError);
            Assert.AreEqual(
                PlayerPartyCoPresenceScope.ContinuousOutdoorPresentation,
                PlayerPartyLocalCoPresenceQuery.Evaluate(world, party, FollowerId).Scope);
        }

        [Test]
        public void DifferentSiteContextDoesNotBlockContinuousJoin()
        {
            var world = BuildContinuousWorld();
            world.WorldPresence.SetAtSiteWithAnchor(
                ActiveId, "site:left", new WorldVec2(5.2f, 5.1f), SurfaceId);
            world.WorldPresence.SetAtSiteWithAnchor(
                FollowerId, "site:right", new WorldVec2(5.28f, 5.1f), SurfaceId);

            var party = new PlayerPartyRuntime();
            party.BindWorld(world);
            Assert.IsTrue(party.TryInitialize(ActiveId, out var initError), initError);
            Assert.IsTrue(party.TryAddMember(world, Roster, FollowerId, out var joinError),
                "Site context is not a physical-space boundary: " + joinError);
        }

        [Test]
        public void SeparateSpaceRejectsCharacterOutsideActiveMapWithSpaceNeutralMessage()
        {
            var world = new SimulationWorld();
            world.LocalMap.EstablishSeparateSpace(
                InteriorMap, "test:places", SeparateSpaceKind.Cave,
                "test:entrance", "test:entrance", "test");
            RegisterPlace(world, "loc:cave", InteriorMap);
            RegisterPlace(world, "loc:outside", "test:other_map");
            AddCharacter(world, ActiveId, "loc:cave");
            AddCharacter(world, FollowerId, "loc:outside");
            world.LocalMap.AddOccupant(ActiveId);

            var party = new PlayerPartyRuntime();
            party.BindWorld(world);
            Assert.IsTrue(party.TryInitialize(ActiveId, out var initError), initError);
            Assert.IsFalse(party.ValidateJoin(world, Roster, FollowerId, out var deny));
            Assert.AreEqual(PlayerPartyLocalCoPresenceQuery.DeniedPlayerMessage, deny);
            StringAssert.DoesNotContain("LocalMap", deny);
        }

        [Test]
        public void ContinuousFollowSynchronizesSurfaceAndTravelingMembership()
        {
            var world = BuildContinuousWorld();
            var party = new PlayerPartyRuntime();
            party.BindWorld(world);
            Assert.IsTrue(party.TryInitialize(ActiveId, out var initError), initError);
            Assert.IsTrue(party.TryAddMember(world, Roster, FollowerId, out var joinError), joinError);

            PlayerPartyTransitionMembership.SyncMemberPresenceFromMotion(world, FollowerId);
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);

            Assert.IsTrue(world.PlayerPartyTravel.TravelingMembers.Contains(FollowerId));
            Assert.IsTrue(world.WorldPresence.TryGet(FollowerId, out var presence));
            Assert.AreEqual(PartyWorldPresenceMode.AtWorldPosition, presence.Mode);
            Assert.AreEqual(SurfaceId, presence.PersonalSurfaceId);
            Assert.AreEqual(world.PlayerPartyTravel.WorldPosition.X, presence.WorldPosX, 1e-6);
            Assert.AreEqual(world.PlayerPartyTravel.WorldPosition.Y, presence.WorldPosY, 1e-6);
            Assert.IsFalse(world.LocalMap.ContainsOccupant(FollowerId));
        }

        [Test]
        public void StopFollowKeepsExactSurfacePosition()
        {
            var world = BuildContinuousWorld();
            var party = new PlayerPartyRuntime();
            party.BindWorld(world);
            Assert.IsTrue(party.TryInitialize(ActiveId, out var initError), initError);
            Assert.IsTrue(party.TryAddMember(world, Roster, FollowerId, out var joinError), joinError);
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);

            var precise = new WorldVec2(7.125f, 6.875f);
            Assert.IsTrue(party.TryRemoveMember(FollowerId, out var removeError), removeError);
            PlayerPartyTransitionMembership.SyncIndependentCharacterPresenceFromPosition(
                world, FollowerId, precise, SurfaceId);
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);

            Assert.IsFalse(world.PlayerPartyTravel.TravelingMembers.Contains(FollowerId));
            Assert.IsTrue(world.WorldPresence.TryGet(FollowerId, out var after));
            Assert.AreEqual(PartyWorldPresenceMode.AtWorldPosition, after.Mode);
            Assert.AreEqual(SurfaceId, after.PersonalSurfaceId);
            Assert.AreEqual(precise.X, after.WorldPosX, 1e-6);
            Assert.AreEqual(precise.Y, after.WorldPosY, 1e-6);
        }

        static SimulationWorld BuildContinuousWorld()
        {
            var world = new SimulationWorld();
            var cells = new List<SurfaceGroundCellKind>();
            for (var i = 0; i < 400; i++) cells.Add(SurfaceGroundCellKind.Ground);
            world.SurfaceGround.Register(new SurfaceGroundNavigation(
                SurfaceId, "rev", SurfaceId, 0f, 0f, 1f, 20, 20, cells));
            AddCharacter(world, ActiveId, string.Empty);
            AddCharacter(world, FollowerId, string.Empty);
            world.PlayerPartyTravel.SetAtSurfacePosition(SurfaceId, new WorldVec2(5.2f, 5.1f));
            world.ContinuousOutdoorMaterialization.Materialize(ActiveId);
            world.ContinuousOutdoorMaterialization.Materialize(FollowerId);
            world.WorldPresence.SetAtWorldPosition(
                ActiveId, new WorldVec2(5.2f, 5.1f), SurfaceId);
            world.WorldPresence.SetAtWorldPosition(
                FollowerId, new WorldVec2(5.28f, 5.1f), SurfaceId);
            return world;
        }

        static void RegisterPlace(SimulationWorld world, string locationId, string localMapId)
        {
            world.LocalPlaces.Register(new WorldLocationState
            {
                Id = locationId,
                LocalMapId = localMapId
            });
        }

        static void AddCharacter(SimulationWorld world, EntityId id, string locationId)
        {
            var entity = new Entity(
                id, new DefinitionId("test", "char_" + id.Value), EntityTag.Character, "test");
            entity.AddComponent(new LifecycleComponent(LifecycleState.Alive));
            entity.AddComponent(new ActionStateComponent());
            if (!string.IsNullOrEmpty(locationId))
                entity.AddComponent(new EntityLocationComponent { LocationId = locationId });
            world.Entities.AddExisting(entity);
        }
    }
}
