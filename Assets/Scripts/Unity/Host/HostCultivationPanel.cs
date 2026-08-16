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
        bool _manualDetailOpen;
        bool _breakConfirmOpen;
        Vector2 _scroll;
        Vector2 _scrollManualDetail;

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
            _manualDetailOpen = false;
            _breakConfirmOpen = false;
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
            _manualDetailOpen = false;
            _breakConfirmOpen = false;
            _status = string.Empty;
            _scrollManualDetail = Vector2.zero;
        }

        public void Close()
        {
            open = false;
            _manualDetailOpen = false;
            _breakConfirmOpen = false;
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
                {
                    if (_breakConfirmOpen)
                        _breakConfirmOpen = false;
                    else if (_manualDetailOpen)
                        _manualDetailOpen = false;
                    else
                        open = false;
                }
            }

            if (open && Input.GetKeyDown(KeyCode.Escape))
            {
                if (_breakConfirmOpen)
                    _breakConfirmOpen = false;
                else if (_manualDetailOpen)
                    _manualDetailOpen = false;
                else
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
                _manualDetailOpen = false;
                _breakConfirmOpen = false;
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
            entity.TryGet<CultivationComponent>(out var cult);

            if (_manualDetailOpen &&
                cult != null &&
                cult.HasLearnedManual &&
                cult.LearnedManualId.HasValue)
            {
                DrawManualMasteryPage(rect, name, cult);
                if (_breakConfirmOpen)
                    DrawManualBreakthroughConfirm(cult);
                return;
            }

            GUI.Label(new Rect(rect.x + 16f, rect.y + 12f, rect.width - 80f, 28f), "修炼 · " + name, _title);
            if (HostImguiStyles.ParchmentBtn(new Rect(rect.xMax - 72f, rect.y + 10f, 56f, 28f), "关闭"))
            {
                open = false;
                return;
            }

            var body = new Rect(rect.x + 16f, rect.y + 48f, rect.width - 32f, rect.height - 100f);
            var contentH = 720f;
            _scroll = GUI.BeginScrollView(body, _scroll, new Rect(0f, 0f, body.width - 18f, contentH));
            var y = 0f;
            y = DrawManualSection(cult, body.width - 18f, y);
            GUI.Label(new Rect(0f, y, body.width - 18f, contentH - y), BuildProgressBody(entity, cult), _body);
            GUI.EndScrollView();

            var can = _cultivation.CanAttemptBreakthrough(bootstrap.Session.World, _subject, out var reason);
            var ritual = bootstrap.BreakthroughRitual;
            if (ritual != null && ritual.IsBusy)
            {
                can = false;
                reason = ritual.IsChanneling ? "正在冲击瓶颈…" : "请先关闭突破结果";
            }

            if (bootstrap.SkillStudyRitual != null && bootstrap.SkillStudyRitual.IsBusy)
            {
                can = false;
                reason = "研读／熟练突破进行中";
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

        void DrawManualMasteryPage(Rect rect, string actorName, CultivationComponent cult)
        {
            var world = bootstrap.Session.World;
            var mid = cult.LearnedManualId.Value;
            world.TryGetManual(mid, out var manual);
            var mName = manual == null || string.IsNullOrEmpty(manual.Name)
                ? ShortId(mid.ToString())
                : manual.Name;
            var profile = SkillMasteryLookup.EnsureOrDefaultManual(manual);
            var mastery = cult.ManualMastery ?? SkillMasteryState.CreateEntry(profile);
            SkillMasteryLookup.SyncProgressCap(mastery, profile);
            cult.ManualMastery = mastery;

            var x = rect.x + 16f;
            var y = rect.y + 12f;
            GUI.Label(new Rect(x, y, rect.width - 200f, 26f), "功法熟练 · " + mName, _title);
            if (HostImguiStyles.ParchmentBtn(new Rect(rect.xMax - 140f, y, 56f, 26f), "返回"))
            {
                _manualDetailOpen = false;
                return;
            }

            if (HostImguiStyles.ParchmentBtn(new Rect(rect.xMax - 72f, y, 56f, 26f), "关闭"))
            {
                open = false;
                _manualDetailOpen = false;
                return;
            }

            y += 32f;
            var grade = manual == null || string.IsNullOrEmpty(manual.Grade) ? "品阶未标" : manual.Grade;
            var summary = manual == null || string.IsNullOrEmpty(manual.EffectSummary)
                ? BuildFallbackEffect(manual)
                : manual.EffectSummary;
            var body = new Rect(x, y, rect.width - 32f, rect.height - 100f - (y - rect.y));
            HostSkillMasteryPanelUi.DrawMasteryDetailBody(
                body,
                mName,
                grade + " · " + actorName,
                summary,
                mastery,
                profile,
                tier => HostSkillMasteryPanelUi.ManualEffectLine(manual, tier),
                world,
                _title,
                _body,
                _small,
                ref _scrollManualDetail);

            var masterySvc = new SkillMasteryService();
            var footerY = rect.yMax - 44f;
            GUI.enabled = !mastery.IsAtBottleneck && cult.Progress >= 10;
            if (HostImguiStyles.ParchmentBtn(new Rect(x, footerY, 120f, 30f), "灌注修为×10"))
            {
                if (masterySvc.TryInfuseManual(world, _subject, 10, out var detail).IsSuccess)
                    _status = detail;
                else
                    _status = "灌注失败";
            }

            var canBreak = masterySvc.CanBreakthroughManual(world, _subject, out var brReason);
            if (bootstrap.SkillStudyRitual != null && bootstrap.SkillStudyRitual.IsBusy)
            {
                canBreak = false;
                brReason = "研读进行中";
            }

            GUI.enabled = canBreak;
            if (HostImguiStyles.ParchmentBtn(new Rect(x + 128f, footerY, 120f, 30f), "冲击下一档"))
                _breakConfirmOpen = true;

            GUI.enabled = true;
            var hint = !canBreak && !string.IsNullOrEmpty(brReason) ? brReason : _status;
            if (!string.IsNullOrEmpty(hint))
                GUI.Label(new Rect(x + 260f, footerY + 6f, rect.width - 290f, 22f), hint, _small);
        }

        void DrawManualBreakthroughConfirm(CultivationComponent cult)
        {
            var world = bootstrap.Session.World;
            var mid = cult.LearnedManualId.Value;
            world.TryGetManual(mid, out var manual);
            var mName = manual == null || string.IsNullOrEmpty(manual.Name)
                ? ShortId(mid.ToString())
                : manual.Name;
            var profile = SkillMasteryLookup.EnsureOrDefaultManual(manual);
            var mastery = cult.ManualMastery ?? SkillMasteryState.CreateEntry(profile);
            var from = SkillMasteryTierNames.Display(mastery.Tier);
            var to = SkillMasteryTierNames.Display(SkillMasteryLookup.NextTier(profile, mastery.Tier));
            var chance = new SkillMasteryService().EvaluateMasteryBreakthroughChance(world, _subject);
            var pct = (int)System.Math.Round(chance * 100.0);
            var costs = SkillMasteryLookup.BreakthroughCosts(profile, mastery.Tier);
            var costLine = "材料：";
            if (costs == null || costs.Count == 0)
                costLine += "无";
            else
            {
                for (var i = 0; i < costs.Count; i++)
                {
                    var c = costs[i];
                    if (c == null || string.IsNullOrEmpty(c.ItemId))
                        continue;
                    if (i > 0)
                        costLine += "、";
                    costLine += HostSkillMasteryPanelUi.ShortItemName(world, c.ItemId) + "×" + c.Count;
                }
            }

            var body =
                "是否冲击功法「" + mName + "」熟练度？\n" +
                from + " → " + to + "\n" +
                "突破成功率约 " + pct + "%\n" +
                costLine + "\n（失败仍消耗材料）";
            var choice = HostSkillMasteryPanelUi.DrawBreakthroughConfirm(
                "确认冲击熟练", body, _title, _body);
            if (choice == HostSkillMasteryPanelUi.ConfirmChoice.No)
            {
                _breakConfirmOpen = false;
                return;
            }

            if (choice != HostSkillMasteryPanelUi.ConfirmChoice.Yes)
                return;

            _breakConfirmOpen = false;
            var study = bootstrap.SkillStudyRitual;
            if (study == null)
                _status = "研读组件未就绪";
            else if (study.TryBeginBreakthroughManual(_subject, out var beginReason))
            {
                _status = "开始冲击熟练…";
                open = false;
                _manualDetailOpen = false;
            }
            else
                _status = string.IsNullOrEmpty(beginReason) ? "无法突破" : beginReason;
        }

        /// <summary>当前所修功法卡片：再点一次进熟练度详情页。</summary>
        float DrawManualSection(CultivationComponent cult, float width, float y0)
        {
            var world = bootstrap.Session.World;
            var boxH = 96f;
            var box = new Rect(0f, y0, width, boxH);
            Fill(box, new Color(0.86f, 0.78f, 0.62f, 0.55f));
            DrawFrame(box, ParchmentDark);

            var x = 10f;
            var y = y0 + 8f;
            GUI.Label(new Rect(x, y, width - 20f, 20f), "当前功法（点开看熟练度）", _title);
            y += 24f;

            if (cult == null)
            {
                GUI.Label(new Rect(x, y, width - 20f, 40f), "无修炼组件", _body);
                return y0 + boxH + 10f;
            }

            if (cult.HasLearnedManual && cult.LearnedManualId.HasValue)
            {
                var mid = cult.LearnedManualId.Value;
                if (world.TryGetManual(mid, out var manual) && manual != null)
                {
                    var mName = string.IsNullOrEmpty(manual.Name) ? ShortId(mid.ToString()) : manual.Name;
                    var grade = string.IsNullOrEmpty(manual.Grade) ? "品阶未标" : manual.Grade;
                    var mastery = cult.ManualMastery ?? SkillMasteryState.CreateEntry(
                        SkillMasteryLookup.EnsureOrDefaultManual(manual));
                    var tierName = SkillMasteryTierNames.Display(mastery.Tier);
                    var speedNow = SkillMasteryLookup.ResolveCultivationSpeed(manual, mastery.Tier);
                    var sub = grade + " · 熟练 " + tierName + " · 打坐+" + speedNow + "/5分";
                    if (mastery.ProgressRequired > 0)
                        sub += " · " + mastery.Progress + "/" + mastery.ProgressRequired;
                    var row = new Rect(8f, y0 + 32f, width - 16f, 56f);
                    if (HostSkillMasteryPanelUi.DrawListRow(row, mName, sub, "详情", false, _body, _small))
                    {
                        _manualDetailOpen = true;
                        _scrollManualDetail = Vector2.zero;
                        _status = string.Empty;
                    }
                }
                else
                {
                    GUI.Label(
                        new Rect(x, y, width - 20f, 40f),
                        "已学功法（定义缺失）　" + ShortId(mid.ToString()),
                        _body);
                }
            }
            else
            {
                GUI.Label(new Rect(x, y, width - 20f, 22f), "还没有学功法", _body);
                y += 22f;
                GUI.Label(
                    new Rect(x, y, width - 20f, 40f),
                    cult.Realm >= RealmStage.QiRefining
                        ? "炼气后突破需要功法。背包秘籍使用后参悟（蓄势＋学习成功率），成功入门。"
                        : "感应境可先打坐积累修为；功法需秘籍／机缘显式学习，不会保底自动获得。",
                    _small);
            }

            return y0 + boxH + 10f;
        }

        static string BuildFallbackEffect(CultivationManualSpec manual)
        {
            if (manual == null)
                return "—";
            var parts = new StringBuilder(64);
            if (manual.CultivationSpeed > 0)
                parts.Append("打坐每 5 游戏分 +").Append(manual.CultivationSpeed).Append(" 修为");
            if (manual.GrantedModifiers != null && manual.GrantedModifiers.Count > 0)
            {
                if (parts.Length > 0)
                    parts.Append("；");
                parts.Append("属性修饰 ×").Append(manual.GrantedModifiers.Count);
            }

            return parts.Length > 0 ? parts.ToString() : "（无摘要）";
        }

        string BuildProgressBody(Entity entity, CultivationComponent cult)
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
            var speed = cult.HasLearnedManual && cult.CultivationSpeed > 0
                ? cult.CultivationSpeed
                : CultivationProgressRules.BaseProgressPerTick;
            sb.AppendLine("修炼速　每 5 游戏分 +" + speed +
                          "（打坐中；倍速加快游戏时间）");

            var world = bootstrap.Session.World;
            if (cult.HasLearnedManual && cult.LearnedManualId.HasValue &&
                world.TryGetManual(cult.LearnedManualId.Value, out var manual) &&
                manual != null &&
                !string.IsNullOrEmpty(manual.RequiredRealm))
                sb.AppendLine("功法所需境界　" + manual.RequiredRealm);

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
                sb.AppendLine("生命　" + vitals.CurrentHp + " / " + attrs.GetFinal(AttributeId.MaxHp));
                sb.AppendLine("体魄　" + attrs.GetFinal(AttributeId.Physique));
                if (cult.Realm >= RealmStage.QiRefining)
                    sb.AppendLine("灵力护盾　" + vitals.CurrentSpiritPower + " / " +
                                  attrs.GetFinal(AttributeId.SpiritPower) +
                                  "（受伤优先扣灵力）");
                else
                    sb.AppendLine("灵力　感应境尚不可运转（入炼气后开启护盾）");
            }

            sb.AppendLine();
            sb.AppendLine("说明：打坐 F6；功法熟练随打坐／灌注增长；入门满后可耗灵药×10＋粗木×10冲击小成。境界突破约 10 秒蓄势。");
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
            _small = HostImguiStyles.InkLabel(12, wordWrap: true);
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

        static string AttrName(AttributeId id) => HostAttributeLabels.Name(id);
    }
}
