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
            if (!_trails.TryGetValue(army.ArmyId, out var trail))
                _trails[army.ArmyId] = trail = new Trail();
            AppendTrail(trail.Points, army.WorldMotion.WorldPosition);

            _members.Clear();
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
                _members.Add(army.MemberCharacterIds[i]);
            _members.Sort();

            var nav = _bootstrap.Session.World.SurfaceGround.TryGet(
                army.WorldMotion.SurfaceId, out var surface) ? surface :
                _bootstrap.Session.World.SurfaceGround.Active;
            var spacing = nav != null ? Mathf.Max(.05f, nav.CellSize * 1.5f) : .35f;
            for (var slot = 0; slot < _members.Count; slot++)
            {
                var id = new EntityId(_members[slot]);
                if (_bootstrap.Session.World.Strategic.Participants.FindByEntity(id) != null)
                    continue;
                var position = SampleBehind(army.WorldMotion, trail.Points, slot * spacing);
                if (slot > 0 && nav != null &&
                    WorldVec2.Distance(position, army.WorldMotion.WorldPosition) < .01f)
                    position = ResolveLocalConnectedSlot(
                        nav, army.WorldMotion.WorldPosition, slot, spacing);
                if (!runtime.TryWorldToPresentation(position, out var presentation) ||
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

        static WorldVec2 SampleBehind(
            FormalArmyWorldMotion motion,
            List<WorldVec2> trail,
            float distance)
        {
            var current = motion.WorldPosition;
            if (distance <= 0f) return current;
            for (var i = trail.Count - 2; i >= 0; i--)
            {
                var previous = trail[i];
                var length = WorldVec2.Distance(current, previous);
                if (length >= distance && length > .0001f)
                {
                    var t = distance / length;
                    return new WorldVec2(current.X + (previous.X - current.X) * t,
                        current.Y + (previous.Y - current.Y) * t);
                }
                distance -= length;
                current = previous;
            }

            // First materialization/read after load has no transient trail. Walk backwards over
            // the already validated owned route; never invent a lateral offset across blockers.
            var path = motion.SurfacePath;
            for (var i = Math.Min(motion.SurfaceWaypointIndex - 1, path.Count - 1); i >= 0; i--)
            {
                var previous = path[i];
                var length = WorldVec2.Distance(current, previous);
                if (length >= distance && length > .0001f)
                {
                    var t = distance / length;
                    return new WorldVec2(current.X + (previous.X - current.X) * t,
                        current.Y + (previous.Y - current.Y) * t);
                }
                distance -= length;
                current = previous;
            }
            return current;
        }

        static WorldVec2 ResolveLocalConnectedSlot(
            XianXia.Core.World.Surface.SurfaceGroundNavigation navigation,
            WorldVec2 anchor,
            int stableSlot,
            float spacing)
        {
            // Deterministic bounded local search. A candidate must have a directly walkable
            // segment to the anchor, preventing first-display slots from crossing water/walls.
            for (var ring = 1; ring <= 4; ring++)
                for (var offset = 0; offset < 8; offset++)
                {
                    var direction = (stableSlot + offset) & 7;
                    var angle = direction * Mathf.PI * .25f;
                    var radius = spacing * ring;
                    var candidate = new WorldVec2(
                        anchor.X + Mathf.Cos(angle) * radius,
                        anchor.Y + Mathf.Sin(angle) * radius);
                    if (navigation.IsWalkable(candidate.X, candidate.Y) &&
                        navigation.IsSegmentWalkable(
                            anchor.X, anchor.Y, candidate.X, candidate.Y))
                        return candidate;
                }
            return anchor;
        }
    }
}
