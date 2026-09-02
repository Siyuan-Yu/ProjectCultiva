using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    public enum HostileActionRoute { LocalCombat, StrategicMilitaryEscalation, Reject }

    public readonly struct HostileActionRouteResult
    {
        public HostileActionRouteResult(HostileActionRoute route, HostileActionClassification target, bool requiresWarDeclaration, string failureReason)
        {
            Route = route;
            TargetEntityId = target.TargetEntityId;
            TargetFactionId = target.TargetFactionId;
            TargetFormalArmyId = target.TargetFormalArmyId;
            RequiresWarDeclaration = requiresWarDeclaration;
            FailureReason = failureReason ?? string.Empty;
        }
        public HostileActionRoute Route { get; }
        public EntityId TargetEntityId { get; }
        public string TargetFactionId { get; }
        public string TargetFormalArmyId { get; }
        public bool RequiresWarDeclaration { get; }
        public string FailureReason { get; }
    }

    /// <summary>Pre-damage target routing only. It never applies damage, opens UI, or commits a battle.</summary>
    public static class LocalHostileActionRoutingService
    {
        public static HostileActionRouteResult Route(SimulationWorld world, PlayerPartyRuntime party, EntityId attackerId, EntityId targetId)
        {
            var empty = new HostileActionClassification(targetId, HostileActionScope.LocalCharacter, string.Empty, string.Empty);
            if (world == null || party == null || !party.HasActive || attackerId.IsNone || targetId.IsNone)
                return new HostileActionRouteResult(HostileActionRoute.Reject, empty, false, "PlayerParty attacker and target are required.");
            if (!world.Entities.TryGet(targetId, out var target) || target == null)
                return new HostileActionRouteResult(HostileActionRoute.Reject, empty, false, "Target entity not found.");

            // Active WORLD_COMBAT owns its participant set; never create another engagement from a tactical strike.
            if (StrategicEncounterHostilityService.IsHostileStrategicNpc(world, target))
                return new HostileActionRouteResult(HostileActionRoute.LocalCombat, empty, false, string.Empty);

            if (!HostileActionClassificationService.TryClassifyTarget(world, targetId, out var classification, out var reason))
                return new HostileActionRouteResult(HostileActionRoute.Reject, empty, false, reason);
            if (classification.Scope == HostileActionScope.LocalCharacter)
                return new HostileActionRouteResult(HostileActionRoute.LocalCombat, classification, false, string.Empty);

            var attackerFaction = world.Strategic?.PlayerFactionId;
            if (string.IsNullOrEmpty(attackerFaction))
                attackerFaction = StrategicFactionCatalog.PlayerFactionId;
            if (string.Equals(attackerFaction, classification.TargetFactionId, StringComparison.Ordinal))
                return new HostileActionRouteResult(HostileActionRoute.Reject, classification, false, "Cannot initiate strategic military aggression against own faction.");

            var stance = world.Strategic?.Diplomacy?.GetStance(attackerFaction, classification.TargetFactionId) ?? FactionStance.Neutral;
            if (stance == FactionStance.Friendly)
                return new HostileActionRouteResult(HostileActionRoute.Reject, classification, false, "Cannot initiate strategic military aggression against a friendly faction.");

            return new HostileActionRouteResult(
                HostileActionRoute.StrategicMilitaryEscalation,
                classification,
                !WarGateService.CanAttack(world, attackerFaction, classification.TargetFactionId),
                string.Empty);
        }

        public static bool CanInitiatePlayerHostileAction(SimulationWorld world, PlayerPartyRuntime party, EntityId attackerId, EntityId targetId) =>
            Route(world, party, attackerId, targetId).Route != HostileActionRoute.Reject;
    }

    /// <summary>Single military-aggression to war authority. Diplomacy mutation remains in WarGateService.</summary>
    public static class StrategicMilitaryAggressionService
    {
        public static bool TryEscalateToWar(SimulationWorld world, string attackerFactionId, string defenderFactionId, out string reason)
        {
            reason = string.Empty;
            var result = WarGateService.DeclareWar(world, attackerFactionId, defenderFactionId);
            if (result.IsSuccess)
                return true;
            reason = result.Error.Message;
            return false;
        }
    }
}
