using NUnit.Framework;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests.EditMode
{
    /// <summary>
    /// Phase 5R-B3B.3：Site Materialization ownership provenance 纯逻辑测试。
    /// 验证 BootstrapFromAuthoredLocal 只允许 NewGame 初始 Site 的首次展开；Wilderness→Site
    /// 进入（即使第一次进入该 Site）/ Site reopen 一律 ProjectCanonicalWorldToLocal；
    /// snapshot restore 优先 LegacyRestoreLocal。不依赖 SimulationWorld（纯函数）。
    /// </summary>
    public sealed class SiteMaterializeModeResolverTests
    {
        const string StartSite = "base:site_huangcun";
        const string OtherSite = "base:site_guanai";

        static PlayerPartySiteMaterializeMode Resolve(
            bool snapshot,
            string initial,
            string current,
            bool consumed,
            out bool consumeNow) =>
            SiteMaterializeModeResolver.Resolve(snapshot, initial, current, consumed, out consumeNow);

        // ---- 1. NewGameInitialSite → BootstrapFromAuthoredLocal（一次性）----

        [Test]
        public void SMR_01_NewGameInitialSite_BootstrapOnce()
        {
            // 启动链记录初始 Site = 当前 Site，未消费 → Bootstrap + 消费标记
            var mode = Resolve(false, StartSite, StartSite, false, out var consumeNow);
            Assert.AreEqual(PlayerPartySiteMaterializeMode.BootstrapFromAuthoredLocal, mode);
            Assert.IsTrue(consumeNow, "首次初始 Site 展开应标记消费");
        }

        [Test]
        public void SMR_02_AfterConsumed_ProjectCanonical()
        {
            // 初始 Site 首次展开已消费 → 之后（含 reopen）一律 ProjectCanonical
            var mode = Resolve(false, StartSite, StartSite, true, out var consumeNow);
            Assert.AreEqual(PlayerPartySiteMaterializeMode.ProjectCanonicalWorldToLocal, mode);
            Assert.IsFalse(consumeNow, "已消费后不再 Bootstrap");
        }

        // ---- 2. Wilderness→Site（即使第一次进入该 Site）→ ProjectCanonicalWorldToLocal ----

        [Test]
        public void SMR_03_WildernessToSite_NoInitialSite_ProjectCanonical()
        {
            // 起点在 Wilderness（初始无 Site provenance）→ 第一次进入任意 Site 也只能 ProjectCanonical
            var mode = Resolve(false, string.Empty, OtherSite, false, out var consumeNow);
            Assert.AreEqual(PlayerPartySiteMaterializeMode.ProjectCanonicalWorldToLocal, mode);
            Assert.IsFalse(consumeNow, "起点非 Site 时永不 Bootstrap");
        }

        [Test]
        public void SMR_04_WildernessToSite_InitialSiteIsDifferent_ProjectCanonical()
        {
            // NewGame 初始在 StartSite，但第一次 Site 展开是 OtherSite（先到 Wilderness 再进别的 Site）
            // → 不能把 OtherSite 当成 bootstrap；保持 InitialBootstrapSiteId 未被错误消费
            var mode = Resolve(false, StartSite, OtherSite, false, out var consumeNow);
            Assert.AreEqual(PlayerPartySiteMaterializeMode.ProjectCanonicalWorldToLocal, mode);
            Assert.IsFalse(consumeNow, "非初始 Site 不得消费初始 Bootstrap provenance");
        }

        // ---- 3. Site reopen → ProjectCanonicalWorldToLocal ----

        [Test]
        public void SMR_05_SiteReopen_ProjectCanonical()
        {
            var mode = Resolve(false, string.Empty, OtherSite, true, out _);
            Assert.AreEqual(PlayerPartySiteMaterializeMode.ProjectCanonicalWorldToLocal, mode);
        }

        // ---- 4/5. 正式 canonical ingress：StartLocation 不参与 = 必须是 ProjectCanonical ----

        [Test]
        public void SMR_06_CanonicalIngress_NeverBootstrap()
        {
            // 有可信 Canonical 的 Wilderness→Site / reopen 主链：任何已消费 / 非初始场景都不得 Bootstrap
            var cases = new[]
            {
                (snapshot: false, initial: StartSite, current: OtherSite, consumed: false),
                (snapshot: false, initial: string.Empty, current: OtherSite, consumed: false),
                (snapshot: false, initial: StartSite, current: StartSite, consumed: true),
                (snapshot: false, initial: string.Empty, current: string.Empty, consumed: false),
            };
            foreach (var c in cases)
            {
                var mode = Resolve(c.snapshot, c.initial, c.current, c.consumed, out _);
                Assert.AreEqual(
                    PlayerPartySiteMaterializeMode.ProjectCanonicalWorldToLocal,
                    mode,
                    "canonical ingress must be ProjectCanonical for " + c);
            }
        }

        // ---- 6. Mapping failure 防护：snapshot / 异常路径不与 Bootstrap 混淆 ----

        [Test]
        public void SMR_07_SnapshotRestore_PrefersLegacyRestore()
        {
            // snapshot restore 优先 LegacyRestoreLocal，即使 initial=current 且未消费
            var mode = Resolve(true, StartSite, StartSite, false, out var consumeNow);
            Assert.AreEqual(PlayerPartySiteMaterializeMode.LegacyRestoreLocal, mode);
            Assert.IsFalse(consumeNow, "snapshot restore 不触发 NewGame Bootstrap 消费");
        }
        // ---- 7. Phase 5R-B3B.5：初始 Site 离开再回来 / 表现层重建，token 不得复活 ----

        [Test]
        public void SMR_08_InitialSiteLeaveAndReturn_ProjectCanonical()
        {
            // 运行时失败确切场景：NewGame 初始荒村首次展开 Bootstrap + 消费 → 离开到 Wilderness
            // → 再进荒村。即使 current==initial，只要已消费就必须 ProjectCanonical（Boundary
            // Canonical 不得被 Bootstrap 覆盖）。
            // 首次：initial=huangcun current=huangcun pending → Bootstrap + 消费
            var mode1 = Resolve(false, StartSite, StartSite, false, out var consume1);
            Assert.AreEqual(PlayerPartySiteMaterializeMode.BootstrapFromAuthoredLocal, mode1);
            Assert.IsTrue(consume1);
            // 离开到 Wilderness 再回荒村：consumed=true → ProjectCanonical
            var mode2 = Resolve(false, StartSite, StartSite, true, out var consume2);
            Assert.AreEqual(PlayerPartySiteMaterializeMode.ProjectCanonicalWorldToLocal, mode2);
            Assert.IsFalse(consume2, "初始 Site 再进入不得重新 Bootstrap");
        }

        [Test]
        public void SMR_09_PresentationRebuild_DoesNotReviveToken()
        {
            // token 消费后 Host／LocalMap 表现层重建（scene 重建）不得让 consumed 重新变 false。
            // Session 持有 InitialBootstrapPending；消费后 pending=false → 传入 resolver 的
            // bootstrapConsumed=!pending=true → 永不 Bootstrap。
            var pending = true;
            var consumed = !pending; // Session.InitialBootstrapPending → bootstrapConsumed 映射
            var mode1 = Resolve(false, StartSite, StartSite, consumed, out _);
            Assert.AreEqual(PlayerPartySiteMaterializeMode.BootstrapFromAuthoredLocal, mode1);
            // 消费：pending=false
            pending = false;
            consumed = !pending;
            // 表现层重建（Session 不被 Clear，pending 保持 false）→ 再进荒村仍 ProjectCanonical
            var mode2 = Resolve(false, StartSite, StartSite, consumed, out var consume2);
            Assert.AreEqual(PlayerPartySiteMaterializeMode.ProjectCanonicalWorldToLocal, mode2);
            Assert.IsFalse(consume2);
        }
    }
}
