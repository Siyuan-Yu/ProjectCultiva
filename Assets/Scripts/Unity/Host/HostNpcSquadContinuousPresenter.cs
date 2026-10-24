using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Npc;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Near-field view projection for modern NPC SquadWorldMotion authority.</summary>
    public sealed class HostNpcSquadContinuousPresenter : MonoBehaviour
    {
        readonly List<ulong> _members = new List<ulong>(16);
        readonly HashSet<EntityView> _gaitViews = new HashSet<EntityView>();
        readonly HashSet<EntityView> _eligibleGaitViews = new HashSet<EntityView>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        readonly HashSet<string> _rejectedPlayerSquads = new HashSet<string>();
#endif
        PlayableHostBootstrap _bootstrap;
        ulong _lastTick = ulong.MaxValue;
        object _lastWorld;
        float _unpausedClock;
        float _lastProjectionClock;

        public void Bind(PlayableHostBootstrap bootstrap)
        {
            ClearProjectionTracking();
            _bootstrap = bootstrap;
        }

        void OnDisable() => ClearProjectionTracking();

        void ClearProjectionTracking()
        {
            foreach (var view in _gaitViews)
                if (view != null) view.ClearProjectedLocomotion();
            _gaitViews.Clear();
            _eligibleGaitViews.Clear();
            _lastTick = ulong.MaxValue;
            _lastWorld = null;
            _unpausedClock = 0f;
            _lastProjectionClock = 0f;
        }

        void Update()
        {
            using var timing = HostDebugHud.NpcSquadUpdate.Auto();
            var session = _bootstrap?.Session;
            var runtime = _bootstrap?.ContinuousOutdoorSurfaceRuntime;
            if (session == null || !session.IsInitialized || runtime == null || !runtime.IsActive)
            {
                ClearProjectionTracking();
                return;
            }
            var world = session.World;
            if (!ReferenceEquals(_lastWorld, world))
            {
                ClearProjectionTracking();
                _lastWorld = world;
            }
            if (!session.IsPaused) _unpausedClock += Time.unscaledDeltaTime;
            var projectPositions = _lastTick != world.Tick.Value;
            var firstProjection = _lastTick == ulong.MaxValue;
            // Paused wall-clock time must not stretch the first resumed gait segment.
            var projectionSeconds = Mathf.Clamp(_unpausedClock - _lastProjectionClock,
                .05f, Mathf.Max(.05f, _bootstrap.SecondsPerAutoTickAt1x));
            if (projectPositions)
            {
                _lastProjectionClock = _unpausedClock;
                _lastTick = world.Tick.Value;
            }
            _eligibleGaitViews.Clear();
            foreach (var pair in world.Strategic.SquadWorldMotions.Motions)
            {
                if (!world.Strategic.Squads.TryGet(pair.Key, out var squad)) continue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (SquadWorldMotionService.IsPlayerPartySquad(world, squad) &&
                    _rejectedPlayerSquads.Add(squad.SquadId))
                    Debug.LogWarning("Ignored stale PlayerParty SquadWorldMotion: " + squad.SquadId);
#endif
                if (!SquadWorldMotionService.IsActiveNpcSquadAuthority(world, squad, pair.Value) ||
                    !runtime.IsFieldSquadInLoadedNeighborhood(squad, pair.Value)) continue;
                _members.Clear();
                for (var i = 0; i < squad.MemberCharacterIds.Count; i++) _members.Add(squad.MemberCharacterIds[i]);
                _members.Sort();
                for (var slot = 0; slot < _members.Count; slot++)
                {
                    var id = new EntityId(_members[slot]);
                    if (world.Strategic.PlayerPartyContext?.IsMember(id) == true ||
                        !CharacterLifeStateQuery.IsLivingForMacroOrder(world, id) ||
                        ActualBattleParticipantQuery.TryFind(world.Strategic.Participants, id, out _) ||
                        (world.Civilians.TryGet(id, out var civilian) &&
                         (civilian.Disposition == CivilianDisposition.CapturedEscorted ||
                          MortalCivilianMovementAuthority.CanCivilianOwnLocalMovement(world, id, civilian))) ||
                        !_bootstrap.ViewSpawner.Registry.TryGet(id, out var view) || view == null) continue;
                    var presentation = Vector3.zero;
                    if (projectPositions && !runtime.TryResolveSquadMemberPresentationPosition(
                            squad, pair.Value, slot, null, out presentation)) continue;
                    HostCharacterVisualContext.ResolveCarry(world, id, out _, out _, out var carried);
                    var gaitEligible = view.IsBoundTo(world, id) && !carried &&
                        !CharacterEncounterService.OwnsParticipantSpatialState(world, id);
                    if (gaitEligible)
                    {
                        _eligibleGaitViews.Add(view);
                        if (projectPositions)
                        {
                            if (!firstProjection && !session.IsPaused && _gaitViews.Contains(view))
                                view.RecordProjectedLocomotion(presentation - view.transform.position, projectionSeconds);
                            else
                                view.ClearProjectedLocomotion();
                        }
                    }
                    // Eligibility is reconciled every frame, but root/Core projection remains
                    // the existing once-per-WorldTick operation. No visual position lag here.
                    if (!projectPositions) continue;
                    view.transform.position = presentation;
                    if (world.Entities.TryGet(id, out var entity))
                    {
                        if (!entity.TryGet<EntityLocationComponent>(out var location)) { location = new EntityLocationComponent(); entity.AddComponent(location); }
                        location.SetPresentationOverride(presentation.x, presentation.y);
                    }
                }
            }
            foreach (var view in _gaitViews)
                if (view != null && !_eligibleGaitViews.Contains(view))
                    view.ClearProjectedLocomotion();
            _gaitViews.Clear();
            foreach (var view in _eligibleGaitViews) _gaitViews.Add(view);
        }
    }
}
