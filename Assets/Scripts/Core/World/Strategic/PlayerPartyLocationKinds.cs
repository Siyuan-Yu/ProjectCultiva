namespace XianXia.Core.World.Strategic
{
    /// <summary>PlayerParty 世界位置种类（与 MovementState 分离）。</summary>
    public enum PlayerPartyLocationKind
    {
        /// <summary>位于 WorldSite LocalMap；世界投影 = LegacyPresenceHex。</summary>
        AtWorldSite = 0,
        /// <summary>位于 Continuous Surface；真源 = SurfaceId + exact WorldPosition。</summary>
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
        /// <summary>Legacy Hex World Tick executor。</summary>
        World = 1,
        /// <summary>Legacy Outdoor LocalMap 可见 executor。</summary>
        LocalVisible = 2,
        /// <summary>正常 Continuous Surface 可见执行器；精确 Surface route 由 Host 推进。</summary>
        SurfaceVisible = 3,
    }
}
