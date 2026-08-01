using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Opportunity;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Cultivation
{
    /// <summary>
    /// Secret-cultivation entry: requires a known AllowsCultivation site; may Learn offered manual.
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
            if (!entity.TryGet<KnownSitesComponent>(out var known))
                return Result.Failure(ErrorCode.ComponentMissing, "KnownSitesComponent missing.");
            if (!entity.TryGet<CultivationComponent>(out var cultivation))
                return Result.Failure(ErrorCode.ComponentMissing, "CultivationComponent missing.");

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

            if (unlockSite == null)
                return Result.Failure(ErrorCode.ActionCannotStart, "No known cultivation OpportunitySite.");

            if (cultivation.HasLearnedManual)
                return Result.Success();

            if (!unlockSite.OfferedManualId.HasValue)
                return Result.Failure(ErrorCode.ActionCannotStart, "Site offers no manual; cannot learn.");

            if (!world.TryGetManual(unlockSite.OfferedManualId.Value, out var manual))
                return Result.Failure(ErrorCode.InvalidOperation, "Offered manual not registered on world.");

            return _cultivation.LearnManual(world, subject, manual);
        }
    }
}
