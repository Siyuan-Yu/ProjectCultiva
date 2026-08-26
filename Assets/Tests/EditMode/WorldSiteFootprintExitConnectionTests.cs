using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;

namespace XianXia.Tests
{
    public sealed class WorldSiteFootprintExitConnectionTests
    {
        const string FactionA = "test:faction_a";
        const string TravelWorldId = "base:hex_world_travel_mvp_30x15";
        const float Depth = SurfaceExitZoneCalculator.DefaultExitTriggerDepth;
        const float FloatTol = 0.08f;

        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        static WildernessLocalWorldProjection.WildernessLocalMapBounds DefaultBounds() =>
            WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(0f, 0f, 1f, 16, 16);

        static SimulationWorld BuildFourHexSiteWorld(out WorldSite site)
        {
            var world = new SimulationWorld();
            world.HexWorld.HexSize = 1f;
            world.HexWorld.FillRectangle(20, 20, HexTerrainType.Plain);
            for (var r = 0; r < 20; r++)
            for (var q = 0; q < 20; q++)
            {
                if (!world.HexWorld.TryGetCell(new HexCoord(q, r), out var cell) || cell == null)
                    continue;
                cell.IsPassable = true;
            }

            var anchor = new HexCoord(5, 5);
            var presence = new HexCoord(6, 5);
            site = new WorldSite
            {
                SiteId = "test:site_four",
                DisplayName = "四格测试",
                AnchorHex = anchor,
                PresenceHex = presence,
                LocalMapId = "test:map_four",
            };
            site.SetFootprint(new[]
            {
                anchor, presence, new HexCoord(5, 6), new HexCoord(6, 6),
            });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);
            return world;
        }

