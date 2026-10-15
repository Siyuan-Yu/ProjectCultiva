using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using XianXia.Core.Construction;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Persistence;
using XianXia.Core.World;
using XianXia.Core.World.Surface;
using XianXia.Data.Bootstrap;
using XianXia.Data.Serialization;

namespace XianXia.Tests.EditMode
{
    public sealed class WorldExpand04Tests
    {
        public static string HeadlessContentRoot;
        static string ContentRoot => string.IsNullOrEmpty(HeadlessContentRoot) ? UnityContentRoot() : HeadlessContentRoot;
        static string UnityContentRoot() => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        static PlayableDayBootstrapResult Start()
        {
            var result = new PlayableDayBootstrap().Start(ContentRoot, new PlayableDayOptions {
                OpeningScenarioId = "base:scenario_ch01_reference", CharacterRosterId = "base:roster_level_tester"
            });
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : "");
            return result.Value;
        }

        [Test]
        public void LevelTesterOpening_UsesExpandedCoverageAndOriginalRoomBedIdentities()
        {
            var session = Start();
            Assert.IsTrue(session.World.SurfaceGround.TryGet("base:surface_main_wilderness_v1", out var ground));
            Assert.AreEqual(3800, ground.Width); Assert.AreEqual(1700, ground.Height);
            var assets = session.World.OutdoorConstructedAssets.Assets.Values.ToList();
            Assert.AreEqual(22, assets.Count(RoomBedGeometry.IsRoom));
            var beds = assets.Where(RoomBedGeometry.IsBed).ToList();
            Assert.AreEqual(27, beds.Count);
            var assigned = beds.Where(x => !x.AssignedCharacterId.IsNone).ToList();
            Assert.AreEqual(20, assigned.Count);
            Assert.AreEqual(20, assigned.Select(x => x.AssignedCharacterId).Distinct().Count());
            foreach (var bed in beds)
                Assert.IsTrue(RoomBedGeometry.TryResolveAccessPoint(session.World.OutdoorConstructedAssets, bed, out _), bed.StableAssetId);
        }

        [Test]
        public void ExpandedNewGame_SaveLoadPreservesFurnitureFieldsAndRehydratesSiteState()
        {
            var session = Start(); var world = session.World;
            var party = new PlayerPartyRuntime(); party.BindWorld(world);
            Assert.IsTrue(party.TryInitialize(session.CharacterIds[0], out var reason), reason);
            var events = new ContentEventService();
            for (var i = 0; world.ContentEvents.HasActive && i < 30; i++)
            {
                var step = ContentEventService.ActiveStep(world);
                var resolved = events.ResolveChoice(world, world.ContentEvents.ActiveActorId, step.Choices.FirstOrDefault()?.Id ?? "");
                Assert.IsTrue(resolved.IsSuccess, resolved.IsFailure ? resolved.Error.ToString() : "");
            }
            var serializer = new JsonSnapshotSerializer();
            var service = new SnapshotService(serializer);
            var captured = service.CaptureJson(world, session.Loop, party);
            Assert.IsTrue(captured.IsSuccess, captured.IsFailure ? captured.Error.ToString() : "");
            var parsed = serializer.Deserialize(captured.Value);
            Assert.IsTrue(parsed.IsSuccess);
            var restored = service.Restore(parsed.Value, world.EnabledPackageVersion,
                (candidate, snapshot) => StrategicContentBootstrap.PrepareSnapshotSiteIdentities(candidate, session.Registry, snapshot));
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : "");
            var candidateWorld = restored.Value.world;
            foreach (var before in world.OutdoorConstructedAssets.Assets.Values)
            {
                Assert.IsTrue(candidateWorld.OutdoorConstructedAssets.Assets.TryGetValue(before.StableAssetId, out var after));
                Assert.AreEqual(before.WorldX, after.WorldX); Assert.AreEqual(before.WorldY, after.WorldY);
                Assert.AreEqual(before.ParentRoomId, after.ParentRoomId);
                Assert.AreEqual(before.RoomUsage, after.RoomUsage);
                Assert.AreEqual(before.QuarterTurns, after.QuarterTurns);
                Assert.AreEqual(before.StructureDurability, after.StructureDurability);
                Assert.AreEqual(before.InitialAssigneeSpawnKey, after.InitialAssigneeSpawnKey);
                Assert.AreEqual(before.AssignedCharacterId, after.AssignedCharacterId);
                Assert.AreEqual(before.ReservedCharacterId, after.ReservedCharacterId);
                Assert.AreEqual(before.OccupantCharacterId, after.OccupantCharacterId);
            }
            var shell = RuntimeContentShellBootstrap.Rehydrate(candidateWorld, session.Registry);
            Assert.IsTrue(shell.IsSuccess, shell.IsFailure ? shell.Error.ToString() : "");
            session.Registry.TryGetOpeningScenario(DefinitionId.Parse("base:scenario_ch01_reference").Value, out var scenario);
            Assert.IsTrue(StrategicContentBootstrap.ApplySurfaceSites(candidateWorld, session.Registry, scenario).IsSuccess);
            Assert.IsTrue(ContentRuntimeBootstrap.RebindPresetWorldSiteCoreMetadata(candidateWorld, session.Registry).IsSuccess);
            var political = StrategicSnapshotHelper.RestorePoliticalState(candidateWorld, parsed.Value.Strategic);
            Assert.IsTrue(political.IsSuccess, political.IsFailure ? political.Error.ToString() : "");
            Assert.IsTrue(WorldSiteStorageRoomBootstrap.Rehydrate(candidateWorld, session.Registry).IsSuccess);
            // Version refusal happens before the identity callback can mutate a candidate.
            parsed.Value.EnabledPackageVersion = "0.0.3-vs01";
            var old = service.Restore(parsed.Value, world.EnabledPackageVersion);
            Assert.IsTrue(old.IsFailure);
            StringAssert.Contains("WORLD-EXPAND-04", old.Error.Message);
        }

        [Test]
        public void ClearExpandedSurfaceRoute_DoesNotAllocateFullWorldAStarBuffers()
        {
            var cells = new SurfaceGroundCellKind[3800 * 1700];
            var ground = new SurfaceGroundNavigation("test:expanded", "", "", 0, 0, 1, 3800, 1700, cells);
            var route = new List<WorldVec2>();
            var before = GC.GetTotalMemory(true);
            var result = ground.TryFindRoute(new WorldVec2(10.5f, 10.5f), new WorldVec2(3790.5f, 1690.5f), route);
            Assert.AreEqual(SurfaceGroundRouteStatus.Found, result);
            Assert.AreEqual(2, route.Count);
            Assert.Less(GC.GetTotalMemory(false) - before, 8L * 1024 * 1024);
        }
    }
}
