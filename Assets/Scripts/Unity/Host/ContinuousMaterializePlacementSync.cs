namespace XianXia.Unity.Host
{
    /// <summary>
    /// Continuous Outdoor 呈现落点收口策略。
    ///
    /// 语义：materialize 写入的 <c>EntityLocationComponent.PresentationOverride</c> 是「该实体在本
    /// loaded scope 内的呈现位置」唯一真源。但 view 可能在 Continuous activation <b>之前</b> 就已被
    /// <c>EntityViewSpawner.Rebuild</c> 创建 —— 那时还没有 materialize，<c>ResolvePresentationPosition</c>
    /// 只能退回 legacy <c>WorldRegion</c> 地点 presentation + stack 偏移；而
    /// <c>SpawnMissingVisibleViews</c> 只补「缺失」view、<b>绝不搬动已存在的 view</b>。
    ///
    /// 症状签名（生产实测）：<c>Expected=17 Materialized=17 Views=17</c> 全部成立，但 16 个 view 停在
    /// presentation 原点附近的 legacy 回退位置（间距恰好是 0.85 的 stack 网格），镜头外 → 「人口都在、
    /// 但看不见人」；<c>NpcPathReq/sec≈9</c> 与 <c>Move retry budget exhausted</c> 同时出现。
    ///
    /// 该策略决定「已存在的 view 是否必须重新对齐到权威落点」，以及在哪些情况下必须让位：
    /// <list type="bullet">
    /// <item>PlayerParty 成员由 <c>AlignPartyPresentationToWorld</c> 负责，不在此处搬动。</item>
    /// <item>正在被 movement 驱动的实体由其自身写位（不得被 authored anchor 重置）。</item>
    /// <item>没有权威落点的实体不猜位置（保持既有 fallback 语义）。</item>
    /// </list>
    /// 纯函数，不依赖任何 Unity 对象，便于无头测试。
    /// </summary>
    public static class ContinuousMaterializePlacementSync
    {
        /// <summary>presentation 单位的对齐容差：低于此距离视为已对齐，避免无意义写入。</summary>
        public const float PositionEpsilon = 0.01f;

        public static bool ShouldRealign(
            bool hasView,
            bool isPartyMember,
            bool isMoving,
            bool hasAuthoritativePlacement,
            float viewPresentationX,
            float viewPresentationY,
            float targetPresentationX,
            float targetPresentationY)
        {
            if (!hasView)
                return false;
            if (isPartyMember)
                return false;
            if (isMoving)
                return false;
            if (!hasAuthoritativePlacement)
                return false;

            var dx = targetPresentationX - viewPresentationX;
            var dy = targetPresentationY - viewPresentationY;
            return dx * dx + dy * dy > PositionEpsilon * PositionEpsilon;
        }
    }
}
