using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    /// <summary>HEX-ATTACK / phantom occupancy?Pure Hex ?????? FormalArmy.CurrentHex?</summary>
    public sealed class HexActiveEnemyArmyQueryTests
    {
        const string PlayerFaction = StrategicFactionCatalog.PlayerFactionId;
        const string SiteHuangcun = "base:site_huangcun";
        static readonly HexCoord SiteHex = Ch01HexPrototypeMapBuilder.HuangcunHex;
        static readonly HexCoord EnemyHex = new HexCoord(SiteHex.Q + 2, SiteHex.R + 4);

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = PlayerFaction;
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            WarGateService.DeclareWar(world, PlayerFaction, StrategicFactionCatalog.BanditId);
            return world;
        }

        static FormalArmy SeedBanditAtEnemyHexWithSiteContext(SimulationWorld world)
        {
            var result = ArmyStackAdapter.EnsureBanditPatrolArmy(world, SiteHuangcun);
            Assert.IsTrue(result.IsSuccess);
            ArmyHexTravelService.InitializeArmyAtHex(result.Value, EnemyHex);
            Assert.IsTrue(world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var stack));
            Assert.AreEqual(SiteHuangcun, stack.SiteId, "stack.SiteId is site business context, not position");
            Assert.AreEqual(EnemyHex, result.Value.CurrentHex);
            return result.Value;
        }

        static List<HexActiveEnemyArmyTarget> Collect(SimulationWorld world, HexCoord hex)
        {
            var list = new List<HexActiveEnemyArmyTarget>(2);
            HexActiveEnemyArmyQuery.CollectAtHex(world, hex, PlayerFaction, list);
            return list;
        }

        [Test]
        public void HEX_PHANTOM_01_SiteContext_DoesNotOccupySiteHex()
        {
            var world = CreateWorld();
            SeedBanditAtEnemyHexWithSiteContext(world);

            var atSite = Collect(world, SiteHex);
            Assert.AreEqual(0, atSite.Count, "enemy at non-site hex must not appear at site hex");

            var atEnemy = Collect(world, EnemyHex);
            Assert.AreEqual(1, atEnemy.Count);
            Assert.AreEqual(ArmyStackAdapter.BanditPatrolFormalArmyId, atEnemy[0].FormalArmyId);
        }

        [Test]
        public void HEX_MOVE_01_RelocateEnemy_UpdatesOccupancyFromCurrentHex()
        {
            var world = CreateWorld();
            var enemy = SeedBanditAtEnemyHexWithSiteContext(world);
            var relocated = new HexCoord(SiteHex.Q + 6, SiteHex.R);
            Ch01HexPrototypeMapBuilder.EnsurePrototypeTestBanditHexPassable(world, relocated);
            ArmyHexTravelService.InitializeArmyAtHex(enemy, relocated);

            Assert.IsTrue(world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var stack));
            Assert.AreEqual(SiteHuangcun, stack.SiteId);

            Assert.AreEqual(0, Collect(world, EnemyHex).Count);
            Assert.AreEqual(1, Collect(world, relocated).Count);
            Assert.AreEqual(ArmyStackAdapter.BanditPatrolFormalArmyId, Collect(world, relocated)[0].FormalArmyId);
        }

        [Test]
        public void HEX_ATTACK_01_RightClickEnemyHex_ShowsAttackMenu()
        {
            var world = CreateWorld();
            SeedBanditAtEnemyHexWithSiteContext(world);

            var resolution = HexRightClickResolver.Resolve(
                world,
                EnemyHex,
                PlayerFaction,
                hasSelectedLivingArmy: true,
                hasSelectedMovableArmy: true,
                passableHex: true,
                selectedArmy: null);

            Assert.AreEqual(HexRightClickResolvedAction.ShowAttackTargetMenu, resolution.Action);
            Assert.Contains(HexStrategicContextActionKind.AttackArmy, resolution.MenuActions);
        }
    }
}
