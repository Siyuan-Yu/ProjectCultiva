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
    /// Formal Army Domain ??????Phase A?Pure Hex??
    /// ???????<see cref="FormalArmy.MemberCharacterIds"/>?
    /// <see cref="ArmyMembershipComponent"/> ???????
    /// </summary>
    public static class ArmyService
    {
        public static Result<FormalArmy> CreateArmy(
            SimulationWorld world,
            string factionId,
            string siteId,
            IReadOnlyList<EntityId> memberCharacterIds,
            EntityId? explicitLeaderId = null)
        {
            if (world?.Strategic?.FormalArmies == null)
                return Result.Fail<FormalArmy>(ErrorCode.InvalidArgument, "SimulationWorld incomplete.");
            if (string.IsNullOrEmpty(factionId))
                return Result.Fail<FormalArmy>(ErrorCode.InvalidArgument, "FactionId required.");
            if (string.IsNullOrEmpty(siteId))
                return Result.Fail<FormalArmy>(ErrorCode.InvalidArgument, "SiteId required.");
            if (memberCharacterIds == null || memberCharacterIds.Count < 1)
                return Result.Fail<FormalArmy>(ErrorCode.InvalidArgument, "Army requires at least one member.");

            if (!Ch01ScenarioArmyFormationPolicy.TryValidateFriendlyNode(world, factionId, siteId, out var siteError))
                return Result.Fail<FormalArmy>(siteError);

            var resolvedMembers = new List<EntityId>(memberCharacterIds.Count);
            for (var i = 0; i < memberCharacterIds.Count; i++)
            {
                var memberId = memberCharacterIds[i];
                if (memberId.IsNone)
                    return Result.Fail<FormalArmy>(ErrorCode.InvalidArgument, "Invalid member EntityId.");

                if (!TryValidateMemberForFormation(world, memberId, factionId, siteId, out var memberError))
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
                State = FormalArmyState.Idle
            };
            army.ReplaceMembers(memberValues);

            world.Strategic.FormalArmies.Register(army);
            SyncMembershipForArmy(world, army);
            if (world.Strategic.Sites.TryGet(siteId, out var site) && site != null)
                ArmyHexTravelService.InitializeArmyAtHex(army, site.AnchorHex);

            return Result.Ok(army);
        }

        public static Result DisbandArmy(SimulationWorld world, string armyId)
        {
            if (world?.Strategic?.FormalArmies == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld incomplete.");
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return Result.Failure(ErrorCode.NotFound, "Army not found.", armyId);

            if (!TryValidateArmyFormationLocation(world, army, out var siteError))
                return Result.Failure(siteError);

            return ForceRemoveArmy(world, army);
        }

        public static Result GarrisonArmy(SimulationWorld world, string armyId)
        {
            if (world?.Strategic?.FormalArmies == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld incomplete.");
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return Result.Failure(ErrorCode.NotFound, "Army not found.", armyId);

            if (!TryValidateArmyFormationLocation(world, army, out var siteError))
                return Result.Failure(siteError);

            if (army.State == FormalArmyState.Garrisoned)
                return Result.Success();

            army.State = FormalArmyState.Garrisoned;
            return Result.Success();
        }

        public static Result MobilizeArmy(SimulationWorld world, string armyId)
        {
            if (world?.Strategic?.FormalArmies == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld incomplete.");
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return Result.Failure(ErrorCode.NotFound, "Army not found.", armyId);

            if (army.State == FormalArmyState.Idle)
                return Result.Success();
            if (army.State != FormalArmyState.Garrisoned)
                return Result.Failure(ErrorCode.InvalidOperation, "Only garrisoned army can mobilize.", armyId);

            army.State = FormalArmyState.Idle;
            return Result.Success();
        }

        public static Result AddMember(SimulationWorld world, string armyId, EntityId memberId)
        {
            if (world?.Strategic?.FormalArmies == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld incomplete.");
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return Result.Failure(ErrorCode.NotFound, "Army not found.", armyId);

            if (!TryValidateArmyFormationLocation(world, army, out var siteError))
                return Result.Failure(siteError);

            if (!TryResolveArmySiteId(world, army, out var armySiteId))
                return Result.Failure(ErrorCode.InvalidOperation, "Army has no formation site.");

            if (!TryValidateMemberForFormation(world, memberId, army.FactionId, armySiteId, out var memberError))
                return Result.Failure(memberError);

            army.AddMember(memberId);
            BackgroundCharacterTravelService.CancelTravelIfAny(world, memberId);
            SyncMembershipForArmy(world, army);
            return Result.Success();
        }

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

        static void DetachMemberAtBattlefieldInternal(SimulationWorld world, FormalArmy army, EntityId memberId)
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

            if (!TryValidateArmyFormationLocation(world, army, out var siteError))
                return Result.Failure(siteError);

            if (!army.ContainsMember(memberId))
                return Result.Failure(ErrorCode.NotFound, "Member not in army.", memberId.ToString());

            if (army.MemberCharacterIds.Count <= 1)
                return Result.Failure(ErrorCode.InvalidOperation, "Cannot remove last member; disband army instead.");

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

            if (!TryValidateArmyFormationLocation(world, army, out var siteError))
                return Result.Failure(siteError);

            if (!army.ContainsMember(newLeaderId))
                return Result.Failure(ErrorCode.InvalidOperation, "Leader must be a member.");

            if (!IsValidLeaderAtFormation(world, newLeaderId))
                return Result.Failure(ErrorCode.InvalidOperation, "Leader is not valid.");

            army.LeaderCharacterId = newLeaderId;
            return Result.Success();
        }

        public static void CollectResidentsAtSite(
            SimulationWorld world,
            string siteId,
            string factionId,
            IReadOnlyList<EntityId> candidateCharacterIds,
            List<EntityId> ungroupedInto,
            List<FormalArmy> armiesAtSiteInto)
        {
            ungroupedInto?.Clear();
            armiesAtSiteInto?.Clear();
            if (world == null || ungroupedInto == null || armiesAtSiteInto == null)
                return;

            if (!Ch01ScenarioArmyFormationPolicy.IsFriendlyNodeForFormation(world, siteId, factionId))
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
                    if (!string.Equals(ResolveCharacterSiteId(world, id), siteId, StringComparison.Ordinal))
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
                if (!TryResolveArmySiteId(world, army, out var armySite) ||
                    !string.Equals(armySite, siteId, StringComparison.Ordinal))
                    continue;
                armiesAtSiteInto.Add(army);
            }
        }

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

            if (!world.Strategic.FormalArmies.TryGetArmyForCharacter(characterId.Value, out army) || army == null)
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

        public static string ResolveCharacterFormationLocationId(SimulationWorld world, EntityId characterId) =>
            ResolveCharacterSiteId(world, characterId);

        public static string ResolveCharacterSiteId(SimulationWorld world, EntityId characterId)
        {
            if (world?.WorldPresence == null || characterId.IsNone)
                return string.Empty;

            if (!world.WorldPresence.TryGet(characterId, out var presence) || presence == null)
                return string.Empty;

            if (presence.Mode != PartyWorldPresenceMode.AtSite)
                return string.Empty;

            return presence.SiteId ?? string.Empty;
        }

        public static bool TryResolveArmySiteId(SimulationWorld world, FormalArmy army, out string siteId)
        {
            siteId = string.Empty;
            return army != null && army.TryGetFormationSiteId(world, out siteId);
        }

        static Result ForceRemoveArmy(SimulationWorld world, FormalArmy army)
        {
            if (army == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Army is null.");
            var disbandSite = TryResolveDisbandSite(world, army);
            ClearMembershipForArmy(world, army);
            world.Strategic.FormalArmies.Remove(army.ArmyId);
            if (disbandSite != null)
                PromoteFormerMembersToSite(world, army, disbandSite);
            return Result.Success();
        }

        static void RemoveMemberInternal(SimulationWorld world, FormalArmy army, EntityId memberId)
        {
            if (world.Entities.TryGet(memberId, out var entity) &&
                entity.TryGet<ArmyMembershipComponent>(out var mem))
                mem.ClearArmyId();
            army.RemoveMember(memberId);
            ClearAtSitePresence(world, memberId);
            if (LingeringBattlefieldPartyService.IsLingeringDowned(world, memberId))
                return;

            var site = TryResolveDisbandSite(world, army);
            if (site != null)
                world.WorldPresence.SetAtSite(memberId, site.SiteId);
        }

        static bool TryValidateMemberForFormation(
            SimulationWorld world,
            EntityId memberId,
            string factionId,
            string siteId,
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
                error = new GameError(ErrorCode.AlreadyExists, "Character already in an army.", memberId.ToString());
                return false;
            }

            if (entity.TryGet<ArmyMembershipComponent>(out var existingMem) && existingMem.IsInArmy)
            {
                error = new GameError(ErrorCode.AlreadyExists, "Character already in an army.", memberId.ToString());
                return false;
            }

            var memberSite = ResolveCharacterSiteId(world, memberId);
            if (string.IsNullOrEmpty(memberSite))
            {
                error = new GameError(ErrorCode.InvalidOperation, "Member must be AtSite to form army.", memberId.ToString());
                return false;
            }

            if (!world.Strategic.Sites.TryGet(siteId, out var formationSite) ||
                formationSite == null ||
                !string.Equals(memberSite, formationSite.SiteId, StringComparison.Ordinal))
            {
                error = new GameError(
                    ErrorCode.InvalidOperation,
                    "All members must be at the same site.",
                    memberId + ";" + memberSite + ";" + siteId);
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
                ClearAtSitePresence(world, memberId);
            }
        }

        static void ClearAtSitePresence(SimulationWorld world, EntityId memberId)
        {
            if (world?.WorldPresence == null ||
                !world.WorldPresence.TryGet(memberId, out var presence) ||
                presence == null ||
                presence.Mode != PartyWorldPresenceMode.AtSite)
                return;
            presence.SiteId = string.Empty;
        }

        static WorldSite TryResolveDisbandSite(SimulationWorld world, FormalArmy army)
        {
            if (world?.Strategic?.Sites == null || army == null)
                return null;
            if (world.Strategic.Sites.TryGetAtHex(army.CurrentHex, out var hexSite) && hexSite != null)
                return hexSite;
            return null;
        }

        static void PromoteFormerMembersToSite(SimulationWorld world, FormalArmy army, WorldSite site)
        {
            if (world?.WorldPresence == null || army == null || site == null)
                return;
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var memberId = new EntityId(army.MemberCharacterIds[i]);
                if (memberId.IsNone)
                    continue;
                world.WorldPresence.SetAtSite(memberId, site.SiteId);
            }
        }

        static bool TryValidateArmyFormationLocation(SimulationWorld world, FormalArmy army, out GameError error)
        {
            error = default;
            if (army == null)
            {
                error = new GameError(ErrorCode.InvalidArgument, "Army missing.");
                return false;
            }

            return ArmyFormationSitePolicy.TryValidateFormationAtHex(world, army.FactionId, army.CurrentHex, out error);
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
