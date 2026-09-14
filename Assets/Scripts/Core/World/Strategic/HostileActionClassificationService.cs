using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Identity scope for a newly initiated hostile action; this does not decide relations or diplomacy.</summary>
    public enum HostileActionScope
    {
        LocalCharacter = 0,
        StrategicMilitary = 1
    }

    /// <summary>
    /// Current Character target identity for hostile-action routing. FormalArmy membership is legacy
    /// diagnostics metadata only: every Character remains LocalCharacter and personal hostility never declares war.
    /// </summary>
    public readonly struct HostileActionClassification
    {
        public HostileActionClassification(EntityId targetEntityId, HostileActionScope scope, string targetFactionId, string targetFormalArmyId)
        {
            TargetEntityId = targetEntityId;
            Scope = scope;
            TargetFactionId = targetFactionId ?? string.Empty;
            TargetFormalArmyId = targetFormalArmyId ?? string.Empty;
        }

        public EntityId TargetEntityId { get; }
        public HostileActionScope Scope { get; }
        public string TargetFactionId { get; }
        public string TargetFormalArmyId { get; }
    }

    public static class HostileActionClassificationService
    {
        public static bool TryClassifyTarget(SimulationWorld world, EntityId targetId, out HostileActionClassification classification, out string reason)
        {
            classification = default;
            reason = string.Empty;
            if (world?.Entities == null || targetId.IsNone)
            {
                reason = "Target is required";
                return false;
            }

            if (!world.Entities.TryGet(targetId, out var target) || target == null)
            {
                reason = "Target entity not found";
                return false;
            }

            // Legacy membership is validated as metadata only. It must never change a Character into
            // StrategicMilitary or select a different battle/participant path.
            if (ArmyService.TryGetArmyForCharacter(world, targetId, out var formalArmy))
            {
                if (target.TryGet<FactionMembershipComponent>(out var membership) &&
                    !string.IsNullOrEmpty(membership.FactionId) &&
                    !string.Equals(membership.FactionId, formalArmy.FactionId, StringComparison.Ordinal))
                {
                    reason = "FormalArmy member faction mismatch";
                    return false;
                }

                classification = new HostileActionClassification(targetId, HostileActionScope.LocalCharacter, formalArmy.FactionId, formalArmy.ArmyId);
                return true;
            }

            var factionId = target.TryGet<FactionMembershipComponent>(out var localMembership) && localMembership.IsAffiliated
                ? localMembership.FactionId
                : string.Empty;
            classification = new HostileActionClassification(targetId, HostileActionScope.LocalCharacter, factionId, string.Empty);
            return true;
        }

    }
}
