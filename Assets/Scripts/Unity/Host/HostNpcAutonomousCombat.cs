using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Attributes;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Navigation;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Loaded, all-to-all NPC combat. Player-party Outdoor contact enters CharacterEncounter.</summary>
    public sealed class HostNpcAutonomousCombat : MonoBehaviour
    {
        const float NoticeRange = 6f;
        const float ScanInterval = .3f;
        PlayableHostBootstrap _host;
        object _trackedWorld;
        float _nextScan;
        readonly List<EntityId> _visible = new List<EntityId>(64);
        readonly Dictionary<ulong, EntityId> _targets = new Dictionary<ulong, EntityId>();
        readonly Dictionary<ulong, float> _strikeCooldown = new Dictionary<ulong, float>();
        readonly HashSet<ulong> _explicitStop = new HashSet<ulong>();
        readonly Dictionary<ulong, float> _retreatingUntil = new Dictionary<ulong, float>();
        readonly List<EntityId> _previousAutonomousActors = new List<EntityId>(64);
        readonly MeleeCombatService _melee = new MeleeCombatService();
        readonly SpiritVeilService _veil = new SpiritVeilService();

        public void Bind(PlayableHostBootstrap host) { _host = host; _trackedWorld = null; }

        public void SetExplicitStop(EntityId id, bool stopped)
        {
            if (id.IsNone) return;
            if (stopped) { _explicitStop.Add(id.Value); _targets.Remove(id.Value); }
            else _explicitStop.Remove(id.Value);
        }

        void Update()
        {
            if (_host?.Session?.World == null || _host.Session.IsPaused) return;
            var world = _host.Session.World;
            if (!ReferenceEquals(_trackedWorld, world))
            { _trackedWorld = world; _targets.Clear(); _strikeCooldown.Clear(); _explicitStop.Clear(); _retreatingUntil.Clear(); _nextScan = 0f; }
            if (world.Strategic.CharacterEncounter != null ||
                world.Strategic.ContinuousManualCombat?.IsActive == true) return;
            var spawner = _host.ViewSpawner;
            if (spawner == null || _host.MoveController == null) return;
            var dt = Mathf.Max(0f, _host.PresentationDeltaTime);
            var ids = new List<ulong>(_strikeCooldown.Keys);
            for (var i = 0; i < ids.Count; i++)
                _strikeCooldown[ids[i]] = Mathf.Max(0f, _strikeCooldown[ids[i]] - dt);
            foreach (var e in world.Entities.All)
                if (e.TryGet<NpcAutonomyState>(out var clocks) &&
                    LocalMapVisibility.IsEntityVisible(world, e.Id)) clocks.AdvanceCooldowns(dt);
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + ScanInterval;
                Scan(world, spawner);
            }
            var manual = _host.GetComponent<HostNpcMeleeAssault>();
            foreach (var actor in _visible)
            {
                if (!_targets.TryGetValue(actor.Value, out var target) || target.IsNone ||
                    _explicitStop.Contains(actor.Value) || HasPlayerAction(world, actor) ||
                    (_retreatingUntil.TryGetValue(actor.Value, out var until) && Time.unscaledTime < until) ||
                    manual?.IsInFight(actor) == true ||
                    world.Strategic.PlayerPartyContext?.ActiveCharacterId == actor ||
                    !world.Entities.TryGet(actor, out var actorEntity) ||
                    !world.Entities.TryGet(target, out var targetEntity) ||
                    !CombatLifeStateService.CanFight(actorEntity) ||
                    (actorEntity.TryGet<NpcAutonomyState>(out var retreatState) && retreatState.Retreating) ||
                    !CombatLifeStateService.CanFight(targetEntity) ||
                    !IsLegalTarget(world, actor, target) ||
                    !spawner.Registry.TryGet(actor, out var actorView) || actorView == null ||
                    !spawner.Registry.TryGet(target, out var targetView) || targetView == null) continue;
                if (!SameBattleSpace(world, actor, target) || PartyRestService.BlocksMovement(actorEntity) ||
                    !HasClearSight(actorView.transform.position, targetView.transform.position)) continue;
                if (world.Strategic.PlayerPartyContext?.IsMember(actor) == true ||
                    world.Strategic.PlayerPartyContext?.IsMember(target) == true)
                {
                    if (!SeparateSpaceCombatPolicy.AreBothInActiveSeparateSpace(world, actor, target))
                    {
                        _host.GetComponent<HostCharacterEncounter>()?.Request(actor, target, automatic: true);
                        return;
                    }
                }
                if (MortalCivilianService.ReactToHostileContact(world, target)) continue;
                if (MortalTransportConstraintService.ValidateAttack(world, actor).IsFailure) continue;
                // Civilians remain in their needs/flee authority; hostile content can explicitly opt in.
                if (MortalCivilianQuery.IsManagedCivilian(world, actorEntity) &&
                    (!actorEntity.TryGet<XianXia.Core.Social.PersonalityProfileComponent>(out var profile) ||
                     !profile.HasTag("hostile"))) continue;
                if (TryRetreatOutdoor(world, actor, target, actorEntity, targetEntity,
                        actorView, targetView)) continue;
                _veil.TryAutoActivateForNonPlayer(world, actor);
                _veil.DeactivateIfSpiritEmpty(actorEntity);
                var range = _veil.ResolveEngageRange(actorEntity);
                var distance = Vector2.Distance(actorView.transform.position, targetView.transform.position);
                if (distance > range || !_host.MoveController.IsCharacterStandClear(actor))
                {
                    if (!_strikeCooldown.TryGetValue(actor.Value, out var chaseCd) || chaseCd <= 0f)
                    {
                        _host.MoveController.OrderCharacterApproach(actor, target, range);
                        _strikeCooldown[actor.Value] = .2f;
                    }
                    continue;
                }
                _host.MoveController.CancelPresentationMovementPublic(actor);
                var persistent = NpcAutonomyState.Ensure(actorEntity);
                if (persistent.StrikeCooldown > 0f) continue;
                if (TryCastArt(world, actor, target, actorView, targetView)) continue;
                persistent.StrikeCooldown = MeleeCombatService.DefaultMeleeIntervalSeconds;
                if (SpiritVeilService.IsActive(actorEntity))
                {
                    var shotWorld = world;
                    _host.GetComponent<HostMeleeStrikeVfx>()?.PlayRangedBetween(spawner, actor, target, () =>
                    {
                        if (_host?.Session?.World != shotWorld ||
                            !shotWorld.Entities.TryGet(actor, out var shooter) ||
                            !shotWorld.Entities.TryGet(target, out var victim) ||
                            !CombatLifeStateService.CanFight(shooter) ||
                            !CombatLifeStateService.CanFight(victim) ||
                            !IsLegalTarget(shotWorld, actor, target) ||
                            !SameBattleSpace(shotWorld, actor, target)) return false;
                        var hit = _melee.ApplyStrike(shotWorld, actor, target, out _, out var defeated);
                        if (hit.IsSuccess && defeated) RefreshDown(target);
                        _host.DispatchDrainedEvents();
                        return hit.IsSuccess;
                    });
                }
                else
                {
                    var hit = _melee.ApplyStrike(world, actor, target, out _, out var defeated);
                    if (hit.IsSuccess)
                    {
                        _host.GetComponent<HostMeleeStrikeVfx>()?.PlayBetween(spawner, actor, target);
                        if (defeated) RefreshDown(target);
                        _host.DispatchDrainedEvents();
                    }
                }
            }
        }

        void Scan(SimulationWorld world, EntityViewSpawner spawner)
        {
            _previousAutonomousActors.Clear();
            foreach (var previous in _targets)
                _previousAutonomousActors.Add(new EntityId(previous.Key));
            _visible.Clear(); _targets.Clear();
            foreach (var view in spawner.Registry.All)
                if (view != null && LocalMapVisibility.IsEntityVisible(world, view.EntityId) &&
                    world.Entities.TryGet(view.EntityId, out var e) &&
                    (e.Tags & (EntityTag.Character | EntityTag.Npc)) != 0 &&
                    CombatLifeStateService.CanFight(e))
                    _visible.Add(view.EntityId);
            _visible.Sort((a, b) => a.Value.CompareTo(b.Value));
            var manual = _host.GetComponent<HostNpcMeleeAssault>();
            var active = world.Strategic.PlayerPartyContext?.ActiveCharacterId ?? EntityId.None;
            for (var i = 0; i < _visible.Count; i++)
            {
                var actor = _visible[i];
                if (actor == active || _explicitStop.Contains(actor.Value) ||
                    (_retreatingUntil.TryGetValue(actor.Value, out var until) && Time.unscaledTime < until) ||
                    HasPlayerAction(world, actor) || manual?.IsInFight(actor) == true ||
                    !spawner.Registry.TryGet(actor, out var actorView)) continue;
                var best = EntityId.None;
                var score = float.PositiveInfinity;
                for (var j = 0; j < _visible.Count; j++)
                {
                    var target = _visible[j];
                    if (!IsLegalTarget(world, actor, target) ||
                        !SameBattleSpace(world, actor, target) ||
                        (!SeparateSpaceCombatPolicy.IsInPlaceCombatSpace(world) &&
                         CharacterEncounterService.IsContactSuppressed(world, actor, target)) ||
                        !spawner.Registry.TryGet(target, out var targetView)) continue;
                    var dist = (actorView.transform.position - targetView.transform.position).sqrMagnitude;
                    if (dist > NoticeRange * NoticeRange ||
                        !HasClearSight(actorView.transform.position, targetView.transform.position)) continue;
                    var priority = dist;
                    if (world.Strategic.PlayerPartyContext?.IsMember(actor) == true &&
                        manual != null && manual.IsAttacker(active) && manual.DefenderId == target)
                        priority -= 1000f;
                    else if (world.Strategic.PlayerPartyContext?.IsMember(actor) == true &&
                             spawner.Registry.TryGet(active, out var activeView) && activeView != null)
                        priority = (targetView.transform.position - activeView.transform.position).sqrMagnitude;
                    if (best.IsNone || priority < score)
                    { best = target; score = priority; }
                }
                if (!best.IsNone) _targets[actor.Value] = best;
            }
            // Once the scan loses contact, the old chase must not keep walking on its own.
            // Explicit player actions and ongoing retreat/manual combat retain their movement.
            foreach (var actor in _previousAutonomousActors)
                if (!_targets.ContainsKey(actor.Value) && actor != active &&
                    !HasPlayerAction(world, actor) && manual?.IsInFight(actor) != true &&
                    (!_retreatingUntil.TryGetValue(actor.Value, out var until) || Time.unscaledTime >= until) &&
                    (!world.Entities.TryGet(actor, out var entity) ||
                     !entity.TryGet<NpcAutonomyState>(out var state) || !state.Retreating))
                    _host.MoveController.CancelPresentationMovementPublic(actor);
        }

        static bool SameBattleSpace(SimulationWorld world, EntityId a, EntityId b)
        {
            if (SeparateSpaceCombatPolicy.IsInPlaceCombatSpace(world))
                return SeparateSpaceCombatPolicy.AreBothInActiveSeparateSpace(world, a, b);
            return CharacterWorldPresenceQuery.TryResolve(world, a, out var pa) &&
                   CharacterWorldPresenceQuery.TryResolve(world, b, out var pb) &&
                   pa.HasWorldPosition && pb.HasWorldPosition && pa.SurfaceId == pb.SurfaceId;
        }

        static bool IsLegalTarget(SimulationWorld world, EntityId actor, EntityId target)
        {
            if (world.Civilians.TryGet(target, out var civilian) &&
                civilian.Disposition == CivilianDisposition.SurrenderWaiting) return false;
            if (world.Entities.TryGet(actor, out var actorEntity) &&
                world.Entities.TryGet(target, out var targetEntity) &&
                actorEntity.TryGet<XianXia.Core.Social.FactionMembershipComponent>(out var aFaction) &&
                targetEntity.TryGet<XianXia.Core.Social.FactionMembershipComponent>(out var bFaction) &&
                !string.IsNullOrEmpty(aFaction.FactionId) &&
                string.Equals(aFaction.FactionId, bFaction.FactionId, StringComparison.Ordinal)) return false;
            if (NpcHostilityService.CanAutoAttack(world, actor, target)) return true;
            var party = world.Strategic.PlayerPartyContext;
            var active = party?.ActiveCharacterId ?? EntityId.None;
            return party?.IsMember(actor) == true && party.IsMember(target) != true &&
                   !active.IsNone &&
                   (NpcHostilityService.CanAutoAttack(world, target, active) ||
                    NpcHostilityService.HasPrivateHostility(world, active, target));
        }

        static bool HasPlayerAction(SimulationWorld world, EntityId id)
        {
            if (world.Strategic.PlayerPartyContext?.IsMember(id) != true) return false;
            if (world.GetOrCreateOrderQueue(id).HasSource(XianXia.Core.Orders.OrderSource.Player)) return true;
            return world.Entities.TryGet(id, out var entity) &&
                   entity.TryGet<ActionStateComponent>(out var state) && state.HasActiveAction &&
                   state.ActiveOrderSource == XianXia.Core.Orders.OrderSource.Player;
        }

        bool TryRetreatOutdoor(SimulationWorld world, EntityId actor, EntityId target,
            Entity actorEntity, Entity targetEntity, EntityView actorView, EntityView targetView)
        {
            if (SeparateSpaceCombatPolicy.IsInPlaceCombatSpace(world) ||
                world.Strategic.PlayerPartyContext?.IsMember(actor) == true ||
                SquadCommandService.OwnsIndividualSchedule(world, actor) ||
                SquadWorldMotionService.OwnsCharacter(world, actor) ||
                !actorEntity.TryGet<AttributesComponent>(out var attrs) ||
                actorEntity.TryGet<XianXia.Core.Social.PersonalityProfileComponent>(out var profile) &&
                (profile.HasTag("fanatic") || profile.HasTag("fight_to_death"))) return false;
            CombatDamageRules.EnsureVitals(actorEntity);
            CombatDamageRules.EnsureVitals(targetEntity);
            if (!actorEntity.TryGet<CombatVitalsComponent>(out var hp) ||
                !targetEntity.TryGet<CombatVitalsComponent>(out var targetHp) ||
                !targetEntity.TryGet<AttributesComponent>(out var targetAttrs)) return false;
            var max = Mathf.Max(1, attrs.GetFinal(AttributeId.MaxHp));
            var targetMax = Mathf.Max(1, targetAttrs.GetFinal(AttributeId.MaxHp));
            if (hp.CurrentHp * 100 > max * 30 ||
                hp.CurrentHp * targetMax >= targetHp.CurrentHp * max) return false;
            var away = actorView.transform.position - targetView.transform.position;
            if (away.sqrMagnitude < .01f) away = actor.Value < target.Value ? Vector3.left : Vector3.right;
            away.Normalize();
            var destination = actorView.transform.position + away * 4f;
            destination.z = HostPresentationSpace.EntityZ;
            if (!_host.MoveController.OrderEntityToWorldPoint(actor, destination, null,
                    issueStop: false, completionPolicy: HostMoveCompletionPolicy.PreserveCurrentCommand))
                return false;
            var surface = _host.ContinuousOutdoorSurfaceRuntime;
            if (surface != null && surface.IsActive &&
                CharacterWorldPresenceQuery.TryResolve(world, actor, out var presence) && presence.HasWorldPosition)
            {
                surface.Mapper.PresentationToWorld(destination.x, destination.y, out var rx, out var ry);
                var state = NpcAutonomyState.Ensure(actorEntity);
                state.Retreating = true; state.RetreatSurface = presence.SurfaceId;
                state.RetreatX = rx; state.RetreatY = ry;
                PersonalLifeService.Ensure(world, actorEntity);
            }
            _retreatingUntil[actor.Value] = Time.unscaledTime + 10f;
            world.Strategic.SuppressedCharacterContacts.Add(CharacterEncounterService.ContactKey(actor, target));
            if (actorEntity.TryGet<PersonalLifeComponent>(out var personal))
            { personal.Activity = PersonalLifeActivity.None; personal.Status = "负伤撤离"; }
            return true;
        }

        bool TryCastArt(SimulationWorld world, EntityId actor, EntityId target,
            EntityView actorView, EntityView targetView)
        {
            if (!world.Entities.TryGet(actor, out var entity) ||
                !entity.TryGet<CombatArtsComponent>(out var arts) ||
                Vector2.Distance(actorView.transform.position, targetView.transform.position) > 3.5f)
                return false;
            var cooldowns = NpcAutonomyState.Ensure(entity);
            for (var slot = 0; slot < CombatArtsComponent.MaxEquippedSlots; slot++)
            {
                if (cooldowns.GetArt(slot) > .05f) continue;
                var id = arts.GetEquipped(slot);
                if (!id.HasValue || !world.TryGetCombatArt(id.Value, out var art) ||
                    art == null || !art.IsActiveSkill) continue;
                var cast = _melee.CastEquippedArt(world, actor, target, slot, out _, out _, out var defeated);
                if (cast.IsFailure) continue;
                cooldowns.SetArt(slot, art.CooldownSeconds > 0f ? art.CooldownSeconds : 2f);
                cooldowns.StrikeCooldown = MeleeCombatService.DefaultMeleeIntervalSeconds;
                _host.GetComponent<HostMeleeStrikeVfx>()?.PlaySkillBetween(_host.ViewSpawner, actor, target);
                if (defeated) RefreshDown(target);
                _host.DispatchDrainedEvents();
                return true;
            }
            return false;
        }

        bool HasClearSight(Vector3 from, Vector3 to)
        {
            var grid = _host?.MoveController?.WalkGrid;
            return grid != null && GridPathfinder.IsWorldSegmentWalkable(grid, from.x, from.y, to.x, to.y);
        }

        void RefreshDown(EntityId id) => HostLifeStatePresentationSync.RefreshDownedOrDead(
            _host?.Session?.World, _host?.ViewSpawner, _host?.MoveController, id,
            captureOrdinaryPlacement: true);
    }
}
