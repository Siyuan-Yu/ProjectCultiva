using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    /// <summary>WorldSite LocalMap 人口：Resident + 足迹�?Army 成员，EnteringArmy 不作人口过滤�?/summary>
    public sealed class WorldSiteLocalMapPopulationTests
    {
        const string PlayerFaction = StrategicFactionCatalog.PlayerFactionId;
        const string NodeHuangcun = "base:site_huangcun";
        const string SiteHuangcun = "base:site_huangcun";
        static readonly HexCoord HuangcunHex = Ch01HexPrototypeMapBuilder.HuangcunHex;

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = PlayerFaction;
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            return world;
        }

        static EntityId SpawnFriendly(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<FactionMembershipComponent>().Assign(PlayerFaction, FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(created.Value.Id, SiteHuangcun);
            return created.Value.Id;
        }

        static FormalArmy CreatePlayerArmyAtHuangcun(
            SimulationWorld world,
            params EntityId[] members)
        {
            Assert.IsNotEmpty(members);
            var created = ArmyService.CreateArmy(world, PlayerFaction, NodeHuangcun, members);
            Assert.IsTrue(created.IsSuccess);
            ArmyHexTravelService.InitializeArmyAtHex(created.Value, HuangcunHex);
            return created.Value;
        }

        static void EnterHuangcunLocalMap(SimulationWorld world, FormalArmy enteringArmy)
        {
            var siteId = Ch01HexPrototypeMapBuilder.SiteHuangcun;
            var enter = WorldTravelService.EnterWorldSiteScene(world, siteId, enteringArmy.ArmyId);
            Assert.IsTrue(enter.IsSuccess, enter.IsFailure ? enter.Error.ToString() : string.Empty);
            world.LocalMap.ActiveMapLayoutId = world.PartyWorld.LocalMapId;
            world.LocalMap.OverworldMapLayoutId = world.PartyWorld.LocalMapId;
        }

        static WorldSite RequireHuangcunSite(SimulationWorld world)
        {
            Assert.IsTrue(world.Strategic.Sites.TryGet(Ch01HexPrototypeMapBuilder.SiteHuangcun, out var site));
            return site;
        }

        static List<EntityId> CollectPopulation(SimulationWorld world, WorldSite site, IReadOnlyList<EntityId> candidates)
        {
            var list = new List<EntityId>(8);
            StrategicWorldSitePopulationService.CollectCharacterIdsPresentAtWorldSite(
                world, site, candidates, list);
            return list;
        }

        [Test]
        public void SITE_POP_01_ResidentCharacter_VisibleAfterWorldSiteEnter()
        {
            var world = CreateWorld();
            var resident = SpawnFriendly(world, "Resident");
            var army = CreatePlayerArmyAtHuangcun(world, SpawnFriendly(world, "Leader"));

            EnterHuangcunLocalMap(world, army);

            Assert.IsTrue(
                StrategicWorldSitePopulationService.IsCharacterPresentAtWorldSite(
                    world, resident, RequireHuangcunSite(world)));
            Assert.IsTrue(LocalMapVisibility.IsEntityVisible(world, resident));
        }

        [Test]
        public void SITE_POP_02_EnteringArmyMembers_AllVisible()
        {
            var world = CreateWorld();
            var a1 = SpawnFriendly(world, "A1");
            var a2 = SpawnFriendly(world, "A2");
            var a3 = SpawnFriendly(world, "A3");
            var army = CreatePlayerArmyAtHuangcun(world, a1, a2, a3);

            EnterHuangcunLocalMap(world, army);

            Assert.IsTrue(LocalMapVisibility.IsEntityVisible(world, a1));
            Assert.IsTrue(LocalMapVisibility.IsEntityVisible(world, a2));
            Assert.IsTrue(LocalMapVisibility.IsEntityVisible(world, a3));
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(army.ArmyId, out var stillThere));
            Assert.AreEqual(HuangcunHex, stillThere.CurrentHex);
        }

        [Test]
        public void SITE_POP_03_ResidentAndArmyMembers_CoexistWithoutDedupLoss()
        {
            var world = CreateWorld();
            var r1 = SpawnFriendly(world, "R1");
            var r2 = SpawnFriendly(world, "R2");
            var a1 = SpawnFriendly(world, "A1");
            var a2 = SpawnFriendly(world, "A2");
            var a3 = SpawnFriendly(world, "A3");
            var army = CreatePlayerArmyAtHuangcun(world, a1, a2, a3);
            var candidates = new List<EntityId> { r1, r2, a1, a2, a3 };

            EnterHuangcunLocalMap(world, army);

            var population = CollectPopulation(world, RequireHuangcunSite(world), candidates);
            Assert.GreaterOrEqual(population.Count, 5);
            Assert.Contains(r1, population);
            Assert.Contains(r2, population);
            Assert.Contains(a1, population);
            Assert.Contains(a2, population);
            Assert.Contains(a3, population);

            for (var i = 0; i < candidates.Count; i++)
                Assert.IsTrue(LocalMapVisibility.IsEntityVisible(world, candidates[i]));
        }

        [Test]
        public void SITE_POP_04_CanLoadMapLayout_UsesSitePopulationNotFocusArmyOnly()
        {
            var world = CreateWorld();
            var resident = SpawnFriendly(world, "Resident");
            var army = CreatePlayerArmyAtHuangcun(world, SpawnFriendly(world, "Leader"));
            EnterHuangcunLocalMap(world, army);

            var mapId = world.PartyWorld.LocalMapId;
            Assert.IsTrue(LocalMapVisibility.CanLoadMapLayoutForParty(
                world, new List<EntityId> { resident }, mapId));
        }

        [Test]
        public void SITE_POP_05_PrototypeBanditsOutsideSite_NotVisibleOnHuangcunLocalMap()
        {
            var world = CreateWorld();
            world.Strategic.PlayerFactionId = PlayerFaction;
            Ch01ScenarioStrategicSetup.Apply(world);

            var army = CreatePlayerArmyAtHuangcun(world, SpawnFriendly(world, "Leader"));
            EnterHuangcunLocalMap(world, army);

            var banditNames = new[] { "BanditLeader", "BanditA", "BanditB", "BanditC", "WeakBandit" };
            for (var i = 0; i < banditNames.Length; i++)
            {
                var id = FindEntityByName(world, banditNames[i]);
                Assert.IsFalse(
                    id.IsNone,
                    banditNames[i] + " should exist after Ch01 setup");
                Assert.IsFalse(
                    LocalMapVisibility.IsEntityVisible(world, id),
                    banditNames[i] + " must not appear on initial 荒村 LocalMap");
            }

            var resident = SpawnFriendly(world, "VillageResident");
            Assert.IsTrue(LocalMapVisibility.IsEntityVisible(world, resident));
        }

        static EntityId FindEntityByName(SimulationWorld world, string displayName)
        {
            foreach (var entity in world.Entities.All)
            {
                if (string.Equals(entity.DisplayName, displayName, System.StringComparison.Ordinal))
                    return entity.Id;
            }

            return EntityId.None;
        }
    }
}
