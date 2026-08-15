using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Input;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Keeps player-ordered gather／labor／cultivate looping until Stop or a conflicting order.
    /// </summary>
    public sealed class HostWorkLoop : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostCommandBridge commandBridge;
        [SerializeField] HostMoveController moveController;

        readonly Dictionary<ulong, PlayerCommandKind> _looping =
            new Dictionary<ulong, PlayerCommandKind>();

        public void Bind(PlayableHostBootstrap host, HostCommandBridge bridge, HostMoveController move)
        {
            bootstrap = host;
            commandBridge = bridge;
            moveController = move;
        }

        public void StartLoop(EntityId id, PlayerCommandKind kind = PlayerCommandKind.Labor)
        {
            if (id.IsNone)
                return;
            if (kind != PlayerCommandKind.Labor && kind != PlayerCommandKind.Cultivate)
                kind = PlayerCommandKind.Labor;
            _looping[id.Value] = kind;
        }

        public void StartLoopMany(IReadOnlyList<EntityId> ids, PlayerCommandKind kind = PlayerCommandKind.Labor)
        {
            if (ids == null)
                return;
            for (var i = 0; i < ids.Count; i++)
                StartLoop(ids[i], kind);
        }

        public void StopLoop(EntityId id)
        {
            if (!id.IsNone)
                _looping.Remove(id.Value);
        }

        public void StopAll() => _looping.Clear();

        public bool IsLooping(EntityId id) => !id.IsNone && _looping.ContainsKey(id.Value);

        void LateUpdate()
        {
            if (_looping.Count == 0 || bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            if (commandBridge == null)
                return;
            if (bootstrap.Session.IsPaused)
                return;

            var world = bootstrap.Session.World;
            var scratch = ListPool.Rent();
            foreach (var kv in _looping)
                scratch.Add(kv);

            for (var i = 0; i < scratch.Count; i++)
            {
                var id = new EntityId(scratch[i].Key);
                var kind = scratch[i].Value;
                if (moveController != null && moveController.IsMoving(id))
                    continue;
                if (!world.Entities.TryGet(id, out var entity))
                {
                    _looping.Remove(scratch[i].Key);
                    continue;
                }

                if (entity.TryGet<ActionStateComponent>(out var actionState) &&
                    actionState.HasActiveAction &&
                    world.ActiveActions.TryGetValue(actionState.ActiveActionId, out var action))
                {
                    if (action is LaborAction || action is CultivateAction)
                        continue;
                    if (action is WaitAction)
                        bootstrap.Session.Loop.StopSubject(id);
                    else
                        continue;
                }

                var dur = kind == PlayerCommandKind.Labor
                    ? commandBridge.GatherDurationTicks()
                    : HostCommandBridge.DefaultDurationTicks;
                commandBridge.IssueOne(id, kind, dur);
            }

            ListPool.Return(scratch);
        }

        static class ListPool
        {
            static readonly Stack<List<KeyValuePair<ulong, PlayerCommandKind>>> Pool =
                new Stack<List<KeyValuePair<ulong, PlayerCommandKind>>>();

            public static List<KeyValuePair<ulong, PlayerCommandKind>> Rent()
            {
                if (Pool.Count > 0)
                {
                    var list = Pool.Pop();
                    list.Clear();
                    return list;
                }

                return new List<KeyValuePair<ulong, PlayerCommandKind>>(8);
            }

            public static void Return(List<KeyValuePair<ulong, PlayerCommandKind>> list)
            {
                if (list == null)
                    return;
                list.Clear();
                Pool.Push(list);
            }
        }
    }
}
