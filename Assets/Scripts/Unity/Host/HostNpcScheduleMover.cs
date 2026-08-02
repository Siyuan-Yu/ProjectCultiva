using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Schedule;
using XianXia.Core.Social;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// NPC 按课表活动在地点间寻路移动（表现层）。己方 Character 不驱动。
    /// </summary>
    public sealed class HostNpcScheduleMover : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostMoveController moveController;
        [SerializeField] EntityViewSpawner viewSpawner;
        [SerializeField] float repathIntervalSeconds = 8f;
        [SerializeField] float arriveRadius = 2.5f;

        readonly Dictionary<ulong, float> _nextRepathAt = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, string> _targetLoc = new Dictionary<ulong, string>();

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
                if (!entity.TryGet<ScheduleComponent>(out var sched) ||
                    string.IsNullOrEmpty(sched.DefinitionId))
                    continue;
                if (!session.World.TryGetSchedule(sched.DefinitionId, out var def) ||
                    !def.TryResolve(session.World.Tick, out var block))
                    continue;
                if (!viewSpawner.Registry.TryGet(entity.Id, out var view) || view == null)
                    continue;
                if (moveController.IsMoving(entity.Id))
                    continue;

                var destId = ResolveDestination(session, entity, block.Activity);
                if (string.IsNullOrEmpty(destId))
                    continue;
                if (!HostZoneQuery.TryGetLocationCenter(session.World, destId, out var center))
                    continue;

                var pos = view.transform.position;
                if ((pos - center).sqrMagnitude <= arriveRadius * arriveRadius)
                {
                    _targetLoc[entity.Id.Value] = destId;
                    continue;
                }

                if (_nextRepathAt.TryGetValue(entity.Id.Value, out var t) && now < t)
                    continue;
                _nextRepathAt[entity.Id.Value] = now + repathIntervalSeconds;
                _targetLoc[entity.Id.Value] = destId;
                moveController.OrderEntityToWorldPoint(entity.Id, center, null, issueStop: false);
            }
        }

        static string ResolveDestination(
            PlayableHostSession session,
            Entity entity,
            ScheduleActivity activity)
        {
            // 优先：角色当前地点旁的「家乡」resident 绑定
            string home = null;
            foreach (var kv in session.World.WorldRegion.Locations)
            {
                if (!string.IsNullOrEmpty(kv.Value.ResidentNpcDefinitionId) &&
                    entity.DefinitionId.ToString() == kv.Value.ResidentNpcDefinitionId)
                {
                    home = kv.Key;
                    break;
                }
            }

            switch (activity)
            {
                case ScheduleActivity.Rest:
                case ScheduleActivity.Eat:
                    return home ?? "base:loc_ref_houses";
                case ScheduleActivity.Labor:
                    if (entity.TryGet<NpcAiRoleComponent>(out var ai) &&
                        ai.Role == NpcAiRoleKind.Mortal)
                    {
                        if (!string.IsNullOrEmpty(home) && home.Contains("herb"))
                            return "base:loc_ref_herb_field";
                        return "base:loc_ref_labor_yard";
                    }

                    return "base:loc_ref_forest";
                case ScheduleActivity.Explore:
                    return "base:loc_ref_road_hub";
                case ScheduleActivity.Patrol:
                case ScheduleActivity.Inspect:
                    return "base:loc_ref_road_hub";
                case ScheduleActivity.Cultivate:
                    return "base:loc_ref_spring";
                default:
                    return home ?? "base:loc_ref_road_hub";
            }
        }
    }
}
