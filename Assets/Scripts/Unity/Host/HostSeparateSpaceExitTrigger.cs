using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// SPACE-01：Active Controlled Character 走进 Cave Exit Trigger → 整队自动离开。
    /// 无右键／无确认；edge-trigger 防止 spawn／Load 在出口内立刻弹出。
    /// </summary>
    public sealed class HostSeparateSpaceExitTrigger : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;

        SeparateSpaceExitEdgeTrigger.State _state;
        string _lastLoggedAction = string.Empty;

        void Awake()
        {
            if (bootstrap == null)
                bootstrap = GetComponent<PlayableHostBootstrap>();
        }

        /// <summary>Snapshot restore／Enter presentation 完成后调用，按当前 Active 位置重新 arm。</summary>
        public void NotifyPresentationRestored() => ResetForCurrentSession("PresentationRestore");

        public void NotifyEnteredSeparateSpace() => ResetForCurrentSession("Enter");

        void ResetForCurrentSession(string reason)
        {
            var world = bootstrap?.Session?.World;
            if (world?.LocalMap == null || !world.LocalMap.IsActive)
            {
                _state = default;
                return;
            }

            var mapId = world.LocalMap.ActiveMapLayoutId?.Trim() ?? string.Empty;
            var inside = IsActiveInsideExit(out _);
            SeparateSpaceExitEdgeTrigger.ResetForMap(ref _state, mapId, inside);
            LogAction(mapId, inside, _state.Armed, inside ? "WaitForExit" : "Armed", reason);
        }

        void Update()
        {
            var session = bootstrap?.Session;
            var world = session?.World;
            if (world?.LocalMap == null || !world.LocalMap.IsActive)
            {
                if (!string.IsNullOrEmpty(_state.MapId))
                    _state = default;
                return;
            }

            var mapId = world.LocalMap.ActiveMapLayoutId?.Trim() ?? string.Empty;
            if (!string.Equals(_state.MapId, mapId, System.StringComparison.Ordinal))
            {
                ResetForCurrentSession("MapChanged");
                return;
            }

            if (!CanAttemptLeave(out var activeId))
                return;

            var inside = IsActiveInsideExit(out _);
            var action = SeparateSpaceExitEdgeTrigger.Tick(ref _state, inside, out var shouldLeave);
            if (!string.Equals(action, _lastLoggedAction, System.StringComparison.Ordinal) &&
                (action == "Armed" || action == "Leave" || action == "WaitForExit"))
                LogAction(mapId, inside, _state.Armed, action, "Tick");

            if (!shouldLeave)
                return;

            var bridge = bootstrap.CommandBridge;
            if (bridge == null)
            {
                _state.TransitionIssued = false;
                return;
            }

            if (bridge.IssueLeaveSeparateSpace(activeId) <= 0)
            {
                Debug.LogWarning(
                    "[SeparateSpaceExit] Leave failed: " + bridge.LastStatus, this);
                _state.TransitionIssued = false;
                return;
            }

            bootstrap.GetComponent<HostFeedbackOverlay>()?.SpawnAtEntity(
                bootstrap.ViewSpawner, activeId, "离开洞府", new Color(0.85f, 0.92f, 0.75f, 1f));
            _state = default;
        }

        bool CanAttemptLeave(out EntityId activeId)
        {
            activeId = EntityId.None;
            var party = bootstrap?.Session?.PlayerParty;
            if (party == null || !party.HasActive)
                return false;
            activeId = party.ActiveCharacterId;
            if (activeId.IsNone)
                return false;

            if (HostInputGate.BlockWorldInteraction)
                return false;

            var world = bootstrap.Session.World;
            if (!world.Entities.TryGet(activeId, out var ent) ||
                !CombatLifeStateService.CanFight(ent))
                return false;

            var melee = bootstrap.GetComponent<HostNpcMeleeAssault>();
            if (melee != null && melee.IsFighting && melee.IsAttacker(activeId))
                return false;

            return true;
        }

        bool IsActiveInsideExit(out string exitLabel)
        {
            exitLabel = string.Empty;
            var party = bootstrap?.Session?.PlayerParty;
            if (party == null || !party.HasActive)
                return false;
            if (!MapLayoutPick.TryGet(bootstrap.Session, out MapLayoutDefinition layout) || layout == null)
                return false;

            var active = party.ActiveCharacterId;
            if (bootstrap.ViewSpawner == null ||
                !bootstrap.ViewSpawner.Registry.TryGet(active, out var view) ||
                view == null)
                return false;

            var p = HostPresentationSpace.ToPresentation(view.transform.position);
            return HostCaveEntranceQuery.TryResolveInteriorExitAtPoint(
                layout, p.x, p.y, out _, out exitLabel);
        }

        void LogAction(string mapId, bool inside, bool armed, string action, string reason)
        {
            _lastLoggedAction = action ?? string.Empty;
            var active = bootstrap?.Session?.PlayerParty?.ActiveCharacterId.Value ?? 0UL;
            Debug.Log(
                "[SeparateSpaceExit] map=" + mapId +
                " active=" + active +
                " inside=" + inside +
                " armed=" + armed +
                " action=" + action +
                " reason=" + reason,
                this);
        }
    }
}
