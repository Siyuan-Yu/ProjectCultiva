using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Hex;
using XianXia.Core.World;

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
            FormalArmy = 2,
            Content = 3,
            Diplomacy = 4,
            Snapshot = 5,
            Battle = 6,
            Diagnostics = 7,
        }

        static readonly string[] TabLabels =
        {
            "时间",
            "后台角色",
            "正规军",
            "内容",
            "外交",
            "存档",
            "战斗",
            "诊断",
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
        readonly LevelTesterCheatFormalArmySection _formalArmy = new LevelTesterCheatFormalArmySection();
        readonly LevelTesterCheatContentSection _content = new LevelTesterCheatContentSection();
        readonly LevelTesterCheatDiplomacySection _diplomacy = new LevelTesterCheatDiplomacySection();

        CheatTab _activeTab = CheatTab.Time;
        bool _resetConfirmPending;
        string _sessionStatus = string.Empty;
        string _snapshotStatus = string.Empty;
        string _battleStatus = string.Empty;
        string _diagnosticStatus = string.Empty;
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
                case CheatTab.FormalArmy:
                    return 960f;
                case CheatTab.Content:
                    return 480f;
                case CheatTab.Diplomacy:
                    return 420f;
                case CheatTab.Snapshot:
                    return 260f;
                case CheatTab.Battle:
                    return 260f;
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
                case CheatTab.FormalArmy:
                    _formalArmy.Draw(bootstrap, x, 0f, width, _body);
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
                " Army=" + saved.FormalArmyCount +
                " " + saved.WorldLocation, _body);
            y += lineH;
            GUI.Label(new Rect(x, y, width, lineH),
                "当前 Runtime: Ch=" + runtime.CharacterCount +
                " Party=" + runtime.PlayerPartyCount +
                " Army=" + runtime.FormalArmyCount +
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
            var forceSolo = AutoBattleCasualtyService.DebugForceSoloAutoBattleIncapacitated;
            var next = GUI.Toggle(
                new Rect(x, y, width, 22f),
                forceSolo,
                "调试：下次单人自动战斗必定失能");
            if (next != forceSolo)
                AutoBattleCasualtyService.DebugForceSoloAutoBattleIncapacitated = next;
            y += 28f;

            var showBattleHexOverlay = BattleEngagementWorldMapDebug.ShowOverlay;
            var nextOverlay = GUI.Toggle(
                new Rect(x, y, width, 22f),
                showBattleHexOverlay,
                "调试：WorldMap 高亮 BattleArea(橙) / SupportArea(蓝)");
            if (nextOverlay != showBattleHexOverlay)
                BattleEngagementWorldMapDebug.ShowOverlay = nextOverlay;
            y += 28f;

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
                var party = bootstrap.Session.PlayerParty;
                if (GUI.Button(new Rect(x, y, width, 24f), "CW-02：当前主控进入弥留") &&
                    party != null && party.HasActive &&
                    world.Entities.TryGet(party.ActiveCharacterId, out var active))
                {
                    var id = party.ActiveCharacterId;
                    var changed = CombatLifeStateService.TryEnterIncapacitated(world, active);
                    bootstrap.PlayerPartyController?.RefreshActiveControlAfterLifeStateChange();
                    _battleStatus = changed
                        ? "已使 " + id.Value + " 进入弥留；应按 Party 固定顺序接替。"
                        : "当前主控无法进入弥留。";
                }
                y += 28f;

                if (GUI.Button(new Rect(x, y, width, 24f), "CW-02：全队进入弥留") && party != null)
                {
                    var changed = 0;
                    for (var i = 0; i < party.Members.Count; i++)
                        if (world.Entities.TryGet(party.Members[i], out var member) &&
                            CombatLifeStateService.TryEnterIncapacitated(world, member))
                            changed++;
                    bootstrap.PlayerPartyController?.RefreshActiveControlAfterLifeStateChange();
                    _battleStatus = "已使 " + changed + " 名队员进入弥留；ControlState=" + party.ControlState;
                }
                y += 28f;

                if (GUI.Button(new Rect(x, y, width, 24f), "CW-02：恢复首位弥留队员") && party != null)
                {
                    var recovered = XianXia.Core.Domain.Ids.EntityId.None;
                    for (var i = 0; i < party.Members.Count; i++)
                    {
                        if (!world.Entities.TryGet(party.Members[i], out var member) ||
                            !CombatLifeStateService.TryRecoverFromIncapacitated(world, member))
                            continue;
                        recovered = party.Members[i];
                        break;
                    }
                    bootstrap.PlayerPartyController?.RefreshActiveControlAfterLifeStateChange();
                    _battleStatus = recovered.IsNone
                        ? "没有可恢复的弥留队员。"
                        : "已恢复 " + recovered.Value + "；Active=" + party.ActiveCharacterId.Value;
                }
                y += 30f;

                if (!string.IsNullOrEmpty(_battleStatus))
                {
                    GUI.Label(new Rect(x, y, width, 42f), _battleStatus, _body);
                    y += 46f;
                }

                var summary = BattleEngagementAuthorityDebug.BuildSummary(world);
                GUI.Label(new Rect(x, y, width, 360f), summary, _body);
            }
        }

        void DrawDiagnosticsTab(float x, float y, float width)
        {
            var strongSep = HostHexWorldRenderer.DebugStrongHexSeparation;
            var nextSep = GUI.Toggle(new Rect(x, y, width, 22f), strongSep,
                "调试：强化 Hex 分离（仅渲染）");
            if (nextSep != strongSep)
                HostHexWorldRenderer.DebugStrongHexSeparation = nextSep;
            y += 30f;
            HostWorldMapPanel.DebugShowW1CCoverage = GUI.Toggle(new Rect(x, y, width, 22f),
                HostWorldMapPanel.DebugShowW1CCoverage, "调试：WorldMap 显示 W1C authored coverage 外框");
            y += 30f;
            var surface = bootstrap?.ContinuousOutdoorSurfaceRuntime;
            var motion = bootstrap?.Session?.World?.PlayerPartyTravel;
            var wildernessOnly = motion != null &&
                                 motion.LocationKind == PlayerPartyLocationKind.AtWorldPosition &&
                                 bootstrap?.Session?.World?.LocalMap?.IsInInterior != true;

            // §18：只读显示真实 presentation authority —— Main Continuous Surface 不再是 W1C。
            GUI.Label(new Rect(x, y, width, 22f),
                bootstrap != null ? bootstrap.OutdoorAuthorityDiagnostic : "Authority=Uninitialized", _body);
            y += 24f;
            GUI.Label(new Rect(x, y, width, 56f),
                bootstrap != null ? bootstrap.OpeningPopulationDiagnostic : string.Empty, _body);
            y += 60f;

            GUI.enabled = bootstrap?.Session?.IsInitialized == true;
            if (GUI.Button(new Rect(x, y, width, 26f), "复制 CW-04 Preset Site/Core/Claim 诊断（只读）"))
            {
                GUIUtility.systemCopyBuffer = BuildPresetSiteControlDiagnostic();
                _diagnosticStatus = "已复制全部 Main Surface preset Site/Core/Claim/Overlay 诊断。";
            }
            GUI.enabled = true;
            y += 30f;

            var selectedArmyId = bootstrap?.WorldMapPanel?.SelectedFormalArmyIdForDiagnostics;
            var diagnosticTarget = string.IsNullOrEmpty(selectedArmyId)
                ? ArmyStackAdapter.BanditWeakPatrolFormalArmyId + "（默认）"
                : selectedArmyId + "（大地图当前选中）";
            GUI.Label(new Rect(x, y, width, 22f), "军队显示诊断目标：" + diagnosticTarget, _body);
            y += 24f;
            GUI.enabled = surface != null && bootstrap?.Session?.IsInitialized == true;
            if (GUI.Button(new Rect(x, y, width, 26f), "复制军队显示诊断（只读）"))
            {
                var report = surface.DescribeFormalArmyDisplayDiagnostics(selectedArmyId);
                GUIUtility.systemCopyBuffer = report;
                _diagnosticStatus = "已复制完整诊断到剪贴板（" + diagnosticTarget + "）。";
            }
            GUI.enabled = true;
            y += 30f;
            if (!string.IsNullOrEmpty(_diagnosticStatus))
            {
                GUI.Label(new Rect(x, y, width, 38f), _diagnosticStatus, _body);
                y += 42f;
            }

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
            y += 356f;

            // §16/§17：Normal NewGame 已直进 Main Continuous Surface；W1C Acceptance teleport 不再是
            // 制作人入口，只保留为 legacy regression 工具（默认收起）。
            var showLegacy = bootstrap != null && bootstrap.ShowLegacyAcceptanceTools;
            var nextLegacy = GUI.Toggle(new Rect(x, y, width, 22f), showLegacy,
                "Regression / Legacy Acceptance（非正常入口，默认收起）");
            if (bootstrap != null)
                bootstrap.ShowLegacyAcceptanceTools = nextLegacy;
            y += 26f;
            if (!nextLegacy)
                return;

            GUI.enabled = wildernessOnly;
            if (GUI.Button(new Rect(x, y, width, 26f),
                    "传送：Continuous World W1C Acceptance（legacy regression）") && wildernessOnly)
            {
                var world = bootstrap?.Session?.World;
                if (surface != null && world?.PlayerPartyTravel != null &&
                    surface.TryGetAcceptanceStartWorldPosition(out var wx, out var wy))
                {
                    var size = world.HexWorld != null && world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
                    var position = new WorldVec2(wx, wy);
                    var hex = HexMath.WorldToHex(wx, wy, size);
                    world.PlayerPartyTravel.SetAtWorldPosition(position, hex);
                    world.PlayerPartyTravel.SetCurrentOutdoorWorldSiteContext(
                        WorldSiteAdministrativeControlResolver.TryResolveOnRegisteredSurface(
                            world, position.X, position.Y, out _, out var site, out _)
                            ? site.SiteId
                            : string.Empty);
                    var party = bootstrap.Session.PlayerParty;
                    if (party != null)
                        for (var i = 0; i < party.Members.Count; i++)
                            world.WorldPresence.SetAtWorldPosition(party.Members[i], position, hex);
                    world.PartyWorld.ClearSiteFocus();
                    world.PartyWorld.Mode = PartyWorldPresenceMode.AtWorldPosition;
                    world.PartyWorld.SiteId = string.Empty;
                    world.PartyWorld.LocalMapId = string.Empty;
                    surface.TryActivateAcceptanceAtCurrentWorldPosition();
                    bootstrap.ActivateSurfaceLocalMapPresentation();
                    bootstrap.FrameCameraOnActiveCharacter();
                }
            }
            GUI.enabled = true;
            y += 30f;
            if (!wildernessOnly)
                GUI.Label(new Rect(x, y, width, 20f),
                    "W1C Acceptance is wilderness-only; exit WorldSite/Interior first.", _body);
        }

        string BuildPresetSiteControlDiagnostic()
        {
            var world = bootstrap?.Session?.World;
            var registry = bootstrap?.Session?.Registry;
            var sb = new StringBuilder(2048);
            sb.AppendLine("[CW04PresetSiteControlDiagnostic]");
            if (world?.Strategic?.Sites == null || registry == null)
                return sb.Append("Unavailable").ToString();
            var overlays = WorldSiteActualControlOverlayBuilder.Build(world);
            foreach (var pair in registry.OutdoorSurfaces)
            {
                var surface = pair.Value;
                if (surface == null || surface.AcceptanceOnly || surface.SiteRegions == null) continue;
                for (var i = 0; i < surface.SiteRegions.Count; i++)
                {
                    var region = surface.SiteRegions[i];
                    if (region == null || !world.Strategic.Sites.TryGet(region.SiteId, out var site) || site == null)
                    {
                        sb.AppendLine("SiteId=" + (region?.SiteId ?? "missing") + " RuntimeSite=Missing");
                        continue;
                    }
                    var claimCount = 0;
                    foreach (var ignored in world.Strategic.TerritoryClaims.EnumerateForSite(site.SiteId)) claimCount++;
                    WorldSiteActualControlOverlay overlay = null;
                    for (var o = 0; o < overlays.Count; o++)
                        if (string.Equals(overlays[o].SiteId, site.SiteId, StringComparison.Ordinal) &&
                            string.Equals(overlays[o].SurfaceId, surface.SurfaceId, StringComparison.Ordinal))
                        { overlay = overlays[o]; break; }
                    CoreLevelControlRange configured = null;
                    if (site.HasContinuousCore)
                        try { configured = world.Strategic.SpatialRules?.RequireLevel(site.CoreLevel); }
                        catch (InvalidOperationException) { }
                    var strategicCount = 0;
                    foreach (var territory in world.Strategic.TerritoryRegions.Regions)
                        if (territory.Value != null &&
                            string.Equals(territory.Value.PrimaryWorldSiteId, site.SiteId, StringComparison.Ordinal))
                            strategicCount += territory.Value.HexCount;
                    sb.Append("SiteId=").Append(site.SiteId)
                        .Append(" Name=").Append(site.DisplayName)
                        .Append(" Owner=").Append(string.IsNullOrEmpty(site.OwnerFactionId) ? "none" : site.OwnerFactionId)
                        .Append(" Type=").Append(site.SiteType)
                        .Append(" HasContinuousCore=").Append(site.HasContinuousCore)
                        .Append(" CoreAsset=").Append(string.IsNullOrEmpty(site.CoreAssetId) ? "none" : site.CoreAssetId)
                        .Append(" CoreWorld=").Append(site.HasCoreWorldPosition
                            ? "(" + site.CoreWorldX.ToString("0.###") + "," + site.CoreWorldY.ToString("0.###") + ")"
                            : "none")
                        .Append(" Level=").Append(site.CoreLevel)
                        .Append(" Configured=").Append(configured?.WidthCells.ToString("0.#") ?? "none")
                        .Append('x').Append(configured?.HeightCells.ToString("0.#") ?? "none").Append("cells")
                        .Append(" Resolved=").Append(site.CoreRangeWidth.ToString("0.###"))
                        .Append('x').Append(site.CoreRangeHeight.ToString("0.###")).Append("world")
                        .Append(" Claims=").Append(claimCount)
                        .Append(" ActualOverlayPieces=").Append(overlay?.Pieces.Count ?? 0)
                        .Append(" ActualBounds=").Append(overlay == null ? "none" :
                            "(" + overlay.MinX.ToString("0.###") + "," + overlay.MinY.ToString("0.###") + ")..(" +
                            overlay.MaxX.ToString("0.###") + "," + overlay.MaxY.ToString("0.###") + ")")
                        .Append(" StrategicHexSummaryCount=").Append(strategicCount)
                        .Append(" MigrationSource=").Append(site.HasContinuousCore
                            ? (world.Strategic.FactionFlags.Flags.ContainsKey(site.CoreAssetId)
                                ? "AuthoredFlagCore/PlayerBuilt" : "PresetCouncilHall")
                            : "None")
                        .AppendLine();
                }
            }

            sb.AppendLine("[WorldMapCoreMarkers]");
            foreach (var pair in world.Strategic.Sites.Sites)
            {
                var site = pair.Value;
                if (site == null || !site.UsesContinuousOutdoorSurface || !site.IsCoreActive)
                    continue;
                var kind = WorldSitePresentationLayer.ResolveMarkerKind(world, site);
                var hasMarkerPosition = WorldSitePresentationLayer.TryResolveMarkerWorldPosition(
                    world, site, out var markerX, out var markerY);
                var pieces = 0;
                for (var i = 0; i < overlays.Count; i++)
                    if (string.Equals(overlays[i].SiteId, site.SiteId, StringComparison.Ordinal))
                        pieces += overlays[i].Pieces.Count;
                sb.Append("SiteId=").Append(site.SiteId)
                    .Append(" MarkerKind=").Append(kind)
                    .Append(" MarkerWorldPosition=").Append(hasMarkerPosition
                        ? "(" + markerX.ToString("0.###") + "," + markerY.ToString("0.###") + ")"
                        : "none")
                    .Append(" ActualOverlayPieces=").Append(pieces)
                    .Append(" LegacyFlagMarkerAlsoVisible=False")
                    .AppendLine();
            }

            sb.AppendLine("[FactionFlags]");
            foreach (var pair in world.Strategic.FactionFlags.Flags)
            {
                var flag = pair.Value;
                if (flag == null) continue;
                var claimCount = 0;
                var overlayPieces = 0;
                var manager = "none";
                if (!string.IsNullOrWhiteSpace(flag.SiteId))
                {
                    foreach (var ignored in world.Strategic.TerritoryClaims.EnumerateForSite(flag.SiteId))
                        claimCount++;
                    for (var i = 0; i < overlays.Count; i++)
                        if (string.Equals(overlays[i].SiteId, flag.SiteId, StringComparison.Ordinal))
                            overlayPieces += overlays[i].Pieces.Count;
                    if (world.Strategic.Sites.TryGet(flag.SiteId, out var coreSite) && coreSite != null &&
                        WorldSiteAdministrativeControlResolver.TryResolve(
                            world, coreSite.CoreSurfaceId, coreSite.CoreWorldX, coreSite.CoreWorldY,
                            out var managedBy, out _))
                        manager = managedBy?.SiteId ?? "none";
                }
                var source = flag.IsAuthoredSiteCore ? "AuthoredFlagCore" :
                    flag.IsSiteCore ? "PlayerBuilt" : "LegacyOnly";
                var productMarkerVisible = false;
                if (flag.IsSiteCore && !string.IsNullOrWhiteSpace(flag.SiteId) &&
                    world.Strategic.Sites.TryGet(flag.SiteId, out var markerSite) &&
                    markerSite != null && markerSite.IsCoreActive)
                    productMarkerVisible = FactionFlagSiteCoreQuery.TryResolveFlagForSite(
                        world, markerSite, out var markerFlag) && ReferenceEquals(markerFlag, flag);
                sb.Append("FlagId=").Append(flag.FlagId)
                    .Append(" Faction=").Append(flag.FactionId)
                    .Append(" ContentKind=").Append(flag.IsAuthoredSiteCore ? "AuthoredSiteCore" : "Legacy")
                    .Append(" IsAuthoredSiteCore=").Append(flag.IsAuthoredSiteCore)
                    .Append(" LegacyDebugOnly=").Append(flag.IsWorldMapDebugOnly)
                    .Append(" AnchorHex=(").Append(flag.AnchorHex.Q).Append(',').Append(flag.AnchorHex.R).Append(')')
                    .Append(" HasWorldPosition=").Append(flag.HasWorldPosition)
                    .Append(" SurfaceId=").Append(string.IsNullOrWhiteSpace(flag.SurfaceId) ? "none" : flag.SurfaceId)
                    .Append(" WorldPosition=").Append(flag.HasWorldPosition
                        ? "(" + flag.WorldX.ToString("0.###") + "," + flag.WorldY.ToString("0.###") + ")"
                        : "none")
                    .Append(" SiteId=").Append(string.IsNullOrWhiteSpace(flag.SiteId) ? "none" : flag.SiteId)
                    .Append(" IsSiteCore=").Append(flag.IsSiteCore)
                    .Append(" ClaimCount=").Append(claimCount)
                    .Append(" ActualOverlayPieces=").Append(overlayPieces)
                    .Append(" CoreCenterActualManager=").Append(manager)
                    .Append(" MigrationSource=").Append(source)
                    .Append(" WorldMapProductMarkerVisible=").Append(productMarkerVisible)
                    .AppendLine();
            }

            var factions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in world.Strategic.Sites.Sites)
                if (pair.Value != null && !string.IsNullOrWhiteSpace(pair.Value.OwnerFactionId))
                    factions.Add(pair.Value.OwnerFactionId);
            foreach (var pair in world.Strategic.FactionFlags.Flags)
                if (pair.Value != null && !string.IsNullOrWhiteSpace(pair.Value.FactionId))
                    factions.Add(pair.Value.FactionId);
            var orderedFactions = new List<string>(factions);
            orderedFactions.Sort(StringComparer.Ordinal);
            sb.AppendLine("[FactionControlSummary]");
            for (var f = 0; f < orderedFactions.Count; f++)
            {
                var factionId = orderedFactions[f];
                var councilCores = 0;
                var flagCores = 0;
                foreach (var pair in world.Strategic.Sites.Sites)
                {
                    var site = pair.Value;
                    if (site == null || !site.HasContinuousCore ||
                        !string.Equals(site.OwnerFactionId, factionId, StringComparison.Ordinal)) continue;
                    if (world.Strategic.FactionFlags.Flags.ContainsKey(site.CoreAssetId)) flagCores++;
                    else councilCores++;
                }
                var actualSites = 0;
                var actualPieces = 0;
                for (var i = 0; i < overlays.Count; i++)
                    if (string.Equals(overlays[i].FactionId, factionId, StringComparison.Ordinal))
                    {
                        actualSites++;
                        actualPieces += overlays[i].Pieces.Count;
                    }
                sb.Append("FactionId=").Append(factionId)
                    .Append(" CouncilHallCoreSites=").Append(councilCores)
                    .Append(" FlagCoreSites=").Append(flagCores)
                    .Append(" ActualControlSites=").Append(actualSites)
                    .Append(" ActualOverlayCount=").Append(actualSites)
                    .Append(" ActualOverlayPieces=").Append(actualPieces)
                    .AppendLine();
            }
            return sb.ToString();
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
