using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 右键情境菜单：NPC＝对话／攻击；洞府＝进入；主管府／树／墙＝攻击。
    /// </summary>
    public sealed class HostNpcContextMenu : MonoBehaviour
    {
        enum Phase
        {
            Closed = 0,
            Menu = 1,
            LocalAttackConfirm = 2,
            RebellionConfirm = 3
        }

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostMoveController moveController;
        [SerializeField] HostDialoguePresenter dialoguePresenter;
        [SerializeField] HostLocalMapEnterPrompt localMapEnterPrompt;
        [SerializeField] Camera worldCamera;

        Phase _phase = Phase.Closed;
        EntityId _targetNpc = EntityId.None;
        EntityId _actor = EntityId.None;
        EntityId _interactionNpc = EntityId.None;
        string _targetControlCoreWorkAreaId = string.Empty;
        string _targetEntranceLocationId = string.Empty;
        HostMapDestructible _targetDestructible;
        bool _leaveInteriorTarget;
        string _targetLabel = string.Empty;
        EntityId _confirmTarget = EntityId.None;
        System.Action _confirmCallback;
        /// <summary>一次 LocalCharacter 攻击确认的 one-shot token：approach 后重新 classify 时不再二次确认。</summary>
        EntityId _confirmedLocalAttackTargetId = EntityId.None;
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
        bool IsCaveEntranceTarget => !string.IsNullOrEmpty(_targetEntranceLocationId);
        bool IsLeaveInteriorTarget => _leaveInteriorTarget;
        bool IsDestructibleTarget =>
            _targetDestructible != null && !_targetDestructible.IsDestroyed;

        public void Bind(
            PlayableHostBootstrap host,
            HostSelectionController selection,
            HostMoveController move,
            HostDialoguePresenter dialogue = null,
            HostLocalMapEnterPrompt enterPrompt = null)
        {
            bootstrap = host;
            selectionController = selection;
            moveController = move;
            dialoguePresenter = dialogue;
            localMapEnterPrompt = enterPrompt;
            if (worldCamera == null)
                worldCamera = Camera.main;
        }

        public void ClearSessionState()
        {
            _confirmedLocalAttackTargetId = EntityId.None;
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
                _targetEntranceLocationId = string.Empty;
                _targetDestructible = null;
                _leaveInteriorTarget = false;
                _targetLabel = ResolveDisplayName(npc);
                _menuScreen = Input.mousePosition;
                _phase = Phase.Menu;
                HostInputGate.BlockWorldInteraction = true;
                return true;
            }

            MapLayoutPick.TryGet(bootstrap.Session, out var layout);

            // 洞内出口 → 离开
            if (bootstrap.Session.World.LocalMap.IsInInterior &&
                HostCaveEntranceQuery.TryPickInteriorExitAtMouse(worldCamera, layout, out var exitLabel))
            {
                _actor = actor;
                _targetNpc = EntityId.None;
                _targetControlCoreWorkAreaId = string.Empty;
                _targetEntranceLocationId = string.Empty;
                _targetDestructible = null;
                _leaveInteriorTarget = true;
                _targetLabel = string.IsNullOrEmpty(exitLabel) ? "洞口" : exitLabel;
                _menuScreen = Input.mousePosition;
                _phase = Phase.Menu;
                HostInputGate.BlockWorldInteraction = true;
                return true;
            }

            // 地表已显形洞府 → 进入
            if (!bootstrap.Session.World.LocalMap.IsInInterior &&
                HostCaveEntranceQuery.TryPickAtMouse(
                    worldCamera, bootstrap.Session.World, layout, out var entranceId) &&
                bootstrap.Session.World.WorldRegion.TryGet(entranceId, out var entrance))
            {
                _actor = actor;
                _targetNpc = EntityId.None;
                _targetControlCoreWorkAreaId = string.Empty;
                _targetEntranceLocationId = entranceId;
                _targetDestructible = null;
                _leaveInteriorTarget = false;
                _targetLabel = string.IsNullOrEmpty(entrance.Name) ? "洞府入口" : entrance.Name;
                _menuScreen = Input.mousePosition;
                _phase = Phase.Menu;
                HostInputGate.BlockWorldInteraction = true;
                return true;
            }

            if (HostControlCoreQuery.TryPickAtMouse(
                    worldCamera, bootstrap.Session.World, layout, out var coreId) &&
                bootstrap.Session.World.ControlCores.TryGet(coreId, out var core) &&
                !core.PlayerControlled)
            {
                _actor = actor;
                _targetNpc = EntityId.None;
                _targetEntranceLocationId = string.Empty;
                _leaveInteriorTarget = false;
                _targetDestructible = null;
                _targetControlCoreWorkAreaId = coreId;
                _targetLabel = string.IsNullOrEmpty(core.Name) ? "主管府" : core.Name;
                _menuScreen = Input.mousePosition;
                _phase = Phase.Menu;
                HostInputGate.BlockWorldInteraction = true;
                return true;
            }

            if (HostPresentationSpace.TryRaycastPlane(worldCamera, Input.mousePosition, out var worldPoint) &&
                HostMapObjectRegistry.TryPickDestructible(worldPoint, 2.2f, out var destructible))
            {
                _actor = actor;
                _targetNpc = EntityId.None;
                _targetControlCoreWorkAreaId = string.Empty;
                _targetEntranceLocationId = string.Empty;
                _leaveInteriorTarget = false;
                _targetDestructible = destructible;
                _targetLabel = destructible.DisplayName;
                _menuScreen = Input.mousePosition;
                _phase = Phase.Menu;
                HostInputGate.BlockWorldInteraction = true;
                return true;
            }

            return false;
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
                    if (IsLeaveInteriorTarget)
                        DrawLeaveMenu();
                    else if (IsCaveEntranceTarget)
                        DrawCaveMenu();
                    else if (IsControlCoreTarget)
                        DrawControlCoreMenu();
                    else if (IsDestructibleTarget)
                        DrawDestructibleMenu();
                    else
                        DrawContextMenu();
                    break;
                case Phase.LocalAttackConfirm:
                    DrawAttackConfirm(
                        "攻击确认",
                        "是否攻击「" + ResolveDisplayName(_confirmTarget) + "」？",
                        "攻击",
                        ConfirmLocalAttack,
                        CloseAll);
                    break;
                case Phase.RebellionConfirm:
                    DrawAttackConfirm(
                        "起事／反抗宗门",
                        "起事后将脱离压迫宗门附庸，并立即与其进入战争。是否继续？",
                        "确认起事",
                        ConfirmRebellion,
                        CloseAll);
                    break;
            }
        }

        void DrawLeaveMenu()
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
            if (GUI.Button(new Rect(guiX + 8f, y, w - 16f, itemH - 4f), "离开", _button))
                BeginLeaveInterior();
            TryDismissOnOutsideClick(_menuGuiRect);
        }

        void BeginLeaveInterior()
        {
            var actor = _actor;
            CloseAll();
            var bridge = bootstrap != null ? bootstrap.CommandBridge : null;
            if (bridge == null)
                return;
            if (!actor.IsNone && selectionController != null &&
                !selectionController.State.Contains(actor))
                selectionController.SelectEntity(actor, false);
            if (bridge.IssueLeaveLocalMap() <= 0)
                Debug.LogWarning("[Host] LeaveLocalMap failed: " + bridge.LastStatus);
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
            if (actor.IsNone || string.IsNullOrEmpty(entranceId))
                return;

            if (localMapEnterPrompt == null && bootstrap != null)
                localMapEnterPrompt = bootstrap.GetComponent<HostLocalMapEnterPrompt>() ??
                                     bootstrap.gameObject.AddComponent<HostLocalMapEnterPrompt>();
            // 确保弹窗已 Bind（运行时 AddComponent 时）
            if (localMapEnterPrompt != null && bootstrap != null)
            {
                localMapEnterPrompt.Bind(
                    bootstrap,
                    selectionController,
                    bootstrap.CommandBridge,
                    moveController);
                localMapEnterPrompt.Open(actor, entranceId);
            }
        }

        void DrawControlCoreMenu()
        {
            const float w = 168f;
            const float itemH = 30f;
            var session = bootstrap?.Session;
            var world = session?.World;
            var canShowRebellion = world?.Strategic?.Ch01FormationScenarioCompat == true &&
                                  string.Equals(
                                      world.PartyWorld?.SiteId,
                                      Ch01ScenarioProgressionHooks.HuangcunSiteId,
                                      System.StringComparison.Ordinal);
            var rebellion = Ch01RebellionService.CanBegin(world, session?.PlayerParty);
            var canAssault = world != null &&
                             CaptureObjectiveService.TryBeginMilitaryAssault(
                                 world,
                                 world.Strategic?.PlayerFactionId ?? string.Empty,
                                 _targetControlCoreWorkAreaId).IsSuccess;
            var h = itemH * (canShowRebellion ? 2 : 1) + 34f;
            var guiX = Mathf.Clamp(_menuScreen.x, 4f, Screen.width - w - 4f);
            var guiY = Mathf.Clamp(Screen.height - _menuScreen.y, 4f, Screen.height - h - 4f);
            _menuGuiRect = new Rect(guiX, guiY, w, h);
            HostUiHitTest.Block(_menuGuiRect);

            Fill(_menuGuiRect, Panel);
            DrawFrame(_menuGuiRect, Border);

            GUI.Label(new Rect(guiX + 10f, guiY + 6f, w - 20f, 22f), _targetLabel, _label);
            var y = guiY + 30f;
            GUI.enabled = canAssault;
            if (GUI.Button(new Rect(guiX + 8f, y, w - 16f, itemH - 4f),
                    canAssault ? "攻击" : "攻击（需要先进入战争）", _button))
                BeginControlCoreAttack();
            GUI.enabled = true;
            y += itemH;

            if (canShowRebellion)
            {
                GUI.enabled = rebellion.IsSuccess;
                if (GUI.Button(new Rect(guiX + 8f, y, w - 16f, itemH - 4f),
                        rebellion.IsSuccess ? "起事／反抗宗门" : "起事条件未满足", _button))
                    BeginRebellionConfirm();
                GUI.enabled = true;
            }
            TryDismissOnOutsideClick(_menuGuiRect);
        }

        void DrawDestructibleMenu()
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
            var verb = _targetDestructible != null && _targetDestructible.IsTree ? "砍伐" : "拆毁";
            if (GUI.Button(new Rect(guiX + 8f, y, w - 16f, itemH - 4f), verb, _button))
                BeginDestructibleAttack();
            TryDismissOnOutsideClick(_menuGuiRect);
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
                // CORRECTION V1: 点击 Attack 立即 route（不等走到面前才发现是 Army）。
                var consumed = TryHandlePlayerHostileAction(_actor, _targetNpc, BeginAttack);
                if (!consumed)
                    BeginAttack();
            }

            TryDismissOnOutsideClick(_menuGuiRect);
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

            var assaultPreflight = CaptureObjectiveService.TryBeginMilitaryAssault(
                world, world.Strategic?.PlayerFactionId ?? string.Empty, coreId);
            if (assaultPreflight.IsFailure)
            {
                Debug.LogWarning("[Host] 主管府突击被战争门槛拒绝：" + assaultPreflight.Error.Message);
                CloseAll();
                return;
            }

            MapLayoutPick.TryGet(session, out var layout);
            if (HostControlCoreQuery.TryGetApproachPoint(world, layout, core, out var approach) &&
                moveController != null)
                moveController.OrderPartyToPointPublic(approach);

            var housing = bootstrap.GetComponent<HostHousingAreaSelection>();
            housing?.SelectControlCore(coreId);

            var assault = bootstrap.GetComponent<HostControlCoreAssault>();
            if (assault != null)
                assault.Begin(coreId);
            else
                Debug.LogWarning("[Host] HostControlCoreAssault 未挂载。");

            var overlay = bootstrap.GetComponent<HostFeedbackOverlay>();
            if (overlay != null && !_actor.IsNone)
            {
                overlay.SpawnAtEntity(
                    bootstrap.ViewSpawner,
                    _actor,
                    "突击 " + _targetLabel,
                    new Color(1f, 0.45f, 0.35f, 1f));
            }

            Debug.Log(
                "[Host] 开始突击主管府：靠近后按近战节奏／攻击力拆耐久；破门后站满 " +
                core.OccupyHoldSeconds + " 秒占领。");

            ResumeTime();
            CloseAll();
        }

        void BeginRebellionConfirm()
        {
            _phase = Phase.RebellionConfirm;
        }

        void ConfirmRebellion()
        {
            var session = bootstrap?.Session;
            var result = Ch01RebellionService.TryBegin(session?.World, session?.PlayerParty);
            var text = result.IsSuccess
                ? "起事已成：脱离附庸，已与压迫宗门开战"
                : "起事失败：" + result.Error.Message;
            var color = result.IsSuccess
                ? new Color(0.95f, 0.55f, 0.28f)
                : new Color(1f, 0.45f, 0.35f);
            var overlay = bootstrap != null ? bootstrap.GetComponent<HostFeedbackOverlay>() : null;
            if (overlay != null && bootstrap.ViewSpawner != null && !_actor.IsNone)
                overlay.SpawnAtEntity(bootstrap.ViewSpawner, _actor, text, color);
            if (result.IsFailure)
                Debug.LogWarning("[Host] 第一章起事失败：" + result.Error.Message);

            ResumeTime();
            CloseAll();
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
            if (_scratchAttackers.Count == 0 && !_actor.IsNone)
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
            {
                session.IsPaused = true;
                return;
            }

            ShowFallbackTalk("（" + _targetLabel + " 暂无对话内容）");
        }

        /// <summary>
        /// Host pre-damage coordinator（右键攻击 / 主动技能共用，单一路由，禁止复制两套判断）。
        /// 返回 true = 本次输入已被消费（确认窗 / BattleOffer / reject）；
        /// 返回 false = caller 应直接执行本地伤害动作（仅 active WORLD_COMBAT participant 直接攻击路径）。
        /// </summary>
        public bool TryHandlePlayerHostileAction(
            EntityId actor,
            EntityId target,
            System.Action onConfirmedLocalAction)
        {
            var session = bootstrap?.Session;
            if (session == null || !session.IsInitialized || actor.IsNone || target.IsNone)
                return true;

            var route = LocalHostileActionRoutingService.Route(
                session.World, session.PlayerParty, actor, target);
            switch (route.Route)
            {
                case HostileActionRoute.LocalCombat:
                    // 已处于 active WORLD_COMBAT 的 hostile participant → 直接 tactical combat。
                    if (IsActiveStrategicCombatTarget(session.World, target))
                        return false;
                    // 普通 Character（无论 faction / hostile tag）→ 一次确认。
                    BeginLocalAttackConfirm(actor, target, onConfirmedLocalAction);
                    return true;

                case HostileActionRoute.Reject:
                    Debug.LogWarning("[Host] Hostile action rejected: " + route.FailureReason);
                    ReleaseInteractionNpcNow(target);
                    CloseAll();
                    return true;

                case HostileActionRoute.StrategicMilitaryEscalation:
                default:
                    PrepareLocalMilitaryOffer(actor, target, route);
                    return true;
            }
        }

        void BeginLocalAttackConfirm(EntityId actor, EntityId target, System.Action onConfirmed)
        {
            _confirmTarget = target;
            _confirmCallback = onConfirmed;
            _phase = Phase.LocalAttackConfirm;
            HostInputGate.BlockWorldInteraction = true;
        }

        void ConfirmLocalAttack()
        {
            if (!_confirmTarget.IsNone)
                _confirmedLocalAttackTargetId = _confirmTarget;
            var cb = _confirmCallback;
            _confirmCallback = null;
            cb?.Invoke();
            CloseAll();
        }

        static bool IsActiveStrategicCombatTarget(SimulationWorld world, EntityId targetId)
        {
            if (world == null || targetId.IsNone ||
                !world.Entities.TryGet(targetId, out var entity) || entity == null)
                return false;
            return StrategicEncounterHostilityService.IsHostileStrategicNpc(world, entity);
        }

        /// <summary>
        /// LocalMap 军事攻击 → 建立 Local-origin BattleOffer（不 DeclareWar、不直接 Manual）。
        /// BattleOffer presenter 自动接管展示；Diplomacy 保持不变，commit point 在 Manual 确认。
        /// </summary>
        void PrepareLocalMilitaryOffer(EntityId actor, EntityId target, HostileActionRouteResult route)
        {
            var session = bootstrap?.Session;
            if (session == null)
            {
                CloseAll();
                return;
            }

            var result = PlayerPartyStrategicCombatCommandService
                .TryPrepareLocalPlayerPartyMilitaryAttackOffer(
                    session.World, session.PlayerParty, route.TargetFormalArmyId);
            if (result.IsFailure)
            {
                Debug.LogWarning(
                    "[Host] Local military offer preparation failed: " + result.Error.Message);
                CloseAll();
                return;
            }

            ReleaseInteractionNpcNow(route.TargetEntityId);
            CloseAll();
        }

        public void OnNpcArriveAttack(EntityId actor, EntityId npc)
        {
            var session = bootstrap?.Session;
            if (session == null || !session.IsInitialized || actor.IsNone)
                return;

            // race-condition safety：approach 期间目标可能加入 FormalArmy → 重新 classify。
            var route = LocalHostileActionRoutingService.Route(
                session.World, session.PlayerParty, actor, npc);
            if (route.Route == HostileActionRoute.StrategicMilitaryEscalation)
            {
                // consume local approach：不造成第一刀 damage，改建 BattleOffer。
                PrepareLocalMilitaryOffer(actor, npc, route);
                return;
            }
            if (route.Route == HostileActionRoute.Reject)
            {
                Debug.LogWarning(
                    "[Host] Hostile action rejected on arrival: " + route.FailureReason);
                ReleaseInteractionNpcNow(npc);
                return;
            }

            // LocalCombat：已确认过（右键确认流）或 active combat participant → 直接近战，不再问第二次。
            if (_confirmedLocalAttackTargetId == npc || IsActiveStrategicCombatTarget(session.World, npc))
            {
                _confirmedLocalAttackTargetId = EntityId.None;
                BeginMelee(actor, npc);
                return;
            }

            // 未确认的直接攻击命令（如纯移动指令）→ 到达时弹一次确认。
            BeginLocalAttackConfirm(actor, npc, () => BeginMelee(actor, npc));
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
            _phase = Phase.Closed;
            _targetNpc = EntityId.None;
            _actor = EntityId.None;
            _targetControlCoreWorkAreaId = string.Empty;
            _targetEntranceLocationId = string.Empty;
            _targetDestructible = null;
            _leaveInteriorTarget = false;
            _targetLabel = string.Empty;
            _confirmTarget = EntityId.None;
            _confirmCallback = null;
            HostInputGate.BlockWorldInteraction = false;
            if (bootstrap?.Session != null &&
                !bootstrap.Session.World.ContentEvents.HasActive &&
                (dialoguePresenter == null || !dialoguePresenter.IsActive) &&
                (localMapEnterPrompt == null || !localMapEnterPrompt.IsOpen))
                bootstrap.Session.IsPaused = false;
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
                (dialoguePresenter == null || !dialoguePresenter.IsActive) &&
                (localMapEnterPrompt == null || !localMapEnterPrompt.IsOpen))
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
