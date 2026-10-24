using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Navigation;
using XianXia.Core.Npc;
using XianXia.Core.World.Surface;

namespace XianXia.Unity.Host
{
    public sealed partial class HostMoveController
    {
        // Foot-space presentation clearance, deliberately independent of heads, sleeves and tools.
        readonly Dictionary<ulong, Vector3> _characterDestinations = new Dictionary<ulong, Vector3>();
        readonly List<EntityView> _characterOccupants = new List<EntityView>(64);
        readonly List<Vector3> _standPathScratch = new List<Vector3>(32);
        int _characterOccupantsFrame = -1;
        object _characterOccupantsWorld;

        static int CompareCharacterMoveViews(EntityView left, EntityView right)
        {
            if (left == null) return right == null ? 0 : 1;
            return right == null ? -1 : left.EntityId.Value.CompareTo(right.EntityId.Value);
        }

        float StandSpacing(EntityView view)
        {
            return (_walkGrid != null ? _walkGrid.CellSize : 1f) * SurfaceCellGeometry.CharacterWidthCells;
        }

        bool IsCharacterSleeping(EntityId id)
        {
            var world = bootstrap?.Session?.World;
            if (world == null) return false;
            if (world.Civilians.TryGet(id, out var civilian) &&
                (civilian.SleepPhase == CivilianSleepPhase.SleepingAtResidence ||
                 civilian.SleepPhase == CivilianSleepPhase.SleepingOnGround)) return true;
            return world.Entities.TryGet(id, out var entity) &&
                entity.TryGet<ActionStateComponent>(out var state) && state.HasActiveAction &&
                world.ActiveActions.TryGetValue(state.ActiveActionId, out var active) &&
                active is BedRestAction rest && rest.Status == ActionStatus.Running;
        }

        bool IsCharacterOccupant(EntityView view)
        {
            if (view == null || !view.IsBound || !view.gameObject.activeInHierarchy ||
                !view.TryGetVisibleBodyBounds(out _)) return false;
            var world = bootstrap?.Session?.World;
            if (world == null || !world.Entities.TryGet(view.EntityId, out var entity) ||
                (entity.Tags & (EntityTag.Character | EntityTag.Npc)) == 0 ||
                !CombatLifeStateService.CanFight(entity) ||
                !LocalMapVisibility.IsEntityVisible(world, view.EntityId)) return false;
            return !IsTransportAttached(view.EntityId, out _) && !IsCharacterSleeping(view.EntityId);
        }

        void RefreshCharacterOccupants()
        {
            var world = bootstrap?.Session?.World;
            if (_characterOccupantsFrame == Time.frameCount &&
                ReferenceEquals(_characterOccupantsWorld, world)) return;
            _characterOccupantsFrame = Time.frameCount;
            _characterOccupantsWorld = world;
            _characterOccupants.Clear();
            if (viewSpawner == null || world == null) return;
            foreach (var view in viewSpawner.Registry.All)
                if (IsCharacterOccupant(view)) _characterOccupants.Add(view);
            _characterOccupants.Sort(CompareCharacterMoveViews);
        }

        /// <summary>Current visible living feet only; a future reservation never pushes a standing person.</summary>
        public bool IsCharacterStandClear(EntityId id)
        {
            return viewSpawner != null && viewSpawner.Registry.TryGet(id, out var view) &&
                view != null && IsCharacterStandClearAt(view, view.transform.position);
        }

        bool IsCharacterStandClearAt(EntityView actor, Vector3 point, bool includeDestinations = false)
        {
            RefreshCharacterOccupants();
            var actorSpacing = StandSpacing(actor);
            for (var i = 0; i < _characterOccupants.Count; i++)
            {
                var other = _characterOccupants[i];
                if (other == null || other == actor || !other.gameObject.activeInHierarchy ||
                    IsFlying(actor.EntityId) != IsFlying(other.EntityId)) continue;
                var spacing = (actorSpacing + StandSpacing(other)) * .5f;
                var delta = point - other.transform.position;
                delta.z = 0f;
                if (FeetOverlap(delta, spacing)) return false;
                if (!includeDestinations ||
                    !_characterDestinations.TryGetValue(other.EntityId.Value, out var destination)) continue;
                delta = point - destination;
                delta.z = 0f;
                if (FeetOverlap(delta, spacing)) return false;
            }
            return true;
        }

