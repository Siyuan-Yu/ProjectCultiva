using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Inventory;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 斗技学习／装配：已学列表、1–6 键位装备、从背包秘本学习。
    /// 入口＝脚下状态板右侧「斗技」（与人物／境界／关系并列）。
    /// </summary>
    public sealed class HostCombatArtsPanel : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] bool open;

        EntityId _subject = EntityId.None;
        DefinitionId? _selectedArt;
        string _status = string.Empty;
        bool _holdingPause;
        Vector2 _scrollLearned;
        Vector2 _scrollBag;

        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _small;
        Texture2D _px;

        static readonly Color Parchment = new Color(0.92f, 0.86f, 0.74f, 0.98f);
        static readonly Color ParchmentDark = new Color(0.70f, 0.58f, 0.42f, 1f);
        static readonly Color Ink = new Color(0.16f, 0.12f, 0.08f, 1f);
        static readonly Color SlotFill = new Color(0.85f, 0.78f, 0.62f, 1f);
        static readonly Color SlotOn = new Color(0.78f, 0.62f, 0.38f, 1f);

        readonly CombatArtItemLearnService _learn = new CombatArtItemLearnService();
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
            _status = string.Empty;
            open = true;
            _scrollLearned = Vector2.zero;
            _scrollBag = Vector2.zero;
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

            if (bootstrap.QuestJournal != null && bootstrap.QuestJournal.IsOpen)
            {
                if (open)
                    open = false;
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
                    Close();
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
            Fill(dim, new Color(0f, 0f, 0f, 0.45f));
            HostUiHitTest.Block(dim);

            var w = 560f;
            var h = 480f;
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            HostUiHitTest.Block(rect);
            Fill(rect, Parchment);
            DrawFrame(rect, ParchmentDark);

            var x = rect.x + 16f;
            var y = rect.y + 12f;
            GUI.Label(new Rect(x, y, w - 100f, 26f), "斗技 · " + name, _title);
            if (GUI.Button(new Rect(rect.xMax - 72f, y, 56f, 26f), "关闭"))
                Close();
            y += 30f;
            GUI.Label(
                new Rect(x, y, w - 32f, 20f),
                isParty
                    ? "已学列表 · 点选后装到 1–6 键 · 可从背包秘本学习（秘本不消耗）"
                    : "仅查看（非己方不可改装配／学习）",
                _small);
            y += 24f;

            // —— 装备栏 1–6 ——
            GUI.Label(new Rect(x, y, w - 32f, 20f), "快捷键装备栏", _body);
            y += 22f;
            var slotW = 78f;
            var gap = 6f;
            for (var i = 0; i < CombatArtsComponent.MaxEquippedSlots; i++)
            {
                var sx = x + i * (slotW + gap);
                var sr = new Rect(sx, y, slotW, 48f);
                Fill(sr, SlotFill);
                DrawFrame(sr, ParchmentDark);
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

            y += 56f;
            GUI.Label(
                new Rect(x, y, w - 32f, 18f),
                "先点下方已学斗技选中，再点上方键位；再点已装键位可卸下",
                _small);
            y += 22f;

            var midH = 200f;
            var leftW = (w - 40f) * 0.58f;
            var rightW = (w - 40f) * 0.42f;
            var leftR = new Rect(x, y, leftW, midH);
            var rightR = new Rect(x + leftW + 8f, y, rightW - 8f, midH);
            DrawFrame(leftR, ParchmentDark);
            DrawFrame(rightR, ParchmentDark);

            // —— 已学 ——
            GUI.Label(new Rect(leftR.x + 8f, leftR.y + 4f, leftW - 16f, 20f), "已学斗技", _body);
            var learnedView = new Rect(leftR.x + 4f, leftR.y + 26f, leftW - 8f, midH - 32f);
            var learnedContent = new Rect(0f, 0f, learnedView.width - 18f, Mathf.Max(arts.Learned.Count * 52f, learnedView.height));
            _scrollLearned = GUI.BeginScrollView(learnedView, _scrollLearned, learnedContent);
            var ly = 0f;
            if (arts.Learned.Count == 0)
            {
                GUI.Label(new Rect(4f, ly, learnedContent.width - 8f, 40f), "尚未学会任何斗技。", _small);
            }
            else
            {
                for (var i = 0; i < arts.Learned.Count; i++)
                {
                    var id = arts.Learned[i];
                    world.TryGetCombatArt(id, out var art);
                    var artName = art == null || string.IsNullOrEmpty(art.Name) ? id.ToString() : art.Name;
                    var grade = art == null || string.IsNullOrEmpty(art.Grade) ? "" : " · " + art.Grade;
                    var kind = art != null && art.IsActiveSkill ? "主动" : "被动";
                    var slotHint = FindEquippedSlot(arts, id);
                    var selected = _selectedArt.HasValue && _selectedArt.Value.Equals(id);
                    var row = new Rect(4f, ly, learnedContent.width - 8f, 48f);
                    if (selected)
                        Fill(row, SlotOn);
                    var line = artName + grade + "（" + kind + "）" +
                               (slotHint >= 0 ? "　键" + (slotHint + 1) : "");
                    if (GUI.Button(row, line + "\n" + Summarize(art), _small))
                    {
                        _selectedArt = id;
                        _status = "已选中「" + artName + "」→ 点上方键位装配";
                    }

                    ly += 52f;
                }
            }

            GUI.EndScrollView();

            // —— 从背包学 ——
            GUI.Label(new Rect(rightR.x + 8f, rightR.y + 4f, rightW - 16f, 20f), "背包秘本", _body);
            CollectTomes(world, arts, _tomeScratch);
            var bagView = new Rect(rightR.x + 4f, rightR.y + 26f, rightW - 16f, midH - 32f);
            var bagContentH = Mathf.Max(_tomeScratch.Count * 56f + 8f, bagView.height);
            var bagContent = new Rect(0f, 0f, bagView.width - 18f, bagContentH);
            _scrollBag = GUI.BeginScrollView(bagView, _scrollBag, bagContent);
            var by = 0f;
            if (_tomeScratch.Count == 0)
            {
                GUI.Label(
                    new Rect(4f, by, bagContent.width - 8f, 60f),
                    "背包无未学斗技秘本。\n洞府／任务可获得。",
                    _small);
            }
            else
            {
                for (var i = 0; i < _tomeScratch.Count; i++)
                {
                    var itemId = _tomeScratch[i].ItemId;
                    var artText = _tomeScratch[i].ArtIdText;
                    CombatArtSpec art = null;
                    if (DefinitionId.TryParse(artText, out var aid))
                        world.TryGetCombatArt(aid, out art);
                    var artName = art == null || string.IsNullOrEmpty(art.Name) ? artText : art.Name;
                    var tomeName = world.InventoryCatalog.GetName(itemId);
                    GUI.Label(
                        new Rect(4f, by, bagContent.width - 8f, 32f),
                        tomeName + "\n→ " + artName,
                        _small);
                    by += 34f;
                    GUI.enabled = isParty;
                    if (GUI.Button(new Rect(4f, by, bagContent.width - 8f, 22f), "学习"))
                    {
                        var result = _learn.TryLearnFromItem(world, _subject, itemId);
                        if (result.IsSuccess)
                        {
                            _status = "已学会「" + artName + "」";
                            _selectedArt = aid;
                            bootstrap.DispatchDrainedEvents();
                        }
                        else
                            _status = result.Error.Message ?? "学习失败";
                    }

                    GUI.enabled = true;
                    by += 26f;
                }
            }

            GUI.EndScrollView();

            y += midH + 10f;
            if (!string.IsNullOrEmpty(_status))
                GUI.Label(new Rect(x, y, w - 32f, 36f), _status, _body);
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

        static string Summarize(CombatArtSpec art)
        {
            if (art == null)
                return "";
            if (!string.IsNullOrEmpty(art.EffectSummary))
            {
                var s = art.EffectSummary;
                return s.Length > 28 ? s.Substring(0, 28) + "…" : s;
            }

            if (art.IsActiveSkill)
                return "主动 ×" + art.HitCount + " · CD " + art.CooldownSeconds.ToString("0.#") + "s";
            return "被动普攻加成";
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

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _px != null ? _px : Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void DrawFrame(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), _px);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), _px);
            GUI.DrawTexture(new Rect(r.x, r.y, 2f, r.height), _px);
            GUI.DrawTexture(new Rect(r.xMax - 2f, r.y, 2f, r.height), _px);
            GUI.color = prev;
        }
    }
}
