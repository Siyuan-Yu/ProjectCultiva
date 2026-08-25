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
using XianXia.Data.Content;

namespace XianXia.Tests
{
    public sealed class PlayerPartyWorldTravelPhase2BTests
    {
        const string FactionA = "test:faction_a";
        const string TravelWorldId = "base:hex_world_travel_mvp_30x15";

        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        static SimulationWorld BuildTinyTravelWorld(
            out WorldSite siteA,
            out WorldSite siteB,
            out HexCoord midHex)
        {
            var world = new SimulationWorld();
            world.HexWorld.MapId = "test:tiny_travel_world";
            world.HexWorld.FillRectangle(20, 12, HexTerrainType.Plain);
            for (var r = 0; r < 12; r++)
            for (var q = 0; q < 20; q++)
            {
                if (!world.HexWorld.TryGetCell(new HexCoord(q, r), out var cell) || cell == null)
                    continue;
                cell.IsPassable = true;
                if (q >= 8 && q <= 11 && r >= 2 && r <= 5)
                    cell.Terrain = HexTerrainType.Forest;
                if (q >= 14 && q <= 15 && r >= 6 && r <= 9)
                    cell.Terrain = HexTerrainType.Mountain;
            }

            var aAnchor = new HexCoord(2, 4);
            var aPresence = new HexCoord(3, 4);
            siteA = new WorldSite
            {
                SiteId = "test:site_huangcun",
                DisplayName = "青石荒村",
                AnchorHex = aAnchor,
                PresenceHex = aPresence,
                LocalMapId = "base:map_ch01_reference",
            };
            siteA.SetFootprint(new[]
            {
                aAnchor, aPresence, new HexCoord(2, 5), new HexCoord(3, 5),
            });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, siteA);

