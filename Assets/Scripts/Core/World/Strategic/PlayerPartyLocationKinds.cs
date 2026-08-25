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
}
