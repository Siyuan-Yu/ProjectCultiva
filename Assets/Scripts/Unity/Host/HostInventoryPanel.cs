using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Inventory;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Shared party bag UI (IMGUI): slots, category filter, one-click organize, 功法秘籍使用.
    /// </summary>
    public sealed class HostInventoryPanel : MonoBehaviour
    {
        const string PauseOwner = "InventoryPanel";

        enum Filter
        {
            All = 0,
            Resource = 1,
            Consumable = 2,
            Other = 3
        }

        enum ViewMode
        {
            Party = 0,
            Warehouse = 1
        }

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] KeyCode toggleKey = KeyCode.B;
        [SerializeField] bool open;

        Filter _filter = Filter.All;
        ViewMode _viewMode = ViewMode.Party;
        Vector2 _scroll;
        string _status = string.Empty;
        bool _holdingPause;
        int _selectedSlot = -1;
        string _selectedWarehouseResourceId = string.Empty;
        readonly List<AccessibleStrategicStock> _warehouseStock = new List<AccessibleStrategicStock>();

        GUIStyle _title;
        GUIStyle _small;
        GUIStyle _slot;

        public bool IsOpen => open;

        public void Toggle()
        {
            if (open)
                Close();
            else
                Open();
        }

        public void Open()
        {
            bootstrap?.ConstructionPanel?.Close();
            bootstrap?.WorldMapPanel?.Close();
            open = true;
            _scroll = Vector2.zero;
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized ||
                !PlayerStrategicResourceService.CanAccessSiteStorageNetwork(bootstrap.Session.World))
                _viewMode = ViewMode.Party;
            if (bootstrap?.Session != null && bootstrap.Session.IsInitialized)
            {
                bootstrap.Session.AcquireModalPause(PauseOwner);
                _holdingPause = true;
                HostInputGate.BlockWorldCamera = true;
                HostInputGate.BlockWorldInteraction = true;
            }
        }

        public void Close()
        {
            open = false;
            ReleasePauseOwnership();
        }

        public void Bind(PlayableHostBootstrap host)
        {
            bootstrap = host;
        }

        public void ClearSessionState()
        {
            ReleasePauseOwnership();
            open = false;
            _selectedSlot = -1;
            _selectedWarehouseResourceId = string.Empty;
            _viewMode = ViewMode.Party;
            _status = string.Empty;
        }

        void Update()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;

            if (Input.GetKeyDown(toggleKey))
                Toggle();

            var journal = bootstrap.QuestJournal;
            var learn = bootstrap.ManualLearnPrompt;
            var construction = bootstrap.ConstructionPanel;
            var map = bootstrap.WorldMapPanel;
            if ((journal != null && journal.IsOpen) ||
                (learn != null && learn.IsOpen) ||
                (construction != null && construction.IsOpen) ||
                (map != null && map.IsOpen))
            {
                if (open)
                    open = false;
                if (_holdingPause)
                    ReleasePauseOwnership();
                return;
            }

            if (open)
            {
                HostInputGate.BlockWorldCamera = true;
                HostInputGate.BlockWorldInteraction = true;
                if (!_holdingPause)
                {
                    bootstrap.Session.AcquireModalPause(PauseOwner);
                    _holdingPause = true;
                }
            }
            else if (_holdingPause)
                ReleasePauseOwnership();
        }

        void OnDisable() => ReleasePauseOwnership();

        void ReleasePauseOwnership()
        {
            if (_holdingPause && bootstrap?.Session != null)
                bootstrap.Session.ReleaseModalPause(PauseOwner);
            _holdingPause = false;
            HostInputGate.Clear();
        }

        void OnGUI()
        {
            if (!open || bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            EnsureStyles();

            var world = bootstrap.Session.World;
            var inv = world.Inventory;
            var catalog = world.InventoryCatalog;
            var canAccessWarehouse = PlayerStrategicResourceService.CanAccessSiteStorageNetwork(world);
            if (_viewMode == ViewMode.Warehouse && !canAccessWarehouse)
            {
                _viewMode = ViewMode.Party;
                _selectedWarehouseResourceId = string.Empty;
                _status = "仅在己方实际控制范围内可访问势力仓库。";
            }
            var w = Mathf.Min(720f, Screen.width - 40f);
            var h = Mathf.Min(560f, Screen.height - 40f);
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(rect, GUIContent.none);
            GUI.Box(rect, GUIContent.none);

            var x = rect.x + 16f;
            var y = rect.y + 12f;
            GUI.Label(
                new Rect(x, y, w - 32f, 28f),
                (_viewMode == ViewMode.Party
                    ? "小队背包  " + inv.UsedSlotCount + "/" + inv.SlotCapacity
                    : "势力仓库 · 战略资源") + "  （B 关闭）",
                _title);
            y += 32f;

            DrawViewTabs(x, y, w, canAccessWarehouse);
            y += 36f;

            if (_viewMode == ViewMode.Warehouse)
            {
                DrawWarehouse(world, rect, x, y, w);
                return;
            }

            DrawFilters(x, y);
            y += 36f;

            if (GUI.Button(new Rect(x, y, 100f, 28f), "一键整理"))
            {
                inv.Organize();
                _status = "已整理";
                _selectedSlot = -1;
            }

            if (GUI.Button(new Rect(x + 110f, y, 72f, 28f), "关闭"))
                Close();
            y += 36f;

            var gridTop = y;
            var gridH = rect.yMax - gridTop - 100f;
            var view = new Rect(rect.x + 12f, gridTop, w - 24f, gridH);
            const int cols = 5;
            const float cell = 88f;
            const float gap = 8f;

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

            var detailY = rect.yMax - 88f;
            var detail = "选中格子查看详情";
            var canUseManual = false;
            var canUseArt = false;
            string selectedItemId = null;
            if (_selectedSlot >= 0 && _selectedSlot < inv.Slots.Count)
            {
                var s = inv.Slots[_selectedSlot];
                if (s.IsEmpty)
                {
                    detail = "空槽位";
                }
                else
                {
                    selectedItemId = s.ItemId;
                    detail = catalog.GetName(s.ItemId) + " ×" + s.Count +
                             "  堆叠上限 " + catalog.GetMaxStack(s.ItemId);
                    if (catalog.IsManualTome(s.ItemId))
                    {
                        canUseManual = true;
                        detail += "\n" + FormatManualDetail(bootstrap.Session.World, catalog.GetTeachesManualId(s.ItemId));
                    }
                    else if (catalog.IsCombatArtTome(s.ItemId))
                    {
                        canUseArt = true;
                        detail += "\n斗技：" + catalog.GetTeachesArtId(s.ItemId);
                    }
                    else
                        detail += "\n" + s.ItemId;
                }
            }

            if (!string.IsNullOrEmpty(_status))
                detail = _status + " ｜ " + detail;
            GUI.Label(new Rect(rect.x + 16f, detailY, w - 140f, 72f), detail, _small);

            if (canUseManual && selectedItemId != null)
            {
                if (GUI.Button(new Rect(rect.xMax - 120f, detailY + 8f, 100f, 32f), "使用"))
                {
                    var prompt = bootstrap.ManualLearnPrompt;
                    if (prompt != null)
                    {
                        Close();
                        prompt.Open(selectedItemId);
                    }
                    else
                        _status = "学功法面板未就绪";
                }
            }
            else if (canUseArt && selectedItemId != null)
            {
                if (GUI.Button(new Rect(rect.xMax - 120f, detailY + 8f, 100f, 32f), "使用"))
                {
                    var prompt = bootstrap.CombatArtLearnPrompt;
                    if (prompt != null)
                    {
                        Close();
                        prompt.Open(selectedItemId);
                    }
                    else
                        _status = "学斗技面板未就绪";
                }
            }
        }

        void DrawViewTabs(float x, float y, float width, bool canAccessWarehouse)
        {
            if (_viewMode == ViewMode.Party)
                GUI.color = new Color(0.95f, 0.85f, 0.55f);
            if (GUI.Button(new Rect(x, y, 100f, 28f), "小队背包"))
            {
                _viewMode = ViewMode.Party;
                _scroll = Vector2.zero;
                _status = string.Empty;
            }
            GUI.color = Color.white;

            var enabled = GUI.enabled;
            GUI.enabled = enabled && canAccessWarehouse;
            if (_viewMode == ViewMode.Warehouse)
                GUI.color = new Color(0.95f, 0.85f, 0.55f);
            if (GUI.Button(new Rect(x + 110f, y, 100f, 28f), "势力仓库"))
            {
                _viewMode = ViewMode.Warehouse;
                _selectedWarehouseResourceId = string.Empty;
                _scroll = Vector2.zero;
                _status = string.Empty;
            }
            GUI.color = Color.white;
            GUI.enabled = enabled;
            if (!canAccessWarehouse)
                GUI.Label(new Rect(x + 220f, y + 3f, width - 236f, 24f),
                    "仅在己方实际控制范围内可访问势力仓库。", _small);
        }

        void DrawWarehouse(
            XianXia.Core.Simulation.SimulationWorld world,
            Rect rect,
            float x,
            float y,
            float width)
        {
            PlayerStrategicResourceService.CollectAccessibleStorageStock(world, _warehouseStock);
            if (GUI.Button(new Rect(x, y, 72f, 28f), "关闭"))
                Close();
            y += 36f;

            var gridH = rect.yMax - y - 100f;
            var view = new Rect(rect.x + 12f, y, width - 24f, gridH);
            const int cols = 5;
            const float cell = 88f;
            const float gap = 8f;
            var rows = Mathf.Max(1, (_warehouseStock.Count + cols - 1) / cols);
            var contentH = rows * (cell + gap) + 8f;
            _scroll = GUI.BeginScrollView(view, _scroll,
                new Rect(0, 0, view.width - 18f, contentH));
            for (var i = 0; i < _warehouseStock.Count; i++)
            {
                var stock = _warehouseStock[i];
                var col = i % cols;
                var row = i / cols;
                var r = new Rect(col * (cell + gap), row * (cell + gap), cell, cell);
                if (string.Equals(_selectedWarehouseResourceId, stock.ResourceId, StringComparison.Ordinal))
                    GUI.color = new Color(1f, 0.92f, 0.7f);
                if (GUI.Button(r, ResourceLabel(world.InventoryCatalog, stock), _slot))
                    _selectedWarehouseResourceId = stock.ResourceId;
                GUI.color = Color.white;
            }
            GUI.EndScrollView();

            var selected = _warehouseStock.Find(s =>
                string.Equals(s.ResourceId, _selectedWarehouseResourceId, StringComparison.Ordinal));
            var detailY = rect.yMax - 88f;
            var detail = _warehouseStock.Count == 0
                ? "当前可访问的势力仓库没有战略资源。"
                : "选择资源后可取到小队背包。";
            if (selected != null)
                detail = world.InventoryCatalog.GetName(selected.ResourceId) + " ×" + selected.Amount;
            if (!string.IsNullOrEmpty(_status))
                detail = _status + " ｜ " + detail;
            GUI.Label(new Rect(rect.x + 16f, detailY, width - 240f, 72f), detail, _small);

            if (selected == null) return;
            if (GUI.Button(new Rect(rect.xMax - 220f, detailY + 8f, 92f, 32f), "取出 1"))
                Withdraw(world, selected.ResourceId, 1);
            if (GUI.Button(new Rect(rect.xMax - 120f, detailY + 8f, 100f, 32f), "取出尽量多"))
            {
                var amount = Mathf.Min(selected.Amount,
                    world.Inventory.GetAddCapacity(selected.ResourceId));
                if (amount <= 0)
                    _status = selected.Amount <= 0 ? "仓库无库存。" : "背包空间不足。";
                else
                    Withdraw(world, selected.ResourceId, amount);
            }
        }

        void Withdraw(
            XianXia.Core.Simulation.SimulationWorld world,
            string resourceId,
            int amount)
        {
            var result = PlayerStrategicResourceService.TryWithdrawToPartyInventory(
                world, resourceId, amount);
            _status = result.IsSuccess
                ? "已取出 " + world.InventoryCatalog.GetName(resourceId) + " ×" + amount
                : result.Error.Message;
        }

        static string ResourceLabel(InventoryCatalog catalog, AccessibleStrategicStock stock)
        {
            var name = catalog.GetName(stock.ResourceId);
            if (name.Length > 6) name = name.Substring(0, 6);
            return name + "\n×" + stock.Amount;
        }

        static string FormatManualDetail(XianXia.Core.Simulation.SimulationWorld world, string manualId)
        {
            if (string.IsNullOrEmpty(manualId) ||
                !DefinitionId.TryParse(manualId, out var mid) ||
                !world.TryGetManual(mid, out var manual) ||
                manual == null)
                return "功法秘籍（数据缺失）";

            var name = string.IsNullOrEmpty(manual.Name) ? manualId : manual.Name;
            var grade = string.IsNullOrEmpty(manual.Grade) ? "品阶未标" : manual.Grade;
            var effect = string.IsNullOrEmpty(manual.EffectSummary)
                ? "打坐每 5 游戏分 +" + manual.CultivationSpeed + " 修为"
                : manual.EffectSummary;
            return "秘籍 → " + name + "（" + grade + "）\n" + effect;
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
                    return catalog.HasTag(slot.ItemId, "consumable") ||
                           catalog.IsManualTome(slot.ItemId) ||
                           catalog.IsCombatArtTome(slot.ItemId);
                case Filter.Other:
                    return !catalog.HasTag(slot.ItemId, "resource") &&
                           !catalog.HasTag(slot.ItemId, "consumable") &&
                           !catalog.IsManualTome(slot.ItemId) &&
                           !catalog.IsCombatArtTome(slot.ItemId);
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
