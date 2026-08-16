using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Inventory;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 斗技：一级＝已学列表排布；二级＝点进后的熟练度／材料／效果页。
    /// 入口＝脚下状态板右侧「斗技」。
    /// </summary>
    public sealed class HostCombatArtsPanel : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] bool open;

        EntityId _subject = EntityId.None;
        DefinitionId? _selectedArt;
        bool _detailOpen;
        bool _breakConfirmOpen;
        string _status = string.Empty;
        bool _holdingPause;
        Vector2 _scrollList;
        Vector2 _scrollDetail;
        Vector2 _scrollBag;

        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _small;
        Texture2D _px;

        static readonly Color Parchment = HostSkillMasteryPanelUi.Parchment;
        static readonly Color ParchmentDark = HostSkillMasteryPanelUi.ParchmentDark;
        static readonly Color Ink = HostSkillMasteryPanelUi.Ink;
        static readonly Color SlotFill = new Color(0.85f, 0.78f, 0.62f, 1f);

        readonly List<(string ItemId, string ArtIdText)> _tomeScratch =
            new List<(string, string)>(8);

        public bool IsOpen => open;

        public void Bind(PlayableHostBootstrap host, HostSelectionController selection)
        {
            bootstrap = host;
            selectionController = selection;
        }

        public void ClearSessionState()
        {
            open = false;
            _detailOpen = false;
            _breakConfirmOpen = false;
            _subject = EntityId.None;
            _selectedArt = null;
            _status = string.Empty;
            _holdingPause = false;
            HostInputGate.Clear();
        }

        public void OpenFor(EntityId id)
        {
            if (id.IsNone)
                return;
            _subject = id;
            _selectedArt = null;
            _detailOpen = false;
            _breakConfirmOpen = false;
            _status = string.Empty;
            open = true;
            _scrollList = Vector2.zero;
            _scrollDetail = Vector2.zero;
            _scrollBag = Vector2.zero;
        }

        public void Close()
        {
            open = false;
            _detailOpen = false;
            _breakConfirmOpen = false;
            ReleasePause();
        }

        void Update()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;

            if (bootstrap.QuestJournal != null && bootstrap.QuestJournal.IsOpen)
            {
                if (open)
                    open = false;
                _detailOpen = false;
                ReleasePause();
                return;
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

                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    if (_breakConfirmOpen)
                        _breakConfirmOpen = false;
                    else if (_detailOpen)
                    {
                        _detailOpen = false;
                        _status = string.Empty;
                    }
                    else
                        Close();
                }
            }
            else
                ReleasePause();
        }

        void ReleasePause()
        {
            if (!_holdingPause)
                return;
            if (bootstrap?.Session != null)
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
                Close();
                return;
            }

            if (!entity.TryGet<CombatArtsComponent>(out var arts))
            {
                arts = new CombatArtsComponent();
                entity.AddComponent(arts);
            }

            var world = bootstrap.Session.World;
            var isParty = selectionController != null && selectionController.IsPartyUnit(_subject);
            var name = string.IsNullOrEmpty(entity.DisplayName) ? _subject.ToString() : entity.DisplayName;

            var dim = new Rect(0f, 0f, Screen.width, Screen.height);
            HostSkillMasteryPanelUi.Fill(dim, new Color(0f, 0f, 0f, 0.45f));
            HostUiHitTest.Block(dim);

            var w = 620f;
            var h = 560f;
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            HostUiHitTest.Block(rect);
            HostSkillMasteryPanelUi.Fill(rect, Parchment);
            HostSkillMasteryPanelUi.DrawFrame(rect, ParchmentDark);

            if (_detailOpen && _selectedArt.HasValue)
                DrawDetailPage(rect, world, arts, isParty, name);
            else
                DrawListPage(rect, world, arts, isParty, name);

            if (_breakConfirmOpen && _selectedArt.HasValue)
                DrawBreakthroughConfirm(world, arts);
        }

        void DrawBreakthroughConfirm(
            XianXia.Core.Simulation.SimulationWorld world,
            CombatArtsComponent arts)
        {
            var artId = _selectedArt.Value;
            world.TryGetCombatArt(artId, out var art);
            var artName = art == null || string.IsNullOrEmpty(art.Name) ? artId.ToString() : art.Name;
            var profile = art != null
                ? SkillMasteryLookup.EnsureOrDefaultArt(art)
                : null;
            var state = arts.GetOrCreateMastery(artId);
            var from = SkillMasteryTierNames.Display(state.Tier);
            var to = SkillMasteryTierNames.Display(SkillMasteryLookup.NextTier(profile, state.Tier));
            var chance = new SkillMasteryService().EvaluateMasteryBreakthroughChance(world, _subject);
            var pct = (int)System.Math.Round(chance * 100.0);
            var costs = SkillMasteryLookup.BreakthroughCosts(profile, state.Tier);
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
                "是否冲击斗技「" + artName + "」熟练度？\n" +
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
            else if (study.TryBeginBreakthroughArt(_subject, artId, out var beginReason))
            {
                _status = "开始冲击熟练…";
                open = false;
                _detailOpen = false;
            }
            else
                _status = string.IsNullOrEmpty(beginReason) ? "无法突破" : beginReason;
        }

        void DrawListPage(
            Rect rect,
            XianXia.Core.Simulation.SimulationWorld world,
            CombatArtsComponent arts,
            bool isParty,
            string actorName)
        {
            var x = rect.x + 16f;
            var y = rect.y + 12f;
            GUI.Label(new Rect(x, y, rect.width - 160f, 26f), "斗技 · " + actorName, _title);
            if (HostImguiStyles.ParchmentBtn(new Rect(rect.xMax - 72f, y, 56f, 26f), "关闭"))
                Close();
            y += 28f;
            GUI.Label(
                new Rect(x, y, rect.width - 32f, 18f),
                isParty
                    ? "点列表进熟练度页 · 上方 1–6 装已选斗技 · 秘本在列表下方"
                    : "仅查看",
                _small);
            y += 22f;

            GUI.Label(new Rect(x, y, rect.width - 32f, 18f), "快捷键装备栏", _body);
            y += 20f;
            var slotW = 88f;
            var gap = 6f;
            for (var i = 0; i < CombatArtsComponent.MaxEquippedSlots; i++)
            {
                var sx = x + i * (slotW + gap);
                var sr = new Rect(sx, y, slotW, 44f);
                HostSkillMasteryPanelUi.Fill(sr, SlotFill);
                HostSkillMasteryPanelUi.DrawFrame(sr, ParchmentDark);
                var eq = arts.GetEquipped(i);
                string label;
                if (!eq.HasValue)
                    label = (i + 1) + "\n—";
                else if (world.TryGetCombatArt(eq.Value, out var art) && art != null)
                {
                    var n = string.IsNullOrEmpty(art.Name) ? eq.Value.ToString() : art.Name;
                    if (n.Length > 4)
                        n = n.Substring(0, 4);
                    label = (i + 1) + "\n" + n;
                }
                else
                    label = (i + 1) + "\n?";

                if (GUI.Button(sr, label, _small) && isParty)
                {
                    if (_selectedArt.HasValue)
                    {
                        if (arts.TryEquipToSlot(i, _selectedArt.Value))
                            _status = "已装到键 " + (i + 1);
                        else
                            _status = "装备失败（需先学会）";
                    }
                    else if (eq.HasValue)
                    {
                        arts.ClearSlot(i);
                        _status = "已卸下键 " + (i + 1);
                    }
                }
            }

            y += 52f;

            var bagH = 110f;
            var listH = rect.yMax - y - bagH - 36f;
            var listR = new Rect(x, y, rect.width - 32f, listH);
            HostSkillMasteryPanelUi.DrawFrame(listR, ParchmentDark);
            GUI.Label(new Rect(listR.x + 8f, listR.y + 4f, 200f, 18f), "已学斗技", _body);

            var view = new Rect(listR.x + 4f, listR.y + 24f, listR.width - 8f, listH - 28f);
            var rowH = 64f;
            var contentH = Mathf.Max(view.height, Mathf.Max(1, arts.Learned.Count) * (rowH + 6f) + 8f);
            _scrollList = GUI.BeginScrollView(view, _scrollList, new Rect(0f, 0f, view.width - 18f, contentH));
            var ly = 4f;
            if (arts.Learned.Count == 0)
            {
                GUI.Label(new Rect(8f, ly, view.width - 30f, 40f), "尚未学会任何斗技。", _small);
            }
            else
            {
                for (var i = 0; i < arts.Learned.Count; i++)
                {
                    var id = arts.Learned[i];
                    world.TryGetCombatArt(id, out var art);
                    var artName = art == null || string.IsNullOrEmpty(art.Name) ? id.ToString() : art.Name;
                    var grade = art == null || string.IsNullOrEmpty(art.Grade) ? "" : art.Grade;
                    var kind = art != null && art.IsActiveSkill ? "主动" : "被动";
                    var m = arts.GetOrCreateMastery(id);
                    var tier = SkillMasteryTierNames.Display(m.Tier);
                    var slotHint = FindEquippedSlot(arts, id);
                    var selected = _selectedArt.HasValue && _selectedArt.Value.Equals(id);
                    var sub = grade + " · " + kind + " · 熟练 " + tier;
                    if (art != null)
                        sub += " · " + HostSkillMasteryPanelUi.ArtEffectLine(art, m.Tier);
                    var badge = slotHint >= 0 ? "键" + (slotHint + 1) : "";
                    var row = new Rect(4f, ly, view.width - 26f, rowH);
                    if (HostSkillMasteryPanelUi.DrawListRow(row, artName, sub, badge, selected, _body, _small))
                    {
                        _selectedArt = id;
                        _detailOpen = true;
                        _scrollDetail = Vector2.zero;
                        _status = string.Empty;
                    }

                    ly += rowH + 6f;
                }
            }

            GUI.EndScrollView();
            y = listR.yMax + 8f;

            // 可学秘本
            var bagR = new Rect(x, y, rect.width - 32f, bagH);
            HostSkillMasteryPanelUi.DrawFrame(bagR, ParchmentDark);
            GUI.Label(new Rect(bagR.x + 8f, bagR.y + 4f, 200f, 18f), "背包秘本（未学）", _body);
            CollectTomes(world, arts, _tomeScratch);
            var bagView = new Rect(bagR.x + 4f, bagR.y + 24f, bagR.width - 8f, bagH - 28f);
            var bagContentH = Mathf.Max(bagView.height, _tomeScratch.Count * 28f + 8f);
            _scrollBag = GUI.BeginScrollView(bagView, _scrollBag, new Rect(0f, 0f, bagView.width - 18f, bagContentH));
            var by = 2f;
            if (_tomeScratch.Count == 0)
                GUI.Label(new Rect(4f, by, bagView.width - 30f, 40f), "无未学秘本。", _small);
            else
            {
                var mastery = new SkillMasteryService();
                for (var i = 0; i < _tomeScratch.Count; i++)
                {
                    var itemId = _tomeScratch[i].ItemId;
                    var artText = _tomeScratch[i].ArtIdText;
                    CombatArtSpec art = null;
                    if (DefinitionId.TryParse(artText, out var aid))
                        world.TryGetCombatArt(aid, out art);
                    var artName = art == null || string.IsNullOrEmpty(art.Name) ? artText : art.Name;
                    var pct = art == null
                        ? 0
                        : (int)Mathf.Round((float)mastery.EvaluateArtLearnChance(world, _subject, art) * 100f);
                    var line = world.InventoryCatalog.GetName(itemId) + " → " + artName +
                               " · 学习成功率约 " + pct + "%";
                    GUI.Label(new Rect(4f, by, bagView.width - 120f, 24f), line, _small);
                    GUI.enabled = isParty;
                    if (HostImguiStyles.ParchmentBtn(new Rect(bagView.width - 100f, by, 72f, 22f), "参悟") &&
                        isParty)
                    {
                        var study = bootstrap.SkillStudyRitual;
                        if (study == null)
                            _status = "研读组件未就绪";
                        else if (study.TryBeginLearnArt(_subject, itemId, out var beginReason))
                        {
                            _status = "开始参悟…";
                            open = false;
                            _detailOpen = false;
                        }
                        else
                            _status = string.IsNullOrEmpty(beginReason) ? "无法参悟" : beginReason;
                    }

                    GUI.enabled = true;
                    by += 26f;
                }
            }

            GUI.EndScrollView();

            if (!string.IsNullOrEmpty(_status))
                GUI.Label(new Rect(x, rect.yMax - 28f, rect.width - 32f, 22f), _status, _small);
        }

        void DrawDetailPage(
            Rect rect,
            XianXia.Core.Simulation.SimulationWorld world,
            CombatArtsComponent arts,
            bool isParty,
            string actorName)
        {
            var artId = _selectedArt.Value;
            world.TryGetCombatArt(artId, out var art);
            var artName = art == null || string.IsNullOrEmpty(art.Name) ? artId.ToString() : art.Name;
            var profile = art != null
                ? SkillMasteryLookup.EnsureOrDefaultArt(art)
                : SkillMasteryLookup.CreateDefaultArtProfile(0, 0, 0);
            var state = arts.GetOrCreateMastery(artId);
            SkillMasteryLookup.SyncProgressCap(state, profile);

            var x = rect.x + 16f;
            var y = rect.y + 12f;
            GUI.Label(new Rect(x, y, rect.width - 200f, 26f), "斗技熟练 · " + artName, _title);
            if (HostImguiStyles.ParchmentBtn(new Rect(rect.xMax - 140f, y, 56f, 26f), "返回"))
            {
                _detailOpen = false;
                return;
            }

            if (HostImguiStyles.ParchmentBtn(new Rect(rect.xMax - 72f, y, 56f, 26f), "关闭"))
            {
                Close();
                return;
            }

            y += 32f;
            var grade = art == null || string.IsNullOrEmpty(art.Grade) ? "品阶未标" : art.Grade;
            var kind = art != null && art.IsActiveSkill ? "主动" : "被动";
            var summary = art == null || string.IsNullOrEmpty(art.EffectSummary)
                ? kind + " · 当前 " + SkillMasteryTierNames.Display(state.Tier)
                : art.EffectSummary + " · " + kind;

            var body = new Rect(x, y, rect.width - 32f, rect.height - 100f - (y - rect.y));
            HostSkillMasteryPanelUi.DrawMasteryDetailBody(
                body,
                artName,
                grade + " · " + actorName,
                summary,
                state,
                profile,
                tier => HostSkillMasteryPanelUi.ArtEffectLine(art, tier),
                world,
                _title,
                _body,
                _small,
                ref _scrollDetail);

            var masterySvc = new SkillMasteryService();
            var footerY = rect.yMax - 44f;
            GUI.enabled = isParty && !state.IsAtBottleneck &&
                          world.Entities.TryGet(_subject, out var ent) &&
                          ent.TryGet<CultivationComponent>(out var cult) &&
                          cult.Progress >= 10;
            if (HostImguiStyles.ParchmentBtn(new Rect(x, footerY, 110f, 30f), "灌注×10"))
            {
                if (masterySvc.TryInfuseArt(world, _subject, artId, 10, out var detail).IsSuccess)
                    _status = detail;
                else
                    _status = "灌注失败";
            }

            string brReason = string.Empty;
            var canBreak = isParty &&
                           masterySvc.CanBreakthroughArt(world, _subject, artId, out brReason) &&
                           (bootstrap.SkillStudyRitual == null || !bootstrap.SkillStudyRitual.IsBusy);
            if (!isParty)
                brReason = "非己方不可冲击";
            else if (bootstrap.SkillStudyRitual != null && bootstrap.SkillStudyRitual.IsBusy)
                brReason = "研读进行中";

            GUI.enabled = canBreak;
            if (HostImguiStyles.ParchmentBtn(new Rect(x + 118f, footerY, 120f, 30f), "冲击下一档"))
                _breakConfirmOpen = true;

            GUI.enabled = true;
            var hint = !canBreak && !string.IsNullOrEmpty(brReason) ? brReason : _status;
            if (!string.IsNullOrEmpty(hint))
                GUI.Label(new Rect(x + 250f, footerY + 6f, rect.width - 280f, 22f), hint, _small);
        }

        static int FindEquippedSlot(CombatArtsComponent arts, DefinitionId id)
        {
            for (var i = 0; i < CombatArtsComponent.MaxEquippedSlots; i++)
            {
                var eq = arts.GetEquipped(i);
                if (eq.HasValue && eq.Value.Equals(id))
                    return i;
            }

            return -1;
        }

        static void CollectTomes(
            XianXia.Core.Simulation.SimulationWorld world,
            CombatArtsComponent arts,
            List<(string ItemId, string ArtIdText)> into)
        {
            into.Clear();
            if (world?.Inventory?.Slots == null)
                return;
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            for (var i = 0; i < world.Inventory.Slots.Count; i++)
            {
                var slot = world.Inventory.Slots[i];
                if (slot == null || slot.IsEmpty)
                    continue;
                if (!world.InventoryCatalog.IsCombatArtTome(slot.ItemId))
                    continue;
                var artText = world.InventoryCatalog.GetTeachesArtId(slot.ItemId);
                if (string.IsNullOrEmpty(artText) || !seen.Add(slot.ItemId))
                    continue;
                if (DefinitionId.TryParse(artText, out var artId) && arts.Knows(artId))
                    continue;
                into.Add((slot.ItemId, artText));
            }
        }

        void EnsureStyles()
        {
            if (_title != null)
                return;
            _px = Texture2D.whiteTexture;
            _title = HostImguiStyles.InkLabel(16, bold: true, ink: Ink);
            _body = HostImguiStyles.InkLabel(13, ink: Ink);
            _small = HostImguiStyles.InkLabel(11, wordWrap: true, ink: Ink);
        }
    }
}
