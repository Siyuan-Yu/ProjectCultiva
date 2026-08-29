using NUnit.Framework;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests.EditMode
{
    /// <summary>
    /// Phase 5R-B3C1：PlayerParty WorldSite Materialization 位置 Authority 收敛的 startup lifecycle 测试。
    /// 覆盖「真实 startup orchestration / authority ownership」的可测部分（纯函数组合，零
    /// SimulationWorld 依赖）：
    ///  - A 组：ShouldRunStartupBootstrap —— NewGame 初始 Site 的 Bootstrap 是否在启动时立即执行
    ///    （初始 LocalMap 第一次真正建立时完成 AuthoredLocal → Canonical，而不是等离开再回来）。
    ///  - B 组：ShouldConsumeBootstrap —— token 成功才消费；失败保留 pending。
    ///  - C 组：ShouldApplyLegacyStartLocationSnap —— PlayerParty Materialize 后禁止 legacy
    ///    StartLocation snap 覆盖（唯一 placement authority）；仅无 PlayerParty legacy 分支保留。
    ///  - D 组：完整 lifecycle 状态机 —— 用 Session 语义（InitialBootstrapPending）驱动全流程。
    /// </summary>
    public sealed class StartupBootstrapAuthorityTests
    {
        const string StartSite = "base:site_huangcun";
        const string OtherSite = "base:site_guanai";

        // ---- A 组：startup 判定（ShouldRunStartupBootstrap）----

        [Test]
        public void A01_NewGameInitialSite_ShouldRunStartupBootstrap()
        {
            Assert.IsTrue(SiteMaterializeModeResolver.ShouldRunStartupBootstrap(
                pending: true, StartSite, StartSite, atWorldSite: true));
        }

        [Test]
        public void A02_TokenConsumed_ShouldNotRunStartupBootstrap()
        {
            // 已消费（pending=false）：即使 current==initial 也不再启动 Bootstrap
            Assert.IsFalse(SiteMaterializeModeResolver.ShouldRunStartupBootstrap(
                pending: false, StartSite, StartSite, atWorldSite: true));
        }

        [Test]
        public void A03_StartInWilderness_NeverStartupBootstrap()
        {
            // 起点在 Wilderness：initial 为空 → 任何 Site 都不得 Bootstrap
            Assert.IsFalse(SiteMaterializeModeResolver.ShouldRunStartupBootstrap(
                pending: true, string.Empty, StartSite, atWorldSite: true));
        }

        [Test]
        public void A04_CurrentNotInitialSite_NotStartupBootstrap()
        {
            Assert.IsFalse(SiteMaterializeModeResolver.ShouldRunStartupBootstrap(
                pending: true, StartSite, OtherSite, atWorldSite: true));
        }

        [Test]
        public void A05_NotAtWorldSite_NotStartupBootstrap()
        {
            // 初始 Context 不在 Site（例如先到 Wilderness）→ 即使 initial==current 也不 Bootstrap
            Assert.IsFalse(SiteMaterializeModeResolver.ShouldRunStartupBootstrap(
                pending: true, StartSite, StartSite, atWorldSite: false));
        }

        // ---- B 组：token 消费决策（ShouldConsumeBootstrap）----

        [Test]
        public void B01_Success_Consumes()
        {
            Assert.IsTrue(SiteMaterializeModeResolver.ShouldConsumeBootstrap(
                consumeBootstrapNow: true, bootstrapSucceeded: true));
        }

        [Test]
        public void B02_Failure_KeepsToken()
        {
            // Materialize 失败（mapping 失败 / canonical commit 被拒）→ 不消费，保留 pending
            Assert.IsFalse(SiteMaterializeModeResolver.ShouldConsumeBootstrap(
                consumeBootstrapNow: true, bootstrapSucceeded: false));
        }

        [Test]
        public void B03_NotRequested_NeverConsumes()
        {
            Assert.IsFalse(SiteMaterializeModeResolver.ShouldConsumeBootstrap(
                consumeBootstrapNow: false, bootstrapSucceeded: true));
            Assert.IsFalse(SiteMaterializeModeResolver.ShouldConsumeBootstrap(
                consumeBootstrapNow: false, bootstrapSucceeded: false));
        }

        // ---- C 组：legacy StartLocation snap 隔离（ShouldApplyLegacyStartLocationSnap）----

        [Test]
        public void C01_PlayerPartyMaterialized_NeverSnap()
        {
            // PlayerParty 已过 Materialization → 其输出是唯一 placement authority，禁止 snap
            Assert.IsFalse(SiteMaterializeModeResolver.ShouldApplyLegacyStartLocationSnap(
                playerPartyMaterialized: true,
                skipStartSnapForWilderness: false,
                skipStartSnapForSavedPlacements: false));
            Assert.IsFalse(SiteMaterializeModeResolver.ShouldApplyLegacyStartLocationSnap(
                playerPartyMaterialized: true,
                skipStartSnapForWilderness: true,
                skipStartSnapForSavedPlacements: true));
        }

        [Test]
        public void C02_Wilderness_NeverSnap()
        {
            Assert.IsFalse(SiteMaterializeModeResolver.ShouldApplyLegacyStartLocationSnap(
                playerPartyMaterialized: false,
                skipStartSnapForWilderness: true,
                skipStartSnapForSavedPlacements: false));
        }

        [Test]
        public void C03_SavedPlacements_NeverSnap()
        {
            Assert.IsFalse(SiteMaterializeModeResolver.ShouldApplyLegacyStartLocationSnap(
                playerPartyMaterialized: false,
                skipStartSnapForWilderness: false,
                skipStartSnapForSavedPlacements: true));
        }

        [Test]
        public void C04_LegacyNoParty_MaySnap()
        {
            // 无 PlayerParty Materialize 的明确 legacy 分支：snap 仍允许（用户 §五 C）
            Assert.IsTrue(SiteMaterializeModeResolver.ShouldApplyLegacyStartLocationSnap(
                playerPartyMaterialized: false,
                skipStartSnapForWilderness: false,
                skipStartSnapForSavedPlacements: false));
        }

        // ---- D 组：完整 startup lifecycle 状态机（Session pending 语义）----

        [Test]
        public void D01_NewGameInitialSite_FullStartupBootstrapFlow()
        {
            // 模拟 PlayableHostSession：Initialize 后 initial=荒村, pending=true
            var pending = true;
            var initial = StartSite;

            // TryInitialize 链：初始 LocalMap 第一次真正建立 → startup 判定为真
            Assert.IsTrue(SiteMaterializeModeResolver.ShouldRunStartupBootstrap(
                pending, initial, StartSite, atWorldSite: true));
            // Resolve 给 Bootstrap + 请求消费
            var mode = SiteMaterializeModeResolver.Resolve(
                false, initial, StartSite, bootstrapConsumed: !pending, out var consumeNow);
            Assert.AreEqual(PlayerPartySiteMaterializeMode.BootstrapFromAuthoredLocal, mode);
            Assert.IsTrue(consumeNow);
            // Materialize 成功 → 消费（pending=false）
            Assert.IsTrue(SiteMaterializeModeResolver.ShouldConsumeBootstrap(consumeNow, bootstrapSucceeded: true));
            pending = false;
            Assert.IsFalse(pending, "token consumed");
        }

        [Test]
        public void D02_LeaveAndReturnInitialSite_ProjectCanonical()
        {
            // 荒村 → Wilderness → 荒村：第二次荒村 materialize = ProjectCanonicalWorldToLocal
            var pending = false; // 已被 startup bootstrap 消费
            var initial = StartSite;
            var mode = SiteMaterializeModeResolver.Resolve(
                false, initial, StartSite, bootstrapConsumed: !pending, out var consumeNow);
            Assert.AreEqual(PlayerPartySiteMaterializeMode.ProjectCanonicalWorldToLocal, mode);
            Assert.IsFalse(consumeNow);
        }

        [Test]
        public void D03_BootstrapFailure_KeepsPending_RetriesNextTime()
        {
            // 失败路径：consumeBootstrapNow=true 但 Materialize 失败 → token 不消费 → 下次仍 Bootstrap
            var pending = true;
            var initial = StartSite;
            var mode = SiteMaterializeModeResolver.Resolve(
                false, initial, StartSite, bootstrapConsumed: !pending, out var consumeNow);
            Assert.AreEqual(PlayerPartySiteMaterializeMode.BootstrapFromAuthoredLocal, mode);
            Assert.IsTrue(consumeNow);
            Assert.IsFalse(
                SiteMaterializeModeResolver.ShouldConsumeBootstrap(consumeNow, bootstrapSucceeded: false),
                "失败不得消费 token");
            // pending 保留 → 下次（再进荒村）仍 Bootstrap
            var retryMode = SiteMaterializeModeResolver.Resolve(
                false, initial, StartSite, bootstrapConsumed: !pending, out _);
            Assert.AreEqual(PlayerPartySiteMaterializeMode.BootstrapFromAuthoredLocal, retryMode);
        }

        [Test]
        public void D04_PresentationRebuild_DoesNotReviveToken()
        {
            // 表现层重建（scene/WorldMap 重建）不清 Session → pending 保持 false → 不复活
            var pending = false;
            var mode = SiteMaterializeModeResolver.Resolve(
                false, StartSite, StartSite, bootstrapConsumed: !pending, out _);
            Assert.AreEqual(PlayerPartySiteMaterializeMode.ProjectCanonicalWorldToLocal, mode);
            Assert.IsFalse(SiteMaterializeModeResolver.ShouldRunStartupBootstrap(
                pending, StartSite, StartSite, atWorldSite: true));
        }

        [Test]
        public void D05_NewGameFinalSpawn_StillStartLocation_FromBootstrapNotSnap()
        {
            // NewGame 初始 Site 最终仍出生 StartLocation，但它来自 Materialization Bootstrap
            // （Bootstrap 分支保持 px/pz=StartLocation 并建立 canonical），不是后置 Start Snap：
            // playerPartyMaterialized=true → ShouldApplyLegacyStartLocationSnap=false（无第二 writer）。
            var pending = true;
            var mode = SiteMaterializeModeResolver.Resolve(
                false, StartSite, StartSite, bootstrapConsumed: !pending, out var consumeNow);
            Assert.AreEqual(PlayerPartySiteMaterializeMode.BootstrapFromAuthoredLocal, mode);
            Assert.IsTrue(consumeNow);
            // 即使未消费，Materialize 已发生 → 后续 snap 一律禁止
            Assert.IsFalse(SiteMaterializeModeResolver.ShouldApplyLegacyStartLocationSnap(
                playerPartyMaterialized: true,
                skipStartSnapForWilderness: false,
                skipStartSnapForSavedPlacements: false));
        }

        [Test]
        public void D06_StartInWilderness_AnySite_ProjectCanonical()
        {
            // 起点 Wilderness（initial 空，pending=false）：startup 不跑；首次进任意 Site 也 ProjectCanonical
            Assert.IsFalse(SiteMaterializeModeResolver.ShouldRunStartupBootstrap(
                pending: false, string.Empty, StartSite, atWorldSite: true));
            var mode = SiteMaterializeModeResolver.Resolve(
                false, string.Empty, StartSite, bootstrapConsumed: true, out var consumeNow);
            Assert.AreEqual(PlayerPartySiteMaterializeMode.ProjectCanonicalWorldToLocal, mode);
            Assert.IsFalse(consumeNow);
        }
    }
}
