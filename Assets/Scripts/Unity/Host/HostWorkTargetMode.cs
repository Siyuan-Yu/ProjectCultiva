using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Input;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// RTS 点选模式：移动／交互／战斗(占位)／修炼。绿可点、红不可点；右键或 Esc 取消。
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
        string _hoverHint = string.Empty;

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

        /// <summary>兼容旧调用：劳动＝交互工区。</summary>
        public void ArmLabor() => ArmInteract();

        public void Cancel() => SetArmed(ArmKind.None);

        void SetArmed(ArmKind kind)
        {
            armed = kind;
            if (kind == ArmKind.None)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                _hoverHint = string.Empty;
            }
        }

        void OnDisable()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        void Update()
        {
            if (bootstrap == null || bootstrap.Session == null || !bootstrap.Session.IsInitialized)
                return;
            if (bootstrap.Session.World.ContentEvents.HasActive)
            {
                SetArmed(ArmKind.None);
                return;
            }

            if (worldCamera == null)
                worldCamera = Camera.main;
            if (moveController == null && bootstrap != null)
                moveController = bootstrap.GetComponent<HostMoveController>();

            if (armed == ArmKind.None)
                return;

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                SetArmed(ArmKind.None);
                return;
            }

            UpdateHover();

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
                    ConfirmCombat(point);
                    break;
                case ArmKind.Cultivate:
                    ConfirmCultivate(point);
                    break;
            }
        }

        void UpdateHover()
        {
            _canTargetUnderMouse = false;
            _hoverHint = string.Empty;
            if (worldCamera == null ||
                !HostPresentationSpace.TryRaycastPlane(worldCamera, Input.mousePosition, out var point))
            {
                ApplyCursor(false);
                return;
            }

            var world = bootstrap.Session.World;
            switch (armed)
            {
                case ArmKind.Move:
                    _canTargetUnderMouse = true;
                    _hoverHint = "移动到此处";
                    break;
                case ArmKind.Interact:
                {
                    if (HostZoneQuery.TryFindWorkSpot(point, out var workSpot))
                    {
                        _canTargetUnderMouse = true;
                        _hoverHint = "交互·" + workSpot.Label;
                        break;
                    }

                    var work = HostZoneQuery.FindWorkLocation(world, point);
                    if (!string.IsNullOrEmpty(work))
                    {
                        _canTargetUnderMouse = true;
                        _hoverHint = "交互·工区";
                        break;
                    }

                    if (TryPickNpcAtMouse(out var npc) && !selectionController.IsPartyUnit(npc))
                    {
                        _canTargetUnderMouse = true;
                        _hoverHint = "交互·人物";
                        break;
                    }

                    _hoverHint = "不可交互";
                    break;
                }
                case ArmKind.Combat:
                {
                    if (TryPickNpcAtMouse(out var foe) && !selectionController.IsPartyUnit(foe))
                    {
                        _canTargetUnderMouse = true;
                        _hoverHint = "战斗（未实装）";
                        break;
                    }

                    _hoverHint = "无可战斗目标";
                    break;
                }
                case ArmKind.Cultivate:
                {
                    if (HostZoneQuery.TryFindCultivateSpot(point, out var cultSpot))
                    {
                        _canTargetUnderMouse = true;
                        _hoverHint = "修炼·" + cultSpot.Label;
                        break;
                    }

                    var spirit = HostZoneQuery.FindCultivateLocation(world, point);
                    if (!string.IsNullOrEmpty(spirit))
                    {
                        _canTargetUnderMouse = true;
                        _hoverHint = "修炼点";
                    }
                    else
                        _hoverHint = "非灵地";
                    break;
                }
            }

            ApplyCursor(_canTargetUnderMouse);
        }

        void ConfirmMove(Vector3 point)
        {
            if (moveController != null)
                moveController.OrderPartyToPointPublic(point);
            else
                return;
            SetArmed(ArmKind.None);
        }

        void ConfirmInteract(Vector3 point)
        {
            var world = bootstrap.Session.World;
            if (HostZoneQuery.TryFindWorkSpot(point, out var spot))
            {
                bootstrap.Session.IsPaused = false;
                if (moveController != null)
                    moveController.OrderPartyToPointThen(spot.WorldPosition, PlayerCommandKind.Labor);
                else
                    FallbackSnapAndIssue(spot.LocationId, PlayerCommandKind.Labor);
                SetArmed(ArmKind.None);
                return;
            }

            var work = HostZoneQuery.FindWorkLocation(world, point);
            if (!string.IsNullOrEmpty(work))
            {
                bootstrap.Session.IsPaused = false;
                if (moveController != null)
                    moveController.OrderPartyToLocation(work, PlayerCommandKind.Labor);
                else
                    FallbackSnapAndIssue(work, PlayerCommandKind.Labor);
                SetArmed(ArmKind.None);
                return;
            }

            if (TryPickNpcAtMouse(out var npc) &&
                selectionController != null &&
                !selectionController.IsPartyUnit(npc))
            {
                // 对话／社交玩法未单独做：先走到对方身边待命。
                bootstrap.Session.IsPaused = false;
                if (moveController != null && TryViewCenter(npc, out var center))
                    moveController.OrderPartyToPointPublic(center);
                SetArmed(ArmKind.None);
                return;
            }

            // 红区点击：不消耗模式，等玩家另选或 Esc。
        }

        void ConfirmCombat(Vector3 _)
        {
            if (!_canTargetUnderMouse)
                return;
            Debug.Log("[Host] Combat target mode: not implemented yet.");
            SetArmed(ArmKind.None);
        }

        void ConfirmCultivate(Vector3 point)
        {
            if (HostZoneQuery.TryFindCultivateSpot(point, out var spot))
            {
                bootstrap.Session.IsPaused = false;
                if (moveController != null)
                    moveController.OrderPartyToPointThen(spot.WorldPosition, PlayerCommandKind.Cultivate);
                else
                    FallbackSnapAndIssue(spot.LocationId, PlayerCommandKind.Cultivate);
                SetArmed(ArmKind.None);
                return;
            }

            var locId = HostZoneQuery.FindCultivateLocation(bootstrap.Session.World, point);
            if (string.IsNullOrEmpty(locId))
                return;
            bootstrap.Session.IsPaused = false;
            if (moveController != null)
                moveController.OrderPartyToLocation(locId, PlayerCommandKind.Cultivate);
            else
                FallbackSnapAndIssue(locId, PlayerCommandKind.Cultivate);
            SetArmed(ArmKind.None);
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

            bootstrap.Session.IsPaused = false;
            if (commandBridge != null)
                commandBridge.IssueSelected(kind);
        }

        void ApplyCursor(bool ok)
        {
            EnsureCursors();
            var tex = ok ? _cursorGreen : _cursorRed;
            Cursor.SetCursor(tex, new Vector2(8f, 8f), CursorMode.Auto);
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
                return;
            var mode = armed == ArmKind.Move ? "移动"
                : armed == ArmKind.Interact ? "交互"
                : armed == ArmKind.Combat ? "战斗"
                : "修炼";
            var msg = mode + "点选：" + (_hoverHint.Length > 0 ? _hoverHint : "…") + "（右键/Esc 取消）";
            GUI.Box(new Rect(12f, Screen.height - 48f, 460f, 32f), msg);
        }
    }
}
