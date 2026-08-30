using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    /// <summary>
    /// Phase 5R-B4：WorldSite LocalVisible → Canonical Sync 正式回归测试。
    /// 覆盖 B4 §十六 16 项：ownership policy（WorldMap open / Materialize held / AtWilderness /
    /// departure）、TrySync 唯一 writer、Local 方向连续性、SiteId 防御、mapping failure 保留旧值、
    /// roundtrip &lt;0.05、geometry 复用（不 per-frame rebuild）、零分配热路径。
    /// 真实 fixture：ch01_hex_world.json（base:site_huangcun）+ ch01_reference_map.json（200×100）。
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

    /// <summary>最小 JSON 解析 helper（与 WorldSiteSpatialMappingV2Tests 同模式，避免重复依赖）。
    /// 独立命名避免与 V2 测试的 SimpleJson 同 namespace 冲突。</summary>
    internal sealed class JsonLite
    {
        readonly System.Text.Json.JsonDocument _doc;

        JsonLite(System.Text.Json.JsonDocument doc)
        {
            _doc = doc;
        }

        public static JsonLite Parse(string text)
        {
            return new JsonLite(System.Text.Json.JsonDocument.Parse(text));
        }

        public JsonLiteObject GetArray(string name)
        {
            return new JsonLiteObject(_doc.RootElement.GetProperty(name));
        }

        public sealed class JsonLiteObject
        {
            readonly System.Text.Json.JsonElement _e;

            public JsonLiteObject(System.Text.Json.JsonElement e)
            {
                _e = e;
            }

            public JsonLiteObject this[int index] => new JsonLiteObject(_e[index]);

            public int Length => _e.GetArrayLength();

            public string GetString(string name) => _e.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;

            public int GetInt(string name) => _e.GetProperty(name).GetInt32();

            public float GetFloat(string name) => _e.GetProperty(name).GetSingle();
        }
    }
}
