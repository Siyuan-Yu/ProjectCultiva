using System;
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
    public sealed class SurfaceExitZoneOverlapTests
    {
        const string FactionA = "test:faction_a";
        const float Depth = SurfaceExitZoneCalculator.DefaultExitTriggerDepth;
        const float FloatTol = 0.02f;

        static WildernessLocalWorldProjection.WildernessLocalMapBounds DefaultBounds() =>
            WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(0f, 0f, 1f, 16, 16);

        static SimulationWorld BuildWorldWithHex(HexCoord hex, out PlayerPartyRuntime party)
        {
            var world = new SimulationWorld();
            world.HexWorld.HexSize = 1f;
            world.HexWorld.FillRectangle(12, 12, HexTerrainType.Plain);
            for (var r = 0; r < 12; r++)
            for (var q = 0; q < 12; q++)
            {
                if (!world.HexWorld.TryGetCell(new HexCoord(q, r), out var cell) || cell == null)
                    continue;
                cell.IsPassable = true;
            }

            var site = new WorldSite
            {
                SiteId = "test:site",
                AnchorHex = new HexCoord(0, 0),
                PresenceHex = new HexCoord(0, 0),
                LocalMapId = "base:map_test",
            };
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);

            var id = world.Entities.CreateCharacter(new DefinitionId("test", "Hero"), "Hero");
            Assert.IsTrue(id.IsSuccess);
            id.Value.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(id.Value.Id, out _));
            world.PlayerPartyTravel.SnapToHexCenter(hex, world.HexWorld.HexSize);
            world.LocalMap.ActiveMapLayoutId = "w";
            world.LocalMap.OverworldMapLayoutId = "w";
            return world;
        }

        static List<SurfaceExitConnection> Collect(SimulationWorld world)
        {
            var list = new List<SurfaceExitConnection>(6);
            SurfaceExitZoneCalculator.CollectConnections(world, DefaultBounds(), Depth, list);
            return list;
        }

        static List<SurfaceExitConnection> BuildRawWithoutResolve(SimulationWorld world, HexCoord hex)
        {
            var list = new List<SurfaceExitConnection>(6);
            var bounds = DefaultBounds();
            for (var dir = 0; dir < 6; dir++)
            {
                var neighbor = HexMath.Neighbor(hex, dir);
                if (!world.HexWorld.TryGetTile(neighbor, out var tile) || tile == null ||
                    tile.Terrain == HexTerrainType.Water || !tile.IsPassable)
                    continue;
                Assert.IsTrue(SurfaceExitZoneCalculator.TryBuildConnectionBetweenHexes(
                    world, hex, neighbor, dir, world.HexWorld.HexSize, bounds, Depth,
                    SurfaceExitZoneCalculator.DefaultSlotSpanFraction, out var c));
                list.Add(c);
            }

            return list;
        }

        static bool RectsOverlap(SurfaceExitCoverageRect a, SurfaceExitCoverageRect b) =>
            a.MinX < b.MaxX - 0.001f && b.MinX < a.MaxX - 0.001f &&
            a.MinY < b.MaxY - 0.001f && b.MinY < a.MaxY - 0.001f;

        static float AlongEdgeSpan(SurfaceExitConnection c, WildernessLocalWorldProjection.WildernessLocalMapBounds bounds)
        {
            var rect = c.SlotRect;
            if (Math.Abs(c.LocalDirectionX) >= Math.Abs(c.LocalDirectionY))
                return rect.Height / (bounds.MaxY - bounds.MinY);
            return rect.Width / (bounds.MaxX - bounds.MinX);
        }

        static bool SameEdge(SurfaceExitConnection a, SurfaceExitConnection b)
        {
            var aVert = Math.Abs(a.LocalDirectionX) >= Math.Abs(a.LocalDirectionY);
            var bVert = Math.Abs(b.LocalDirectionX) >= Math.Abs(b.LocalDirectionY);
            if (aVert != bVert)
                return false;
            if (aVert)
                return (a.LocalDirectionX > 0f) == (b.LocalDirectionX > 0f);
            return (a.LocalDirectionY > 0f) == (b.LocalDirectionY > 0f);
        }

        [Test]
        public void ExitZonesNeverOverlapAfterResolution()
        {
            var world = BuildWorldWithHex(new HexCoord(1, 1), out _);
            var connections = Collect(world);
            Assert.Greater(connections.Count, 1);
            for (var i = 0; i < connections.Count; i++)
            for (var j = i + 1; j < connections.Count; j++)
            {
                if (!SameEdge(connections[i], connections[j]))
                    continue;
                Assert.IsFalse(
                    RectsOverlap(connections[i].SlotRect, connections[j].SlotRect),
                    "dir " + connections[i].DirectionIndex + " vs " + connections[j].DirectionIndex);
            }
        }

        [Test]
        public void ExitZoneSpanNeverExceedsOneHalf()
        {
            var world = BuildWorldWithHex(new HexCoord(1, 1), out _);
            var bounds = DefaultBounds();
            var connections = Collect(world);
            for (var i = 0; i < connections.Count; i++)
            {
                var frac = AlongEdgeSpan(connections[i], bounds);
                Assert.LessOrEqual(frac, SurfaceExitZoneCalculator.MaxSlotSpanFraction + FloatTol);
            }
        }

        [Test]
        public void ExitZoneSpanDoesNotShrinkBelowOneSixthUnlessAbsolutelyRequired()
        {
            var world = BuildWorldWithHex(new HexCoord(6, 4), out _);
            var bounds = DefaultBounds();
            var connections = Collect(world);
            for (var i = 0; i < connections.Count; i++)
            {
                var frac = AlongEdgeSpan(connections[i], bounds);
                Assert.GreaterOrEqual(frac, SurfaceExitZoneCalculator.MinSlotSpanFraction - FloatTol);
            }
        }

        [Test]
        public void OverlapResolutionPrefersShrinkBeforePositionShift()
        {
            var hex = new HexCoord(6, 4);
            var world = BuildWorldWithHex(hex, out _);
            var raw = BuildRawWithoutResolve(world, hex);
            var resolved = Collect(world);
            Assert.AreEqual(raw.Count, resolved.Count);

            for (var i = 0; i < resolved.Count; i++)
            {
                SurfaceExitConnection? rawMatch = null;
                for (var j = 0; j < raw.Count; j++)
                {
                    if (raw[j].DirectionIndex != resolved[i].DirectionIndex)
                        continue;
                    rawMatch = raw[j];
                    break;
                }

                Assert.IsTrue(rawMatch.HasValue);
                var rawC = rawMatch.Value;
                var resC = resolved[i];
                var rawSpan = AlongEdgeSpan(rawC, DefaultBounds());
                var resSpan = AlongEdgeSpan(resC, DefaultBounds());
                if (resSpan + FloatTol < rawSpan)
                {
                    Assert.AreEqual(rawC.ExitCenterLocalX, resC.ExitCenterLocalX, FloatTol);
                    Assert.AreEqual(rawC.ExitCenterLocalY, resC.ExitCenterLocalY, FloatTol);
                }
            }
        }

        [Test]
        public void ResolvedZonesPreserveDirectionalOrdering()
        {
            var world = BuildWorldWithHex(new HexCoord(1, 1), out _);
            var connections = Collect(world);
            for (var edge = 0; edge < 4; edge++)
            {
                var onEdge = new List<SurfaceExitConnection>(4);
                for (var i = 0; i < connections.Count; i++)
                {
                    var c = connections[i];
                    var vert = Math.Abs(c.LocalDirectionX) >= Math.Abs(c.LocalDirectionY);
                    var edgeId = vert
                        ? (c.LocalDirectionX > 0f ? 0 : 1)
                        : (c.LocalDirectionY > 0f ? 2 : 3);
                    if (edgeId != edge)
                        continue;
                    onEdge.Add(c);
                }

                if (onEdge.Count < 2)
                    continue;

                onEdge.Sort((a, b) =>
                {
                    var aCoord = Math.Abs(a.LocalDirectionX) >= Math.Abs(a.LocalDirectionY)
                        ? a.ExitCenterLocalY : a.ExitCenterLocalX;
                    var bCoord = Math.Abs(b.LocalDirectionX) >= Math.Abs(b.LocalDirectionY)
                        ? b.ExitCenterLocalY : b.ExitCenterLocalX;
                    return aCoord.CompareTo(bCoord);
                });

                for (var i = 1; i < onEdge.Count; i++)
                {
                    var prev = onEdge[i - 1];
                    var cur = onEdge[i];
                    if (Math.Abs(prev.LocalDirectionX) >= Math.Abs(prev.LocalDirectionY))
                        Assert.LessOrEqual(prev.ExitCenterLocalY, cur.ExitCenterLocalY + FloatTol);
                    else
                        Assert.LessOrEqual(prev.ExitCenterLocalX, cur.ExitCenterLocalX + FloatTol);
                }
            }
        }

        [Test]
        public void ResolvedGeometryIsDeterministic()
        {
            var world = BuildWorldWithHex(new HexCoord(1, 1), out _);
            var a = Collect(world);
            var b = Collect(world);
            Assert.AreEqual(a.Count, b.Count);
            for (var i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].DirectionIndex, b[i].DirectionIndex);
                Assert.AreEqual(a[i].SlotRect.MinX, b[i].SlotRect.MinX, FloatTol);
                Assert.AreEqual(a[i].SlotRect.MaxX, b[i].SlotRect.MaxX, FloatTol);
                Assert.AreEqual(a[i].SlotRect.MinY, b[i].SlotRect.MinY, FloatTol);
                Assert.AreEqual(a[i].SlotRect.MaxY, b[i].SlotRect.MaxY, FloatTol);
            }
        }

        [Test]
        public void RendererAndDetectionUseSameResolvedGeometry()
        {
            var world = BuildWorldWithHex(new HexCoord(1, 1), out _);
            var bounds = DefaultBounds();
            var connections = Collect(world);
            var step = 0.15f;
            for (var i = 0; i < connections.Count; i++)
            {
                var conn = connections[i];
                var rects = new List<SurfaceExitCoverageRect>(1);
                SurfaceExitZoneCalculator.AppendConnectionCoverageRects(conn, rects);
                Assert.AreEqual(1, rects.Count);
                for (var x = bounds.MinX; x <= bounds.MaxX + 0.001f; x += step)
                for (var y = bounds.MinY; y <= bounds.MaxY + 0.001f; y += step)
                {
                    var belongs = SurfaceExitZoneCalculator.PointBelongsToConnection(x, y, conn, Depth);
                    if (!belongs)
                        continue;
                    var rect = rects[0];
                    Assert.IsTrue(
                        x >= rect.MinX - 0.001f && x <= rect.MaxX + 0.001f &&
                        y >= rect.MinY - 0.001f && y <= rect.MaxY + 0.001f);
                }
            }
        }

        [Test]
        public void ConnectionCountDoesNotChangeDuringOverlapResolution()
        {
            var hex = new HexCoord(1, 1);
            var world = BuildWorldWithHex(hex, out _);
            var rawCount = BuildRawWithoutResolve(world, hex).Count;
            var resolvedCount = Collect(world).Count;
            Assert.AreEqual(rawCount, resolvedCount);
            Assert.Greater(resolvedCount, 0);
        }
    }
}
