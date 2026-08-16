using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Input;
using XianXia.Core.Results;

namespace XianXia.Unity.Host
{
    public enum SkillStudyKind
    {
        LearnManual = 0,
        LearnArt = 1,
        BreakthroughManual = 2,
        BreakthroughArt = 3
    }

    /// <summary>
    /// 功法／斗技：研读蓄势或熟练突破蓄势（黄条）→ 结果弹窗。
    /// 可取消；移动／受伤／其他指令会打断（不扣材料／不消耗秘籍）。
    /// </summary>
    public sealed class HostSkillStudyRitual : MonoBehaviour
    {
        public const float DefaultDurationSeconds = 8f;

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] float durationSeconds = DefaultDurationSeconds;

        readonly SkillMasteryService _mastery = new SkillMasteryService();

        SkillStudyKind _kind;
        EntityId _subject = EntityId.None;
        string _subjectName = string.Empty;
        string _itemId = string.Empty;
        DefinitionId _artId;
        CultivationManualSpec _manual;
        float _elapsed;
        bool _channeling;
        bool _resultOpen;
        bool _holdingPause;
        SkillStudyReport _report;
        int _baselineHp = -1;
        Vector3 _baselineWorldPos;
        bool _hasBaselinePos;

        GUIStyle _title;
        GUIStyle _body;

        static readonly Color Parchment = new Color(0.92f, 0.86f, 0.74f, 0.98f);
        static readonly Color ParchmentDark = new Color(0.70f, 0.58f, 0.42f, 1f);
        static readonly Color Ink = new Color(0.16f, 0.12f, 0.08f, 1f);
        static readonly Color BarYellow = new Color(0.95f, 0.82f, 0.22f, 1f);
        static readonly Color BarYellowTrack = new Color(0.45f, 0.38f, 0.22f, 0.85f);

        public bool IsBusy => _channeling || _resultOpen;
        public bool IsChanneling => _channeling;
        public EntityId ChannelSubject => _channeling ? _subject : EntityId.None;
        public float Progress01 =>
            !_channeling || durationSeconds <= 0.01f
                ? 0f
                : Mathf.Clamp01(_elapsed / durationSeconds);

        public bool IsChannelingSubject(EntityId id) =>
            _channeling && !id.IsNone && id.Equals(_subject);

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public void ClearSessionState()
        {
            _channeling = false;
            _resultOpen = false;
            _report = null;
            _subject = EntityId.None;
            _subjectName = string.Empty;
            _itemId = string.Empty;
            _manual = null;
            _elapsed = 0f;
            ClearBaseline();
            ReleasePause();
        }

        public bool TryBeginLearnManual(EntityId subject, string itemId, CultivationManualSpec manual, out string reason)
        {
            reason = string.Empty;
            if (!PrepareBegin(subject, out reason))
                return false;
            if (string.IsNullOrEmpty(itemId) || manual == null)
            {
                reason = "秘籍无效";
                return false;
            }

            if (bootstrap.Session.World.Inventory.GetCount(itemId) < 1)
            {
                reason = "背包无此秘籍";
                return false;
            }

            _kind = SkillStudyKind.LearnManual;
            _itemId = itemId;
            _manual = manual;
            StartChannel(subject);
            return true;
        }

        public bool TryBeginLearnArt(EntityId subject, string itemId, out string reason)
        {
            reason = string.Empty;
            if (!PrepareBegin(subject, out reason))
                return false;
            if (string.IsNullOrEmpty(itemId))
            {
                reason = "秘本无效";
                return false;
            }

            if (bootstrap.Session.World.Inventory.GetCount(itemId) < 1)
            {
                reason = "背包无此秘本";
                return false;
            }

            var artIdText = bootstrap.Session.World.InventoryCatalog.GetTeachesArtId(itemId);
            if (string.IsNullOrEmpty(artIdText) || !DefinitionId.TryParse(artIdText, out var artId))
            {
                reason = "秘本无效";
                return false;
            }

            if (bootstrap.Session.World.Entities.TryGet(subject, out var e) &&
                e.TryGet<CombatArtsComponent>(out var arts) &&
                arts.Knows(artId))
            {
                reason = "已学会该斗技";
                return false;
            }

            _kind = SkillStudyKind.LearnArt;
            _itemId = itemId;
            _artId = artId;
            _manual = null;
            StartChannel(subject);
            return true;
        }

