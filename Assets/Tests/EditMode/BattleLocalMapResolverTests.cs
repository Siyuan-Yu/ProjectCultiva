using NUnit.Framework;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class BattleLocalMapResolverTests
    {
        static SimulationWorld World()
        {
            var w = new SimulationWorld(); w.HexWorld.FillRectangle(5, 5, HexTerrainType.Plain); return w;
        }

        [Test] public void WorldSiteUsesSiteMapNotAnchorOrPresence()
        {
            var w = World(); var s = new WorldSite { SiteId = "s", LocalMapId = "site:map", AnchorHex = new HexCoord(1, 1), PresenceHex = new HexCoord(1, 1) };
            s.SetFootprint(new[] { new HexCoord(1, 1), new HexCoord(2, 1) }); w.Strategic.Sites.Register(s);
            var r = BattleLocalMapResolver.Resolve(w, new BattleLocalMapLocation { Kind = BattleLocalMapResolutionKind.WorldSite, SiteId = "s" });
            Assert.IsTrue(r.Success); Assert.AreEqual("site:map", r.LocalMapId); s.AnchorHex = new HexCoord(4, 4); s.PresenceHex = new HexCoord(4, 4);
            Assert.AreEqual("site:map", BattleLocalMapResolver.Resolve(w, new BattleLocalMapLocation { Kind = BattleLocalMapResolutionKind.WorldSite, SiteId = "s" }).LocalMapId);
        }

        [Test] public void WorldSiteFailuresNeverFallback()
        {
            var r = BattleLocalMapResolver.Resolve(World(), new BattleLocalMapLocation { Kind = BattleLocalMapResolutionKind.WorldSite, SiteId = "missing" });
            Assert.IsFalse(r.Success); Assert.AreNotEqual(StrategicEncounterCatalog.DefaultEncounterLocalMapId, r.LocalMapId);
        }

        [Test] public void WildernessUsesExistingFallback()
        {
            var w = World(); var r = BattleLocalMapResolver.Resolve(w, new BattleLocalMapLocation { Kind = BattleLocalMapResolutionKind.Wilderness, BattleHex = new HexCoord(2, 2) });
            Assert.IsTrue(r.Success); Assert.AreEqual(WildernessLocalMapFallback.PlainsWildernessLocalMapId, r.LocalMapId);
        }

        [Test] public void ExplicitMapIsOnlyExplicitKind()
        {
            var r = BattleLocalMapResolver.Resolve(World(), new BattleLocalMapLocation { Kind = BattleLocalMapResolutionKind.ExplicitEncounterMap, ExplicitLocalMapId = "special:arena" });
            Assert.IsTrue(r.Success); Assert.AreEqual("special:arena", r.LocalMapId); Assert.AreEqual(BattleLocalMapResolutionKind.ExplicitEncounterMap, r.Kind);
        }
    }
}
