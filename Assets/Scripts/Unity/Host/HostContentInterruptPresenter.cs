using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Input;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Formal content interrupt：ContentEvent 选项弹层＋Quest 接取／完成提醒。
    /// 规则真源仍在 Core ContentEvent／Quest；本组件只做 Host 呈现与暂停策略。
    /// </summary>
    public sealed class HostContentInterruptPresenter : MonoBehaviour
    {
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
        [SerializeField] bool holdPause = true;

        readonly Queue<QuestNotify> _questQueue = new Queue<QuestNotify>();
        readonly HashSet<string> _seenQuestStarted = new HashSet<string>();
        readonly HashSet<string> _seenQuestCompleted = new HashSet<string>();
        readonly HashSet<string> _seenQuestFailed = new HashSet<string>();

        QuestNotify? _activeQuestNotify;
        bool _holdingPause;
        Texture2D _px;
        GUIStyle _title;
        GUIStyle _body;
        bool _stylesReady;

        static readonly Color Parchment = new Color(0.90f, 0.84f, 0.72f, 0.96f);
        static readonly Color ParchmentDark = new Color(0.72f, 0.62f, 0.48f, 1f);
        static readonly Color Ink = new Color(0.18f, 0.14f, 0.10f, 1f);

        /// <summary>事件选项或任务提醒占用中（应暂停并阻断 RTS）。</summary>
        public bool HasBlockingInterrupt
        {
            get
            {
                var session = bootstrap != null ? bootstrap.Session : null;
                if (session == null || !session.IsInitialized)
                    return false;
                return session.World.ContentEvents.HasActive || _activeQuestNotify.HasValue;
            }
        }

        public void Bind(
            PlayableHostBootstrap host,
            HostCommandBridge bridge,
            HostSelectionController selection)
        {
            bootstrap = host;
            commandBridge = bridge;
            selectionController = selection;
        }

        public void ClearSessionState()
        {
            _questQueue.Clear();
            _seenQuestStarted.Clear();
            _seenQuestCompleted.Clear();
            _seenQuestFailed.Clear();
            _activeQuestNotify = null;
            _holdingPause = false;
        }

        /// <summary>在 DomainEvent Drain 之后调用，吞掉任务类事件做提醒队列。</summary>
        public void Ingest(IReadOnlyList<DomainEvent> drained)
        {
            if (drained == null || drained.Count == 0)
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

        /// <summary>EditMode／测试可直接推进暂停与任务提醒队列。</summary>
        public void TickInterruptState()
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized)
            {
                ClearSessionState();
                return;
            }

            if (!session.World.ContentEvents.HasActive &&
                !_activeQuestNotify.HasValue &&
                _questQueue.Count > 0)
            {
                _activeQuestNotify = _questQueue.Dequeue();
            }

            SyncPause(session);
        }

        void SyncPause(PlayableHostSession session)
        {
            if (!holdPause)
                return;

            if (HasBlockingInterrupt)
            {
                session.IsPaused = true;
                _holdingPause = true;
            }
            else if (_holdingPause)
            {
                session.IsPaused = false;
                _holdingPause = false;
            }
        }

        void OnGUI()
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized)
                return;

            EnsureStyles();
            if (session.World.ContentEvents.HasActive)
            {
                DrawEventModal(session);
                return;
            }

            if (_activeQuestNotify.HasValue)
                DrawQuestModal(session, _activeQuestNotify.Value);
        }

        void DrawEventModal(PlayableHostSession session)
        {
            if (!session.World.ContentEvents.TryGet(session.World.ContentEvents.ActiveEventId, out var spec))
                return;

            DrawDim();
            var box = ModalBox();
            Fill(box, Parchment);
            DrawFrame(box, ParchmentDark);

            var title = string.IsNullOrEmpty(spec.Name) ? ShortId(spec.Id) : spec.Name;
            GUI.Label(new Rect(box.x + 16f, box.y + 12f, box.width - 32f, 26f), "事件 · " + title, _title);
            GUI.Label(
                new Rect(box.x + 16f, box.y + 42f, box.width - 32f, 24f),
                "已暂停 — 请选择后继续",
                _body);
            var body = string.IsNullOrEmpty(spec.Body) ? "（无正文）" : spec.Body;
            GUI.Label(new Rect(box.x + 16f, box.y + 72f, box.width - 32f, 110f), body, _body);

            var subject = ResolveSubject(session);
            var by = box.y + 190f;
            var bw = box.width - 32f;
            for (var i = 0; i < spec.Choices.Count; i++)
            {
                var choice = spec.Choices[i];
                var ok = ContentConditionEvaluator.AllPass(session.World, subject, choice.Conditions);
                var label = string.IsNullOrEmpty(choice.Text) ? choice.Id : choice.Text;
                if (!ok)
                    label += "（条件未满足）";
                GUI.enabled = ok && commandBridge != null;
                if (GUI.Button(new Rect(box.x + 16f, by, bw, 32f), label))
                    commandBridge.ResolveContentChoice(choice.Id);
                GUI.enabled = true;
                by += 40f;
                if (by + 32f > box.yMax - 12f)
                    break;
            }
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
                    ? "任务完成"
                    : "任务失败";
            var name = notify.QuestId;
            var desc = "";
            if (session.World.Quests.TryGetSpec(notify.QuestId, out var spec))
            {
                if (!string.IsNullOrEmpty(spec.Name))
                    name = spec.Name;
                desc = spec.Description ?? "";
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
