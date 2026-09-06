using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Construction;
using XianXia.Core.Results;

namespace XianXia.Unity.Host
{
    /// <summary>Global Construction modal; sibling of WorldMap and Inventory panels.</summary>
    public sealed class HostConstructionPanel : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] bool open;

        Vector2 _scroll;
        string _status = string.Empty;
        bool _holdingPause;
        GUIStyle _title;
        GUIStyle _small;

        public bool IsOpen => open;

        public void Bind(PlayableHostBootstrap host) => bootstrap = host;

        public void Toggle()
        {
            if (open)
                Close();
            else
                Open();
        }

        public void Open()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            bootstrap.InventoryPanel?.Close();
            bootstrap.WorldMapPanel?.Close();
            bootstrap.QuestJournal?.Close();
            open = true;
            bootstrap.Session.IsPaused = true;
            _holdingPause = true;
            HostInputGate.BlockWorldCamera = true;
            HostInputGate.BlockWorldInteraction = true;
        }

        public void Close()
        {
            open = false;
            ReleasePauseOwnership();
        }

        public void ClearSessionState()
        {
            open = false;
            _scroll = Vector2.zero;
            _status = string.Empty;
            _holdingPause = false;
        }

        /// <summary>Synchronously release this modal before placement takes world-input ownership.</summary>
        public void CloseForPlacement()
        {
            open = false;
            ReleasePauseOwnership();
        }

        void Update()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;

            if (OtherBlockingModalOpen())
            {
                open = false;
                _holdingPause = false;
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
            }
            else if (_holdingPause)
                ReleasePauseOwnership();
        }

        void ReleasePauseOwnership()
        {
            if (_holdingPause && bootstrap?.Session != null)
                bootstrap.Session.IsPaused = false;
            _holdingPause = false;
            HostInputGate.Clear();
        }

        bool OtherBlockingModalOpen()
        {
            return (bootstrap.InventoryPanel != null && bootstrap.InventoryPanel.IsOpen) ||
                   (bootstrap.WorldMapPanel != null && bootstrap.WorldMapPanel.IsOpen) ||
                   (bootstrap.QuestJournal != null && bootstrap.QuestJournal.IsOpen) ||
                   (bootstrap.ManualLearnPrompt != null && bootstrap.ManualLearnPrompt.IsOpen) ||
                   (bootstrap.CombatArtLearnPrompt != null && bootstrap.CombatArtLearnPrompt.IsOpen);
        }

        void OnGUI()
        {
            if (!open || bootstrap?.Session == null || !bootstrap.Session.IsInitialized)
                return;
            EnsureStyles();

            var world = bootstrap.Session.World;
            var width = Mathf.Min(720f, Screen.width - 40f);
            var height = Mathf.Min(520f, Screen.height - 40f);
            var window = new Rect(
                (Screen.width - width) * .5f,
                (Screen.height - height) * .5f,
                width,
                height);
            HostUiHitTest.Block(window);
            GUI.Box(window, GUIContent.none);
            GUI.Box(window, GUIContent.none);
            GUI.Label(new Rect(window.x + 16f, window.y + 12f, width - 32f, 28f), "建筑", _title);

            var specs = new List<BuildingConstructionSpec>();
            foreach (var pair in world.ConstructionCatalog.Buildings)
                if (pair.Value != null && pair.Value.UnlockedByDefault)
                    specs.Add(pair.Value);
            specs.Sort((a, b) => string.CompareOrdinal(a.BuildingId, b.BuildingId));

            var view = new Rect(window.x + 12f, window.y + 50f, width - 24f, height - 106f);
            var contentHeight = Mathf.Max(view.height, specs.Count * 168f + 8f);
            _scroll = GUI.BeginScrollView(
                view, _scroll, new Rect(0f, 0f, view.width - 18f, contentHeight));
            if (specs.Count == 0)
                GUI.Label(new Rect(10f, 8f, view.width - 30f, 30f), "暂无已解锁建筑。", _small);

            for (var i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                var card = new Rect(8f, i * 168f + 4f, view.width - 34f, 156f);
                GUI.Box(card, GUIContent.none);
                GUI.Label(new Rect(card.x + 12f, card.y + 8f, card.width - 130f, 24f),
                    spec.DisplayName, _title);
                GUI.Label(new Rect(card.x + 12f, card.y + 34f, card.width - 24f, 30f),
                    spec.Description, _small);

                var materialY = card.y + 67f;
                for (var c = 0; c < spec.Costs.Count; c++)
                {
                    var cost = spec.Costs[c];
                    var have = world.Inventory.GetCount(cost.ItemId);
                    GUI.Label(new Rect(card.x + 12f, materialY, card.width - 150f, 20f),
                        world.InventoryCatalog.GetName(cost.ItemId) + "  " + have + " / " + cost.Count +
                        (have >= cost.Count ? "  ✓" : "  ✕"), _small);
                    materialY += 20f;
                }
                GUI.Label(new Rect(card.x + 12f, card.yMax - 42f, card.width - 140f, 22f),
                    "拆除返还：" + Mathf.FloorToInt(spec.DismantleRefundRate * 100f) + "%", _small);

                var canBuild = ConstructionService.HasRequiredMaterials(world, spec, out _);
                GUI.enabled = canBuild;
                if (GUI.Button(new Rect(card.xMax - 116f, card.yMax - 48f, 100f, 34f),
                        canBuild ? "建造" : "材料不足"))
                {
                    CloseForPlacement();
                    var controller = bootstrap.GetComponent<HostConstructionController>();
                    var result = controller != null
                        ? controller.BeginPlacement(spec.BuildingId)
                        : Result.Failure(ErrorCode.InvalidOperation, "建造控制器未就绪。");
                    if (result.IsFailure)
                    {
                        _status = result.Error.Message;
                        Open();
                    }
                }
                GUI.enabled = true;
            }
            GUI.EndScrollView();

            if (!string.IsNullOrEmpty(_status))
                GUI.Label(new Rect(window.x + 16f, window.yMax - 48f, width - 120f, 28f),
                    _status, _small);
            if (GUI.Button(new Rect(window.xMax - 92f, window.yMax - 48f, 76f, 32f), "关闭"))
                Close();
        }

        void EnsureStyles()
        {
            if (_title != null)
                return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
        }
    }
}
