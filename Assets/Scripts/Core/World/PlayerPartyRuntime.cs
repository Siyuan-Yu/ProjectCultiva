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
    /// <summary>
    /// RPG-First LocalMap party: one Active + up to five Followers. Single runtime truth (Phase 1).
    /// </summary>
    public sealed class PlayerPartyRuntime
    {
        public const int MaxMembers = 6;

        readonly List<EntityId> _members = new List<EntityId>(MaxMembers);
        EntityId _activeId = EntityId.None;
        bool _awaitingSuccession;

        public IReadOnlyList<EntityId> Members => _members;

        public EntityId ActiveCharacterId => _activeId;

        public bool IsAwaitingSuccession => _awaitingSuccession;

        public int Count => _members.Count;

        public bool HasActive => !_activeId.IsNone && _members.Contains(_activeId);

        public bool IsMember(EntityId id) => !id.IsNone && _members.Contains(id);

        public bool IsActive(EntityId id) => !id.IsNone && id == _activeId;

        public bool IsFollower(EntityId id) => IsMember(id) && !IsActive(id);

        public void Reset()
        {
            _members.Clear();
            _activeId = EntityId.None;
            _awaitingSuccession = false;
        }

        public bool TryInitialize(EntityId firstActive, out string error)
        {
            error = null;
            Reset();
            if (firstActive.IsNone)
            {
                error = "No active character.";
                return false;
            }

            _members.Add(firstActive);
            _activeId = firstActive;
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

            _members.Add(candidate);
            _awaitingSuccession = false;
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

            _members.Remove(id);
            return true;
        }

        public bool TrySetActive(SimulationWorld world, EntityId id, out string error)
        {
            error = null;
            if (_awaitingSuccession)
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
            return true;
        }

        /// <summary>When Active enters Dying/Dead, pick another living member or wipe.</summary>
        public void RefreshActiveAfterLifeState(SimulationWorld world)
        {
            if (world == null || _members.Count == 0)
            {
                _activeId = EntityId.None;
                _awaitingSuccession = false;
                return;
            }

            if (!_activeId.IsNone &&
                world.Entities.TryGet(_activeId, out var activeEnt) &&
                CombatLifeStateService.CanFight(activeEnt))
                return;

            for (var i = 0; i < _members.Count; i++)
            {
                var m = _members[i];
                if (m == _activeId)
                    continue;
                if (world.Entities.TryGet(m, out var ent) && CombatLifeStateService.CanFight(ent))
                {
                    _activeId = m;
                    _awaitingSuccession = false;
                    return;
                }
            }

            _activeId = EntityId.None;
            _awaitingSuccession = true;
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

            if (_members.Count >= MaxMembers)
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
                if (!IsOnSameLocalMap(world, candidate, _activeId))
                {
                    error = "Must be on the same LocalMap as the active character.";
                    return false;
                }

                if (!CanActAsActive(world, candidate, out error))
                    return false;

                if (ArmyService.TryGetArmyForCharacter(world, candidate, out _))
                {
                    error = "Character is in a formal army.";
                    return false;
                }
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

            if (ArmyService.TryGetArmyForCharacter(world, id, out _))
            {
                error = "Character is in a formal army.";
                return false;
            }

            return true;
        }

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
