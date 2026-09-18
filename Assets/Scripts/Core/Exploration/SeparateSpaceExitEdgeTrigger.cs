namespace XianXia.Core.Exploration
{
    /// <summary>
    /// SPACE-01：Separate Space Exit 的 edge-trigger 状态机（防 spawn／Load 在出口内立刻离开）。
    /// Host 用 presentation 点测 inside；本类不依赖 Unity。
    /// </summary>
    public static class SeparateSpaceExitEdgeTrigger
    {
        public struct State
        {
            public string MapId;
            public bool WasInside;
            public bool Armed;
            public bool TransitionIssued;
        }

        /// <summary>进入／Load 后初始化：若当前已在 Exit 内则先 WaitForExit。</summary>
        public static void ResetForMap(ref State state, string mapId, bool currentlyInside)
        {
            state.MapId = mapId ?? string.Empty;
            state.WasInside = currentlyInside;
            state.Armed = !currentlyInside;
            state.TransitionIssued = false;
        }

        /// <summary>
        /// 每帧推进。返回诊断 action；shouldLeave=true 时由 Host 提交 Leave。
        /// </summary>
        public static string Tick(ref State state, bool currentlyInside, out bool shouldLeave)
        {
            shouldLeave = false;
            if (state.TransitionIssued)
                return "WaitTransition";

            if (!state.Armed)
            {
                if (!currentlyInside)
                {
                    state.Armed = true;
                    state.WasInside = false;
                    return "Armed";
                }

                state.WasInside = true;
                return "WaitForExit";
            }

            if (!state.WasInside && currentlyInside)
            {
                state.TransitionIssued = true;
                state.WasInside = true;
                shouldLeave = true;
                return "Leave";
            }

            state.WasInside = currentlyInside;
            return currentlyInside ? "InsideArmed" : "OutsideArmed";
        }
    }
}
