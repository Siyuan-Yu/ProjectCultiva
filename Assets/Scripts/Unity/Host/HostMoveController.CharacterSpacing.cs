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
        readonly Dictionary<ulong, (Vector3 center, float radius)> _freeStandRequests =
            new Dictionary<ulong, (Vector3 center, float radius)>();
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
            // Ordinary residence sleep cannot own a body after tactical takeover.
            if (XianXia.Core.World.Strategic.CharacterEncounterService.OwnsParticipantSpatialState(world, id)) return false;
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
            return StillOccupiesFootSpace(view) &&
                LocalMapVisibility.IsEntityVisible(bootstrap.Session.World, view.EntityId);
        }

        // The frame snapshot already checked renderer bounds and visibility. Collision edges
        // only need to recheck mutable life/custody, not recompute visual bounds for every ray.
        bool StillOccupiesFootSpace(EntityView view)
        {
            if (view == null || !view.gameObject.activeInHierarchy) return false;
            var world = bootstrap?.Session?.World;
            if (world == null || !world.Entities.TryGet(view.EntityId, out var entity) ||
                (entity.Tags & (EntityTag.Character | EntityTag.Npc)) == 0 ||
                (!CombatLifeStateService.CanFight(entity) && !PrisonConfinementService.CanWalk(world, entity))) return false;
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
                if (other == null || other == actor || !StillOccupiesFootSpace(other) ||
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
                !viewSpawner.Registry.TryGet(id, out var view) || view == null || !view.IsBound ||
                !StillOccupiesFootSpace(view) ||
                float.IsNaN(allowedRadius) || float.IsInfinity(allowedRadius) || allowedRadius < 0f ||
                float.IsNaN(center.x) || float.IsInfinity(center.x) ||
                float.IsNaN(center.y) || float.IsInfinity(center.y)) return false;
            if (IsFlying(id))
            {
                if (IsAirPointAllowed(id, center) && IsCharacterStandClearAt(view, center, true))
                { resolved = center; return true; }
                // Use the same bounded landing search for occupied air destinations.
            }
            bool Accept(Vector3 point, out Vector3 result)
            {
                result = default;
                return (IsFlying(id) || !requireClearSight || GridPathfinder.IsWorldSegmentWalkable(
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
            if (!IsOpeningMoveAllowed(view.EntityId, candidate)) return false;
            if (IsFlying(view.EntityId))
            {
                if (!IsAirPointAllowed(view.EntityId, candidate) ||
                    !IsCharacterStandClearAt(view, candidate, includeDestinations: true)) return false;
                resolved = candidate;
                return true;
            }
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

        public string DescribeNavigationAvailability(EntityId actor, EntityId target)
        {
            var world = bootstrap?.Session?.World;
            if (world == null || viewSpawner == null) return "WorldOrSpawnerMissing";
            if (!viewSpawner.Registry.TryGet(actor, out var from) || from == null ||
                !viewSpawner.Registry.TryGet(target, out var to) || to == null) return "ViewMissing";
            if (!world.Entities.TryGet(actor, out var entity)) return "ActorMissing";
            if (!CombatLifeStateService.CanFight(entity)) return "ActorIncapacitated";
            if (IsTransportAttached(actor, out _)) return "ActorCarried";
            if (!IsFlying(actor) && !CombatInjuryService.CanWalk(entity)) return "InjuryPreventsWalking";
            if (PartyRestService.BlocksMovement(entity)) return "ExhaustionPreventsWalking";
            if (IsCharacterSleeping(actor)) return "OrdinarySleepBlocksMovement";
            if (_walkGrid == null) return "WalkGridMissing";
            if (!_walkGrid.TryWorldToCell(from.transform.position.x, from.transform.position.y, out var x, out var y))
                return "ActorOutsideWalkGrid";
            if (!_walkGrid.IsWalkable(x, y) && !IsFlying(actor)) return "ActorInBlockedCell";
            if (!_walkGrid.TryWorldToCell(to.transform.position.x, to.transform.position.y, out _, out _))
                return "TargetOutsideWalkGrid";
            return "NoReachableFreeAttackStand";
        }

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
            // Attack cooldown is not a path invalidation clock. Keep a reachable landing
            // while the target is still in its attack disk, including long routes via doors.
            if (!issueStop && _characterDestinations.TryGetValue(actor.Value, out var previousStand) &&
                Vector2.Distance(previousStand, targetView.transform.position) <= range &&
                IsCharacterStandClearAt(actorView, previousStand, includeDestinations: true) &&
                (!requireClearSight || IsFlying(actor) || GridPathfinder.IsWorldSegmentWalkable(
                    _walkGrid, previousStand.x, previousStand.y,
                    targetView.transform.position.x, targetView.transform.position.y)) &&
                IsMoving(actor) && IsRemainingPathValid(actor)) return true;
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
            if (actor != null && !IsOpeningMoveAllowed(actor.EntityId, proposed)) return from;
            if (actor != null && IsFlying(actor.EntityId))
                return IsAirPointAllowed(actor.EntityId, proposed) && IsCharacterStepClear(actor, from, proposed) ? proposed : from;
            if (!IsSpacingStepNavigable(from, proposed, waypoint, requireWaypointConnection)) return from;
            if (actor == null || !IsCharacterOccupant(actor) ||
                IsCharacterStepClear(actor, from, proposed)) return proposed;
            // Path followers insert a validated local detour instead of alternating left/right
            // every frame. Manual directional motion can still slide along another character.
            if (requireWaypointConnection) return from;
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
                if (other == null || other == actor || !StillOccupiesFootSpace(other) ||
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

        sealed class NavigationProgress
        {
            public float BestRemaining = float.PositiveInfinity;
            public float StalledSeconds;
            public float NextRetryAt;
        }

        readonly Dictionary<ulong, NavigationProgress> _navigationProgress = new Dictionary<ulong, NavigationProgress>();
        readonly List<Vector3> _detourNodes = new List<Vector3>(68);
        readonly List<Vector3> _detourRoute = new List<Vector3>(68);
        readonly float[] _detourCosts = new float[68];
        readonly int[] _detourParents = new int[68];
        readonly bool[] _detourClosed = new bool[68];
        int _navigationRetryFrame = -1;
        int _navigationRetriesThisFrame;

        bool NavigationEdgeClear(EntityView actor, Vector3 from, Vector3 to) =>
            IsOpeningMoveAllowed(actor.EntityId, to) &&
            (IsFlying(actor.EntityId) ? IsAirPointAllowed(actor.EntityId, to) :
                GridPathfinder.IsWorldSegmentWalkable(_walkGrid, from.x, from.y, to.x, to.y)) &&
            IsCharacterStepClear(actor, from, to);

        // Bounded visibility graph around nearby feet. The static A* route remains the
        // authority for wall/door routing; this graph rejoins its nearby remaining legs.
        bool TryInsertCharacterDetour(EntityView actor)
        {
            if (_walkGrid == null || !_paths.TryGetValue(actor.EntityId.Value, out var path) ||
                !_pathIndex.TryGetValue(actor.EntityId.Value, out var index) || index >= path.Count) return false;
            var from = actor.transform.position;
            var firstIndex = index;
            var remaining = _walkGrid.CellSize * 6f;
            var rejoin = from;
            // Rejoin beyond an occupied intermediate waypoint (for example a door corner),
            // not necessarily at that occupied point. Keep original wall-turn nodes available.
            while (index < path.Count - 1 && index - firstIndex < 12 &&
                   Vector3.Distance(rejoin, path[index]) < remaining)
            {
                remaining -= Vector3.Distance(rejoin, path[index]);
                rejoin = path[index++];
            }
            rejoin = Vector3.MoveTowards(rejoin, path[index], remaining);
            _detourNodes.Clear();
            _detourNodes.Add(from);
            _detourNodes.Add(rejoin);
            for (var i = firstIndex; i < index; i++)
                if (IsCharacterStandClearAt(actor, path[i])) _detourNodes.Add(path[i]);
            RefreshCharacterOccupants();
            for (var i = 0; i < _characterOccupants.Count && _detourNodes.Count <= 64; i++)
            {
                var other = _characterOccupants[i];
                if (other == actor || !StillOccupiesFootSpace(other) ||
                    IsFlying(actor.EntityId) != IsFlying(other.EntityId) ||
                    Vector2.Distance(from, other.transform.position) > _walkGrid.CellSize * 8f) continue;
                var clearance = (StandSpacing(actor) + StandSpacing(other)) * .5f + _walkGrid.CellSize * .06f;
                for (var corner = 0; corner < 4; corner++)
                {
                    var point = other.transform.position + new Vector3(
                        (corner % 2 == 0 ? -1f : 1f) * clearance,
                        (corner < 2 ? -1f : 1f) * clearance, 0f);
                    point.z = HostPresentationSpace.EntityZ;
                    if (!IsOpeningMoveAllowed(actor.EntityId, point) ||
                        !IsCharacterStandClearAt(actor, point)) continue;
                    if (IsFlying(actor.EntityId) ? !IsAirPointAllowed(actor.EntityId, point) :
                        !_walkGrid.TryWorldToCell(point.x, point.y, out var cx, out var cy) ||
                        !_walkGrid.IsWalkable(cx, cy)) continue;
                    _detourNodes.Add(point);
                }
            }
            var count = _detourNodes.Count;
            for (var i = 0; i < count; i++)
            { _detourCosts[i] = float.PositiveInfinity; _detourParents[i] = -1; _detourClosed[i] = false; }
            _detourCosts[0] = 0f;
            for (var iteration = 0; iteration < count; iteration++)
            {
                var current = -1;
                for (var i = 0; i < count; i++)
                    if (!_detourClosed[i] && !float.IsPositiveInfinity(_detourCosts[i]) &&
                        (current < 0 || _detourCosts[i] < _detourCosts[current])) current = i;
                if (current < 0) return false;
                if (current == 1) break;
                _detourClosed[current] = true;
                for (var next = 1; next < count; next++)
                {
                    if (_detourClosed[next]) continue;
                    var cost = _detourCosts[current] + Vector3.Distance(_detourNodes[current], _detourNodes[next]);
                    if (cost >= _detourCosts[next] ||
                        !NavigationEdgeClear(actor, _detourNodes[current], _detourNodes[next])) continue;
                    _detourCosts[next] = cost;
                    _detourParents[next] = current;
                }
            }
            if (_detourParents[1] < 0) return false;
            _detourRoute.Clear();
            for (var node = 1; node > 0; node = _detourParents[node]) _detourRoute.Add(_detourNodes[node]);
            _detourRoute.Reverse();
            // A direct edge is not a recovery and must not endlessly grow the same path.
            if (_detourRoute.Count <= 1 && NavigationEdgeClear(actor, from, path[firstIndex])) return false;
            if ((rejoin - path[index]).sqrMagnitude < .0001f) index++;
            path.RemoveRange(0, index);
            path.InsertRange(0, _detourRoute);
            _pathIndex[actor.EntityId.Value] = 0;
            _targets[actor] = path[0];
            ResetNavigationDistance(actor.EntityId, from);
            return true;
        }

        bool TryRebuildRemainingRoute(EntityView view)
        {
            if (!_paths.TryGetValue(view.EntityId.Value, out var old) || old.Count == 0 ||
                !TryBuildCharacterPath(view.EntityId, view.transform.position, old[old.Count - 1],
                    _wpScratch, exactGoal: true)) return false;
            for (var i = 0; i < _wpScratch.Count; i++)
                if (!IsOpeningMoveAllowed(view.EntityId, _wpScratch[i])) return false;
            old.Clear(); old.AddRange(_wpScratch);
            _pathIndex[view.EntityId.Value] = 0;
            _targets[view] = old[0];
            ResetNavigationDistance(view.EntityId, view.transform.position);
            // This is the same command: do not Stop, change its revision, release its
            // destination, or execute its arrival callback while repairing its route.
            return true;
        }

        bool TryRelocateOccupiedLanding(EntityView view)
        {
            if (!_freeStandRequests.TryGetValue(view.EntityId.Value, out var request) ||
                !_characterDestinations.TryGetValue(view.EntityId.Value, out var landing) ||
                IsCharacterStandClearAt(view, landing, includeDestinations: true) ||
                !TryResolveFreeStandPoint(view.EntityId, request.center, request.radius, out var free) ||
                !TryBuildCharacterPath(view.EntityId, view.transform.position, free, _wpScratch, exactGoal: true)) return false;
            for (var i = 0; i < _wpScratch.Count; i++)
                if (!IsOpeningMoveAllowed(view.EntityId, _wpScratch[i])) return false;
            if (!_paths.TryGetValue(view.EntityId.Value, out var path)) return false;
            path.Clear(); path.AddRange(_wpScratch);
            _pathIndex[view.EntityId.Value] = 0;
            _targets[view] = path[0];
            ReserveCharacterDestination(view.EntityId, free);
            ResetNavigationDistance(view.EntityId, view.transform.position);
            return true;
        }

        float RemainingNavigationDistance(EntityId id, Vector3 point)
        {
            if (!_paths.TryGetValue(id.Value, out var path) || !_pathIndex.TryGetValue(id.Value, out var index))
                return float.PositiveInfinity;
            var distance = 0f;
            for (var i = index; i < path.Count; i++)
            { distance += Vector3.Distance(point, path[i]); point = path[i]; }
            return distance;
        }

        void ResetNavigationDistance(EntityId id, Vector3 point)
        {
            if (_navigationProgress.TryGetValue(id.Value, out var progress))
                progress.BestRemaining = RemainingNavigationDistance(id, point);
        }

        // Return false only after a sustained real-time stall. Paused frames never call this.
        bool MaintainNavigationProgress(EntityView view, Vector3 before, Vector3 after, bool invalidLeg)
        {
            var now = Time.unscaledTime;
            var id = view.EntityId.Value;
            if (!_navigationProgress.TryGetValue(id, out var progress))
            {
                progress = new NavigationProgress { BestRemaining = RemainingNavigationDistance(view.EntityId, before) };
                _navigationProgress[id] = progress;
            }
            var remaining = RemainingNavigationDistance(view.EntityId, after);
            var threshold = (_walkGrid?.CellSize ?? 1f) * .05f;
            if (remaining + threshold < progress.BestRemaining)
            { progress.BestRemaining = remaining; progress.StalledSeconds = 0f; }
            else progress.StalledSeconds += Time.unscaledDeltaTime;
            var blocked = invalidLeg || Vector2.Distance(before, after) < .0001f;
            if (blocked && now >= progress.NextRetryAt)
            {
                if (_navigationRetryFrame != Time.frameCount)
                { _navigationRetryFrame = Time.frameCount; _navigationRetriesThisFrame = 0; }
                if (_navigationRetriesThisFrame < 2)
                {
                    _navigationRetriesThisFrame++;
                    progress.NextRetryAt = now + .6f;
                    if (!TryRelocateOccupiedLanding(view))
                    {
                        if (invalidLeg) TryRebuildRemainingRoute(view);
                        else TryInsertCharacterDetour(view);
                    }
                }
            }
            return progress.StalledSeconds < 8f;
        }

        void ReleaseCharacterDestination(EntityId id)
        {
            _characterDestinations.Remove(id.Value);
            _freeStandRequests.Remove(id.Value);
            _navigationProgress.Remove(id.Value);
        }

        void ResetCharacterSpacing()
        {
            _characterDestinations.Clear();
            _freeStandRequests.Clear();
            _navigationProgress.Clear();
            _characterOccupants.Clear();
            _characterOccupantsFrame = -1;
            _characterOccupantsWorld = null;
        }
    }
}
