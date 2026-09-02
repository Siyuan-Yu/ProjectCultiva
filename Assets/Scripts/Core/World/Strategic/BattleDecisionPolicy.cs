using System;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Manual / Auto / Retreat 资格单一真源（Phase 4）。</summary>
    public static class BattleDecisionPolicy
    {
        public static bool CanPlayerManuallyParticipate(PendingEngagementRuntime engagement) =>
            engagement != null && engagement.PlayerPartyIncluded;

        public static bool CanPlayerManuallyParticipate(SimulationWorld world) =>
            CanPlayerManuallyParticipate(world?.Strategic?.PendingEngagement);

        public static BattleDecisionOptions ResolveDecisionOptions(PendingEngagementRuntime engagement)
        {
            var options = new BattleDecisionOptions();
            if (engagement == null || !engagement.RequiresPlayerDecision)
                return options;

            options.PlayerDecisionSubjectKind = engagement.DecisionSubjectKind;
            options.PlayerDecisionSubjectFormalArmyId = engagement.DecisionSubjectFormalArmyId;
            options.Auto = true;
            options.Retreat = engagement.DecisionSubjectKind != BattleDecisionSubjectKind.None;
            options.Manual = CanPlayerManuallyParticipate(engagement);
            return options;
        }

        public static BattleDecisionOptions ResolveDecisionOptions(SimulationWorld world)
        {
            var options = ResolveDecisionOptions(world?.Strategic?.PendingEngagement);
            var offer = world?.Strategic?.BattleOffer;
            if (offer != null && offer.Origin == BattleOfferOrigin.LocalMapHostileAction)
                options.Auto = false;
            return options;
        }
    }
}
