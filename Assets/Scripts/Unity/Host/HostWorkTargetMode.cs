using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Input;
using XianXia.Core.Npc;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// RTS 点选＋情境光标：武装时绿/红；未武装但选中己方时，悬停热点显示可交互光标，右键＝前往并交互。
    /// </summary>
    public sealed class HostWorkTargetMode : MonoBehaviour
    {
        public enum ArmKind
        {
            None = 0,
            Move = 1,
            Interact = 2,
            Combat = 3,
            Cultivate = 4
        }

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostCommandBridge commandBridge;
        [SerializeField] HostMoveController moveController;
        [SerializeField] Camera worldCamera;
        [SerializeField] ArmKind armed;

        Texture2D _cursorGreen;
        Texture2D _cursorRed;
        bool _canTargetUnderMouse;
        bool _idleHoverInteractable;
        string _hoverHint = string.Empty;
        bool _cursorOverridden;

        public bool IsActive => armed != ArmKind.None;

        public ArmKind Armed => armed;

        public void Bind(
            PlayableHostBootstrap host,
            HostSelectionController selection,
            HostCommandBridge bridge)
        {
            bootstrap = host;
            selectionController = selection;
            commandBridge = bridge;
            moveController = host != null ? host.GetComponent<HostMoveController>() : moveController;
            if (worldCamera == null)
                worldCamera = Camera.main;
        }

        public void ArmMove() => SetArmed(ArmKind.Move);

        public void ArmInteract() => SetArmed(ArmKind.Interact);

        public void ArmCombat() => SetArmed(ArmKind.Combat);

        public void ArmCultivate() => SetArmed(ArmKind.Cultivate);

        public void ArmLabor() => ArmInteract();

        public void Cancel() => SetArmed(ArmKind.None);

        /// <summary>
        /// 未武装时：右键热点＝前往并交互／修炼；返回 true 表示已消费（MoveController 勿再纯移动）。
        /// </summary>
        public bool TryHandleContextRightClick()
        {
            if (armed != ArmKind.None)
                return false;
            if (!HasCommandableParty())
                return false;
            if (HostUiHitTest.ContainsScreenPoint(Input.mousePosition))
                return false;
            if (worldCamera == null)
                worldCamera = Camera.main;
            if (worldCamera == null ||
                !HostPresentationSpace.TryRaycastPlane(worldCamera, Input.mousePosition, out var point))
                return false;

            if (HostZoneQuery.TryFindWorkSpot(point, out var work))
            {
                IssueWorkAtSpot(work);
                return true;
            }

            if (HostZoneQuery.TryFindLootSpot(point, out var loot))
            {
                IssueLootAtSpot(loot);
                return true;
            }

            if (HostZoneQuery.TryFindExploreSpot(point, out var explore))
            {
                IssueExploreAtSpot(explore);
                return true;
            }

            if (HostZoneQuery.TryFindCultivateSpot(point, out var cult))
            {
                IssueCultivateAtSpot(cult);
                return true;
            }

            return false;
        }

        void SetArmed(ArmKind kind)
        {
            armed = kind;
            if (kind == ArmKind.None)
                ClearCursorOverride();
            _hoverHint = string.Empty;
            _canTargetUnderMouse = false;
            _idleHoverInteractable = false;
        }

        void OnDisable() => ClearCursorOverride();

        void Update()
        {
            if (bootstrap == null || bootstrap.Session == null || !bootstrap.Session.IsInitialized)
            {
                ClearCursorOverride();
                return;
            }

            if (HostInputGate.BlockWorldInteraction ||
                bootstrap.Session.World.ContentEvents.HasActive ||
                (bootstrap.ContentInterrupt != null && bootstrap.ContentInterrupt.HasBlockingInterrupt))
            {
                SetArmed(ArmKind.None);
                ClearCursorOverride();
                return;
            }

            if (worldCamera == null)
                worldCamera = Camera.main;
            if (moveController == null && bootstrap != null)
                moveController = bootstrap.GetComponent<HostMoveController>();

            if (armed == ArmKind.None)
            {
                UpdateIdleHover();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                SetArmed(ArmKind.None);
                return;
            }

            UpdateArmedHover();

            if (!Input.GetMouseButtonDown(0))
                return;
            if (HostUiHitTest.ContainsScreenPoint(Input.mousePosition))
                return;
            if (!HostPresentationSpace.TryRaycastPlane(worldCamera, Input.mousePosition, out var point))
                return;

            switch (armed)
            {
                case ArmKind.Move:
                    ConfirmMove(point);
                    break;
                case ArmKind.Interact:
                    ConfirmInteract(point);
                    break;
                case ArmKind.Combat:
                    ConfirmCombat();
                    break;
                case ArmKind.Cultivate:
                    ConfirmCultivate(point);
                    break;
            }
        }

        void UpdateIdleHover()
        {
            _idleHoverInteractable = false;
            _hoverHint = string.Empty;

            if (!HasCommandableParty() ||
                HostUiHitTest.ContainsScreenPoint(Input.mousePosition) ||
                worldCamera == null ||
                !HostPresentationSpace.TryRaycastPlane(worldCamera, Input.mousePosition, out var point))
            {
                ClearCursorOverride();
                return;
            }

            if (HostZoneQuery.TryFindWorkSpot(point, out var work))
            {
                _idleHoverInteractable = true;
                _hoverHint = "右键交互·" + work.Label;
                ApplyCursor(true);
                return;
            }

            if (HostZoneQuery.TryFindLootSpot(point, out var loot))
            {
                _idleHoverInteractable = true;
                _hoverHint = "右键拾取·" + loot.Label;
                ApplyCursor(true);
                return;
            }

            if (HostZoneQuery.TryFindExploreSpot(point, out var explore))
            {
                _idleHoverInteractable = true;
                _hoverHint = "右键探索·" + explore.Label;
                ApplyCursor(true);
                return;
            }

            if (HostZoneQuery.TryFindCultivateSpot(point, out var cult))
            {
                _idleHoverInteractable = true;
                _hoverHint = "右键修炼·" + cult.Label;
                ApplyCursor(true);
                return;
            }

            if (TryPickNpcAtMouse(out var npc) &&
                selectionController != null &&
                !selectionController.IsPartyUnit(npc))
            {
                _idleHoverInteractable = true;
                _hoverHint = "右键·对话/攻击";
                ApplyCursor(true);
                return;
            }

            ClearCursorOverride();
        }

        void UpdateArmedHover()
        {
            _canTargetUnderMouse = false;
            _hoverHint = string.Empty;
            if (worldCamera == null ||
                HostUiHitTest.ContainsScreenPoint(Input.mousePosition) ||
                !HostPresentationSpace.TryRaycastPlane(worldCamera, Input.mousePosition, out var point))
            {
                ApplyCursor(false);
                return;
            }

            switch (armed)
            {
                case ArmKind.Move:
                    _canTargetUnderMouse = true;
                    _hoverHint = "移动到此处";
                    break;
                case ArmKind.Interact:
                    // 仅热点／人物为绿；麦田其它位置也是红（不用大色带）。
                    if (HostZoneQuery.TryFindWorkSpot(point, out var workSpot))
                    {
                        _canTargetUnderMouse = true;
                        _hoverHint = "交互·" + workSpot.Label;
                    }
                    else if (HostZoneQuery.TryFindLootSpot(point, out var lootSpot))
                    {
                        _canTargetUnderMouse = true;
                        _hoverHint = "拾取·" + lootSpot.Label;
                    }
                    else if (TryPickNpcAtMouse(out var npc) &&
                             selectionController != null &&
                             !selectionController.IsPartyUnit(npc))
                    {
                        _canTargetUnderMouse = true;
                        _hoverHint = "交互·人物";
                    }
                    else
                        _hoverHint = "不可交互";
                    break;
                case ArmKind.Combat:
                    if (TryPickNpcAtMouse(out var foe) &&
                        selectionController != null &&
                        !selectionController.IsPartyUnit(foe))
                    {
                        _canTargetUnderMouse = true;
                        _hoverHint = "战斗（未实装）";
                    }
                    else
                        _hoverHint = "无可战斗目标";
                    break;
                case ArmKind.Cultivate:
                    if (HostZoneQuery.TryFindCultivateSpot(point, out var cultSpot))
                    {
                        _canTargetUnderMouse = true;
                        _hoverHint = "修炼·" + cultSpot.Label;
                    }
                    else
                        _hoverHint = "非修炼点";
                    break;
            }

            ApplyCursor(_canTargetUnderMouse);
        }

        void ConfirmMove(Vector3 point)
        {
            if (moveController != null)
                moveController.OrderPartyToPointPublic(point);
            SetArmed(ArmKind.None);
        }

        void ConfirmInteract(Vector3 point)
        {
            if (!_canTargetUnderMouse)
                return;

            if (HostZoneQuery.TryFindWorkSpot(point, out var spot))
            {
                IssueWorkAtSpot(spot);
                SetArmed(ArmKind.None);
                return;
            }

            if (HostZoneQuery.TryFindLootSpot(point, out var loot))
            {
                IssueLootAtSpot(loot);
                SetArmed(ArmKind.None);
                return;
            }

            if (TryPickNpcAtMouse(out var npc) &&
                selectionController != null &&
                !selectionController.IsPartyUnit(npc) &&
                TryViewCenter(npc, out var center))
            {
                Resume();
                if (moveController != null)
                    moveController.OrderPartyToPointPublic(center);
                SetArmed(ArmKind.None);
            }
        }

        void ConfirmCombat()
        {
            if (!_canTargetUnderMouse)
                return;

            var cam = Camera.main;
            if (cam == null)
            {
                SetArmed(ArmKind.None);
                return;
            }

            var ray = cam.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.forward, Vector3.zero);
            if (!plane.Raycast(ray, out var enter))
            {
                SetArmed(ArmKind.None);
                return;
            }

            var point = ray.GetPoint(enter);
            var session = bootstrap != null ? bootstrap.Session : null;
            var world = session?.World;
            MapLayoutDefinition layout = null;
            if (session != null)
                MapLayoutPick.TryGet(session, out layout);

            string coreId = null;
            if (world != null &&
                HostControlCoreQuery.TryPickAtWorld(world, layout, point, out coreId) &&
                world.ControlCores.TryGet(coreId, out var core))
            {
                Resume();
                if (HostControlCoreQuery.TryGetApproachPoint(world, layout, core, out var target) &&
                    moveController != null)
                    moveController.OrderPartyToPointPublic(target);

                var housingSel = bootstrap != null
                    ? bootstrap.GetComponent<HostHousingAreaSelection>()
                    : null;
                housingSel?.SelectControlCore(core.WorkAreaId);

                var assault = bootstrap != null
                    ? bootstrap.GetComponent<HostControlCoreAssault>()
                    : null;
                if (core.PlayerControlled)
                {
                    Debug.Log("[Host] 主管府已占领。");
                }
                else if (assault != null)
                {
                    assault.Begin(core.WorkAreaId);
                    Debug.Log("[Host] 开始突击主管府：靠近建筑每秒 -" +
                              ControlCoreService.TestMeleeDamagePerHit +
                              "；破门后站满 " + core.OccupyHoldSeconds + " 秒占领。");
                }
                else
                {
                    Debug.LogWarning("[Host] HostControlCoreAssault 未挂载。");
                }

                SetArmed(ArmKind.None);
                return;
            }

            Debug.Log("[Host] Combat: 点主管府近战占领；NPC 请右键→攻击（地图互砍）。");
            SetArmed(ArmKind.None);
        }

        void ConfirmCultivate(Vector3 point)
        {
            if (!_canTargetUnderMouse)
                return;
            if (!HostZoneQuery.TryFindCultivateSpot(point, out var spot))
                return;
            IssueCultivateAtSpot(spot);
            SetArmed(ArmKind.None);
        }

        void IssueWorkAtSpot(HostInteractSpot spot)
        {
            Resume();
            var locId = spot.LocationId;
            if (string.IsNullOrEmpty(locId) && bootstrap?.Session != null)
                locId = HostZoneQuery.FindWorkLocation(bootstrap.Session.World, spot.WorldPosition);

            if (moveController != null)
                moveController.OrderPartyToPointThen(spot.WorldPosition, PlayerCommandKind.Labor, locId);
            else
                FallbackSnapAndIssue(locId, PlayerCommandKind.Labor);

            var loop = bootstrap != null ? bootstrap.GetComponent<HostWorkLoop>() : null;
            if (loop != null && selectionController != null)
            {
                for (var i = 0; i < selectionController.State.Count; i++)
                {
                    var id = selectionController.State.SelectedIds[i];
                    if (selectionController.IsPartyUnit(id))
                        loop.StartLoop(id);
                }
            }
        }

        void IssueCultivateAtSpot(HostInteractSpot spot)
        {
            Resume();
            if (moveController != null)
                moveController.OrderPartyToPointThen(spot.WorldPosition, PlayerCommandKind.Cultivate);
            else
                FallbackSnapAndIssue(spot.LocationId, PlayerCommandKind.Cultivate);
        }

        void IssueExploreAtSpot(HostInteractSpot spot)
        {
            Resume();
            var locId = spot.LocationId;
            if (moveController != null)
                moveController.OrderPartyToPointThen(spot.WorldPosition, PlayerCommandKind.Explore, locId);
            else
                FallbackSnapAndIssue(locId, PlayerCommandKind.Explore);
        }

        void IssueLootAtSpot(HostInteractSpot spot)
        {
            Resume();
            if (string.IsNullOrEmpty(spot.LootItemId) || string.IsNullOrEmpty(spot.LootSpotId))
                return;

            var actor = HostNpcInteraction.ResolvePartyActor(selectionController);
            if (actor.IsNone)
                return;

            System.Action doPickup = () =>
            {
                if (commandBridge == null)
                    return;
                var ok = commandBridge.IssuePickupLoot(actor, spot.LootSpotId, spot.LootItemId) > 0;
                var overlay = bootstrap != null ? bootstrap.GetComponent<HostFeedbackOverlay>() : null;
                if (overlay != null)
                {
                    overlay.SpawnAtEntity(
                        bootstrap.ViewSpawner,
                        actor,
                        ok ? "拾取·" + spot.Label : "拾取失败",
                        ok ? new Color(0.95f, 0.85f, 0.35f, 1f) : new Color(1f, 0.4f, 0.35f, 1f));
                }

                if (ok)
                    bootstrap?.RefreshMapStampsOnly();
            };

            if (moveController != null)
                moveController.OrderEntityToWorldPointPublic(actor, spot.WorldPosition, doPickup);
            else
                doPickup();
        }

        void Resume()
        {
            if (bootstrap?.Session != null && !bootstrap.Session.World.ContentEvents.HasActive)
                bootstrap.Session.IsPaused = false;
        }

        bool HasCommandableParty()
        {
            if (selectionController == null || selectionController.State.Count == 0)
                return false;
            for (var i = 0; i < selectionController.State.Count; i++)
            {
                if (selectionController.IsPartyUnit(selectionController.State.SelectedIds[i]))
                    return true;
            }

            return false;
        }

        bool TryViewCenter(EntityId id, out Vector3 center)
        {
            center = default;
            var spawner = bootstrap != null ? bootstrap.ViewSpawner : null;
            if (spawner == null || !spawner.Registry.TryGet(id, out var view) || view == null)
                return false;
            center = view.transform.position;
            return true;
        }

        bool TryPickNpcAtMouse(out EntityId id)
        {
            id = EntityId.None;
            var spawner = bootstrap != null ? bootstrap.ViewSpawner : null;
            if (spawner == null || worldCamera == null || selectionController == null)
                return false;

            var ray = worldCamera.ScreenPointToRay(Input.mousePosition);
            EntityView best = null;
            var bestDist = 2.2f;
            foreach (var view in spawner.Registry.All)
            {
                if (view == null || !view.IsBound)
                    continue;
                var d = Vector3.Cross(ray.direction, view.transform.position - ray.origin).magnitude;
                if (d >= bestDist)
                    continue;
                bestDist = d;
                best = view;
            }

            if (best == null)
                return false;
            id = best.EntityId;
            return !id.IsNone;
        }

        void FallbackSnapAndIssue(string locId, PlayerCommandKind kind)
        {
            for (var i = 0; i < selectionController.State.Count; i++)
            {
                var id = selectionController.State.SelectedIds[i];
                if (!selectionController.IsPartyUnit(id))
                    continue;
                if (!bootstrap.Session.World.Entities.TryGet(id, out var entity))
                    continue;
                if (entity.TryGet<EntityLocationComponent>(out var loc))
                    loc.LocationId = locId;
            }

            Resume();
            if (commandBridge != null)
                commandBridge.IssueSelected(kind);
        }

        void ApplyCursor(bool ok)
        {
            EnsureCursors();
            _cursorOverridden = true;
            Cursor.SetCursor(ok ? _cursorGreen : _cursorRed, new Vector2(8f, 8f), CursorMode.Auto);
        }

        void ClearCursorOverride()
        {
            if (!_cursorOverridden)
                return;
            _cursorOverridden = false;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        void EnsureCursors()
        {
            if (_cursorGreen == null)
                _cursorGreen = MakeCursorTex(new Color(0.25f, 0.85f, 0.35f, 0.95f));
            if (_cursorRed == null)
                _cursorRed = MakeCursorTex(new Color(0.9f, 0.25f, 0.22f, 0.95f));
        }

        static Texture2D MakeCursorTex(Color fill)
        {
            const int n = 16;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var dx = x - 7.5f;
                var dy = y - 7.5f;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < 3.2f)
                    t.SetPixel(x, y, Color.white);
                else if (d < 6.5f)
                    t.SetPixel(x, y, fill);
                else
                    t.SetPixel(x, y, clear);
            }

            t.Apply();
            return t;
        }

        void OnGUI()
        {
            if (armed == ArmKind.None)
            {
                if (_idleHoverInteractable && _hoverHint.Length > 0)
                    GUI.Box(new Rect(12f, Screen.height - 48f, 420f, 32f), _hoverHint);
                return;
            }

            var mode = armed == ArmKind.Move ? "移动"
                : armed == ArmKind.Interact ? "交互"
                : armed == ArmKind.Combat ? "战斗"
                : "修炼";
            var msg = mode + "点选：" + (_hoverHint.Length > 0 ? _hoverHint : "…") + "（右键/Esc 取消）";
            GUI.Box(new Rect(12f, Screen.height - 48f, 460f, 32f), msg);
        }
    }
}