        static SimulationWorld BuildSingleHexSiteWorld(out WorldSite site)
        {
            var world = new SimulationWorld();
            world.HexWorld.HexSize = 1f;
            world.HexWorld.FillRectangle(12, 12, HexTerrainType.Plain);
            var hex = new HexCoord(4, 4);
            site = new WorldSite
            {
                SiteId = "test:site_single",
                DisplayName = "单格",
                AnchorHex = hex,
                PresenceHex = hex,
                LocalMapId = "test:map_single",
            };
            site.SetFootprint(new[] { hex });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);
            return world;
        }

        static EntityId Spawn(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            return created.Value.Id;
        }

        static PlayerPartyRuntime BuildParty(SimulationWorld world, EntityId leader)
        {
            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(leader, out _));
            return party;
        }

        static void SetupAtSite(SimulationWorld world, WorldSite site, PlayerPartyRuntime party)
        {
            world.LocalMap.ActiveMapLayoutId = site.LocalMapId;
            world.PlayerPartyTravel.SetAtWorldSite(
                site.SiteId, site.PresenceHex, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
        }

        static List<SurfaceExitConnection> CollectSiteConnections(SimulationWorld world, WorldSite site)
        {
            SetupAtSite(world, site, BuildParty(world, Spawn(world, "Lead")));
            var list = new List<SurfaceExitConnection>(16);
            SurfaceExitZoneCalculator.CollectConnections(world, DefaultBounds(), Depth, list);
            return list;
        }

        static int CountOutsideFromHexOnly(SimulationWorld world, WorldSite site, HexCoord onlyHex)
        {
            var set = new HashSet<HexCoord>();
            for (var d = 0; d < 6; d++)
            {
                var n = HexMath.Neighbor(onlyHex, d);
                if (site.OccupiesHex(n))
                    continue;
                if (!world.HexWorld.TryGetTile(n, out var tile) || tile == null)
                    continue;
                if (tile.Terrain == HexTerrainType.Water || !tile.IsPassable)
                    continue;
                set.Add(n);
            }

            return set.Count;
        }

        static SimulationWorld LoadTravelMvpWorld()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            Assert.IsTrue(
                loaded.Value.Registry.TryGetHexWorldContent(
                    DefinitionId.Parse(TravelWorldId).Value,
                    out var definition));
            var world = new SimulationWorld();
            Assert.IsTrue(HexWorldContentLoader.Apply(world, definition).IsSuccess);
            return world;
        }

        [Test]
        public void WorldSiteExitResolverUsesEntireFootprint()
        {
            var world = BuildFourHexSiteWorld(out var site);
            var full = WorldSiteFootprintExitConnectionResolver.CountUniqueTraversableOutsideNeighbors(
                world, site);
            var anchorOnly = CountOutsideFromHexOnly(world, site, site.AnchorHex);
            var presenceOnly = CountOutsideFromHexOnly(world, site, site.PresenceHex);
            Assert.Greater(full, anchorOnly);
            Assert.Greater(full, presenceOnly);
        }

        [Test]
        public void WorldSiteExitResolverDoesNotUseAnchorHexOnly()
        {
            var world = BuildFourHexSiteWorld(out var site);
            var connections = CollectSiteConnections(world, site);
            var anchorOnly = CountOutsideFromHexOnly(world, site, site.AnchorHex);
            Assert.Greater(connections.Count, anchorOnly);
        }

        [Test]
        public void WorldSiteExitResolverDoesNotUsePresenceHexOnly()
        {
            var world = BuildFourHexSiteWorld(out var site);
            var connections = CollectSiteConnections(world, site);
            var presenceOnly = CountOutsideFromHexOnly(world, site, site.PresenceHex);
            Assert.Greater(connections.Count, presenceOnly);
        }

        [Test]
        public void InternalFootprintAdjacencyProducesNoExitConnection()
        {
            var world = BuildFourHexSiteWorld(out var site);
            var connections = CollectSiteConnections(world, site);
            foreach (var c in connections)
                Assert.IsFalse(site.OccupiesHex(c.DestinationHex));
        }

        [Test]
        public void UniqueOutsideHexProducesSingleExitConnection()
        {
            var world = BuildFourHexSiteWorld(out var site);
            var connections = CollectSiteConnections(world, site);
            var seen = new HashSet<HexCoord>();
            foreach (var c in connections)
                Assert.IsTrue(seen.Add(c.DestinationHex), "duplicate dest " + c.DestinationHex);
        }

        [Test]
        public void FourHexWorldSiteCanProduceMoreThanSixOutsideConnections()
        {
            var world = BuildFourHexSiteWorld(out var site);
            var count = WorldSiteFootprintExitConnectionResolver.CountUniqueTraversableOutsideNeighbors(
                world, site);
            Assert.Greater(count, 6);
        }

        [Test]
        public void WorldSiteExitConnectionCountEqualsUniqueTraversableOutsideNeighbors()
        {
            var world = BuildFourHexSiteWorld(out var site);
            var expected = WorldSiteFootprintExitConnectionResolver.CountUniqueTraversableOutsideNeighbors(
                world, site);
            var connections = CollectSiteConnections(world, site);
            Assert.AreEqual(expected, connections.Count);
        }

        [Test]
        public void WorldSiteBoundaryContactUsesActualFootprintBoundary()
        {
            var world = BuildFourHexSiteWorld(out var site);
            var hexSize = world.HexWorld.HexSize;
            WorldSiteFootprintExitConnectionResolver.ComputeFootprintWorldCenter(
                site, hexSize, out var cx, out var cy);
            var connections = CollectSiteConnections(world, site);
            Assert.Greater(connections.Count, 0);
            foreach (var c in connections)
            {
                ComputeExpectedBoundaryContact(world, site, c.DestinationHex, hexSize, out var expectedX, out var expectedY);
                Assert.AreEqual(expectedX, c.BoundaryContactWorldX, FloatTol, c.DestinationHex.ToString());
                Assert.AreEqual(expectedY, c.BoundaryContactWorldY, FloatTol, c.DestinationHex.ToString());
                var toCenterX = c.BoundaryContactWorldX - cx;
                var toCenterY = c.BoundaryContactWorldY - cy;
                var dot = toCenterX * c.LocalDirectionX + toCenterY * c.LocalDirectionY;
                Assert.Greater(dot, 0f);
            }
        }

        static void ComputeExpectedBoundaryContact(
            SimulationWorld world,
            WorldSite site,
            HexCoord destination,
            float hexSize,
            out float contactX,
            out float contactY)
        {
            contactX = 0f;
            contactY = 0f;
            var count = 0;
            foreach (var footprintHex in site.EnumerateFootprintHexes())
            {
                for (var d = 0; d < 6; d++)
                {
                    if (HexMath.Neighbor(footprintHex, d) != destination)
                        continue;
                    HexMath.ToWorldPosition(footprintHex, hexSize, out var fx, out var fy);
                    HexMath.ToWorldPosition(destination, hexSize, out var dx, out var dy);
                    contactX += (fx + dx) * 0.5f;
                    contactY += (fy + dy) * 0.5f;
                    count++;
                }
            }

            Assert.Greater(count, 0);
            contactX /= count;
            contactY /= count;
        }

        [Test]
        public void WorldSiteExitProjectionUsesActualWorldDirection()
        {
            var world = BuildFourHexSiteWorld(out var site);
            var hexSize = world.HexWorld.HexSize;
            WorldSiteFootprintExitConnectionResolver.ComputeFootprintWorldCenter(
                site, hexSize, out var fcx, out var fcy);
            var bounds = DefaultBounds();
            var connections = CollectSiteConnections(world, site);
            foreach (var c in connections)
            {
                var wdx = c.BoundaryContactWorldX - fcx;
                var wdy = c.BoundaryContactWorldY - fcy;
                LocalMapHexDirectionProjection.HexWorldDeltaToLocalPlane(wdx, wdy, out var ldx, out var ldy);
                Assert.AreEqual(ldx / Math.Sqrt(ldx * ldx + ldy * ldy), c.LocalDirectionX, FloatTol);
                Assert.AreEqual(ldy / Math.Sqrt(ldx * ldx + ldy * ldy), c.LocalDirectionY, FloatTol);
                var toCenterDx = c.ExitCenterLocalX - bounds.CenterX;
                var toCenterDy = c.ExitCenterLocalY - bounds.CenterY;
                Assert.Greater(
                    toCenterDx * c.LocalDirectionX + toCenterDy * c.LocalDirectionY,
                    0f);
            }
        }

        [Test]
        public void WorldSiteExitOrderingMatchesWorldBoundaryOrdering()
        {
            var world = BuildFourHexSiteWorld(out var site);
            var connections = CollectSiteConnections(world, site);
            var bounds = DefaultBounds();
            var groups = new Dictionary<int, List<(float along, HexCoord dest)>>();
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                ClassifyEdge(c, bounds, out var edge, out var along);
                if (!groups.TryGetValue(edge, out var list))
                {
                    list = new List<(float, HexCoord)>();
                    groups[edge] = list;
                }

                list.Add((along, c.DestinationHex));
            }

            foreach (var kv in groups)
            {
                if (kv.Value.Count < 2)
                    continue;
                kv.Value.Sort((a, b) => a.along.CompareTo(b.along));
                for (var i = 1; i < kv.Value.Count; i++)
                    Assert.Greater(kv.Value[i].along, kv.Value[i - 1].along - 0.001f);
            }
        }

        [Test]
        public void WorldSiteExitZonesDoNotOverlapAfterResolution()
        {
            var world = BuildFourHexSiteWorld(out var site);
            var connections = CollectSiteConnections(world, site);
            for (var i = 0; i < connections.Count; i++)
            for (var j = i + 1; j < connections.Count; j++)
                Assert.IsFalse(RectsOverlap(connections[i].SlotRect, connections[j].SlotRect));
        }

        [Test]
        public void WorldSiteRendererAndDetectionUseSameResolvedGeometry()
        {
            var world = BuildFourHexSiteWorld(out var site);
            SetupAtSite(world, site, BuildParty(world, Spawn(world, "Lead")));
            var bounds = DefaultBounds();
            var geometries = new List<SurfaceExitZoneGeometry>(16);
            SurfaceExitZoneCalculator.BuildCanonicalGeometries(world, bounds, Depth, geometries);
            for (var i = 0; i < geometries.Count; i++)
            {
                var g = geometries[i];
                var c = g.Connection;
                Assert.AreEqual(c.SlotRect.MinX, g.Connection.SlotRect.MinX, FloatTol);
                Assert.IsTrue(SurfaceExitZoneCalculator.PointBelongsToConnection(
                    c.ExitCenterLocalX, c.ExitCenterLocalY, c, Depth));
            }
        }

        [Test]
        public void SingleHexWorldSiteUsesSameFootprintBoundaryPipeline()
        {
            var world = BuildSingleHexSiteWorld(out var site);
            var expected = WorldSiteFootprintExitConnectionResolver.CountUniqueTraversableOutsideNeighbors(
                world, site);
            var connections = CollectSiteConnections(world, site);
            Assert.AreEqual(expected, connections.Count);
            Assert.LessOrEqual(connections.Count, 6);
        }

        [Test]
        public void WorldSiteExitGeometryDoesNotDependOnCharacterPosition()
        {
            var world = BuildFourHexSiteWorld(out var site);
            var party = BuildParty(world, Spawn(world, "Lead"));
            SetupAtSite(world, site, party);
            var bounds = DefaultBounds();
            var first = new List<SurfaceExitConnection>(16);
            SurfaceExitZoneCalculator.CollectConnections(world, bounds, Depth, first);

            world.PlayerPartyTravel.SetAtWorldSite(
                site.SiteId, site.PresenceHex, world.HexWorld.HexSize);
            var second = new List<SurfaceExitConnection>(16);
            SurfaceExitZoneCalculator.CollectConnections(world, bounds, Depth, second);

            Assert.AreEqual(first.Count, second.Count);
            for (var i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].DestinationHex, second[i].DestinationHex);
                Assert.AreEqual(first[i].ExitCenterLocalX, second[i].ExitCenterLocalX, FloatTol);
                Assert.AreEqual(first[i].ExitCenterLocalY, second[i].ExitCenterLocalY, FloatTol);
            }
        }

        [Test]
        public void WorldSiteExitGeometrySameAfterReentry()
        {
            var world = BuildFourHexSiteWorld(out var site);
            var party = BuildParty(world, Spawn(world, "Lead"));
            SetupAtSite(world, site, party);
            var bounds = DefaultBounds();
            var before = new List<SurfaceExitConnection>(16);
            SurfaceExitZoneCalculator.CollectConnections(world, bounds, Depth, before);

            var exit = before[0];
            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryExitWorldSiteByConnection(
                world, party, exit).IsSuccess);
            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, site).IsSuccess);

            var after = new List<SurfaceExitConnection>(16);
            SurfaceExitZoneCalculator.CollectConnections(world, bounds, Depth, after);
            Assert.AreEqual(before.Count, after.Count);
            for (var i = 0; i < before.Count; i++)
            {
                Assert.AreEqual(before[i].DestinationHex, after[i].DestinationHex);
                Assert.AreEqual(before[i].SlotRect.MinX, after[i].SlotRect.MinX, FloatTol);
            }
        }

        [Test]
        public void WorldSiteExitZoneTransitionsToItsBoundDestinationHex()
        {
            var world = BuildFourHexSiteWorld(out var site);
            var party = BuildParty(world, Spawn(world, "Lead"));
            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, site).IsSuccess);

            var connections = new List<SurfaceExitConnection>(16);
            SurfaceExitZoneCalculator.CollectConnections(world, DefaultBounds(), Depth, connections);
            Assert.Greater(connections.Count, 0);
            var target = connections[0];
            Assert.IsTrue(PlayerPartyWildernessTransitionService.TryExitWorldSiteByConnection(
                world, party, target).IsSuccess);
            Assert.AreEqual(target.DestinationHex, world.PlayerPartyTravel.CurrentHex);
        }

        [Test]
        public void OrdinaryHexExitTestsRemainPASS()
        {
            var world = new SimulationWorld();
            world.HexWorld.HexSize = 1f;
            world.HexWorld.FillRectangle(8, 8, HexTerrainType.Plain);
            var hex = new HexCoord(3, 3);
            world.PlayerPartyTravel.SetAtWorldPosition(new WorldVec2(0f, 0f), hex);
            world.LocalMap.ActiveMapLayoutId = "w";

            var count = 0;
            for (var d = 0; d < 6; d++)
            {
                var n = HexMath.Neighbor(hex, d);
                if (world.HexWorld.TryGetTile(n, out var tile) && tile != null && tile.IsPassable)
                    count++;
            }

            var connections = new List<SurfaceExitConnection>(6);
            SurfaceExitZoneCalculator.CollectConnections(world, DefaultBounds(), Depth, connections);
            Assert.AreEqual(count, connections.Count);
        }

        [Test]
        public void TravelMvp_MultiHexSites_ReportOutsideConnectionCounts()
        {
            var world = LoadTravelMvpWorld();
            AssertConnectionCount(world, "base:site_huangcun");
            AssertConnectionCount(world, "base:site_chengzhen");
            AssertConnectionCount(world, "base:site_zhuangyuan");
        }

        static void AssertConnectionCount(SimulationWorld world, string siteId)
        {
            Assert.IsTrue(world.Strategic.Sites.TryGet(siteId, out var site), siteId);
            var expected = WorldSiteFootprintExitConnectionResolver.CountUniqueTraversableOutsideNeighbors(
                world, site);
            world.LocalMap.ActiveMapLayoutId = site.LocalMapId;
            world.PlayerPartyTravel.SetAtWorldSite(
                site.SiteId, site.PresenceHex, world.HexWorld.HexSize);
            var list = new List<SurfaceExitConnection>(16);
            SurfaceExitZoneCalculator.CollectConnections(world, DefaultBounds(), Depth, list);
            Assert.AreEqual(expected, list.Count, siteId);
            Assert.Greater(expected, 0, siteId);
        }

        static void ClassifyEdge(
            SurfaceExitConnection c,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            out int edge,
            out float along)
        {
            if (Math.Abs(c.LocalDirectionX) >= Math.Abs(c.LocalDirectionY))
            {
                edge = c.LocalDirectionX > 0f ? 0 : 1;
                along = c.ExitCenterLocalY;
            }
            else
            {
                edge = c.LocalDirectionY > 0f ? 2 : 3;
                along = c.ExitCenterLocalX;
            }
        }

        static bool RectsOverlap(SurfaceExitCoverageRect a, SurfaceExitCoverageRect b)
        {
            return a.MinX < b.MaxX - 0.0001f &&
                   a.MaxX > b.MinX + 0.0001f &&
                   a.MinY < b.MaxY - 0.0001f &&
                   a.MaxY > b.MinY + 0.0001f;
        }
    }
}
