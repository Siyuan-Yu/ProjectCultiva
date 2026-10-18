using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Navigation;
using XianXia.Core.Npc;

namespace XianXia.Unity.Host
{
    public sealed partial class HostMoveController
    {
        // Foot-space presentation clearance, deliberately independent of heads, sleeves and tools.
        const float CharacterStandSpacing = .8f;
        const float CrowdTransitWaitSeconds = .75f;
        const float StandRetrySeconds = .4f;
        static readonly float[] AvoidanceAngles = { -30f, 30f, -55f, 55f, -80f, 80f };

        sealed class FreeStandRequest
        {
            public Vector3 Center;
            public float Radius;
            public float RetryRemaining;
        }

        readonly Dictionary<ulong, Vector3> _characterDestinations = new Dictionary<ulong, Vector3>();
        readonly Dictionary<ulong, FreeStandRequest> _freeStandRequests =
            new Dictionary<ulong, FreeStandRequest>();
        readonly Dictionary<ulong, float> _crowdWait = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, int> _crowdWaitFrame = new Dictionary<ulong, int>();
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
            var scale = view.transform.lossyScale;
            return CharacterStandSpacing * Mathf.Max(.25f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
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
                if (other == null || other == actor || !other.gameObject.activeInHierarchy) continue;
                var spacing = (actorSpacing + StandSpacing(other)) * .5f;
                var delta = point - other.transform.position;
                delta.z = 0f;
                if (delta.sqrMagnitude < spacing * spacing - .0001f) return false;
                if (!includeDestinations ||
                    !_characterDestinations.TryGetValue(other.EntityId.Value, out var destination)) continue;
                delta = point - destination;
                delta.z = 0f;
                if (delta.sqrMagnitude < spacing * spacing - .0001f) return false;
            }
            return true;
        }

