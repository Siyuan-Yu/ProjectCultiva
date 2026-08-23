using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>FormalArmy 战略 Hex 位置真源视图（CurrentHex + 相邻格 MoveProgress）。</summary>
    public readonly struct ArmyHexPosition
    {
        public ArmyHexPosition(FormalArmy army)
        {
            if (army == null || !army.UsesHexStrategicPosition)
            {
                CurrentHex = default;
                MoveProgress = 0f;
                HasNextHex = false;
                NextHex = default;
                State = FormalArmyState.Idle;
                return;
            }

            CurrentHex = army.CurrentHex;
            MoveProgress = army.MoveProgress;
            HasNextHex = army.TryGetNextHex(out var next);
            NextHex = next;
            State = army.State;
        }

        public HexCoord CurrentHex { get; }
        public bool HasNextHex { get; }
        public HexCoord NextHex { get; }
        public float MoveProgress { get; }
        public FormalArmyState State { get; }
    }
}
