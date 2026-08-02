using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Drives Host pathfinding from Core <see cref="MovementIntentComponent"/> (MoveAction).
    /// No hardcoded location ids — destinations come from WorkArea／Location data.
    /// </summary>
    public sealed class HostNpcScheduleMover : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostMoveController moveController;
        [SerializeField] EntityViewSpawner viewSpawner;
        [SerializeField] float repathIntervalSeconds = 8f;
        [SerializeField] float arriveRadius = 2.5f;

        readonly Dictionary<ulong, float> _nextRepathAt = new Dictionary<ulong, float>();

        public void Bind(PlayableHostBootstrap host, HostMoveController move, EntityViewSpawner spawner)
        {
            bootstrap = host;
            moveController = move;
            viewSpawner = spawner;
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
            var now = Time.unscaledTime;
            foreach (var entity in session.World.Entities.All)
            {
                if ((entity.Tags & EntityTag.Character) != 0)
                    continue;
                if ((entity.Tags & EntityTag.Npc) == 0)
                    continue;
                if (!entity.TryGet<MovementIntentComponent>(out var intent) || !intent.Active)
                    continue;
                if (!viewSpawner.Registry.TryGet(entity.Id, out var view) || view == null)
                    continue;

                if (!TryResolveWorldTarget(session.World, intent, out var center))
                    continue;

                var pos = view.transform.position;
                if ((pos - center).sqrMagnitude <= arriveRadius * arriveRadius)
                {
                    intent.HostArrived = true;
                    continue;
                }

                if (moveController.IsMoving(entity.Id))
                    continue;

                if (_nextRepathAt.TryGetValue(entity.Id.Value, out var t) && now < t)
                    continue;
                _nextRepathAt[entity.Id.Value] = now + repathIntervalSeconds;
                moveController.OrderEntityToWorldPoint(entity.Id, center, null, issueStop: false);
            }
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

            // Presentation offset from content → world (XY = presentation X/Z).
            worldCenter = center + new Vector3(ox, 0f, oz);
            return true;
        }
    }
}
