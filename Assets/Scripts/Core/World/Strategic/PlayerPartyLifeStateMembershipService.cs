using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Reconciles the player controlled squad after a member stops being able to fight.
    /// A living successor remains the control anchor. Incapacitated members stay in the Party so
    /// a later full-party emergency handoff can preserve the defeated group as one recovery squad;
    /// Host movement already skips them. Dead/Removed members keep the existing singleton/corpse
    /// retirement when another Active remains. If nobody can act, membership is retained until an
    /// external handoff succeeds or a member recovers.
    /// </summary>
    public static class PlayerPartyLifeStateMembershipService
    {
        public static void ReconcilePlayerPartyAfterLifeStateChange(SimulationWorld world)
        {
            var party = world?.Strategic?.PlayerPartyContext;
            if (party == null)
                return;

            var members = new List<EntityId>(party.Members);
            var hasLivingMember = false;
            for (var i = 0; i < members.Count; i++)
            {
                if (world.Entities.TryGet(members[i], out var entity) && entity != null &&
                    CombatLifeStateService.CanFight(entity))
                {
                    hasLivingMember = true;
                    break;
                }
            }

            party.RefreshActiveAfterLifeState(world);
            // Active combat can temporarily restrict external handoff. Keep the logical control squad
            // intact until an eligible successor can be selected, while all physical gates still
            // reject the non-fighting members immediately.
            if (!hasLivingMember || !party.HasActive)
            {
                PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
                return;
            }

            for (var i = 0; i < members.Count; i++)
            {
                var id = members[i];
                if (!party.IsMember(id) || id == party.ActiveCharacterId)
                    continue;
                if (!world.Entities.TryGet(id, out var entity) || entity == null ||
                    !entity.TryGet<LifecycleComponent>(out var life) || life == null ||
                    (life.State != LifecycleState.Dead &&
                     life.State != LifecycleState.Removed))
                    continue;

                // A battle lock may reject this transfer. Repeated calls at Host, transition and
                // restore boundaries make the operation eventually complete and idempotent.
                SquadMembershipService.LeaveToSingleton(world, id);
            }

            party.RefreshActiveAfterLifeState(world);
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
        }
    }
}
