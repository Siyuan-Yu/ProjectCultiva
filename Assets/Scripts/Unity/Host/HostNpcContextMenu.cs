using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Construction;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 右键情境菜单：NPC＝对话／攻击；洞府＝进入；主管府／阵营旗／树／墙＝攻击。
    /// </summary>
    public sealed class HostNpcContextMenu : MonoBehaviour
    {
        const string DismantlePauseOwner = "NpcContext.DismantleConfirm";
        enum Phase
        {
            Closed = 0,
            Menu = 1,
            StrategicAggressionConfirm = 3,
            DismantleConfirm = 4
        }

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostMoveController moveController;
        [SerializeField] HostDialoguePresenter dialoguePresenter;
        [SerializeField] Camera worldCamera;

        Phase _phase = Phase.Closed;
        EntityId _targetNpc = EntityId.None;
        EntityId _actor = EntityId.None;
        EntityId _interactionNpc = EntityId.None;
        string _targetControlCoreWorkAreaId = string.Empty;
        string _targetFactionFlagId = string.Empty;
        string _targetEntranceLocationId = string.Empty;
        HostMapDestructible _targetDestructible;
        WorldObjectInteractionTarget _worldObjectTarget;
        string _targetLabel = string.Empty;
        System.Action _confirmCallback;
        string _aggressionAttackerFactionId = string.Empty;
        string _aggressionDefenderFactionId = string.Empty;
        string _dismantleStatus = string.Empty;
        bool _holdingDismantlePause;
        const string AggressionPauseOwner = "SiteCoreAggressionConfirmation";
        Vector2 _menuScreen;
        Rect _menuGuiRect;

        Texture2D _px;
        GUIStyle _label;
        GUIStyle _button;
        bool _stylesReady;

        static readonly Color Panel = new Color(0.14f, 0.13f, 0.12f, 0.94f);
        static readonly Color Border = new Color(0.72f, 0.58f, 0.38f, 1f);
        static readonly Color Ink = new Color(0.95f, 0.90f, 0.82f, 1f);

        public bool IsOpen => _phase != Phase.Closed;

        bool IsControlCoreTarget => !string.IsNullOrEmpty(_targetControlCoreWorkAreaId);
        bool IsFactionFlagTarget => !string.IsNullOrEmpty(_targetFactionFlagId);
        bool IsCaveEntranceTarget => !string.IsNullOrEmpty(_targetEntranceLocationId);
        bool IsDestructibleTarget =>
            _targetDestructible != null && !_targetDestructible.IsDestroyed;
        bool IsRecoverySpotTarget => _worldObjectTarget.Kind == WorldObjectTargetKind.RecoverySpot &&
                                     _worldObjectTarget.Plot != null;
        bool IsReadOnlyWorldObjectTarget =>
            _worldObjectTarget.Kind == WorldObjectTargetKind.Housing ||
            _worldObjectTarget.Kind == WorldObjectTargetKind.WorkArea ||
            _worldObjectTarget.Kind == WorldObjectTargetKind.StorageRoom;

        public void Bind(
            PlayableHostBootstrap host,
            HostSelectionController selection,
            HostMoveController move,
            HostDialoguePresenter dialogue = null)
        {
            bootstrap = host;
            selectionController = selection;
            moveController = move;
            dialoguePresenter = dialogue;
            if (worldCamera == null)
                worldCamera = Camera.main;
        }

        public void ClearSessionState()
        {
            ReleaseInteractionNpcNow();
            CloseAll();
        }

        public bool TryOpenAtMouse()
        {
            if (IsOpen || bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return false;
            if (HostInputGate.BlockWorldInteraction)
                return false;
            if (HostUiHitTest.ContainsScreenPoint(Input.mousePosition))
                return false;

            var actor = HostNpcInteraction.ResolveActiveCommandAuthority(bootstrap?.Session);
            if (actor.IsNone)
                return false;

            if (worldCamera == null)
                worldCamera = Camera.main;
            var spawner = bootstrap.ViewSpawner;

            if (HostNpcPicker.TryPickAtMouse(worldCamera, spawner, out var npc, out _) &&
                !selectionController.IsPartyUnit(npc))
            {
                _actor = actor;
                _targetNpc = npc;
                _targetControlCoreWorkAreaId = string.Empty;
                _targetFactionFlagId = string.Empty;
                _targetEntranceLocationId = string.Empty;
                _targetDestructible = null;
                _targetLabel = ResolveDisplayName(npc);
                _menuScreen = Input.mousePosition;
                _phase = Phase.Menu;
                HostInputGate.BlockWorldInteraction = true;
                return true;
            }

            MapLayoutDefinition layout = null;
            if (bootstrap.ContinuousOutdoorSurfaceRuntime == null ||
                !bootstrap.ContinuousOutdoorSurfaceRuntime.IsActive)
                MapLayoutPick.TryGet(bootstrap.Session, out layout);

            // 地表已显形洞府 → 进入
            if (!bootstrap.Session.World.LocalMap.IsInInterior &&
                HostCaveEntranceQuery.TryPickAtMouse(
                    worldCamera, bootstrap.Session.World, layout,
                    bootstrap.ContinuousOutdoorSurfaceRuntime, out var entranceId) &&
                (bootstrap.Session.World.ContinuousOutdoorMaterialization.TryGetAnyPlace(entranceId, out var entrance) ||
                 bootstrap.Session.World.LocalPlaces.TryGet(entranceId, out entrance)))
            {
                var near = HostCaveEntranceQuery.IsNearEntrance(
                    bootstrap, actor, entrance, out var distance);
                Debug.Log("[CaveApproach] entranceId=" + entranceId +
                    " distance=" + distance.ToString("0.##") +
                    " action=" + (near ? "OpenEnterMenu" : "Move"), this);
                if (!near)
                    return false;
                _actor = actor;
                _targetNpc = EntityId.None;
                _targetControlCoreWorkAreaId = string.Empty;
                _targetFactionFlagId = string.Empty;
                _targetEntranceLocationId = entranceId;
                _targetDestructible = null;
                _targetLabel = string.IsNullOrEmpty(entrance.Name) ? "洞府入口" : entrance.Name;
                _menuScreen = Input.mousePosition;
                _phase = Phase.Menu;
                HostInputGate.BlockWorldInteraction = true;
                return true;
            }

            if (HostWorldObjectPicker.TryPickAtScreenPoint(
                    bootstrap, worldCamera, Input.mousePosition, out var objectTarget))
            {
                if (objectTarget.Kind == WorldObjectTargetKind.FarmPlot)
                    return bootstrap.WorkTargetMode != null &&
                           bootstrap.WorkTargetMode.TryHandleContextTarget(objectTarget);
                _actor = actor;
                _targetNpc = EntityId.None;
                _targetEntranceLocationId = string.Empty;
                SetWorldObjectTarget(objectTarget);
                _menuScreen = Input.mousePosition;
                _phase = Phase.Menu;
                HostInputGate.BlockWorldInteraction = true;
                return true;
            }

            return false;
        }

        void SetWorldObjectTarget(WorldObjectInteractionTarget target)
        {
            _worldObjectTarget = target;
            _targetControlCoreWorkAreaId = target.Kind == WorldObjectTargetKind.ControlCore ? target.WorkAreaId : string.Empty;
            _targetFactionFlagId = target.Kind == WorldObjectTargetKind.FactionFlag ? target.FactionFlagId : string.Empty;
            _targetDestructible = target.Kind == WorldObjectTargetKind.Destructible ? target.Destructible : null;
            _targetLabel = target.DisplayLabel;
        }

        void ShowWorldObjectDetails()
        {
            bootstrap?.GetComponent<HostHousingAreaSelection>()?.Inspect.Set(_worldObjectTarget);
            CloseAll();
        }

        void Update()
        {
            if (_phase == Phase.Closed)
            {
                TryReleaseInteractionNpc();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
                CloseAll();
            TryReleaseInteractionNpc();
        }

        void OnGUI()
        {
            if (_phase == Phase.Closed)
                return;

            EnsureStyles();
            switch (_phase)
            {
                case Phase.Menu:
                    if (IsCaveEntranceTarget)
                        DrawCaveMenu();
                    else if (IsControlCoreTarget)
                        DrawControlCoreMenu();
                    else if (IsFactionFlagTarget)
                        DrawFactionFlagMenu();
                    else if (IsDestructibleTarget)
                        DrawDestructibleMenu();
                    else if (IsRecoverySpotTarget)
                        DrawRecoverySpotMenu();
                    else if (IsReadOnlyWorldObjectTarget)
                        DrawReadOnlyWorldObjectMenu();
                    else
                        DrawContextMenu();
                    break;
                case Phase.StrategicAggressionConfirm:
                    DrawAttackConfirm(
                        "军事侵略确认",
                        BuildAggressionConfirmText(),
                        "确认攻击",
                        ConfirmStrategicAggression,
                        CloseAll);
                    break;
                case Phase.DismantleConfirm:
                    DrawDismantleConfirm();
                    break;
            }
        }

        void DrawCaveMenu()
        {
            const float w = 168f;
            const float itemH = 30f;
            var h = itemH + 34f;
            var guiX = Mathf.Clamp(_menuScreen.x, 4f, Screen.width - w - 4f);
            var guiY = Mathf.Clamp(Screen.height - _menuScreen.y, 4f, Screen.height - h - 4f);
            _menuGuiRect = new Rect(guiX, guiY, w, h);
            HostUiHitTest.Block(_menuGuiRect);

            Fill(_menuGuiRect, Panel);
            DrawFrame(_menuGuiRect, Border);

            GUI.Label(new Rect(guiX + 10f, guiY + 6f, w - 20f, 22f), _targetLabel, _label);
            var y = guiY + 30f;
            if (GUI.Button(new Rect(guiX + 8f, y, w - 16f, itemH - 4f), "进入", _button))
                BeginCaveEnter();
            TryDismissOnOutsideClick(_menuGuiRect);
        }

        void BeginCaveEnter()
        {
            var actor = _actor;
            var entranceId = _targetEntranceLocationId;
            CloseAll();
            if (actor.IsNone || string.IsNullOrEmpty(entranceId) || bootstrap?.CommandBridge == null)
                return;

            // SPACE-01：当前随队成员全部自动进入，不再弹出队员选择窗。
            if (bootstrap.CommandBridge.IssueEnterSeparateSpace(actor, entranceId) <= 0)
                Debug.LogWarning("[Host] Enter Separate Space failed: " + bootstrap.CommandBridge.LastStatus);
        }

        void DrawControlCoreMenu()
        {
            const float w = 220f;
            const float itemH = 30f;
            var session = bootstrap?.Session;
            var world = session?.World;
            WorldSite site = null;
            var hasSite = world != null &&
                WorldSiteCoreWarfareService.TryGetBoundSiteForFixedCore(
                    world, _targetControlCoreWorkAreaId, out site);
            var friendly = hasSite &&
                string.Equals(site.OwnerFactionId, world.Strategic.PlayerFactionId, System.StringComparison.Ordinal);
            var canAttack = hasSite && !friendly;
            var h = canAttack ? itemH * 2f + 58f : itemH + 34f;
            var guiX = Mathf.Clamp(_menuScreen.x, 4f, Screen.width - w - 4f);
            var guiY = Mathf.Clamp(Screen.height - _menuScreen.y, 4f, Screen.height - h - 4f);
            _menuGuiRect = new Rect(guiX, guiY, w, h);
            HostUiHitTest.Block(_menuGuiRect);

            Fill(_menuGuiRect, Panel);
            DrawFrame(_menuGuiRect, Border);

            GUI.Label(new Rect(guiX + 10f, guiY + 6f, w - 20f, 22f), _targetLabel, _label);
            var y = guiY + 30f;
            if (GUI.Button(new Rect(guiX + 8f, y, w - 16f, itemH - 4f), "查看详情", _button))
                ShowWorldObjectDetails();
            y += itemH;
            if (canAttack && GUI.Button(new Rect(guiX + 8f, y, w - 16f, itemH - 4f), "攻击据点核心", _button))
                BeginControlCoreAttack();
            if (canAttack)
                GUI.Label(new Rect(guiX + 10f, y + itemH, w - 20f, 22f), "攻破后可占领该据点。", _label);
            TryDismissOnOutsideClick(_menuGuiRect);
        }

        void DrawDestructibleMenu()
        {
            const float w = 168f;
            const float itemH = 30f;
            var h = itemH * 2f + 34f;
            var guiX = Mathf.Clamp(_menuScreen.x, 4f, Screen.width - w - 4f);
            var guiY = Mathf.Clamp(Screen.height - _menuScreen.y, 4f, Screen.height - h - 4f);
            _menuGuiRect = new Rect(guiX, guiY, w, h);
            HostUiHitTest.Block(_menuGuiRect);

            Fill(_menuGuiRect, Panel);
            DrawFrame(_menuGuiRect, Border);

            GUI.Label(new Rect(guiX + 10f, guiY + 6f, w - 20f, 22f), _targetLabel, _label);
            var y = guiY + 30f;
            if (GUI.Button(new Rect(guiX + 8f, y, w - 16f, itemH - 4f), "查看详情", _button))
                ShowWorldObjectDetails();
            y += itemH;
            var verb = _targetDestructible != null && _targetDestructible.IsTree ? "砍伐" : "拆毁";
            if (GUI.Button(new Rect(guiX + 8f, y, w - 16f, itemH - 4f), verb, _button))
                BeginDestructibleAttack();
            TryDismissOnOutsideClick(_menuGuiRect);
        }

        void DrawRecoverySpotMenu()
        {
            const float w = 184f;
            const float itemH = 30f;
            var h = itemH * 2f + 34f;
            var x = Mathf.Clamp(_menuScreen.x, 4f, Screen.width - w - 4f);
            var y = Mathf.Clamp(Screen.height - _menuScreen.y, 4f, Screen.height - h - 4f);
            _menuGuiRect = new Rect(x, y, w, h);
            HostUiHitTest.Block(_menuGuiRect);
            Fill(_menuGuiRect, Panel);
            DrawFrame(_menuGuiRect, Border);
            GUI.Label(new Rect(x + 10f, y + 6f, w - 20f, 22f), "恢复处", _label);
            if (GUI.Button(new Rect(x + 8f, y + 30f, w - 16f, itemH - 4f), "查看详情", _button))
                ShowWorldObjectDetails();
            if (GUI.Button(new Rect(x + 8f, y + 60f, w - 16f, itemH - 4f), "休息恢复", _button))
                BeginRecoverySpot();
            TryDismissOnOutsideClick(_menuGuiRect);
        }

        void BeginRecoverySpot()
        {
            var actor = _actor;
            var plot = _worldObjectTarget.Plot;
            var stableId = plot != null ? plot.StableCellId : string.Empty;
            var destination = plot != null ? plot.transform.position : Vector3.zero;
            var message = string.Empty;
            CloseAll();
            if (actor.IsNone || string.IsNullOrWhiteSpace(stableId) || !CanBeginRecovery(actor, out message))
            {
                ShowRecoveryFeedback(actor, string.IsNullOrEmpty(message) ? "恢复处不可用。" : message, false);
                return;
            }
            if (bootstrap.ViewSpawner != null && bootstrap.ViewSpawner.Registry.TryGet(actor, out var view) &&
                view != null && Vector3.Distance(view.transform.position, destination) > 1.5f)
            {
                if (moveController == null || !moveController.OrderEntityToWorldPointPublic(actor, destination,
                        () => CompleteRecoverySpotApproach(actor, stableId)))
                    ShowRecoveryFeedback(actor, "无法前往恢复处。", false);
                return;
            }
            CompleteRecoverySpotApproach(actor, stableId);
        }

        void CompleteRecoverySpotApproach(EntityId actor, string stableId)
        {
            var message = string.Empty;
            if (!TryFindRecoverySpot(stableId, out _) || !CanBeginRecovery(actor, out message))
            {
                ShowRecoveryFeedback(actor, string.IsNullOrEmpty(message) ? "恢复处已不可用。" : message, false);
                return;
            }
            var issued = bootstrap.CommandBridge != null &&
                         bootstrap.CommandBridge.IssueRecovery(actor, stableId) > 0;
            if (!issued)
            {
                ShowRecoveryFeedback(actor, bootstrap.CommandBridge?.LastStatus ?? "恢复指令失败。", false);
                return;
            }
            if (bootstrap.Session.ManualPaused && !bootstrap.Session.ModalHardPaused)
                bootstrap.Session.ManualPaused = false;
            ShowRecoveryFeedback(actor, "开始恢复（30分钟）", true);
        }

        bool CanBeginRecovery(EntityId actor, out string message)
        {
            message = string.Empty;
            var world = bootstrap?.Session?.World;
            if (world == null || world.Strategic.CharacterEncounter?.Phase == CharacterEncounterPhase.Active ||
                world.Strategic.ClockFreeze.Reason != StrategicClockFreezeReason.None)
            {
                message = "战斗中不可使用。";
                return false;
            }
            if (!world.Entities.TryGet(actor, out var entity))
            {
                message = "恢复角色不存在。";
                return false;
            }
            var can = CombatRecoveryService.CanRecover(entity);
            if (can.IsFailure) message = can.Error.Message;
            return can.IsSuccess;
        }

        static bool TryFindRecoverySpot(string stableId, out HostMapPlotCell plot)
        {
            plot = null;
            var plots = HostMapObjectRegistry.AllPlots;
            for (var i = 0; i < plots.Count; i++)
                if (plots[i] != null && plots[i].IsRecoverySpot &&
                    string.Equals(plots[i].StableCellId, stableId, System.StringComparison.Ordinal))
                {
                    plot = plots[i];
                    return true;
                }
            return false;
        }

        void ShowRecoveryFeedback(EntityId actor, string message, bool success)
        {
            var overlay = bootstrap != null ? bootstrap.GetComponent<HostFeedbackOverlay>() : null;
            if (overlay != null)
                overlay.SpawnAtEntity(bootstrap.ViewSpawner, actor, message,
                    success ? new Color(.35f, .95f, .85f, 1f) : new Color(1f, .65f, .3f, 1f));
            else
                Debug.Log("[RecoverySpot] " + message);
        }

        void DrawContextMenu()
        {
            const float w = 168f;
            const float itemH = 30f;
            var hostile = HostNpcInteraction.IsHostileNpc(bootstrap?.Session, _targetNpc);
            var canAttack = CanInitiatePlayerHostileAction(_actor, _targetNpc);
            var rows = hostile ? 1 : 2;
            var h = itemH * rows + 34f;
            var guiX = Mathf.Clamp(_menuScreen.x, 4f, Screen.width - w - 4f);
            var guiY = Mathf.Clamp(Screen.height - _menuScreen.y, 4f, Screen.height - h - 4f);
            _menuGuiRect = new Rect(guiX, guiY, w, h);
            HostUiHitTest.Block(_menuGuiRect);

            Fill(_menuGuiRect, Panel);
            DrawFrame(_menuGuiRect, Border);

            GUI.Label(
                new Rect(guiX + 10f, guiY + 6f, w - 20f, 22f),
                hostile ? _targetLabel + "（敌对）" : _targetLabel,
                _label);
            var y = guiY + 30f;
            if (!hostile)
            {
                if (GUI.Button(new Rect(guiX + 8f, y, w - 16f, itemH - 4f), "对话", _button))
                    BeginTalk();
                y += itemH;
            }

            if (canAttack && GUI.Button(
                    new Rect(guiX + 8f, y, w - 16f, itemH - 4f),
                    hostile ? "攻击" : "攻击…",
                    _button))
            {
                // 点击 Attack 立即按 Character 目标分类，不等移动完成后再决定 Encounter。
                var consumed = TryHandlePlayerHostileAction(_actor, _targetNpc, null);
                if (!consumed)
                    BeginAttack();
            }

            TryDismissOnOutsideClick(_menuGuiRect);
        }

        void DrawFactionFlagMenu()
        {
            const float w = 300f;
            const float itemH = 30f;
            var world = bootstrap?.Session?.World;
            if (world == null || !world.Strategic.FactionFlags.Flags.TryGetValue(_targetFactionFlagId, out var flag) || flag == null)
            {
                CloseAll();
                return;
            }
            var friendly = string.Equals(flag.FactionId, world.Strategic.PlayerFactionId, System.StringComparison.Ordinal);
            var h = itemH * 2f + 34f;
            var guiX = Mathf.Clamp(_menuScreen.x, 4f, Screen.width - w - 4f);
            var guiY = Mathf.Clamp(Screen.height - _menuScreen.y, 4f, Screen.height - h - 4f);
            _menuGuiRect = new Rect(guiX, guiY, w, h);
            HostUiHitTest.Block(_menuGuiRect);
            Fill(_menuGuiRect, Panel);
            DrawFrame(_menuGuiRect, Border);
            GUI.Label(new Rect(guiX + 10f, guiY + 6f, w - 20f, 22f),
                friendly ? "阵营控制建筑" : _targetLabel, _label);
            var actionY = guiY + 30f;
            if (GUI.Button(new Rect(guiX + 8f, actionY, w - 16f, itemH - 4f), "查看详情", _button))
                ShowWorldObjectDetails();
            actionY += itemH;
            if (!friendly && GUI.Button(new Rect(guiX + 8f, actionY, w - 16f, itemH - 4f),
                    "攻击势力旗", _button))
                BeginFactionFlagAttack();
            if (friendly && GUI.Button(new Rect(guiX + 8f, actionY, w - 16f, itemH - 4f),
                    "拆除", _button))
                BeginFactionFlagDismantle();
            TryDismissOnOutsideClick(_menuGuiRect);
        }

        void DrawReadOnlyWorldObjectMenu()
        {
            const float w = 168f; const float itemH = 30f;
            var h = itemH + 34f;
            var x = Mathf.Clamp(_menuScreen.x, 4f, Screen.width - w - 4f);
            var y = Mathf.Clamp(Screen.height - _menuScreen.y, 4f, Screen.height - h - 4f);
            _menuGuiRect = new Rect(x, y, w, h); HostUiHitTest.Block(_menuGuiRect);
            Fill(_menuGuiRect, Panel); DrawFrame(_menuGuiRect, Border);
            GUI.Label(new Rect(x + 10f, y + 6f, w - 20f, 22f), _targetLabel, _label);
            if (GUI.Button(new Rect(x + 8f, y + 30f, w - 16f, itemH - 4f), "查看详情", _button))
                ShowWorldObjectDetails();
            TryDismissOnOutsideClick(_menuGuiRect);
        }

        void BeginFactionFlagDismantle()
        {
            _dismantleStatus = string.Empty;
            _phase = Phase.DismantleConfirm;
            if (!_holdingDismantlePause && bootstrap?.Session != null)
            {
                bootstrap.Session.AcquireModalPause(DismantlePauseOwner);
                _holdingDismantlePause = true;
            }
        }

        void DrawDismantleConfirm()
        {
            var world = bootstrap?.Session?.World;
            if (world == null ||
                !world.Strategic.FactionFlags.Flags.TryGetValue(_targetFactionFlagId, out var flag) || flag == null ||
                !world.ConstructionCatalog.TryGet(ConstructionService.FactionControlPostBuildingId, out var spec) || spec == null)
            {
                CloseAll();
                return;
            }

            DrawDim();
            var box = ModalBox(420f, 300f);
            HostUiHitTest.Block(box);
            Fill(box, Panel);
            DrawFrame(box, Border);
            GUI.Label(new Rect(box.x + 16f, box.y + 14f, box.width - 32f, 24f),
                "确定拆除此势力控制建筑？", _label);
            var y = box.y + 46f;
            GUI.Label(new Rect(box.x + 16f, y, box.width - 32f, 20f), "建造成本：", _label);
            y += 22f;
            for (var i = 0; i < spec.Costs.Count; i++)
            {
                var cost = spec.Costs[i];
                GUI.Label(new Rect(box.x + 28f, y, box.width - 44f, 20f),
                    world.InventoryCatalog.GetName(cost.ItemId) + " ×" + cost.Count, _label);
                y += 20f;
            }
            GUI.Label(new Rect(box.x + 16f, y + 3f, box.width - 32f, 20f), "将返还：", _label);
            y += 25f;
            var refunds = ConstructionService.CalculateDismantleRefunds(spec);
            for (var i = 0; i < refunds.Count; i++)
            {
                var refund = refunds[i];
                GUI.Label(new Rect(box.x + 28f, y, box.width - 44f, 20f),
                    world.InventoryCatalog.GetName(refund.ItemId) + " ×" + refund.Count, _label);
                y += 20f;
            }
            GUI.Label(new Rect(box.x + 16f, y + 4f, box.width - 32f, 42f),
                "拆除后该控制资产会立即消失，\n势力范围将重新计算。", _label);
            if (!string.IsNullOrEmpty(_dismantleStatus))
                GUI.Label(new Rect(box.x + 16f, box.yMax - 82f, box.width - 32f, 22f),
                    _dismantleStatus, _label);
            var btnW = (box.width - 40f) * .5f;
            var btnY = box.yMax - 44f;
            if (GUI.Button(new Rect(box.x + 14f, btnY, btnW, 32f), "确认拆除", _button))
            {
                var result = ConstructionService.TryDismantleFactionFlag(
                    world, ConstructionService.FactionControlPostBuildingId,
                    world.Strategic.PlayerFactionId, flag.FlagId, out _);
                if (result.IsFailure)
                    _dismantleStatus = result.Error.Message;
                else
                {
                    bootstrap.RefreshFactionFlagWalkGrid();
                    CloseAll();
                }
            }
            if (GUI.Button(new Rect(box.x + 22f + btnW, btnY, btnW, 32f), "取消", _button))
                CloseAll();
        }

        void BeginFactionFlagAttack()
        {
            var world = bootstrap?.Session?.World;
            if (world == null || !world.Strategic.FactionFlags.Flags.TryGetValue(_targetFactionFlagId, out var flag) || flag == null)
            {
                CloseAll();
                return;
            }
            if (!HostWorldSiteCoreWarfare.ValidateTarget(bootstrap, _actor, flag.SiteId)) { CloseAll(); return; }
            BeginStrategicAggressionIfNeeded(
                world.Strategic.PlayerFactionId, flag.FactionId, BeginFactionFlagAttackAfterAggression);
        }

        void BeginFactionFlagAttackAfterAggression()
        {
            var world = bootstrap?.Session?.World;
            if (world == null || !world.Strategic.FactionFlags.Flags.TryGetValue(_targetFactionFlagId, out var flag)) { CloseAll(); return; }
            var siteId = flag.SiteId;
            var actor = _actor;
            CloseAll();
            ResumeTime();
            HostWorldSiteCoreWarfare.BeginConfirmed(bootstrap, actor, siteId);
        }

        void DrawAttackConfirm(string title, string body, string okLabel, System.Action onOk, System.Action onCancel)
        {
            DrawDim();
            var box = ModalBox(360f, 200f);
            HostUiHitTest.Block(box);
            Fill(box, Panel);
            DrawFrame(box, Border);
            GUI.Label(new Rect(box.x + 16f, box.y + 14f, box.width - 32f, 24f), title, _label);
            GUI.Label(new Rect(box.x + 16f, box.y + 44f, box.width - 32f, 72f), body, _label);
            var btnW = (box.width - 40f) * 0.5f;
            var btnY = box.yMax - 44f;
            if (GUI.Button(new Rect(box.x + 14f, btnY, btnW, 32f), okLabel, _button))
                onOk?.Invoke();
            if (GUI.Button(new Rect(box.x + 22f + btnW, btnY, btnW, 32f), "取消", _button))
                onCancel?.Invoke();
            TryDismissOnOutsideClick(box);
        }

        void BeginControlCoreAttack()
        {
            var session = bootstrap?.Session;
            var world = session?.World;
            var coreId = _targetControlCoreWorkAreaId;
            if (world == null || string.IsNullOrEmpty(coreId) ||
                !world.ControlCores.TryGet(coreId, out var core))
            {
                CloseAll();
                return;
            }

            var playerFaction = world.Strategic?.PlayerFactionId ?? string.Empty;
            if (!WorldSiteCoreWarfareService.TryGetBoundSiteForFixedCore(world, core.WorkAreaId, out var site))
            {
                HostWorldSiteCoreWarfare.Feedback(bootstrap, "议政厅缺少正式据点绑定，无法发起攻击。");
                CloseAll();
                return;
            }
            var siteId = site.SiteId;
            if (!HostWorldSiteCoreWarfare.ValidateTarget(bootstrap, _actor, siteId)) { CloseAll(); return; }
            BeginStrategicAggressionIfNeeded(playerFaction, site.OwnerFactionId, BeginControlCoreAttackAfterAggression);
            return;
        }

        void BeginControlCoreAttackAfterAggression()
        {
            var world = bootstrap?.Session?.World;
            if (world == null || !world.ControlCores.TryGet(_targetControlCoreWorkAreaId, out var core) ||
                !WorldSiteCoreWarfareService.TryGetBoundSiteForFixedCore(world, core.WorkAreaId, out var site))
            {
                HostWorldSiteCoreWarfare.Feedback(bootstrap, "议政厅的正式据点绑定已失效，攻击已取消。");
                CloseAll(); return;
            }
            var siteId = site.SiteId;
            var actor = _actor;
            CloseAll();
            ResumeTime();
            HostWorldSiteCoreWarfare.BeginConfirmed(bootstrap, actor, siteId);
        }

        /// <summary>F8 等非右键入口复用完全相同的议政厅攻城请求与政治确认链。</summary>
        public bool TryRequestWorldSiteSiege(string controlCoreWorkAreaId)
        {
            var session = bootstrap?.Session;
            var world = session?.World;
            if (world == null || string.IsNullOrEmpty(controlCoreWorkAreaId) ||
                !world.ControlCores.TryGet(controlCoreWorkAreaId, out var core))
                return false;
            _targetControlCoreWorkAreaId = controlCoreWorkAreaId;
            _targetLabel = string.IsNullOrEmpty(core.Name) ? "议政厅" : core.Name;
            _actor = HostNpcInteraction.ResolveActiveCommandAuthority(session);
            BeginControlCoreAttack();
            return true;
        }


        void BeginTalk()
        {
            if (moveController == null || _actor.IsNone || _targetNpc.IsNone)
            {
                CloseAll();
                return;
            }

            if (!moveController.OrderActorToNpc(_actor, _targetNpc, HostNpcArriveAction.Talk))
            {
                CloseAll();
                return;
            }

            _interactionNpc = _targetNpc;
            ResumeTime();
            CloseAll();
        }

        void BeginDestructibleAttack()
        {
            var target = _targetDestructible;
            if (target == null || target.IsDestroyed || _actor.IsNone)
            {
                CloseAll();
                return;
            }

            if (moveController != null)
            {
                var dest = target.transform.position;
                dest.z = HostPresentationSpace.EntityZ;
                moveController.OrderPartyToPointPublic(dest);
            }

            bootstrap.GetComponent<HostHousingAreaSelection>()?.SelectDestructible(target);
            var assault = bootstrap.GetComponent<HostDestructibleAssault>();
            if (assault != null)
                assault.Begin(_actor, target);
            else
                Debug.LogWarning("[Host] HostDestructibleAssault 未挂载。");

            var overlay = bootstrap.GetComponent<HostFeedbackOverlay>();
            if (overlay != null)
            {
                overlay.SpawnAtEntity(
                    bootstrap.ViewSpawner,
                    _actor,
                    (_targetDestructible.IsTree ? "砍伐 " : "拆毁 ") + _targetLabel,
                    new Color(0.65f, 0.9f, 0.45f, 1f));
            }

            ResumeTime();
            CloseAll();
        }

        void BeginAttack()
        {
            if (_targetNpc.IsNone)
            {
                CloseAll();
                return;
            }

            var npc = _targetNpc;
            CollectSelectedPartyAttackers(_scratchAttackers);
            var characterEncounter = bootstrap?.Session?.World?.Strategic?.CharacterEncounter;
            if (characterEncounter != null &&
                characterEncounter.Phase == CharacterEncounterPhase.ReadyToEnd)
            {
                _scratchAttackers.Clear();
                var active = bootstrap.Session.PlayerParty.ActiveCharacterId;
                if (!active.IsNone && characterEncounter.Find(active.Value) != null)
                    _scratchAttackers.Add(active);
            }
            if (_scratchAttackers.Count == 0 && !_actor.IsNone &&
                (characterEncounter == null ||
                 characterEncounter.Phase != CharacterEncounterPhase.ReadyToEnd))
                _scratchAttackers.Add(_actor);
            if (_scratchAttackers.Count == 0)
            {
                CloseAll();
                return;
            }

            var melee = bootstrap != null ? bootstrap.GetComponent<HostNpcMeleeAssault>() : null;
            var any = false;
            for (var i = 0; i < _scratchAttackers.Count; i++)
            {
                var actor = _scratchAttackers[i];
                if (actor.IsNone)
                    continue;

                if (melee != null && melee.IsWithinMeleeRange(actor, npc))
                {
                    OnNpcArriveAttack(actor, npc);
                    any = true;
                    continue;
                }

                if (moveController == null)
                    continue;
                if (moveController.OrderActorToNpc(actor, npc, HostNpcArriveAction.Attack))
                {
                    _interactionNpc = npc;
                    any = true;
                }
            }

            if (any)
                ResumeTime();
            CloseAll();
        }

        readonly List<EntityId> _scratchAttackers = new List<EntityId>(4);

        void CollectSelectedPartyAttackers(List<EntityId> into)
        {
            into.Clear();
            if (selectionController == null)
                return;
            for (var i = 0; i < selectionController.State.Count; i++)
            {
                var id = selectionController.State.SelectedIds[i];
                if (!selectionController.IsPartyUnit(id))
                    continue;
                into.Add(id);
            }
        }

        public void OnNpcArriveTalk(EntityId actor, EntityId npc)
        {
            var session = bootstrap?.Session;
            if (session == null || !session.IsInitialized || actor.IsNone)
                return;

            _targetLabel = ResolveDisplayName(npc);

            if (!HostNpcInteraction.TryResolveDefinitionId(session, npc, out var npcDefId))
            {
                ShowFallbackTalk("（无法识别对话对象）");
                return;
            }

            var talk = new ContentEventService();
            talk.TryTalkToNpc(session.World, actor, npcDefId);
            bootstrap.DispatchDrainedEvents();

            if (session.World.ContentEvents.HasActive &&
                dialoguePresenter != null &&
                dialoguePresenter.TryPresentOnTalk(actor, npc))
                return;

            if (session.World.ContentEvents.HasActive)
                return;

            ShowFallbackTalk("（" + _targetLabel + " 暂无对话内容）");
        }

        /// <summary>
        /// Host pre-damage coordinator（右键攻击 / 主动技能共用，单一路由，禁止复制两套判断）。
        /// 返回 true = 本次输入已被人物遭遇确认或拒绝消费；
        /// 返回 false = caller 应直接执行本地伤害动作（仅当前 CharacterEncounter participant 或 Separate Space 原地战）。
        /// </summary>
        public bool TryHandlePlayerHostileAction(
            EntityId actor,
            EntityId target,
            System.Action onConfirmedLocalAction)
        {
            var session = bootstrap?.Session;
            if (session == null || !session.IsInitialized || actor.IsNone || target.IsNone)
                return true;

            var characterEncounter = session.World.Strategic.CharacterEncounter;
            if (characterEncounter != null &&
                characterEncounter.Phase == CharacterEncounterPhase.ReadyToEnd)
            {
                var active = session.PlayerParty.ActiveCharacterId;
                if (active.IsNone || !characterEncounter.Opposing(active.Value, target.Value) ||
                    !session.World.Entities.TryGet(active, out var activeEntity) ||
                    !CombatLifeStateService.CanFight(activeEntity) ||
                    !session.World.Entities.TryGet(target, out var targetEntity) ||
                    !CombatLifeStateService.CanBeAttacked(targetEntity))
                {
                    Debug.LogWarning("[Host] ReadyToEnd hostile action rejected outside the active participant scope.");
                    ReleaseInteractionNpcNow(target);
                    CloseAll();
                    return true;
                }

                ReleaseInteractionNpcNow(target);
                bootstrap.GetComponent<HostCharacterEncounter>()?.SetTarget(active, target);
                ResumeTime();
                CloseAll();
                return true;
            }

            var route = LocalHostileActionRoutingService.Route(
                session.World, session.PlayerParty, actor, target);
            switch (route.Route)
            {
                case HostileActionRoute.LocalCombat:
                    // SPACE-01：双方均在 Separate Space → 原地战斗，不创建 CharacterEncounter。
                    if (SeparateSpaceCombatPolicy.IsInPlaceCombatSpace(session.World))
                    {
                        if (!SeparateSpaceCombatPolicy.AreBothInActiveSeparateSpace(
                                session.World, actor, target))
                        {
                            Debug.LogWarning("[Host] Hostile action rejected: 目标不在当前独立空间。");
                            ReleaseInteractionNpcNow(target);
                            CloseAll();
                            return true;
                        }

                        if (IsActiveEncounterCombatTarget(session.World, target))
                            return false;
                        return false;
                    }

                    // 已处于 active CharacterEncounter 的 hostile participant → 直接 tactical combat。
                    if (IsActiveEncounterCombatTarget(session.World, target))
                        return false;
                    // Continuous Outdoor 普通 Character → CharacterEncounter。
                    ReleaseInteractionNpcNow(target);
                    CloseAll();
                    bootstrap.GetComponent<HostCharacterEncounter>().Request(actor, target, onEntered: onConfirmedLocalAction);
                    return true;

                case HostileActionRoute.Reject:
                    Debug.LogWarning("[Host] Hostile action rejected: " + route.FailureReason);
                    ReleaseInteractionNpcNow(target);
                    CloseAll();
                    return true;

                default:
                    Debug.LogWarning("[Host] Unsupported character hostile-action route: " + route.Route);
                    ReleaseInteractionNpcNow(target);
                    CloseAll();
                    return true;
            }
        }

        bool BeginStrategicAggressionIfNeeded(string attackerFactionId, string defenderFactionId, System.Action afterCommit)
        {
            var world = bootstrap?.Session?.World;
            if (!StrategicMilitaryAggressionService.TryPreview(
                    world, attackerFactionId, defenderFactionId, out var preview, out var reason))
            {
                Debug.LogWarning("[Host] 军事侵略预览失败：" + reason);
                HostWorldSiteCoreWarfare.Feedback(bootstrap, "无法确认攻击：" + reason);
                CloseAll();
                return false;
            }
            if (!preview.RequiresConfirmation)
            {
                afterCommit?.Invoke();
                return true;
            }

            _aggressionAttackerFactionId = attackerFactionId;
            _aggressionDefenderFactionId = defenderFactionId;
            _confirmCallback = afterCommit;
            _phase = Phase.StrategicAggressionConfirm;
            bootstrap.Session.AcquireModalPause(AggressionPauseOwner);
            HostInputGate.BlockWorldInteraction = true;
            return false;
        }

        string BuildAggressionConfirmText()
        {
            var world = bootstrap?.Session?.World;
            if (StrategicMilitaryAggressionService.TryPreview(
                    world, _aggressionAttackerFactionId, _aggressionDefenderFactionId, out var preview, out _))
                return "当前与目标势力关系：" + FormatRelation(preview.Relation) + "\n" + preview.Description;
            return "无法确认本次军事侵略。";
        }

        void ConfirmStrategicAggression()
        {
            var world = bootstrap?.Session?.World;
            if (!StrategicMilitaryAggressionService.TryCommit(
                    world, _aggressionAttackerFactionId, _aggressionDefenderFactionId, out var reason))
            {
                Debug.LogWarning("[Host] 军事侵略提交失败：" + reason);
                HostWorldSiteCoreWarfare.Feedback(bootstrap, "宣战失败：" + reason);
                CloseAll();
                return;
            }
            var callback = _confirmCallback;
            _confirmCallback = null;
            callback?.Invoke();
        }

        static string FormatRelation(FactionDiplomacyRelation relation)
        {
            switch (relation)
            {
                case FactionDiplomacyRelation.War: return "战争";
                case FactionDiplomacyRelation.Alliance: return "联盟";
                case FactionDiplomacyRelation.Overlord: return "宗主";
                case FactionDiplomacyRelation.Vassal: return "附庸";
                case FactionDiplomacyRelation.Self: return "自身";
                default: return "普通";
            }
        }

        static bool IsActiveEncounterCombatTarget(SimulationWorld world, EntityId targetId)
        {
            if (world == null || targetId.IsNone ||
                !world.Entities.TryGet(targetId, out var entity) || entity == null)
                return false;
            return CharacterEncounterHostilityService.IsHostileEncounterParticipant(world, entity);
        }

        public void OnNpcArriveAttack(EntityId actor, EntityId npc)
        {
            if (bootstrap?.Session?.World == null || actor.IsNone || npc.IsNone) return;
            ReleaseInteractionNpcNow(npc);
            var world = bootstrap.Session.World;
            if (SeparateSpaceCombatPolicy.AreBothInActiveSeparateSpace(world, actor, npc))
            {
                BeginMelee(actor, npc);
                return;
            }

            bootstrap.GetComponent<HostCharacterEncounter>()?.Request(actor, npc, automatic: true);
        }

        void BeginMelee(EntityId actor, EntityId npc)
        {
            var name = ResolveDisplayName(npc);
            var melee = bootstrap != null ? bootstrap.GetComponent<HostNpcMeleeAssault>() : null;
            if (melee == null)
            {
                Debug.LogWarning("[Host] HostNpcMeleeAssault missing.");
                return;
            }

            melee.Begin(actor, npc);
            var overlay = bootstrap.GetComponent<HostFeedbackOverlay>();
            if (overlay != null && bootstrap.ViewSpawner != null)
            {
                overlay.SpawnAtEntity(
                    bootstrap.ViewSpawner,
                    actor,
                    "交战 " + name,
                    new Color(1f, 0.45f, 0.35f, 1f));
            }

            ReleaseInteractionNpcNow(npc);
            ResumeTime();
        }

        bool CanInitiatePlayerHostileAction(EntityId actor, EntityId target)
        {
            var session = bootstrap?.Session;
            var encounter = session?.World?.Strategic?.CharacterEncounter;
            if (encounter != null && encounter.Phase == CharacterEncounterPhase.ReadyToEnd)
            {
                var active = session.PlayerParty.ActiveCharacterId;
                return !active.IsNone && encounter.Opposing(active.Value, target.Value) &&
                       session.World.Entities.TryGet(active, out var activeEntity) &&
                       CombatLifeStateService.CanFight(activeEntity) &&
                       session.World.Entities.TryGet(target, out var targetEntity) &&
                       CombatLifeStateService.CanBeAttacked(targetEntity);
            }
            return session != null && LocalHostileActionRoutingService.CanInitiatePlayerHostileAction(
                session.World, session.PlayerParty, actor, target);
        }

        void ShowFallbackTalk(string body)
        {
            if (dialoguePresenter != null)
            {
                dialoguePresenter.ShowFallback(_targetLabel, body);
                return;
            }

            Debug.Log("[Host] Fallback talk: " + _targetLabel + " — " + body);
            ReleaseInteractionNpcNow();
            ResumeTime();
        }

        void CloseAll()
        {
            bootstrap?.Session?.ReleaseModalPause(AggressionPauseOwner);
            _phase = Phase.Closed;
            _targetNpc = EntityId.None;
            _actor = EntityId.None;
            _targetControlCoreWorkAreaId = string.Empty;
            _targetFactionFlagId = string.Empty;
            _targetEntranceLocationId = string.Empty;
            _targetDestructible = null;
            _worldObjectTarget = default;
            _targetLabel = string.Empty;
            _confirmCallback = null;
            _aggressionAttackerFactionId = string.Empty;
            _aggressionDefenderFactionId = string.Empty;
            _dismantleStatus = string.Empty;
            HostInputGate.BlockWorldInteraction = false;
            if (_holdingDismantlePause && bootstrap?.Session != null)
                bootstrap.Session.ReleaseModalPause(DismantlePauseOwner);
            _holdingDismantlePause = false;
        }

        void OnDisable()
        {
            bootstrap?.Session?.ReleaseModalPause(AggressionPauseOwner);
            if (_holdingDismantlePause && bootstrap?.Session != null)
                bootstrap.Session.ReleaseModalPause(DismantlePauseOwner);
            _holdingDismantlePause = false;
        }

        void TryReleaseInteractionNpc()
        {
            if (_interactionNpc.IsNone || moveController == null || bootstrap?.Session == null)
                return;
            if (moveController.IsApproachingNpc(_interactionNpc))
                return;
            if (dialoguePresenter != null && dialoguePresenter.IsActive)
                return;
            if (bootstrap.Session.World.ContentEvents.HasActive)
                return;
            ReleaseInteractionNpcNow();
        }

        void ReleaseInteractionNpcNow(EntityId npc = default)
        {
            var id = npc.IsNone ? _interactionNpc : npc;
            if (id.IsNone || moveController == null)
                return;
            moveController.ReleaseNpcForInteraction(id);
            if (id == _interactionNpc)
                _interactionNpc = EntityId.None;
        }

        void TryDismissOnOutsideClick(Rect keepOpenRect)
        {
            var ev = Event.current;
            if (ev.type != EventType.MouseDown)
                return;
            if (ev.button != 0 && ev.button != 1)
                return;
            if (keepOpenRect.Contains(ev.mousePosition))
                return;
            CloseAll();
            ev.Use();
        }

        void ResumeTime()
        {
            if (bootstrap?.Session != null &&
                !bootstrap.Session.World.ContentEvents.HasActive &&
                (dialoguePresenter == null || !dialoguePresenter.IsActive))
                bootstrap.Session.IsPaused = false;
        }

        string ResolveDisplayName(EntityId id)
        {
            var session = bootstrap?.Session;
            if (session != null && session.World.Entities.TryGet(id, out var entity) &&
                !string.IsNullOrEmpty(entity.DisplayName))
                return entity.DisplayName;
            return id.IsNone ? "?" : id.ToString();
        }

        static Rect ModalBox(float w, float h) =>
            new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        void DrawDim()
        {
            EnsureStyles();
            var dim = new Rect(0f, 0f, Screen.width, Screen.height);
            HostUiHitTest.Block(dim);
            Fill(dim, new Color(0f, 0f, 0f, 0.45f));
        }

        void EnsureStyles()
        {
            if (_stylesReady)
                return;
            _px = Texture2D.whiteTexture;
            _label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = Ink }
            };
            _button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                normal = { textColor = Ink }
            };
            _stylesReady = true;
        }

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _px != null ? _px : Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void DrawFrame(Rect r, Color c)
        {
            const float t = 1f;
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }
    }
}
