using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Near-field view driver for FormalArmy members. Domain travel remains world-tick owned;
    /// this component only places the one registered view per member along the army's validated
    /// trail. Loaded-neighborhood reconciliation is owned by ContinuousOutdoorSurfaceRuntime.
    /// </summary>
    public sealed class HostFormalArmyContinuousPresenter : MonoBehaviour
    {
        sealed class Trail
        {
            public readonly List<WorldVec2> Points = new List<WorldVec2>(64);
        }

        readonly Dictionary<string, Trail> _trails =
            new Dictionary<string, Trail>(StringComparer.Ordinal);
        readonly List<ulong> _members = new List<ulong>(16);
        PlayableHostBootstrap _bootstrap;
        ulong _lastTick = ulong.MaxValue;
        object _lastWorld;

        public void Bind(PlayableHostBootstrap bootstrap) => _bootstrap = bootstrap;

        void Update()
        {
            var session = _bootstrap?.Session;
            var runtime = _bootstrap?.ContinuousOutdoorSurfaceRuntime;
            if (session == null || !session.IsInitialized || runtime == null || !runtime.IsActive)
            {
                // Do not connect two observations with an unverified straight chord after the
                // army advanced off-screen. Re-entry reconstructs from the owned Surface route.
                _trails.Clear();
                _lastTick = ulong.MaxValue;
                return;
            }
            var world = session.World;
            if (!ReferenceEquals(_lastWorld, world))
            {
                _lastWorld = world;
                _lastTick = ulong.MaxValue;
                _trails.Clear();
            }
            if (_lastTick == world.Tick.Value) return;
            _lastTick = world.Tick.Value;

            foreach (var pair in world.Strategic.FormalArmies.Armies)
            {
                var army = pair.Value;
                if (army == null || !army.WorldMotion.HasPosition ||
                    army.State == FormalArmyState.Garrisoned ||
                    FormalArmyMemberPresenceSync.IsArmyEngaged(world, army))
                    continue;
                PresentArmy(runtime, army);
            }
        }

        void PresentArmy(ContinuousOutdoorSurfaceRuntime runtime, FormalArmy army)
        {
            if (!runtime.IsFieldFormalArmyInLoadedNeighborhood(army))
                return;
            if (!_trails.TryGetValue(army.ArmyId, out var trail))
                _trails[army.ArmyId] = trail = new Trail();
            AppendTrail(trail.Points, army.WorldMotion.WorldPosition);

            _members.Clear();
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
                _members.Add(army.MemberCharacterIds[i]);
            _members.Sort();

            for (var slot = 0; slot < _members.Count; slot++)
            {
                var id = new EntityId(_members[slot]);
                if (!LingeringBattlefieldPartyService.IsLivingForMacroOrder(_bootstrap.Session.World, id))
                    continue;
                if (ActualBattleParticipantQuery.TryFind(
                        _bootstrap.Session.World.Strategic.Participants, id, out _))
                    continue;
                if (!runtime.TryResolveFormalArmyMemberPresentationPosition(
                        army, slot, trail.Points, out var presentation) ||
                    !_bootstrap.ViewSpawner.Registry.TryGet(id, out var view) || view == null)
                    continue;
                view.transform.position = presentation;
                if (_bootstrap.Session.World.Entities.TryGet(id, out var entity))
                {
                    if (!entity.TryGet<EntityLocationComponent>(out var location))
                    {
                        location = new EntityLocationComponent();
                        entity.AddComponent(location);
                    }
                    location.SetPresentationOverride(presentation.x, presentation.y);
                }
            }
        }

        static void AppendTrail(List<WorldVec2> trail, WorldVec2 position)
        {
            if (trail.Count == 0 || WorldVec2.Distance(trail[trail.Count - 1], position) > .02f)
                trail.Add(position);
            const int maxTrailPoints = 96;
            if (trail.Count > maxTrailPoints)
                trail.RemoveRange(0, trail.Count - maxTrailPoints);
        }

    }

}
