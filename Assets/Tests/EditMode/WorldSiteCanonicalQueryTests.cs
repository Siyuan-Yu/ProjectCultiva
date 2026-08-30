using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    /// <summary>
    /// Phase 5R-B5：AtWorldSite Canonical Query &amp; WorldMap Marker 位置权威。
    /// Query = PlayerPartyWorldLocationQuery.TryResolve 唯一 authority；
    /// AtWorldSite + valid Canonical → WorldPosition = motion.WorldPosition（连续，不量化 Hex center）；
    /// DerivedHex = WorldToHex(Canonical)；non-finite/no-position → PresenceHex legacy fallback（只读）。
    /// 真实 fixture：ch01_hex_world.json（base:site_huangcun，4-Hex footprint）。
    /// </summary>
    public sealed class WorldSiteCanonicalQueryTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        const float HexSize = 1f;

        static string WorldJsonPath => Path.Combine(BaseGamePath, "Data", "Worlds", "ch01_hex_world.json");

        // 荒村 footprint world domain：[137.698, 142.028] × [75.5, 79.0]（hexSize=1）。
        static readonly WorldVec2 LeftCanonical = new WorldVec2(138.2f, 76.5f);
        static readonly WorldVec2 RightCanonical = new WorldVec2(141.6f, 76.5f);
        static readonly WorldVec2 CenterCanonical = new WorldVec2(139.8f, 77.2f);

        static WorldSite LoadHuangcun()
        {
            var json = File.ReadAllText(WorldJsonPath);
            var j = JsonLite.Parse(json);
            var sites = j.GetArray("definitions")[0].GetArray("sites");
            for (var i = 0; i < sites.Length; i++)
            {
                var s = sites[i];
                if (s.GetString("siteId") != "base:site_huangcun")
                    continue;
                var site = new WorldSite { SiteId = "base:site_huangcun" };
                var fp = s.GetArray("footprint");
                var hexes = new List<HexCoord>();
                for (var f = 0; f < fp.Length; f++)
                    hexes.Add(new HexCoord(fp[f].GetInt("q"), fp[f].GetInt("r")));
                site.AnchorHex = hexes[0];
                site.SetFootprint(hexes);
                return site;
            }
            Assert.Fail("huangcun site not found");
            return null;
        }

        static (SimulationWorld World, WorldSite Site) BuildWorld()
        {
            var site = LoadHuangcun();
            var world = new SimulationWorld();
            world.HexWorld.MapId = "test:ch01";
            world.HexWorld.FillRectangle(140, 100, HexTerrainType.Plain);
            world.Strategic.Sites.Register(site);
            return (world, site);
        }

        /// <summary>AtWorldSite + canonical（footprint 内连续位置，模拟 B4 同步后状态）。</summary>
        static void SetCanonicalAtSite(SimulationWorld world, WorldSite site, WorldVec2 canonical)
        {
            var m = world.PlayerPartyTravel;
            m.SetAtWorldSite(site.SiteId, site.PresenceHex, HexSize);
            Assert.IsTrue(m.TryUpdateWorldPositionWithinSite(site.SiteId, canonical), "canonical set");
        }

        static void AssertUnchangedAfterQuery(
            SimulationWorld world,
            PlayerPartyLocationKind kind,
            string siteId,
            WorldVec2 pos,
            HexCoord hex)
        {
            Assert.AreEqual(kind, world.PlayerPartyTravel.LocationKind, "LocationKind 未变");
            Assert.AreEqual(siteId, world.PlayerPartyTravel.SiteId ?? string.Empty, "SiteId 未变");
            Assert.AreEqual(pos, world.PlayerPartyTravel.WorldPosition, "WorldPosition 未变");
            Assert.AreEqual(hex, world.PlayerPartyTravel.CurrentHex, "CurrentHex 未变");
        }

        // ============================ [1] AtWorldSite + valid Canonical → Query = Canonical ============================

        [Test]
        public void B5_01_AtWorldSiteValidCanonical_QueryReturnsCanonical()
        {
            var (world, site) = BuildWorld();
            SetCanonicalAtSite(world, site, CenterCanonical);

            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, null, out var r), "resolve");
            Assert.IsTrue(r.HasValue);
            Assert.AreEqual(CenterCanonical, r.WorldPosition, "Query WorldPosition == motion.WorldPosition");
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, r.LocationKind, "Context 保持 AtWorldSite");
            Assert.AreEqual(site.SiteId, r.SiteId, "SiteId 保持");
            Assert.IsFalse(r.IsLegacyFallback, "valid Canonical 不走 legacy fallback");
        }

        // ============================ [2] Query 不再返回 PresenceHex center ============================

        [Test]
        public void B5_02_ValidCanonical_NotPresenceHexCenter()
        {
            var (world, site) = BuildWorld();
            // canonical 与 presence center 明显不同
            HexMath.ToWorldPosition(site.PresenceHex, HexSize, out var px, out var py);
            var presenceCenter = new WorldVec2(px, py);
            Assert.AreNotEqual(presenceCenter, CenterCanonical, "fixture: canonical != presence center");

            SetCanonicalAtSite(world, site, CenterCanonical);
            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, null, out var r), "resolve");
            Assert.AreNotEqual(presenceCenter, r.WorldPosition, "不再返回 PresenceHex center");
            Assert.AreEqual(CenterCanonical, r.WorldPosition, "返回 Canonical");
        }

        // ============================ [3][4] 左/右 → Query 左/右 ============================

        [Test]
        public void B5_03_04_CanonicalLeftRight_QueryFollowsContinuously()
        {
            var (world, site) = BuildWorld();

            SetCanonicalAtSite(world, site, LeftCanonical);
            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, null, out var left), "resolve left");
            Assert.AreEqual(LeftCanonical, left.WorldPosition, "左侧 canonical → query 左侧");

            SetCanonicalAtSite(world, site, RightCanonical);
            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, null, out var right), "resolve right");
            Assert.AreEqual(RightCanonical, right.WorldPosition, "右侧 canonical → query 右侧");

            Assert.Greater(right.WorldPosition.X, left.WorldPosition.X, "左→右 连续（不量化/不跳 hex center）");
        }

        // ============================ [5] 同一 Hex 内两个 Canonical → 两个连续位置 ============================

        [Test]
        public void B5_05_SameHexTwoCanonicals_NotQuantizedToHexCenter()
        {
            var (world, site) = BuildWorld();
            // 同一 footprint hex 内两个不同 canonical（基于 hex center 邻域，远离边界数值歧义区）
            var hex = new HexCoord(80, 51);
            HexMath.ToWorldPosition(hex, HexSize, out var cx, out var cy);
            var a = new WorldVec2(cx - 0.2f, cy - 0.05f);
            var b = new WorldVec2(cx + 0.2f, cy + 0.1f);
            Assert.AreEqual(hex, HexMath.WorldToHex(a.X, a.Y, HexSize), "fixture: a in hex");
            Assert.AreEqual(hex, HexMath.WorldToHex(b.X, b.Y, HexSize), "fixture: b in hex");
            var hexCenter = new WorldVec2(cx, cy);

            SetCanonicalAtSite(world, site, a);
            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, null, out var ra), "resolve a");
            SetCanonicalAtSite(world, site, b);
            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, null, out var rb), "resolve b");

            Assert.AreNotEqual(ra.WorldPosition, rb.WorldPosition, "同一 hex 内两个 canonical → 两个不同连续位置");
            Assert.AreNotEqual(hexCenter, ra.WorldPosition, "不量化到 hex center");
            Assert.AreNotEqual(hexCenter, rb.WorldPosition, "不量化到 hex center");
        }

        // ============================ [6] DerivedHex = WorldToHex(Canonical) ============================

        [Test]
        public void B5_06_DerivedHex_IsWorldToHexOfCanonical()
        {
            var (world, site) = BuildWorld();
            // 右缘 canonical：derivedHex 应明显非 PresenceHex（覆盖 footprint 右半区）
            SetCanonicalAtSite(world, site, RightCanonical);

            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, null, out var r), "resolve");
            var expected = HexMath.WorldToHex(RightCanonical.X, RightCanonical.Y, HexSize);
            Assert.AreEqual(expected, r.DerivedHex, "DerivedHex == WorldToHex(Canonical)");
            Assert.AreNotEqual(site.PresenceHex, r.DerivedHex,
                "valid Canonical 时 DerivedHex 不再 = PresenceHex（右缘 canonical 明显不同 hex）");
        }

        // ============================ [7] Query 完全只读 ============================

        [Test]
        public void B5_07_Query_DoesNotMutateMotion()
        {
            var (world, site) = BuildWorld();
            SetCanonicalAtSite(world, site, CenterCanonical);
            var m = world.PlayerPartyTravel;
            AssertUnchangedAfterQuery(world, m.LocationKind, m.SiteId, m.WorldPosition, m.CurrentHex);

            PlayerPartyWorldLocationQuery.TryResolve(world, null, out _);

            AssertUnchangedAfterQuery(world, m.LocationKind, m.SiteId, m.WorldPosition, m.CurrentHex);
        }

        // ============================ [8] healDrift 不覆盖 valid Canonical ============================

        [Test]
        public void B5_08_HealDrift_DoesNotOverwriteValidCanonical()
        {
            var (world, site) = BuildWorld();
            SetCanonicalAtSite(world, site, CenterCanonical);
            var m = world.PlayerPartyTravel;

            PlayerPartyWorldLocationQuery.TryResolve(world, null, out _, healDrift: true);

            Assert.AreEqual(CenterCanonical, m.WorldPosition, "healDrift 不得改写 valid Canonical");
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, m.LocationKind, "Context 保持");
            Assert.AreEqual(site.SiteId, m.SiteId, "SiteId 保持");
        }

        // ============================ [9] PresenceHex 改变 → valid Canonical query 不变 ============================

        [Test]
        public void B5_09_PresenceHexChange_DoesNotAffectValidCanonicalQuery()
        {
            var (world, site) = BuildWorld();
            SetCanonicalAtSite(world, site, CenterCanonical);
            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, null, out var before), "resolve before");

            var newPresence = new HexCoord(81, 52); // footprint 内另一 hex
            site.PresenceHex = newPresence;
            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, null, out var after), "resolve after");

            Assert.AreEqual(before.WorldPosition, after.WorldPosition, "PresenceHex 改变不影响 valid Canonical query");
            Assert.AreEqual(CenterCanonical, after.WorldPosition, "仍为 Canonical");
        }

        // ============================ [10] AnchorHex 改变 → valid Canonical query 不变 ============================

        [Test]
        public void B5_10_AnchorHexChange_DoesNotAffectValidCanonicalQuery()
        {
            var (world, site) = BuildWorld();
            SetCanonicalAtSite(world, site, CenterCanonical);
            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, null, out var before), "resolve before");

            site.AnchorHex = new HexCoord(81, 51); // footprint 内另一 hex
            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, null, out var after), "resolve after");

            Assert.AreEqual(before.WorldPosition, after.WorldPosition, "AnchorHex 改变不影响 valid Canonical query");
            Assert.AreEqual(CenterCanonical, after.WorldPosition, "仍为 Canonical");
        }

        // ============================ [11] non-finite → legacy fallback（只读，不写回 motion） ============================

        [Test]
        public void B5_11_NonFiniteCanonical_LegacyFallbackReadOnly()
        {
            var (world, site) = BuildWorld();
            var m = world.PlayerPartyTravel;
            m.SetAtWorldSite(site.SiteId, site.PresenceHex, HexSize);
            Assert.IsTrue(
                m.TryUpdateWorldPositionWithinSite(site.SiteId, new WorldVec2(float.NaN, 76f)),
                "non-finite canonical set");

            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, null, out var r), "resolve");
            HexMath.ToWorldPosition(site.PresenceHex, HexSize, out var px, out var py);
            Assert.AreEqual(new WorldVec2(px, py), r.WorldPosition, "legacy fallback = PresenceHex center（只读输出）");
            Assert.IsTrue(r.IsLegacyFallback, "fallback 标记");
            Assert.AreEqual(site.PresenceHex, r.DerivedHex, "fallback DerivedHex = PresenceHex");

            // fallback 不写回 motion：WorldPosition 仍是 NaN
            Assert.IsTrue(float.IsNaN(m.WorldPosition.X), "motion 未被写回（保持 non-finite）");
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, m.LocationKind, "Context 保持");
            Assert.AreEqual(site.SiteId, m.SiteId, "SiteId 保持");
        }

        // ============================ [12] AtWorldPosition / Wilderness → 保持现有行为 ============================

        [Test]
        public void B5_12_AtWorldPosition_KeepsExistingBehavior()
        {
            var (world, site) = BuildWorld();
            var m = world.PlayerPartyTravel;
            var hex = new HexCoord(20, 25);
            m.SetAtWorldPosition(new WorldVec2(50f, 50f), hex);

            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, null, out var r), "resolve");
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, r.LocationKind, "kind");
            Assert.AreEqual(string.Empty, r.SiteId ?? string.Empty, "siteId 空");
            Assert.AreEqual(new WorldVec2(50f, 50f), r.WorldPosition, "WorldPosition == motion.WorldPosition");
            Assert.AreEqual(hex, r.DerivedHex, "DerivedHex == contextHex");
        }

        // ============================ [13] traveling / route state → 保持现有行为 ============================

        [Test]
        public void B5_13_TravelingState_KeepsExistingBehavior()
        {
            var (world, site) = BuildWorld();
            var m = world.PlayerPartyTravel;
            m.SetAtWorldSite(site.SiteId, site.PresenceHex, HexSize);
            // 构造跨格 AutoTravel（IsMoving=true，Site departure presentation 未设置）
            m.BeginAutoTravel(
                new List<HexCoord> { site.PresenceHex, new HexCoord(79, 51) },
                new HexCoord(79, 51),
                string.Empty,
                HexTravelMode.Ground,
                HexSize);
            Assert.IsTrue(m.IsMoving, "fixture: moving");

            Assert.IsTrue(PlayerPartyWorldLocationQuery.TryResolve(world, null, out var r), "resolve");
            // travel 中：World executor owns → travel presentation（此处无 departure/uses → 即 WorldPosition）
            Assert.AreEqual(m.ResolveTravelPresentationWorld(HexSize), r.WorldPosition, "travel presentation 位置");
            Assert.AreEqual(m.CurrentHex, r.DerivedHex, "moving 时 DerivedHex = CurrentHex");
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, r.LocationKind, "Context 保持");
        }
    }

    /// <summary>
    /// Phase 5R-B4：WorldSite LocalVisible → Canonical Sync 正式回归测试。
    /// 覆盖 B4 §十六 16 项：ownership policy（WorldMap open / Materialize held / AtWilderness /
    /// departure）、TrySync 唯一 writer、Local 方向连续性、SiteId 防御、mapping failure 保留旧值、
    /// roundtrip &lt;0.05、geometry 复用（不 per-frame rebuild）、零分配热路径。
    /// 真实 fixture：ch01_hex_world.json（base:site_huangcun）+ ch01_reference_map.json（200×100）。
    /// （B4 阶段为独立文件；合并进本文件以保证 Unity 导入/编译可见性。）
    /// </summary>
    public sealed class WorldSiteLocalVisibleSyncTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        const float HexSize = 1f;

        static string WorldJsonPath => Path.Combine(BaseGamePath, "Data", "Worlds", "ch01_hex_world.json");
        static string ReferenceMapJsonPath => Path.Combine(BaseGamePath, "Data", "Maps", "ch01_reference_map.json");

        static WorldSite LoadHuangcun()
        {
            var json = File.ReadAllText(WorldJsonPath);
            var j = JsonLite.Parse(json);
            var sites = j.GetArray("definitions")[0].GetArray("sites");
            for (var i = 0; i < sites.Length; i++)
            {
                var s = sites[i];
                if (s.GetString("siteId") != "base:site_huangcun")
                    continue;
                var site = new WorldSite { SiteId = "base:site_huangcun" };
                var fp = s.GetArray("footprint");
                var hexes = new List<HexCoord>();
                for (var f = 0; f < fp.Length; f++)
                    hexes.Add(new HexCoord(fp[f].GetInt("q"), fp[f].GetInt("r")));
                site.AnchorHex = hexes[0];
                site.SetFootprint(hexes);
                return site;
            }
            Assert.Fail("huangcun site not found");
            return null;
        }

        static WorldSiteSpatialMapping.WorldSiteLocalMapBounds LoadReferenceBounds()
        {
            var json = File.ReadAllText(ReferenceMapJsonPath);
            var j = JsonLite.Parse(json);
            var m = j.GetArray("definitions")[0];
            return WorldSiteSpatialMapping.WorldSiteLocalMapBounds.FromOriginSize(
                m.GetFloat("originX"),
                m.GetFloat("originY"),
                m.GetFloat("cellSize"),
                m.GetInt("width"),
                m.GetInt("height"));
        }

        static HexFootprintSpatialGeometry BuildGeometry(out WorldSiteSpatialMapping.WorldSiteLocalMapBounds bounds)
        {
            var site = LoadHuangcun();
            bounds = LoadReferenceBounds();
            Assert.IsTrue(WorldSiteSpatialMapping.TryBuildGeometry(site, HexSize, out var g), "huangcun geometry");
            Assert.IsTrue(g.HasKernel, "huangcun kernel");
            return g;
        }

        static WorldSiteLocalVisibleSyncContext OkCtx(bool held = false) =>
            new WorldSiteLocalVisibleSyncContext(
                inputBlocked: false,
                isWorldMapOpen: false,
                hasActiveView: true,
                isAtWorldSite: true,
                hasSiteId: true,
                isSiteDeparturePending: false,
                usesTravelPresentation: false,
                isMaterializeHeld: held,
                hasGeometry: true);

        // ============================ [1] AtWorldSite + LocalVisible owner → sync 成功 ============================

        [Test]
        public void B4_01_AtWorldSiteLocalVisibleOwner_SyncSucceeds()
        {
            var g = BuildGeometry(out var bounds);
            var motion = new PlayerPartyWorldMotion();
            motion.SetAtWorldSite("base:site_huangcun", new HexCoord(80, 51), HexSize);

            Assert.IsTrue(WorldSiteLocalVisibleSyncPolicy.CanSync(OkCtx()), "ownership policy");

            var local = new WorldVec2(60f, 25f); // LocalMap 中心
            var outcome = PlayerPartyWorldSiteLocalVisibleSync.TrySync(motion, g, bounds, local, out var canonical);
            Assert.AreEqual(WorldSiteSyncOutcome.Synced, outcome, "center sync");
            Assert.Greater(canonical.X, 137f, "canonical in huangcun footprint X");
            Assert.Less(canonical.X, 143f, "canonical in huangcun footprint X");
            Assert.Greater(canonical.Y, 75f, "canonical in huangcun footprint Y");
            Assert.Less(canonical.Y, 80f, "canonical in huangcun footprint Y");
            Assert.AreEqual(canonical, motion.WorldPosition, "canonical written");
        }

        // ============================ [2] Local 左→右 → canonical 左→右 ============================

        [Test]
        public void B4_02_LocalLeftToRight_MovesCanonicalLeftToRight()
        {
            var g = BuildGeometry(out var bounds);
            var motion = new PlayerPartyWorldMotion();
            motion.SetAtWorldSite("base:site_huangcun", new HexCoord(80, 51), HexSize);

            PlayerPartyWorldSiteLocalVisibleSync.TrySync(motion, g, bounds, new WorldVec2(0f, 25f), out var left);
            PlayerPartyWorldSiteLocalVisibleSync.TrySync(motion, g, bounds, new WorldVec2(120f, 25f), out var right);

            Assert.Greater(right.X, left.X, "Local 右移 → Canonical X 增大（footprint 内连续）");
        }

        // ============================ [3] Local 上→下 → canonical 方向正确 ============================

        [Test]
        public void B4_03_LocalTopToBottom_MovesCanonicalTopToBottom()
        {
            var g = BuildGeometry(out var bounds);
            var motion = new PlayerPartyWorldMotion();
            motion.SetAtWorldSite("base:site_huangcun", new HexCoord(80, 51), HexSize);

            PlayerPartyWorldSiteLocalVisibleSync.TrySync(motion, g, bounds, new WorldVec2(60f, 70f), out var top);
            PlayerPartyWorldSiteLocalVisibleSync.TrySync(motion, g, bounds, new WorldVec2(60f, -20f), out var bottom);

            Assert.Less(bottom.Y, top.Y, "Local 下移 → Canonical Y 减小（footprint 内连续）");
        }

        // ============================ [4] Sync 后 LocationKind / SiteId 不变 ============================

        [Test]
        public void B4_04_SyncKeepsAtWorldSiteContext()
        {
            var g = BuildGeometry(out var bounds);
            var motion = new PlayerPartyWorldMotion();
            motion.SetAtWorldSite("base:site_huangcun", new HexCoord(80, 51), HexSize);

            PlayerPartyWorldSiteLocalVisibleSync.TrySync(motion, g, bounds, new WorldVec2(60f, 25f), out _);
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldSite, motion.LocationKind, "LocationKind 不变");
            Assert.AreEqual("base:site_huangcun", motion.SiteId, "SiteId 不变");
        }

        // ============================ [5] Sync 不修改 CurrentHex ============================

        [Test]
        public void B4_05_SyncDoesNotWriteCurrentHex()
        {
            var g = BuildGeometry(out var bounds);
            var motion = new PlayerPartyWorldMotion();
            motion.SetAtWorldSite("base:site_huangcun", new HexCoord(80, 51), HexSize);
            var before = motion.CurrentHex;

            PlayerPartyWorldSiteLocalVisibleSync.TrySync(motion, g, bounds, new WorldVec2(60f, 25f), out _);
            PlayerPartyWorldSiteLocalVisibleSync.TrySync(motion, g, bounds, new WorldVec2(120f, 40f), out _);

            Assert.AreEqual(before, motion.CurrentHex, "B4 禁止写 CurrentHex（留 5R-C）");
        }

        // ============================ [6] WorldMap open → sync disabled ============================

        [Test]
        public void B4_06_WorldMapOpen_DisablesSync()
        {
            var ctx = OkCtx();
            Assert.IsTrue(WorldSiteLocalVisibleSyncPolicy.CanSync(ctx));
            Assert.IsFalse(
                WorldSiteLocalVisibleSyncPolicy.CanSync(
                    new WorldSiteLocalVisibleSyncContext(
                        ctx.InputBlocked, isWorldMapOpen: true, ctx.HasActiveView, ctx.IsAtWorldSite,
                        ctx.HasSiteId, ctx.IsSiteDeparturePending, ctx.UsesTravelPresentation,
                        ctx.IsMaterializeHeld, ctx.HasGeometry)),
                "WorldMap OPEN → World executor owns → Local→Canonical 禁止");
        }

        // ============================ [7] Materializing → sync disabled ============================

        [Test]
        public void B4_07_Materializing_DisablesSync()
        {
            Assert.IsFalse(WorldSiteLocalVisibleSyncPolicy.CanSync(OkCtx(held: true)), "Materialize 完成帧禁止反写");
        }

        // ============================ [8] Materialize completed → sync enabled ============================

        [Test]
        public void B4_08_MaterializeCompleted_EnablesSync()
        {
            Assert.IsTrue(WorldSiteLocalVisibleSyncPolicy.IsOwnershipEstablished(OkCtx(held: true)),
                "Materialize 完成后（held=false）ownership 建立 → sync enabled");
        }

        // ============================ [9] AtWilderness → sync disabled ============================

        [Test]
        public void B4_09_AtWilderness_DisablesSync()
        {
            var ctx = OkCtx();
            Assert.IsFalse(
                WorldSiteLocalVisibleSyncPolicy.CanSync(
                    new WorldSiteLocalVisibleSyncContext(
                        ctx.InputBlocked, ctx.IsWorldMapOpen, ctx.HasActiveView, isAtWorldSite: false,
                        ctx.HasSiteId, ctx.IsSiteDeparturePending, ctx.UsesTravelPresentation,
                        ctx.IsMaterializeHeld, ctx.HasGeometry)),
                "AtWorldPosition / Wilderness → 禁止 Local→Canonical");
        }

        // ============================ [10] SiteId mismatch → canonical 不修改 ============================

        [Test]
        public void B4_10_SiteIdMismatch_KeepsCanonical()
        {
            var g = BuildGeometry(out var bounds);
            var motion = new PlayerPartyWorldMotion();
            motion.SetAtWorldSite("base:site_huangcun", new HexCoord(80, 51), HexSize);
            var before = motion.WorldPosition;

            // TryUpdateWorldPositionWithinSite 自身防御：expected != motion.SiteId → 拒绝。
            var rejected = motion.TryUpdateWorldPositionWithinSite("base:site_daoguan", new WorldVec2(200f, 200f));
            Assert.IsFalse(rejected, "SiteId mismatch → TryUpdate 拒绝");
            Assert.AreEqual(before, motion.WorldPosition, "canonical 不修改");

            // 非 AtWorldSite 时 TrySync 走 SiteIdRejected（防御路径），canonical 也不变。
            var wildernessMotion = new PlayerPartyWorldMotion();
            wildernessMotion.SetAtWorldPosition(new WorldVec2(10f, 10f), new HexCoord(1, 1));
            var outcome = PlayerPartyWorldSiteLocalVisibleSync.TrySync(
                wildernessMotion, g, bounds, new WorldVec2(60f, 25f), out _);
            Assert.AreEqual(WorldSiteSyncOutcome.SiteIdRejected, outcome, "非 AtWorldSite → SiteIdRejected");
            Assert.AreEqual(new WorldVec2(10f, 10f), wildernessMotion.WorldPosition, "canonical 保持旧值");
        }

        // ============================ [11] mapping failure → canonical 保持旧值 ============================

        [Test]
        public void B4_11_MappingFailure_KeepsCanonical()
        {
            var g = BuildGeometry(out var bounds);
            var motion = new PlayerPartyWorldMotion();
            motion.SetAtWorldSite("base:site_huangcun", new HexCoord(80, 51), HexSize);
            var before = motion.WorldPosition;

            var outcome = PlayerPartyWorldSiteLocalVisibleSync.TrySync(motion, null, bounds, new WorldVec2(60f, 25f), out _);
            Assert.AreEqual(WorldSiteSyncOutcome.MappingFailed, outcome, "geometry null → MappingFailed");
            Assert.AreEqual(before, motion.WorldPosition, "失败保留旧 Canonical（无 fallback）");
        }

        // ============================ [12] Local→Canonical→Local roundtrip < 0.05 ============================

        [Test]
        public void B4_12_LocalCanonicalLocal_RoundtripStable()
        {
            var g = BuildGeometry(out var bounds);
            var motion = new PlayerPartyWorldMotion();
            motion.SetAtWorldSite("base:site_huangcun", new HexCoord(80, 51), HexSize);

            var samples = new[]
            {
                new WorldVec2(60f, 25f),   // 中心
                new WorldVec2(0f, 25f),    // 左中
                new WorldVec2(120f, 25f),  // 右中
                new WorldVec2(60f, 70f),   // 上
                new WorldVec2(60f, -20f),  // 下
                new WorldVec2(-30f, -15f), // 左下
                new WorldVec2(150f, 65f),  // 右上
            };
            var maxErr = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var outcome = PlayerPartyWorldSiteLocalVisibleSync.TrySync(
                    motion, g, bounds, samples[i], out var canonical);
                Assert.AreEqual(WorldSiteSyncOutcome.Synced, outcome, "L2W sync " + i);
                Assert.IsTrue(
                    WorldSiteSpatialMapping.TryWorldSurfaceToLocal(g, bounds, canonical, out var back),
                    "W2L " + i);
                var dx = samples[i].X - back.X;
                var dy = samples[i].Y - back.Y;
                maxErr = System.Math.Max(maxErr, (float)System.Math.Sqrt(dx * dx + dy * dy));
            }
            Assert.Less(maxErr, 0.05f, "Local→Canonical→Local max err（B4 目标 < 0.05）");
        }

        // ============================ [13] AutoTravel 共用同一 writer（TrySync 与移动原因无关） ============================

        [Test]
        public void B4_13_SameWriter_ForWasmRtsAndAutoTravel()
        {
            var g = BuildGeometry(out var bounds);
            var idle = new PlayerPartyWorldMotion();
            idle.SetAtWorldSite("base:site_huangcun", new HexCoord(80, 51), HexSize);

            var moving = new PlayerPartyWorldMotion();
            moving.SetAtWorldSite("base:site_huangcun", new HexCoord(80, 51), HexSize);
            moving.BeginAutoTravel(
                new[] { new HexCoord(80, 51), new HexCoord(81, 51) },
                new HexCoord(81, 51),
                string.Empty,
                HexTravelMode.Ground,
                HexSize);

            var local = new WorldVec2(90f, 30f);
            PlayerPartyWorldSiteLocalVisibleSync.TrySync(idle, g, bounds, local, out var a);
            PlayerPartyWorldSiteLocalVisibleSync.TrySync(moving, g, bounds, local, out var b);

            Assert.AreEqual(a, b, "同一 local → 同一 canonical（移动原因无关，单一 writer）");
        }

        // ============================ [14] Site departure → sync disabled ============================

        [Test]
        public void B4_14_SiteDeparture_DisablesSync()
        {
            var ctx = OkCtx();
            Assert.IsFalse(
                WorldSiteLocalVisibleSyncPolicy.CanSync(
                    new WorldSiteLocalVisibleSyncContext(
                        ctx.InputBlocked, ctx.IsWorldMapOpen, ctx.HasActiveView, ctx.IsAtWorldSite,
                        ctx.HasSiteId, isSiteDeparturePending: true, ctx.UsesTravelPresentation,
                        ctx.IsMaterializeHeld, ctx.HasGeometry)),
                "Site departure / transition ownership → B4 停止");
        }

        // ============================ [15] geometry 在 ownership 生命周期复用（不 per-frame rebuild） ============================

        [Test]
        public void B4_15_GeometryReused_NotRebuiltPerFrame()
        {
            var g = BuildGeometry(out _);
            // TrySync 接收外部已构建 geometry（不内部重建）；同一实例连续复用输出确定、无状态污染。
            // Host 层 cache 语义（同 SiteId+MapId 不重建）由 HostPlayerPartyController 的引用缓存保证。
            var first = default(WorldVec2);
            var second = default(WorldVec2);
            var local = new WorldVec2(60f, 25f);
            for (var i = 0; i < 64; i++)
            {
                var motion = new PlayerPartyWorldMotion();
                motion.SetAtWorldSite("base:site_huangcun", new HexCoord(80, 51), HexSize);
                var outcome = PlayerPartyWorldSiteLocalVisibleSync.TrySync(motion, g, new WorldSiteSpatialMapping.WorldSiteLocalMapBounds(
                    -40f, -25f, 1f, 200, 100), local, out var canonical);
                Assert.AreEqual(WorldSiteSyncOutcome.Synced, outcome, "reuse iter " + i);
                if (i == 0)
                    first = canonical;
                second = canonical;
            }

            Assert.AreEqual(first, second, "同一 geometry 复用 → 输出确定（无内部重建副作用）");
            Assert.IsTrue(g.HasKernel, "geometry remains valid");
        }

        // ============================ [16] geometry overload 热路径 0 分配 ============================

        [Test]
        public void B4_16_HotPath_NoPerCallAllocation()
        {
            var g = BuildGeometry(out var bounds);
            var motion = new PlayerPartyWorldMotion();
            motion.SetAtWorldSite("base:site_huangcun", new HexCoord(80, 51), HexSize);

            // 热身（JIT / static init）
            PlayerPartyWorldSiteLocalVisibleSync.TrySync(motion, g, bounds, new WorldVec2(60f, 25f), out _);

            System.GC.Collect();
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 2000; i++)
            {
                PlayerPartyWorldSiteLocalVisibleSync.TrySync(
                    motion, g, bounds, new WorldVec2(-40 + i % 200, -25 + (i / 200) % 100), out _);
            }

            var allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, "2000 轮 TrySync（L2W + epsilon + TryUpdate）应零分配，got " + allocated);
        }
    }

    /// <summary>
    /// 纯自实现 JSON 解析（零外部依赖，Unity 可用——Unity 默认无 System.Text.Json）。
    /// 支持测试所需子集：object / array / string / number / bool / null。
    /// </summary>
    internal sealed class TestJsonValue
    {
        public bool IsObject;
        public List<KeyValuePair<string, TestJsonValue>> Object;
        public bool IsArray;
        public List<TestJsonValue> Array;
        public bool IsString;
        public string Str;
        public bool IsNumber;
        public double Num;
        public bool IsBool;
        public bool Bool;
        public bool IsNull;

        static readonly TestJsonValue NullValue = new TestJsonValue { IsNull = true };

        public TestJsonValue Get(string name)
        {
            if (!IsObject || Object == null)
                return NullValue;
            for (var i = 0; i < Object.Count; i++)
            {
                if (Object[i].Key == name)
                    return Object[i].Value;
            }
            return NullValue;
        }

        public TestJsonValue At(int index)
        {
            if (!IsArray || Array == null || index < 0 || index >= Array.Count)
                return NullValue;
            return Array[index];
        }

        public int ArrayCount => IsArray && Array != null ? Array.Count : 0;
    }

    /// <summary>递归下降 JSON 解析器（测试 helper，零依赖）。</summary>
    internal static class TestJson
    {
        public static TestJsonValue Parse(string text)
        {
            var p = new Parser(text);
            var v = p.ParseValue();
            p.SkipWs();
            if (p.Pos < text.Length)
                throw new System.FormatException("TestJson: trailing content at " + p.Pos);
            return v;
        }

        sealed class Parser
        {
            readonly string _s;
            int _pos;

            public Parser(string s)
            {
                _s = s ?? string.Empty;
            }

            public int Pos => _pos;

            public void SkipWs()
            {
                while (_pos < _s.Length &&
                       (_s[_pos] == ' ' || _s[_pos] == '\t' || _s[_pos] == '\r' || _s[_pos] == '\n'))
                    _pos++;
            }

            char Peek()
            {
                if (_pos >= _s.Length)
                    throw new System.FormatException("TestJson: unexpected end of input");
                return _s[_pos];
            }

            void Expect(char c)
            {
                if (Peek() != c)
                    throw new System.FormatException("TestJson: expected '" + c + "' at " + _pos);
                _pos++;
            }

            public TestJsonValue ParseValue()
            {
                SkipWs();
                var c = Peek();
                switch (c)
                {
                    case '{':
                        return ParseObject();
                    case '[':
                        return ParseArray();
                    case '"':
                        return new TestJsonValue { IsString = true, Str = ParseString() };
                    case 't':
                        ExpectWord("true");
                        return new TestJsonValue { IsBool = true, Bool = true };
                    case 'f':
                        ExpectWord("false");
                        return new TestJsonValue { IsBool = true, Bool = false };
                    case 'n':
                        ExpectWord("null");
                        return new TestJsonValue { IsNull = true };
                    default:
                        if (c == '-' || (c >= '0' && c <= '9'))
                            return new TestJsonValue { IsNumber = true, Num = ParseNumber() };
                        throw new System.FormatException("TestJson: unexpected char '" + c + "' at " + _pos);
                }
            }

            void ExpectWord(string w)
            {
                for (var i = 0; i < w.Length; i++)
                {
                    if (_pos + i >= _s.Length || _s[_pos + i] != w[i])
                        throw new System.FormatException("TestJson: expected '" + w + "' at " + _pos);
                }
                _pos += w.Length;
            }

            TestJsonValue ParseObject()
            {
                var v = new TestJsonValue
                {
                    IsObject = true,
                    Object = new List<KeyValuePair<string, TestJsonValue>>()
                };
                _pos++; // '{'
                SkipWs();
                if (Peek() == '}')
                {
                    _pos++;
                    return v;
                }
                while (true)
                {
                    SkipWs();
                    var key = ParseString();
                    SkipWs();
                    Expect(':');
                    var val = ParseValue();
                    v.Object.Add(new KeyValuePair<string, TestJsonValue>(key, val));
                    SkipWs();
                    var c = Peek();
                    if (c == ',')
                    {
                        _pos++;
                        continue;
                    }
                    if (c == '}')
                    {
                        _pos++;
                        return v;
                    }
                    throw new System.FormatException("TestJson: expected ',' or '}' at " + _pos);
                }
            }

            TestJsonValue ParseArray()
            {
                var v = new TestJsonValue { IsArray = true, Array = new List<TestJsonValue>() };
                _pos++; // '['
                SkipWs();
                if (Peek() == ']')
                {
                    _pos++;
                    return v;
                }
                while (true)
                {
                    v.Array.Add(ParseValue());
                    SkipWs();
                    var c = Peek();
                    if (c == ',')
                    {
                        _pos++;
                        continue;
                    }
                    if (c == ']')
                    {
                        _pos++;
                        return v;
                    }
                    throw new System.FormatException("TestJson: expected ',' or ']' at " + _pos);
                }
            }

            string ParseString()
            {
                Expect('"');
                var sb = new System.Text.StringBuilder();
                while (true)
                {
                    if (_pos >= _s.Length)
                        throw new System.FormatException("TestJson: unterminated string");
                    var c = _s[_pos++];
                    if (c == '"')
                        return sb.ToString();
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    if (_pos >= _s.Length)
                        throw new System.FormatException("TestJson: bad escape");
                    var e = _s[_pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_pos + 4 > _s.Length)
                                throw new System.FormatException("TestJson: bad \\u escape");
                            var hex = _s.Substring(_pos, 4);
                            _pos += 4;
                            sb.Append((char)System.Convert.ToInt32(hex, 16));
                            break;
                        default:
                            throw new System.FormatException("TestJson: bad escape '\\" + e + "'");
                    }
                }
            }

            double ParseNumber()
            {
                var start = _pos;
                if (_pos < _s.Length && _s[_pos] == '-')
                    _pos++;
                while (_pos < _s.Length && _s[_pos] >= '0' && _s[_pos] <= '9')
                    _pos++;
                if (_pos < _s.Length && _s[_pos] == '.')
                {
                    _pos++;
                    while (_pos < _s.Length && _s[_pos] >= '0' && _s[_pos] <= '9')
                        _pos++;
                }
                if (_pos < _s.Length && (_s[_pos] == 'e' || _s[_pos] == 'E'))
                {
                    _pos++;
                    if (_pos < _s.Length && (_s[_pos] == '+' || _s[_pos] == '-'))
                        _pos++;
                    while (_pos < _s.Length && _s[_pos] >= '0' && _s[_pos] <= '9')
                        _pos++;
                }
                return double.Parse(
                    _s.Substring(start, _pos - start),
                    System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }

    /// <summary>最小 JSON 访问 wrapper（纯自实现 <see cref="TestJson"/> 之上，Unity 可用）。</summary>
    internal sealed class JsonLite
    {
        readonly TestJsonValue _v;

        JsonLite(TestJsonValue v)
        {
            _v = v;
        }

        public static JsonLite Parse(string text)
        {
            return new JsonLite(TestJson.Parse(text));
        }

        public JsonLiteObject GetArray(string name)
        {
            return new JsonLiteObject(_v.Get(name));
        }

        public sealed class JsonLiteObject
        {
            readonly TestJsonValue _v;

            public JsonLiteObject(TestJsonValue v)
            {
                _v = v;
            }

            public JsonLiteObject this[int index] => new JsonLiteObject(_v.At(index));

            public int Length => _v.ArrayCount;

            public JsonLiteObject GetArray(string name) => new JsonLiteObject(_v.Get(name));

            public string GetString(string name) => _v.Get(name).IsString ? _v.Get(name).Str : null;

            public int GetInt(string name) => (int)_v.Get(name).Num;

            public float GetFloat(string name) => (float)_v.Get(name).Num;

            public bool GetBool(string name) => _v.Get(name).IsBool && _v.Get(name).Bool;
        }
    }
}
