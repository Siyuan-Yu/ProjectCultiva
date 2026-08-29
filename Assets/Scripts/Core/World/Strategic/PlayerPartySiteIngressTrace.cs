using System;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 5R-B3B.3/.4（B3C1.1 精简后）：Site ingress 结构化诊断（只针对正式 Wilderness→Site 进入路径）。
    /// 输出节点（同一 ingress 同一 trace id）：
    ///  [1 IngressBoundary]（BeginIngress：EnterWorldSiteAsParty 入口，含 boundary/fromHex/footprintHex）
    ///  [2 AtSiteCommit]（Context-preserving AtSite 提交后）
    ///  [3 MaterializeDecision]（Host 侧 Materialization ownership 与决策输入 provenance）
    ///  [4 WorldToLocal]（canonical → Local 映射成功/失败，含 domain/u,v/bounds/startLoc 数值）
    ///  [IngressAborted] / [BootstrapFailed] / [BootstrapTokenKept] / [StartupBootstrapFailed]（failure）
    ///  [BootstrapTokenConsumed] / [StartupBootstrapCommitted]（token 状态变化）
    /// 一次性 writer 追踪（PresentationWrite / FinalCharacterPosition / MaterializeMode 等）已随
    /// B3C1.1 清理，不再输出。
    /// Sink 未订阅时零成本；只打状态变化（key 去重），不刷每帧。
    /// 目标是人工跑一次就能看出：Canonical Boundary WorldPosition 在哪一步丢失 / 被 StartLocation 覆盖。
    /// </summary>
    public static class PlayerPartySiteIngressTrace
    {
        /// <summary>订阅后接收一次性 Site ingress 诊断行；默认 null = 零成本。
        /// Host 层（PlayableHostBootstrap.Awake）订阅到 UnityEngine.Debug.Log，LevelTester 在 Unity
        /// Console 可见。</summary>
        public static Action<string> Sink;

        /// <summary>当前 ingress trace id：BeginIngress 生成，Materialize 完成后 EndIngress 清空。
        /// 跨 EnterWorldSiteAsParty（Core）→ Host 展开 → Materialize（Core）存活；非 ingress 路径为空。</summary>
        public static string ActiveIngressId { get; private set; } = string.Empty;

        static int _seq;
        static string _lastKey = string.Empty;

        /// <summary>开始一次正式 Wilderness→Site ingress：生成 trace id 并输出 [1 IngressBoundary]。
        /// 返回值 = ActiveIngressId（供后续节点对照）。不改变任何行为。</summary>
        public static string BeginIngress(
            string siteId,
            WorldVec2 boundaryWorld,
            HexCoord fromHex,
            HexCoord ingressFootprintHex)
        {
            ActiveIngressId = "ing#" + (++_seq);
            Log(
                "IngressBoundary",
                "id=" + ActiveIngressId +
                " site=" + siteId +
                " boundary=" + boundaryWorld +
                " fromHex=" + fromHex +
                " ingressFootprintHex=" + ingressFootprintHex);
            return ActiveIngressId;
        }

        /// <summary>Materialize 完成后清空 trace id（下一次 ingress 重新生成）。
        /// 不改变任何行为。</summary>
        public static void EndIngress() => ActiveIngressId = string.Empty;

        public static void Log(string reason, string payload)
        {
            if (Sink == null)
                return;
            var key = reason + "|" + payload;
            if (key == _lastKey)
                return;
            _lastKey = key;
            var idTag = string.IsNullOrEmpty(ActiveIngressId) ? string.Empty : " #" + ActiveIngressId;
            Sink("[SiteIngressTrace" + idTag + "] " + reason + " " + payload);
        }

        /// <summary>LevelTester 复验后清空去重缓存与当前 ingress id（新会话重新诊断）。</summary>
        public static void ResetDedupe()
        {
            _lastKey = string.Empty;
            ActiveIngressId = string.Empty;
        }
    }
}
