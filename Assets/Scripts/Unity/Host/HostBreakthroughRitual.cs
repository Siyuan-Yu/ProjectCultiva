using System.Text;
using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Input;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 突破蓄势（约 10 秒黄条）→ 结算后暂停世界弹窗。
    /// 可取消；移动／受伤／其他指令会打断并失败。
    /// </summary>
    public sealed class HostBreakthroughRitual : MonoBehaviour
    {
        public const float DefaultDurationSeconds = 10f;

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] float durationSeconds = DefaultDurationSeconds;

        readonly CultivationService _cultivation = new CultivationService();

        EntityId _subject = EntityId.None;
        string _subjectName = string.Empty;
        float _elapsed;
        bool _channeling;
        bool _resultOpen;
        bool _holdingPause;
        BreakthroughReport _report;
        Vector2 _scroll;

        int _baselineHp = -1;
        int _baselineSpirit = -1;
        Vector3 _baselineWorldPos;
        bool _hasBaselinePos;

        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _small;
        Texture2D _px;

        static readonly Color Parchment = new Color(0.92f, 0.86f, 0.74f, 0.98f);
        static readonly Color ParchmentDark = new Color(0.70f, 0.58f, 0.42f, 1f);
        static readonly Color Ink = new Color(0.16f, 0.12f, 0.08f, 1f);
        static readonly Color BarYellow = new Color(0.95f, 0.82f, 0.22f, 1f);
        static readonly Color BarYellowTrack = new Color(0.45f, 0.38f, 0.22f, 0.85f);

        public bool IsChanneling => _channeling;
        public bool IsResultOpen => _resultOpen;
        public bool IsBusy => _channeling || _resultOpen;
        public EntityId ChannelSubject => _channeling ? _subject : EntityId.None;
        public float Progress01 =>
            !_channeling || durationSeconds <= 0.01f
                ? 0f
                : Mathf.Clamp01(_elapsed / durationSeconds);
        public string ChannelSubjectName => _subjectName;

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
            _elapsed = 0f;
            ClearBaseline();
            ReleasePause();
        }

        public bool TryBegin(EntityId subject, out string reason)
        {
            reason = string.Empty;
            if (IsBusy)
            {
                reason = "已有突破进行中";
                return false;
            }

            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
            {
                reason = "会话未就绪";
                return false;
            }

            if (!_cultivation.CanAttemptBreakthrough(bootstrap.Session.World, subject, out reason))
                return false;

            if (!bootstrap.Session.World.Entities.TryGet(subject, out var entity))
            {
                reason = "目标不存在";
                return false;
            }

            // 停下当前行动，专心冲击；之后再采基线，避免把自己的 Stop 当成打断。
            bootstrap.Session.Loop?.StopSubject(subject);
            bootstrap.GetComponent<HostWorkLoop>()?.StopLoop(subject);
            if (bootstrap.MoveController != null && bootstrap.MoveController.IsMoving(subject))
            {
                reason = "移动中无法冲击瓶颈，请先停下";
                return false;
            }

            _subject = subject;
            _subjectName = string.IsNullOrEmpty(entity.DisplayName) ? subject.ToString() : entity.DisplayName;
            _elapsed = 0f;
            _channeling = true;
            _resultOpen = false;
            _report = null;
            CaptureBaseline(entity);

            bootstrap.CultivationPanel?.Close();
            bootstrap.CharacterSheetPanel?.Close();
            bootstrap.RelationPanel?.Close();
            return true;
        }

        /// <summary>玩家主动取消：无修为惩罚、无弹窗。</summary>
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

        /// <summary>被移动／受伤／其他指令打断：修为小损 + 失败弹窗。</summary>
        public void InterruptChannel(string detail)
        {
            if (!_channeling)
                return;

            var subject = _subject;
            var name = _subjectName;
            _channeling = false;
            ClearBaseline();

            BreakthroughReport report;
            if (bootstrap?.Session != null && bootstrap.Session.IsInitialized)
            {
                var resolved = _cultivation.FailBreakthroughChannel(
                    bootstrap.Session.World,
                    subject,
                    string.IsNullOrEmpty(detail) ? "冲击被打断，突破失败。" : detail);
                if (resolved.IsSuccess)
                    report = resolved.Value;
                else
                {
                    report = new BreakthroughReport
                    {
                        Subject = subject,
                        ActorName = name,
                        Succeeded = false,
                        FromRealmLabel = "—",
                        ToRealmLabel = "—",
                        Detail = resolved.Error != null ? resolved.Error.Message : "冲击被打断"
                    };
                }
            }
            else
            {
                report = new BreakthroughReport
                {
                    Subject = subject,
                    ActorName = name,
                    Succeeded = false,
                    FromRealmLabel = "—",
                    ToRealmLabel = "—",
                    Detail = "冲击被打断，突破失败。"
                };
            }

            _report = report;
            _resultOpen = true;
            if (bootstrap?.Session != null)
            {
                bootstrap.Session.IsPaused = true;
                _holdingPause = true;
            }
        }

        /// <summary>
        /// 指令桥调用：Stop＝取消；其他行动指令＝打断失败。
        /// 返回 true 表示本指令不应再继续下发（已取消／已打断）。
        /// </summary>
        public bool TryHandleCommandDuringChannel(EntityId subject, PlayerCommandKind kind)
        {
            if (!IsChannelingSubject(subject))
                return false;

            if (kind == PlayerCommandKind.Stop)
            {
                CancelChannel();
                return true;
            }

            InterruptChannel("下达了其他指令，冲击中断。");
            return true;
        }

        /// <summary>移动下令前调用；返回 true 表示已打断、移动仍可继续。</summary>
        public bool NotifyMoveOrdered(EntityId subject)
        {
            if (!IsChannelingSubject(subject))
                return false;
            InterruptChannel("移动打断了冲击瓶颈。");
            return true;
        }

        void Update()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;

            if (_resultOpen)
            {
                HostInputGate.BlockWorldCamera = true;
                HostInputGate.BlockWorldInteraction = true;
                if (!_holdingPause)
                {
                    bootstrap.Session.IsPaused = true;
                    _holdingPause = true;
                }

                return;
            }

            if (!_channeling)
            {
                ReleasePause();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F1))
            {
                CancelChannel();
                return;
            }

            if (_subject.IsNone ||
                !bootstrap.Session.World.Entities.TryGet(_subject, out var entity))
            {
                ClearSessionState();
                return;
            }

            if (DetectInterrupt(entity, out var why))
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

            CompleteChannelNatural();
        }

        void CompleteChannelNatural()
        {
            _channeling = false;
            ClearBaseline();
            var resolved = _cultivation.TryBreakthrough(bootstrap.Session.World, _subject);
            if (resolved.IsFailure)
            {
                _report = new BreakthroughReport
                {
                    Subject = _subject,
                    ActorName = _subjectName,
                    Succeeded = false,
                    FromRealmLabel = "—",
                    ToRealmLabel = "—",
                    Detail = resolved.Error != null ? resolved.Error.Message : "突破未能结算"
                };
            }
            else
                _report = resolved.Value;

            _resultOpen = true;
            bootstrap.Session.IsPaused = true;
            _holdingPause = true;
        }

        bool DetectInterrupt(Entity entity, out string why)
        {
            why = string.Empty;
            if (bootstrap.MoveController != null && bootstrap.MoveController.IsMoving(_subject))
            {
                why = "移动打断了冲击瓶颈。";
                return true;
            }

            if (_hasBaselinePos)
            {
                var viewSpawner = bootstrap.ViewSpawner;
                if (viewSpawner != null &&
                    viewSpawner.Registry.TryGet(_subject, out var view) &&
                    view != null)
                {
                    var delta = view.transform.position - _baselineWorldPos;
                    delta.z = 0f;
                    if (delta.sqrMagnitude > 0.35f * 0.35f)
                    {
                        why = "身形挪动，冲击中断。";
                        return true;
                    }
                }
            }

            CombatDamageRules.EnsureVitals(entity);
            if (entity.TryGet<CombatVitalsComponent>(out var vitals))
            {
                if (_baselineHp >= 0 && vitals.CurrentHp < _baselineHp)
                {
                    why = "受伤打断了冲击瓶颈。";
                    return true;
                }

                if (_baselineSpirit >= 0 && vitals.CurrentSpiritPower < _baselineSpirit)
                {
                    why = "灵力受创，冲击中断。";
                    return true;
                }
            }

            if (entity.TryGet<ActionStateComponent>(out var st) &&
                st.HasActiveAction &&
                bootstrap.Session.World.ActiveActions.TryGetValue(st.ActiveActionId, out var action))
            {
                if (action is MoveAction)
                {
                    why = "移动打断了冲击瓶颈。";
                    return true;
                }

                // Wait＝占位；Cultivate／Work 等说明被拉去做别的事。
                if (!(action is WaitAction))
                {
                    why = "其他行动打断了冲击瓶颈。";
                    return true;
                }
            }

            return false;
        }

        void CaptureBaseline(Entity entity)
        {
            ClearBaseline();
            CombatDamageRules.EnsureVitals(entity);
            if (entity.TryGet<CombatVitalsComponent>(out var vitals))
            {
                _baselineHp = vitals.CurrentHp;
                _baselineSpirit = vitals.CurrentSpiritPower;
            }

            var viewSpawner = bootstrap != null ? bootstrap.ViewSpawner : null;
            if (viewSpawner != null &&
                viewSpawner.Registry.TryGet(_subject, out var view) &&
                view != null)
            {
                _baselineWorldPos = view.transform.position;
                _hasBaselinePos = true;
            }
        }

        void ClearBaseline()
        {
            _baselineHp = -1;
            _baselineSpirit = -1;
            _hasBaselinePos = false;
        }

        void OnGUI()
        {
            if (_channeling)
            {
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

            var dim = new Rect(0f, 0f, Screen.width, Screen.height);
            Fill(dim, new Color(0f, 0f, 0f, 0.45f));
            HostUiHitTest.Block(dim);

            var w = Mathf.Min(480f, Screen.width - 40f);
            var h = Mathf.Min(360f, Screen.height - 40f);
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            HostUiHitTest.Block(rect);
            Fill(rect, Parchment);
            DrawFrame(rect, ParchmentDark);

            var headline = _report.Succeeded ? "突破成功" : "突破失败";
            GUI.Label(new Rect(rect.x + 16f, rect.y + 12f, rect.width - 32f, 28f), headline, _title);

            var body = new Rect(rect.x + 16f, rect.y + 48f, rect.width - 32f, rect.height - 100f);
            var text = BuildResultText(_report);
            var viewH = Mathf.Max(body.height, _body.CalcHeight(new GUIContent(text), body.width - 18f) + 8f);
            _scroll = GUI.BeginScrollView(body, _scroll, new Rect(0f, 0f, body.width - 18f, viewH));
            GUI.Label(new Rect(0f, 0f, body.width - 18f, viewH), text, _body);
            GUI.EndScrollView();

            if (HostImguiStyles.ParchmentBtn(new Rect(rect.x + (rect.width - 120f) * 0.5f, rect.yMax - 44f, 120f, 32f), "知道了"))
                CloseResult();
        }

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

            var label = (_subjectName.Length > 0 ? _subjectName : "修士") + " 冲击瓶颈… Esc/F1 取消";
            var labelStyle = HostImguiStyles.InkLabel(11, bold: true, ink: Ink);
            labelStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(bar, label, labelStyle);

            var cancelRect = new Rect(bar.xMax + gap, bar.y - 2f, cancelW, barH + 4f);
            if (HostImguiStyles.ParchmentBtn(cancelRect, "取消"))
                CancelChannel();
        }

        void CloseResult()
        {
            _resultOpen = false;
            _report = null;
            _subject = EntityId.None;
            _subjectName = string.Empty;
            ReleasePause();
        }

        void ReleasePause()
        {
            if (!_holdingPause)
                return;
            _holdingPause = false;
            if (bootstrap?.Session != null)
                bootstrap.Session.IsPaused = false;
            HostInputGate.Clear();
        }

        static string BuildResultText(BreakthroughReport report)
        {
            var sb = new StringBuilder(256);
            var name = string.IsNullOrEmpty(report.ActorName) ? "修士" : report.ActorName;
            if (report.Succeeded)
            {
                sb.AppendLine(name + " 突破成功！");
                sb.AppendLine();
                sb.AppendLine("境界　" + report.FromRealmLabel + " → " + report.ToRealmLabel);
                sb.AppendLine();
                if (report.AttributeChanges != null && report.AttributeChanges.Count > 0)
                {
                    sb.AppendLine("属性变化");
                    for (var i = 0; i < report.AttributeChanges.Count; i++)
                    {
                        var d = report.AttributeChanges[i];
                        var sign = d.Delta > 0 ? "+" : string.Empty;
                        sb.AppendLine("　" + AttrName(d.Id) + "　" + d.Before + " → " + d.After +
                                      "（" + sign + d.Delta + "）");
                    }
                }
                else
                    sb.AppendLine("属性　无明显变化");
            }
            else
            {
                sb.AppendLine(name + " 突破失败。");
                sb.AppendLine();
                sb.AppendLine("境界　仍为 " + report.FromRealmLabel);
                if (report.ProgressLost > 0)
                    sb.AppendLine("修为　损失 " + report.ProgressLost);
                if (!string.IsNullOrEmpty(report.Detail))
                {
                    sb.AppendLine();
                    sb.AppendLine(report.Detail);
                }
            }

            return sb.ToString();
        }

        static string AttrName(AttributeId id)
        {
            switch (id)
            {
                case AttributeId.MaxHp: return "体魄";
                case AttributeId.Attack: return "攻击";
                case AttributeId.Defense: return "防御";
                case AttributeId.Speed: return "身法";
                case AttributeId.Stamina: return "耐力";
                case AttributeId.SpiritSense: return "神识";
                case AttributeId.Comprehension: return "悟性";
                case AttributeId.SpiritPower: return "灵力";
                case AttributeId.Cultivation: return "修为";
                case AttributeId.MindState: return "心境";
                default: return id.ToString();
            }
        }

        void EnsureStyles()
        {
            if (_title != null)
                return;
            _px = Texture2D.whiteTexture;
            _title = HostImguiStyles.InkLabel(18, bold: true, ink: Ink);
            _body = HostImguiStyles.InkLabel(13, wordWrap: true, ink: Ink);
            _small = HostImguiStyles.InkLabel(12, ink: Ink);
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
            Fill(new Rect(r.x, r.y, r.width, 1f), c);
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            Fill(new Rect(r.x, r.y, 1f, r.height), c);
            Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }
    }
}
