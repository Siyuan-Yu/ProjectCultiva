using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Entities;
using XianXia.Core.Schedule;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Demo-like overhead activity labels from Core Action／Schedule (presentation only).
    /// </summary>
    public sealed class HostActivityPresenter : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] EntityViewSpawner viewSpawner;

        public void Bind(PlayableHostBootstrap host, EntityViewSpawner spawner)
        {
            bootstrap = host;
            viewSpawner = spawner;
        }

        void LateUpdate()
        {
            if (bootstrap == null || viewSpawner == null)
                return;
            var session = bootstrap.Session;
            if (session == null || !session.IsInitialized)
                return;

            foreach (var view in viewSpawner.Registry.All)
            {
                if (view == null || !view.IsBound)
                    continue;
                if (!session.World.Entities.TryGet(view.EntityId, out var entity))
                    continue;
                if (bootstrap.MoveController != null && bootstrap.MoveController.IsMoving(view.EntityId))
                {
                    view.SetActivityText("移动中");
                    continue;
                }

                view.SetActivityText(ResolveLabel(session, entity));
            }
        }

        static string ResolveLabel(PlayableHostSession session, Entity entity)
        {
            if (entity.TryGet<ActionStateComponent>(out var actionState) &&
                actionState.HasActiveAction &&
                session.World.ActiveActions.TryGetValue(actionState.ActiveActionId, out var action))
            {
                if (action is MoveAction)
                    return "移动中";
                if (action is WorkAction work)
                {
                    switch (work.Activity)
                    {
                        case ScheduleActivity.Rest:
                        case ScheduleActivity.Eat:
                            return "休息中";
                        case ScheduleActivity.Patrol:
                        case ScheduleActivity.Inspect:
                            return "巡查中";
                        case ScheduleActivity.Cultivate:
                            return "修炼中";
                        default:
                            return "工作中";
                    }
                }
                if (action is LaborAction)
                    return "工作中";
                if (action is CultivateAction)
                    return "修炼中";
                if (action is RestAction)
                    return "休息中";
                if (action is ObserveAction)
                    return "观察中";
                if (action is WaitAction)
                    return "待命";
                return "行动中";
            }

            // 己方不跟课表自动走：空闲时不要用课表块冒充「巡视中／工作中」。
            if ((entity.Tags & EntityTag.Character) != 0)
                return string.Empty;

            if (entity.TryGet<ScheduleComponent>(out var sched) &&
                !string.IsNullOrEmpty(sched.DefinitionId) &&
                session.World.TryGetSchedule(sched.DefinitionId, out var def) &&
                def.TryResolve(session.World.Tick, out var block))
            {
                switch (block.Activity)
                {
                    case ScheduleActivity.Labor: return "工作中";
                    case ScheduleActivity.Rest: return "休息中";
                    case ScheduleActivity.Eat: return "吃饭中";
                    case ScheduleActivity.Cultivate: return "修炼中";
                    case ScheduleActivity.Explore: return "探索中";
                    case ScheduleActivity.Patrol: return "巡视中";
                    case ScheduleActivity.Inspect: return "检查中";
                }
            }

            return string.Empty;
        }
    }
}