        public bool TryBeginBreakthroughManual(EntityId subject, out string reason)
        {
            reason = string.Empty;
            if (!PrepareBegin(subject, out reason))
                return false;
            if (!_mastery.CanBreakthroughManual(bootstrap.Session.World, subject, out reason))
                return false;
            _kind = SkillStudyKind.BreakthroughManual;
            _itemId = string.Empty;
            _manual = null;
            StartChannel(subject);
            return true;
        }

        public bool TryBeginBreakthroughArt(EntityId subject, DefinitionId artId, out string reason)
        {
            reason = string.Empty;
            if (!PrepareBegin(subject, out reason))
                return false;
            if (!_mastery.CanBreakthroughArt(bootstrap.Session.World, subject, artId, out reason))
                return false;
            _kind = SkillStudyKind.BreakthroughArt;
            _artId = artId;
            _itemId = string.Empty;
            _manual = null;
            StartChannel(subject);
            return true;
        }

        public void CancelChannel()
        {
            if (!_channeling)
                return;
            _channeling = false;
            _subject = EntityId.None;
            _subjectName = string.Empty;
            _elapsed = 0f;
            ClearBaseline();
        }

        public void InterruptChannel(string detail)
        {
            if (!_channeling)
                return;
            _channeling = false;
            ClearBaseline();
            _report = new SkillStudyReport
            {
                Success = false,
                Title = "中断",
                Body = "参悟被打断：" + (string.IsNullOrEmpty(detail) ? "未知原因" : detail)
            };
            _resultOpen = true;
            HoldPause();
        }

        public bool NotifyMoveOrdered(EntityId subject)
        {
            if (!_channeling || subject.IsNone || !subject.Equals(_subject))
                return false;
            InterruptChannel("移动");
            return true;
        }

        /// <summary>指令期间：Stop＝取消；其他＝打断失败弹窗。返回 true 表示已处理蓄势。</summary>
        public bool TryHandleCommandDuringChannel(EntityId subject, PlayerCommandKind kind)
        {
            if (!_channeling || subject.IsNone || !subject.Equals(_subject))
                return false;
            if (kind == PlayerCommandKind.Stop)
            {
                CancelChannel();
                return true;
            }

            InterruptChannel("下达其他指令");
            return true;
        }

        bool PrepareBegin(EntityId subject, out string reason)
        {
            reason = string.Empty;
            if (IsBusy)
            {
                reason = "已有研读／突破进行中";
                return false;
            }

            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
            {
                reason = "会话未就绪";
                return false;
            }

            if (subject.IsNone || !bootstrap.Session.World.Entities.TryGet(subject, out _))
            {
                reason = "目标不存在";
                return false;
            }

            if (bootstrap.BreakthroughRitual != null && bootstrap.BreakthroughRitual.IsBusy)
            {
                reason = "境界突破进行中";
                return false;
            }

            bootstrap.Session.Loop?.StopSubject(subject);
            bootstrap.GetComponent<HostWorkLoop>()?.StopLoop(subject);
            if (bootstrap.MoveController != null && bootstrap.MoveController.IsMoving(subject))
            {
                reason = "移动中无法专心参悟，请先停下";
                return false;
            }

            return true;
        }

        void StartChannel(EntityId subject)
        {
            var world = bootstrap.Session.World;
            world.Entities.TryGet(subject, out var entity);
            _subject = subject;
            _subjectName = entity != null && !string.IsNullOrEmpty(entity.DisplayName)
                ? entity.DisplayName
                : subject.ToString();
            _elapsed = 0f;
            _channeling = true;
            _resultOpen = false;
            _report = null;
            CaptureBaseline(entity);
            bootstrap.CultivationPanel?.Close();
            bootstrap.CombatArtsPanel?.Close();
            bootstrap.ManualLearnPrompt?.Close();
            bootstrap.CombatArtLearnPrompt?.Close();
        }

