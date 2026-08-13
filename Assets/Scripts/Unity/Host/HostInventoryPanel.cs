using UnityEngine;
using XianXia.Core.Inventory;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Shared party bag UI (IMGUI): slots, category filter, one-click organize.
    /// </summary>
    public sealed class HostInventoryPanel : MonoBehaviour
    {
        enum Filter
        {
            All = 0,
            Resource = 1,
            Consumable = 2,
            Other = 3
        }

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] KeyCode toggleKey = KeyCode.B;
        [SerializeField] bool open;

        Filter _filter = Filter.All;
        Vector2 _scroll;
        string _status = string.Empty;
        bool _holdingPause;
        int _selectedSlot = -1;

        GUIStyle _title;
        GUIStyle _small;
        GUIStyle _slot;

        public bool IsOpen => open;

        public void Toggle() => open = !open;

        public void Open() => open = true;

        public void Close() => open = false;

        public void Bind(PlayableHostBootstrap host)
        {
            bootstrap = host;
        }

        public void ClearSessionState()
        {
            open = false;
            _selectedSlot = -1;
            _status = string.Empty;
            _holdingPause = false;
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
                if (_holdingPause)
                    _holdingPause = false;
                return;
            }

            if (Input.GetKeyDown(toggleKey))
                open = !open;

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
            else if (_holdingPause)
            {
                bootstrap.Session.IsPaused = false;
                _holdingPause = false;
                HostInputGate.Clear();
            }
        }

        void OnGUI()
        {
            if (!open || bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            EnsureStyles();

            var inv = bootstrap.Session.World.Inventory;
            var catalog = bootstrap.Session.World.InventoryCatalog;
            var w = Mathf.Min(720f, Screen.width - 40f);
            var h = Mathf.Min(560f, Screen.height - 40f);
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(rect, GUIContent.none);
            GUI.Box(rect, GUIContent.none);

            var x = rect.x + 16f;
            var y = rect.y + 12f;
            GUI.Label(
                new Rect(x, y, w - 32f, 28f),
                "小队背包  " + inv.UsedSlotCount + "/" + inv.SlotCapacity + "  （B 关闭）",
                _title);
            y += 32f;

            DrawFilters(x, y);
            y += 36f;

            if (GUI.Button(new Rect(x, y, 100f, 28f), "一键整理"))
            {
                inv.Organize();
                _status = "已整理";
                _selectedSlot = -1;
            }

            if (GUI.Button(new Rect(x + 110f, y, 72f, 28f), "关闭"))
                open = false;
            y += 36f;

            var gridTop = y;
            var gridH = rect.yMax - gridTop - 56f;
            var view = new Rect(rect.x + 12f, gridTop, w - 24f, gridH);
            const int cols = 5;
            const float cell = 88f;
            const float gap = 8f;

            // Count visible for scroll height.
            var visible = 0;
            for (var i = 0; i < inv.SlotCapacity; i++)
            {
                if (IsVisible(catalog, inv.Slots[i]))
                    visible++;
            }

            var rows = Mathf.Max(1, (visible + cols - 1) / cols);
            var contentH = rows * (cell + gap) + 8f;
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0, 0, view.width - 18f, contentH));

            var drawIndex = 0;
            for (var i = 0; i < inv.SlotCapacity; i++)
            {
                var slot = inv.Slots[i];
                if (!IsVisible(catalog, slot))
                    continue;

                var col = drawIndex % cols;
                var row = drawIndex / cols;
                var r = new Rect(col * (cell + gap), row * (cell + gap), cell, cell);
                if (_selectedSlot == i)
                    GUI.color = new Color(1f, 0.92f, 0.7f);
                if (GUI.Button(r, SlotLabel(catalog, slot), _slot))
                    _selectedSlot = i;
                GUI.color = Color.white;
                drawIndex++;
            }

            GUI.EndScrollView();

            var detailY = rect.yMax - 44f;
            var detail = "选中格子查看详情";
            if (_selectedSlot >= 0 && _selectedSlot < inv.Slots.Count)
            {
                var s = inv.Slots[_selectedSlot];
                detail = s.IsEmpty
                    ? "空槽位"
                    : catalog.GetName(s.ItemId) + " ×" + s.Count +
                      "  堆叠上限 " + catalog.GetMaxStack(s.ItemId) +
                      "\n" + s.ItemId;
            }

            if (!string.IsNullOrEmpty(_status))
                detail = _status + " ｜ " + detail;
            GUI.Label(new Rect(rect.x + 16f, detailY, w - 32f, 40f), detail, _small);
        }

        void DrawFilters(float x, float y)
        {
            var labels = new[] { "全部", "资源", "消耗", "其它" };
            for (var i = 0; i < labels.Length; i++)
            {
                var on = (int)_filter == i;
                var r = new Rect(x + i * 76f, y, 70f, 26f);
                if (on)
                    GUI.color = new Color(0.95f, 0.85f, 0.55f);
                if (GUI.Button(r, labels[i]))
                {
                    _filter = (Filter)i;
                    _selectedSlot = -1;
                }

                GUI.color = Color.white;
            }
        }

        bool IsVisible(InventoryCatalog catalog, InventorySlot slot)
        {
            if (_filter == Filter.All)
                return true;
            if (slot.IsEmpty)
                return false;
            switch (_filter)
            {
                case Filter.Resource:
                    return catalog.HasTag(slot.ItemId, "resource") &&
                           !catalog.HasTag(slot.ItemId, "consumable");
                case Filter.Consumable:
                    return catalog.HasTag(slot.ItemId, "consumable");
                case Filter.Other:
                    return !catalog.HasTag(slot.ItemId, "resource") &&
                           !catalog.HasTag(slot.ItemId, "consumable");
                default:
                    return true;
            }
        }

        static string SlotLabel(InventoryCatalog catalog, InventorySlot slot)
        {
            if (slot == null || slot.IsEmpty)
                return "空";
            var name = catalog.GetName(slot.ItemId);
            if (name.Length > 6)
                name = name.Substring(0, 6);
            return name + "\n×" + slot.Count;
        }

        void EnsureStyles()
        {
            if (_title != null)
                return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _slot = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
        }
    }
}
