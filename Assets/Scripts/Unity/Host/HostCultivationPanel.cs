using System.Text;
using UnityEngine;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 修炼境界／突破面板：打开时暂停。入口＝脚下「境界」。打坐用 F6。
    /// </summary>
    public sealed class HostCultivationPanel : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] KeyCode toggleKey = KeyCode.C;
        [SerializeField] bool open;

        EntityId _subject = EntityId.None;
        string _status = string.Empty;
        bool _holdingPause;
        Vector2 _scroll;

        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _small;
        Texture2D _px;
        readonly CultivationService _cultivation = new CultivationService();

        static readonly Color Parchment = new Color(0.92f, 0.86f, 0.74f, 0.98f);
        static readonly Color ParchmentDark = new Color(0.70f, 0.58f, 0.42f, 1f);

        public bool IsOpen => open;

        public void Bind(PlayableHostBootstrap host, HostSelectionController selection)
        {
            bootstrap = host;
            selectionController = selection;
        }

        public void ClearSessionState()
        {
            open = false;
            _subject = EntityId.None;
            _status = string.Empty;
            _holdingPause = false;
            HostInputGate.Clear();
        }

        public void OpenFor(EntityId id)
        {
            if (id.IsNone)
                return;
            _subject = id;
            open = true;
            _status = string.Empty;
        }

        public void Close()
        {
            open = false;
            ReleasePause();
        }

        void Update()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;

            var journal = bootstrap.QuestJournal;
            if (journal != null && journal.IsOpen)
            {
                if (open)
                    open = false;
                ReleasePause();
                return;
            }

            if (Input.GetKeyDown(toggleKey))
            {
                // 默认关闭快捷键；打开靠脚下「境界」。保留键以便调试。
                if (open)
                    open = false;
            }

            if (open)
            {
                HostInputGate.BlockWorldCamera = true;
                HostInputGate.BlockWorldInteraction = true;
                if (!_holdingPause)
                {
                    bootstrap.Session.IsPaused = true;
                    _holdingPause = true;
                }
            }
            else
            {
                ReleasePause();
            }
        }

        void ReleasePause()
        {
            if (!_holdingPause)
                return;
            bootstrap.Session.IsPaused = false;
            _holdingPause = false;
            HostInputGate.Clear();
        }

        void OnGUI()
        {
            if (!open || bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            EnsureStyles();

            if (_subject.IsNone ||
                !bootstrap.Session.World.Entities.TryGet(_subject, out var entity))
            {
                open = false;
                return;
            }

            var w = Mathf.Min(640f, Screen.width - 40f);
            var h = Mathf.Min(520f, Screen.height - 40f);
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            HostUiHitTest.Block(rect);
            Fill(rect, Parchment);
            DrawFrame(rect, ParchmentDark);

            var name = string.IsNullOrEmpty(entity.DisplayName) ? _subject.ToString() : entity.DisplayName;
            GUI.Label(new Rect(rect.x + 16f, rect.y + 12f, rect.width - 80f, 28f), "修炼 · " + name, _title);
            if (HostImguiStyles.ParchmentBtn(new Rect(rect.xMax - 72f, rect.y + 10f, 56f, 28f), "关闭"))
            {
                open = false;
                return;
            }

            entity.TryGet<CultivationComponent>(out var cult);
            var body = new Rect(rect.x + 16f, rect.y + 48f, rect.width - 32f, rect.height - 100f);
            _scroll = GUI.BeginScrollView(body, _scroll, new Rect(0f, 0f, body.width - 18f, 420f));
            GUI.Label(new Rect(0f, 0f, body.width - 18f, 400f), BuildBody(entity, cult), _body);
            GUI.EndScrollView();

            var can = _cultivation.CanAttemptBreakthrough(bootstrap.Session.World, _subject, out var reason);
            var ritual = bootstrap.BreakthroughRitual;
            if (ritual != null && ritual.IsBusy)
            {
                can = false;
                reason = ritual.IsChanneling ? "正在冲击瓶颈…" : "请先关闭突破结果";
            }

            var btn = new Rect(rect.x + 16f, rect.yMax - 44f, 140f, 32f);
            GUI.enabled = can;
            if (HostImguiStyles.ParchmentBtn(btn, "尝试突破"))
            {
                if (ritual == null)
                    _status = "突破组件未就绪";
                else if (ritual.TryBegin(_subject, out var beginReason))
                {
                    _status = "开始冲击瓶颈…";
                    open = false;
                }
                else
                    _status = string.IsNullOrEmpty(beginReason) ? "无法开始突破" : beginReason;
            }

            GUI.enabled = true;
            if (!can)
                GUI.Label(new Rect(btn.xMax + 12f, btn.y + 6f, rect.width - 180f, 24f), reason, _small);
            else if (!string.IsNullOrEmpty(_status))
                GUI.Label(new Rect(btn.xMax + 12f, btn.y + 6f, rect.width - 180f, 24f), _status, _small);
        }

        string BuildBody(Entity entity, CultivationComponent cult)
        {
            var sb = new StringBuilder(512);
            if (cult == null)
            {
                sb.Append("无修炼组件");
                return sb.ToString();
            }

            sb.AppendLine("境界　" + RealmDisplay.Format(cult.Realm, cult.MinorStage));
            sb.AppendLine("修为　" + cult.Progress + " / " + cult.BreakthroughProgressRequired +
                          (cult.IsAtBottleneck ? "　【瓶颈】" : ""));
            sb.AppendLine("修炼速　每 5 游戏分 +" + CultivationProgressRules.BaseProgressPerTick +
                          "（打坐中；倍速加快游戏时间）");
            if (cult.HasLearnedManual && cult.LearnedManualId.HasValue)
                sb.AppendLine("功法　" + ShortId(cult.LearnedManualId.Value.ToString()));
            else if (cult.Realm >= RealmStage.QiRefining)
                sb.AppendLine("功法　未得（炼气后突破需要）");
            else
                sb.AppendLine("功法　感应境无需（入炼气后才要）");

            var world = bootstrap.Session.World;
            if (world.RealmLadder != null &&
                world.RealmLadder.TryGetStep(cult.Realm, cult.MinorStage, out var step))
            {
                sb.AppendLine();
                sb.AppendLine("下一关　" + RealmDisplay.FormatStep(step));
                sb.AppendLine("所需修为　" + step.ProgressRequired);
                sb.AppendLine("基础成功率　" + step.SuccessPercent + "%（悟性略加成）");
                if (step.MajorRealmJump)
                    sb.AppendLine("※ 大境界跃迁，属性提升较大");
                if (step.GrantSpiritPower > 0)
                    sb.AppendLine("※ 成功后获得灵力上限 " + step.GrantSpiritPower + "（战斗护盾）");
                if (step.AttributeBonuses != null && step.AttributeBonuses.Count > 0)
                {
                    sb.Append("突破加成　");
                    var first = true;
                    foreach (var kv in step.AttributeBonuses)
                    {
                        if (!first) sb.Append("、");
                        first = false;
                        sb.Append(AttrName(kv.Key)).Append('+').Append(kv.Value);
                    }

                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("已无下一阶配置（或已达当前阶梯尽头）");
            }

            CombatDamageRules.EnsureVitals(entity);
            if (entity.TryGet<CombatVitalsComponent>(out var vitals) &&
                entity.TryGet<AttributesComponent>(out var attrs))
            {
                sb.AppendLine();
                sb.AppendLine("体魄　" + vitals.CurrentHp + " / " + attrs.GetFinal(AttributeId.MaxHp));
                if (cult.Realm >= RealmStage.QiRefining)
                    sb.AppendLine("灵力护盾　" + vitals.CurrentSpiritPower + " / " +
                                  attrs.GetFinal(AttributeId.SpiritPower) +
                                  "（受伤优先扣灵力）");
                else
                    sb.AppendLine("灵力　感应境尚不可运转（入炼气后开启护盾）");
            }

            sb.AppendLine();
            sb.AppendLine("说明：打坐请用 F6／底栏修炼钮；「尝试突破」后约 10 秒蓄势（可取消／移动受伤会打断失败），结束弹窗结算。");
            sb.AppendLine("天气／灵地细判尚未接入，成功率以配置＋悟性为主。");
            return sb.ToString();
        }

        EntityId ResolveFocus()
        {
            if (selectionController != null && selectionController.State.Count > 0)
                return selectionController.State.SelectedIds[0];
            return EntityId.None;
        }

        void EnsureStyles()
        {
            if (_title != null)
                return;
            _px = Texture2D.whiteTexture;
            _title = HostImguiStyles.InkLabel(18, bold: true);
            _body = HostImguiStyles.InkLabel(13, wordWrap: true);
            _small = HostImguiStyles.InkLabel(12);
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
            Fill(new Rect(r.x, r.y, r.width, 1f), c);
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            Fill(new Rect(r.x, r.y, 1f, r.height), c);
            Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }

        static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "-";
            var i = id.IndexOf(':');
            return i >= 0 && i + 1 < id.Length ? id.Substring(i + 1) : id;
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
    }
}
