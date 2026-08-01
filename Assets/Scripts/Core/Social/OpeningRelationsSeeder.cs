using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Social
{
    /// <summary>
    /// Seeds mutual opening companion favor among starting characters (Alpha constants).
    /// </summary>
    public static class OpeningRelationsSeeder
    {
        public static Result SeedCompanions(
            SimulationWorld world,
            IReadOnlyList<EntityId> characterIds,
            RelationshipService service = null)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (characterIds == null || characterIds.Count < 2)
                return Result.Success();

            service = service ?? new RelationshipService();
            for (var i = 0; i < characterIds.Count; i++)
            {
                for (var j = i + 1; j < characterIds.Count; j++)
                {
                    var a = characterIds[i];
                    var b = characterIds[j];
                    var ab = service.Record(
                        world,
                        a,
                        b,
                        SocialAlphaConstants.OpeningCompanionFavor,
                        SocialAlphaConstants.ReasonOpeningCompanion);
                    if (ab.IsFailure)
                        return ab;

                    var ba = service.Record(
                        world,
                        b,
                        a,
                        SocialAlphaConstants.OpeningCompanionFavor,
                        SocialAlphaConstants.ReasonOpeningCompanion);
                    if (ba.IsFailure)
                        return ba;
                }
            }

            return Result.Success();
        }
    }
}
