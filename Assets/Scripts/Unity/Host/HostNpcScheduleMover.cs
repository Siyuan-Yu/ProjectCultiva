using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Navigation;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Drives Host pathfinding from Core <see cref="MovementIntentComponent"/> (MoveAction).
    /// Shared WalkGrid with player RTS — no wall clipping. No hardcoded location ids.
    /// </summary>
    public sealed class HostNpcScheduleMover : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostMoveController moveController;
        [SerializeField] EntityViewSpawner viewSpawner;
        [SerializeField] float repathIntervalSeconds = 3f;
        [SerializeField] float arriveRadius = 2.5f;
        [SerializeField] float stuckSeconds = 4f;
        [SerializeField] float stuckMoveEpsilon = 0.15f;
        [SerializeField] int goalSnapCells = 6;

        readonly Dictionary<ulong, float> _nextRepathAt = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, Vector3> _lastPos = new Dictionary<ulong, Vector3>();
        readonly Dictionary<ulong, float> _lastProgressAt = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, string> _lastTargetKey = new Dictionary<ulong, string>();

        public void Bind(PlayableHostBootstrap host, HostMoveController move, EntityViewSpawner spawner)
        {
            bootstrap = host;
            moveController = move;
            viewSpawner = spawner;
        }

        /// <summary>Call when dialogue／menu releases an NPC so they repath immediately.</summary>
        public void NotifyNpcReleased(EntityId npc)
        {
            if (npc.IsNone)
                return;
            _nextRepathAt[npc.Value] = 0f;
            _lastProgressAt[npc.Value] = Time.unscaledTime;
        }

        void Update()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            if (bootstrap.Session.IsPaused)
                return;
            if (bootstrap.Session.World.ContentEvents.HasActive)
                return;
            if (moveController == null || viewSpawner == null)
                return;

            var session = bootstrap.Session;
            var grid = moveController.WalkGrid;
            var speed = Mathf.Max(1, bootstrap.EffectiveSpeedMultiplier());
            var now = Time.unscaledTime;
            var repathInterval = repathIntervalSeconds / speed;
            var stuckLimit = stuckSeconds / speed;
            foreach (var entity in session.World.Entities.All)
            {
                if ((entity.Tags & EntityTag.Character) != 0)
                    continue;
                if ((entity.Tags & EntityTag.Npc) == 0)
                    continue;
                if (!LocalMapVisibility.IsEntityVisible(session.World, entity.Id))
                    continue;
                // 弥留／尸体不跑日程寻路
                if (!CombatLifeStateService.CanFight(entity))
                    continue;
                if (!entity.TryGet<MovementIntentComponent>(out var intent) || !intent.Active)
                    continue;
                if (!viewSpawner.Registry.TryGet(entity.Id, out var view) || view == null)
                    continue;

                if (!TryResolveWorldTarget(session.World, intent, out var rawCenter))
                    continue;

                var center = SnapGoalToWalkable(grid, rawCenter, goalSnapCells);
                var pos = view.transform.position;
                if ((pos - center).sqrMagnitude <= arriveRadius * arriveRadius)
                {
                    intent.HostArrived = true;
                    _lastTargetKey.Remove(entity.Id.Value);
                    continue;
                }

                if (moveController.IsNpcHeldForInteraction(entity.Id))
                    continue;

                TrackProgress(entity.Id.Value, pos, now);
                var targetKey = intent.TargetWorkAreaId + "|" + intent.TargetLocationId;
                var targetChanged = !_lastTargetKey.TryGetValue(entity.Id.Value, out var prev) ||
                                    !string.Equals(prev, targetKey, System.StringComparison.Ordinal);
                var stuck = IsStuck(entity.Id.Value, now, stuckLimit);
                var due = !_nextRepathAt.TryGetValue(entity.Id.Value, out var t) || now >= t;

                if (!targetChanged && !stuck && moveController.IsMoving(entity.Id) && !due)
                    continue;

                if (!due && !targetChanged && !stuck)
                    continue;

                _nextRepathAt[entity.Id.Value] = now + repathInterval;
                _lastTargetKey[entity.Id.Value] = targetKey;
                if (!moveController.OrderEntityToWorldPoint(entity.Id, center, null, issueStop: false))
                {
                    // Unreachable: accept arrival so Core can advance schedule instead of freezing.
                    intent.HostArrived = true;
                }
                else
                {
                    _lastProgressAt[entity.Id.Value] = now;
                    _lastPos[entity.Id.Value] = pos;
                }
            }
        }

        void TrackProgress(ulong id, Vector3 pos, float now)
        {
            if (!_lastPos.TryGetValue(id, out var prev) ||
                (pos - prev).sqrMagnitude >= stuckMoveEpsilon * stuckMoveEpsilon)
            {
                _lastPos[id] = pos;
                _lastProgressAt[id] = now;
            }
        }

        bool IsStuck(ulong id, float now, float stuckLimit)
        {
            if (!_lastProgressAt.TryGetValue(id, out var t))
                return false;
            return now - t >= stuckLimit;
        }

        static Vector3 SnapGoalToWalkable(WalkGrid grid, Vector3 world, int snapRadius)
        {
            if (grid == null)
                return world;
            if (!grid.TryWorldToCell(world.x, world.y, out var cx, out var cy))
                return world;
            if (grid.IsWalkable(cx, cy))
                return world;
            if (!grid.TryFindNearestWalkable(cx, cy, snapRadius > 0 ? snapRadius : 8, out var nx, out var ny))
                return world;
            grid.CellToWorldCenter(nx, ny, out var wx, out var wy);
            return new Vector3(wx, wy, HostPresentationSpace.EntityZ);
        }

        static bool TryResolveWorldTarget(
            XianXia.Core.Simulation.SimulationWorld world,
            MovementIntentComponent intent,
            out Vector3 worldCenter)
        {
            worldCenter = default;
            if (world == null || intent == null)
                return false;

            float ox = 0f, oz = 0f;
            string locationId = intent.TargetLocationId;
            if (!string.IsNullOrEmpty(intent.TargetWorkAreaId) &&
                world.TryGetWorkArea(intent.TargetWorkAreaId, out var area))
            {
                if (!string.IsNullOrEmpty(area.LocationId))
                    locationId = area.LocationId;
                ox = area.OffsetX;
                oz = area.OffsetZ;
            }

            if (string.IsNullOrEmpty(locationId))
                return false;
            if (!HostZoneQuery.TryGetLocationCenter(world, locationId, out var center))
                return false;

            // Soft slot → interact spot (农田多点) or ring around area offset.
            if (intent.SlotIndex >= 0)
            {
                var kind = HostInteractSpotKind.Work;
                if (!string.IsNullOrEmpty(intent.TargetWorkAreaId) &&
                    world.TryGetWorkArea(intent.TargetWorkAreaId, out var slotArea) &&
                    slotArea.AllowedActivities != null)
                {
                    for (var i = 0; i < slotArea.AllowedActivities.Count; i++)
                    {
                        if (string.Equals(slotArea.AllowedActivities[i], "Cultivate", System.StringComparison.OrdinalIgnoreCase))
                        {
                            kind = HostInteractSpotKind.Cultivate;
                            break;
                        }
                    }
                }

                if (HostInteractSpots.TryGetSlotSpot(
                        locationId, kind, intent.SlotIndex, out var spot, world))
                {
                    worldCenter = spot.WorldPosition;
                    worldCenter.z = HostPresentationSpace.EntityZ;
                    return true;
                }

                worldCenter = center + new Vector3(ox, oz, 0f) + HostInteractSpots.RingOffset(intent.SlotIndex);
                worldCenter.z = HostPresentationSpace.EntityZ;
                return true;
            }

            // Presentation offset from content → world (XY = presentation X/Z).
            worldCenter = center + new Vector3(ox, oz, 0f);
            worldCenter.z = HostPresentationSpace.EntityZ;
            return true;
        }
    }
}
