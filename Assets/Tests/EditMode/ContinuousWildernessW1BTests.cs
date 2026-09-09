using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class ContinuousWildernessW1BTests
    {
        [Test]
        public void PresentationSurfaceLocalRoundtrip_WhenLoadedSetActive()
        {
            var offset = new Vector2(50f, 0f);
            Assert.IsTrue(ContinuousWildernessSurfaceCoordinates.PresentationToSurfaceLocal(
                true, offset, 40f, -10f, out var localX, out var localY));
            Assert.AreEqual(-10f, localX, 0.0001f);
            Assert.AreEqual(-10f, localY, 0.0001f);
            Assert.IsTrue(ContinuousWildernessSurfaceCoordinates.SurfaceLocalToPresentation(
                true, offset, localX, localY, out var presX, out var presY));
            Assert.AreEqual(40f, presX, 0.0001f);
            Assert.AreEqual(-10f, presY, 0.0001f);
        }

        [Test]
        public void PresentationSurfaceLocal_IsIdentity_WhenLoadedSetInactive()
        {
            Assert.IsTrue(ContinuousWildernessSurfaceCoordinates.PresentationToSurfaceLocal(
                false, Vector2.zero, 12f, 8f, out var localX, out var localY));
            Assert.AreEqual(12f, localX, 0.0001f);
            Assert.AreEqual(8f, localY, 0.0001f);
        }

        [Test]
        public void ManualSeamlessCommit_DoesNotClearOccupants()
        {
            var world = BuildTinyWildernessWorld(out var party);
            world.LocalMap.ActiveMapLayoutId = "base:map_wilderness_plain_fallback";
            world.LocalMap.OverworldMapLayoutId = world.LocalMap.ActiveMapLayoutId;
            var occupant = Spawn(world, "occupant");
            world.LocalMap.AddOccupant(occupant);

            var connection = BuildHorizontalConnection(new HexCoord(0, 1), new HexCoord(1, 1));
            var result = PlayerPartyWildernessTransitionService.TryCommitSeamlessWildernessCrossing(
                world, party, connection);

            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : string.Empty);
            Assert.AreEqual(new HexCoord(1, 1), world.PlayerPartyTravel.CurrentHex);
            Assert.AreEqual("base:map_wilderness_plain_fallback", world.LocalMap.ActiveMapLayoutId);
            Assert.IsTrue(world.LocalMap.ContainsOccupant(occupant));
        }

        [Test]
        public void AutoTravelSeamlessCommit_PreservesHexPathAndExecutionMode()
        {
            var world = BuildTinyWildernessWorld(out var party);
            var motion = world.PlayerPartyTravel;
            motion.BeginAutoTravel(
                new List<HexCoord> { new HexCoord(0, 1), new HexCoord(1, 1), new HexCoord(2, 1) },
                new HexCoord(2, 1),
                string.Empty,
                HexTravelMode.Ground,
                1f);
            motion.SetExecutionMode(PlayerPartyTravelExecutionMode.LocalVisible);

            var connection = BuildHorizontalConnection(new HexCoord(0, 1), new HexCoord(1, 1));
            var result =
                PlayerPartyWildernessTransitionService.TryCommitSeamlessWildernessCrossingPreservingLocalVisibleAutoTravel(
                    world, party, connection);

            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : string.Empty);
            Assert.AreEqual(PlayerPartyMovementKind.AutoTravel, motion.MovementKind);
            Assert.AreEqual(PlayerPartyTravelExecutionMode.LocalVisible, motion.ExecutionMode);
            Assert.AreEqual(3, motion.HexPathCount);
            Assert.AreEqual(new HexCoord(2, 1), motion.DestinationHex);
            Assert.AreEqual(1, motion.SegmentIndex);
        }

        [Test]
        public void LoadedSet_Deactivate_ClearsActiveState()
        {
            var go = new GameObject("w1b-loaded-set");
            try
            {
                var loadedSet = go.AddComponent<ContinuousWildernessLoadedSet>();
                loadedSet.DeactivateToLegacy();
                Assert.IsFalse(loadedSet.IsActive);
                loadedSet.DeactivateToLegacy();
                Assert.IsFalse(loadedSet.IsActive);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        static SimulationWorld BuildTinyWildernessWorld(out PlayerPartyRuntime party)
        {
            var world = new SimulationWorld();
            world.HexWorld.MapId = "test:w1b";
            world.HexWorld.HexSize = 1f;
            world.HexWorld.FillRectangle(5, 5, HexTerrainType.Plain);
            for (var r = 0; r < 5; r++)
            for (var q = 0; q < 5; q++)
            {
                if (world.HexWorld.TryGetCell(new HexCoord(q, r), out var cell) && cell != null)
                    cell.IsPassable = true;
            }

            var leader = Spawn(world, "leader");
            party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(leader, out _));
            world.PlayerPartyTravel.SetAtWorldPosition(new WorldVec2(0f, 1f), new HexCoord(0, 1));
            world.PartyWorld.LocalMapId = "base:map_wilderness_plain_fallback";
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtHex;
            return world;
        }

        static EntityId Spawn(SimulationWorld world, string name)
        {
            var ent = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(ent.IsSuccess);
            world.WorldPresence.SetAtHex(ent.Value.Id, new HexCoord(0, 1));
            return ent.Value.Id;
        }

        static SurfaceExitConnection BuildHorizontalConnection(HexCoord source, HexCoord destination)
        {
            var slot = new SurfaceExitCoverageRect(20f, 22f, -2f, 2f);
            return new SurfaceExitConnection(
                source,
                destination,
                0,
                SurfaceExitDestinationKind.WildernessHex,
                string.Empty,
                1f,
                0f,
                21f,
                0f,
                slot,
                1.5f,
                1f);
        }
    }
}
