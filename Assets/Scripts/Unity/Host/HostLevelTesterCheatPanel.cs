using System.Text;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Attributes;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// LevelTester 统一 Development Cheat Tools UI。
    /// 仅服务于 LevelTester 人工验收；不占用 F-Key；不嵌入正式 Gameplay UI。
    /// </summary>
    public sealed class HostLevelTesterCheatPanel : MonoBehaviour
    {
        enum CheatTab
        {
            Time = 0,
            Background = 1,
            Content = 2,
            Diplomacy = 3,
            Snapshot = 4,
            Battle = 5,
            Diagnostics = 6,
        }

        static readonly string[] TabLabels =
        {
            "时间",
            "后台角色",
            "内容",
            "外交",
            "存档",
            "战斗",
            "系统 / 性能",
        };

        const int WindowId = 0x1E7E573;
        const float PanelWidth = 620f;
        const float PanelMinHeight = 480f;
        const float TabRowHeight = 24f;
        const int TabsPerRow = 4;

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] bool visible;
        [SerializeField] KeyCode toggleKey = KeyCode.BackQuote;

        readonly LevelTesterCheatTimeSection _time = new LevelTesterCheatTimeSection();
        readonly LevelTesterCheatBackgroundSection _background = new LevelTesterCheatBackgroundSection();
        readonly LevelTesterCheatContentSection _content = new LevelTesterCheatContentSection();
        readonly LevelTesterCheatDiplomacySection _diplomacy = new LevelTesterCheatDiplomacySection();

        CheatTab _activeTab = CheatTab.Time;
        bool _resetConfirmPending;
        string _sessionStatus = string.Empty;
        string _snapshotStatus = string.Empty;
        string _partyCombatCheatStatus = string.Empty;
        Vector2 _tabScroll;
        Rect _panelRect;
        bool _panelRectInitialized;
        GUIStyle _title;
        GUIStyle _body;

        public bool IsVisible => visible;

        public void Bind(PlayableHostBootstrap host, HostSelectionController selection)
        {
            bootstrap = host;
            selectionController = selection;
        }

        public void ToggleVisible() => visible = !visible;

        public void Show() => visible = true;

        public void Hide() => visible = false;

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                visible = !visible;
        }

        void OnGUI()
        {
            if (!visible)
                return;

            EnsureStyles();
            EnsurePanelRect();
            _panelRect = GUI.Window(WindowId, _panelRect, DrawWindow, "LevelTester 开发工具");
            HostUiHitTest.Block(_panelRect);
        }

        void EnsurePanelRect()
        {
            if (_panelRectInitialized)
                return;
            _panelRect = new Rect(24f, 120f, PanelWidth, PanelMinHeight);
            _panelRectInitialized = true;
        }

        void EnsureStyles()
        {
            if (_body != null)
                return;
            _title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _body = new GUIStyle(GUI.skin.label) { wordWrap = true };
        }

        void DrawWindow(int id)
        {
            GUI.DragWindow(new Rect(0f, 0f, PanelWidth, 22f));

            if (GUI.Button(new Rect(PanelWidth - 72f, 4f, 64f, 20f), "关闭"))
                visible = false;

            const float pad = 8f;
            var innerW = PanelWidth - pad * 2f - 16f;
            var tabBarTop = 26f;
            var tabBarWidth = PanelWidth - pad * 2f;
            var tabW = tabBarWidth / TabsPerRow - 2f;

            for (var i = 0; i < TabLabels.Length; i++)
            {
                var row = i / TabsPerRow;
                var col = i % TabsPerRow;
                var rect = new Rect(
                    pad + col * (tabW + 2f),
                    tabBarTop + row * TabRowHeight,
                    tabW,
                    TabRowHeight - 2f);
                var selected = _activeTab == (CheatTab)i;
                if (GUI.Toggle(rect, selected, TabLabels[i], GUI.skin.button))
                    _activeTab = (CheatTab)i;
            }

            var tabRows = (TabLabels.Length + TabsPerRow - 1) / TabsPerRow;
            var contentTop = tabBarTop + tabRows * TabRowHeight + 4f;
            var viewH = Mathf.Max(120f, _panelRect.height - contentTop - pad);
            var contentH = EstimateActiveTabContentHeight();
            _tabScroll = GUI.BeginScrollView(
                new Rect(pad, contentTop, PanelWidth - pad * 2f, viewH),
                _tabScroll,
                new Rect(0f, 0f, innerW, contentH));

            DrawActiveTab(0f, innerW);
            GUI.EndScrollView();
        }

        float EstimateActiveTabContentHeight()
        {
            switch (_activeTab)
            {
                case CheatTab.Time:
                    return 280f;
                case CheatTab.Background:
                    return 520f;
                case CheatTab.Content:
                    return 480f;
                case CheatTab.Diplomacy:
                    return 420f;
                case CheatTab.Snapshot:
                    return 260f;
                case CheatTab.Battle:
                    return 520f;
                case CheatTab.Diagnostics:
                    return 760f;
                default:
                    return 400f;
            }
        }

        void DrawActiveTab(float x, float width)
        {
            switch (_activeTab)
            {
                case CheatTab.Time:
                    _time.Draw(bootstrap, x, 0f, width, _body);
                    break;
                case CheatTab.Background:
                    _background.Draw(bootstrap, x, 0f, width, _body);
                    break;
                case CheatTab.Content:
                    _content.Draw(bootstrap, selectionController, x, 0f, width, _body);
                    break;
                case CheatTab.Diplomacy:
                    _diplomacy.Draw(bootstrap, x, 0f, width, _body);
                    break;
                case CheatTab.Snapshot:
                    DrawSnapshotTab(x, 0f, width);
                    break;
                case CheatTab.Battle:
                    DrawBattleTab(x, 0f, width);
                    break;
                case CheatTab.Diagnostics:
                    DrawDiagnosticsTab(x, 0f, width);
                    break;
            }
        }

        void DrawSnapshotTab(float x, float y, float width)
        {
            var lineH = 18f;
            GUI.Label(new Rect(x, y, width, lineH),
                "存档 v" + HostLevelTesterSnapshotOps.SchemaVersion + "  " +
                HostLevelTesterSnapshotOps.SlotPath, _body);
            y += lineH + 4f;

            var saved = HostLevelTesterSnapshotSummary.LastSaved;
            var runtime = HostLevelTesterSnapshotSummary.LastRuntime;
            GUI.Label(new Rect(x, y, width, lineH),
                "上次保存: Ch=" + saved.CharacterCount +
                " Party=" + saved.PlayerPartyCount +
                " Squad=" + saved.SquadCount +
                " " + saved.WorldLocation, _body);
            y += lineH;
            GUI.Label(new Rect(x, y, width, lineH),
                "当前 Runtime: Ch=" + runtime.CharacterCount +
                " Party=" + runtime.PlayerPartyCount +
                " Squad=" + runtime.SquadCount +
                " " + runtime.WorldLocation, _body);
            y += lineH;
            if (!string.IsNullOrEmpty(saved.PlayerPartyDetail))
            {
                GUI.Label(new Rect(x, y, width, lineH), saved.PlayerPartyDetail, _body);
                y += lineH;
            }

            if (!string.IsNullOrEmpty(saved.LocalPlacementsDetail))
            {
                GUI.Label(new Rect(x, y, width, lineH), saved.LocalPlacementsDetail, _body);
                y += lineH;
            }

            if (!string.IsNullOrEmpty(runtime.PlayerPartyDetail))
            {
                GUI.Label(new Rect(x, y, width, lineH), runtime.PlayerPartyDetail, _body);
                y += lineH;
            }

            if (!string.IsNullOrEmpty(runtime.LocalPlacementsDetail))
            {
                GUI.Label(new Rect(x, y, width, lineH), runtime.LocalPlacementsDetail, _body);
                y += lineH;
            }

            if (bootstrap?.ViewSpawner != null)
            {
                GUI.Label(new Rect(x, y, width, lineH),
                    "Presented=" + bootstrap.ViewSpawner.SpawnedCount, _body);
                y += lineH;
            }

            if (GUI.Button(new Rect(x, y, width * 0.48f, 24f), "保存存档"))
            {
                var r = HostLevelTesterSnapshotOps.TrySave(bootstrap);
                _snapshotStatus = (r.Success ? "成功：" : "失败：") + r.Message;
            }

            if (GUI.Button(new Rect(x + width * 0.52f, y, width * 0.48f, 24f), "读取存档"))
            {
                var r = HostLevelTesterSnapshotOps.TryLoad(bootstrap);
                _snapshotStatus = (r.Success ? "成功：" : "失败：") + r.Message;
            }

            y += 28f;
            if (!_resetConfirmPending)
            {
                if (GUI.Button(new Rect(x, y, width, 24f), "重置 LevelTester 会话…"))
                    _resetConfirmPending = true;
            }
            else
            {
                GUI.Label(new Rect(x, y, width, lineH * 2f),
                    "将按当前 Inspector 配置重建整个会话。", _body);
                y += lineH * 2f;
                if (GUI.Button(new Rect(x, y, width * 0.48f, 24f), "确认重置"))
                {
                    _resetConfirmPending = false;
                    if (bootstrap != null)
                    {
                        var ok = bootstrap.TryInitialize();
                        _sessionStatus = ok ? "成功：会话已重置。" : "失败：会话重置失败。";
                    }
                    else
                    {
                        _sessionStatus = "失败：未找到 Bootstrap。";
                    }
                }

                if (GUI.Button(new Rect(x + width * 0.52f, y, width * 0.48f, 24f), "取消"))
                    _resetConfirmPending = false;
            }

            y += 28f;
            if (!string.IsNullOrEmpty(_snapshotStatus))
                GUI.Label(new Rect(x, y, width, lineH * 2f), _snapshotStatus, _body);
            if (!string.IsNullOrEmpty(_sessionStatus))
                GUI.Label(new Rect(x, y + lineH * 2f, width, lineH * 2f), _sessionStatus, _body);
        }

        void DrawBattleTab(float x, float y, float width)
        {
            GUI.Label(new Rect(x, y, width, 22f), "我方角色数值作弊", _title);
            y += 26f;
            var buttonWidth = (width - 12f) / 3f;
            if (GUI.Button(new Rect(x, y, buttonWidth, 24f), "我方全员攻击 +10"))
                ApplyPartyAttributeCheat(AttributeId.Attack, 10, refill: false);
            if (GUI.Button(new Rect(x + buttonWidth + 6f, y, buttonWidth, 24f), "最大生命 +50 并回满"))
                ApplyPartyAttributeCheat(AttributeId.MaxHp, 50, refill: true);
            if (GUI.Button(new Rect(x + (buttonWidth + 6f) * 2f, y, buttonWidth, 24f), "生命／灵力回满"))
                RefillPartyCombatPools();
            y += 28f;
            if (GUI.Button(new Rect(x, y, width, 24f), "选中角色进入弥留"))
                ForceSelectedCharacterIncapacitated();
            y += 28f;
            if (GUI.Button(new Rect(x, y, width, 24f), "让所选 NPC 小队移动到主控附近测试点"))
                MoveSelectedNpcSquadNearPlayer();
            y += 28f;
            if (!string.IsNullOrEmpty(_partyCombatCheatStatus))
            {
                GUI.Label(new Rect(x, y, width, 44f), _partyCombatCheatStatus, _body);
                y += 48f;
            }

            var world = bootstrap?.Session?.World;
            if (world != null)
            {
                var encounter = world.Strategic.CharacterEncounter;
                if (encounter != null)
                {
                    GUI.Label(new Rect(x, y, width, 22), "遭遇 " + encounter.EncounterId + " 名单版本 " + encounter.RosterVersion);
                    y += 26;
                    foreach (var candidate in encounter.Candidates)
                    {
                        GUI.Label(new Rect(x, y, width - 130, 22), candidate.SquadId + " " + candidate.Phase + " roll=" + candidate.Roll);
                        if (candidate.Phase == EncounterCandidatePhase.Undecided &&
                            GUI.Button(new Rect(x + width - 125, y, 125, 22), "关系合格则介入"))
                            CharacterEncounterService.DecideCandidate(world, candidate, manualAccept: true);
                        y += 26;
                    }
                }
            }
        }

        void ApplyPartyAttributeCheat(AttributeId attribute, int delta, bool refill)
        {
            var world = bootstrap?.Session?.World;
            var members = bootstrap?.Session?.PlayerParty?.Members;
            var result = attribute == AttributeId.Attack
                ? LevelTesterPartyCombatCheats.AddAttack(world, members)
                : LevelTesterPartyCombatCheats.AddMaxHpAndRefill(world, members);
            _partyCombatCheatStatus = "上次数值作弊：" +
                (attribute == AttributeId.Attack ? "攻击 +10" : "最大生命 +50 并回满") +
                "　成功 " + result.Succeeded + " / 跳过 " + result.Skipped;
        }

        void RefillPartyCombatPools()
        {
            var world = bootstrap?.Session?.World;
            var members = bootstrap?.Session?.PlayerParty?.Members;
            var result = LevelTesterPartyCombatCheats.Refill(world, members);
            _partyCombatCheatStatus = "上次数值作弊：生命／灵力回满　成功 " +
                                      result.Succeeded + " / 跳过 " + result.Skipped;
        }

        void ForceSelectedCharacterIncapacitated()
        {
            var selected = selectionController?.State?.SelectedIds;
            var result = LevelTesterCharacterCombatCheats.TryForceSelectedCharacterIncapacitated(
                bootstrap?.Session?.World,
                selected);
            if (result.Success && selected != null && selected.Count == 1)
                HostSnapshotLocalPlacementCaptureSync.TryCaptureCharacterPlacementFromView(
                    bootstrap?.Session?.World, bootstrap?.ViewSpawner, selected[0]);
            _partyCombatCheatStatus = result.Message;
        }

        void MoveSelectedNpcSquadNearPlayer()
        {
            _partyCombatCheatStatus = LevelTesterNpcSquadMotionCheats.TryMoveSelectedNpcSquadNearPlayer(
                bootstrap?.Session?.World, selectionController?.State?.SelectedIds).Message;
        }

        void DrawDiagnosticsTab(float x, float y, float width)
        {
            var surface = bootstrap?.ContinuousOutdoorSurfaceRuntime;
            GUI.Label(new Rect(x, y, width, 24f), "系统 / 性能", _title);
            y += 30f;
            GUI.Label(new Rect(x, y, width, 22f),
                bootstrap != null ? bootstrap.OutdoorAuthorityDiagnostic : "Authority=Uninitialized", _body);
            y += 24f;
            GUI.Label(new Rect(x, y, width, 56f),
                bootstrap != null ? bootstrap.OpeningPopulationDiagnostic : string.Empty, _body);
            y += 60f;

            var world = bootstrap?.Session?.World;
            var strategic = world?.Strategic;
            GUI.Label(new Rect(x, y, width, 40f),
                "NPC Squad Runtime: Squads=" + (strategic?.Squads?.Squads?.Count ?? 0) +
                "  ActiveNpcSquadWorldMotions=" + CountActiveNpcSquadWorldMotions() + "\n" +
                "Legacy Runtime: RETIRED  Modern AtHex Producers=0  Outdoor LocalMap Active=" +
                (world?.LocalMap != null && !world.LocalMap.IsInInterior &&
                 !string.IsNullOrEmpty(world.LocalMap.ActiveMapLayoutId)), _body);
            y += 44f;

            var mover = bootstrap != null ? bootstrap.NpcScheduleMover : null;
            var perfText =
                // 性能诊断（判断卡顿是否 A* storm／registry rebuild spike）：
                "[Perf] VisibleEntity=" + (bootstrap?.ViewSpawner != null ? bootstrap.ViewSpawner.SpawnedCount : 0) +
                " OutdoorMaterialized=" + (surface != null ? surface.MaterializedOutdoorEntityCount : 0) +
                " MovingNpc=" + (mover != null ? mover.MovingNpcCount : 0) + "\n" +
                "[Perf] NpcPathReq frame=" + (mover != null ? mover.NpcPathRequestsThisFrame : 0) +
                " /sec=" + (mover != null ? mover.NpcPathRequestsLastSecond : 0) +
                " repath/sec=" + (mover != null ? mover.NpcRepathRequestsLastSecond : 0) + "\n" +
                "[Perf] PathBuildMs last=" + (mover != null ? mover.LastNpcPathBuildMs.ToString("0.0") : "0") +
                " max/sec=" + (mover != null ? mover.MaxNpcPathBuildMsLastSecond.ToString("0.0") : "0") + "\n" +
                "[Perf] InteractSpots=" + HostInteractSpots.LoadedSpotCount +
                " Plots=" + HostMapObjectRegistry.AllPlots.Count +
                " PlaceGen=" + (surface != null ? surface.PlaceRefreshGeneration : 0) +
                " EntityGen=" + (surface != null ? surface.EntityReconcileGeneration : 0) +
                " LoadedChunks=" + (surface != null ? surface.LoadedChunkCount : 0) + "\n" +
                "[Startup] Postcondition=" +
                (bootstrap != null && !string.IsNullOrEmpty(bootstrap.ContinuousStartupPostconditionDiagnostic)
                    ? bootstrap.ContinuousStartupPostconditionDiagnostic
                    : "ok") + "\n" +
                (surface != null ? surface.DescribeDiagnostics() : string.Empty);
            GUI.Label(new Rect(x, y, width, 350f), perfText, _body);
        }

        int CountActiveNpcSquadWorldMotions()
        {
            var world = bootstrap?.Session?.World;
            if (world?.Strategic?.SquadWorldMotions == null) return 0;
            var count = 0;
            foreach (var pair in world.Strategic.SquadWorldMotions.Motions)
                if (world.Strategic.Squads.TryGet(pair.Key, out var squad) &&
                    SquadWorldMotionService.IsActiveNpcSquadAuthority(world, squad, pair.Value)) count++;
            return count;
        }

        public const float TopBarEntryY = 8f;
        public const float TopBarEntryW = 72f;
        public const float TopBarEntryH = 32f;
        /// <summary>FormalHud 顶栏：紧挨 20x 右侧（pause@300 + 60 + 4×44 + 4）。</summary>
        public const float TopBarEntryX = 540f;

        public static void DrawTopBarEntryButton(PlayableHostBootstrap bootstrap)
        {
            DrawTopBarEntryButton(bootstrap, TopBarEntryX);
        }

        public static void DrawTopBarEntryButton(PlayableHostBootstrap bootstrap, float x)
        {
            if (bootstrap == null)
                return;

            var btn = new Rect(x, TopBarEntryY, TopBarEntryW, TopBarEntryH);
            HostUiHitTest.Block(btn);
            var cheat = bootstrap.LevelTesterCheatPanel ??
                        bootstrap.GetComponent<HostLevelTesterCheatPanel>();
            var label = cheat != null && cheat.IsVisible ? "关闭作弊" : "作弊工具";
            if (GUI.Button(btn, label) && cheat != null)
                cheat.ToggleVisible();
        }
    }
}
