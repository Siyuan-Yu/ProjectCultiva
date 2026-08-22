using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Formal Army Domain 唯一写入口（Phase A）。
    /// 成员正向真源：<see cref="FormalArmy.MemberCharacterIds"/>；
    /// <see cref="ArmyMembershipComponent"/> 仅作反向索引。
    /// </summary>
    public static class ArmyService
    {
        public static Result<FormalArmy> CreateArmy(
            SimulationWorld world,
            string factionId,
            string nodeId,
            IReadOnlyList<EntityId> memberCharacterIds,
            EntityId? explicitLeaderId = null)
        {
            if (world?.Strategic?.FormalArmies == null)
                return Result.Fail<FormalArmy>(ErrorCode.InvalidArgument, "SimulationWorld incomplete.");
            if (string.IsNullOrEmpty(factionId))
                return Result.Fail<FormalArmy>(ErrorCode.InvalidArgument, "FactionId required.");
            if (string.IsNullOrEmpty(nodeId))
                return Result.Fail<FormalArmy>(ErrorCode.InvalidArgument, "NodeId required.");
            if (memberCharacterIds == null || memberCharacterIds.Count < 1)
                return Result.Fail<FormalArmy>(ErrorCode.InvalidArgument, "Army requires at least one member.");

            if (!TryValidateFriendlyNodeInternal(world, factionId, nodeId, out var nodeError))
                return Result.Fail<FormalArmy>(nodeError);

            var resolvedMembers = new List<EntityId>(memberCharacterIds.Count);
            for (var i = 0; i < memberCharacterIds.Count; i++)
            {
                var memberId = memberCharacterIds[i];
                if (memberId.IsNone)
                    return Result.Fail<FormalArmy>(ErrorCode.InvalidArgument, "Invalid member EntityId.");

                if (!TryValidateMemberForFormation(world, memberId, factionId, nodeId, out var memberError))
                    return Result.Fail<FormalArmy>(memberError);

                if (ContainsEntity(resolvedMembers, memberId))
                    continue;
                resolvedMembers.Add(memberId);
            }

            if (resolvedMembers.Count < 1)
                return Result.Fail<FormalArmy>(ErrorCode.InvalidArgument, "Army requires at least one member.");

            if (!TryResolveLeader(world, resolvedMembers, explicitLeaderId, out var leaderId, out var leaderError))
                return Result.Fail<FormalArmy>(leaderError);

            var memberValues = new List<ulong>(resolvedMembers.Count);
            for (var i = 0; i < resolvedMembers.Count; i++)
                memberValues.Add(resolvedMembers[i].Value);

            var armyId = world.Strategic.FormalArmies.AllocateArmyId();
            var army = new FormalArmy
            {
                ArmyId = armyId,
                FactionId = factionId,
                LeaderCharacterId = leaderId,
                NodeId = nodeId,
                State = FormalArmyState.AtNode
            };
            army.ReplaceMembers(memberValues);

            world.Strategic.FormalArmies.Register(army);
            SyncMembershipForArmy(world, army);
            return Result.Ok(army);
        }

        public static Result DisbandArmy(SimulationWorld world, string armyId)
        {
            if (world?.Strategic?.FormalArmies == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld incomplete.");
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return Result.Failure(ErrorCode.NotFound, "Army not found.", armyId);

            if (!TryValidateFriendlyNodeInternal(world, army.FactionId, army.NodeId, out var nodeError))
                return Result.Failure(nodeError);

            return ForceRemoveArmy(world, army);
        }

        public static Result GarrisonArmy(SimulationWorld world, string armyId)
        {
            if (world?.Strategic?.FormalArmies == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld incomplete.");
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return Result.Failure(ErrorCode.NotFound, "Army not found.", armyId);

            if (!TryValidateFriendlyNodeInternal(world, army.FactionId, army.NodeId, out var nodeError))
                return Result.Failure(nodeError);

            if (army.State == FormalArmyState.Garrisoned)
                return Result.Success();

            army.State = FormalArmyState.Garrisoned;
            return Result.Success();
        }

        /// <summary>Garrisoned → AtNode，以便战略移动／追击。</summary>
        public static Result MobilizeArmy(SimulationWorld world, string armyId)
        {
            if (world?.Strategic?.FormalArmies == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld incomplete.");
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return Result.Failure(ErrorCode.NotFound, "Army not found.", armyId);

            if (army.State == FormalArmyState.AtNode)
                return Result.Success();
            if (army.State != FormalArmyState.Garrisoned)
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "Only garrisoned army can mobilize.",
                    armyId);

            army.State = FormalArmyState.AtNode;
            return Result.Success();
        }

        public static Result AddMember(SimulationWorld world, string armyId, EntityId memberId)
        {
            if (world?.Strategic?.FormalArmies == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld incomplete.");
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return Result.Failure(ErrorCode.NotFound, "Army not found.", armyId);

            if (!TryValidateFriendlyNodeInternal(world, army.FactionId, army.NodeId, out var nodeError))
                return Result.Failure(nodeError);

            if (!TryValidateMemberForFormation(world, memberId, army.FactionId, army.NodeId, out var memberError))
                return Result.Failure(memberError);

            army.AddMember(memberId);
            SyncMembershipForArmy(world, army);
            return Result.Success();
        }

        /// <summary>
        /// 战后接战点：弥留／尸体脱离军团（不校验 FriendlyNode；最后一人则解散）。
        /// </summary>
        public static void DetachNonLivingMembersAtBattlefield(SimulationWorld world, FormalArmy army)
        {
            if (world == null || army == null)
                return;

            var detach = new List<EntityId>(army.MemberCharacterIds.Count);
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(army.MemberCharacterIds[i]);
                if (LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id))
                    continue;
                detach.Add(id);
            }

            for (var i = 0; i < detach.Count; i++)
                DetachMemberAtBattlefieldInternal(world, army, detach[i]);

            if (world.Strategic.FormalArmies.TryGet(army.ArmyId, out var stillThere) && stillThere != null)
                RefreshLeader(world, army.ArmyId);
        }

        static void DetachMemberAtBattlefieldInternal(
            SimulationWorld world,
            FormalArmy army,
            EntityId memberId)
        {
            if (army == null || memberId.IsNone || !army.ContainsMember(memberId))
                return;

            if (army.MemberCharacterIds.Count <= 1)
            {
                RemoveMemberInternal(world, army, memberId);
                ForceRemoveArmy(world, army);
                return;
            }

            RemoveMemberInternal(world, army, memberId);
        }

        public static Result RemoveMember(SimulationWorld world, string armyId, EntityId memberId)
        {
            if (world?.Strategic?.FormalArmies == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld incomplete.");
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return Result.Failure(ErrorCode.NotFound, "Army not found.", armyId);

            if (!TryValidateFriendlyNodeInternal(world, army.FactionId, army.NodeId, out var nodeError))
                return Result.Failure(nodeError);

            if (!army.ContainsMember(memberId))
                return Result.Failure(ErrorCode.NotFound, "Member not in army.", memberId.ToString());

            if (army.MemberCharacterIds.Count <= 1)
            {
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "Cannot remove last member; disband army instead.");
            }

            RemoveMemberInternal(world, army, memberId);
            if (!IsValidLeaderCandidate(world, army.LeaderCharacterId, army))
                return RefreshLeader(world, armyId);
            return Result.Success();
        }

        public static Result ChangeLeader(SimulationWorld world, string armyId, EntityId newLeaderId)
        {
            if (world?.Strategic?.FormalArmies == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld incomplete.");
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return Result.Failure(ErrorCode.NotFound, "Army not found.", armyId);

            if (!TryValidateFriendlyNodeInternal(world, army.FactionId, army.NodeId, out var nodeError))
                return Result.Failure(nodeError);

            if (!army.ContainsMember(newLeaderId))
                return Result.Failure(ErrorCode.InvalidOperation, "Leader must be a member.");

            if (!IsValidLeaderAtFormation(world, newLeaderId))
                return Result.Failure(ErrorCode.InvalidOperation, "Leader is not valid.");

            army.LeaderCharacterId = newLeaderId;
            return Result.Success();
        }

        public static void CollectResidentsAtNode(
            SimulationWorld world,
            string nodeId,
            string factionId,
            IReadOnlyList<EntityId> candidateCharacterIds,
            List<EntityId> ungroupedInto,
            List<FormalArmy> armiesAtNodeInto)
        {
            ungroupedInto?.Clear();
            armiesAtNodeInto?.Clear();
            if (world == null || ungroupedInto == null || armiesAtNodeInto == null)
                return;

            if (!ArmyFormationNodePolicy.IsFriendlyNodeForFaction(world, nodeId, factionId))
                return;

            if (candidateCharacterIds != null)
            {
                for (var i = 0; i < candidateCharacterIds.Count; i++)
                {
                    var id = candidateCharacterIds[i];
                    if (id.IsNone)
                        continue;
                    if (!string.Equals(ResolveCharacterFactionId(world, id), factionId, StringComparison.Ordinal))
                        continue;
                    if (!string.Equals(ResolveCharacterNodeId(world, id), nodeId, StringComparison.Ordinal))
                        continue;
                    if (TryGetArmyForCharacter(world, id, out _))
                        continue;
                    ungroupedInto.Add(id);
                }
            }

            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null)
                    continue;
                if (!string.Equals(army.FactionId, factionId, StringComparison.Ordinal))
                    continue;
                if (!string.Equals(army.NodeId, nodeId, StringComparison.Ordinal))
                    continue;
                armiesAtNodeInto.Add(army);
            }
        }

        /// <summary>Leader 失效后按成员顺序递补；无合法成员则强制清理 Army。</summary>
        public static Result RefreshLeader(SimulationWorld world, string armyId)
        {
            if (world?.Strategic?.FormalArmies == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld incomplete.");
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return Result.Failure(ErrorCode.NotFound, "Army not found.", armyId);

            if (IsValidLeaderCandidate(world, army.LeaderCharacterId, army))
                return Result.Success();

            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var candidate = new EntityId(army.MemberCharacterIds[i]);
                if (IsValidLeaderCandidate(world, candidate, army))
                {
                    army.LeaderCharacterId = candidate;
                    return Result.Success();
                }
            }

            return ForceRemoveArmy(world, army);
        }

        public static bool TryGetArmyForCharacter(SimulationWorld world, EntityId characterId, out FormalArmy army)
        {
            army = null;
            if (world?.Strategic?.FormalArmies == null || characterId.IsNone)
                return false;

            if (!world.Strategic.FormalArmies.TryGetArmyForCharacter(characterId.Value, out army) ||
                army == null)
                return false;

            if (!world.Entities.TryGet(characterId, out var entity) ||
                !entity.TryGet<ArmyMembershipComponent>(out var mem) ||
                !mem.IsInArmy ||
                !string.Equals(mem.ArmyId, army.ArmyId, StringComparison.Ordinal))
                return false;

            return true;
        }

        public static string ResolveCharacterFactionId(SimulationWorld world, EntityId characterId)
        {
            if (world?.Entities == null || characterId.IsNone ||
                !world.Entities.TryGet(characterId, out var entity))
                return string.Empty;

            if (!entity.TryGet<FactionMembershipComponent>(out var mem) || !mem.IsAffiliated)
                return string.Empty;
            return mem.FactionId ?? string.Empty;
        }

        public static string ResolveCharacterNodeId(SimulationWorld world, EntityId characterId)
        {
            if (world?.WorldPresence == null || characterId.IsNone)
                return string.Empty;

            if (!world.WorldPresence.TryGet(characterId, out var presence) || presence == null)
                return string.Empty;

            if (presence.Mode != PartyWorldPresenceMode.AtNode)
                return string.Empty;

            return presence.NodeId ?? string.Empty;
        }

        static Result ForceRemoveArmy(SimulationWorld world, FormalArmy army)
        {
            if (army == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Army is null.");
            ClearMembershipForArmy(world, army);
            world.Strategic.FormalArmies.Remove(army.ArmyId);
            return Result.Success();
        }

        static void RemoveMemberInternal(SimulationWorld world, FormalArmy army, EntityId memberId)
        {
            if (world.Entities.TryGet(memberId, out var entity) &&
                entity.TryGet<ArmyMembershipComponent>(out var mem))
                mem.ClearArmyId();
            army.RemoveMember(memberId);
        }

        static bool TryValidateMemberForFormation(
            SimulationWorld world,
            EntityId memberId,
            string factionId,
            string nodeId,
            out GameError error)
        {
            error = default;
            if (!world.Entities.TryGet(memberId, out var entity))
            {
                error = new GameError(ErrorCode.EntityNotFound, "Member not found.", memberId.ToString());
                return false;
            }

            var memberFaction = ResolveCharacterFactionId(world, memberId);
            if (string.IsNullOrEmpty(memberFaction))
            {
                error = new GameError(ErrorCode.InvalidOperation, "Member has no faction.", memberId.ToString());
                return false;
            }

            if (!string.Equals(memberFaction, factionId, StringComparison.Ordinal))
            {
                error = new GameError(
                    ErrorCode.InvalidOperation,
                    "Cross-faction army formation forbidden.",
                    memberId + ";" + memberFaction + ";" + factionId);
                return false;
            }

            if (world.Strategic.FormalArmies.TryGetArmyForCharacter(memberId.Value, out _))
            {
                error = new GameError(
                    ErrorCode.AlreadyExists,
                    "Character already in an army.",
                    memberId.ToString());
                return false;
            }

            if (entity.TryGet<ArmyMembershipComponent>(out var existingMem) && existingMem.IsInArmy)
            {
                error = new GameError(
                    ErrorCode.AlreadyExists,
                    "Character already in an army.",
                    memberId.ToString());
                return false;
            }

            var memberNode = ResolveCharacterNodeId(world, memberId);
            if (string.IsNullOrEmpty(memberNode))
            {
                error = new GameError(
                    ErrorCode.InvalidOperation,
                    "Member must be AtNode to form army.",
                    memberId.ToString());
                return false;
            }

            if (!string.Equals(memberNode, nodeId, StringComparison.Ordinal))
            {
                error = new GameError(
                    ErrorCode.InvalidOperation,
                    "All members must be at the same node.",
                    memberId + ";" + memberNode + ";" + nodeId);
                return false;
            }

            return true;
        }

        static bool TryResolveLeader(
            SimulationWorld world,
            IReadOnlyList<EntityId> members,
            EntityId? explicitLeaderId,
            out EntityId leaderId,
            out GameError error)
        {
            error = default;
            leaderId = EntityId.None;

            if (explicitLeaderId.HasValue && !explicitLeaderId.Value.IsNone)
            {
                if (!ContainsEntity(members, explicitLeaderId.Value))
                {
                    error = new GameError(ErrorCode.InvalidOperation, "Explicit leader must be a member.");
                    return false;
                }

                if (!IsValidLeaderAtFormation(world, explicitLeaderId.Value))
                {
                    error = new GameError(ErrorCode.InvalidOperation, "Explicit leader is not a valid leader.");
                    return false;
                }

                leaderId = explicitLeaderId.Value;
                return true;
            }

            for (var i = 0; i < members.Count; i++)
            {
                if (!IsValidLeaderAtFormation(world, members[i]))
                    continue;
                leaderId = members[i];
                return true;
            }

            error = new GameError(ErrorCode.InvalidOperation, "No valid leader among members.");
            return false;
        }

        static bool IsValidLeaderAtFormation(SimulationWorld world, EntityId candidate)
        {
            if (candidate.IsNone || !world.Entities.TryGet(candidate, out var entity))
                return false;
            if (!entity.TryGet<LifecycleComponent>(out var life))
                return false;
            return LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, candidate);
        }

        static bool IsValidLeaderCandidate(SimulationWorld world, EntityId candidate, FormalArmy army)
        {
            if (candidate.IsNone || army == null || !army.ContainsMember(candidate))
                return false;
            return IsValidLeaderAtFormation(world, candidate);
        }

        static void SyncMembershipForArmy(SimulationWorld world, FormalArmy army)
        {
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var memberId = new EntityId(army.MemberCharacterIds[i]);
                if (!world.Entities.TryGet(memberId, out var entity))
                    continue;
                ArmyInvariants.EnsureMembershipComponent(entity);
                entity.Get<ArmyMembershipComponent>().SetArmyId(army.ArmyId);
            }
        }

        static void ClearMembershipForArmy(SimulationWorld world, FormalArmy army)
        {
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var memberId = new EntityId(army.MemberCharacterIds[i]);
                if (!world.Entities.TryGet(memberId, out var entity))
                    continue;
                if (entity.TryGet<ArmyMembershipComponent>(out var mem))
                    mem.ClearArmyId();
            }
        }

        static bool TryValidateFriendlyNodeInternal(
            SimulationWorld world,
            string factionId,
            string nodeId,
            out GameError error)
        {
            if (world?.Strategic != null && world.Strategic.Ch01FormationScenarioCompat)
                return Ch01ScenarioArmyFormationPolicy.TryValidateFriendlyNode(world, factionId, nodeId, out error);

            return ArmyFormationNodePolicy.TryValidateFriendlyNode(world, factionId, nodeId, out error);
        }

        static bool ContainsEntity(IReadOnlyList<EntityId> list, EntityId id)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Value == id.Value)
                    return true;
            }

            return false;
        }
    }
}