        void Update()
        {
            if (_resultOpen)
            {
                HoldPause();
                return;
            }

            if (!_channeling)
            {
                ReleasePause();
                return;
            }

            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
            {
                InterruptChannel("会话中断");
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F1))
            {
                CancelChannel();
                return;
            }

            if (CheckInterrupted(out var why))
            {
                InterruptChannel(why);
                return;
            }

            var dt = bootstrap.PresentationDeltaTime;
            if (dt <= 0f)
                dt = Time.unscaledDeltaTime;
            _elapsed += dt;
            if (_elapsed < Mathf.Max(0.5f, durationSeconds))
                return;

            FinishChannel();
        }

        void FinishChannel()
        {
            _channeling = false;
            ClearBaseline();
            var world = bootstrap.Session.World;
            Result r;
            SkillStudyReport report = null;
            switch (_kind)
            {
                case SkillStudyKind.LearnManual:
                    r = _mastery.TryFinishManualStudy(world, _subject, _manual, out report);
                    break;
                case SkillStudyKind.LearnArt:
                    r = _mastery.TryFinishArtStudy(world, _subject, _itemId, out report);
                    break;
                case SkillStudyKind.BreakthroughManual:
                    r = _mastery.TryBreakthroughManual(world, _subject, out report);
                    break;
                case SkillStudyKind.BreakthroughArt:
                    r = _mastery.TryBreakthroughArt(world, _subject, _artId, out report);
                    break;
                default:
                    r = Result.Failure(ErrorCode.InvalidOperation, "Unknown kind.");
                    break;
            }

            if (r.IsFailure)
            {
                report = new SkillStudyReport
                {
                    Success = false,
                    Title = "失败",
                    Body = r.Error != null ? r.Error.Message : "未知错误"
                };
            }
            else if (report != null && report.Success)
            {
                QuestProgressRefresh.AfterWorldChange(world, _subject);
            }

            _report = report;
            _resultOpen = true;
            HoldPause();
            bootstrap.DispatchDrainedEvents();
        }

        bool CheckInterrupted(out string why)
        {
            why = string.Empty;
            var world = bootstrap.Session.World;
            if (!world.Entities.TryGet(_subject, out var entity))
            {
                why = "角色消失";
                return true;
            }

            if (entity.TryGet<LifecycleComponent>(out var life) && (life.IsDead || life.IsRemoved))
            {
                why = "角色倒下";
                return true;
            }

            if (bootstrap.MoveController != null && bootstrap.MoveController.IsMoving(_subject))
            {
                why = "移动";
                return true;
            }

            if (_hasBaselinePos && bootstrap.ViewSpawner != null &&
                bootstrap.ViewSpawner.Registry.TryGet(_subject, out var view) && view != null)
            {
                if ((view.transform.position - _baselineWorldPos).sqrMagnitude > 0.25f)
                {
                    why = "离开原地";
                    return true;
                }
            }

            if (_baselineHp >= 0 &&
                entity.TryGet<CombatVitalsComponent>(out var vitals) &&
                vitals.CurrentHp < _baselineHp)
            {
                why = "受伤";
                return true;
            }

            return false;
        }

        void CaptureBaseline(Entity entity)
        {
            _baselineHp = -1;
            _hasBaselinePos = false;
            if (entity != null && entity.TryGet<CombatVitalsComponent>(out var v))
                _baselineHp = v.CurrentHp;
            if (bootstrap.ViewSpawner != null &&
                bootstrap.ViewSpawner.Registry.TryGet(_subject, out var view) && view != null)
            {
                _baselineWorldPos = view.transform.position;
                _hasBaselinePos = true;
            }
        }

        void ClearBaseline()
        {
            _baselineHp = -1;
            _hasBaselinePos = false;
        }

        void HoldPause()
        {
            if (bootstrap?.Session == null)
                return;
            if (!_holdingPause)
            {
                bootstrap.Session.IsPaused = true;
                _holdingPause = true;
            }

            HostInputGate.BlockWorldCamera = true;
            HostInputGate.BlockWorldInteraction = true;
        }

        void ReleasePause()
        {
            if (_holdingPause && bootstrap?.Session != null)
            {
                bootstrap.Session.IsPaused = false;
                _holdingPause = false;
            }
        }

        void CloseResult()
        {
            _resultOpen = false;
            _report = null;
            _subject = EntityId.None;
            ReleasePause();
            HostInputGate.Clear();
        }

        void OnGUI()
        {
            if (_channeling)
            {
                // 与境界突破同一位置：底栏状态板上方黄条（勿再画屏幕中上独立框）
                var panelW = 560f;
                var panelH = 192f;
                var panel = new Rect(
                    (Screen.width - panelW) * 0.5f,
                    Screen.height - panelH - 10f,
                    panelW,
                    panelH);
                DrawChannelBarAbove(panel);
            }

            if (!_resultOpen || _report == null)
                return;
            EnsureStyles();
            DrawResult();
        }

        /// <summary>对齐 <see cref="HostBreakthroughRitual.DrawChannelBarAbove"/>：面板顶黄条＋取消。</summary>
        public void DrawChannelBarAbove(Rect statusPanel)
        {
            if (!_channeling)
                return;
            EnsureStyles();
            var barH = 14f;
            var cancelW = 56f;
            var gap = 6f;
            var bar = new Rect(
                statusPanel.x,
                statusPanel.y - barH - 6f,
                statusPanel.width - cancelW - gap,
                barH);
            Fill(bar, BarYellowTrack);
            var fillW = bar.width * Progress01;
            if (fillW > 1f)
                Fill(new Rect(bar.x, bar.y, fillW, bar.height), BarYellow);
            DrawFrame(bar, ParchmentDark);
            HostUiHitTest.Block(new Rect(bar.x, bar.y - 2f, bar.width + cancelW + gap, bar.height + 4f));

            string label;
            if (_kind == SkillStudyKind.LearnManual || _kind == SkillStudyKind.LearnArt)
            {
                label = (_subjectName.Length > 0 ? _subjectName : "修士") +
                        " 参悟中… Esc/F1 取消";
            }
            else
            {
                label = (_subjectName.Length > 0 ? _subjectName : "修士") +
                        " 冲击熟练瓶颈… Esc/F1 取消";
            }

            var labelStyle = HostImguiStyles.InkLabel(11, bold: true, ink: Ink);
            labelStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(bar, label, labelStyle);

            var cancelRect = new Rect(bar.xMax + gap, bar.y - 2f, cancelW, barH + 4f);
            if (HostImguiStyles.ParchmentBtn(cancelRect, "取消"))
                CancelChannel();
        }

        void DrawResult()
        {
            HostUiHitTest.Block(new Rect(0f, 0f, Screen.width, Screen.height));
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.5f));
            var w = 420f;
            var h = 200f;
            var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            HostUiHitTest.Block(r);
            Fill(r, Parchment);
            DrawFrame(r, ParchmentDark);
            GUI.Label(new Rect(r.x + 16f, r.y + 14f, w - 32f, 28f), _report.Title, _title);
            GUI.Label(new Rect(r.x + 16f, r.y + 48f, w - 32f, 90f), _report.Body, _body);
            if (HostImguiStyles.ParchmentBtn(new Rect(r.x + w * 0.5f - 48f, r.yMax - 44f, 96f, 28f), "知道了"))
                CloseResult();
        }

        void EnsureStyles()
        {
            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true
                };
                _title.normal.textColor = Ink;
            }

            if (_body == null)
            {
                _body = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
                _body.normal.textColor = Ink;
            }
        }

        static void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        static void DrawFrame(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, 2f, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 2f, r.y, 2f, r.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
