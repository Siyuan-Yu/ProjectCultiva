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

            return new HostileActionRouteResult(
                HostileActionRoute.StrategicMilitaryEscalation,
                classification,
                !WarGateService.CanAttack(world, attackerFaction, classification.TargetFactionId),
                string.Empty);
        }

        public static bool CanInitiatePlayerHostileAction(SimulationWorld world, PlayerPartyRuntime party, EntityId attackerId, EntityId targetId) =>
            Route(world, party, attackerId, targetId).Route != HostileActionRoute.Reject;
    }

    public enum StrategicMilitaryAggressionConsequence
    {
        None,
        DeclareWar,
        ReleaseOwnVassalageThenDeclareWar,
        ReleaseTargetVassalageThenDeclareWar,
        LeaveAllianceThenDeclareWar
    }

    public readonly struct StrategicMilitaryAggressionPreview
    {
        public StrategicMilitaryAggressionPreview(
            string attackerFactionId,
            string defenderFactionId,
            FactionDiplomacyRelation relation,
            StrategicMilitaryAggressionConsequence consequence,
            string description)
        {
            AttackerFactionId = attackerFactionId ?? string.Empty;
            DefenderFactionId = defenderFactionId ?? string.Empty;
            Relation = relation;
            Consequence = consequence;
            Description = description ?? string.Empty;
        }
        public string AttackerFactionId { get; }
        public string DefenderFactionId { get; }
        public FactionDiplomacyRelation Relation { get; }
        public StrategicMilitaryAggressionConsequence Consequence { get; }
        public string Description { get; }
        public bool RequiresConfirmation => Consequence != StrategicMilitaryAggressionConsequence.None;
    }

    /// <summary>
    /// 玩家确认一次战略军事侵略后唯一允许改变外交关系的入口。
    /// Host 只展示 Preview，不得自行拼接联盟、附庸和宣战规则。
    /// </summary>
    public static class StrategicMilitaryAggressionService
    {
        public static bool TryPreview(
            SimulationWorld world,
            string attackerFactionId,
            string defenderFactionId,
            out StrategicMilitaryAggressionPreview preview,
            out string reason)
        {
            reason = string.Empty;
            preview = default;
            var valid = WarGateService.CanDeclareWar(world, attackerFactionId, defenderFactionId);
            if (valid.IsFailure)
            {
                reason = valid.Error.Message;
                return false;
            }

            var relation = FactionDiplomacyRelationQuery.GetRelation(world, attackerFactionId, defenderFactionId);
            if (relation == FactionDiplomacyRelation.Self)
            {
                reason = "Cannot initiate strategic military aggression against own faction.";
                return false;
            }

            var consequence = StrategicMilitaryAggressionConsequence.DeclareWar;
            var description = "继续攻击将视为向该势力宣战。";
            if (relation == FactionDiplomacyRelation.War)
            {
                consequence = StrategicMilitaryAggressionConsequence.None;
                description = "双方已经处于战争状态。";
            }
            else if (world.Strategic.Vassalages.TryGetOverlord(attackerFactionId, out _))
            {
                consequence = StrategicMilitaryAggressionConsequence.ReleaseOwnVassalageThenDeclareWar;
                description = "继续攻击将脱离附庸，并向该势力宣战。";
            }
            else if (world.Strategic.Vassalages.TryGetOverlord(defenderFactionId, out var targetOverlord) &&
                     string.Equals(targetOverlord, attackerFactionId, StringComparison.Ordinal))
            {
                consequence = StrategicMilitaryAggressionConsequence.ReleaseTargetVassalageThenDeclareWar;
                description = "继续攻击将解除该附庸关系，并向该势力宣战。";
            }
            else if (relation == FactionDiplomacyRelation.Alliance)
            {
                consequence = StrategicMilitaryAggressionConsequence.LeaveAllianceThenDeclareWar;
                description = "继续攻击将退出当前联盟，并向该势力宣战。";
            }

            preview = new StrategicMilitaryAggressionPreview(
                attackerFactionId, defenderFactionId, relation, consequence, description);
            return true;
        }

        public static bool TryCommit(
            SimulationWorld world,
            string attackerFactionId,
            string defenderFactionId,
            out string reason)
        {
            reason = string.Empty;
            if (!TryPreview(world, attackerFactionId, defenderFactionId, out var preview, out reason))
                return false;
            if (preview.Consequence == StrategicMilitaryAggressionConsequence.None)
                return true;

            var vassalages = world.Strategic.Vassalages;
            var alliances = world.Strategic.Alliances;
            var releasedVassal = string.Empty;
            var releasedOverlord = string.Empty;
            var previousAllianceId = string.Empty;
            System.Collections.Generic.List<string> previousAllianceMembers = null;

            if (preview.Consequence == StrategicMilitaryAggressionConsequence.ReleaseOwnVassalageThenDeclareWar)
            {
                if (!vassalages.TryGetOverlord(attackerFactionId, out releasedOverlord) ||
                    !vassalages.TryReleaseVassalage(attackerFactionId, releasedOverlord))
                {
                    reason = "解除附庸关系失败。";
                    return false;
                }
                releasedVassal = attackerFactionId;
            }
            else if (preview.Consequence == StrategicMilitaryAggressionConsequence.ReleaseTargetVassalageThenDeclareWar)
            {
                if (!vassalages.TryGetOverlord(defenderFactionId, out releasedOverlord) ||
                    !vassalages.TryReleaseVassalage(defenderFactionId, releasedOverlord))
                {
                    reason = "解除附庸关系失败。";
                    return false;
                }
                releasedVassal = defenderFactionId;
            }
            else if (preview.Consequence == StrategicMilitaryAggressionConsequence.LeaveAllianceThenDeclareWar)
            {
                if (!alliances.TryLeaveAlliance(attackerFactionId, out previousAllianceId, out previousAllianceMembers))
                {
                    reason = "退出联盟失败。";
                    return false;
                }
            }

            var result = WarGateService.DeclareWar(world, attackerFactionId, defenderFactionId);
            if (result.IsSuccess)
            {
                Ch01ScenarioProgressionHooks.OnStrategicMilitaryAggressionCommitted(
                    world, attackerFactionId, defenderFactionId);
                return true;
            }

            if (!string.IsNullOrEmpty(releasedVassal))
                vassalages.TryBindVassalage(releasedVassal, releasedOverlord);
            if (!string.IsNullOrEmpty(previousAllianceId) && previousAllianceMembers != null)
                alliances.RestoreAlliance(previousAllianceId, previousAllianceMembers);
            reason = result.Error.Message;
            return false;
        }

        /// <summary>旧调用点兼容别名；新代码应使用 TryPreview + TryCommit。</summary>
        public static bool TryEscalateToWar(SimulationWorld world, string attackerFactionId, string defenderFactionId, out string reason) =>
            TryCommit(world, attackerFactionId, defenderFactionId, out reason);
    }
}