            var bAnchor = new HexCoord(10, 4);
            siteB = new WorldSite
            {
                SiteId = "test:site_chengzhen",
                DisplayName = "青石镇",
                AnchorHex = bAnchor,
                PresenceHex = bAnchor,
                LocalMapId = "base:map_site_chengzhen",
            };
            siteB.SetFootprint(new[]
            {
                bAnchor, new HexCoord(11, 4), new HexCoord(10, 5), new HexCoord(11, 5),
            });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, siteB);

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
            var roster = new List<EntityId>(members);
            for (var i = 1; i < members.Length; i++)
                Assert.IsTrue(party.TryAddMember(world, roster, members[i], out var err), err);
            return party;
        }

        static void ForceAdvanceToDestination(SimulationWorld world, int maxTicks = 5000)
        {
            for (var i = 0; i < maxTicks && world.PlayerPartyTravel.IsMoving; i++)
                PlayerPartyHexTravelService.AdvanceAll(world, 1);
        }

        [Test]
        public void TRAVEL_01_PlayerParty_CanOwnWorldHex()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out _);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.IsTrue(PlayerPartyHexTravelService.TryResolvePartyWorldHex(world, party, out var hex));
            Assert.AreEqual(siteA.PresenceHex, hex);
        }

        [Test]
        public void TRAVEL_02_AtWorldSite_WorldHex_Equals_PresenceHex()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out _);
            var a = Spawn(world, "LinQing");
            BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);

            Assert.IsTrue(CharacterWorldPresenceQuery.TryGetWorldHex(world, a, out var hex));
            Assert.AreEqual(siteA.PresenceHex, hex);
            Assert.AreNotEqual(siteA.AnchorHex, hex);
        }

        [Test]
        public void TRAVEL_03_Travel_DoesNotCreate_FormalArmy()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            var before = world.Strategic.FormalArmies.Armies.Count;
            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            Assert.AreEqual(before, world.Strategic.FormalArmies.Armies.Count);
            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, a, out _));
        }

        [Test]
        public void TRAVEL_04_MemberCount_And_Active_Unchanged()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var b = Spawn(world, "WangChen");
            var party = BuildParty(world, siteA, a, b);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);

            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            ForceAdvanceToDestination(world);
            Assert.AreEqual(2, party.Count);
            Assert.AreEqual(a, party.ActiveCharacterId);
        }

        [Test]
        public void TRAVEL_05_Path_Uses_Hex_Adjacency_And_PerHex_Update()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);

            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            Assert.IsTrue(world.PlayerPartyTravel.IsMoving);
            Assert.GreaterOrEqual(world.PlayerPartyTravel.HexPathCount, 2);

            var path = world.PlayerPartyTravel.HexPath;
            for (var i = 1; i < path.Count; i++)
                Assert.AreEqual(1, HexMath.Distance(path[i - 1], path[i]));

            var start = world.PlayerPartyTravel.CurrentHex;
            var sawStep = false;
            for (var i = 0; i < 5000 && world.PlayerPartyTravel.IsMoving; i++)
            {
                PlayerPartyHexTravelService.AdvanceAll(world, 1);
                if (world.PlayerPartyTravel.CurrentHex != start)
                {
                    sawStep = true;
                    Assert.AreEqual(1, HexMath.Distance(start, world.PlayerPartyTravel.CurrentHex));
                    break;
                }
            }

            Assert.IsTrue(sawStep, "Travel must advance hex-by-hex, not teleport.");
        }

        [Test]
        public void TRAVEL_06_Cancel_Stops_At_Current_Hex()
        {
            var world = BuildTinyTravelWorld(out var siteA, out var siteB, out _);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);

            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, siteB.PresenceHex).IsSuccess);
            var start = world.PlayerPartyTravel.CurrentHex;
            HexCoord? stepped = null;
            for (var i = 0; i < 5000 && world.PlayerPartyTravel.IsMoving; i++)
            {
                PlayerPartyHexTravelService.AdvanceAll(world, 1);
                if (world.PlayerPartyTravel.CurrentHex != start)
                {
                    stepped = world.PlayerPartyTravel.CurrentHex;
                    break;
                }
            }

            Assert.IsTrue(stepped.HasValue);
            Assert.IsTrue(PlayerPartyHexTravelService.CancelTravel(world, party).IsSuccess);
            Assert.IsFalse(world.PlayerPartyTravel.IsMoving);
            Assert.AreEqual(stepped.Value, world.PlayerPartyTravel.CurrentHex);
            Assert.AreNotEqual(start, world.PlayerPartyTravel.CurrentHex);
            Assert.AreNotEqual(siteB.PresenceHex, world.PlayerPartyTravel.CurrentHex);
        }

        [Test]
        public void TRAVEL_07_Arrive_Site_Footprint_Resolves_Same_Site_And_PresenceHex()
        {
            var world = BuildTinyTravelWorld(out var siteA, out var siteB, out _);
            var a = Spawn(world, "LinQing");
            var b = Spawn(world, "WangChen");
            var party = BuildParty(world, siteA, a, b);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);

            var edge = new HexCoord(11, 5);
            Assert.IsTrue(siteB.OccupiesHex(edge));
            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, edge).IsSuccess);
            ForceAdvanceToDestination(world);
            Assert.IsTrue(world.Strategic.Sites.TryGetAtHex(world.PlayerPartyTravel.CurrentHex, out var at));
            Assert.AreEqual(siteB.SiteId, at.SiteId);

            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, siteB).IsSuccess);
            Assert.AreEqual(siteB.PresenceHex, world.PlayerPartyTravel.CurrentHex);
            Assert.IsTrue(CharacterWorldPresenceQuery.TryGetWorldHex(world, a, out var hexA));
            Assert.AreEqual(siteB.PresenceHex, hexA);
            Assert.AreEqual(a, party.ActiveCharacterId);
            Assert.AreEqual(2, party.Count);
        }

        [Test]
        public void TRAVEL_08_Wilderness_Fallback_Resolves_By_Terrain()
        {
            Assert.IsTrue(WildernessLocalMapFallback.TryResolve(HexTerrainType.Plain, out var plain));
            Assert.AreEqual(WildernessLocalMapFallback.PlainsWildernessLocalMapId, plain);
            Assert.AreNotEqual(WildernessLocalMapFallback.ForbiddenHuangyuanSiteLocalMapId, plain);
            Assert.IsTrue(WildernessLocalMapFallback.TryResolve(HexTerrainType.Road, out var road));
            Assert.AreEqual(WildernessLocalMapFallback.RoadWildernessLocalMapId, road);
            Assert.AreNotEqual(WildernessLocalMapFallback.ForbiddenHuangyuanSiteLocalMapId, road);
            Assert.IsTrue(WildernessLocalMapFallback.TryResolve(HexTerrainType.Forest, out var forest));
            Assert.AreEqual(WildernessLocalMapFallback.ForestWildernessLocalMapId, forest);
            Assert.AreNotEqual(WildernessLocalMapFallback.ForbiddenHuangyuanSiteLocalMapId, forest);
            Assert.IsTrue(WildernessLocalMapFallback.TryResolve(HexTerrainType.Mountain, out var mountain));
            Assert.AreEqual(WildernessLocalMapFallback.MountainWildernessLocalMapId, mountain);
            Assert.AreNotEqual(WildernessLocalMapFallback.ForbiddenHuangyuanSiteLocalMapId, mountain);
            Assert.IsFalse(WildernessLocalMapFallback.TryResolve(HexTerrainType.Water, out _));

            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);
            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            ForceAdvanceToDestination(world);
            Assert.IsTrue(PlayerPartyHexTravelService.EnterLocalViewAtCurrentHex(world, party).IsSuccess);
            Assert.IsFalse(string.IsNullOrEmpty(world.PartyWorld.LocalMapId));
            Assert.AreEqual(mid, world.PlayerPartyTravel.CurrentHex);
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var wp));
            Assert.IsTrue(wp.UsesHexPresence);
            Assert.AreEqual(mid, wp.ResidualHex);
        }

        [Test]
        public void TRAVEL_09_Background_Does_Not_Follow_Party_Travel()
        {
            var world = BuildTinyTravelWorld(out var siteA, out var siteB, out _);
            var a = Spawn(world, "LinQing");
            var b = Spawn(world, "WangChen");
            var party = BuildParty(world, siteA, a, b);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);

            Assert.IsTrue(party.TryRemoveMember(b, out _));
            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, siteB.PresenceHex).IsSuccess);
            ForceAdvanceToDestination(world);
            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, siteB).IsSuccess);

            Assert.IsTrue(world.WorldPresence.TryGet(b, out var left));
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, left.Mode);
            Assert.AreEqual(siteA.SiteId, left.SiteId);
            Assert.IsTrue(CharacterWorldPresenceQuery.TryGetWorldHex(world, b, out var leftHex));
            Assert.AreEqual(siteA.PresenceHex, leftHex);
            Assert.IsFalse(ArmyWorldMapPresentation.ShouldDrawIndependentCharacterPortrait(world, b));
        }

        [Test]
        public void TRAVEL_10_Marker_Uses_Active_Avatar_PartyId_Stable()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out _);
            var a = Spawn(world, "LinQing");
            var b = Spawn(world, "WangChen");
            var party = BuildParty(world, siteA, a, b);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);

            Assert.AreEqual(a, party.ActiveCharacterId);
            Assert.IsTrue(party.TrySetActive(world, b, out _));
            Assert.AreEqual(b, party.ActiveCharacterId);
            Assert.AreEqual(2, party.Count);
            Assert.IsTrue(PlayerPartyHexTravelService.TryResolvePartyWorldHex(world, party, out var hex));
            Assert.AreEqual(siteA.PresenceHex, hex);
            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, a, out _));
            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, b, out _));
        }

        [Test]
        public void TRAVEL_11_FormalArmy_And_PlayerParty_Are_Distinct()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);

            var before = world.Strategic.FormalArmies.Armies.Count;
            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            ForceAdvanceToDestination(world);
            Assert.AreEqual(before, world.Strategic.FormalArmies.Armies.Count);
            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, a, out _));
            Assert.IsTrue(world.PlayerPartyTravel.HasPosition);
            Assert.AreEqual(mid, world.PlayerPartyTravel.CurrentHex);
        }

        [Test]
        public void TRAVEL_12_Wilderness_Materialize_Shows_Active_And_Followers()
        {
            var world = BuildTinyTravelWorld(out var siteA, out _, out var mid);
            var a = Spawn(world, "LinQing");
            var b = Spawn(world, "WangChen");
            var party = BuildParty(world, siteA, a, b);
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);

            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            ForceAdvanceToDestination(world);
            Assert.IsTrue(PlayerPartyHexTravelService.EnterLocalViewAtCurrentHex(world, party).IsSuccess);

            var beforeActive = party.ActiveCharacterId;
            var beforeCount = party.Count;
            PlayerPartyLocalMapMaterializationService.MaterializePartyOnResolvedLocalMap(
                world, party.Members);

            Assert.AreEqual(beforeActive, party.ActiveCharacterId);
            Assert.AreEqual(beforeCount, party.Count);
            Assert.AreEqual(mid, world.PlayerPartyTravel.CurrentHex);
            Assert.IsTrue(world.LocalMap.ContainsOccupant(a));
            Assert.IsTrue(world.LocalMap.ContainsOccupant(b));
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var wpA));
            Assert.IsTrue(wpA.UsesHexPresence);
            Assert.AreEqual(mid, wpA.ResidualHex);
            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, a, out _));

            world.LocalMap.ActiveMapLayoutId = world.PartyWorld.LocalMapId;
            Assert.IsTrue(
                PlayerPartyLocalMapMaterializationService.IsWildernessPartyMemberVisibleOnActiveLocalMap(
                    world, a, wpA));
            Assert.IsTrue(world.WorldPresence.TryGet(b, out var wpB));
            Assert.IsTrue(
                PlayerPartyLocalMapMaterializationService.IsWildernessPartyMemberVisibleOnActiveLocalMap(
                    world, b, wpB));
        }

        [Test]
        public void TRAVEL_13_Site_And_Wilderness_Share_Materialize_Entry()
        {
            var world = BuildTinyTravelWorld(out var siteA, out var siteB, out var mid);
            var a = Spawn(world, "LinQing");
            var party = BuildParty(world, siteA, a);

            // Wilderness expand
            world.PlayerPartyTravel.SetIdleAt(siteA.PresenceHex);
            Assert.IsTrue(PlayerPartyHexTravelService.BeginTravel(world, party, mid).IsSuccess);
            ForceAdvanceToDestination(world);
            Assert.IsTrue(PlayerPartyHexTravelService.EnterLocalViewAtCurrentHex(world, party).IsSuccess);
            Assert.IsTrue(PlayerPartyLocalMapMaterializationService.IsWildernessLocalExpand(world));
            PlayerPartyLocalMapMaterializationService.MaterializePartyOnResolvedLocalMap(
                world, party.Members);
            Assert.IsTrue(world.LocalMap.ContainsOccupant(a));

            // Site expand via same Materialize API
            Assert.IsTrue(PlayerPartyHexTravelService.EnterWorldSiteAsParty(world, party, siteB).IsSuccess);
            Assert.IsFalse(PlayerPartyLocalMapMaterializationService.IsWildernessLocalExpand(world));
            PlayerPartyLocalMapMaterializationService.MaterializePartyOnResolvedLocalMap(
                world, party.Members);
            Assert.AreEqual(a, party.ActiveCharacterId);
            Assert.AreEqual(siteB.PresenceHex, world.PlayerPartyTravel.CurrentHex);
            Assert.IsTrue(world.LocalMap.ContainsOccupant(a));
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var atSite));
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, atSite.Mode);
            Assert.AreEqual(siteB.SiteId, atSite.SiteId);
        }

        [Test]
        public void TESTWORLD_01_TravelMvp_Content_Size_Sites_NoOverlap()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            Assert.IsTrue(
                loaded.Value.Registry.TryGetHexWorldContent(
                    DefinitionId.Parse(TravelWorldId).Value,
                    out var definition),
                "Missing " + TravelWorldId);

            Assert.AreEqual(30, definition.Width);
            Assert.AreEqual(15, definition.Height);
            Assert.GreaterOrEqual(definition.Sites.Count, 4);
            Assert.LessOrEqual(definition.Sites.Count, 6);

            var world = new SimulationWorld();
            Assert.IsTrue(HexWorldContentLoader.Apply(world, definition).IsSuccess);
            Assert.AreEqual(30, world.HexWorld.Width);
            Assert.AreEqual(15, world.HexWorld.Height);
            Assert.GreaterOrEqual(world.Strategic.Sites.Sites.Count, 4);
            Assert.LessOrEqual(world.Strategic.Sites.Sites.Count, 6);

            var seen = new Dictionary<HexCoord, string>();
            foreach (var kv in world.Strategic.Sites.Sites)
            {
                var site = kv.Value;
                if (site == null)
                    continue;
                Assert.IsTrue(WorldSiteFootprintValidator.IsPresenceInFootprint(site), site.SiteId);
                foreach (var hex in site.EnumerateFootprintHexes())
                {
                    Assert.IsFalse(seen.ContainsKey(hex), "Footprint overlap at " + hex);
                    seen[hex] = site.SiteId;
                }
            }

            // Ch01 formal world must still exist untouched.
            Assert.IsTrue(
                loaded.Value.Registry.TryGetHexWorldContent(
                    DefinitionId.Parse(HexStrategicMapBootstrap.DefaultHexWorldContentId).Value,
                    out var ch01));
            Assert.AreEqual(200, ch01.Width);
            Assert.AreEqual(100, ch01.Height);
        }
    }
}
