using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Opportunity;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Cultivation
{
    /// <summary>
    /// 坐下修炼：随时可入定。若已知「可修炼」机缘点且未学功法，顺带学点上功法。
    /// </summary>
    public sealed class CultivationAttemptGate
    {
        readonly CultivationService _cultivation = new CultivationService();

        public Result Prepare(SimulationWorld world, EntityId subject)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "World is null.");
            if (!world.Entities.TryGet(subject, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Subject missing.");
            if (!entity.TryGet<CultivationComponent>(out var cultivation))
                return Result.Failure(ErrorCode.ComponentMissing, "CultivationComponent missing.");

            // 基础吐纳随时可坐；功法仍优先从已知修炼点领取。
            if (cultivation.HasLearnedManual)
                return Result.Success();

            if (!entity.TryGet<KnownSitesComponent>(out var known))
                return Result.Success();

            OpportunitySite unlockSite = null;
            var knownIds = new List<string>(known.KnownIds);
            knownIds.Sort(System.StringComparer.Ordinal);
            foreach (var siteId in knownIds)
            {
                if (!DefinitionId.TryParse(siteId, out var id))
                    continue;
                if (!world.TryGetOpportunitySite(id, out var site) || !site.AllowsCultivation)
                    continue;
                unlockSite = site;
                break;
            }

            if (unlockSite == null || !unlockSite.OfferedManualId.HasValue)
                return Result.Success();

            if (!world.TryGetManual(unlockSite.OfferedManualId.Value, out var manual))
                return Result.Success();

            return _cultivation.LearnManual(world, subject, manual);
        }
    }
}
