using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Input;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Keeps player-ordered gather／labor looping until Stop or a non-work order.
    /// </summary>
    public sealed class HostWorkLoop : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostCommandBridge commandBridge;
        [SerializeField] HostMoveController moveController;

        readonly HashSet<ulong> _looping = new HashSet<ulong>();

        public void Bind(PlayableHostBootstrap host, HostCommandBridge bridge, HostMoveController move)
        {
            bootstrap = host;
            commandBridge = bridge;
            moveController = move;
        }

        public void StartLoop(EntityId id)
        {
            if (!id.IsNone)
                _looping.Add(id.Value);
        }

        public void StartLoopMany(IReadOnlyList<EntityId> ids)
        {
            if (ids == null)
                return;
            for (var i = 0; i < ids.Count; i++)
                StartLoop(ids[i]);
        }

        public void StopLoop(EntityId id)
        {
            if (!id.IsNone)
                _looping.Remove(id.Value);
        }

        public void StopAll() => _looping.Clear();

        public bool IsLooping(EntityId id) => !id.IsNone && _looping.Contains(id.Value);

        void LateUpdate()
        {
            if (_looping.Count == 0 || bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            if (commandBridge == null)
                return;
            if (bootstrap.Session.IsPaused)
                return;

            var world = bootstrap.Session.World;
            // Copy keys — may mutate during Issue.
            var scratch = ListPool.Rent();
            foreach (var v in _looping)
                scratch.Add(v);

            for (var i = 0; i < scratch.Count; i++)
            {
                var id = new EntityId(scratch[i]);
                if (moveController != null && moveController.IsMoving(id))
                    continue;
                if (!world.Entities.TryGet(id, out var entity))
                {
                    _looping.Remove(scratch[i]);
                    continue;
                }

                if (entity.TryGet<ActionStateComponent>(out var actionState) &&
                    actionState.HasActiveAction &&
                    world.ActiveActions.TryGetValue(actionState.ActiveActionId, out var action))
                {
                    if (action is LaborAction || action is CultivateAction)
                        continue;
                    // Wait／其它：打断后重开劳动
                    if (action is WaitAction)
                        bootstrap.Session.Loop.StopSubject(id);
                    else
                        continue;
                }

                commandBridge.IssueOne(id, PlayerCommandKind.Labor, commandBridge.GatherDurationTicks());
            }

            ListPool.Return(scratch);
        }

        static class ListPool
        {
            static readonly Stack<List<ulong>> Pool = new Stack<List<ulong>>();

            public static List<ulong> Rent()
            {
                if (Pool.Count > 0)
                {
                    var list = Pool.Pop();
                    list.Clear();
                    return list;
                }

                return new List<ulong>(8);
            }

            public static void Return(List<ulong> list)
            {
                if (list == null)
                    return;
                list.Clear();
                Pool.Push(list);
            }
        }
    }
}
