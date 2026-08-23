using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
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

                if (WorldTravelService.BlocksFormalArmyMemberIndependentTravel(world, id))
                {
                    lastFail = "Formal Army members cannot travel independently.";
                    continue;
                }

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

        /// <summary>Phase D：Formal Army 宏观移动（成员 presence 由 Adapter 投影）。</summary>
        public void BeginArmyMacroOrder(string armyId, WorldTravelTarget target, bool closeWorldMap = false)
        {
            _status = string.Empty;
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized ||
                string.IsNullOrEmpty(armyId))
                return;

            var world = bootstrap.Session.World;
            if (StrategicClockFreezeService.IsModalEncounter(world))
            {
                _status = "手动遭遇中：禁止战略出行";
                return;
            }

            if (closeWorldMap)
                bootstrap.WorldMapPanel?.Close();

            if (world.Strategic.FormalArmies.TryGet(armyId, out var army) && army != null)
                ArmyTravelCommandService.ReconcileArmyWithLivingMembers(world, army);

            if (!ArmyHexCommandService.TryResolveDestinationHex(world, target, out var destHex, out var destLabel))
            {
                _status = "无法解析 Hex 目标";
                return;
            }

            var started = ArmyHexCommandService.MoveArmy(world, armyId, destHex);
            if (started.IsSuccess)
            {
                if (world.Strategic.FormalArmies.TryGet(armyId, out army) && army != null)
                {
                    for (var i = 0; i < army.MemberCharacterIds.Count; i++)
                    {
                        var memberId = new EntityId(army.MemberCharacterIds[i]);
                        bootstrap.MoveController?.CancelPresentationMovementPublic(memberId);
                        HideFromLocalMap(memberId);
                    }
                }

                bootstrap.WorldMapPanel?.SetArmyHexPathPreview(armyId, destHex);
                _status = "军团已出发前往 " + destLabel;
            }
            else
                _status = started.Error.Message;

            bootstrap.Resume();
        }

        /// <summary>
        /// Formal Army 攻击：与 BeginArmyMacroOrder 同移动管线（MoveArmyToStackAnchor），
        /// 额外挂 Pursuit 标记；抵达弹接战而非到站提示。
        /// </summary>
        public void BeginFormalArmyPursuit(string armyId, ArmyStack stack)
        {
            _status = string.Empty;
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized ||
                string.IsNullOrEmpty(armyId) || stack == null)
                return;

            var world = bootstrap.Session.World;
            if (StrategicClockFreezeService.IsModalEncounter(world))
            {
                _status = "手动遭遇中：禁止战略追击出行";
                return;
            }

            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
            {
                _status = "军团不存在";
                return;
            }

            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(army.MemberCharacterIds[i]);
                if (id.IsNone)
                    continue;
                bootstrap.MoveController?.CancelPresentationMovementPublic(id);
                HideFromLocalMap(id);
            }

            var hexStarted = ArmyHexCommandService.AttackStack(world, armyId, stack);
            if (hexStarted.IsSuccess &&
                world.Strategic.FormalArmies.TryGet(armyId, out army) &&
                army != null)
            {
                bootstrap.WorldMapPanel?.SetArmyHexPathPreview(armyId, army.DestinationHex);
                var stackLabel = string.IsNullOrEmpty(stack.DisplayName) ? stack.Id : stack.DisplayName;
                _status = world.Strategic.HasBattleOffer
                    ? "已抵达，接战弹窗已打开"
                    : "军团已出发追击「" + stackLabel + "」";
            }
            else if (!string.IsNullOrEmpty(hexStarted.Error.Message))
                _status = hexStarted.Error.Message;

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

            if (!ArmyStackAdapter.TryResolveAttackerArmyId(world, agents, out var attackerArmyId) ||
                string.IsNullOrEmpty(attackerArmyId))
            {
                _status = "玩家战略追击须通过 Formal Army";
                return;
            }

            BeginFormalArmyPursuit(attackerArmyId, stack);
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
