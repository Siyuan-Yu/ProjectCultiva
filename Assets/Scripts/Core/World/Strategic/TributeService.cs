using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Phase I：Tribute 最小结算 hook（数值公式 DEFER）。</summary>
    public static class TributeService
    {
        public static int PlaceholderTributeAmount { get; set; } = 10;

        public static Result TryCollectTribute(
            SimulationWorld world,
            string payerFactionId,
            string receiverFactionId,
            out int amount)
        {
            amount = 0;
            if (world == null ||
                string.IsNullOrEmpty(payerFactionId) ||
                string.IsNullOrEmpty(receiverFactionId))
            {
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid tribute request.");
            }

            amount = PlaceholderTributeAmount;
            return Result.Success();
        }
    }
}
