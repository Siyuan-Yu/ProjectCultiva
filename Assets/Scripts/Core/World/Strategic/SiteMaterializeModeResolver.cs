using System;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 5R-B2：Site LocalMap Materialization 初始化 ownership 的 transient 模式。
    /// 只在 Materialize 调用阶段区分「第一次建立 Canonical WorldPosition」的来源；
    /// 不落盘、不进入 <see cref="PlayerPartyWorldMotion"/>、不是长期位置真源。
    /// </summary>
    public enum PlayerPartySiteMaterializeMode
    {
        /// <summary>不改变既有行为（非 Site 展开或调用方未指定）。</summary>
        Default = 0,
        /// <summary>NewGame 首次出生在 Site：StartLocation Local → LocalToWorld → 建立真实 Canonical。</summary>
        BootstrapFromAuthoredLocal = 1,
        /// <summary>已有可信 Canonical（Wilderness→Site 进入 / WorldMap 重开 / 新格式 save）：World → WorldToLocal。</summary>
        ProjectCanonicalWorldToLocal = 2,
        /// <summary>Legacy save 无可信 Canonical：仅当 snapshot 提供 Local placement 时 Local → LocalToWorld 一次性 bootstrap。</summary>
        LegacyRestoreLocal = 3,
    }

    /// <summary>
    /// Phase 5R-B3B.3：Site Materialization ownership 的正式 provenance 解析（纯函数，零依赖，可测）。\n
    /// 取代旧 Host 判定「会话内第一次 Site 展开 = NewGame bootstrap」（_siteInitialBootstrapDone）——\n
    /// 该判定在起点为 Wilderness 时错误把首次 Wilderness→Site 进入识别为 bootstrap，导致 StartLocation\n
    /// 覆盖 Canonical BoundaryContact WorldPosition。\n
    /// 正式语义：\n
    ///  - <see cref=\"PlayerPartySiteMaterializeMode.BootstrapFromAuthoredLocal\"/> 只允许 NewGame 启动且\n
    ///    初始 Context 本来就在 WorldSite（由启动链记录 initialBootstrapSiteId）的<b>首次</b> Site 展开，\n
    ///    消费一次后永久失效；\n
    ///  - 其余（Wilderness→Site 进入 / Site reopen / 新格式 restore，已有可信 Canonical）一律\n
    ///    <see cref=\"PlayerPartySiteMaterializeMode.ProjectCanonicalWorldToLocal\"/>，即使这是第一次进入该 Site；\n
    ///  - snapshot restore 优先 <see cref=\"PlayerPartySiteMaterializeMode.LegacyRestoreLocal\"/>。\n
    /// transient provenance：不落盘、不进入 <see cref=\"PlayerPartyWorldMotion\"/>、非长期位置真源。\n
    /// </summary>
    public static class SiteMaterializeModeResolver
    {
        public static PlayerPartySiteMaterializeMode Resolve(
            bool isSnapshotRestore,
            string initialBootstrapSiteId,
            string currentSiteId,
            bool bootstrapConsumed,
            out bool consumeBootstrapNow)
        {
            consumeBootstrapNow = false;
            if (isSnapshotRestore)
                return PlayerPartySiteMaterializeMode.LegacyRestoreLocal;
            if (!bootstrapConsumed &&
                !string.IsNullOrEmpty(initialBootstrapSiteId) &&
                string.Equals(
                    initialBootstrapSiteId, currentSiteId ?? string.Empty, StringComparison.Ordinal))
            {
                consumeBootstrapNow = true;
                return PlayerPartySiteMaterializeMode.BootstrapFromAuthoredLocal;
            }

            return PlayerPartySiteMaterializeMode.ProjectCanonicalWorldToLocal;
        }

        /// <summary>
        /// Phase 5R-B3C1：NewGame 初始 Site 的 Bootstrap 是否应在 <b>启动时</b>立即执行
        /// （startup orchestration 判定，与 <see cref="Resolve"/> 同源：pending && initial==current && AtWorldSite）。
        /// 让初始 LocalMap 第一次真正建立时就完成 AuthoredLocal → Canonical，而不是等玩家离开再回来。
        /// 纯函数，可测。
        /// </summary>
        public static bool ShouldRunStartupBootstrap(
            bool pending,
            string initialBootstrapSiteId,
            string currentSiteId,
            bool atWorldSite)
        {
            if (!pending)
                return false;
            if (string.IsNullOrEmpty(initialBootstrapSiteId))
                return false;
            if (!atWorldSite)
                return false;
            return string.Equals(initialBootstrapSiteId, currentSiteId ?? string.Empty, StringComparison.Ordinal);
        }

        /// <summary>
        /// Phase 5R-B3C1：初始 Site Bootstrap 的 token 消费决策 —— <b>成功才消费</b>（
        /// <see cref="PlayerPartyLocalMapMaterializationService.MaterializePartyOnResolvedLocalMap"/> 返回
        /// Success 才算真正完成 AuthoredLocal→Canonical）。失败 → 不消费（保留 pending，下次正确执行）。
        /// 纯函数，可测。
        /// </summary>
        public static bool ShouldConsumeBootstrap(bool consumeBootstrapNow, bool bootstrapSucceeded) =>
            consumeBootstrapNow && bootstrapSucceeded;

        /// <summary>
        /// Phase 5R-B3C1：PlayerParty 已经过 Materialization 后，该 Materialization 输出就是本次
        /// LocalMap 的唯一 placement authority；Host 不得再执行 legacy StartLocation snap
        /// （SetPresentationOverride(StartLocation) 覆盖）。返回 true = 允许执行 legacy StartLocation
        /// snap（仅无 PlayerParty Materialize 的 legacy 分支）。纯函数，可测。
        /// </summary>
        public static bool ShouldApplyLegacyStartLocationSnap(
            bool playerPartyMaterialized,
            bool skipStartSnapForWilderness,
            bool skipStartSnapForSavedPlacements)
        {
            if (playerPartyMaterialized)
                return false;
            if (skipStartSnapForWilderness)
                return false;
            if (skipStartSnapForSavedPlacements)
                return false;
            return true;
        }
    }
}
