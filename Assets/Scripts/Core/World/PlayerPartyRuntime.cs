using System;
using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.World
{
    public enum PlayerPartyControlState
    {
        Active = 0,
        TemporarilyUnavailable = 1,
        AllMembersDead = 2
    }

    /// <summary>
    /// RPG-First LocalMap party: one Active + up to five Followers. Single runtime truth (Phase 1).
    /// </summary>
    public sealed class PlayerPartyRuntime
    {
        public const int MaxMembers = 6;

        readonly List<EntityId> _memberProjection = new List<EntityId>(MaxMembers);
        SimulationWorld _world;
        string _controlledSquadId = string.Empty;
        EntityId _activeId = EntityId.None;
        PlayerPartyControlState _controlState = PlayerPartyControlState.Active;

        public IReadOnlyList<EntityId> Members
        {
            get
            {
                _memberProjection.Clear();
                if (TryGetControlledSquad(out var squad))
                    for (var i = 0; i < squad.MemberCharacterIds.Count; i++)
                        _memberProjection.Add(new EntityId(squad.MemberCharacterIds[i]));
                return _memberProjection;
            }
        }

        public string ControlledSquadId => _controlledSquadId;

        public EntityId ActiveCharacterId => _activeId;

        public PlayerPartyControlState ControlState => _controlState;

        /// <summary>Only true when every current member is genuinely Dead/Removed.</summary>
        public bool IsAwaitingSuccession => _controlState == PlayerPartyControlState.AllMembersDead;

        public bool IsTemporarilyUnavailable =>
            _controlState == PlayerPartyControlState.TemporarilyUnavailable;

        public int Count => TryGetControlledSquad(out var squad) ? squad.MemberCharacterIds.Count : 0;

        public bool HasActive => !_activeId.IsNone && IsMember(_activeId);

        public bool IsMember(EntityId id) => TryGetControlledSquad(out var squad) && squad.Contains(id);

        public bool IsActive(EntityId id) => !id.IsNone && id == _activeId;

        public bool IsFollower(EntityId id) => IsMember(id) && !IsActive(id);

        public void Reset()
        {
            _world = null;
            _controlledSquadId = string.Empty;
            _memberProjection.Clear();
            _activeId = EntityId.None;
            _controlState = PlayerPartyControlState.Active;
        }

        public void BindWorld(SimulationWorld world)
        {
            _world = world;
            if (world?.Strategic != null) world.Strategic.PlayerPartyContext = this;
        }

        public bool TryBindControlledSquad(string squadId, EntityId activeId, out string error)
        {
            error = null;
            if (_world?.Strategic?.Squads == null || !_world.Strategic.Squads.TryGet(squadId, out var squad) || squad.MemberCharacterIds.Count == 0)
            { error = "Controlled squad not found."; return false; }
            _controlledSquadId = squad.SquadId;
            _activeId = activeId.IsNone || !squad.Contains(activeId) ? new EntityId(squad.MemberCharacterIds[0]) : activeId;
            _controlState = PlayerPartyControlState.Active;
            return true;
        }

        bool TryGetControlledSquad(out SquadState squad)
        {
            squad = null;
            return _world?.Strategic?.Squads != null && !string.IsNullOrEmpty(_controlledSquadId) &&
                   _world.Strategic.Squads.TryGet(_controlledSquadId, out squad);
        }

        public bool TryInitialize(EntityId firstActive, out string error)
        {
            error = null;
            if (firstActive.IsNone)
            {
                error = "No active character.";
                return false;
            }

            if (_world == null) { error = "PlayerParty is not bound to a world."; return false; }
            if (!_world.Strategic.Squads.TryGet(SquadMembershipService.PlayerSquadId, out var squad))
            {
                var created = SquadMembershipService.Create(_world, SquadMembershipService.PlayerSquadId,
                    new[] { firstActive }, firstActive, command: SquadCommandKind.FollowLeader);
                if (created.IsFailure) { error = created.Error.ToString(); return false; }
                squad = created.Value;
            }
            _controlledSquadId = squad.SquadId;
            _activeId = squad.Contains(firstActive) ? firstActive : new EntityId(squad.MemberCharacterIds[0]);
            _controlState = PlayerPartyControlState.Active;
            RefreshActiveAfterLifeState(_world);
            return true;
        }

        /// <summary>
        /// Snapshot restore：按存档顺序重建 Membership，跳过 Join 校验（LocalMap 尚未 Materialize）。
        /// </summary>
        public bool TryRestoreFromSnapshot(
            EntityId activeId,
            IReadOnlyList<EntityId> orderedMembers,
            out string error)
        {
            error = null;
            if (_world == null) { error = "PlayerParty is not bound to a world."; return false; }
            if (orderedMembers == null || orderedMembers.Count == 0)
            {
                error = "No members.";
                return false;
            }

            var restoredMembers = new List<EntityId>(orderedMembers.Count);
            for (var i = 0; i < orderedMembers.Count; i++)
            {
                var id = orderedMembers[i];
                if (id.IsNone || restoredMembers.Contains(id))
                    continue;
                restoredMembers.Add(id);
            }

            if (restoredMembers.Count == 0)
            {
                error = "No valid members.";
                return false;
            }

            if (_world.Strategic.Squads.TryGet(SquadMembershipService.PlayerSquadId, out var restoredSquad))
                return TryBindControlledSquad(restoredSquad.SquadId, activeId, out error);
            var created = SquadMembershipService.Create(_world, SquadMembershipService.PlayerSquadId,
                restoredMembers, activeId, command: SquadCommandKind.FollowLeader, importingSnapshot: true);
            if (created.IsFailure) { error = created.Error.ToString(); return false; }
            _controlledSquadId = created.Value.SquadId;
            if (activeId.IsNone || !created.Value.Contains(activeId))
                activeId = new EntityId(created.Value.MemberCharacterIds[0]);

            _activeId = activeId;
            _controlState = PlayerPartyControlState.Active;
            return true;
        }

        public bool TryAddMember(
            SimulationWorld world,
            IReadOnlyList<EntityId> roster,
            EntityId candidate,
            out string error)
        {
            error = null;
            if (!ValidateJoin(world, roster, candidate, out error))
                return false;

            var moved = SquadMembershipService.Transfer(world, candidate, _controlledSquadId);
            if (moved.IsFailure) { error = moved.Error.ToString(); return false; }
            RefreshActiveAfterLifeState(world);
            return true;
        }

        public bool TryRemoveMember(EntityId id, out string error)
        {
            error = null;
            if (!IsMember(id))
            {
                error = "Not in party.";
                return false;
            }

            if (IsActive(id))
            {
                error = "Cannot remove active; switch active first.";
                return false;
            }

            var left = SquadMembershipService.LeaveToSingleton(_world, id);
            if (left.IsFailure) { error = left.Error.ToString(); return false; }
            return true;
        }

        public bool TrySetActive(SimulationWorld world, EntityId id, out string error)
        {
            error = null;
            if (IsAwaitingSuccession)
            {
                error = "Party wiped; awaiting succession.";
                return false;
            }

            if (!IsMember(id))
            {
                error = "Not in party.";
                return false;
            }

            if (!CanActAsActive(world, id, out error))
                return false;

            _activeId = id;
            _controlState = PlayerPartyControlState.Active;
            return true;
        }

        /// <summary>
        /// Keeps the current valid Active. Otherwise selects the first eligible member in the
        /// stable Party order. No eligible member is classified separately from an actual wipe.
        /// </summary>
        public void RefreshActiveAfterLifeState(SimulationWorld world)
        {
            var members = Members;
            if (world == null || members.Count == 0)
            {
                _activeId = EntityId.None;
                _controlState = PlayerPartyControlState.TemporarilyUnavailable;
                return;
            }

            var continuousCombat = world.Strategic?.ContinuousManualCombat;
            var restrictToBattleParticipants = continuousCombat != null && continuousCombat.IsActive;
            if (!_activeId.IsNone &&
                (!restrictToBattleParticipants || continuousCombat.IsFriendly(_activeId)) &&
                CanActAsActive(world, _activeId, out _))
            {
                _controlState = PlayerPartyControlState.Active;
                return;
            }

            for (var i = 0; i < members.Count; i++)
            {
                var m = members[i];
                if (m == _activeId)
                    continue;
                if (restrictToBattleParticipants && !continuousCombat.IsFriendly(m))
                    continue;
                if (CanActAsActive(world, m, out _))
                {
                    _activeId = m;
                    _controlState = PlayerPartyControlState.Active;
                    return;
                }
            }

            _activeId = EntityId.None;
            _controlState = AreAllMembersTrulyDead(world)
                ? PlayerPartyControlState.AllMembersDead
                : PlayerPartyControlState.TemporarilyUnavailable;
        }

        bool AreAllMembersTrulyDead(SimulationWorld world)
        {
            var members = Members;
            if (world == null || members.Count == 0)
                return false;
            for (var i = 0; i < members.Count; i++)
            {
                if (!world.Entities.TryGet(members[i], out var entity) || entity == null ||
                    !entity.TryGet<LifecycleComponent>(out var life) || life == null ||
                    (!life.IsDead && !life.IsRemoved))
                    return false;
            }
            return true;
        }

        public bool ValidateJoin(
            SimulationWorld world,
            IReadOnlyList<EntityId> roster,
            EntityId candidate,
            out string error)
        {
            error = null;
            if (candidate.IsNone)
            {
                error = "Invalid character.";
                return false;
            }

            if (!IsInRoster(roster, candidate))
            {
                error = "Not a manageable character.";
                return false;
            }

            if (IsMember(candidate))
            {
                error = "Already in party.";
                return false;
            }

            if (Count >= MaxMembers)
            {
                error = "Party is full (max " + MaxMembers + ").";
                return false;
            }

            if (!HasActive)
            {
                error = "No active character.";
                return false;
            }

            if (world != null)
            {
                // §B：统一 co-presence 判定。Continuous Outdoor 已经没有 Outdoor LocalMap，
                // 继续用 IsOnSameLocalMap 会把物理相邻的两人判成不可加入（legacy gate）。
                // Legacy／Interior／Cave 仍由 PlayerPartyLocalCoPresenceQuery 走同一 LocalMap 规则。
                var coPresence = PlayerPartyLocalCoPresenceQuery.Evaluate(world, this, candidate);
                if (!coPresence.IsCoPresent)
                {
                    error = coPresence.PlayerMessage;
                    return false;
                }

                if (!CanActAsActive(world, candidate, out error))
                    return false;

                if (ActualBattleParticipantQuery.TryFind(world.Strategic.Participants, candidate, out _))
                { error = "Character is locked by the current battle."; return false; }
            }

            return true;
        }

        public static bool CanActAsActive(SimulationWorld world, EntityId id, out string error)
        {
            error = null;
            if (id.IsNone)
            {
                error = "Invalid character.";
                return false;
            }

            if (world == null)
                return true;

            if (!world.Entities.TryGet(id, out var entity))
            {
                error = "Character not found.";
                return false;
            }

            if (!CombatLifeStateService.CanFight(entity))
            {
                error = "Character cannot act (dying/dead).";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Legacy compatibility query（同一 LocalMap occupant）。保留给 Interior／Cave／old save 与既有
        /// 调用方；<b>Normal Continuous Outdoor join 不得使用</b> —— 那条路径走
        /// <see cref="PlayerPartyLocalCoPresenceQuery"/>。
        /// </summary>
        public static bool IsOnSameLocalMap(SimulationWorld world, EntityId a, EntityId b)
        {
            if (world?.LocalMap == null || a.IsNone || b.IsNone)
                return false;

            // Interior / cave enter flow still uses explicit occupant registry.
            if (world.LocalMap.ContainsOccupant(a) && world.LocalMap.ContainsOccupant(b))
                return true;

            var activeMapId = world.LocalMap.ActiveMapLayoutId;
            if (string.IsNullOrEmpty(activeMapId))
                return false;

            if (StrategicWorldSitePopulationService.TryResolvePartyFocusSite(world, out var site) &&
                string.Equals(
                    WorldTravelService.ResolveWorldSiteLocalMapId(site),
                    activeMapId,
                    StringComparison.Ordinal) &&
                StrategicWorldSitePopulationService.IsCharacterPresentAtWorldSite(world, a, site) &&
                StrategicWorldSitePopulationService.IsCharacterPresentAtWorldSite(world, b, site))
                return true;

            return IsEntityOnActiveMapLayout(world, a, activeMapId) &&
                   IsEntityOnActiveMapLayout(world, b, activeMapId);
        }

        static bool IsEntityOnActiveMapLayout(
            SimulationWorld world,
            EntityId id,
            string activeMapLayoutId)
        {
            if (world == null || id.IsNone || string.IsNullOrEmpty(activeMapLayoutId))
                return false;

            if (world.WorldPresence != null &&
                world.WorldPresence.TryGet(id, out var wp) &&
                wp != null &&
                wp.Mode == PartyWorldPresenceMode.AtSite &&
                !string.IsNullOrEmpty(wp.SiteId) &&
                world.Strategic.Sites.TryGet(wp.SiteId, out var site) &&
                site != null &&
                string.Equals(
                    WorldTravelService.ResolveWorldSiteLocalMapId(site),
                    activeMapLayoutId,
                    StringComparison.Ordinal))
                return true;

            if (!world.Entities.TryGet(id, out var entity))
                return false;
            if (!entity.TryGet<EntityLocationComponent>(out var loc) || !loc.HasLocation)
                return false;
            if (!world.WorldRegion.TryGet(loc.LocationId, out var place))
                return false;

            return string.IsNullOrEmpty(place.LocalMapId) ||
                   string.Equals(place.LocalMapId, activeMapLayoutId, StringComparison.Ordinal);
        }

        static bool IsInRoster(IReadOnlyList<EntityId> roster, EntityId id)
        {
            if (roster == null || id.IsNone)
                return false;
            for (var i = 0; i < roster.Count; i++)
            {
                if (roster[i] == id)
                    return true;
            }

            return false;
        }
    }

}
