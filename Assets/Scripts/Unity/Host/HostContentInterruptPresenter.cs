using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Input;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// ContentEvent 非 onTalk 弹层 +（可选）任务提醒弹窗。
    /// onTalk 由 <see cref="HostDialoguePresenter"/> 底栏呈现。
    /// </summary>
    public sealed class HostContentInterruptPresenter : MonoBehaviour
    {
        const string PauseOwner = "ContentInterrupt";
        enum QuestNotifyKind
        {
            Started,
            Completed,
            Failed
        }

        struct QuestNotify
        {
            public QuestNotifyKind Kind;
            public string QuestId;
        }

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostCommandBridge commandBridge;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostDialoguePresenter dialoguePresenter;
        [SerializeField] bool holdPause = true;
        [Tooltip("NPC／内容事件选项弹层。默认关：自动选第一条可用选项。")]
        [SerializeField] bool enableContentEventPopups = true;
        [Tooltip("任务接取／完成／失败的「知道了」弹窗；默认关。")]
        [SerializeField] bool enableQuestNotifyPopups;

        readonly Queue<QuestNotify> _questQueue = new Queue<QuestNotify>();
        readonly HashSet<string> _seenQuestStarted = new HashSet<string>();
        readonly HashSet<string> _seenQuestCompleted = new HashSet<string>();
        readonly HashSet<string> _seenQuestFailed = new HashSet<string>();

        readonly HostDialogueController _eventController = new HostDialogueController();
        Vector2 _eventScroll;
        QuestNotify? _activeQuestNotify;
        bool _holdingPause;
        Texture2D _px;
        GUIStyle _title;
        GUIStyle _body;
        bool _stylesReady;

        static readonly Color Parchment = new Color(0.90f, 0.84f, 0.72f, 0.96f);
        static readonly Color ParchmentDark = new Color(0.72f, 0.62f, 0.48f, 1f);
        static readonly Color Ink = new Color(0.18f, 0.14f, 0.10f, 1f);

        public bool HasBlockingInterrupt
        {
            get
            {
                var session = bootstrap != null ? bootstrap.Session : null;
                if (session == null || !session.IsInitialized)
                    return false;
                if (dialoguePresenter != null && dialoguePresenter.IsActive)
                    return true;
                if (session.World.ContentEvents.HasActive &&
                    ShouldDelegateOnTalkToDialogue(session) &&
                    dialoguePresenter != null)
                    return true;
                if (enableContentEventPopups && session.World.ContentEvents.HasActive &&
                    !ShouldDelegateOnTalkToDialogue(session))
                    return true;
                if (enableQuestNotifyPopups && _activeQuestNotify.HasValue)
                    return true;
                return false;
            }
        }

        public void Bind(
            PlayableHostBootstrap host,
            HostCommandBridge bridge,
            HostSelectionController selection,
            HostDialoguePresenter dialogue = null)
        {
            bootstrap = host;
            commandBridge = bridge;
            selectionController = selection;
            dialoguePresenter = dialogue;
        }

        public void ClearSessionState()
        {
            if (_holdingPause && bootstrap?.Session != null)
                bootstrap.Session.ReleaseModalPause(PauseOwner);
            _questQueue.Clear();
            _seenQuestStarted.Clear();
            _seenQuestCompleted.Clear();
            _seenQuestFailed.Clear();
            _activeQuestNotify = null;
            _holdingPause = false;
        }

        public void Ingest(IReadOnlyList<DomainEvent> drained)
        {
            if (!enableQuestNotifyPopups || drained == null || drained.Count == 0)
                return;
            for (var i = 0; i < drained.Count; i++)
            {
                var evt = drained[i];
                if (evt == null || string.IsNullOrEmpty(evt.Payload))
                    continue;
                switch (evt.Type)
                {
                    case XianXia.Core.Events.EventType.QuestStarted:
                        EnqueueQuest(QuestNotifyKind.Started, evt.Payload, _seenQuestStarted);
                        break;
                    case XianXia.Core.Events.EventType.QuestCompleted:
                        EnqueueQuest(QuestNotifyKind.Completed, evt.Payload, _seenQuestCompleted);
                        break;
                    case XianXia.Core.Events.EventType.QuestFailed:
                        EnqueueQuest(QuestNotifyKind.Failed, evt.Payload, _seenQuestFailed);
                        break;
                }
            }
        }

        void EnqueueQuest(QuestNotifyKind kind, string questId, HashSet<string> seen)
        {
            var key = kind + ":" + questId;
            if (!seen.Add(key))
                return;
            _questQueue.Enqueue(new QuestNotify { Kind = kind, QuestId = questId });
        }

        void Update() => TickInterruptState();

        public void TickInterruptState()
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized)
            {
                ClearSessionState();
                return;
            }

            if (!enableQuestNotifyPopups)
            {
                _questQueue.Clear();
                _activeQuestNotify = null;
            }

            if (!enableContentEventPopups && session.World.ContentEvents.HasActive)
                TryAutoResolveActiveEvent(session);
            else if (enableContentEventPopups &&
                     session.World.ContentEvents.HasActive &&
                     ShouldDelegateOnTalkToDialogue(session) &&
                     dialoguePresenter != null &&
                     !dialoguePresenter.IsActive)
            {
                // onTalk is shown by HostDialoguePresenter after NPC arrive; keep pause via SyncPause.
            }

            if (enableQuestNotifyPopups &&
                !session.World.ContentEvents.HasActive &&
                !(dialoguePresenter != null && dialoguePresenter.IsActive) &&
                !_activeQuestNotify.HasValue &&
                _questQueue.Count > 0)
            {
                _activeQuestNotify = _questQueue.Dequeue();
            }

            SyncPause(session);
            ScheduledContentEventDispatcher.Dispatch(session.World, CanPresentScheduledEvent(session));
            SyncPause(session);
        }

        // One Host policy, shared by every scheduled presentation attempt. Manual pause is allowed.
        bool CanPresentScheduledEvent(PlayableHostSession session)
        {
            if (!session.IsInitialized || session.InitialBootstrapPending || session.PendingRestoredStrategicSnapshot != null ||
                session.World.ContentEvents.HasActive || session.ModalHardPaused || HasBlockingInterrupt ||
                HostInputGate.BlockWorldInteraction || (dialoguePresenter != null && dialoguePresenter.IsActive)) return false;
            if (XianXia.Core.World.Strategic.CharacterEncounterService.BlocksOrdinaryContinuousSurface(session.World) ||
                XianXia.Core.World.Strategic.StrategicClockFreezeService.IsWorldTickFrozen(session.World)) return false;
            if (session.World.Strategic?.ContinuousManualCombat?.IsActive == true) return false;
            var melee = bootstrap.GetComponent<HostNpcMeleeAssault>();
            if (melee != null && melee.IsFighting) return false;
            var surface = bootstrap.ContinuousOutdoorSurfaceRuntime;
            if (surface != null && surface.IsTransitioning) return false;
            return true;
        }

        void TryAutoResolveActiveEvent(PlayableHostSession session)
        {
            if (ShouldDelegateOnTalkToDialogue(session)) return;
            if (!_eventController.TryBuildFromActiveEvent(session)) return;
            for (var i = 0; i < _eventController.Model.Choices.Count; i++)
                if (_eventController.Model.Choices[i].Enabled)
                {
                    _eventController.TrySelectChoice(i, session, commandBridge, bootstrap);
                    return;
                }
        }

        void OnDisable()
        {
            if (_holdingPause && bootstrap?.Session != null)
                bootstrap.Session.ReleaseModalPause(PauseOwner);
            _holdingPause = false;
        }

        void SyncPause(PlayableHostSession session)
        {
            if (!holdPause)
            {
                if (_holdingPause)
                {
                    session.ReleaseModalPause(PauseOwner);
                    _holdingPause = false;
                }
                return;
            }

            if (HasBlockingInterrupt)
            {
                session.AcquireModalPause(PauseOwner);
                _holdingPause = true;
            }
            else if (_holdingPause)
            {
                session.ReleaseModalPause(PauseOwner);
                _holdingPause = false;
            }
        }

        static bool ShouldDelegateOnTalkToDialogue(PlayableHostSession session) =>
            HostDialogueController.IsActiveOnTalk(session.World);

        void OnGUI()
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized)
                return;

            EnsureStyles();
            if (dialoguePresenter != null && dialoguePresenter.IsActive)
                return;

            if (enableContentEventPopups &&
                session.World.ContentEvents.HasActive &&
                !ShouldDelegateOnTalkToDialogue(session))
            {
                DrawEventModal(session);
                return;
            }

            if (enableQuestNotifyPopups && _activeQuestNotify.HasValue)
                DrawQuestModal(session, _activeQuestNotify.Value);
        }

        void DrawEventModal(PlayableHostSession session)
        {
            if (!_eventController.TryBuildFromActiveEvent(session)) return;
            var model = _eventController.Model;
            DrawDim();
            var box = ModalBox();
            Fill(box, Parchment);
            DrawFrame(box, ParchmentDark);
            GUI.Label(new Rect(box.x + 16f, box.y + 12f, box.width - 32f, 26f), model.SpeakerName, _title);
            GUILayout.BeginArea(new Rect(box.x + 16f, box.y + 44f, box.width - 32f, box.height - 56f));
            _eventScroll = GUILayout.BeginScrollView(_eventScroll);
            GUILayout.Label(model.Body, _body);
            for (var i = 0; i < model.Choices.Count; i++)
            {
                var line = model.Choices[i];
                GUI.enabled = line.Enabled;
                if (GUILayout.Button(line.Label, GUILayout.MinHeight(32f)))
                    _eventController.TrySelectChoice(i, session, commandBridge, bootstrap);
                GUI.enabled = true;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawQuestModal(PlayableHostSession session, QuestNotify notify)
        {
            DrawDim();
            var box = ModalBox();
            Fill(box, Parchment);
            DrawFrame(box, ParchmentDark);

            var kindLabel = notify.Kind == QuestNotifyKind.Started
                ? "任务接取"
                : notify.Kind == QuestNotifyKind.Completed
                    ? "任务可领奖"
                    : "任务失败";
            var name = notify.QuestId;
            var desc = "";
            if (session.World.Quests.TryGet(notify.QuestId, out var runtime) &&
                session.World.Quests.TryGetSpec(runtime.QuestId, out var spec))
            {
                if (!string.IsNullOrEmpty(spec.Name))
                    name = spec.Name;
                desc = spec.Description ?? "";
                if (!string.IsNullOrEmpty(runtime.IssuerDisplayName))
                    desc = "发布者：" + runtime.IssuerDisplayName + "\n" + desc;
            }

            GUI.Label(new Rect(box.x + 16f, box.y + 12f, box.width - 32f, 26f), kindLabel + " · " + name, _title);
            GUI.Label(
                new Rect(box.x + 16f, box.y + 48f, box.width - 32f, 140f),
                string.IsNullOrEmpty(desc) ? "（无任务说明）" : desc,
                _body);
            if (GUI.Button(new Rect(box.x + 16f, box.yMax - 48f, box.width - 32f, 36f), "知道了"))
                _activeQuestNotify = null;
        }

        EntityId ResolveSubject(PlayableHostSession session)
        {
            if (selectionController != null && selectionController.State.Count > 0)
            {
                var id = selectionController.State.SelectedIds[0];
                if (!id.IsNone)
                    return id;
            }

            return session.CharacterIds.Count > 0 ? session.CharacterIds[0] : EntityId.None;
        }

        void DrawDim()
        {
            EnsurePx();
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _px);
            GUI.color = prev;
        }

        static Rect ModalBox()
        {
            var w = Mathf.Min(520f, Screen.width - 40f);
            var h = Mathf.Min(360f, Screen.height - 80f);
            return new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        }

        void EnsureStyles()
        {
            if (_stylesReady)
                return;
            EnsurePx();
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Ink }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = Ink }
            };
            _stylesReady = true;
        }

        void EnsurePx()
        {
            if (_px != null)
                return;
            _px = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _px.SetPixel(0, 0, Color.white);
            _px.Apply();
        }

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _px);
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

        static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "-";
            var i = id.LastIndexOf('_');
            if (i >= 0 && i + 1 < id.Length)
                return id.Substring(i + 1);
            i = id.IndexOf(':');
            return i >= 0 && i + 1 < id.Length ? id.Substring(i + 1) : id;
        }
    }
}
