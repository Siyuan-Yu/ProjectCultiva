using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Input;
using XianXia.Core.Navigation;
using XianXia.Core.World;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 确认后的宏观出行：场景内「未出行」走向边缘；途中可被玩家操控打断；
    /// 只有真正走出场景边缘后才上大地图慢移。
    /// </summary>
    public sealed class HostWorldTravelDeparture : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;

        readonly Dictionary<ulong, string> _pendingDest = new Dictionary<ulong, string>();
        bool _suppressOverride;
        string _status = string.Empty;

        public string LastStatus => _status;

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public void ClearSessionState()
        {
            _pendingDest.Clear();
            _status = string.Empty;
            _suppressOverride = false;
        }

        public bool IsDeparting(EntityId id) =>
            !id.IsNone && _pendingDest.ContainsKey(id.Value);

        /// <summary>玩家再下令／改走位时调用：取消未完成的离场，不上大地图。</summary>
        public bool NotifyPlayerOverride(EntityId id)
        {
            if (_suppressOverride || id.IsNone)
                return false;
            return CancelDeparture(id, "出行已取消（改做其他事）");
        }

        public bool CancelDeparture(EntityId id, string reason = null)
        {
            if (id.IsNone || bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return false;

            var world = bootstrap.Session.World;
            var was = _pendingDest.Remove(id.Value);
            if (world.WorldPresence.TryGet(id, out var p) &&
                p != null &&
                p.Mode == PartyWorldPresenceMode.DepartingLocalMap)
            {
                p.Mode = PartyWorldPresenceMode.AtNode;
                p.DestNodeId = string.Empty;
                p.RouteId = string.Empty;
                p.RemainingTravelTicks = 0;
                p.TravelTotalTicks = 0;
                was = true;
            }

            if (!was)
                return false;

            bootstrap.MoveController?.CancelPresentationMovementPublic(id);
            if (!string.IsNullOrEmpty(reason))
                _status = reason;
            return true;
        }

        public void BeginDeparture(IReadOnlyList<EntityId> agents, string destNodeId)
        {
            _status = string.Empty;
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized ||
                agents == null || agents.Count == 0 || string.IsNullOrEmpty(destNodeId))
                return;

            var world = bootstrap.Session.World;
            if (!world.WorldGraph.TryGetNode(destNodeId, out var destNode))
            {
                _status = "目标节点不存在";
                return;
            }

            // 大地图下的宏观下令：直接上路网，不要求先走 LocalMap 边缘
            var macroFromWorldMap = bootstrap.WorldMapPanel != null && bootstrap.WorldMapPanel.IsOpen;
            bootstrap.WorldMapPanel?.Close();

            var anyLocalWalk = false;
            var anyTravelStarted = false;
            for (var i = 0; i < agents.Count; i++)
            {
                var id = agents[i];
                if (id.IsNone)
                    continue;

                _suppressOverride = true;
                try
                {
                    bootstrap.CommandBridge?.IssueOne(id, PlayerCommandKind.Stop, 0);
                    bootstrap.MoveController?.CancelPresentationMovementPublic(id);

                    if (!macroFromWorldMap && NeedsLocalMapExit(world, id))
                    {
                        var mark = WorldTravelService.MarkDepartingLocalMap(world, id, destNodeId);
                        if (mark.IsFailure)
                        {
                            _status = mark.Error.Message;
                            continue;
                        }

                        _pendingDest[id.Value] = destNodeId;
                        if (!TryOrderToMapEdge(id, destNodeId, () => OnReachedMapEdge(id)))
                        {
                            // 寻路失败不直接上路：取消离场，避免“没走出场景就上大地图”
                            CancelDeparture(id, "无法走到地图边缘，出行取消");
                        }
                        else
                        {
                            anyLocalWalk = true;
                        }
                    }
                    else
                    {
                        // 人不在当前场景（已在别处／无 LocalMap）：直接上宏观路
                        var start = WorldTravelService.StartTravel(world, id, destNodeId);
                        if (start.IsFailure)
                            _status = start.Error.Message;
                        else
                        {
                            anyTravelStarted = true;
                            HideFromLocalMap(id);
                        }
                    }
                }
                finally
                {
                    _suppressOverride = false;
                }
            }

            WorldTravelService.SyncPartyFocus(world);
            if (anyLocalWalk)
                _status = "未出行：正走向地图边缘（再下令／走动可取消）";
            else if (anyTravelStarted && string.IsNullOrEmpty(_status))
                _status = "已出发前往 " + (string.IsNullOrEmpty(destNode.Name) ? destNodeId : destNode.Name);

            bootstrap.Resume();
        }

        bool NeedsLocalMapExit(XianXia.Core.Simulation.SimulationWorld world, EntityId id)
        {
            if (!world.WorldPresence.TryGet(id, out var p) || p == null)
                return false;
            if (p.Mode != PartyWorldPresenceMode.AtNode &&
                p.Mode != PartyWorldPresenceMode.DepartingLocalMap)
                return false;
            if (string.IsNullOrEmpty(p.NodeId) ||
                !world.WorldGraph.TryGetNode(p.NodeId, out var node))
                return false;

            var mapId = WorldTravelService.ResolveLocalMapId(node);
            var active = world.LocalMap.ActiveMapLayoutId;
            if (string.IsNullOrEmpty(active) ||
                !string.Equals(active, mapId, System.StringComparison.Ordinal))
                return false;

            var spawner = bootstrap.ViewSpawner;
            return spawner != null && spawner.Registry.TryGet(id, out _);
        }

        bool TryOrderToMapEdge(EntityId id, string destNodeId, System.Action onArrive)
        {
            var move = bootstrap.MoveController;
            var spawner = bootstrap.ViewSpawner;
            if (move == null || spawner == null ||
                !spawner.Registry.TryGet(id, out var view) || view == null)
                return false;

            var grid = move.WalkGrid;
            if (grid == null)
                return false;

            var world = bootstrap.Session.World;
            var dir = Vector2.right;
            if (world.WorldPresence.TryGet(id, out var p) &&
                world.WorldGraph.TryGetNode(p.NodeId, out var from) &&
                world.WorldGraph.TryGetNode(destNodeId, out var to))
            {
                dir = new Vector2(to.WorldX - from.WorldX, to.WorldY - from.WorldY);
                if (dir.sqrMagnitude < 0.01f)
                    dir = Vector2.right;
                dir.Normalize();
            }

            if (!TryPickEdgeWorldPoint(grid, view.transform.position, dir, out var edge))
                return false;

            return move.OrderEntityToWorldPointPublic(id, edge, onArrive);
        }

        static bool TryPickEdgeWorldPoint(WalkGrid grid, Vector3 from, Vector2 dir, out Vector3 edge)
        {
            edge = from;
            var bestScore = float.NegativeInfinity;
            var found = false;
            float bestWx = 0f, bestWy = 0f;

            void Consider(int cx, int cy)
            {
                if (!grid.IsWalkable(cx, cy))
                    return;
                grid.CellToWorldCenter(cx, cy, out var wx, out var wy);
                var to = new Vector2(wx - from.x, wy - from.y);
                var dist = to.magnitude;
                if (dist < 0.2f)
                    return;
                var score = Vector2.Dot(to.normalized, dir) * 10f + dist * 0.05f;
                if (score <= bestScore)
                    return;
                bestScore = score;
                bestWx = wx;
                bestWy = wy;
                found = true;
            }

            for (var x = 0; x < grid.Width; x++)
            {
                Consider(x, 0);
                Consider(x, grid.Height - 1);
            }

            for (var y = 0; y < grid.Height; y++)
            {
                Consider(0, y);
                Consider(grid.Width - 1, y);
            }

            if (!found)
                return false;
            edge = new Vector3(bestWx, bestWy, HostPresentationSpace.EntityZ);
            return true;
        }

        void OnReachedMapEdge(EntityId id)
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;

            var world = bootstrap.Session.World;
            // 已被打断则不上路
            if (!world.WorldPresence.TryGet(id, out var p) ||
                p == null ||
                p.Mode != PartyWorldPresenceMode.DepartingLocalMap)
            {
                _pendingDest.Remove(id.Value);
                return;
            }

            bootstrap.MoveController?.CancelPresentationMovementPublic(id);
            HideFromLocalMap(id);

            var commit = WorldTravelService.CommitTravelAfterLocalExit(world, id);
            _pendingDest.Remove(id.Value);
            if (commit.IsFailure)
                _status = commit.Error.Message;
            else
                _status = "已离开场景，进入大地图行程";

            // 只更新宏观摘要；不要 Rebuild／挪镜头——离场者已 Despawn 即可
            WorldTravelService.SyncPartyFocus(world);
        }

        void HideFromLocalMap(EntityId id)
        {
            var world = bootstrap.Session?.World;
            if (world != null && world.Entities.TryGet(id, out var ent) && ent != null)
            {
                if (ent.TryGet<EntityLocationComponent>(out var loc))
                {
                    loc.LocationId = string.Empty;
                    loc.HasPresentationOverride = false;
                }
            }

            bootstrap.ViewSpawner?.Despawn(id);
        }
    }
}
