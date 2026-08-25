using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Tests
{
    public sealed class CharacterWorldPresencePhase2ATests
    {
        const string FactionA = "test:faction_a";
        const string SiteId = "test:site_qingshi";

        static SimulationWorld BuildSiteWorld(out WorldSite site, out HexCoord presenceHex)
        {
            var world = new SimulationWorld();
            world.HexWorld.FillRectangle(20, 20);
            var anchor = new HexCoord(8, 8);
            presenceHex = HexMath.Neighbor(anchor, 0);
            Assert.AreNotEqual(anchor, presenceHex);
            var footprint = new List<HexCoord>
            {
                anchor,
                presenceHex,
                HexMath.Neighbor(anchor, 1),
                HexMath.Neighbor(anchor, 5),
            };
            site = new WorldSite
            {
                SiteId = SiteId,
                DisplayName = "青石镇",
                SiteType = "Town",
                AnchorHex = anchor,
                PresenceHex = presenceHex,
                LocalMapId = "test:map_qingshi",
            };
            site.SetFootprint(footprint);
            Assert.IsTrue(WorldSiteFootprintValidator.IsPresenceInFootprint(site));
            Assert.AreNotEqual(site.AnchorHex, site.PresenceHex);
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

        [Test]
        public void PRESENCE_01_PresenceHexMustBelongToFootprint()
        {
            var site = new WorldSite
            {
                SiteId = "test:bad",
                AnchorHex = new HexCoord(1, 1),
                PresenceHex = new HexCoord(9, 9),
            };
            site.SetFootprint(new[] { new HexCoord(1, 1), new HexCoord(2, 1) });
            // SetFootprint EnsurePresenceHexValid migrates invalid Presence → Anchor
            Assert.IsTrue(WorldSiteFootprintValidator.IsPresenceInFootprint(site));
            Assert.AreEqual(site.AnchorHex, site.PresenceHex);
        }

        [Test]
        public void PRESENCE_02_MultiHexSite_AllowsNonAnchorPresenceHex()
        {
            BuildSiteWorld(out var site, out var presence);
            Assert.AreNotEqual(site.AnchorHex, presence);
            Assert.AreEqual(presence, site.PresenceHex);
            Assert.IsTrue(site.OccupiesHex(presence));
        }

        [Test]
        public void PRESENCE_03_QueryReturnsPresenceHex_NotAnchor()
        {
            var world = BuildSiteWorld(out var site, out var presence);
            var id = Spawn(world, "WangChen");
            world.WorldPresence.SetAtSite(id, site.SiteId);
            Assert.IsTrue(CharacterWorldPresenceQuery.TryGetWorldHex(world, id, out var hex));
            Assert.AreEqual(presence, hex);
            Assert.AreNotEqual(site.AnchorHex, hex);
        }

        [Test]
        public void PRESENCE_04_LoadedLocalMapCharacter_WorldHexEqualsSitePresence()
        {
            var world = BuildSiteWorld(out var site, out var presence);
            var id = Spawn(world, "LinQing");
            world.WorldPresence.SetAtSite(id, site.SiteId);
            world.PartyWorld.SiteId = site.SiteId;
            world.PartyWorld.LocalMapId = site.LocalMapId;
            world.LocalMap.AddOccupant(id);

            Assert.IsTrue(CharacterWorldPresenceQuery.TryDescribe(
                world, id, out var state, out var siteId, out var hex, out var loaded));
            Assert.AreEqual(CharacterWorldPresenceQuery.PresenceState.AtWorldSite, state);
            Assert.AreEqual(site.SiteId, siteId);
            Assert.AreEqual(presence, hex);
            Assert.IsTrue(loaded);
        }

        [Test]
        public void PRESENCE_05_MultipleCharactersShareSamePresenceHex()
        {
            var world = BuildSiteWorld(out var site, out var presence);
            var a = Spawn(world, "A");
            var b = Spawn(world, "B");
            var c = Spawn(world, "C");
            world.WorldPresence.SetAtSite(a, site.SiteId);
            world.WorldPresence.SetAtSite(b, site.SiteId);
            world.WorldPresence.SetAtSite(c, site.SiteId);
            Assert.IsTrue(CharacterWorldPresenceQuery.TryGetWorldHex(world, a, out var ha));
            Assert.IsTrue(CharacterWorldPresenceQuery.TryGetWorldHex(world, b, out var hb));
            Assert.IsTrue(CharacterWorldPresenceQuery.TryGetWorldHex(world, c, out var hc));
            Assert.AreEqual(presence, ha);
            Assert.AreEqual(presence, hb);
            Assert.AreEqual(presence, hc);
        }

        [Test]
        public void PRESENCE_06_StopFollow_KeepsWorldSiteAndPresenceHex()
        {
            var world = BuildSiteWorld(out var site, out var presence);
            var active = Spawn(world, "Active");
            var follower = Spawn(world, "Follower");
            world.WorldPresence.SetAtSite(active, site.SiteId);
            world.WorldPresence.SetAtSite(follower, site.SiteId);
            world.LocalMap.AddOccupant(active);
            world.LocalMap.AddOccupant(follower);

            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(active, out _));
            Assert.IsTrue(party.TryAddMember(world, new[] { active, follower }, follower, out _));
            Assert.IsTrue(party.TryRemoveMember(follower, out _));

            Assert.IsFalse(party.IsMember(follower));
            Assert.IsTrue(world.WorldPresence.TryGet(follower, out var wp));
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, wp.Mode);
            Assert.AreEqual(site.SiteId, wp.SiteId);
            Assert.IsTrue(CharacterWorldPresenceQuery.TryGetWorldHex(world, follower, out var hex));
            Assert.AreEqual(presence, hex);
        }

        [Test]
        public void PRESENCE_07_SwitchActive_DoesNotChangePartyWorldHex()
        {
            var world = BuildSiteWorld(out var site, out var presence);
            var a = Spawn(world, "A");
            var b = Spawn(world, "B");
            world.WorldPresence.SetAtSite(a, site.SiteId);
            world.WorldPresence.SetAtSite(b, site.SiteId);
            world.LocalMap.AddOccupant(a);
            world.LocalMap.AddOccupant(b);
            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryInitialize(a, out _));
            Assert.IsTrue(party.TryAddMember(world, new[] { a, b }, b, out _));
            Assert.IsTrue(CharacterWorldPresenceQuery.TryGetPartyWorldHex(world, party, out var before));
            Assert.AreEqual(presence, before);
            Assert.IsTrue(party.TrySetActive(world, b, out _));
            Assert.IsTrue(CharacterWorldPresenceQuery.TryGetPartyWorldHex(world, party, out var after));
            Assert.AreEqual(before, after);
        }

        [Test]
        public void PRESENCE_08_LocalMapUnload_KeepsDomainAndWorldLocation()
        {
            var world = BuildSiteWorld(out var site, out var presence);
            var id = Spawn(world, "ZhaoYue");
            world.WorldPresence.SetAtSite(id, site.SiteId);
            world.PartyWorld.SiteId = site.SiteId;
            world.PartyWorld.LocalMapId = site.LocalMapId;
            world.LocalMap.AddOccupant(id);

            // Simulate LocalMap unload / leave site: clear presentation focus, keep Domain + Presence
            world.LocalMap.Clear();
            world.PartyWorld.ClearSiteFocus();
            world.PartyWorld.LocalMapId = string.Empty;

            Assert.IsTrue(world.Entities.TryGet(id, out var ent) && ent != null);
            Assert.IsTrue(world.WorldPresence.TryGet(id, out var wp));
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, wp.Mode);
            Assert.AreEqual(site.SiteId, wp.SiteId);
            Assert.IsTrue(CharacterWorldPresenceQuery.TryGetWorldHex(world, id, out var hex));
            Assert.AreEqual(presence, hex);
            Assert.IsTrue(CharacterWorldPresenceQuery.TryDescribe(
                world, id, out _, out _, out _, out var loaded));
            Assert.IsFalse(loaded);
        }

        [Test]
        public void PRESENCE_09_FormalArmyMember_WorldHexIsArmyCurrentHex()
        {
            var world = BuildSiteWorld(out _, out _);
            var armyHex = new HexCoord(3, 3);
            world.HexWorld.GetOrCreate(armyHex).IsPassable = true;
            var leader = Spawn(world, "ArmyLeader");
            world.WorldPresence.SetAtSite(leader, SiteId);
            var army = ArmyService.CreateArmy(world, FactionA, SiteId, new[] { leader }).Value;
            ArmyHexTravelService.InitializeArmyAtHex(army, armyHex);
            Assert.IsTrue(army.UsesHexStrategicPosition);
            Assert.IsTrue(CharacterWorldPresenceQuery.TryGetWorldHex(world, leader, out var hex));
            Assert.AreEqual(armyHex, hex);
            Assert.IsTrue(CharacterWorldPresenceQuery.TryDescribe(
                world, leader, out var state, out _, out _, out _));
            Assert.AreEqual(CharacterWorldPresenceQuery.PresenceState.FormalArmyMember, state);
        }

        [Test]
        public void PRESENCE_10_BackgroundAtSite_NoWorldMapAvatar()
        {
            var world = BuildSiteWorld(out var site, out _);
            var id = Spawn(world, "BackgroundNpc");
            world.WorldPresence.SetAtSite(id, site.SiteId);
            Assert.IsFalse(ArmyWorldMapPresentation.ShouldDrawIndependentCharacterPortrait(world, id));
        }

        [Test]
        public void PRESENCE_11_SaveLoad_KeepsBackgroundWorldSiteAndHexQuery()
        {
            var world = BuildSiteWorld(out var site, out var presence);
            var id = Spawn(world, "PersistedNpc");
            world.WorldPresence.SetAtSite(id, site.SiteId);

            var strategic = StrategicSnapshotHelper.Capture(world);
            Assert.AreEqual(1, strategic.CharacterWorldPresences.Count);
            Assert.AreEqual(site.SiteId, strategic.CharacterWorldPresences[0].SiteId);
            Assert.AreEqual((int)PartyWorldPresenceMode.AtSite, strategic.CharacterWorldPresences[0].Mode);

            var loaded = BuildSiteWorld(out _, out _);
            var id2 = Spawn(loaded, "PersistedNpc");
            strategic.CharacterWorldPresences[0].CharacterId = id2.Value;
            StrategicSnapshotHelper.Restore(loaded, strategic);

            Assert.IsTrue(loaded.WorldPresence.TryGet(id2, out var wp));
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, wp.Mode);
            Assert.AreEqual(site.SiteId, wp.SiteId);
            Assert.IsTrue(CharacterWorldPresenceQuery.TryGetWorldHex(loaded, id2, out var hex));
            Assert.AreEqual(presence, hex);
        }

        [Test]
        public void PRESENCE_12_LegacyContent_MissingPresence_MigratesToAnchor()
        {
            var def = new HexWorldContentDefinition
            {
                Id = DefinitionId.Parse("test:hex_legacy_presence").Value,
                Name = "Legacy",
                Width = 12,
                Height = 12,
                HexSize = 1f,
            };
            def.Sites.Add(new HexWorldSiteDefinition
            {
                SiteId = "test:legacy_site",
                DisplayName = "Legacy Site",
                SiteType = "Village",
                AnchorQ = 4,
                AnchorR = 4,
                Footprint =
                {
                    new HexWorldCoordDefinition { Q = 4, R = 4 },
                    new HexWorldCoordDefinition { Q = 5, R = 4 },
                },
            });

            var world = new SimulationWorld();
            Assert.IsTrue(HexWorldContentLoader.Apply(world, def).IsSuccess);
            Assert.IsTrue(world.Strategic.Sites.TryGet("test:legacy_site", out var site));
            Assert.AreEqual(new HexCoord(4, 4), site.PresenceHex);
            Assert.IsTrue(WorldSiteFootprintValidator.IsPresenceInFootprint(site));
        }

        [Test]
        public void PRESENCE_13_Ch01Content_PresenceHexPresentAndValid()
        {
            var baseGame = Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));
            var loaded = new ContentPackageLoader().Load(new[] { baseGame });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            Assert.IsTrue(
                loaded.Value.Registry.TryGetHexWorldContent(
                    DefinitionId.Parse(HexStrategicMapBootstrap.DefaultHexWorldContentId).Value,
                    out var definition));

            foreach (var siteDef in definition.Sites)
            {
                Assert.IsTrue(siteDef.PresenceQ.HasValue, siteDef.SiteId);
                Assert.IsTrue(siteDef.PresenceR.HasValue, siteDef.SiteId);
            }

            var world = new SimulationWorld();
            Assert.IsTrue(HexWorldContentLoader.Apply(world, definition).IsSuccess);
            Assert.IsTrue(world.Strategic.Sites.TryGet("base:site_chengzhen", out var site));
            Assert.IsTrue(WorldSiteFootprintValidator.IsPresenceInFootprint(site));
        }
    }
}
