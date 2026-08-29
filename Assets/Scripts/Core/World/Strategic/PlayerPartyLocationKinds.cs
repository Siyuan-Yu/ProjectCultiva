namespace XianXia.Core.World.Strategic
{
    /// <summary>PlayerParty 世界位置种类（与 MovementState 分离）。</summary>
    public enum PlayerPartyLocationKind
    {
        /// <summary>位于 WorldSite LocalMap；世界投影 = PresenceHex。</summary>
        AtWorldSite = 0,
        /// <summary>位于普通 Hex 连续开世界；真源 = Continuous WorldPosition。</summary>
        AtWorldPosition = 1,
    }

    /// <summary>PlayerParty 移动状态（不回答“在哪里”）。</summary>
    public enum PlayerPartyMovementKind
    {
        Idle = 0,
        AutoTravel = 1,
    }

    /// <summary>
    /// 当前由谁推进 PlayerParty AutoTravel（Phase 5B View Takeover）。
    /// 不保存第二份 Path / Progress / WorldPosition——那些仍在 PlayerPartyWorldMotion。
    /// </summary>
    public enum PlayerPartyTravelExecutionMode
    {
        /// <summary>无进行中的 AutoTravel 执行权（Idle）。</summary>
        None = 0,
        /// <summary>World Tick：StrategicTravelDriver / AdvanceDistanceBudget 可推进。</summary>
        World = 1,
        /// <summary>近景可见：World Advance 必须跳过该 PlayerParty。</summary>
        LocalVisible = 2,
    }
}
