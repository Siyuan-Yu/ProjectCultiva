using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 大地图 RTS 出行：
    /// · 下令 → LocalMap 上立刻 Despawn → 宏观上路
    /// · 路上再点别处 → 改目标（可打断）
    /// · 全员离开后：不卸图、不挪镜头，视线留在当前 LocalMap
    /// </summary>
    public sealed class HostWorldTravelDeparture : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;

        string _status = string.Empty;

        public string LastStatus => _status;

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public void ClearSessionState() => _status = string.Empty;

        public void BeginDeparture(IReadOnlyList<EntityId> agents, string destNodeId)
        {
            BeginMacroOrder(agents, WorldTravelTarget.AtNode(destNodeId), closeWorldMap: true);
        }

        /// <summary>大地图节点／道路下令。</summary>
        public void BeginMacroOrder(
            IReadOnlyList<EntityId> agents,
            WorldTravelTarget target,
            bool closeWorldMap = false)
        {
            _status = string.Empty;
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized ||
                agents == null || agents.Count == 0)
                return;

            var world = bootstrap.Session.World;
            if (StrategicClockFreezeService.IsModalEncounter(world))
            {
                _status = "手动遭遇中：禁止战略出行";
                return;
            }

            if (target.IsRouteProgress)
            {
                if (string.IsNullOrEmpty(target.RouteId))
                {
                    _status = "无效的道路目标";
                    return;
                }
            }
            else if (string.IsNullOrEmpty(target.NodeId) ||
                     !world.WorldGraph.TryGetNode(target.NodeId, out _))
            {
                _status = "目标节点不存在";
                return;
            }

            if (closeWorldMap)
                bootstrap.WorldMapPanel?.Close();

            var ok = 0;
            string lastFail = null;
            for (var i = 0; i < agents.Count; i++)
            {
                var id = agents[i];
                if (id.IsNone)
                    continue;

                // 普通宏观移动：取消战斗追击意图，恢复到站提示逻辑
                if (world.WorldPresence.TryGet(id, out var presence) && presence != null)
                    presence.ClearCombatPursuit();

                bootstrap.MoveController?.CancelPresentationMovementPublic(id);
                var started = WorldTravelPathService.StartAgentTravelToTarget(world, id, target);
                if (started.IsSuccess)
                {
                    ok++;
                    HideFromLocalMap(id);
                }
                else
                    lastFail = started.Error.Message;
            }

            WorldTravelService.SyncPartyFocus(world);
            // 不卸图、不 FrameCamera：视线留在当前 LocalMap

            if (ok > 0)
                _status = "已出发前往 " + target.Describe(world.WorldGraph) +
                          (ok < agents.Count ? "（" + ok + "/" + agents.Count + " 人）" : "");
            else if (!string.IsNullOrEmpty(lastFail))
                _status = lastFail;

            bootstrap.Resume();
        }

        /// <summary>攻击追击：立刻离开场景并上路（必挂追击标记，到站只弹接战）。</summary>
        public void BeginPursuitToStackAnchor(IReadOnlyList<EntityId> agents, ArmyStack stack)
        {
            _status = string.Empty;
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized ||
                agents == null || agents.Count == 0 || stack == null)
                return;

            var world = bootstrap.Session.World;
            if (StrategicClockFreezeService.IsModalEncounter(world))
            {
                _status = "手动遭遇中：禁止战略追击出行";
                return;
            }

            StrategicPursuitService.BeginPursuit(world, agents, stack);
            world.Strategic.ClearArrivalNotice();

            var ok = 0;
            string lastFail = null;
            for (var i = 0; i < agents.Count; i++)
            {
                var id = agents[i];
                if (id.IsNone)
                    continue;

                bootstrap.MoveController?.CancelPresentationMovementPublic(id);
                var started = WorldTravelService.StartTravelToStackAnchor(world, id, stack);
                if (started.IsFailure)
                    lastFail = started.Error.Message;
                else
                {
                    ok++;
                    HideFromLocalMap(id);
                    WorldTravelService.ClampPursuitTravelToStackAnchor(world, id, stack);
                }
            }

            WorldTravelService.SyncPartyFocus(world);
            // 若已重合（Clamp 后立刻到位），马上尝试接战弹窗
            StrategicPursuitService.AfterTravelTick(world);

            var name = string.IsNullOrEmpty(stack.DisplayName) ? stack.Id : stack.DisplayName;
            if (world.Strategic.HasBattleOffer)
                _status = "已抵达，接战弹窗已打开";
            else if (ok > 0)
                _status = "已出发追击「" + name + "」" +
                          (ok < agents.Count ? "（" + ok + "/" + agents.Count + " 人）" : "");
            else if (!string.IsNullOrEmpty(lastFail))
                _status = lastFail;

            bootstrap.Resume();
        }

        public void HidePartyFromLocalMapPublic(IReadOnlyList<EntityId> agents)
        {
            if (agents == null)
                return;
            for (var i = 0; i < agents.Count; i++)
                HideFromLocalMap(agents[i]);
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