        /// <summary>Bounded, walkable and reachable presentation landing; never changes the supplied radius.</summary>
        public bool TryResolveFreeStandPoint(EntityId id, Vector3 center, float allowedRadius, out Vector3 resolved,
            bool requireClearSight = false)
        {
            resolved = default;
            if (id.IsNone || _walkGrid == null || viewSpawner == null ||
                !viewSpawner.Registry.TryGet(id, out var view) || !IsCharacterOccupant(view) ||
                float.IsNaN(allowedRadius) || float.IsInfinity(allowedRadius) || allowedRadius < 0f ||
                float.IsNaN(center.x) || float.IsInfinity(center.x) ||
                float.IsNaN(center.y) || float.IsInfinity(center.y)) return false;
            if (IsFlying(id) && IsAirPointAllowed(id, center)) { resolved = center; return true; }
            bool Accept(Vector3 point, out Vector3 result)
            {
                result = default;
                return (!requireClearSight || GridPathfinder.IsWorldSegmentWalkable(
                    _walkGrid, point.x, point.y, center.x, center.y)) && TryStandCandidate(view, point, out result);
            }
            center.z = HostPresentationSpace.EntityZ;
            if (Accept(center, out resolved)) return true;
            var current = view.transform.position;
            current.z = HostPresentationSpace.EntityZ;
            if (Vector2.Distance(current, center) <= allowedRadius &&
                Accept(current, out resolved)) return true;
            var away = current - center;
            var startAngle = away.sqrMagnitude > .0001f
                ? Mathf.Atan2(away.y, away.x) : (id.Value % 16) * Mathf.PI / 8f;
            // Three rings and sixteen directions bound both the search and its path-build work.
            for (var ring = 1; ring <= 3; ring++)
            {
                var radius = allowedRadius * ring / 3f;
                if (radius <= .0001f) continue;
                for (var slot = 0; slot < 16; slot++)
                {
                    var alternate = slot == 0 ? 0 : (slot + 1) / 2 * (slot % 2 == 1 ? 1 : -1);
                    var angle = startAngle + alternate * Mathf.PI / 8f;
                    var candidate = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                    if (Accept(candidate, out resolved)) return true;
                }
            }
            return false;
        }

        bool TryStandCandidate(EntityView view, Vector3 candidate, out Vector3 resolved)
        {
            resolved = default;
            if (!_walkGrid.TryWorldToCell(candidate.x, candidate.y, out var x, out var y) ||
                !_walkGrid.IsWalkable(x, y) || !IsCharacterStandClearAt(view, candidate, includeDestinations: true) ||
                !TryBuildWorldPath(view.transform.position, candidate, _standPathScratch, exactGoal: true)) return false;
            resolved = candidate;
            return true;
        }

        /// <summary>Same terrain/wall grid as movement; character foot reservations are not sight blockers.</summary>
        public bool HasProjectileSight(Vector3 from, Vector3 to) =>
            _walkGrid != null && GridPathfinder.IsWorldSegmentWalkable(_walkGrid, from.x, from.y, to.x, to.y);

        public bool HasCharacterSight(EntityId actor, EntityId target)
        {
            return _walkGrid != null && viewSpawner != null &&
                viewSpawner.Registry.TryGet(actor, out var from) && from != null &&
                viewSpawner.Registry.TryGet(target, out var to) && to != null &&
                ((IsFlying(actor) || IsFlying(target)) ? IsAirPointAllowed(actor, from.transform.position) && IsAirPointAllowed(target, to.transform.position) :
                GridPathfinder.IsWorldSegmentWalkable(_walkGrid, from.transform.position.x,
                    from.transform.position.y, to.transform.position.x, to.transform.position.y));
        }

        public bool OrderCharacterApproach(EntityId actor, EntityId target, float range,
            HostMoveCompletionPolicy completionPolicy = HostMoveCompletionPolicy.HoldStandby,
            bool requireClearSight = false) =>
            TryOrderCharacterApproach(actor, target, range, issueStop: false,
                completionPolicy: completionPolicy, requireClearSight: requireClearSight);

        bool TryOrderCharacterApproach(EntityId actor, EntityId target, float range, bool issueStop,
            bool preserveNpcIntent = false,
            HostMoveCompletionPolicy completionPolicy = HostMoveCompletionPolicy.HoldStandby,
            bool requireClearSight = false)
        {
            if (actor.IsNone || target.IsNone || actor == target || range <= 0f ||
                float.IsNaN(range) || float.IsInfinity(range) || viewSpawner == null ||
                !viewSpawner.Registry.TryGet(actor, out var actorView) || actorView == null ||
                !viewSpawner.Registry.TryGet(target, out var targetView) || targetView == null) return false;
            if (Vector2.Distance(actorView.transform.position, targetView.transform.position) <= range &&
                IsCharacterStandClear(actor) && (!requireClearSight ||
                 GridPathfinder.IsWorldSegmentWalkable(_walkGrid, actorView.transform.position.x,
                     actorView.transform.position.y, targetView.transform.position.x, targetView.transform.position.y)))
            {
                if (issueStop) StopOne(actor);
                else CancelPresentationMovement(actor);
                return true;
            }
            if (!TryResolveFreeStandPoint(actor, targetView.transform.position, range, out var stand, requireClearSight)) return false;
            // Keep the original target/range for bounded in-place reallocation if this point is taken.
            if (!OrderEntityToWorldPoint(actor, stand, null, issueStop,
                    completionPolicy: completionPolicy, exactGoal: true,
                    preserveNpcIntent: preserveNpcIntent)) return false;
            _characterDestinations[actor.Value] = stand;
            return true;
        }

