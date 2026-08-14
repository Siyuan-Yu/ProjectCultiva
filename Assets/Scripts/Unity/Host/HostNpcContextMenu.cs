using UnityEngine;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 右键 NPC 情境菜单：对话 / 攻击（非敌对二次确认）。
    /// 对话抵达后触发 contentEvent（onTalk）；攻击暂为占位。
    /// </summary>
    public sealed class HostNpcContextMenu : MonoBehaviour
    {
        enum Phase
        {
            Closed = 0,
            Menu = 1,
            AttackConfirm1 = 2,
            AttackConfirm2 = 3
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
        string _targetLabel = string.Empty;
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

            var actor = HostNpcInteraction.ResolvePartyActor(selectionController);
            if (actor.IsNone)
                return false;

            if (worldCamera == null)
                worldCamera = Camera.main;
            var spawner = bootstrap.ViewSpawner;
            if (!HostNpcPicker.TryPickAtMouse(worldCamera, spawner, out var npc, out _) ||
                selectionController.IsPartyUnit(npc))
                return false;

            _actor = actor;
            _targetNpc = npc;
            _targetLabel = ResolveDisplayName(npc);
            _menuScreen = Input.mousePosition;
            _phase = Phase.Menu;
            HostInputGate.BlockWorldInteraction = true;
            return true;
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
                    DrawContextMenu();
                    break;
                case Phase.AttackConfirm1:
                    DrawAttackConfirm(
                        "当前为非敌对状态",
                        "确定要攻击「" + _targetLabel + "」吗？",
                        "确定",
                        () => _phase = Phase.AttackConfirm2,
                        CloseAll);
                    break;
                case Phase.AttackConfirm2:
                    DrawAttackConfirm(
                        "再次确认",
                        "攻击后将无法撤销，且可能引发严重后果。",
                        "确认攻击",
                        BeginAttack,
                        () => _phase = Phase.AttackConfirm1);
                    break;
            }
        }

        void DrawContextMenu()
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
            if (GUI.Button(new Rect(guiX + 8f, y, w - 16f, itemH - 4f), "对话", _button))
                BeginTalk();
            y += itemH;
            if (GUI.Button(new Rect(guiX + 8f, y, w - 16f, itemH - 4f), "攻击", _button))
                _phase = Phase.AttackConfirm1;
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

        void BeginAttack()
        {
            if (moveController == null || _actor.IsNone || _targetNpc.IsNone)
            {
                CloseAll();
                return;
            }

            if (!moveController.OrderActorToNpc(_actor, _targetNpc, HostNpcArriveAction.Attack))
            {
                CloseAll();
                return;
            }

            _interactionNpc = _targetNpc;
            ResumeTime();
            CloseAll();
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

        public void OnNpcArriveAttack(EntityId actor, EntityId npc)
        {
            var name = ResolveDisplayName(npc);
            Debug.Log("[Host] Attack intent vs " + name + " (combat not implemented).");
            var overlay = bootstrap.GetComponent<HostFeedbackOverlay>();
            if (overlay != null && bootstrap.ViewSpawner != null)
            {
                overlay.SpawnAtEntity(
                    bootstrap.ViewSpawner,
                    actor,
                    "攻击 " + name + "（战斗未实装）",
                    new Color(1f, 0.45f, 0.35f, 1f));
            }

            ReleaseInteractionNpcNow(npc);
            ResumeTime();
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
            _targetLabel = string.Empty;
            HostInputGate.BlockWorldInteraction = false;
            if (bootstrap?.Session != null &&
                !bootstrap.Session.World.ContentEvents.HasActive &&
                (dialoguePresenter == null || !dialoguePresenter.IsActive))
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
