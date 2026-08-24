using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    /// <summary>SITE-RCLICK-01..05 + 回归：Hex WorldSite 菜单进入 LocalMap。</summary>
    public sealed class WorldSiteEntryTests
    {
        const string PlayerFaction = StrategicFactionCatalog.PlayerFactionId;
        const string NodeHuangcun = "base:node_huangcun";
        const string SiteHuangcun = "base:site_huangcun";
        static readonly HexCoord HuangcunHex = Ch01HexPrototypeMapBuilder.HuangcunHex;
        static readonly HexCoord QingyunLuHex = Ch01HexPrototypeMapBuilder.QingyunLuHex;
        static readonly HexCoord PlainHex = new HexCoord(15, 15);

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = PlayerFaction;
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            WarGateService.DeclareWar(world, PlayerFaction, StrategicFactionCatalog.BanditId);
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

        static FormalArmy SpawnPlayerArmy(SimulationWorld world, HexCoord hex)
        {
            var leader = SpawnFriendly(world, "Leader");
            var created = ArmyService.CreateArmy(
                world,
                PlayerFaction,
                NodeHuangcun,
                new[] { leader });
            Assert.IsTrue(created.IsSuccess);
            ArmyHexTravelService.InitializeArmyAtHex(created.Value, hex);
            return created.Value;
        }

        static FormalArmy SpawnActiveEnemyArmy(SimulationWorld world, HexCoord hex)
        {
            var result = ArmyStackAdapter.EnsureBanditPatrolArmy(
                world, NodeHuangcun, string.Empty, string.Empty, -1f);
            Assert.IsTrue(result.IsSuccess);
            ArmyHexTravelService.InitializeArmyAtHex(result.Value, hex);
            return result.Value;
        }

        static FormalArmy SeedBanditAtEnemyHexWithLegacyNode(SimulationWorld world)
        {
            var enemyHex = new HexCoord(HuangcunHex.Q + 2, HuangcunHex.R + 4);
            var army = SpawnActiveEnemyArmy(world, enemyHex);
            Assert.IsTrue(world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var stack));
            Assert.AreEqual(NodeHuangcun, stack.NodeId);
            return army;
        }

        static HexRightClickResolution Resolve(
            SimulationWorld world,
            HexCoord hex,
            FormalArmy selectedArmy,
            bool hasSelectedArmy = true,
            bool hasMovableArmy = true)
        {
            return HexRightClickResolver.Resolve(
                world,
                hex,
                PlayerFaction,
                hasSelectedArmy,
                hasMovableArmy,
                true,
                selectedArmy);
        }

        static void SeedSelfLingeringAtHex(SimulationWorld world, HexCoord hex)
        {
            var downed = SpawnFriendly(world, "Downed");
            EnterIncapacitated(world, downed);
            StrategicResidualPresenceService.PlaceCharacterAtResidualHex(world, downed, hex);

            world.Strategic.Encounter.BattlefieldLingering = true;
            ArmyHexBattleAnchorService.SetBattleAnchorHex(world.Strategic.Participants, hex);
            world.Strategic.Participants.BattleAnchorNodeId = NodeHuangcun;
        }

        static void EnterIncapacitated(SimulationWorld world, EntityId id)
        {
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            CombatDamageRules.EnsureVitals(entity);
            if (entity.TryGet<CombatVitalsComponent>(out var vitals))
                vitals.CurrentHp = 0;
            Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, entity));
        }

        [Test]
        public void HEX_SITE_01_PhantomBanditAtOffset_ShowsEnterMenuAtHuangcun()
        {
            var world = CreateWorld();
            var army = SpawnPlayerArmy(world, HuangcunHex);
            SeedBanditAtEnemyHexWithLegacyNode(world);

            var atSite = new List<HexActiveEnemyArmyTarget>(1);
            HexActiveEnemyArmyQuery.CollectAtHex(world, HuangcunHex, PlayerFaction, atSite);
            Assert.AreEqual(0, atSite.Count, "no phantom 荒村山匪 at site hex");

            var resolution = Resolve(world, HuangcunHex, army);
            Assert.AreEqual(HexRightClickResolvedAction.ShowWorldSiteEnterMenu, resolution.Action);
            Assert.AreNotEqual(HexRightClickResolvedAction.ShowAttackTargetMenu, resolution.Action);
        }

        [Test]
        public void HEX_SITE_02_PhantomBandit_NoAttackBanditMenuLabelAtHuangcun()
        {
            var world = CreateWorld();
            var army = SpawnPlayerArmy(world, HuangcunHex);
            SeedBanditAtEnemyHexWithLegacyNode(world);

            var resolution = Resolve(world, HuangcunHex, army);
            if (resolution.MenuActions != null)
            {
                Assert.IsFalse(resolution.MenuActions.Contains(HexStrategicContextActionKind.AttackArmy));
            }

            Assert.AreNotEqual(HexRightClickResolvedAction.ShowAttackTargetMenu, resolution.Action);
        }

        [Test]
        public void SITE_RCLICK_01_HuangcunArmyAtSite_ShowsEnterMenuNotDirectEnter()
        {
            var world = CreateWorld();
            var army = SpawnPlayerArmy(world, HuangcunHex);

            var resolution = Resolve(world, HuangcunHex, army);
            Assert.AreEqual(HexRightClickResolvedAction.ShowWorldSiteEnterMenu, resolution.Action);
            Assert.AreNotEqual(HexRightClickResolvedAction.DirectMove, resolution.Action);
            Assert.AreEqual(Ch01HexPrototypeMapBuilder.SiteHuangcun, resolution.SiteId);
            Assert.AreEqual("进入青石荒村", StrategicWorldSiteAccessService.BuildEnterSiteMenuLabel(
                world.Strategic.Sites.Sites[Ch01HexPrototypeMapBuilder.SiteHuangcun]));
        }

        [Test]
        public void SITE_RCLICK_02_ClickEnterMenu_EntersHuangcunLocalMap()
        {
            var world = CreateWorld();
            var army = SpawnPlayerArmy(world, HuangcunHex);

            var enter = WorldTravelService.EnterWorldSiteScene(
                world, Ch01HexPrototypeMapBuilder.SiteHuangcun, army.ArmyId);
            Assert.IsTrue(enter.IsSuccess, enter.IsFailure ? enter.Error.ToString() : "");
            Assert.AreEqual("base:map_huangcun", world.PartyWorld.LocalMapId);
            Assert.AreEqual(string.Empty, world.PartyWorld.NodeId);
            Assert.AreEqual(army.ArmyId, world.PartyWorld.FocusFormalArmyId);
        }

        [Test]
        public void SITE_RCLICK_03_ArmyNotAtSite_DirectMoveNoEnterMenu()
        {
            var world = CreateWorld();
            var army = SpawnPlayerArmy(world, PlainHex);

            var resolution = Resolve(world, HuangcunHex, army);
            Assert.AreEqual(HexRightClickResolvedAction.DirectMove, resolution.Action);
            Assert.AreNotEqual(HexRightClickResolvedAction.ShowWorldSiteEnterMenu, resolution.Action);

            var blocked = WorldTravelService.EnterWorldSiteScene(
                world, Ch01HexPrototypeMapBuilder.SiteHuangcun, army.ArmyId);
            Assert.IsTrue(blocked.IsFailure);
            StringAssert.Contains("不在该地点", blocked.Error.Message);
            Assert.AreEqual(PlainHex, army.CurrentHex);
        }

        [Test]
        public void SITE_RCLICK_04_OtherMappedSite_ShowsEnterMenuAndEntersOwnMap()
        {
            var world = CreateWorld();
            var army = SpawnPlayerArmy(world, QingyunLuHex);

            Assert.IsTrue(world.Strategic.Sites.TryGetAtHex(QingyunLuHex, out var site));
            var resolution = Resolve(world, QingyunLuHex, army);
            Assert.AreEqual(HexRightClickResolvedAction.ShowWorldSiteEnterMenu, resolution.Action);

            var enter = WorldTravelService.EnterWorldSiteScene(world, site.SiteId, army.ArmyId);
            Assert.IsTrue(enter.IsSuccess);
            Assert.AreEqual("base:map_qingyun_lu", world.PartyWorld.LocalMapId);
            Assert.AreNotEqual("base:map_huangcun", world.PartyWorld.LocalMapId);
        }

        [Test]
        public void SITE_RCLICK_05_SiteWithoutLocalMap_NoEnterMenu()
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = PlayerFaction;
            world.HexWorld.FillRectangle(8, 8);
            var hex = new HexCoord(3, 3);
            var site = new WorldSite
            {
                SiteId = "base:site_no_map",
                DisplayName = "无图地点",
                AnchorHex = hex,
                LocalMapId = string.Empty,
            };
            site.SetFootprint(new[] { hex });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);

            var leader = world.Entities.CreateCharacter(new DefinitionId("test", "L"), "L").Value;
            leader.Get<FactionMembershipComponent>().Assign(PlayerFaction, FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(leader.Id, SiteHuangcun);
            var created = ArmyService.CreateArmy(world, PlayerFaction, NodeHuangcun, new[] { leader.Id });
            Assert.IsTrue(created.IsSuccess);
            ArmyHexTravelService.InitializeArmyAtHex(created.Value, hex);

            Assert.IsFalse(StrategicWorldSiteAccessService.TryGetEnterableWorldSiteAtHex(
                world, hex, out _));

            var resolution = Resolve(world, hex, created.Value);
            Assert.AreNotEqual(HexRightClickResolvedAction.ShowWorldSiteEnterMenu, resolution.Action);

            var enter = WorldTravelService.EnterWorldSiteScene(world, site.SiteId, created.Value.ArmyId);
            Assert.IsTrue(enter.IsFailure);
            StringAssert.Contains("LocalMap", enter.Error.Message);
        }

        [Test]
        public void SITE_RCLICK_06_EnterThenExit_ArmyStaysAtSiteHex()
        {
            var world = CreateWorld();
            var army = SpawnPlayerArmy(world, HuangcunHex);
            var hexBefore = army.CurrentHex;
            var memberCount = army.MemberCharacterIds.Count;
            var leader = new EntityId(army.MemberCharacterIds[0]);

            Assert.IsTrue(WorldTravelService.EnterWorldSiteScene(
                world, Ch01HexPrototypeMapBuilder.SiteHuangcun, army.ArmyId).IsSuccess);
            Assert.AreEqual(hexBefore, army.CurrentHex);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(army.ArmyId, out _));
            Assert.AreEqual(memberCount, army.MemberCharacterIds.Count);

            world.LocalMap.OverworldMapLayoutId = world.PartyWorld.LocalMapId;
            world.LocalMap.ActiveMapLayoutId = world.PartyWorld.LocalMapId;
            world.LocalMap.ReturnLocationId = "base:loc_stub_return";
            world.LocalMap.ActiveMapLayoutId = world.LocalMap.OverworldMapLayoutId;
            world.LocalMap.ClearOccupants();

            Assert.AreEqual(hexBefore, army.CurrentHex);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(army.ArmyId, out var stillThere));
            Assert.AreEqual(memberCount, stillThere.MemberCharacterIds.Count);
            Assert.AreEqual(leader.Value, stillThere.MemberCharacterIds[0]);
        }

        [Test]
        public void SITE_RCLICK_REG_01_ActiveEnemyAtSite_AttackTakesPriority()
        {
            var world = CreateWorld();
            var army = SpawnPlayerArmy(world, HuangcunHex);
            SpawnActiveEnemyArmy(world, HuangcunHex);

            var resolution = Resolve(world, HuangcunHex, army);
            Assert.AreEqual(HexRightClickResolvedAction.ShowAttackTargetMenu, resolution.Action);
            Assert.AreNotEqual(HexRightClickResolvedAction.ShowWorldSiteEnterMenu, resolution.Action);
        }

        [Test]
        public void SITE_RCLICK_REG_02_LingeringAtSite_ResidualTakesPriority()
        {
            var world = CreateWorld();
            var army = SpawnPlayerArmy(world, HuangcunHex);
            SeedSelfLingeringAtHex(world, HuangcunHex);

            var resolution = Resolve(world, HuangcunHex, army);
            Assert.AreEqual(HexRightClickResolvedAction.DirectEnterFriendlyLingering, resolution.Action);
            Assert.AreNotEqual(HexRightClickResolvedAction.ShowWorldSiteEnterMenu, resolution.Action);
        }

        [Test]
        public void SITE_RCLICK_REG_03_PlainHex_NormalMove()
        {
            var world = CreateWorld();
            var army = SpawnPlayerArmy(world, HuangcunHex);

            var resolution = Resolve(world, PlainHex, army);
            Assert.AreEqual(HexRightClickResolvedAction.DirectMove, resolution.Action);
            Assert.AreNotEqual(HexRightClickResolvedAction.ShowWorldSiteEnterMenu, resolution.Action);
        }
    }
}
