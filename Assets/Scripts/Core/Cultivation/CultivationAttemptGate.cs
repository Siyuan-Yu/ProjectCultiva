using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Cultivation
{
    /// <summary>
    /// 坐下修炼前校验。功法须经秘籍／任务／面板显式学习；不从机缘点静默保底。
    /// </summary>
    public sealed class CultivationAttemptGate
    {
        public Result Prepare(SimulationWorld world, EntityId subject)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World is null.");
            if (!world.Entities.TryGet(subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.");
            if (!entity.TryGet<CultivationComponent>(out _))
                return Result.Failure(ErrorCode.ComponentMissing, "CultivationComponent missing.");

            // 基础吐纳随时可坐；青云诀等须背包秘籍／将老／洞府流程学会。
            return Result.Success();
        }
    }
}
