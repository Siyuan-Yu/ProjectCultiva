using System.Collections.Generic;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>FormalArmy Hex 战略旅行计划（无 RouteId / Node 语义）。</summary>
    public sealed class HexTravelPlan
    {
        public string ArmyId { get; set; } = string.Empty;
        public HexCoord DestinationHex { get; set; }
        public List<HexCoord> Path { get; } = new List<HexCoord>(32);
        public int NextStepIndex { get; set; }

        public bool HasPath => Path.Count > 0;

        public bool TryGetNextStep(out HexCoord step)
        {
            step = default;
            if (NextStepIndex < 0 || NextStepIndex >= Path.Count)
                return false;
            step = Path[NextStepIndex];
            return true;
        }
    }
}
