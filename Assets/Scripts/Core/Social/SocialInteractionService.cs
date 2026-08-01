using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Social
{
    /// <summary>
    /// VS0.5 Phase C: thin social intents that only write through RelationshipService.
    /// </summary>
    public sealed class SocialInteractionService
    {
        readonly RelationshipService _relationships;

        public SocialInteractionService(RelationshipService relationships = null)
        {
            _relationships = relationships ?? new RelationshipService();
        }

        public Result Help(SimulationWorld world, EntityId actor, EntityId target) =>
            _relationships.Record(
                world,
                actor,
                target,
                SocialAlphaConstants.HelpDelta,
                SocialAlphaConstants.ReasonHelp);

        public Result Slight(SimulationWorld world, EntityId actor, EntityId target) =>
            _relationships.Record(
                world,
                actor,
                target,
                SocialAlphaConstants.SlightDelta,
                SocialAlphaConstants.ReasonSlight);
    }
}