        /// <summary>Bounded, walkable and reachable presentation landing; never changes the supplied radius.</summary>
        public bool TryResolveFreeStandPoint(EntityId id, Vector3 center, float allowedRadius, out Vector3 resolved)
        {
            resolved = default;
            if (id.IsNone || _walkGrid == null || viewSpawner == null ||
                !viewSpawner.Registry.TryGet(id, out var view) || !IsCharacterOccupant(view) ||
                float.IsNaN(allowedRadius) || float.IsInfinity(allowedRadius) || allowedRadius < 0f ||
                float.IsNaN(center.x) || float.IsInfinity(center.x) ||
                float.IsNaN(center.y) || float.IsInfinity(center.y)) return false;
            center.z = HostPresentationSpace.EntityZ;
            if (TryStandCandidate(view, center, out resolved)) return true;
            var current = view.transform.position;
            current.z = HostPresentationSpace.EntityZ;
            if (Vector2.Distance(current, center) <= allowedRadius &&
                TryStandCandidate(view, current, out resolved)) return true;
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
                    if (TryStandCandidate(view, candidate, out resolved)) return true;
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

        public bool OrderCharacterApproach(EntityId actor, EntityId target, float range,
            HostMoveCompletionPolicy completionPolicy = HostMoveCompletionPolicy.HoldStandby) =>
            TryOrderCharacterApproach(actor, target, range, issueStop: false,
                completionPolicy: completionPolicy);

        bool TryOrderCharacterApproach(EntityId actor, EntityId target, float range, bool issueStop,
            bool preserveNpcIntent = false,
            HostMoveCompletionPolicy completionPolicy = HostMoveCompletionPolicy.HoldStandby)
        {
            if (actor.IsNone || target.IsNone || actor == target || range <= 0f ||
                float.IsNaN(range) || float.IsInfinity(range) || viewSpawner == null ||
                !viewSpawner.Registry.TryGet(actor, out var actorView) || actorView == null ||
                !viewSpawner.Registry.TryGet(target, out var targetView) || targetView == null) return false;
            if (Vector2.Distance(actorView.transform.position, targetView.transform.position) <= range &&
                IsCharacterStandClear(actor))
            {
                if (issueStop) StopOne(actor);
                else CancelPresentationMovement(actor);
                return true;
            }
            if (!TryResolveFreeStandPoint(actor, targetView.transform.position, range, out var stand)) return false;
            // Keep the original target/range for bounded in-place reallocation if this point is taken.
            if (!OrderEntityToWorldPoint(actor, stand, null, issueStop,
                    completionPolicy: completionPolicy, exactGoal: true,
                    preserveNpcIntent: preserveNpcIntent)) return false;
            _freeStandRequests[actor.Value] = new FreeStandRequest
            { Center = targetView.transform.position, Radius = range };
            return true;
        }

        void ReserveCharacterDestination(EntityId id, Vector3 destination, Vector3 center, float radius)
        {
            _characterDestinations[id.Value] = destination;
            if (radius > 0f)
                _freeStandRequests[id.Value] = new FreeStandRequest { Center = center, Radius = radius };
        }

        void RefreshFreeStandDestination(EntityView view, float deltaTime)
        {
            var id = view.EntityId.Value;
            if (!_freeStandRequests.TryGetValue(id, out var request) ||
                !_characterDestinations.TryGetValue(id, out var destination) ||
                IsCharacterStandClearAt(view, destination, includeDestinations: true)) return;
            request.RetryRemaining -= deltaTime;
            if (request.RetryRemaining > 0f) return;
            request.RetryRemaining = StandRetrySeconds;
            if (!TryResolveFreeStandPoint(view.EntityId, request.Center, request.Radius, out var replacement) ||
                !TryBuildWorldPath(view.transform.position, replacement, _standPathScratch, exactGoal: true)) return;
            // Preserve command callbacks, completion policy, map binding and the source ownership revision.
            var path = new List<Vector3>(_standPathScratch);
            _paths[id] = path;
            _pathIndex[id] = 0;
            _targets[view] = path[0];
            _characterDestinations[id] = path[path.Count - 1];
        }

        Vector3 FilterCharacterStep(EntityView view, Vector3 from, Vector3 proposed, Vector3 waypoint,
            float deltaTime, bool settling, bool requireWaypointConnection = true)
        {
            if (deltaTime <= 0f) return from;
            var delta = proposed - from;
            delta.z = 0f;
            var length = delta.magnitude;
            if (length <= .00001f)
                return !settling || IsCharacterStandClearAt(view, proposed) ? proposed : from;
            var direction = delta / length;
            var lookahead = Mathf.Max(length, StandSpacing(view) * .9f);
            if (requireWaypointConnection)
                lookahead = Mathf.Min(lookahead, Mathf.Max(length, Vector2.Distance(from, waypoint)));
            if (IsSpacingStepNavigable(from, proposed, waypoint, requireWaypointConnection) &&
                IsCharacterStepClear(view, from, proposed, direction * lookahead, settling))
            {
                _crowdWait.Remove(view.EntityId.Value);
                return proposed;
            }
            _crowdWait.TryGetValue(view.EntityId.Value, out var waited);
            // Accumulate once per frame's movement budget, not on every passed path corner.
            if (!_crowdWaitFrame.TryGetValue(view.EntityId.Value, out var waitFrame) ||
                waitFrame != Time.frameCount)
            {
                if (Time.frameCount - waitFrame > 1) waited = 0f;
                waited += Mathf.Min(deltaTime, .1f);
                _crowdWaitFrame[view.EntityId.Value] = Time.frameCount;
            }
            _crowdWait[view.EntityId.Value] = waited;
            if (!settling)
            {
                for (var i = 0; i < AvoidanceAngles.Length; i++)
                {
                    var turn = Quaternion.Euler(0f, 0f, AvoidanceAngles[i]) * direction;
                    var candidate = from + turn * length;
                    if (!IsSpacingStepNavigable(from, candidate, waypoint, requireWaypointConnection) ||
                        !IsCharacterStepClear(view, from, candidate, turn * lookahead, settling: false)) continue;
                    _crowdWait.Remove(view.EntityId.Value);
                    return candidate;
                }
                var slow = from + delta * .3f;
                if (IsSpacingStepNavigable(from, slow, waypoint, requireWaypointConnection) &&
                    IsCharacterStepClear(view, from, slow, slow - from, settling: false)) return slow;
                // Soft bodies can cross a narrow corridor after yielding. They still cannot finish overlapped.
                if (waited >= CrowdTransitWaitSeconds &&
                    _characterDestinations.TryGetValue(view.EntityId.Value, out var final) &&
                    Vector2.Distance(proposed, final) > StandSpacing(view) &&
                    IsSpacingStepNavigable(from, proposed, waypoint, requireWaypointConnection)) return proposed;
            }
            return from;
        }

        bool IsSpacingStepNavigable(Vector3 from, Vector3 candidate, Vector3 waypoint, bool requireConnection)
        {
            return _walkGrid != null &&
                GridPathfinder.IsWorldSegmentWalkable(_walkGrid, from.x, from.y, candidate.x, candidate.y) &&
                (!requireConnection || GridPathfinder.IsWorldSegmentWalkable(
                    _walkGrid, candidate.x, candidate.y, waypoint.x, waypoint.y));
        }

        bool IsCharacterStepClear(EntityView actor, Vector3 from, Vector3 to, Vector3 prediction, bool settling)
        {
            if (settling && !IsCharacterStandClearAt(actor, to)) return false;
            RefreshCharacterOccupants();
            var predicted = from + prediction;
            for (var i = 0; i < _characterOccupants.Count; i++)
            {
                var other = _characterOccupants[i];
                if (other == null || other == actor || !other.gameObject.activeInHierarchy) continue;
                var point = other.transform.position;
                var spacing = (StandSpacing(actor) + StandSpacing(other)) * .5f;
                var initial = Vector2.Distance(from, point);
                var next = Vector2.Distance(to, point);
                // Existing overlaps can only improve through the actor's own legal motion.
                if (initial < spacing)
                {
                    if (next <= initial + .00001f) return false;
                    continue;
                }
                if (PointSegmentDistanceSquared(point, from, predicted) < spacing * spacing - .0001f)
                    return false;
            }
            return true;
        }

        static float PointSegmentDistanceSquared(Vector3 point, Vector3 from, Vector3 to)
        {
            var line = (Vector2)(to - from);
            var offset = (Vector2)(point - from);
            var t = line.sqrMagnitude > .0000001f ? Mathf.Clamp01(Vector2.Dot(offset, line) / line.sqrMagnitude) : 0f;
            return (offset - line * t).sqrMagnitude;
        }

        void ReleaseCharacterDestination(EntityId id, bool preserveCrowdWait = false)
        {
            _characterDestinations.Remove(id.Value);
            _freeStandRequests.Remove(id.Value);
            // Repath throttles may be shorter than the transit yield. Replacing the same
            // actor's path must not reset its wait forever; actual cancellation still does.
            if (!preserveCrowdWait)
            {
                _crowdWait.Remove(id.Value);
                _crowdWaitFrame.Remove(id.Value);
            }
        }

        void ResetCharacterSpacing()
        {
            _characterDestinations.Clear();
            _freeStandRequests.Clear();
            _crowdWait.Clear();
            _crowdWaitFrame.Clear();
            _characterOccupants.Clear();
            _characterOccupantsFrame = -1;
            _characterOccupantsWorld = null;
        }
    }
}
