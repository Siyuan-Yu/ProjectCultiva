using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class SurfaceExitConnectionTests
    {
        const string FactionA = "test:faction_a";
        const float Depth = SurfaceExitZoneCalculator.DefaultExitTriggerDepth;
        const float FloatTol = 0.08f;

        static WildernessLocalWorldProjection.WildernessLocalMapBounds DefaultBounds() =>
            WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(0f, 0f, 1f, 16, 16);

        static SimulationWorld BuildTinyTravelWorld(
            out WorldSite siteA,
            out HexCoord midHex)
        {
            var world = new SimulationWorld();
            world.HexWorld.MapId = "test:tiny_travel_world";
            world.HexWorld.HexSize = 1f;
            world.HexWorld.FillRectangle(20, 12, HexTerrainType.Plain);
            for (var r = 0; r < 12; r++)
            for (var q = 0; q < 20; q++)
            {
                if (!world.HexWorld.TryGetCell(new HexCoord(q, r), out var cell) || cell == null)
                    continue;
                cell.IsPassable = true;
            }

            var aAnchor = new HexCoord(2, 4);
            siteA = new WorldSite
            {
                SiteId = "test:site_huangcun",
                DisplayName = "青石荒村",
                AnchorHex = aAnchor,
                PresenceHex = new HexCoord(3, 4),
                LocalMapId = "base:map_ch01_reference",
            };
            siteA.SetFootprint(new[]
            {
                aAnchor, new HexCoord(3, 4), new HexCoord(2, 5), new HexCoord(3, 5),
            });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, siteA);

            midHex = new HexCoord(6, 4);
            Assert.IsTrue(world.HexWorld.TryGetTile(midHex, out var mid) && mid.IsPassable);
            return world;
        }

        static EntityId Spawn(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            return created.Value.Id;
        }

        static PlayerPartyRuntime BuildParty(SimulationWorld world, WorldSite site, params EntityId[] members)
        {
            world.LocalMap.ActiveMapLayoutId = site.LocalMapId;
            world.PartyWorld.SiteId = site.SiteId;
            world.PartyWorld.LocalMapId = site.LocalMapId;
            for (var i = 0; i < members.Length; i++)
            {
                world.WorldPresence.SetAtSite(members[i], site.SiteId);
                world.LocalMap.AddOccupant(members[i]);
            }

            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(members[0], out _));
            return party;
        }

        static void SetupWildernessAtHex(SimulationWorld world, HexCoord hex, PlayerPartyRuntime party)
        {
            world.PlayerPartyTravel.SnapToHexCenter(hex, world.HexWorld.HexSize);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            world.LocalMap.ActiveMapLayoutId = "w";
            world.LocalMap.OverworldMapLayoutId = "w";
        }

        static int CountTraversableNeighbors(SimulationWorld world, HexCoord hex)
        {
            var count = 0;
            for (var d = 0; d < 6; d++)
            {
                var n = HexMath.Neighbor(hex, d);
                if (!world.HexWorld.TryGetTile(n, out var tile) || tile == null)
                    continue;
                if (tile.Terrain == HexTerrainType.Water || !tile.IsPassable)
                    continue;
                count++;
            }

            return count;
        }

        static void AssertWorldAndLocalDirectionMatch(
            SimulationWorld world,
            HexCoord sourceHex,
            SurfaceExitConnection connection)
        {
            var hexSize = world.HexWorld.HexSize;
            HexMath.ToWorldPosition(sourceHex, hexSize, out var sx, out var sy);
            HexMath.ToWorldPosition(connection.DestinationHex, hexSize, out var dx, out var dy);
            var worldDx = dx - sx;
            var worldDy = dy - sy;
            LocalMapHexDirectionProjection.HexWorldDeltaToLocalPlane(
                worldDx, worldDy, out var ldx, out var ldy);
            var wLen = System.Math.Sqrt(worldDx * worldDx + worldDy * worldDy);
            var lLen = System.Math.Sqrt(ldx * ldx + ldy * ldy);
            Assert.Greater(wLen, 0.001f);
            Assert.AreEqual(worldDx / wLen, connection.LocalDirectionX, FloatTol);
            Assert.AreEqual(worldDy / wLen, connection.LocalDirectionY, FloatTol);
            Assert.AreEqual(ldx / lLen, connection.LocalDirectionX, FloatTol);
            Assert.AreEqual(ldy / lLen, connection.LocalDirectionY, FloatTol);

            var bounds = DefaultBounds();
            var toCenterDx = connection.ExitCenterLocalX - bounds.CenterX;
            var toCenterDy = connection.ExitCenterLocalY - bounds.CenterY;
            Assert.Greater(
                toCenterDx * connection.LocalDirectionX + toCenterDy * connection.LocalDirectionY,
                0f);
        }

        [Test]
        public void OrdinaryHexExitConnectionCountEqualsTraversableNeighborCount()
        {
            var world = BuildTinyTravelWorld(out var siteA, out var mid);
            var party = BuildParty(world, siteA, Spawn(world, "LinQing"));
            SetupWildernessAtHex(world, mid, party);
            var bounds = DefaultBounds();

            var expected = CountTraversableNeighbors(world, mid);
            var connections = new List<SurfaceExitConnection>(6);
            SurfaceExitZoneCalculator.CollectConnections(world, bounds, Depth, connections);
            Assert.AreEqual(expected, connections.Count);
        }

        [Test]
        public void MissingNeighborProducesNoExitConnection()
        {
            var world = new SimulationWorld();
            world.HexWorld.HexSize = 1f;
            world.HexWorld.FillRectangle(3, 3, HexTerrainType.Plain);
            var corner = new HexCoord(0, 0);
            world.PlayerPartyTravel.SetAtWorldPosition(
                new WorldVec2(0f, 0f), corner);
            world.LocalMap.ActiveMapLayoutId = "w";

            var connections = new List<SurfaceExitConnection>(6);
            SurfaceExitZoneCalculator.CollectConnections(
                world, DefaultBounds(), Depth, connections);
            Assert.Less(connections.Count, 6);
        }

        [Test]
        public void ImpassableNeighborProducesNoExitConnection()
        {
            var world = BuildTinyTravelWorld(out var siteA, out var mid);
            var party = BuildParty(world, siteA, Spawn(world, "LinQing"));
            SetupWildernessAtHex(world, mid, party);
            const int blockedDir = 1;
            var blocked = HexMath.Neighbor(mid, blockedDir);
            Assert.IsTrue(world.HexWorld.TryGetCell(blocked, out var cell) && cell != null);
            cell.Terrain = HexTerrainType.Water;
            cell.IsPassable = false;

            var connections = new List<SurfaceExitConnection>(6);
            SurfaceExitZoneCalculator.CollectConnections(
                world, DefaultBounds(), Depth, connections);
            for (var i = 0; i < connections.Count; i++)
                Assert.AreNotEqual(blockedDir, connections[i].DirectionIndex);
            Assert.AreEqual(CountTraversableNeighbors(world, mid), connections.Count);
        }

        [Test]
        public void ExitZonePositionUsesActualNeighborWorldDirection()
        {
            var world = BuildTinyTravelWorld(out var siteA, out var mid);
            var party = BuildParty(world, siteA, Spawn(world, "LinQing"));
            SetupWildernessAtHex(world, mid, party);
            var bounds = DefaultBounds();
            var connections = new List<SurfaceExitConnection>(6);
            SurfaceExitZoneCalculator.CollectConnections(world, bounds, Depth, connections);
            Assert.Greater(connections.Count, 0);
            for (var i = 0; i < connections.Count; i++)
                AssertWorldAndLocalDirectionMatch(world, mid, connections[i]);
        }

        [Test]
        public void LocalMapExitVisualDirectionMatchesWorldNeighborDirection()
        {
            var world = BuildTinyTravelWorld(out var siteA, out var mid);
            var party = BuildParty(world, siteA, Spawn(world, "LinQing"));
            SetupWildernessAtHex(world, mid, party);
            var bounds = DefaultBounds();
            var hexSize = world.HexWorld.HexSize;
            HexMath.ToWorldPosition(mid, hexSize, out var sx, out var sy);

            var connections = new List<SurfaceExitConnection>(6);
            SurfaceExitZoneCalculator.CollectConnections(world, bounds, Depth, connections);
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                HexMath.ToWorldPosition(c.DestinationHex, hexSize, out var dx, out var dy);
                var worldAngle = System.Math.Atan2(dy - sy, dx - sx);
                var localAngle = System.Math.Atan2(c.LocalDirectionY, c.LocalDirectionX);
                Assert.AreEqual(worldAngle, localAngle, FloatTol, "dir " + c.DirectionIndex);
            }
        }

        [Test]
        public void ReverseConnectionUsesOppositeWorldDirection()
        {
            var world = BuildTinyTravelWorld(out var siteA, out var mid);
            var party = BuildParty(world, siteA, Spawn(world, "LinQing"));
            SetupWildernessAtHex(world, mid, party);
            var bounds = DefaultBounds();
            const int exitDir = 1;
            var neighbor = HexMath.Neighbor(mid, exitDir);

            var forwardList = new List<SurfaceExitConnection>(6);
            SurfaceExitZoneCalculator.CollectConnections(world, bounds, Depth, forwardList);
            SurfaceExitConnection? forward = null;
            for (var i = 0; i < forwardList.Count; i++)
            {
                if (forwardList[i].DirectionIndex == exitDir)
                {
                    forward = forwardList[i];
                    break;
                }
            }

            Assert.IsTrue(forward.HasValue);
            SetupWildernessAtHex(world, neighbor, party);
            var reverseList = new List<SurfaceExitConnection>(6);
            SurfaceExitZoneCalculator.CollectConnections(world, bounds, Depth, reverseList);
            SurfaceExitConnection? reverse = null;
            for (var i = 0; i < reverseList.Count; i++)
            {
                if (!reverseList[i].DestinationHex.Equals(mid))
                    continue;
                reverse = reverseList[i];
                break;
            }

            Assert.IsTrue(reverse.HasValue);
            Assert.AreEqual(-forward.Value.LocalDirectionX, reverse.Value.LocalDirectionX, FloatTol);
            Assert.AreEqual(-forward.Value.LocalDirectionY, reverse.Value.LocalDirectionY, FloatTol);

            Assert.IsTrue(SurfaceExitZoneCalculator.PointBelongsToConnection(
                reverse.Value.ExitCenterLocalX,
                reverse.Value.ExitCenterLocalY,
                reverse.Value,
                Depth));
        }

        [Test]
        public void ExitConnectionToWorldSiteResolvesWorldSiteMembership()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _);
            var party = BuildParty(world, siteA, Spawn(world, "LinQing"));
            var hex = new HexCoord(4, 4);
            Assert.IsTrue(world.HexWorld.TryGetTile(hex, out _));
            SetupWildernessAtHex(world, hex, party);

            var siteNeighbor = siteA.PresenceHex;
            var dir = -1;
            for (var d = 0; d < 6; d++)
            {
                if (HexMath.Neighbor(hex, d).Equals(siteNeighbor))
                {
                    dir = d;
                    break;
                }
            }

            Assert.GreaterOrEqual(dir, 0);
            var connections = new List<SurfaceExitConnection>(6);
            SurfaceExitZoneCalculator.CollectConnections(world, DefaultBounds(), Depth, connections);
            SurfaceExitConnection? found = null;
            for (var i = 0; i < connections.Count; i++)
            {
                if (connections[i].DirectionIndex != dir)
                    continue;
                found = connections[i];
                break;
            }

            Assert.IsTrue(found.HasValue);
            Assert.AreEqual(SurfaceExitDestinationKind.WorldSite, found.Value.DestinationKind);
            Assert.AreEqual(siteA.SiteId, found.Value.DestinationSiteId);
        }

        [Test]
        public void NoExplicitSixDestinationFieldsRequired()
        {
            var world = BuildTinyTravelWorld(out var siteA, out var mid);
            var party = BuildParty(world, siteA, Spawn(world, "LinQing"));
            SetupWildernessAtHex(world, mid, party);
            var neighbor = HexMath.Neighbor(mid, 0);
            var connections = new List<SurfaceExitConnection>(6);
            SurfaceExitZoneCalculator.CollectConnections(world, DefaultBounds(), Depth, connections);
            SurfaceExitConnection? east = null;
            for (var i = 0; i < connections.Count; i++)
            {
                if (connections[i].DirectionIndex == 0)
                {
                    east = connections[i];
                    break;
                }
            }

            Assert.IsTrue(east.HasValue);
            Assert.AreEqual(neighbor, east.Value.DestinationHex);
        }
    }
}
