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
        FormalArmyWorldMotion = 2
    }

    /// <summary>Persistent action group. Membership can only be changed by SquadMembershipService.</summary>
    public sealed class SquadState
    {
        readonly List<ulong> _members = new List<ulong>(PlayerPartyRuntime.MaxMembers);
        ReadOnlyCollection<ulong> _view;

        public string SquadId { get; internal set; } = string.Empty;
        public EntityId LeaderCharacterId { get; internal set; }
        public string LegacyArmyId { get; internal set; } = string.Empty;
        public SquadCommandKind CommandKind { get; internal set; }
        public ulong CommandRevision { get; internal set; }
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
        public static string ArmySquadId(string armyId) => "squad:army:" + (armyId ?? string.Empty);
        public static string SingletonSquadId(EntityId id) => "squad:character:" + id.Value;

        public static Result<SquadState> Create(
            SimulationWorld world, string squadId, IReadOnlyList<EntityId> members,
            EntityId leader, string legacyArmyId = "", SquadCommandKind command = SquadCommandKind.None)
        {
            if (world?.Strategic?.Squads == null || string.IsNullOrWhiteSpace(squadId) || members == null || members.Count == 0)
                return Result.Fail<SquadState>(ErrorCode.InvalidArgument, "Squad requires world, identity and members.");
            if (world.Strategic.Squads.TryGet(squadId, out _))
                return Result.Fail<SquadState>(ErrorCode.AlreadyExists, "Squad already exists.", squadId);
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
                if (!values.Contains(id.Value)) values.Add(id.Value);
            }
            if (leader.IsNone || !values.Contains(leader.Value)) leader = new EntityId(values[0]);
            var squad = new SquadState { SquadId = squadId, LeaderCharacterId = leader, LegacyArmyId = legacyArmyId ?? string.Empty, CommandKind = command };
            squad.Replace(values);
            for (var i = 0; i < members.Count; i++)
            {
                if (!world.Strategic.Squads.TryGetForCharacter(members[i], out var singleton)) continue;
                singleton.Remove(members[i]);
                world.Strategic.Squads.RemoveReverse(members[i]);
                world.Strategic.Squads.Remove(singleton.SquadId);
            }
            try { world.Strategic.Squads.Register(squad); }
            catch (Exception ex) { return Result.Fail<SquadState>(ErrorCode.InvalidOperation, "Squad registration failed.", ex.Message); }
            return Result.Ok(squad);
        }

        public static Result Transfer(SimulationWorld world, EntityId member, string targetSquadId)
        {
            if (world?.Strategic?.Squads == null || member.IsNone || !world.Strategic.Squads.TryGet(targetSquadId, out var target))
                return Result.Failure(ErrorCode.InvalidArgument, "Squad transfer target is invalid.");
            if (target.Contains(member)) return Result.Success();
            if (target.MemberCharacterIds.Count >= PlayerPartyRuntime.MaxMembers)
                return Result.Failure(ErrorCode.InvalidOperation, target.IsOverCapacity ? "Legacy over-capacity squad is closed to additions." : "Squad is full.");
            world.Strategic.Squads.TryGetForCharacter(member, out var source);
            if (IsBattleLocked(world, target) || IsBattleLocked(world, source))
                return Result.Failure(ErrorCode.InvalidOperation, "Battle participant cannot change squad during the active encounter.");
            var sourceArmyId = source?.LegacyArmyId ?? string.Empty;
            source?.Remove(member);
            world.Strategic.Squads.RemoveReverse(member);
            target.Add(member);
            world.Strategic.Squads.SetReverse(member, target.SquadId);
            if (source != null)
            {
                if (source.LeaderCharacterId == member && source.MemberCharacterIds.Count > 0)
                    source.LeaderCharacterId = new EntityId(source.MemberCharacterIds[0]);
                if (source.MemberCharacterIds.Count == 0 && string.IsNullOrEmpty(source.LegacyArmyId))
                    world.Strategic.Squads.Remove(source.SquadId);
            }
            if (world.Entities.TryGet(member, out var entity) && entity != null)
            {
                ArmyInvariants.EnsureMembershipComponent(entity);
                var membership = entity.Get<ArmyMembershipComponent>();
                if (!string.IsNullOrEmpty(target.LegacyArmyId)) membership.SetArmyId(target.LegacyArmyId);
                else if (!string.IsNullOrEmpty(sourceArmyId)) membership.ClearArmyId();
            }
            if (!string.IsNullOrEmpty(sourceArmyId) && source != null && source.MemberCharacterIds.Count == 0)
            {
                world.Strategic.FormalArmies.Remove(sourceArmyId);
                world.Strategic.Squads.Remove(source.SquadId);
            }
            return Result.Success();
        }

        static bool IsBattleLocked(SimulationWorld world, SquadState squad)
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
                // Register after removing from source so the one-membership invariant stays exact.
                source.Remove(member); world.Strategic.Squads.RemoveReverse(member);
                var created = Create(world, singletonId, new[] { member }, member);
                if (created.IsFailure) { source.Add(member); world.Strategic.Squads.SetReverse(member, source.SquadId); return Result.Failure(created.Error); }
                if (source.MemberCharacterIds.Count == 0 && string.IsNullOrEmpty(source.LegacyArmyId)) world.Strategic.Squads.Remove(source.SquadId);
                return Result.Success();
            }
            return Transfer(world, member, singletonId);
        }

        public static void EnsureSingletonsForUnassignedCharacters(SimulationWorld world)
        {
            if (world?.Strategic?.Squads == null) return;
            foreach (var entity in world.Entities.All)
            {
                if (entity == null || (entity.Tags & (EntityTag.Character | EntityTag.Npc)) == 0 ||
                    world.Strategic.Squads.TryGetForCharacter(entity.Id, out _)) continue;
                Create(world, SingletonSquadId(entity.Id), new[] { entity.Id }, entity.Id);
            }
        }
    }
}
