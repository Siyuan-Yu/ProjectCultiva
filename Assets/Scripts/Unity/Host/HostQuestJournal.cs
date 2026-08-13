using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Input;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 任务日志：可接／锁定｜进行中｜已完成；接取／领奖／放弃／追踪。
    /// 打开时暂停世界并阻断相机缩放；右侧 FormalHud「任务」只显示追踪中的任务。
    /// </summary>
    public sealed class HostQuestJournal : MonoBehaviour
    {
        enum Tab
        {
            Offer = 0,
            Active = 1,
            Done = 2
        }

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostCommandBridge commandBridge;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] KeyCode toggleKey = KeyCode.J;
        [SerializeField] bool open;

        readonly List<QuestListEntry> _entries = new List<QuestListEntry>(64);
        Tab _tab = Tab.Active;
        string _selectedId = string.Empty;
        string _trackedQuestId = string.Empty;
        bool _suppressAutoTrack;
        Vector2 _listScroll;
        Vector2 _detailScroll;
        string _status = string.Empty;
        bool _holdingPause;

        Texture2D _px;
        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _small;
        bool _stylesReady;

        static readonly Color Parchment = new Color(0.92f, 0.86f, 0.74f, 0.98f);
        static readonly Color ParchmentDark = new Color(0.70f, 0.58f, 0.42f, 1f);
        static readonly Color Ink = new Color(0.16f, 0.12f, 0.08f, 1f);
        static readonly Color RedDot = new Color(0.92f, 0.22f, 0.18f, 1f);
        static readonly Color Accent = new Color(0.78f, 0.42f, 0.18f, 1f);

        public bool IsOpen => open;

        public string TrackedQuestId => _trackedQuestId ?? string.Empty;

        public void Bind(PlayableHostBootstrap host, HostCommandBridge bridge, HostSelectionController selection)
        {
            bootstrap = host;
            commandBridge = bridge;
            selectionController = selection;
        }

        public void ClearSessionState()
        {
            open = false;
            _trackedQuestId = string.Empty;
            _suppressAutoTrack = false;
            _selectedId = string.Empty;
            _status = string.Empty;
            _holdingPause = false;
            HostInputGate.Clear();
        }

        public void SetTrackedQuest(string questId)
        {
            _trackedQuestId = questId ?? string.Empty;
        }

        public void OpenToClaim()
        {
            open = true;
            _tab = Tab.Active;
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                open = !open;

            SyncPauseAndInputGate();
            ValidateTrackedQuest();
        }

        void SyncPauseAndInputGate()
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized)
            {
                HostInputGate.Clear();
                _holdingPause = false;
                return;
            }

            var invOpen = bootstrap.InventoryPanel != null && bootstrap.InventoryPanel.IsOpen;
            HostInputGate.BlockWorldCamera = open || invOpen;
            HostInputGate.BlockWorldInteraction = open || invOpen;

            if (open)
            {
                session.IsPaused = true;
                _holdingPause = true;
            }
            else if (_holdingPause && !invOpen)
            {
                session.IsPaused = false;
                _holdingPause = false;
            }
        }

        void ValidateTrackedQuest()
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized)
                return;

            if (!string.IsNullOrEmpty(_trackedQuestId))
            {
                if (!session.World.Quests.TryGet(_trackedQuestId, out var rt) ||
                    rt.Status == QuestStatus.Inactive ||
                    rt.Status == QuestStatus.Failed ||
                    rt.Status == QuestStatus.Completed)
                    _trackedQuestId = string.Empty;
            }

            if (!string.IsNullOrEmpty(_trackedQuestId))
                return;
            if (_suppressAutoTrack)
                return;

            // 开局／尚无追踪时，自动盯住第一个进行中／待领奖任务
            foreach (var kv in session.World.Quests.Runtime)
            {
                if (kv.Value.Status == QuestStatus.ReadyToClaim ||
                    kv.Value.Status == QuestStatus.Active)
                {
                    _trackedQuestId = kv.Key;
                    return;
                }
            }
        }

        void OnGUI()
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized)
                return;
            if (bootstrap.ContentInterrupt != null && bootstrap.ContentInterrupt.HasBlockingInterrupt)
                return;

            EnsureStyles();
            DrawLauncherButton(session);

            if (!open)
                return;

            HostUiHitTest.Block(new Rect(0f, 0f, Screen.width, Screen.height));
            RefreshEntries(session);
            DrawJournal(session);
        }

        void DrawLauncherButton(PlayableHostSession session)
        {
            const float bw = 72f;
            const float bh = 36f;
            var r = new Rect(Screen.width - bw - 12f, 56f, bw, bh);
            HostUiHitTest.Block(r);
            Fill(r, Parchment);
            DrawFrame(r, ParchmentDark);
            if (GUI.Button(r, open ? "关闭" : "任务", _title))
                open = !open;

            if (QuestJournalQuery.HasUnclaimedRewards(session.World))
            {
                var dot = new Rect(r.xMax - 14f, r.y + 4f, 10f, 10f);
                Fill(dot, RedDot);
            }
        }

        void RefreshEntries(PlayableHostSession session)
        {
            QuestJournalQuery.Collect(session.World, ResolveSubject(session), _entries);
            if (string.IsNullOrEmpty(_selectedId) && _entries.Count > 0)
                _selectedId = FirstVisibleId();
        }

        string FirstVisibleId()
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (MatchesTab(_entries[i]))
                    return _entries[i].QuestId;
            }

            return string.Empty;
        }

        bool MatchesTab(QuestListEntry e)
        {
            switch (_tab)
            {
                case Tab.Offer:
                    return e.Kind == QuestListKind.Available || e.Kind == QuestListKind.Locked;
                case Tab.Active:
                    return e.Kind == QuestListKind.Active || e.Kind == QuestListKind.ReadyToClaim;
                default:
                    return e.Kind == QuestListKind.Completed || e.Kind == QuestListKind.Failed;
            }
        }

        void DrawJournal(PlayableHostSession session)
        {
            var w = Mathf.Min(720f, Screen.width - 40f);
            var h = Mathf.Min(500f, Screen.height - 80f);
            var box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            DrawDim();
            Fill(box, Parchment);
            DrawFrame(box, ParchmentDark);

            GUI.Label(new Rect(box.x + 16f, box.y + 10f, 200f, 28f), "任务日志", _title);
            if (GUI.Button(new Rect(box.xMax - 88f, box.y + 10f, 72f, 28f), "关闭"))
                open = false;

            var tabY = box.y + 44f;
            DrawTab(new Rect(box.x + 16f, tabY, 140f, 28f), Tab.Offer, "可接／未来");
            DrawTab(new Rect(box.x + 164f, tabY, 140f, 28f), Tab.Active, "进行中");
            DrawTab(new Rect(box.x + 312f, tabY, 140f, 28f), Tab.Done, "已完成");

            if (QuestJournalQuery.HasUnclaimedRewards(session.World))
                GUI.Label(new Rect(box.x + 460f, tabY + 4f, 200f, 22f), "● 有奖励待领取", _small);

            var listRect = new Rect(box.x + 16f, box.y + 84f, 260f, box.height - 120f);
            var detailRect = new Rect(listRect.xMax + 12f, listRect.y, box.xMax - listRect.xMax - 28f, listRect.height);
            Fill(listRect, new Color(1f, 1f, 1f, 0.22f));
            Fill(detailRect, new Color(1f, 1f, 1f, 0.18f));
            DrawFrame(listRect, ParchmentDark);
            DrawFrame(detailRect, ParchmentDark);

            DrawList(listRect);
            DrawDetail(session, detailRect);

            if (!string.IsNullOrEmpty(_status))
                GUI.Label(new Rect(box.x + 16f, box.yMax - 28f, box.width - 32f, 22f), _status, _small);
        }

        void DrawTab(Rect r, Tab tab, string label)
        {
            var on = _tab == tab;
            if (on)
                Fill(r, new Color(Accent.r, Accent.g, Accent.b, 0.35f));
            DrawFrame(r, ParchmentDark);
            if (GUI.Button(r, label, _body))
            {
                _tab = tab;
                _selectedId = FirstVisibleId();
                _listScroll = Vector2.zero;
            }
        }

        void DrawList(Rect listRect)
        {
            var inner = new Rect(0f, 0f, listRect.width - 18f, Mathf.Max(listRect.height, CountVisible() * 36f + 8f));
            _listScroll = GUI.BeginScrollView(listRect, _listScroll, inner);
            var y = 4f;
            for (var i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!MatchesTab(e))
                    continue;
                var row = new Rect(4f, y, inner.width - 8f, 32f);
                var selected = e.QuestId == _selectedId;
                if (selected)
                    Fill(row, new Color(Accent.r, Accent.g, Accent.b, 0.28f));
                var label = KindPrefix(e) + e.Name;
                if (e.QuestId == _trackedQuestId)
                    label = "★ " + label;
                if (GUI.Button(row, label, _body))
                    _selectedId = e.QuestId;
                if (e.CanClaim)
                {
                    var dot = new Rect(row.xMax - 14f, row.y + 11f, 8f, 8f);
                    Fill(dot, RedDot);
                }

                y += 36f;
            }

            if (y <= 8f)
                GUI.Label(new Rect(8f, 8f, inner.width - 16f, 40f), "（本栏暂无任务）", _small);
            GUI.EndScrollView();
        }

        int CountVisible()
        {
            var n = 0;
            for (var i = 0; i < _entries.Count; i++)
            {
                if (MatchesTab(_entries[i]))
                    n++;
            }

            return n;
        }

        static string KindPrefix(QuestListEntry e)
        {
            switch (e.Kind)
            {
                case QuestListKind.Available: return "[可接] ";
                case QuestListKind.Locked: return "[锁定] ";
                case QuestListKind.Active: return "[进行] ";
                case QuestListKind.ReadyToClaim: return "[领奖] ";
                case QuestListKind.Completed: return "[完成] ";
                case QuestListKind.Failed: return "[失败] ";
                default: return "";
            }
        }

        void DrawDetail(PlayableHostSession session, Rect detailRect)
        {
            QuestListEntry selected = null;
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].QuestId == _selectedId)
                {
                    selected = _entries[i];
                    break;
                }
            }

            if (selected == null)
            {
                GUI.Label(new Rect(detailRect.x + 10f, detailRect.y + 10f, detailRect.width - 20f, 40f),
                    "选择左侧任务查看详情", _body);
                return;
            }

            var pad = 10f;
            var contentH = 300f;
            var view = new Rect(detailRect.x, detailRect.y, detailRect.width, detailRect.height - 88f);
            var inner = new Rect(0f, 0f, view.width - 18f, contentH);
            _detailScroll = GUI.BeginScrollView(view, _detailScroll, inner);

            var x = pad;
            var y = pad;
            var tw = inner.width - pad * 2f;
            GUI.Label(new Rect(x, y, tw, 24f), selected.Name, _title);
            y += 28f;
            var status = StatusLabel(selected);
            if (selected.QuestId == _trackedQuestId)
                status += " · 追踪中";
            GUI.Label(new Rect(x, y, tw, 18f), "状态：" + status, _small);
            y += 22f;
            GUI.Label(new Rect(x, y, tw, 72f),
                string.IsNullOrEmpty(selected.Description) ? "（无说明）" : selected.Description, _body);
            y += 78f;
            GUI.Label(new Rect(x, y, tw, 18f), "目标", _title);
            y += 22f;
            if (!string.IsNullOrEmpty(selected.ProgressLabel))
            {
                GUI.Label(new Rect(x, y, tw, 18f), "进度 " + selected.ProgressLabel, _body);
                y += 20f;
            }

            GUI.Label(new Rect(x, y, tw, 48f), selected.ObjectivesSummary, _body);
            y += 54f;
            GUI.Label(new Rect(x, y, tw, 18f), "奖励", _title);
            y += 22f;
            GUI.Label(new Rect(x, y, tw, 48f), selected.RewardsSummary, _body);
            y += 54f;
            if (selected.Kind == QuestListKind.Locked && !string.IsNullOrEmpty(selected.LockReason))
                GUI.Label(new Rect(x, y, tw, 36f), "未解锁：" + selected.LockReason, _small);

            GUI.EndScrollView();

            var btnY = detailRect.yMax - 76f;
            var btnW = (detailRect.width - 28f) / 3f;
            if (selected.CanAccept)
            {
                if (GUI.Button(new Rect(detailRect.x + 10f, btnY, btnW, 32f), "接取任务"))
                    DoQuestCommand(PlayerCommandKind.StartQuest, selected.QuestId, "已接取");
            }
            else
            {
                GUI.enabled = false;
                GUI.Button(new Rect(detailRect.x + 10f, btnY, btnW, 32f), "接取任务");
                GUI.enabled = true;
            }

            if (selected.CanClaim)
            {
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.75f, 0.35f, 1f);
                if (GUI.Button(new Rect(detailRect.x + 14f + btnW, btnY, btnW, 32f), "领取奖励"))
                    DoQuestCommand(PlayerCommandKind.ClaimQuestRewards, selected.QuestId, "已领取奖励");
                GUI.backgroundColor = prev;
            }
            else
            {
                GUI.enabled = false;
                GUI.Button(new Rect(detailRect.x + 14f + btnW, btnY, btnW, 32f), "领取奖励");
                GUI.enabled = true;
            }

            if (selected.CanAbandon)
            {
                if (GUI.Button(new Rect(detailRect.x + 18f + btnW * 2f, btnY, btnW, 32f), "放弃任务"))
                    DoQuestCommand(PlayerCommandKind.AbandonQuest, selected.QuestId, "已放弃");
            }
            else
            {
                GUI.enabled = false;
                GUI.Button(new Rect(detailRect.x + 18f + btnW * 2f, btnY, btnW, 32f), "放弃任务");
                GUI.enabled = true;
            }

            var trackY = detailRect.yMax - 40f;
            var canTrack = selected.Kind == QuestListKind.Active || selected.Kind == QuestListKind.ReadyToClaim;
            var isTracked = selected.QuestId == _trackedQuestId;
            GUI.enabled = canTrack;
            if (GUI.Button(new Rect(detailRect.x + 10f, trackY, detailRect.width - 20f, 28f),
                    isTracked ? "取消追踪" : "追踪此任务（显示在右侧任务栏）"))
            {
                if (isTracked)
                {
                    _trackedQuestId = string.Empty;
                    _suppressAutoTrack = true;
                    _status = "已取消追踪";
                }
                else
                {
                    _trackedQuestId = selected.QuestId;
                    _suppressAutoTrack = false;
                    _status = "已追踪 · " + selected.Name;
                }
            }

            GUI.enabled = true;
        }

        static string StatusLabel(QuestListEntry e)
        {
            switch (e.Kind)
            {
                case QuestListKind.Available: return "可接取";
                case QuestListKind.Locked: return "未解锁";
                case QuestListKind.Active: return "进行中";
                case QuestListKind.ReadyToClaim: return "待领取奖励";
                case QuestListKind.Completed: return "已完成";
                case QuestListKind.Failed: return "已失败";
                default: return e.Status.ToString();
            }
        }

        void DoQuestCommand(PlayerCommandKind kind, string questId, string okMsg)
        {
            if (commandBridge == null)
            {
                _status = "CommandBridge 未绑定";
                return;
            }

            if (commandBridge.SubmitQuestCommand(kind, questId))
            {
                _status = okMsg + " · " + questId;
                if (kind == PlayerCommandKind.StartQuest)
                {
                    _trackedQuestId = questId;
                    _suppressAutoTrack = false;
                }
                else if (kind == PlayerCommandKind.AbandonQuest)
                {
                    if (_trackedQuestId == questId)
                        _trackedQuestId = string.Empty;
                    _selectedId = string.Empty;
                }
                else if (kind == PlayerCommandKind.ClaimQuestRewards)
                {
                    if (_trackedQuestId == questId)
                        _trackedQuestId = string.Empty;
                }
            }
            else
            {
                _status = commandBridge.LastStatus;
            }
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
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _px);
            GUI.color = prev;
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
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Ink }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Ink }
            };
            _small = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
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
            EnsurePx();
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
    }
}