        void ReserveCharacterDestination(EntityId id, Vector3 destination)
        {
            _characterDestinations[id.Value] = destination;
        }

        Vector3 FilterCharacterStep(EntityView actor, Vector3 from, Vector3 proposed, Vector3 waypoint,
            float deltaTime, bool requireWaypointConnection = true)
        {
            if (deltaTime <= 0f) return from;
            if (actor != null && IsFlying(actor.EntityId))
                return IsAirPointAllowed(actor.EntityId, proposed) && IsCharacterStepClear(actor, from, proposed) ? proposed : from;
            if (actor != null && !IsOpeningMoveAllowed(actor.EntityId, proposed)) return from;
            if (!IsSpacingStepNavigable(from, proposed, waypoint, requireWaypointConnection)) return from;
            if (actor == null || !IsCharacterOccupant(actor) ||
                IsCharacterStepClear(actor, from, proposed)) return proposed;
            var delta = proposed - from;
            delta.z = 0f;
            if (delta.sqrMagnitude < .000001f) return from;
            // A bounded side step lets two opposing walkers pass without claiming a
            // facility slot or changing the static route. The next frame resumes the route.
            var side = new Vector3(-delta.y, delta.x, 0f);
            var first = actor.EntityId.Value % 2 == 0 ? 1f : -1f;
            for (var i = 0; i < 2; i++)
            {
                var candidate = from + side * (i == 0 ? first : -first);
                candidate.z = HostPresentationSpace.EntityZ;
                if (IsOpeningMoveAllowed(actor.EntityId, candidate) &&
                    IsSpacingStepNavigable(from, candidate, waypoint, requireWaypointConnection) &&
                    IsCharacterStepClear(actor, from, candidate)) return candidate;
            }
            return from;
        }

        bool IsCharacterStepClear(EntityView actor, Vector3 from, Vector3 to)
        {
            RefreshCharacterOccupants();
            var travel = to - from;
            travel.z = 0f;
            for (var i = 0; i < _characterOccupants.Count; i++)
            {
                var other = _characterOccupants[i];
                if (other == null || other == actor || !other.gameObject.activeInHierarchy ||
                    IsFlying(actor.EntityId) != IsFlying(other.EntityId)) continue;
                var spacing = (StandSpacing(actor) + StandSpacing(other)) * .5f;
                var start = from - other.transform.position;
                var end = to - other.transform.position;
                start.z = end.z = 0f;
                // If a save or spawn already overlaps, allow a step out, never deeper in.
                if (FeetOverlap(start, spacing))
                {
                    if (Mathf.Max(Mathf.Abs(end.x), Mathf.Abs(end.y)) <=
                        Mathf.Max(Mathf.Abs(start.x), Mathf.Abs(start.y)) + .0001f) return false;
                    continue;
                }
                var enter = 0f; var exit = 1f;
                var extent = spacing - .0001f;
                if (ClipFootAxis(start.x, travel.x, extent, ref enter, ref exit) &&
                    ClipFootAxis(start.y, travel.y, extent, ref enter, ref exit)) return false;
            }
            return true;
        }

        static bool FeetOverlap(Vector3 delta, float width) =>
            Mathf.Abs(delta.x) < width - .0001f && Mathf.Abs(delta.y) < width - .0001f;

        static bool ClipFootAxis(float start, float delta, float extent, ref float enter, ref float exit)
        {
            if (Mathf.Abs(delta) < .000001f) return Mathf.Abs(start) < extent;
            var a = (-extent - start) / delta;
            var b = (extent - start) / delta;
            enter = Mathf.Max(enter, Mathf.Min(a, b));
            exit = Mathf.Min(exit, Mathf.Max(a, b));
            return enter <= exit;
        }

        bool IsSpacingStepNavigable(Vector3 from, Vector3 candidate, Vector3 waypoint, bool requireConnection)
        {
            return _walkGrid != null &&
                GridPathfinder.IsWorldSegmentWalkable(_walkGrid, from.x, from.y, candidate.x, candidate.y) &&
                (!requireConnection || GridPathfinder.IsWorldSegmentWalkable(
                    _walkGrid, candidate.x, candidate.y, waypoint.x, waypoint.y));
        }

        void ReleaseCharacterDestination(EntityId id)
        {
            _characterDestinations.Remove(id.Value);
        }

        void ResetCharacterSpacing()
        {
            _characterDestinations.Clear();
            _characterOccupants.Clear();
            _characterOccupantsFrame = -1;
            _characterOccupantsWorld = null;
        }
    }
}
