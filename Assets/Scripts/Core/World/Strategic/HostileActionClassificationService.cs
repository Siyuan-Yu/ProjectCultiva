using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Identity scope for a newly initiated hostile action; this does not decide relations or diplomacy.</summary>
    public enum HostileActionScope
    {
        LocalCharacter = 0
    }

    /// <summary>
    /// Current Character target identity for hostile-action routing.
    /// </summary>
    public readonly struct HostileActionClassification
    {
        public HostileActionClassification(EntityId targetEntityId, HostileActionScope scope, string targetFactionId)
        {
            TargetEntityId = targetEntityId;
            Scope = scope;
            TargetFactionId = targetFactionId ?? string.Empty;
        }

        public EntityId TargetEntityId { get; }
        public HostileActionScope Scope { get; }
        public string TargetFactionId { get; }
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

            var factionId = target.TryGet<FactionMembershipComponent>(out var localMembership) && localMembership.IsAffiliated
                ? localMembership.FactionId
                : string.Empty;
            classification = new HostileActionClassification(targetId, HostileActionScope.LocalCharacter, factionId);
            return true;
        }

    }
}
