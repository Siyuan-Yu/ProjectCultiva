using XianXia.Core.World;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    public enum SquadCommandKind
    {
        None = 0,
        FollowLeader = 1,
        SquadWorldMotion = 3
    }

    /// <summary>Persistent action group. Membership can only be changed by SquadMembershipService.</summary>
    public sealed class SquadState
    {
        readonly List<ulong> _members = new List<ulong>(PlayerPartyRuntime.MaxMembers);
        ReadOnlyCollection<ulong> _view;

        public string SquadId { get; internal set; } = string.Empty;
        public string DisplayName { get; internal set; } = string.Empty;
        public string FactionId { get; internal set; } = string.Empty;
        public EntityId LeaderCharacterId { get; internal set; }
        public SquadCommandKind CommandKind { get; internal set; }
        public ulong CommandRevision { get; internal set; }
        public EntityId CommandTargetCharacterId { get; internal set; }
        public IReadOnlyList<ulong> MemberCharacterIds => _view ?? (_view = _members.AsReadOnly());
        public bool IsOverCapacity => _members.Count > PlayerPartyRuntime.MaxMembers;

        public bool Contains(EntityId id) => !id.IsNone && _members.Contains(id.Value);
        internal void Add(EntityId id) { if (!id.IsNone && !_members.Contains(id.Value)) _members.Add(id.Value); }
        internal bool Remove(EntityId id) => !id.IsNone && _members.Remove(id.Value);
        internal void Replace(IReadOnlyList<ulong> ids)
        {
            _members.Clear();
            if (ids == null) return;
            for (var i = 0; i < ids.Count; i++)
                if (ids[i] != 0 && !_members.Contains(ids[i])) _members.Add(ids[i]);
        }
        internal void SetCommand(SquadCommandKind kind) { CommandKind = kind; CommandRevision++; }
    }

    public sealed class SquadBoard
    {
        readonly Dictionary<string, SquadState> _squads = new Dictionary<string, SquadState>(StringComparer.Ordinal);
        readonly Dictionary<ulong, string> _byCharacter = new Dictionary<ulong, string>();
        public IReadOnlyDictionary<string, SquadState> Squads => _squads;

        public bool TryGet(string id, out SquadState squad) => _squads.TryGetValue(id ?? string.Empty, out squad);
        public bool TryGetForCharacter(EntityId id, out SquadState squad)
        {
            squad = null;
            return !id.IsNone && _byCharacter.TryGetValue(id.Value, out var sid) && _squads.TryGetValue(sid, out squad);
        }
        internal void Register(SquadState squad)
        {
            if (squad == null || string.IsNullOrWhiteSpace(squad.SquadId) || _squads.ContainsKey(squad.SquadId))
                throw new InvalidOperationException("Duplicate or invalid SquadId: " + (squad?.SquadId ?? "<null>"));
            // Validate the entire reverse index before publishing either dictionary.
            var seen = new HashSet<ulong>();
            for (var i = 0; i < squad.MemberCharacterIds.Count; i++)
            {
                var id = squad.MemberCharacterIds[i];
                if (id == 0 || !seen.Add(id) || _byCharacter.ContainsKey(id))
                    throw new InvalidOperationException("Character belongs to multiple squads: " + id);
            }
            _squads.Add(squad.SquadId, squad);
            for (var i = 0; i < squad.MemberCharacterIds.Count; i++)
            {
                var id = squad.MemberCharacterIds[i];
                if (_byCharacter.ContainsKey(id)) throw new InvalidOperationException("Character belongs to multiple squads: " + id);
                _byCharacter.Add(id, squad.SquadId);
            }
        }
        internal void SetReverse(EntityId id, string squadId) { if (!id.IsNone) _byCharacter[id.Value] = squadId; }
        internal void RemoveReverse(EntityId id) { if (!id.IsNone) _byCharacter.Remove(id.Value); }
        internal void Remove(string squadId) { _squads.Remove(squadId ?? string.Empty); }
        public void Clear() { _squads.Clear(); _byCharacter.Clear(); }
    }

    public static class SquadMembershipService
    {
        public const string PlayerSquadId = "squad:player";
        public static string SingletonSquadId(EntityId id) => "squad:character:" + id.Value;

        public static Result<SquadState> Create(
            SimulationWorld world, string squadId, IReadOnlyList<EntityId> members,
            EntityId leader, SquadCommandKind command = SquadCommandKind.None,
            bool importingSnapshot = false, string displayName = "", string factionId = "")
        {
            if (world?.Strategic?.Squads == null || string.IsNullOrWhiteSpace(squadId) || members == null || members.Count == 0)
                return Result.Fail<SquadState>(ErrorCode.InvalidArgument, "Squad requires world, identity and members.");
            if (world.Strategic.Squads.TryGet(squadId, out _))
                return Result.Fail<SquadState>(ErrorCode.AlreadyExists, "Squad already exists.", squadId);
            if (!importingSnapshot && members.Count > PlayerPartyRuntime.MaxMembers)
                return Result.Fail<SquadState>(ErrorCode.InvalidArgument, "Squad exceeds six members.");
            var values = new List<ulong>(members.Count);
            for (var i = 0; i < members.Count; i++)
            {
                var id = members[i];
                if (id.IsNone || !world.Entities.TryGet(id, out var entity) ||
                    (entity.Tags & (EntityTag.Character | EntityTag.Npc)) == 0)
                    return Result.Fail<SquadState>(ErrorCode.InvalidArgument, "Squad member must be a real Character.", id.ToString());
                if (world.Strategic.Squads.TryGetForCharacter(id, out var existing) &&
                    !(existing.SquadId == SingletonSquadId(id) && existing.MemberCharacterIds.Count == 1))
                    return Result.Fail<SquadState>(ErrorCode.AlreadyExists, "Character already belongs to a non-singleton squad.", id + ";" + existing.SquadId);
                if (IsBattleLocked(world, existing))
                    return Result.Fail<SquadState>(ErrorCode.InvalidOperation, "Battle participant cannot change squad.", id.ToString());
                if (values.Contains(id.Value))
                    return Result.Fail<SquadState>(ErrorCode.InvalidArgument, "Duplicate squad member.", id.ToString());
                values.Add(id.Value);
            }
            if (leader.IsNone || !values.Contains(leader.Value)) leader = new EntityId(values[0]);
            var squad = new SquadState {
                SquadId = squadId, LeaderCharacterId = leader,
                CommandKind = command,
                DisplayName = displayName ?? string.Empty, FactionId = factionId ?? string.Empty
            };
            squad.Replace(values);
            var replaced = new List<SquadState>();
            for (var i = 0; i < members.Count; i++)
            {
                if (!world.Strategic.Squads.TryGetForCharacter(members[i], out var singleton)) continue;
                replaced.Add(singleton);
                world.Strategic.Squads.RemoveReverse(members[i]);
                world.Strategic.Squads.Remove(singleton.SquadId);
            }
            try { world.Strategic.Squads.Register(squad); }
            catch (InvalidOperationException ex)
            {
                for (var i = 0; i < replaced.Count; i++) world.Strategic.Squads.Register(replaced[i]);
                return Result.Fail<SquadState>(ErrorCode.InvalidOperation, "Squad registration failed.", ex.Message);
            }
            if (!importingSnapshot && command != SquadCommandKind.None)
                for (var i = 0; i < members.Count; i++)
                    BackgroundCharacterTravelService.CancelTravelIfAny(world, members[i]);
            return Result.Ok(squad);
        }

        public static Result Transfer(SimulationWorld world, EntityId member, string targetSquadId)
        {
            if (world?.Strategic?.Squads == null || member.IsNone || !world.Strategic.Squads.TryGet(targetSquadId, out var target))
                return Result.Failure(ErrorCode.InvalidArgument, "Squad transfer target is invalid.");
            if (target.Contains(member)) return Result.Success();
            if (!world.Entities.TryGet(member, out var realMember) ||
                (realMember.Tags & (EntityTag.Character | EntityTag.Npc)) == 0)
                return Result.Failure(ErrorCode.InvalidArgument, "Squad member must be a real Character.");
            if (target.MemberCharacterIds.Count >= PlayerPartyRuntime.MaxMembers)
                return Result.Failure(ErrorCode.InvalidOperation, target.IsOverCapacity ? "Legacy over-capacity squad is closed to additions." : "Squad is full.");
            world.Strategic.Squads.TryGetForCharacter(member, out var source);
            if (IsBattleLocked(world, target) || IsBattleLocked(world, source))
                return Result.Failure(ErrorCode.InvalidOperation, "Battle participant cannot change squad during the active encounter.");
            source?.Remove(member);
            world.Strategic.Squads.RemoveReverse(member);
            target.Add(member);
            world.Strategic.Squads.SetReverse(member, target.SquadId);
            if (source != null)
            {
                if (source.LeaderCharacterId == member && source.MemberCharacterIds.Count > 0)
                    source.LeaderCharacterId = new EntityId(source.MemberCharacterIds[0]);
                if (source.MemberCharacterIds.Count == 0)
                    world.Strategic.Squads.Remove(source.SquadId);
            }
            if (target.CommandKind != SquadCommandKind.None)
                BackgroundCharacterTravelService.CancelTravelIfAny(world, member);
            return Result.Success();
        }

        internal static bool IsBattleLocked(SimulationWorld world, SquadState squad)
        {
            if (world?.Strategic?.Participants == null || squad == null) return false;
            for (var i = 0; i < squad.MemberCharacterIds.Count; i++)
                if (ActualBattleParticipantQuery.TryFind(world.Strategic.Participants,
                        new EntityId(squad.MemberCharacterIds[i]), out _)) return true;
            return false;
        }

        public static Result LeaveToSingleton(SimulationWorld world, EntityId member)
        {
            if (world?.Strategic?.Squads == null || member.IsNone || !world.Strategic.Squads.TryGetForCharacter(member, out var source))
                return Result.Failure(ErrorCode.NotFound, "Character squad not found.");
            if (IsBattleLocked(world, source))
                return Result.Failure(ErrorCode.InvalidOperation, "Battle participant cannot change squad during the active encounter.");
            var singletonId = SingletonSquadId(member);
            if (source.SquadId == singletonId && source.MemberCharacterIds.Count == 1) return Result.Success();
            if (!world.Strategic.Squads.TryGet(singletonId, out _))
            {
                // Empty destination is private to this synchronous transaction. Transfer owns
                // leader, reverse membership and legacy cleanup for both leave paths.
                var target = new SquadState { SquadId = singletonId, LeaderCharacterId = member };
                world.Strategic.Squads.Register(target);
                var result = Transfer(world, member, singletonId);
                if (result.IsFailure) world.Strategic.Squads.Remove(singletonId);
                return result;
            }
            return Transfer(world, member, singletonId);
        }

        /// <summary>
        /// Replaces a PlayerParty that has no eligible Active with one external controller. A
        /// genuine succession retires every old member to singleton authority. An emergency
        /// takeover keeps living old members together in an idle recovery squad while dead or
        /// removed members use the existing singleton/corpse retirement. Lifecycle and exact
        /// spatial state are deliberately untouched.
        /// </summary>
        public static Result<SquadState> ReplacePlayerSquadForExternalHandoff(
            SimulationWorld world,
            PlayerPartyRuntime party,
            EntityId successor,
            PlayerFactionControlHandoffKind kind,
            out string recoverySquadId)
        {
            recoverySquadId = string.Empty;
            if (world?.Strategic?.Squads == null || party == null || successor.IsNone ||
                !world.Entities.TryGet(successor, out var successorEntity) ||
                successorEntity == null ||
                (successorEntity.Tags & (EntityTag.Character | EntityTag.Npc)) == 0)
                return Result.Fail<SquadState>(ErrorCode.InvalidArgument,
                    "External control handoff requires a real Character successor.");
            if (!party.NeedsExternalControlHandoff ||
                string.IsNullOrEmpty(party.ControlledSquadId) ||
                !world.Strategic.Squads.TryGet(party.ControlledSquadId, out var oldPlayerSquad) ||
                oldPlayerSquad == null || oldPlayerSquad.Contains(successor))
                return Result.Fail<SquadState>(ErrorCode.InvalidOperation,
                    "PlayerParty is not ready for external control handoff.");
            if (!string.Equals(oldPlayerSquad.SquadId, PlayerSquadId, StringComparison.Ordinal))
                return Result.Fail<SquadState>(ErrorCode.InvalidOperation,
                    "Controlled PlayerParty squad identity is invalid.", oldPlayerSquad.SquadId);

            world.Strategic.Squads.TryGetForCharacter(successor, out var sourceSquad);
            if (IsBattleLocked(world, oldPlayerSquad) || IsBattleLocked(world, sourceSquad))
                return Result.Fail<SquadState>(ErrorCode.InvalidOperation,
                    "Battle participant cannot change squad during external control handoff.");

            if ((kind == PlayerFactionControlHandoffKind.EmergencyTakeover &&
                 party.ControlState != PlayerPartyControlState.TemporarilyUnavailable) ||
                (kind == PlayerFactionControlHandoffKind.Succession && !party.IsAwaitingSuccession))
                return Result.Fail<SquadState>(ErrorCode.InvalidOperation,
                    "External control handoff kind does not match PlayerParty state.");

            var retiredMembers = new List<EntityId>(oldPlayerSquad.MemberCharacterIds.Count);
            var recoveryMembers = new List<EntityId>(oldPlayerSquad.MemberCharacterIds.Count);
            for (var i = 0; i < oldPlayerSquad.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(oldPlayerSquad.MemberCharacterIds[i]);
                var isLiving = world.Entities.TryGet(id, out var memberEntity) && memberEntity != null &&
                    memberEntity.TryGet<LifecycleComponent>(out var life) && life != null &&
                    !life.IsDead && !life.IsRemoved;
                if (kind == PlayerFactionControlHandoffKind.EmergencyTakeover && isLiving)
                {
                    recoveryMembers.Add(id);
                    continue;
                }
                var singletonId = SingletonSquadId(id);
                if (world.Strategic.Squads.TryGet(singletonId, out var existing) &&
                    !ReferenceEquals(existing, oldPlayerSquad))
                    return Result.Fail<SquadState>(ErrorCode.AlreadyExists,
                        "Retired PlayerParty member already has a singleton squad.", singletonId);
                retiredMembers.Add(id);
            }
            if (kind == PlayerFactionControlHandoffKind.EmergencyTakeover && recoveryMembers.Count == 0)
                return Result.Fail<SquadState>(ErrorCode.InvalidOperation,
                    "Emergency takeover requires at least one living old Party member.");
            if (recoveryMembers.Count > 0)
                recoverySquadId = NextRecoverySquadId(world);

            // Detach only the successor. The source squad's remaining member order, command and
            // motion object stay intact; leader replacement follows the existing stable rule.
            if (sourceSquad != null)
            {
                sourceSquad.Remove(successor);
                world.Strategic.Squads.RemoveReverse(successor);
                if (sourceSquad.LeaderCharacterId == successor &&
                    sourceSquad.MemberCharacterIds.Count > 0)
                    sourceSquad.LeaderCharacterId =
                        new EntityId(sourceSquad.MemberCharacterIds[0]);
                if (sourceSquad.CommandTargetCharacterId == successor)
                {
                    sourceSquad.CommandTargetCharacterId =
                        sourceSquad.MemberCharacterIds.Count > 0
                            ? sourceSquad.LeaderCharacterId
                            : EntityId.None;
                    sourceSquad.CommandRevision++;
                }
                if (sourceSquad.MemberCharacterIds.Count == 0)
                {
                    world.Strategic.Squads.Remove(sourceSquad.SquadId);
                    world.Strategic.SquadWorldMotions.Remove(sourceSquad.SquadId);
                }
            }

            // Retire the old controlled identity without altering lifecycle or world presence.
            for (var i = 0; i < oldPlayerSquad.MemberCharacterIds.Count; i++)
                world.Strategic.Squads.RemoveReverse(
                    new EntityId(oldPlayerSquad.MemberCharacterIds[i]));
            world.Strategic.Squads.Remove(oldPlayerSquad.SquadId);
            world.Strategic.SquadWorldMotions.Remove(oldPlayerSquad.SquadId);

            if (recoveryMembers.Count > 0)
            {
                var recovery = new SquadState
                {
                    SquadId = recoverySquadId,
                    DisplayName = "失能待援小队",
                    FactionId = string.IsNullOrEmpty(oldPlayerSquad.FactionId)
                        ? world.Strategic.PlayerFactionId
                        : oldPlayerSquad.FactionId,
                    LeaderCharacterId = recoveryMembers[0],
                    CommandKind = SquadCommandKind.None,
                    CommandTargetCharacterId = EntityId.None
                };
                for (var i = 0; i < recoveryMembers.Count; i++)
                    recovery.Add(recoveryMembers[i]);
                world.Strategic.Squads.Register(recovery);
            }
            for (var i = 0; i < retiredMembers.Count; i++)
            {
                var id = retiredMembers[i];
                var singleton = new SquadState
                {
                    SquadId = SingletonSquadId(id),
                    LeaderCharacterId = id
                };
                singleton.Add(id);
                world.Strategic.Squads.Register(singleton);
            }

            var replacement = new SquadState
            {
                SquadId = PlayerSquadId,
                DisplayName = oldPlayerSquad.DisplayName,
                FactionId = string.IsNullOrEmpty(oldPlayerSquad.FactionId)
                    ? world.Strategic.PlayerFactionId
                    : oldPlayerSquad.FactionId,
                LeaderCharacterId = successor,
                CommandKind = SquadCommandKind.FollowLeader
            };
            replacement.Add(successor);
            world.Strategic.Squads.Register(replacement);
            BackgroundCharacterTravelService.CancelTravelIfAny(world, successor);
            return Result.Ok(replacement);
        }

        static string NextRecoverySquadId(SimulationWorld world)
        {
            const string prefix = "squad:recovery:";
            ulong sequence = 1;
            while (world.Strategic.Squads.TryGet(prefix + sequence, out _))
                sequence++;
            return prefix + sequence;
        }

        public static void EnsureSingletonsForUnassignedCharacters(SimulationWorld world)
        {
            if (world?.Strategic?.Squads == null) return;
            foreach (var entity in world.Entities.All)
            {
                EnsureSingletonForCharacter(world, entity);
            }
        }

        public static void EnsureSingletonForCharacter(SimulationWorld world, Entity entity)
        {
            if (world?.Strategic?.Squads == null || entity == null ||
                (entity.Tags & (EntityTag.Character | EntityTag.Npc)) == 0 ||
                world.Strategic.Squads.TryGetForCharacter(entity.Id, out _)) return;
            var result = Create(world, SingletonSquadId(entity.Id), new[] { entity.Id }, entity.Id);
            if (result.IsFailure) throw new InvalidOperationException(result.Error.ToString());
        }
    }

    /// <summary>Common command ownership; existing leader and WorldMotion executors retain navigation.</summary>
    public static class SquadCommandService
    {
        public static bool SetExecution(SimulationWorld world, string squadId, SquadCommandKind kind, EntityId target)
        {
            if (world?.Strategic?.Squads == null || !world.Strategic.Squads.TryGet(squadId, out var squad)) return false;
            if (kind == SquadCommandKind.FollowLeader && !squad.Contains(target)) return false;
            if (squad.CommandKind == kind && squad.CommandTargetCharacterId == target) return true;
            squad.CommandTargetCharacterId = target;
            squad.SetCommand(kind);
            return true;
        }

        public static bool OwnsIndividualSchedule(SimulationWorld world, EntityId id)
        {
            if (world?.Strategic?.Squads == null || !world.Strategic.Squads.TryGetForCharacter(id, out var squad) ||
                !CharacterLifeStateQuery.IsLivingForMacroOrder(world, id)) return false;
            // An idle field formation still owns its members; stopping its route must not
            // let Core start work while the Host continues to hold formation. Garrisoned
            // members are excluded by the same authority predicate used by the Host.
            if (SquadWorldMotionService.OwnsCharacter(world, id)) return true;
            return squad.MemberCharacterIds.Count > 1 && squad.CommandKind == SquadCommandKind.FollowLeader &&
                   id != (squad.CommandTargetCharacterId.IsNone ? squad.LeaderCharacterId : squad.CommandTargetCharacterId);
        }

        public static bool TryResolveWorldTarget(SimulationWorld world, SquadState squad, out WorldVec2 position)
        {
            position = default;
            if (world == null || squad == null) return false;
            if (squad.CommandKind == SquadCommandKind.SquadWorldMotion &&
                world.Strategic.SquadWorldMotions.TryGet(squad.SquadId, out var squadMotion) &&
                SquadWorldMotionService.IsActiveNpcSquadAuthority(world, squad, squadMotion) &&
                squadMotion.IsMoving)
            {
                position = squadMotion.Destination;
                return true;
            }
            var target = squad.CommandTargetCharacterId.IsNone ? squad.LeaderCharacterId : squad.CommandTargetCharacterId;
            if (squad.CommandKind == SquadCommandKind.FollowLeader &&
                world.WorldPresence.TryGet(target, out var presence) && presence.HasContinuousWorldPosition)
            {
                position = presence.ContinuousWorldPosition;
                return true;
            }
            return false;
        }
    }
}
