using NUnit.Framework;
using System.Linq;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

public sealed class WorldSiteBattleSpatialPolicyTests
{
    static HexCoord H(int q, int r) => new HexCoord(q, r);

    [Test] public void SingleHexAreaAndRing() {
        var s = new WorldSite(); s.SetFootprint(new[] { H(3, 3) });
        var ring = WorldSiteBattleSpatialPolicy.CollectSupportRing(s);
        Assert.AreEqual(1, WorldSiteBattleSpatialPolicy.CollectBattleArea(s).Count);
        Assert.AreEqual(6, ring.Count); Assert.IsFalse(ring.Contains(H(3, 3)));
    }

    [Test] public void AdjacentFootprintIsUnionWithoutDuplicates() {
        var s = new WorldSite(); s.SetFootprint(new[] { H(3, 3), H(4, 3) });
        var ring = WorldSiteBattleSpatialPolicy.CollectSupportRing(s);
        Assert.AreEqual(2, WorldSiteBattleSpatialPolicy.CollectBattleArea(s).Count);
        Assert.IsFalse(ring.Contains(H(3, 3)) || ring.Contains(H(4, 3)));
        Assert.AreEqual(ring.Count, ring.Distinct().Count());
    }

    [Test] public void IrregularFootprintDoesNotUseBoundingBox() {
        var s = new WorldSite(); s.SetFootprint(new[] { H(0, 0), H(1, 0), H(0, 1) });
        var ring = WorldSiteBattleSpatialPolicy.CollectSupportRing(s);
        Assert.IsTrue(ring.Contains(H(1, 1)));
    }

    [Test] public void AnchorAndPresenceDoNotChangeArea() {
        var s = new WorldSite();
        s.SetFootprint(new[] { H(2, 2), H(3, 2) });
        s.AnchorHex = H(0, 0); s.PresenceHex = H(0, 0);
        var before = WorldSiteBattleSpatialPolicy.CollectSupportRing(s);
        s.AnchorHex = H(9, 9); s.PresenceHex = H(9, 9);
        CollectionAssert.AreEquivalent(before, WorldSiteBattleSpatialPolicy.CollectSupportRing(s));
    }
}
