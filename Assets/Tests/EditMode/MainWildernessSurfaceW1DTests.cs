using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Actions;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Npc;
using XianXia.Core.Orders;
using XianXia.Core.World.Hex;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Surface;
using XianXia.Data.Content;
using XianXia.Unity.Host;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Data.Serialization;

namespace XianXia.Tests.EditMode
{
    public sealed class MainWildernessSurfaceW1DTests
    {
        const string MainSurfaceId = "base:surface_main_wilderness_v1";
        const string TravelWorldId = "base:hex_world_travel_mvp_30x15";

        /// <summary>headless harness 注入的内容根（Unity Test Runner 下保持 null，行为完全不变）。</summary>
        public static string HeadlessContentRoot;

        /// <summary>
        /// BaseGame 内容根。Unity 调用（Application.dataPath）隔离在独立方法里 —— 一个方法只要
        /// **包含** ECall，在 Unity 之外 JIT 就会失败（哪怕分支不执行）。
        /// </summary>
        static string BaseGamePath =>
            !string.IsNullOrEmpty(HeadlessContentRoot) ? HeadlessContentRoot : UnityDataPathBaseGame();

        static string UnityDataPathBaseGame() =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void MainSurface_CoversEveryPassableOrdinaryWildernessHexCenter()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            var registry = loaded.Value.Registry;
            Assert.IsTrue(registry.TryGetOutdoorSurface(DefinitionId.Parse(MainSurfaceId).Value, out var surface));
            Assert.IsTrue(registry.TryGetHexWorldContent(DefinitionId.Parse(TravelWorldId).Value, out var world));

            var siteHexes = new HashSet<HexCoord>();
            foreach (var site in world.Sites)
                foreach (var hex in site.Footprint)
                    siteHexes.Add(new HexCoord(hex.Q, hex.R));
            var covered = 0;
            foreach (var cell in world.Cells)
            {
                var hex = new HexCoord(cell.Q, cell.R);
                if (cell.Passable != true || string.Equals(cell.Terrain, "Water", System.StringComparison.OrdinalIgnoreCase) || siteHexes.Contains(hex))
                    continue;
                HexMath.ToWorldPosition(hex, world.HexSize, out var x, out var y);
                Assert.IsTrue(OutdoorSurfaceCoverageResolver.ContainsWorldPosition(surface, x, y), hex.ToString());
                covered++;
            }
            Assert.Greater(covered, 0);
        }

