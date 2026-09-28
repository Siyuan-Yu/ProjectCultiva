using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Combat;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
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
                var specialActivity = string.Empty;
                if (bootstrap.BreakthroughRitual != null &&
                    bootstrap.BreakthroughRitual.IsChannelingSubject(view.EntityId))
                    specialActivity = "冲击瓶颈";
                else if (bootstrap.SkillStudyRitual != null &&
                    bootstrap.SkillStudyRitual.IsChannelingSubject(view.EntityId))
                    specialActivity = "参悟中";

                // 田区农作自管头顶字，勿盖成「发呆中」
                var farm = bootstrap.GetComponent<HostFarmFieldLabor>();
                var melee = bootstrap.GetComponent<HostNpcMeleeAssault>();
                var resolved = HostCharacterActivityPresentation.Resolve(session, entity,
                    inLocalCombat: melee != null && melee.IsInFight(view.EntityId),
                    isMoving: bootstrap.MoveController != null && bootstrap.MoveController.IsMoving(view.EntityId),
                    specialActivity: specialActivity,
                    emptyWhenIdle: true);
                if (farm != null && farm.IsFarming(view.EntityId) &&
                    resolved != "交战中" && !CombatLifeStateService.IsDown(entity))
                    continue;
                view.SetActivityText(resolved);
            }
        }
    }
}