        [Test]
        public void NormalResolver_ChoosesMainSurfaceAndExcludesAcceptanceOnlyOverlap()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            HexMath.ToWorldPosition(new HexCoord(1, 1), 1f, out var x, out var y);
            Assert.IsTrue(OutdoorSurfaceCoverageResolver.TryResolveAtWorldPosition(
                loaded.Value.Registry, x, y, out var resolved));
            Assert.AreEqual(MainSurfaceId, resolved.SurfaceId);
            Assert.IsFalse(resolved.AcceptanceOnly);
        }

        [Test]
        public void AllSevenOutdoorSites_HaveCheckedInRegionsPlacementsAndValidChunks()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            var registry = loaded.Value.Registry;
            Assert.IsTrue(registry.TryGetOutdoorSurface(DefinitionId.Parse(MainSurfaceId).Value, out var surface));
            Assert.AreEqual(7, surface.SiteRegions.Count);
            Assert.GreaterOrEqual(surface.SitePlacements.Count, 7);
            var ids = new HashSet<string>();
            foreach (var placement in surface.SitePlacements)
            {
                Assert.IsTrue(ids.Add(placement.StableId), placement.StableId);
                Assert.IsTrue(surface.Chunks.Exists(c => c.Coord == new XianXia.Core.World.Surface.SurfaceChunkCoord(
                    placement.ChunkX, placement.ChunkY)), placement.StableId);
            }
            foreach (var region in surface.SiteRegions)
            {
                Assert.IsTrue(surface.SitePlacements.Exists(p => p.SiteId == region.SiteId), region.SiteId);
                Assert.IsTrue(surface.SitePlaces.Exists(p => p.SiteId == region.SiteId), region.SiteId);
            }
            Assert.AreEqual(405, CountRenderedObjects(surface.SitePlacements));
        }

        [TestCase("road", 1, 1, 1)]
        [TestCase("wall", 6, 1, 6)]
        [TestCase("herbField", 12, 12, 144)]
        [TestCase("controlCore", 8, 8, 1)]
        [TestCase("zoneHousing", 12, 12, 1)]
        public void OutdoorPlacement_RenderCountUsesAuthoredCellsAndStampMode(
            string kind, int cellsW, int cellsH, int expected)
        {
            var placement = new OutdoorSurfacePlacementDefinition
            { Kind = kind, SourceCellsW = cellsW, SourceCellsH = cellsH };
            Assert.IsTrue(HostDemoTileMap.TryEstimateOutdoorRenderedObjectCount(placement, out var actual));
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void OutdoorStatefulObjects_AreIncludedInSnapshotJson()
        {
            var world = new SimulationWorld();
            world.OutdoorStatefulObjects.SetDestructible("base:wall:0:0", 37, false);
            world.OutdoorStatefulObjects.SetFarmPlot("base:field:11:7", "base:crop_herb", 2, .75f);
            var serializer = new JsonSnapshotSerializer();
            var snapshot = new SnapshotService(serializer).Capture(world, new SimulationLoop(world));
            var json = serializer.Serialize(snapshot);
            Assert.IsTrue(json.IsSuccess, json.IsFailure ? json.Error.ToString() : string.Empty);
            var decoded = serializer.Deserialize(json.Value);
            Assert.IsTrue(decoded.IsSuccess, decoded.IsFailure ? decoded.Error.ToString() : string.Empty);
            Assert.AreEqual("base:wall:0:0", decoded.Value.OutdoorDestructibles[0].StableId);
            Assert.AreEqual(37, decoded.Value.OutdoorDestructibles[0].CurrentHp);
            Assert.AreEqual("base:field:11:7", decoded.Value.OutdoorFarmPlots[0].StableCellId);
            Assert.AreEqual(.75f, decoded.Value.OutdoorFarmPlots[0].Growth, .0001f);

            var restored = new SnapshotService(serializer).RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : string.Empty);
            Assert.IsTrue(restored.Value.world.OutdoorStatefulObjects.TryGetDestructible(
                "base:wall:0:0", out var restoredWall));
            Assert.AreEqual(37, restoredWall.Hp);
            Assert.IsFalse(restoredWall.Destroyed);
            Assert.IsTrue(restored.Value.world.OutdoorStatefulObjects.TryGetFarmPlot(
                "base:field:11:7", out var restoredFarm));
            Assert.AreEqual("base:crop_herb", restoredFarm.CropId);
            Assert.AreEqual(2, restoredFarm.CropStage);
            Assert.AreEqual(.75f, restoredFarm.Growth, .0001f);
        }

        [Test]
        public void ContinuousMaterializationDiff_KeepsExistingPositionAndInitializesOnlyAdd()
        {
            var board = new ContinuousOutdoorMaterializationBoard();
            var existing = new EntityId(11);
            var added = new EntityId(12);
            board.Materialize(existing);
            var existingPosition = 42f;
            var addedPosition = 0f;
            var addCalls = 0;

            board.ReconcileEntities(new[] { existing, added }, id =>
            {
                addCalls++;
                if (id == existing) existingPosition = 1f;
                if (id == added) addedPosition = 7f;
            }, null);

            Assert.AreEqual(42f, existingPosition, "Keep must not reapply the authored anchor.");
            Assert.AreEqual(7f, addedPosition, "Add must receive its initial anchor once.");
            Assert.AreEqual(1, addCalls);
        }

        [Test]
        public void ContinuousMoveAction_RetriesThenFailsWithoutFakeArrivalOrPermanentActiveAction()
        {
            var world = new SimulationWorld();
            var npc = world.Entities.CreateNpc(new DefinitionId("base", "npc_continuous_move"), "行者").Value;
            npc.AddComponent(new EntityLocationComponent { LocationId = "base:loc_home" });
            world.ContinuousOutdoorMaterialization.RegisterPlace("base:site_test", new WorldLocationState
            {
                Id = "base:loc_work", Name = "工区", PresentationX = 14f, PresentationZ = 9f
            });
            world.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "base:wa_work", LocationId = "base:loc_work", AllowedActivities = { "Labor" }
            });
            var move = new MoveAction(
                new ActionId(1), npc.Id, new OrderId(1), 2UL, "base:wa_work");

            Assert.IsTrue(move.CanStart(world).IsSuccess);
            Assert.IsTrue(move.Start(world).IsSuccess);
            Assert.IsTrue(move.Advance(world).IsSuccess);
            Assert.AreEqual(ActionStatus.Running, move.Status);
            Assert.AreEqual("base:loc_home", npc.Get<EntityLocationComponent>().LocationId);
            npc.Get<MovementIntentComponent>().MarkRetryablePathUnavailable();
            Assert.IsTrue(npc.Get<MovementIntentComponent>().Active);
            var exhausted = move.Advance(world);
            Assert.IsTrue(exhausted.IsFailure);
            Assert.AreEqual(ActionStatus.Failed, move.Status);
            Assert.IsFalse(npc.Get<MovementIntentComponent>().Active);
            Assert.AreEqual(MovementHostPathState.PermanentFailure,
                npc.Get<MovementIntentComponent>().HostPathState);
            Assert.AreEqual("base:loc_home", npc.Get<EntityLocationComponent>().LocationId);

            var arrived = new MoveAction(
                new ActionId(2), npc.Id, new OrderId(2), 2UL, "base:wa_work");
            Assert.IsTrue(arrived.Start(world).IsSuccess);
            npc.Get<MovementIntentComponent>().HostArrived = true;
            Assert.IsTrue(arrived.Advance(world).IsSuccess);
            Assert.AreEqual(ActionStatus.Completed, arrived.Status);
            Assert.AreEqual("base:loc_work", npc.Get<EntityLocationComponent>().LocationId);
        }

        [Test]
        public void NonMembershipWorldTick_DoesNotRefreshContinuousScope()
        {
            var world = new SimulationWorld();
            var board = world.ContinuousOutdoorMaterialization;
            board.ClearPlaces();
            board.ReconcileEntities(System.Array.Empty<EntityId>(), null, null);
            var placeRevision = board.PlaceRevision;
            var entityRevision = board.EntityReconcileRevision;

            Assert.IsTrue(new SimulationLoop(world).TickOnce().IsSuccess);

            Assert.AreEqual(placeRevision, board.PlaceRevision);
            Assert.AreEqual(entityRevision, board.EntityReconcileRevision);
        }

        [Test]
        public void ChunkScopeLeaveAndReload_UsesCommittedPreciseAnchor()
        {
            var world = new SimulationWorld();
            var id = world.Entities.CreateNpc(new DefinitionId("base", "npc_scope_anchor"), "行者").Value.Id;
            var board = world.ContinuousOutdoorMaterialization;
            var committed = new WorldVec2(27.5f, 19.25f);
            world.WorldPresence.SetAtSiteWithAnchor(id, "base:site_test", committed);
            board.Materialize(id);

            board.ReconcileEntities(System.Array.Empty<EntityId>(), null, null);
            Assert.IsFalse(board.IsMaterialized(id));
            Assert.IsTrue(world.WorldPresence.TryGet(id, out var stored));
            Assert.AreEqual(committed.X, stored.WorldPosX, .0001f);
            Assert.AreEqual(committed.Y, stored.WorldPosY, .0001f);

            WorldVec2 rematerialized = default;
            board.ReconcileEntities(new[] { id }, added =>
            {
                Assert.IsTrue(world.WorldPresence.TryGet(added, out var presence));
                rematerialized = presence.ContinuousWorldPosition;
            }, null);
            Assert.AreEqual(committed.X, rematerialized.X, .0001f);
            Assert.AreEqual(committed.Y, rematerialized.Y, .0001f);
        }

        [Test]
        public void BakedContinuousPlaces_AreSelfContainedAfterLegacyWorldRegionClear()
        {
            var loaded = new ContentPackageLoader().Load(new[] { Path.GetFullPath("Content/BaseGame") });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            Assert.IsTrue(loaded.Value.Registry.TryGetOutdoorSurface(
                DefinitionId.Parse(MainSurfaceId).Value, out var surface));
            var labor = surface.SitePlaces.Find(p => p.LocationId == "base:loc_ref_labor_yard");
            Assert.IsNotNull(labor);
            Assert.AreEqual("Settlement", labor.Kind);
            CollectionAssert.Contains(labor.AllowedActivities, "Labor");
            CollectionAssert.Contains(labor.Tags, "farm");
            Assert.AreEqual("base:resource_grain", labor.ResourceOnExploreId);
            var home = surface.SitePlaces.Find(p => p.LocationId == "base:loc_ref_houses");
            Assert.IsNotNull(home);
            CollectionAssert.Contains(home.AllowedActivities, "Rest");
            CollectionAssert.Contains(home.AllowedActivities, "Eat");
            Assert.IsFalse(string.IsNullOrEmpty(home.ResidentNpcDefinitionId));

            var mapper = new OutdoorSurfaceCoordinateMapper(
                surface.ChunkWidth, surface.ChunkHeight, surface.CellSize,
                presentationUnitsPerWorldUnit: 1f / surface.CellSize,
                originWorldX: surface.OriginWorldX, originWorldY: surface.OriginWorldY);
            var world = new SimulationWorld();
            world.ContinuousOutdoorMaterialization.RegisterPlace(
                labor.SiteId, ContinuousOutdoorSurfaceRuntime.CreateMaterializedPlace(labor, mapper));
            Assert.IsTrue(WorldLocationQuery.TryGet(world, labor.LocationId, out var runtimeLabor));
            Assert.AreEqual(LocationKind.Settlement, runtimeLabor.Kind);
            CollectionAssert.Contains(runtimeLabor.AllowedActivities, "Labor");
            Assert.IsTrue(HostZoneQuery.TryGetLocationCenter(world, labor.LocationId, out var center));
            mapper.WorldToPresentation(labor.WorldX, labor.WorldY, out var expectedX, out var expectedY);
            Assert.AreEqual(expectedX, center.x, .0001f);
            Assert.AreEqual(expectedY, center.y, .0001f);
        }

        [Test]
        public void ContinuousArrivalAnchor_PreservesAtSiteMembership()
        {
            var world = new SimulationWorld();
            var id = world.Entities.CreateNpc(
                new DefinitionId("base", "npc_anchor_roundtrip"), "行者").Value.Id;
            world.WorldPresence.SetAtSiteWithAnchor(
                id, "base:site_test", new WorldVec2(8.5f, 3.25f));
            Assert.IsTrue(world.WorldPresence.TryGet(id, out var presence));
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, presence.Mode);
            Assert.AreEqual("base:site_test", presence.SiteId);
            Assert.IsTrue(presence.HasContinuousWorldPosition);
            Assert.AreEqual(8.5f, presence.WorldPosX, .0001f);
            Assert.AreEqual(3.25f, presence.WorldPosY, .0001f);

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, new SimulationLoop(world));
            Assert.IsTrue(json.IsSuccess, json.IsFailure ? json.Error.ToString() : string.Empty);
            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : string.Empty);
            Assert.IsTrue(restored.Value.world.WorldPresence.TryGet(id, out var restoredPresence));
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, restoredPresence.Mode);
            Assert.AreEqual("base:site_test", restoredPresence.SiteId);
            Assert.IsTrue(restoredPresence.HasContinuousWorldPosition);
            Assert.AreEqual(8.5f, restoredPresence.WorldPosX, .0001f);
            Assert.AreEqual(3.25f, restoredPresence.WorldPosY, .0001f);
        }

        [Test]
        public void ScheduleMover_MarksArrivalOnlyInsideArrivalRadius()
        {
            var intent = new MovementIntentComponent();
            intent.Begin("base:loc_work", "base:wa_work");
            Assert.IsFalse(HostNpcScheduleMover.TryMarkArrivedWithinRadius(
                intent, new UnityEngine.Vector3(0f, 0f), new UnityEngine.Vector3(10f, 0f), 2.5f));
            Assert.IsFalse(intent.HostArrived, "A failed/unavailable path must not fabricate arrival.");
            Assert.IsTrue(HostNpcScheduleMover.TryMarkArrivedWithinRadius(
                intent, new UnityEngine.Vector3(9f, 0f), new UnityEngine.Vector3(10f, 0f), 2.5f));
            Assert.IsTrue(intent.HostArrived);
        }

        static int CountRenderedObjects(IReadOnlyList<OutdoorSurfacePlacementDefinition> placements)
        {
            var count = 0;
            for (var i = 0; i < placements.Count; i++)
            {
                Assert.IsTrue(HostDemoTileMap.TryEstimateOutdoorRenderedObjectCount(placements[i], out var n),
                    placements[i].StableId);
                count += n;
            }
            return count;
        }
    }
}
