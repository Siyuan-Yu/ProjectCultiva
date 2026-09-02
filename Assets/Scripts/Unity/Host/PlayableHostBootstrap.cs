using System.Collections.Generic;
using System.IO;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Events;
using XianXia.Core.Navigation;
using XianXia.Core.Results;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// VS0.4 Playable Host entry. Loads BaseGame, builds session, EntityViews, tick／HUD wiring.
    /// </summary>
    public sealed class PlayableHostBootstrap : MonoBehaviour
    {
        [Header("Content")]
        [Tooltip("Optional override. Empty = Editor repo Content/BaseGame.")]
        [SerializeField] string contentPackageDirectoryOverride = "";

        [Header("Level Tester · 关卡地图")]
        [Tooltip("1x 下每 Tick 现实秒数。1 tick=5 游戏分；默认 1s → 1 现实秒=5 游戏分，5x=25 游戏分/秒。")]
        [SerializeField] string mapLayoutFilePath = "";
        [Tooltip("1x 下每 Tick 现实秒数。1 tick=5 游戏分；默认 1s → 1 现实秒=5 游戏分，5x=25 游戏分/秒。")]
        [SerializeField] string openingScenarioId = "base:scenario_ch01_reference";
        [Header("Level Tester · 人物名册")]
        [Tooltip("1x 下每 Tick 现实秒数。1 tick=5 游戏分；默认 1s → 1 现实秒=5 游戏分，5x=25 游戏分/秒。")]
        [SerializeField] string characterRosterId = "base:roster_level_tester";
        [HideInInspector]
        [SerializeField] string preferredMapLayoutId = "";
        // Phase 5S-B2-3.1：Loaded 战略人口 reconcile 的 playable bounds 缓存（按 ActiveMapLayoutId
        // 键控，仅地图切换时重建一次 WalkGrid，StepTick 每帧复用；避免刷日志 / 每帧重建）。
        string _loadedStrategicBoundsMapId = string.Empty;
        WildernessLocalWorldProjection.WildernessLocalMapBounds? _loadedStrategicWildernessBounds;
        WorldSiteSpatialMapping.WorldSiteLocalMapBounds? _loadedStrategicSiteBounds;
        [HideInInspector]
        [SerializeField] TextAsset mapLayoutJsonOverride;

        [Header("Session options (Host config only; does not change Core defaults when unused)")]
        [SerializeField] bool overrideObservationDiscoverChance;
        [Range(0, 100)]
        [SerializeField] int observationDiscoverChancePercent = 100;
        [SerializeField] int dailyRequiredAmount = 10;

        [Header("Presentation")]
        [SerializeField] EntityViewSpawner entityViewSpawner;
        [SerializeField] PlayableHostCameraRig cameraRig;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostCommandBridge commandBridge;
        [SerializeField] HostDebugHud debugHud;
        [SerializeField] HostLevelTesterCheatPanel levelTesterCheatPanel;
        [SerializeField] HostEventFeed eventFeed;
        [SerializeField] HostMapGraybox mapGraybox;
        [SerializeField] HostMoveController moveController;
        [SerializeField] HostActionMenu actionMenu;
        [SerializeField] HostFormalHud formalHud;
        [SerializeField] HostActivityPresenter activityPresenter;
        [SerializeField] HostCrowdPresenter crowdPresenter;
        [SerializeField] HostFeedbackOverlay feedbackOverlay;
        [SerializeField] HostWorkTargetMode workTargetMode;
        [SerializeField] HostContentInterruptPresenter contentInterrupt;
        [SerializeField] HostStrategicInterruptPresenter strategicInterrupt;
        [SerializeField] HostDialoguePresenter dialoguePresenter;
        [SerializeField] HostQuestJournal questJournal;
        [SerializeField] HostInventoryPanel inventoryPanel;
        [SerializeField] HostWorldMapPanel worldMapPanel;
        [SerializeField] HostManualLearnPrompt manualLearnPrompt;
        [SerializeField] HostCombatArtLearnPrompt combatArtLearnPrompt;
        [SerializeField] HostCombatArtsPanel combatArtsPanel;
        [SerializeField] HostCultivationPanel cultivationPanel;
        [SerializeField] HostCharacterSheetPanel characterSheetPanel;
        [SerializeField] HostRelationPanel relationPanel;
        [SerializeField] HostCultivateConfirmPrompt cultivateConfirm;
        [SerializeField] HostBreakthroughRitual breakthroughRitual;
        [SerializeField] HostSkillStudyRitual skillStudyRitual;
        [SerializeField] HostTicTacToePanel ticTacToePanel;
        [SerializeField] HostCaveSurveyPresenter caveSurveyPresenter;
        [SerializeField] HostLocalMapEnterPrompt localMapEnterPrompt;
        [SerializeField] HostSelectedUnitChrome selectedUnitChrome;
        [SerializeField] HostInteractSpotPresenter interactSpotPresenter;
        [SerializeField] HostSurfaceExitZonePresenter surfaceExitZonePresenter;
        [SerializeField] HostNpcScheduleMover npcScheduleMover;
        [SerializeField] HostNpcContextMenu npcContextMenu;

        [Header("Tick debug")]
        [SerializeField] bool initializeOnPlay = true;
        [SerializeField] bool autoTickWhenUnpaused = true;
        [Tooltip("1x 下每 Tick 现实秒数。1 tick=5 游戏分；默认 1s → 1 现实秒=5 游戏分，5x=25 游戏分/秒。")]
        [SerializeField] float secondsPerAutoTickAt1x = SimulationTickPacing.SecondsPerTickAt1x;
        [SerializeField] KeyCode togglePauseKey = KeyCode.Space;

        PlayableHostSession _session = new PlayableHostSession();
        float _autoTickAccumulator;
        string _resolvedContentPath = string.Empty;
        string _status = "Idle";
        // Phase 5R-B3B.3：NewGame 初始 Site 的一次性 Bootstrap provenance 与 consumed 状态全部由
        // _session 持有（InitialBootstrapSiteId + InitialBootstrapPending，完整运行 Session 生命周期）。
        // B3B.5 修复：consumed 之前挂在 PlayableHostBootstrap 实例字段（scene／WorldMap／LocalMap 重建
        // 会归零 → 离开初始 Site 再进入时重新 Bootstrap 覆盖 Boundary Canonical）。现在真正第一次
        // Bootstrap 完成后 _session.ConsumeInitialBootstrap() 一次性清空 id + pending。

        public PlayableHostSession Session => _session;

        public EntityViewSpawner ViewSpawner => entityViewSpawner;

        public HostSelectionController SelectionController => selectionController;

        public HostCommandBridge CommandBridge => commandBridge;

        public HostDebugHud DebugHud => debugHud;

        public HostLevelTesterCheatPanel LevelTesterCheatPanel => levelTesterCheatPanel;

        public HostEventFeed EventFeed => eventFeed;

        public HostMoveController MoveController => moveController;

        public HostPlayerPartyController PlayerPartyController =>
            GetComponent<HostPlayerPartyController>();

        public HostWorkTargetMode WorkTargetMode => workTargetMode;

        public HostContentInterruptPresenter ContentInterrupt => contentInterrupt;

        public HostStrategicInterruptPresenter StrategicInterrupt => strategicInterrupt;

        public HostDialoguePresenter DialoguePresenter => dialoguePresenter;

        public HostQuestJournal QuestJournal => questJournal;

        public HostInventoryPanel InventoryPanel => inventoryPanel;

        public HostWorldMapPanel WorldMapPanel => worldMapPanel;

        public HostManualLearnPrompt ManualLearnPrompt => manualLearnPrompt;
        public HostCombatArtLearnPrompt CombatArtLearnPrompt => combatArtLearnPrompt;

        public HostCombatArtsPanel CombatArtsPanel => combatArtsPanel;

        public HostCultivationPanel CultivationPanel => cultivationPanel;

        public HostCharacterSheetPanel CharacterSheetPanel => characterSheetPanel;

        public HostRelationPanel RelationPanel => relationPanel;

        public HostCultivateConfirmPrompt CultivateConfirm => cultivateConfirm;

        public HostBreakthroughRitual BreakthroughRitual => breakthroughRitual;

        public HostSkillStudyRitual SkillStudyRitual => skillStudyRitual;

        public HostTicTacToePanel TicTacToePanel => ticTacToePanel;

        public HostCaveSurveyPresenter CaveSurveyPresenter => caveSurveyPresenter;

        public HostLocalMapEnterPrompt LocalMapEnterPrompt => localMapEnterPrompt;

        public HostNpcContextMenu NpcContextMenu => npcContextMenu;

        public string StatusLine => _status;

        public string ResolvedContentPath => _resolvedContentPath;

        void Awake()
        {
            PlayerPartyWorldLocationDebug.Sink = msg => Debug.Log(msg, this);
            // Phase 5R-B3B.4：Site ingress 结构化诊断（保留 ownership / 映射数值 / failure；
            // 已移除一次性 writer 追踪）。订阅前先清去重/id，避免上次会话残留干扰。
            PlayerPartySiteIngressTrace.ResetDedupe();
            PlayerPartySiteIngressTrace.Sink = msg => Debug.Log(msg, this);

            if (entityViewSpawner == null)
                entityViewSpawner = GetComponent<EntityViewSpawner>() ?? GetComponentInChildren<EntityViewSpawner>();
            if (cameraRig == null)
                cameraRig = GetComponent<PlayableHostCameraRig>() ?? GetComponentInChildren<PlayableHostCameraRig>();
            if (selectionController == null)
                selectionController = GetComponent<HostSelectionController>() ??
                                     GetComponentInChildren<HostSelectionController>();
            if (commandBridge == null)
                commandBridge = GetComponent<HostCommandBridge>() ??
                               GetComponentInChildren<HostCommandBridge>();
            if (debugHud == null)
                debugHud = GetComponent<HostDebugHud>() ?? GetComponentInChildren<HostDebugHud>();
            if (eventFeed == null)
                eventFeed = GetComponent<HostEventFeed>() ?? GetComponentInChildren<HostEventFeed>();
            if (mapGraybox == null)
                mapGraybox = GetComponent<HostMapGraybox>() ?? GetComponentInChildren<HostMapGraybox>();
            if (moveController == null)
                moveController = GetComponent<HostMoveController>() ?? GetComponentInChildren<HostMoveController>();
            if (actionMenu == null)
                actionMenu = GetComponent<HostActionMenu>() ?? GetComponentInChildren<HostActionMenu>();
            if (formalHud == null)
                formalHud = GetComponent<HostFormalHud>() ?? GetComponentInChildren<HostFormalHud>();
            if (contentInterrupt == null)
                contentInterrupt = GetComponent<HostContentInterruptPresenter>() ??
                                  GetComponentInChildren<HostContentInterruptPresenter>();
            if (strategicInterrupt == null)
                strategicInterrupt = GetComponent<HostStrategicInterruptPresenter>() ??
                                    GetComponentInChildren<HostStrategicInterruptPresenter>();
            if (dialoguePresenter == null)
                dialoguePresenter = GetComponent<HostDialoguePresenter>() ??
                                   GetComponentInChildren<HostDialoguePresenter>();
            if (questJournal == null)
                questJournal = GetComponent<HostQuestJournal>() ??
                              GetComponentInChildren<HostQuestJournal>();
            if (inventoryPanel == null)
                inventoryPanel = GetComponent<HostInventoryPanel>() ??
                                GetComponentInChildren<HostInventoryPanel>();
            if (worldMapPanel == null)
                worldMapPanel = GetComponent<HostWorldMapPanel>() ??
                               GetComponentInChildren<HostWorldMapPanel>();
            if (manualLearnPrompt == null)
                manualLearnPrompt = GetComponent<HostManualLearnPrompt>() ??
                                   GetComponentInChildren<HostManualLearnPrompt>();
            if (combatArtLearnPrompt == null)
                combatArtLearnPrompt = GetComponent<HostCombatArtLearnPrompt>() ??
                                      GetComponentInChildren<HostCombatArtLearnPrompt>();

            secondsPerAutoTickAt1x = SimulationTickPacing.SecondsPerTickAt1x;
        }

        void Start()
        {
            EnsureLevelTesterComponents();
            if (initializeOnPlay)
                TryInitialize();
        }

        void EnsureLevelTesterComponents()
        {
            if (!IsLevelTesterContext())
                return;

            if (GetComponent<LevelTesterHud>() == null)
                gameObject.AddComponent<LevelTesterHud>();

            if (GetComponent<HostLevelTesterCheatPanel>() == null)
                gameObject.AddComponent<HostLevelTesterCheatPanel>();
        }

        public bool IsLevelTesterContext() =>
            GetComponent<LevelTesterHud>() != null ||
            !string.IsNullOrWhiteSpace(mapLayoutFilePath) ||
            !string.IsNullOrWhiteSpace(preferredMapLayoutId) ||
            mapLayoutJsonOverride != null;

        void Update()
        {
            if (!_session.IsInitialized)
                return;

            if (_session.World?.Strategic != null)
                _session.World.Strategic.PlayerPartyContext = _session.PlayerParty;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            FormalArmyStrategicMutationDiagnosticsHost.TickFrame();
#endif

            // Phase 5R-B6.5-B：Modal 强制暂停期间 Space 不能切换（ModalHardPaused 分层）。
            if (Input.GetKeyDown(togglePauseKey) &&
                !_session.ModalHardPaused &&
                (questJournal == null || !questJournal.IsOpen) &&
                (inventoryPanel == null || !inventoryPanel.IsOpen) &&
                (manualLearnPrompt == null || !manualLearnPrompt.IsOpen) &&
                (combatArtLearnPrompt == null || !combatArtLearnPrompt.IsOpen) &&
                (combatArtsPanel == null || !combatArtsPanel.IsOpen) &&
                (cultivationPanel == null || !cultivationPanel.IsOpen) &&
                (characterSheetPanel == null || !characterSheetPanel.IsOpen) &&
                (relationPanel == null || !relationPanel.IsOpen) &&
                (cultivateConfirm == null || !cultivateConfirm.IsOpen) &&
                (breakthroughRitual == null || !breakthroughRitual.IsResultOpen) &&
                (ticTacToePanel == null || !ticTacToePanel.IsOpen) &&
                (contentInterrupt == null || !contentInterrupt.HasBlockingInterrupt) &&
                (strategicInterrupt == null || !strategicInterrupt.HasBlockingInterrupt))
            {
                _session.ManualPaused = !_session.ManualPaused;
                RefreshStatus();
            }

            if (!_session.IsPaused &&
                autoTickWhenUnpaused &&
                !StrategicClockFreezeService.IsWorldTickFrozen(_session.World))
            {
                var speed = EffectiveSpeedMultiplier();
                _autoTickAccumulator += Time.unscaledDeltaTime * speed;
                var interval = SecondsPerAutoTickAt1x;
                while (_autoTickAccumulator >= interval)
                {
                    _autoTickAccumulator -= interval;
                    StepTick();
                }
            }

        }

        /// <summary>Set before <see cref="TryInitialize"/> (sample scene / EditMode).</summary>
        public void ConfigureOpeningScenario(string scenarioId)
        {
            openingScenarioId = scenarioId ?? "";
        }

        public void ConfigurePreferredMapLayout(string mapLayoutId)
        {
            preferredMapLayoutId = mapLayoutId ?? "";
        }

        public float SecondsPerAutoTickAt1x => Mathf.Max(0.01f, secondsPerAutoTickAt1x);

        public void ResetAutoTickAccumulator() => _autoTickAccumulator = 0f;

        public int EffectiveSpeedMultiplier()
        {
            EnsureDebugHud();
            var speed = debugHud != null ? debugHud.SpeedMultiplier : 1;
            return speed < 1 ? 1 : speed;
        }

        /// <summary>ADR-0023：Resolve 后恢复开战前倍速/summary>
        public void ApplySavedSpeedMultiplier(int multiplier)
        {
            EnsureDebugHud();
            if (debugHud == null)
                return;
            var m = multiplier < 1 ? 1 : multiplier;
            debugHud.SetSpeedMultiplier(m);
        }

        /// <summary>
        /// 顶栏 1xxx0x：统一Host 倍速
        /// Tick 驱动的工作／休息／吃饭／修炼／作息与表现层移动共用此倍率
        /// </summary>
        public void SetSpeedMultiplier(int multiplier)
        {
            EnsureDebugHud();
            debugHud?.SetSpeedMultiplier(multiplier);
            ResetAutoTickAccumulator();
            RefreshStatus();
        }

        void EnsureDebugHud()
        {
            if (debugHud != null)
                return;
            debugHud = GetComponent<HostDebugHud>() ?? gameObject.AddComponent<HostDebugHud>();
            debugHud.Bind(this, selectionController);
        }

        void EnsureLevelTesterCheatPanel()
        {
            if (!IsLevelTesterContext())
                return;
            levelTesterCheatPanel = GetComponent<HostLevelTesterCheatPanel>() ??
                                    gameObject.AddComponent<HostLevelTesterCheatPanel>();
        }

        /// <summary>
        /// 表现层帧间隔：受暂停Host 倍速影响（移动／分离等）
        /// Core 行动进度Tick（已按倍速推进）；连续位移必须用同一倍率
        /// </summary>
        public float PresentationDeltaTime
        {
            get
            {
                if (_session == null || !_session.IsInitialized || _session.IsPaused)
                    return 0f;
                return Time.unscaledDeltaTime * EffectiveSpeedMultiplier();
            }
        }

        public int EffectiveGameMinutesPerRealSecond() =>
            SimulationTickPacing.GameMinutesPerRealSecondAtSpeed(EffectiveSpeedMultiplier());

        public string PreferredMapLayoutId => preferredMapLayoutId ?? "";

        public string OpeningScenarioId => openingScenarioId ?? "";

        public string CharacterRosterId => characterRosterId ?? "";

        public string MapLayoutFilePath => mapLayoutFilePath ?? "";

        public TextAsset MapLayoutJsonOverride => mapLayoutJsonOverride;

        public bool TryInitialize()
        {
            if (entityViewSpawner == null)
                entityViewSpawner = GetComponent<EntityViewSpawner>() ?? gameObject.AddComponent<EntityViewSpawner>();
            if (selectionController == null)
                selectionController = GetComponent<HostSelectionController>() ??
                                     gameObject.AddComponent<HostSelectionController>();
            if (commandBridge == null)
                commandBridge = GetComponent<HostCommandBridge>() ??
                               gameObject.AddComponent<HostCommandBridge>();
            if (debugHud == null)
                debugHud = GetComponent<HostDebugHud>() ?? gameObject.AddComponent<HostDebugHud>();
            EnsureLevelTesterCheatPanel();
            if (eventFeed == null)
                eventFeed = GetComponent<HostEventFeed>() ?? gameObject.AddComponent<HostEventFeed>();
            if (mapGraybox == null)
                mapGraybox = GetComponent<HostMapGraybox>() ?? gameObject.AddComponent<HostMapGraybox>();
            if (moveController == null)
                moveController = GetComponent<HostMoveController>() ?? gameObject.AddComponent<HostMoveController>();
            if (actionMenu == null)
                actionMenu = GetComponent<HostActionMenu>() ?? gameObject.AddComponent<HostActionMenu>();
            if (formalHud == null)
                formalHud = GetComponent<HostFormalHud>() ?? gameObject.AddComponent<HostFormalHud>();
            if (GetComponent<HostHousingAreaSelection>() == null)
                gameObject.AddComponent<HostHousingAreaSelection>();
            if (GetComponent<HostControlCoreAssault>() == null)
                gameObject.AddComponent<HostControlCoreAssault>();
            if (GetComponent<HostDestructibleAssault>() == null)
                gameObject.AddComponent<HostDestructibleAssault>();
            if (GetComponent<HostFarmFieldLabor>() == null)
                gameObject.AddComponent<HostFarmFieldLabor>();
            if (GetComponent<HostNpcMeleeAssault>() == null)
                gameObject.AddComponent<HostNpcMeleeAssault>();
            if (GetComponent<HostMeleeStrikeVfx>() == null)
                gameObject.AddComponent<HostMeleeStrikeVfx>();
            if (GetComponent<HostSpiritVeilController>() == null)
                gameObject.AddComponent<HostSpiritVeilController>();
            if (GetComponent<HostCombatVitalsBars>() == null)
                gameObject.AddComponent<HostCombatVitalsBars>();
            if (GetComponent<HostCombatSkillBar>() == null)
                gameObject.AddComponent<HostCombatSkillBar>();
            if (activityPresenter == null)
                activityPresenter = GetComponent<HostActivityPresenter>() ??
                                   gameObject.AddComponent<HostActivityPresenter>();
            if (crowdPresenter == null)
                crowdPresenter = GetComponent<HostCrowdPresenter>() ??
                                gameObject.AddComponent<HostCrowdPresenter>();
            if (feedbackOverlay == null)
                feedbackOverlay = GetComponent<HostFeedbackOverlay>() ??
                                  gameObject.AddComponent<HostFeedbackOverlay>();
            if (workTargetMode == null)
                workTargetMode = GetComponent<HostWorkTargetMode>() ??
                                 gameObject.AddComponent<HostWorkTargetMode>();
            if (contentInterrupt == null)
                contentInterrupt = GetComponent<HostContentInterruptPresenter>() ??
                                  gameObject.AddComponent<HostContentInterruptPresenter>();
            if (strategicInterrupt == null)
                strategicInterrupt = GetComponent<HostStrategicInterruptPresenter>() ??
                                    gameObject.AddComponent<HostStrategicInterruptPresenter>();
            if (dialoguePresenter == null)
                dialoguePresenter = GetComponent<HostDialoguePresenter>() ??
                                   gameObject.AddComponent<HostDialoguePresenter>();
            if (dialoguePresenter != null && dialoguePresenter.GetComponent<HostDialogueUguiView>() == null)
                dialoguePresenter.gameObject.AddComponent<HostDialogueUguiView>();
            if (questJournal == null)
                questJournal = GetComponent<HostQuestJournal>() ??
                              gameObject.AddComponent<HostQuestJournal>();
            if (inventoryPanel == null)
                inventoryPanel = GetComponent<HostInventoryPanel>() ??
                                gameObject.AddComponent<HostInventoryPanel>();
            if (worldMapPanel == null)
                worldMapPanel = GetComponent<HostWorldMapPanel>() ??
                               gameObject.AddComponent<HostWorldMapPanel>();
            if (manualLearnPrompt == null)
                manualLearnPrompt = GetComponent<HostManualLearnPrompt>() ??
                                   gameObject.AddComponent<HostManualLearnPrompt>();
            if (combatArtLearnPrompt == null)
                combatArtLearnPrompt = GetComponent<HostCombatArtLearnPrompt>() ??
                                      gameObject.AddComponent<HostCombatArtLearnPrompt>();
            if (combatArtsPanel == null)
                combatArtsPanel = GetComponent<HostCombatArtsPanel>() ??
                                 gameObject.AddComponent<HostCombatArtsPanel>();
            if (cultivationPanel == null)
                cultivationPanel = GetComponent<HostCultivationPanel>() ??
                                  gameObject.AddComponent<HostCultivationPanel>();
            if (characterSheetPanel == null)
                characterSheetPanel = GetComponent<HostCharacterSheetPanel>() ??
                                     gameObject.AddComponent<HostCharacterSheetPanel>();
            if (relationPanel == null)
                relationPanel = GetComponent<HostRelationPanel>() ??
                               gameObject.AddComponent<HostRelationPanel>();
            if (cultivateConfirm == null)
                cultivateConfirm = GetComponent<HostCultivateConfirmPrompt>() ??
                                  gameObject.AddComponent<HostCultivateConfirmPrompt>();
            if (breakthroughRitual == null)
                breakthroughRitual = GetComponent<HostBreakthroughRitual>() ??
                                    gameObject.AddComponent<HostBreakthroughRitual>();
            if (skillStudyRitual == null)
                skillStudyRitual = GetComponent<HostSkillStudyRitual>() ??
                                  gameObject.AddComponent<HostSkillStudyRitual>();
            if (ticTacToePanel == null)
                ticTacToePanel = GetComponent<HostTicTacToePanel>() ??
                                gameObject.AddComponent<HostTicTacToePanel>();
            if (caveSurveyPresenter == null)
                caveSurveyPresenter = GetComponent<HostCaveSurveyPresenter>() ??
                                     gameObject.AddComponent<HostCaveSurveyPresenter>();
            if (localMapEnterPrompt == null)
                localMapEnterPrompt = GetComponent<HostLocalMapEnterPrompt>() ??
                                     gameObject.AddComponent<HostLocalMapEnterPrompt>();
            if (selectedUnitChrome == null)
                selectedUnitChrome = GetComponent<HostSelectedUnitChrome>() ??
                                    gameObject.AddComponent<HostSelectedUnitChrome>();
            if (GetComponent<HostWorkLoop>() == null)
                gameObject.AddComponent<HostWorkLoop>();
            if (interactSpotPresenter == null)
                interactSpotPresenter = GetComponent<HostInteractSpotPresenter>() ??
                                       gameObject.AddComponent<HostInteractSpotPresenter>();
            if (surfaceExitZonePresenter == null)
                surfaceExitZonePresenter = GetComponent<HostSurfaceExitZonePresenter>() ??
                                          gameObject.AddComponent<HostSurfaceExitZonePresenter>();
            if (npcScheduleMover == null)
                npcScheduleMover = GetComponent<HostNpcScheduleMover>() ??
                                  gameObject.AddComponent<HostNpcScheduleMover>();
            if (npcContextMenu == null)
                npcContextMenu = GetComponent<HostNpcContextMenu>() ??
                                gameObject.AddComponent<HostNpcContextMenu>();
            if (GetComponent<HostPartyPathPreview>() == null)
                gameObject.AddComponent<HostPartyPathPreview>();

            selectionController.ClearSelection();
            entityViewSpawner.Clear();
            eventFeed.Clear();
            contentInterrupt.ClearSessionState();
            if (strategicInterrupt != null)
                strategicInterrupt.ClearSessionState();
            if (dialoguePresenter != null)
                dialoguePresenter.ClearSessionState();
            if (npcContextMenu != null)
                npcContextMenu.ClearSessionState();
            if (questJournal != null)
                questJournal.ClearSessionState();
            if (inventoryPanel != null)
                inventoryPanel.ClearSessionState();
            if (worldMapPanel != null)
                worldMapPanel.ClearSessionState();
            if (manualLearnPrompt != null)
                manualLearnPrompt.ClearSessionState();
            if (combatArtLearnPrompt != null)
                combatArtLearnPrompt.ClearSessionState();
            if (combatArtsPanel != null)
                combatArtsPanel.ClearSessionState();
            var skillBarClear = GetComponent<HostCombatSkillBar>();
            skillBarClear?.ClearSessionState();
            if (cultivationPanel != null)
                cultivationPanel.ClearSessionState();
            if (characterSheetPanel != null)
                characterSheetPanel.ClearSessionState();
            if (relationPanel != null)
                relationPanel.ClearSessionState();
            if (cultivateConfirm != null)
                cultivateConfirm.ClearSessionState();
            if (breakthroughRitual != null)
                breakthroughRitual.ClearSessionState();
            if (skillStudyRitual != null)
                skillStudyRitual.ClearSessionState();
            if (ticTacToePanel != null)
                ticTacToePanel.ClearSessionState();
            if (caveSurveyPresenter != null)
                caveSurveyPresenter.ClearSessionState();
            if (localMapEnterPrompt != null)
                localMapEnterPrompt.ClearSessionState();
            mapGraybox.Clear();
            interactSpotPresenter.Clear();
            if (surfaceExitZonePresenter != null)
                surfaceExitZonePresenter.Clear();

            if (!TryResolveContentPackageDirectory(out _resolvedContentPath, out var pathError))
            {
                _status = "INIT FAILED: " + pathError;
                Debug.LogError("[PlayableHost] " + pathError, this);
                _session.Clear();
                selectionController.ClearSelection();
                return false;
            }

            var options = new PlayableDayOptions
            {
                DailyRequiredAmount = Mathf.Max(1, dailyRequiredAmount),
                OpeningScenarioId = string.IsNullOrWhiteSpace(openingScenarioId)
                    ? "base:scenario_ch01_reference"
                    : openingScenarioId.Trim(),
                CharacterRosterId = string.IsNullOrWhiteSpace(characterRosterId)
                    ? null
                    : characterRosterId.Trim()
            };
            if (overrideObservationDiscoverChance)
                options.ObservationDiscoverChancePercent = observationDiscoverChancePercent;

            var init = _session.Initialize(_resolvedContentPath, options);
            if (init.IsFailure)
            {
                _status = "INIT FAILED: " + init.Error;
                Debug.LogError("[PlayableHost] " + init.Error, this);
                entityViewSpawner.Clear();
                selectionController.ClearSelection();
                return false;
            }

            if (!ApplyMapLayoutOverrides(out var mapError))
            {
                _status = "MAP OVERRIDE FAILED: " + mapError;
                Debug.LogError("[PlayableHost] " + mapError, this);
                return false;
            }

            _session.PreferredMapLayoutId = string.IsNullOrWhiteSpace(preferredMapLayoutId)
                ? _session.PreferredMapLayoutId
                : preferredMapLayoutId.Trim();
            if (!string.IsNullOrWhiteSpace(_session.PreferredMapLayoutId))
                _session.World.LocalMap.EnsureOverworld(_session.PreferredMapLayoutId);
            else if (MapLayoutPick.TryGet(_session, out var picked) && picked != null)
            {
                _session.PreferredMapLayoutId = picked.Id.ToString();
                _session.World.LocalMap.EnsureOverworld(_session.PreferredMapLayoutId);
            }

            var synced = MapLayoutPresentationSync.Apply(_session);
            if (synced > 0)
                Debug.Log("[PlayableHost] Synced " + synced + " location presentation(s) from mapLayout", this);
            entityViewSpawner.Rebuild(_session);
            mapGraybox.Rebuild(_session);
            if (interactSpotPresenter != null)
                interactSpotPresenter.Rebuild();
            var cam = Camera.main;
            selectionController.Bind(entityViewSpawner, cam);
            selectionController.SetPartyFilter(_session.CharacterIds);
            if (_session.CharacterIds.Count > 0 && !_session.PlayerParty.HasActive)
                _session.PlayerParty.TryInitialize(_session.CharacterIds[0], out _);
            // 开局权威位置愈合：禁止 AtWorldPosition 漂移冒充 Site。
            PlayerPartyWorldLocationQuery.TryResolve(
                _session.World, _session.PlayerParty, out _, healDrift: true);
            PlayerPartyWorldLocationDebug.LogSnapshot(
                _session.World, _session.PlayerParty, "StartupResolve");
            var playerPartyController = GetComponent<HostPlayerPartyController>() ??
                                        gameObject.AddComponent<HostPlayerPartyController>();
            playerPartyController.Bind(this);
            var housingSel = GetComponent<HostHousingAreaSelection>();
            if (housingSel != null)
                housingSel.Bind(this, selectionController, cam);
            var assault = GetComponent<HostControlCoreAssault>();
            if (assault != null)
                assault.Bind(this);
            var destructibleAssault = GetComponent<HostDestructibleAssault>();
            if (destructibleAssault != null)
                destructibleAssault.Bind(this);
            var farmLabor = GetComponent<HostFarmFieldLabor>();
            if (farmLabor != null)
                farmLabor.Bind(this);
            var npcMelee = GetComponent<HostNpcMeleeAssault>();
            if (npcMelee != null)
                npcMelee.Bind(this);
            var spiritVeil = GetComponent<HostSpiritVeilController>();
            if (spiritVeil != null)
                spiritVeil.Bind(this);
            var skillBar = GetComponent<HostCombatSkillBar>();
            if (skillBar != null)
                skillBar.Bind(this);
            var vitalsBars = GetComponent<HostCombatVitalsBars>();
            if (vitalsBars != null)
                vitalsBars.Bind(this);
            if (_session.CharacterIds.Count > 0)
            {
                if (!_session.PlayerParty.HasActive)
                    _session.PlayerParty.TryInitialize(_session.CharacterIds[0], out _);
                selectionController.SelectEntity(_session.PlayerParty.ActiveCharacterId, false);
            }
            feedbackOverlay.Bind(cam);
            commandBridge.Bind(_session, selectionController, feedbackOverlay);
            var workLoop = GetComponent<HostWorkLoop>();
            if (workLoop != null)
                workLoop.Bind(this, commandBridge, moveController);
            debugHud.Bind(this, selectionController);
            EnsureLevelTesterCheatPanel();
            if (levelTesterCheatPanel != null)
                levelTesterCheatPanel.Bind(this, selectionController);
            moveController.Bind(this, selectionController, entityViewSpawner, commandBridge, npcContextMenu);
            var pathPreview = GetComponent<HostPartyPathPreview>();
            if (pathPreview != null)
                pathPreview.Bind(this, moveController, selectionController, cam);
            moveController.SetWalkGrid(ResolveWalkGrid());
            // Phase 5R-B3C1：NewGame 初始 Site 的第一次 Bootstrap 必须在【初始 LocalMap 第一次真正建立】
            // 时发生（TryInitialize 启动链内），不能等玩家离开 Site 再回来才第一次执行。
            TryRunInitialSiteBootstrap();
            moveController.BindLocalMapContext(_session.World.LocalMap.ActiveMapLayoutId);
            // Host-side Safe+Walkable fallback：materialize+Rebuild 之后、OnLocalMapMaterialized
            // （→RebindAllFollowers）之前 —— WalkGrid 已 ready、EntityView 已就位。
            playerPartyController.ValidateAndRepairPlayerPartyMaterializedPlacement();
            playerPartyController.OnLocalMapMaterialized(_session.World.LocalMap.ActiveMapLayoutId);
            if (npcContextMenu != null)
                npcContextMenu.Bind(this, selectionController, moveController, dialoguePresenter, localMapEnterPrompt);
            if (localMapEnterPrompt != null)
                localMapEnterPrompt.Bind(this, selectionController, commandBridge, moveController);
            actionMenu.Bind(this, selectionController, commandBridge);
            formalHud.Bind(this, selectionController, eventFeed);
            activityPresenter.Bind(this, entityViewSpawner);
            crowdPresenter.Bind(this);
            workTargetMode.Bind(this, selectionController, commandBridge);
            if (dialoguePresenter != null)
                dialoguePresenter.Bind(this, commandBridge, selectionController);
            contentInterrupt.Bind(this, commandBridge, selectionController, dialoguePresenter);
            if (strategicInterrupt != null)
                strategicInterrupt.Bind(this);
            questJournal.Bind(this, commandBridge, selectionController);
            inventoryPanel.Bind(this);
            if (interactSpotPresenter != null)
                interactSpotPresenter.Bind(this);
            if (worldMapPanel != null)
                worldMapPanel.Bind(this);
            if (manualLearnPrompt != null)
                manualLearnPrompt.Bind(this);
            if (combatArtLearnPrompt != null)
                combatArtLearnPrompt.Bind(this);
            if (combatArtsPanel != null)
                combatArtsPanel.Bind(this, selectionController);
            cultivationPanel.Bind(this, selectionController);
            characterSheetPanel.Bind(this, selectionController);
            relationPanel.Bind(this);
            cultivateConfirm.Bind(this, selectionController, commandBridge);
            if (breakthroughRitual != null)
                breakthroughRitual.Bind(this);
            if (skillStudyRitual != null)
                skillStudyRitual.Bind(this);
            if (ticTacToePanel != null)
                ticTacToePanel.Bind(this);
            if (caveSurveyPresenter != null)
                caveSurveyPresenter.Bind(this, selectionController, commandBridge);
            selectedUnitChrome.Bind(
                this,
                selectionController,
                cultivationPanel,
                characterSheetPanel,
                relationPanel,
                cam);
            npcScheduleMover.Bind(this, moveController, entityViewSpawner);
            ActivateSurfaceLocalMapPresentation();
            // Bootstrap already published WorldInitialized／EntityCreated capture once.
            DispatchDrainedEvents();
            FrameCameraOnSlots();

            _session.IsPaused = true;
            _autoTickAccumulator = 0f;
            RefreshStatus();
            Debug.Log(
                "[PlayableHost] Initialized. Characters=" + _session.CharacterIds.Count +
                " Views=" + entityViewSpawner.SpawnedCount +
                " Content=" + _resolvedContentPath,
                this);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            FormalArmyStrategicMutationDiagnosticsHost.BindSession(this);
#endif
            return true;
        }

        /// <summary>After Snapshot restore: rebuild views and rebind Host adapters.</summary>
        public void RebindHostControlAfterSnapshotRestore()
        {
            if (!_session.IsInitialized)
                return;

            HostInputGate.Clear();
            _session.IsPaused = false;

            var cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
            if (selectionController != null && entityViewSpawner != null)
                selectionController.Bind(entityViewSpawner, cam);

            if (commandBridge != null && selectionController != null)
                commandBridge.Bind(_session, selectionController);

            if (debugHud != null && selectionController != null)
                debugHud.Bind(this, selectionController);
            if (levelTesterCheatPanel != null && selectionController != null)
                levelTesterCheatPanel.Bind(this, selectionController);

            if (moveController != null && selectionController != null && entityViewSpawner != null)
            {
                moveController.Bind(this, selectionController, entityViewSpawner, commandBridge, npcContextMenu);
                moveController.ResetPresentationMovementState();
                moveController.SetWalkGrid(ResolveWalkGrid());
                moveController.BindLocalMapContext(_session.World.LocalMap.ActiveMapLayoutId);
                if (_session.PlayerParty != null && _session.PlayerParty.Count > 0)
                    moveController.InvalidatePartyLocalMovement(_session.PlayerParty.Members);
            }

            var partyController = PlayerPartyController;
            if (partyController != null)
                partyController.Bind(this);

            if (selectionController != null && _session.PlayerParty.HasActive)
                selectionController.SelectEntity(_session.PlayerParty.ActiveCharacterId, false);

            if (selectionController != null)
                selectionController.SetPartyFilter(_session.CharacterIds);

            SnapCameraToActiveAfterSnapshotRestore();

            HostSnapshotActiveControlTrace.LogAfterPresentationRebuild(this);
        }

        /// <summary>Snapshot Load 完成后：对准 Materialize 后的 Active Presentation（一次性）。</summary>
        void SnapCameraToActiveAfterSnapshotRestore()
        {
            var partyController = PlayerPartyController;
            if (partyController != null)
            {
                partyController.SnapCameraToActiveOnce();
                return;
            }

            if (cameraRig == null || entityViewSpawner == null || !_session.PlayerParty.HasActive)
                return;

            var activeId = _session.PlayerParty.ActiveCharacterId;
            if (entityViewSpawner.Registry.TryGet(activeId, out var view) && view != null)
                cameraRig.FrameWorldPoint(view.transform.position);
        }

        /// <summary>After Snapshot restore: rebuild views and rebind Host adapters.</summary>
        public void RebuildPresentationAfterLoad()
        {
            if (!_session.IsInitialized)
                return;

            if (entityViewSpawner == null)
                entityViewSpawner = GetComponent<EntityViewSpawner>() ?? gameObject.AddComponent<EntityViewSpawner>();
            if (selectionController == null)
                selectionController = GetComponent<HostSelectionController>() ??
                                     gameObject.AddComponent<HostSelectionController>();
            if (commandBridge == null)
                commandBridge = GetComponent<HostCommandBridge>() ??
                               gameObject.AddComponent<HostCommandBridge>();
            if (debugHud == null)
                debugHud = GetComponent<HostDebugHud>() ?? gameObject.AddComponent<HostDebugHud>();
            EnsureLevelTesterCheatPanel();
            if (eventFeed == null)
                eventFeed = GetComponent<HostEventFeed>() ?? gameObject.AddComponent<HostEventFeed>();

            HostSnapshotSessionRehydration.ResolvePartyWorldFromActiveControlledCharacter(
                _session.World,
                _session.PlayerParty);

            selectionController.ClearSelection();
            if (worldMapPanel != null)
                worldMapPanel.ClearSessionState();
            entityViewSpawner.Clear();
            if (moveController != null)
                moveController.ResetPresentationMovementState();
            commandBridge.Bind(_session, selectionController);
            debugHud.Bind(this, selectionController);
            if (levelTesterCheatPanel != null)
                levelTesterCheatPanel.Bind(this, selectionController);
            eventFeed.Clear();

            ApplyPartyWorldSitePresentation(closeWorldMap: false);

            RebindHostControlAfterSnapshotRestore();

            DispatchDrainedEvents();
            _autoTickAccumulator = 0f;
            RefreshStatus();
        }

        void FrameCameraOnSlots()
        {
            if (cameraRig == null)
                return;

            // 进出洞府：优先对准可见己方，避免整图中心与落点错
            if (TryFrameCameraOnParty())
                return;

            if (MapLayoutPresentationSync.TryGetLayout(_session, out var layout) &&
                layout.Width > 0 && layout.Height > 0)
            {
                var cs = layout.CellSize > 0f ? layout.CellSize : 1f;
                var cx = layout.OriginX + layout.Width * cs * 0.5f;
                var cy = layout.OriginY + layout.Height * cs * 0.5f;
                cameraRig.FrameSlots(HostPresentationSpace.FromPresentation(cx, cy));
                return;
            }

            if (entityViewSpawner == null)
                return;

            var slots = entityViewSpawner.SlotPositions;
            if (slots == null || slots.Count == 0)
            {
                cameraRig.FrameSlots(Vector3.zero);
                return;
            }

            var sum = Vector3.zero;
            for (var i = 0; i < slots.Count; i++)
                sum += slots[i];
            cameraRig.FrameSlots(sum / slots.Count);
        }

        bool TryFrameCameraOnParty()
        {
            if (cameraRig == null || entityViewSpawner == null || !_session.IsInitialized)
                return false;

            var sum = Vector3.zero;
            var n = 0;
            var ids = _session.CharacterIds;
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (!LocalMapVisibility.IsEntityVisible(_session.World, id))
                    continue;
                if (!entityViewSpawner.Registry.TryGet(id, out var view) || view == null)
                    continue;
                sum += view.transform.position;
                n++;
            }

            if (n == 0)
                return false;
            cameraRig.FrameSlots(sum / n);
            return true;
        }

        void LogWorldCombatAssembly(
            SimulationWorld world,
            string resolvedBattleLocalMapId,
            string activeMapBeforePresentation,
            string phase)
        {
            var strategic = world?.Strategic;
            var encounter = strategic?.Encounter;
            var snapshot = strategic?.Participants;
            if (encounter == null || snapshot == null)
                return;

            var tracked = BattlefieldSpawnScope.GetSpawnList(world);
            var trackedCount = tracked?.Count ?? 0;
            var livingTrackedCount = 0;
            var presentedTrackedCount = 0;
            if (tracked != null)
            {
                for (var i = 0; i < tracked.Count; i++)
                {
                    var id = new XianXia.Core.Domain.Ids.EntityId(tracked[i]);
                    if (!world.Entities.TryGet(id, out var entity))
                        continue;
                    if (entity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var location) &&
                        location.HasPresentationOverride)
                        presentedTrackedCount++;
                    if (entity.TryGet<XianXia.Core.Entities.LifecycleComponent>(out var life) &&
                        life.State == XianXia.Core.Entities.LifecycleState.Alive)
                        livingTrackedCount++;
                }
            }

            var enemyIds = new List<XianXia.Core.Domain.Ids.EntityId>(8);
            snapshot.CollectEnemyEntityIds(enemyIds);
            var enemyEntityCount = 0;
            var visibleEnemyCount = 0;
            for (var i = 0; i < enemyIds.Count; i++)
            {
                if (world.Entities.TryGet(enemyIds[i], out _))
                    enemyEntityCount++;
                if (LocalMapVisibility.IsEntityVisible(world, enemyIds[i]))
                    visibleEnemyCount++;
            }

            var selectedFriendlyCount = 0;
            var friendlyFormalArmyParticipantCount = 0;
            var visibleFriendlyArmyCount = 0;
            for (var i = 0; i < snapshot.Records.Count; i++)
            {
                var record = snapshot.Records[i];
                var selectedFriendly =
                    record.Kind == BattleParticipantKind.MandatoryFriendly ||
                    (record.Kind == BattleParticipantKind.OptionalFriendly && record.Selected);
                if (selectedFriendly)
                    selectedFriendlyCount++;
                if (!selectedFriendly || string.IsNullOrEmpty(record.FormalArmyId))
                    continue;

                friendlyFormalArmyParticipantCount++;
                if (LocalMapVisibility.IsEntityVisible(world, record.EntityId))
                    visibleFriendlyArmyCount++;
            }

            var activeMap = world.LocalMap?.ActiveMapLayoutId ?? string.Empty;
            var reuseCurrentMap = string.Equals(
                activeMapBeforePresentation?.Trim(),
                resolvedBattleLocalMapId?.Trim(),
                System.StringComparison.Ordinal);
            Debug.Log(
                "[WorldCombatAssembly] " + phase +
                " SpawnOnNextMapLoad=" + encounter.SpawnOnNextMapLoad +
                " ParticipantCount=" + snapshot.Records.Count +
                " SelectedFriendlyCount=" + selectedFriendlyCount +
                " EnemyStackCount=" + snapshot.CollectEnemyStackIds().Count +
                " EngagedPartyCount=" + encounter.EngagedPartyIds.Count +
                " TrackedCount=" + trackedCount +
                " LivingTrackedCount=" + livingTrackedCount +
                " PresentedTrackedCount=" + presentedTrackedCount +
                " EnemyEntityCount=" + enemyEntityCount +
                " FriendlyFormalArmyParticipantCount=" + friendlyFormalArmyParticipantCount +
                " VisibleEnemyCount=" + visibleEnemyCount +
                " VisibleFriendlyArmyCount=" + visibleFriendlyArmyCount +
                " ActiveMapLayoutId=" + activeMap +
                " ResolvedBattleLocalMapId=" + (resolvedBattleLocalMapId ?? string.Empty) +
                " ReuseCurrentLocalMap=" + reuseCurrentMap,
                this);
        }

        /// <summary>LocalMap 进出后：PreferredMapLayout、重建灰盒／实体／寻路/summary>
        /// <param name="frameCamera">勘查显形等轻量刷新应false，避免镜头乱跳/param>
        public void ReloadLocalMapPresentation(bool frameCamera = true)
        {
            if (!_session.IsInitialized)
                return;

            var active = _session.World.LocalMap.ActiveMapLayoutId;
            if (!string.IsNullOrWhiteSpace(active))
                _session.PreferredMapLayoutId = active.Trim();

            MapLayoutPresentationSync.Apply(_session);
            SyncExitTriggerDepthFromActiveMap();
            if (entityViewSpawner != null)
                entityViewSpawner.Rebuild(_session);
            if (mapGraybox != null)
                mapGraybox.Rebuild(_session);
            if (interactSpotPresenter != null)
                interactSpotPresenter.Rebuild();
            if (moveController != null)
            {
                moveController.SetWalkGrid(ResolveWalkGrid());
                moveController.BindLocalMapContext(active?.Trim() ?? string.Empty);
            }
            ActivateSurfaceLocalMapPresentation();
            if (frameCamera)
                FrameCameraOnSlots();
            RefreshStatus();
        }

        /// <summary>
        /// 从当前 Active MapLayout 写入 ExitTriggerDepth（Gameplay）。
        /// Geometry 只认 MapLayout；与角色位置 / Entry 无关。
        /// </summary>
        void SyncExitTriggerDepthFromActiveMap()
        {
            if (!_session.IsInitialized || _session.World?.LocalMap == null)
                return;
            var lm = _session.World.LocalMap;
            if (lm.IsInInterior)
            {
                lm.ExitTriggerDepth = 0f;
                lm.ClearPlayableBounds();
                return;
            }

            if (MapLayoutPick.TryGet(_session, out var layout) && layout != null)
            {
                if (layout.ExitTriggerDepth > 0.0001f)
                    lm.ExitTriggerDepth = layout.ExitTriggerDepth;
                else
                {
                    var cs = layout.CellSize > 0.0001f ? layout.CellSize : 1f;
                    lm.ExitTriggerDepth = cs * SurfaceExitZoneCalculator.DefaultExitTriggerDepth;
                }

                var cellSize = layout.CellSize > 0.0001f ? layout.CellSize : 1f;
                lm.SetPlayableBounds(layout.OriginX, layout.OriginY, cellSize, layout.Width, layout.Height);
                return;
            }

            var walk = ResolveWalkGrid();
            if (walk != null)
            {
                lm.SetPlayableBounds(
                    walk.OriginX,
                    walk.OriginY,
                    walk.CellSize > 0.0001f ? walk.CellSize : 1f,
                    walk.Width,
                    walk.Height);
            }
            else
            {
                lm.ClearPlayableBounds();
            }

            if (lm.ExitTriggerDepth <= 0.0001f)
                lm.ExitTriggerDepth = SurfaceExitZoneCalculator.DefaultExitTriggerDepth;
        }

        /// <summary>显式清空 Active LocalMap 表现（进入场景失败／无目标图时用）。全员上路时不要调用/summary>
        public void UnloadActiveLocalMapPresentation(bool clearEmptyEncounter = false)
        {
            if (!_session.IsInitialized)
                return;

            var world = _session.World;
            var active = world.LocalMap.ActiveMapLayoutId ?? string.Empty;
            if (clearEmptyEncounter &&
                !string.IsNullOrWhiteSpace(active) &&
                world.Strategic?.Encounter != null &&
                string.Equals(
                    active.Trim(),
                    BattleOfferService.ResolveActiveEncounterLocalMapId(world),
                    System.StringComparison.Ordinal) &&
                !LocalMapVisibility.HasFriendlyCharacterOnMapLayout(
                    world, _session.CharacterIds, active))
            {
                StrategicEncounterSpawner.ClearSpawned(world);
                world.Strategic.Encounter.ClearEngagedParty();
            }

            LoadedDestinationArrivalMaterializer.ReleaseEligibleOccupantsOnLocalMapUnload(
                world,
                _session.PlayerParty);

            world.LocalMap.ActiveMapLayoutId = string.Empty;
            world.LocalMap.OverworldMapLayoutId = string.Empty;
            world.LocalMap.ClearPlayableBounds();
            preferredMapLayoutId = string.Empty;
            _loadedStrategicBoundsMapId = string.Empty;
            _loadedStrategicWildernessBounds = null;
            _loadedStrategicSiteBounds = null;
            _session.PreferredMapLayoutId = string.Empty;
            if (world.Strategic?.Encounter != null)
                world.Strategic.Encounter.ActiveBattlefieldId = string.Empty;
            if (entityViewSpawner != null)
                entityViewSpawner.Clear();
            if (mapGraybox != null)
                mapGraybox.Rebuild(_session);
            if (interactSpotPresenter != null)
                interactSpotPresenter.Rebuild();
            if (surfaceExitZonePresenter != null)
                surfaceExitZonePresenter.Clear();
            if (moveController != null)
                moveController.SetWalkGrid(null);
            RefreshStatus();
        }

        /// <summary>
        /// 正式展开链路：当前 PartyWorld 已 Resolve 的 LocalMap → Materialize PlayerParty → 重建表现。
        /// WorldSite 与 Wilderness 共用；未来 Close WorldMap 也应调用此入口。
        /// </summary>
        public void ExpandLocalMapForCurrentPartyWorld(bool closeWorldMap = false) =>
            ApplyPartyWorldSitePresentation(closeWorldMap);

        /// <summary>
        /// WorldSite／Wilderness 到站后：PartyWorld.LocalMapId 卸／装实体图；Materialize PlayerParty。
        /// </summary>
        /// <param name="closeWorldMap">从大地图「进入场景」时应为 true，关掉全屏地图页</param>
        public void ApplyPartyWorldSitePresentation(bool closeWorldMap = false)
        {
            if (closeWorldMap && worldMapPanel != null)
                worldMapPanel.Close();

            if (!_session.IsInitialized)
                return;

            var world = _session.World;
            // World Combat 复用已加载的真实 LocalMap 时，不会有另一条隐藏的装图入口。
            // 记录正式 ApplyPending 调用点的前后状态，用于区分「未消费 pending」与
            // 「已准备实体但未落表现／未重建视图」。PendingEngagement 会在调用方返回后清理，
            // 因而必须在本次 presentation 调用中判定。
            var activeMapBeforePresentation = world.LocalMap?.ActiveMapLayoutId ?? string.Empty;
            if (SnapshotActiveControlledLocalMapResolver.TryResolveRequiredLocalMap(
                    world,
                    _session.PlayerParty,
                    out var requiredFocus) &&
                requiredFocus.HasValue &&
                !string.IsNullOrEmpty(requiredFocus.LocalMapId))
            {
                SnapshotActiveControlledLocalMapResolver.ApplyResolvedPartyWorldFocus(world, in requiredFocus);
            }

            var targetMap = world.PartyWorld.LocalMapId ?? string.Empty;
            if (BattleOfferService.HasActiveManualEncounter(world))
            {
                targetMap = BattleOfferService.ResolveActiveEncounterLocalMapId(world);
                world.PartyWorld.LocalMapId = targetMap;
            }
            var onEncounterMap = BattleOfferService.HasActiveManualEncounter(world) &&
                                 !string.IsNullOrWhiteSpace(targetMap) &&
                                 string.Equals(
                                     targetMap.Trim(),
                                     StrategicEncounterCatalog.DefaultEncounterLocalMapId,
                                     System.StringComparison.Ordinal);
            var sameMapWorldCombat =
                world.Strategic?.PendingEngagement != null &&
                world.Strategic.PendingEngagement.IsActive &&
                !string.IsNullOrWhiteSpace(targetMap) &&
                string.Equals(
                    activeMapBeforePresentation.Trim(),
                    targetMap.Trim(),
                    System.StringComparison.Ordinal);

            // 目标图上暂无我方（例如全员已上路）：保持当前 LocalMap 画面，禁止卸图把视线带走
            // Wilderness：CanLoadMapLayoutForParty 已认 AtHex + PartyWorld.LocalMapId
            if (!string.IsNullOrWhiteSpace(targetMap) &&
                !(world.Strategic?.Encounter != null && world.Strategic.Encounter.SpawnOnNextMapLoad) &&
                !LocalMapVisibility.CanLoadMapLayoutForParty(
                    world, _session.CharacterIds, targetMap.Trim()) &&
                !SnapshotActiveControlledLocalMapResolver.ActiveAuthorizesMapLoad(
                    world, _session.PlayerParty, targetMap.Trim()))
            {
                RefreshStatus();
                return;
            }

            // 目标图必须在内容包里，否则禁止带着荒村图「假装切换
            if (!string.IsNullOrWhiteSpace(targetMap))
            {
                var parsedMap = XianXia.Core.Domain.Ids.DefinitionId.Parse(targetMap.Trim());
                if (parsedMap.IsFailure ||
                    !_session.Registry.TryGetMapLayout(parsedMap.Value, out _))
                {
                    Debug.LogError(
                        "[PlayableHost] LocalMap missing in registry: " + targetMap,
                        this);
                    RefreshStatus();
                    return;
                }
            }

            var places = WorldRegionBootstrap.ActivatePlacesForMapLayout(
                world, _session.Registry, targetMap);
            if (places.IsFailure)
                Debug.LogWarning("[PlayableHost] ActivatePlaces: " + places.Error, this);

            if (string.IsNullOrWhiteSpace(targetMap))
            {
                // 焦点图为空但画面仍在：保留当LocalMap（全员上路时视线不带走）
                if (!string.IsNullOrWhiteSpace(world.LocalMap.ActiveMapLayoutId))
                {
                    RefreshStatus();
                    return;
                }

                UnloadActiveLocalMapPresentation(clearEmptyEncounter: false);
                return;
            }

            // Phase 2C：先绑定目标 LocalMap 并装图，再用该图 WalkGrid 做 Materialize。
            // 禁止在旧图 bounds 上投影后再切图（会导致 Active 落点非法／看起来「消失」）。
            preferredMapLayoutId = targetMap;
            _session.PreferredMapLayoutId = targetMap;
            world.LocalMap.ActiveMapLayoutId = targetMap;
            world.LocalMap.OverworldMapLayoutId = targetMap;
            _session.RefreshViewableEntityIds();

            if (LoadedLocalMapPlacementSnapshotRestore.IsRestoringFromSnapshot)
            {
                LoadedLocalMapPlacementSnapshotRestore.ApplySavedPlacementsToDomain(world, targetMap);
                HostSnapshotLocalPlacementTrace.LogPartyMembersAfterPhase(
                    world, _session.PlayerParty, targetMap, "PreReload");
            }

            ReloadLocalMapPresentation(frameCamera: false);

            WildernessLocalWorldProjection.WildernessLocalMapBounds? materializeBounds = null;
            var playerPartyMaterialized = false;
            if (!onEncounterMap &&
                !string.IsNullOrWhiteSpace(targetMap) &&
                _session.PlayerParty != null &&
                _session.PlayerParty.Count > 0)
            {
                if (PlayerPartyLocalMapMaterializationService.IsWildernessLocalExpand(world) ||
                    (world.PlayerPartyTravel != null &&
                     world.PlayerPartyTravel.LocationKind == PlayerPartyLocationKind.AtWorldPosition))
                {
                    var walk = ResolveWalkGrid();
                    if (walk != null)
                    {
                        materializeBounds = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                            walk.OriginX, walk.OriginY, walk.CellSize, walk.Width, walk.Height);
                    }
                }

                // Phase 5R-B2：Site Spatial Initialization Handshake —— 识别 ownership 并构造真实 bounds。
                // NewGame（首次 AtWorldSite 展开）= BootstrapFromAuthoredLocal（StartLocation→Canonical）；
                // Snapshot restore = LegacyRestoreLocal（snapshot local placement→bootstrap，无则保持默认）；
                // 其余（Wilderness→Site 进入 / WorldMap 重开 / 新格式 restore）= ProjectCanonicalWorldToLocal。
                WorldSiteSpatialMapping.WorldSiteLocalMapBounds? siteBounds = null;
                var siteMode = PlayerPartySiteMaterializeMode.Default;
                // Phase 5R-B3C1：消费决策 out 提升到本方法层（isSiteExpand 块外也要读，见 Materialize 消费段）。
                var consumeBootstrapNow = false;
                var isSiteExpand = !string.IsNullOrWhiteSpace(world.PartyWorld?.SiteId) &&
                                   world.PlayerPartyTravel != null &&
                                   world.PlayerPartyTravel.LocationKind == PlayerPartyLocationKind.AtWorldSite;
                if (isSiteExpand)
                {
                    var walkForSite = ResolveWalkGrid();
                    if (walkForSite != null)
                    {
                        siteBounds = WorldSiteSpatialMapping.WorldSiteLocalMapBounds.FromOriginSize(
                            walkForSite.OriginX, walkForSite.OriginY, walkForSite.CellSize,
                            walkForSite.Width, walkForSite.Height);
                    }

                    // Phase 5R-B3B.3：ownership 由 Core 纯函数按真正 provenance 解析——
                    // BootstrapFromAuthoredLocal 仅限 NewGame 初始 Site 的首次展开（启动链记录
                    // _session.InitialBootstrapSiteId）；起点在 Wilderness 等场景该值空 → 任何
                    // Wilderness→Site 进入都只能 ProjectCanonicalWorldToLocal（不覆盖 BoundaryContact）。
                    siteMode = SiteMaterializeModeResolver.Resolve(
                        LoadedLocalMapPlacementSnapshotRestore.IsRestoringFromSnapshot,
                        _session.InitialBootstrapSiteId,
                        world.PartyWorld?.SiteId ?? string.Empty,
                        !_session.InitialBootstrapPending,
                        out consumeBootstrapNow);
                    // Phase 5R-B3B.4：[3 MaterializeDecision] —— 决策输入（真正 provenance）；
                    // 同一 ingress trace id。Materialize 内 [4 WorldToLocal] 输出映射结果对照。
                    var decisionReason =
                        LoadedLocalMapPlacementSnapshotRestore.IsRestoringFromSnapshot
                            ? "snapshot restore (LegacyRestore priority)"
                            : !string.IsNullOrEmpty(_session.InitialBootstrapSiteId) &&
                              string.Equals(
                                  _session.InitialBootstrapSiteId,
                                  world.PartyWorld?.SiteId ?? string.Empty,
                                  System.StringComparison.Ordinal) &&
                              _session.InitialBootstrapPending
                                ? "NewGame initial site first expand (Bootstrap)"
                                : "canonical existing (ProjectCanonicalWorldToLocal)";
                    PlayerPartySiteIngressTrace.Log(
                        "MaterializeDecision",
                        "mode=" + siteMode +
                        " initialBootstrapSiteId=" + (_session.InitialBootstrapSiteId ?? string.Empty) +
                        " currentSiteId=" + (world.PartyWorld?.SiteId ?? string.Empty) +
                        " bootstrapConsumed=" + !_session.InitialBootstrapPending +
                        " hasSnapshot=" + LoadedLocalMapPlacementSnapshotRestore.IsRestoringFromSnapshot +
                        " reason=" + decisionReason);
                }

                // Phase 5R-B3C1：Materialize 返回 Result —— Bootstrap 成功（IsSuccess）才消费 token；
                // 失败明确 error 且不消费（保留 pending，下次正确执行）。ProjectCanonical 失败已由
                // Materialize 内部 return Failure（不静默 DefaultStart）。
                var materializeResult = PlayerPartyLocalMapMaterializationService.MaterializePartyOnResolvedLocalMap(
                    world, _session.PlayerParty.Members, materializeBounds, siteBounds, siteMode);
                playerPartyMaterialized = true;
                if (SiteMaterializeModeResolver.ShouldConsumeBootstrap(
                        consumeBootstrapNow, materializeResult.IsSuccess))
                {
                    _session.ConsumeInitialBootstrap();
                    PlayerPartySiteIngressTrace.Log(
                        "BootstrapTokenConsumed",
                        "site=" + (world.PartyWorld?.SiteId ?? string.Empty));
                }
                else if (consumeBootstrapNow)
                {
                    Debug.LogError(
                        "[PlayableHost] Initial site bootstrap FAILED; token NOT consumed: " +
                        materializeResult.Error, this);
                    PlayerPartySiteIngressTrace.Log(
                        "BootstrapTokenKept",
                        "error=" + materializeResult.Error);
                }

                if (LoadedLocalMapPlacementSnapshotRestore.IsRestoringFromSnapshot)
                {
                    HostSnapshotLocalPlacementTrace.LogPartyMembersAfterPhase(
                        world, _session.PlayerParty, targetMap, "Materialize");
                }

                if (materializeBounds.HasValue &&
                    PlayerPartyLocalMapMaterializationService.IsWildernessLocalExpand(world))
                {
                    LoadedDestinationArrivalMaterializer.MaterializeEligibleWildernessCharactersOnLocalMap(
                        world,
                        _session.PlayerParty,
                        materializeBounds.Value);
                }

                WildernessLocalWorldProjection.WildernessLocalMapBounds? logBounds = materializeBounds;
                if (!logBounds.HasValue)
                {
                    var walkForLog = ResolveWalkGrid();
                    if (walkForLog != null)
                    {
                        logBounds = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                            walkForLog.OriginX, walkForLog.OriginY, walkForLog.CellSize,
                            walkForLog.Width, walkForLog.Height);
                    }
                }

                if (logBounds.HasValue)
                {
                    var depth = SurfaceExitZoneCalculator.ResolveDepthFromSession(
                        world, logBounds.Value);
                    SurfaceExitZoneCalculator.LogSurfaceExitConnectionsOnMaterialize(
                        world, logBounds.Value, depth);
                }

                if (materializeResult.IsSuccess)
                {
                    PlayerPartyTransitionMembership.ReconcilePlayerPartyMemberWorldPresenceFromMotion(
                        world, _session.PlayerParty, "SurfaceMaterialize");
                }

                PlayerPartyWorldLocationDebug.LogSnapshot(
                    world, _session.PlayerParty, "MaterializeLocalView");

                if (_session.PlayerParty.HasActive &&
                    !PlayerPartyLocalMapMaterializationService.TryAssertActiveMaterializedOnce(
                        world,
                        _session.PlayerParty.ActiveCharacterId,
                        materializeBounds,
                        out var matErr))
                {
                    Debug.LogError(
                        "[PlayableHost] Active materialize assert failed: " + matErr,
                        this);
                }
            }
            else
            {
                // 遭遇／无 Party：保留旧 Army／Encounter 落点逻辑
                PlaceLegacyFocusCharactersOnLocalMap(world, onEncounterMap);
            }

            // Phase 5S-B2-3.2：Friendly battle tactical assembly 必须在正确 Battle LocalMap
            // 加载后（PlayerParty 已按 BattleHex materialize）、enemy ApplyPending 前执行。
            // participant authority 用当前 frozen BattleParticipantSnapshot（不清扫 SupportArea、
            // 不重新 gather）；ExplicitEncounterMap 与战后（Participants.Clear 后 kind 重置）不触发。
            if (!onEncounterMap &&
                StrategicEncounterSpawner.HasActiveRealLocalMapManualEncounter(world))
            {
                StrategicEncounterSpawner.MaterializeFriendlyParticipantsForRealLocalMap(
                    world, _session.PlayerParty);
            }

            if (sameMapWorldCombat)
                LogWorldCombatAssembly(world, targetMap, activeMapBeforePresentation, "Before");

            var spawned = StrategicEncounterSpawner.ApplyPending(world);
            if (spawned.IsFailure)
                Debug.LogWarning("[PlayableHost] Strategic encounter spawn: " + spawned.Error, this);
            if (sameMapWorldCombat)
                LogWorldCombatAssembly(world, targetMap, activeMapBeforePresentation, "AfterApplyPending");
            if (onEncounterMap)
            {
                StrategicEncounterSpawner.EnsureTrackedSpawnsLocalPresentation(world);
                _session.RefreshViewableEntityIds();
                entityViewSpawner?.Rebuild(_session);
            }
            else
            {
                if (world.Strategic?.Encounter != null)
                {
                    world.Strategic.Encounter.ActiveBattlefieldId = string.Empty;
                    if (world.Strategic.Encounter.SpawnedEntityIds.Count > 0)
                    {
                        _session.RefreshViewableEntityIds();
                        entityViewSpawner?.Rebuild(_session);
                    }
                }

                // Wilderness／Site：确保 Materialize 后的 Party 视图已刷出
                // Phase 5S-B2-3.1：补齐 FormalArmy / Residual 战略人口 —— Player 走到 Army A 的
                // Hex / WorldSite → load LocalMap → Army A members 当场出现，无需 Battle。
                ReconcileLoadedStrategicPopulation();
                _session.RefreshViewableEntityIds();
                entityViewSpawner?.Rebuild(_session);
                FlushLoadedDestinationArrivals();
            }

            // 切图后再对齐一次地点坐标（MapLayout sync 之后）并选中在场角色
            var startId = world.WorldRegion.StartLocationId;
            var encounter = world.Strategic?.Encounter;
            var filterEngaged = onEncounterMap && encounter != null && encounter.HasEngagedParty;
            // Wilderness AtWorldPosition：Materialize 已按 WorldPosition 投影，禁止再吸回 startLocation。
            var skipStartSnapForWilderness =
                world.PlayerPartyTravel != null &&
                world.PlayerPartyTravel.HasPosition &&
                world.PlayerPartyTravel.LocationKind == PlayerPartyLocationKind.AtWorldPosition &&
                string.IsNullOrEmpty(world.PartyWorld?.SiteId);
            var activeMapForSnap = world.LocalMap.ActiveMapLayoutId?.Trim() ?? string.Empty;
            var skipStartSnapForSavedPlacements =
                LoadedLocalMapPlacementSnapshotRestore.HasRestoredPlacementsForMap(activeMapForSnap);
            // Phase 5R-B3C1：PlayerParty 已经过 Materialization → 其输出就是唯一 placement authority，
            // 禁止再被 legacy StartLocation snap 覆盖；snap 仅保留给无 PlayerParty 的 legacy 分支。
            if (SiteMaterializeModeResolver.ShouldApplyLegacyStartLocationSnap(
                    playerPartyMaterialized,
                    skipStartSnapForWilderness,
                    skipStartSnapForSavedPlacements) &&
                !string.IsNullOrEmpty(startId) &&
                world.WorldRegion.TryGet(startId, out var syncedStart))
            {
                for (var i = 0; i < _session.CharacterIds.Count; i++)
                {
                    var id = _session.CharacterIds[i];
                    if (filterEngaged && !encounter.IsEngaged(id))
                        continue;
                    if (!LocalMapVisibility.IsEntityVisible(world, id))
                        continue;
                    var activeMapId = world.LocalMap.ActiveMapLayoutId?.Trim() ?? string.Empty;
                    if (LoadedLocalMapPlacementSnapshotRestore.TryGetPlacement(id, activeMapId, out _, out _))
                        continue;
                    if (!world.Entities.TryGet(id, out var ent) ||
                        !ent.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc))
                        continue;
                    loc.LocationId = startId;
                    loc.SetPresentationOverride(syncedStart.PresentationX, syncedStart.PresentationZ);
                }

                entityViewSpawner?.SyncLocations(_session);
            }
            else if (skipStartSnapForWilderness || skipStartSnapForSavedPlacements)
            {
                entityViewSpawner?.SyncLocations(_session);
            }

            if (LoadedLocalMapPlacementSnapshotRestore.IsRestoringFromSnapshot)
            {
                HostSnapshotLocalPlacementTrace.LogPartyMembersAfterPhase(
                    world, _session.PlayerParty, activeMapForSnap, "Rebuild");
            }

            if (selectionController != null)
            {
                var party = _session.PlayerParty;
                if (party != null && party.HasActive &&
                    LocalMapVisibility.IsEntityVisible(world, party.ActiveCharacterId))
                    selectionController.SelectEntity(party.ActiveCharacterId, false);
                else
                {
                    for (var i = 0; i < _session.CharacterIds.Count; i++)
                    {
                        var id = _session.CharacterIds[i];
                        if (!LocalMapVisibility.IsEntityVisible(world, id))
                            continue;
                        selectionController.SelectEntity(id, false);
                        break;
                    }
                }
            }

            if (!LoadedLocalMapPlacementSnapshotRestore.IsRestoringFromSnapshot)
            {
                // Phase 5R-B3C1.1：正式 Site transition（Wilderness→WorldSite / reopen / ingress）后
                // one-shot 对准 Active Character —— SnapCameraToActiveOnce 置 Free 模式 + 一次性对准，
                // 不进入持续跟随；普通状态自由镜头 / WASD / 中键 / RTS 右键规则不变。
                // Wilderness→Wilderness 保持既有 TryFrameCameraOnParty 行为，不受影响。
                var atSiteTransition = playerPartyMaterialized &&
                                       world.PlayerPartyTravel != null &&
                                       world.PlayerPartyTravel.LocationKind ==
                                       PlayerPartyLocationKind.AtWorldSite;
                var pc = PlayerPartyController;
                if (atSiteTransition && pc != null &&
                    _session.PlayerParty != null && _session.PlayerParty.HasActive)
                    pc.SnapCameraToActiveOnce();
                else
                    TryFrameCameraOnParty();
            }
            ActivateSurfaceLocalMapPresentation();
            RestorePlayerPartyLocalMapPresentation(targetMap);
        }

        void RestorePlayerPartyLocalMapPresentation(string localMapId)
        {
            if (string.IsNullOrWhiteSpace(localMapId))
                return;

            if (LoadedLocalMapPlacementSnapshotRestore.IsRestoringFromSnapshot &&
                _session?.PlayerParty != null)
            {
                for (var i = 0; i < _session.PlayerParty.Members.Count; i++)
                {
                    var id = _session.PlayerParty.Members[i];
                    HostSnapshotLocalPlacementTrace.LogWorldSiteLocalRestore(
                        _session.World,
                        id,
                        localMapId.Trim(),
                        LoadedLocalMapPlacementSnapshotRestore.TryGetPlacement(
                            id,
                            localMapId,
                            out _,
                            out _)
                            ? "SnapshotLocalPlacement"
                            : "DefaultStart");
                }
            }

            PlayerPartyController?.ValidateAndRepairPlayerPartyMaterializedPlacement();
            PlayerPartyController?.OnLocalMapMaterialized(localMapId.Trim());
            LoadedLocalMapPlacementSnapshotRestore.FinishRestorePresentation();
        }

        void PlaceLegacyFocusCharactersOnLocalMap(SimulationWorld world, bool onEncounterMap)
        {
            var focusNode = world.PartyWorld.SiteId;
            var focusSiteId = world.PartyWorld.SiteId;
            WorldSite focusSite = null;
            if (!string.IsNullOrEmpty(focusSiteId))
                world.Strategic?.Sites?.TryGet(focusSiteId, out focusSite);
            var focusArmyId = world.PartyWorld.FocusFormalArmyId;
            FormalArmy focusArmy = null;
            if (!string.IsNullOrEmpty(focusArmyId))
                world.Strategic?.FormalArmies?.TryGet(focusArmyId, out focusArmy);
            var startId = world.WorldRegion.StartLocationId;
            var encounter = world.Strategic?.Encounter;
            var filterEngaged = onEncounterMap && encounter != null && encounter.HasEngagedParty;
            for (var i = 0; i < _session.CharacterIds.Count; i++)
            {
                var id = _session.CharacterIds[i];
                if (filterEngaged && !encounter.IsEngaged(id))
                    continue;
                world.WorldPresence.TryGet(id, out var wp);
                if (wp != null &&
                    !onEncounterMap &&
                    (wp.Mode == XianXia.Core.World.PartyWorldPresenceMode.InEncounter ||
                     wp.Mode == XianXia.Core.World.PartyWorldPresenceMode.AtSite))
                    continue;
                var engagedInEncounter = filterEngaged && encounter.IsEngaged(id);
                if (!engagedInEncounter && focusSite != null)
                {
                    if (!StrategicWorldSitePopulationService.IsCharacterPresentAtWorldSite(
                            world, id, focusSite))
                        continue;
                }
                else if (!engagedInEncounter && focusArmy != null)
                {
                    if (!focusArmy.ContainsMember(id))
                        continue;
                }
                else if (!engagedInEncounter &&
                    wp != null &&
                    !string.IsNullOrEmpty(focusNode) &&
                    !string.Equals(wp.SiteId, focusNode, System.StringComparison.Ordinal))
                    continue;
                if (wp != null && wp.Mode == XianXia.Core.World.PartyWorldPresenceMode.AtSite)
                {
                    if (!onEncounterMap)
                        continue;
                    wp.Mode = XianXia.Core.World.PartyWorldPresenceMode.InEncounter;
                }
                else if (wp != null &&
                         wp.Mode == XianXia.Core.World.PartyWorldPresenceMode.AtSite &&
                         onEncounterMap &&
                         engagedInEncounter)
                {
                    wp.Mode = XianXia.Core.World.PartyWorldPresenceMode.InEncounter;
                }

                if (!world.Entities.TryGet(id, out var ent))
                    continue;
                if (!ent.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc))
                {
                    loc = new XianXia.Core.Exploration.EntityLocationComponent();
                    ent.AddComponent(loc);
                }

                if (!string.IsNullOrEmpty(startId) && world.WorldRegion.TryGet(startId, out var startLoc))
                {
                    loc.LocationId = startId;
                    loc.SetPresentationOverride(startLoc.PresentationX, startLoc.PresentationZ);
                }
                else
                {
                    loc.LocationId = string.Empty;
                    loc.SetPresentationOverride(0f, 0f);
                }
            }
        }

        /// <summary>残留战场：存活角色「查看」弥留同伴／再入接战 LocalMap</summary>
        public void EnterLingeringBattlefield(IReadOnlyList<EntityId> party)
        {
            if (!_session.IsInitialized || party == null || party.Count == 0)
                return;
            var world = _session.World;
            if (world?.Strategic?.Encounter == null ||
                !BattleOfferService.HasLingeringBattlefield(world))
                return;

            var scratch = new List<EntityId>(party.Count);
            var focus = party[0];
            for (var i = 0; i < party.Count; i++)
            {
                if (LingeringBattlefieldPartyService.IsIncapacitated(world, party[i]))
                {
                    focus = party[i];
                    break;
                }
            }

            var mandatoryLiving = new List<EntityId>(party.Count);
            for (var i = 0; i < party.Count; i++)
            {
                if (LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, party[i]))
                    mandatoryLiving.Add(party[i]);
            }

            if (!LingeringBattlefieldPartyService.CanEnterLingeringBattlefield(
                    world,
                    _session.CharacterIds,
                    focus,
                    scratch,
                    mandatoryLiving))
                return;

            world.Strategic.ClearPendingLingeringVisit();

            HexCoord targetHex = default;
            if (StrategicResidualPresenceService.TryGetResidualHex(world, focus, out targetHex) ||
                ArmyHexBattleAnchorService.TryGetBattleAnchorHex(world.Strategic.Participants, out targetHex))
                StrategicEncounterSpawner.TryPrepareLingeringLocalMapSession(world, targetHex);

            var rt = world.Strategic.Encounter;
            var mapId = BattleOfferService.ResolveActiveEncounterLocalMapId(world);
            var stackId = !string.IsNullOrEmpty(rt.ArmyStackId)
                ? rt.ArmyStackId
                : world.Strategic.Participants?.PrimaryEnemyStackId ?? string.Empty;
            StrategicEncounterSpawner.PlanManualEncounter(
                world,
                stackId,
                string.IsNullOrEmpty(rt.EncounterLinkId) ? "linger" : rt.EncounterLinkId,
                scratch);
            world.PartyWorld.LocalMapId = mapId;
            world.PartyWorld.EncounterId = string.IsNullOrEmpty(rt.EncounterLinkId)
                ? "linger"
                : rt.EncounterLinkId;
            preferredMapLayoutId = mapId;
            _session.PreferredMapLayoutId = mapId;
            StrategicClockFreezeService.BeginOrPromote(
                world, StrategicClockFreezeReason.ManualEncounter);
            _session.IsPaused = false;
            if (worldMapPanel != null)
                worldMapPanel.Close();
            ApplyPartyWorldSitePresentation(closeWorldMap: true);
        }

        /// <summary>仅重刷地表戳（如勘查显形），不重建实体、不挪镜头/summary>
        public void RefreshMapStampsOnly()
        {
            if (!_session.IsInitialized)
                return;
            MapLayoutPresentationSync.Apply(_session);
            if (mapGraybox != null)
                mapGraybox.Rebuild(_session);
            if (interactSpotPresenter != null)
                interactSpotPresenter.Rebuild();
        }

        /// <summary>
        /// Surface LocalMap 正式激活后：同步 ExitTriggerDepth 并 Bind Exit Zone Presentation。
        /// 所有进入 Surface 图的路径（开局／Load／Expand／Reload）必须调用；Interior 自动 Clear。
        /// </summary>
        public void ActivateSurfaceLocalMapPresentation()
        {
            if (!_session.IsInitialized)
                return;

            if (surfaceExitZonePresenter == null)
                surfaceExitZonePresenter = GetComponent<HostSurfaceExitZonePresenter>() ??
                                          gameObject.AddComponent<HostSurfaceExitZonePresenter>();

            if (!SurfaceExitZoneCalculator.ShouldPresent(_session.World))
            {
                surfaceExitZonePresenter.Clear();
                return;
            }

            SyncExitTriggerDepthFromActiveMap();
            surfaceExitZonePresenter.Bind(this);
            surfaceExitZonePresenter.Rebuild();
        }

        /// <summary>Surface Exit Zone 与 WalkGrid 对齐后强制刷新（Expand 末尾保险）。</summary>
        public void RefreshSurfaceExitZones() => ActivateSurfaceLocalMapPresentation();

        /// <summary>Background Travel 到达已 Loaded LocalMap 后刷出增量 EntityView。</summary>
        public void FlushLoadedDestinationArrivals()
        {
            if (!_session.IsInitialized || entityViewSpawner == null)
                return;

            var pending = LoadedDestinationArrivalMaterializer.PendingPresentationFlush;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BackgroundBgTravelFullTrace.ActiveTraceId > 0 &&
                pending != null && pending.Count > 0)
            {
                BackgroundBgTravelFullTrace.LogFlush(
                    pending.Count,
                    spawnedHint: -1);
            }
#endif
            if (pending == null || pending.Count == 0)
                return;

            _session.RefreshViewableEntityIds();
            entityViewSpawner.SpawnMissingVisibleViews(_session);
            LoadedDestinationArrivalMaterializer.ClearPendingPresentationFlush();
        }

        /// <summary>
        /// Phase 5S-B2-3.1：当前 Loaded surface LocalMap 的 playable bounds 缓存（按 map id 键控）。
        /// 仅地图切换时重建一次；StepTick 每帧复用，避免重建 WalkGrid / 刷日志。
        /// </summary>
        void ResolveLoadedStrategicBounds(SimulationWorld world)
        {
            var mapId = world?.LocalMap?.ActiveMapLayoutId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(mapId))
                return;
            if (string.Equals(mapId, _loadedStrategicBoundsMapId, System.StringComparison.Ordinal))
                return;

            _loadedStrategicWildernessBounds = null;
            _loadedStrategicSiteBounds = null;
            _loadedStrategicBoundsMapId = mapId;

            var walk = ResolveWalkGrid();
            if (walk == null)
                return;

            if (PlayerPartyLocalMapMaterializationService.IsWildernessLocalExpand(world))
            {
                _loadedStrategicWildernessBounds =
                    WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                        walk.OriginX, walk.OriginY, walk.CellSize, walk.Width, walk.Height);
            }
            else if (!string.IsNullOrWhiteSpace(world.PartyWorld?.SiteId))
            {
                _loadedStrategicSiteBounds =
                    WorldSiteSpatialMapping.WorldSiteLocalMapBounds.FromOriginSize(
                        walk.OriginX, walk.OriginY, walk.CellSize, walk.Width, walk.Height);
            }
        }

        /// <summary>
        /// Phase 5S-B2-3.1：reconcile 当前 Loaded LocalMap 的 FormalArmy / Residual 战略人口。
        /// 返回是否发生变化（Added / Removed）。只改变 LocalMap occupant + presentation，
        /// 不修改 WorldMotion / WorldPresence / PlayerParty。
        /// </summary>
        public bool ReconcileLoadedStrategicPopulation()
        {
            if (!_session.IsInitialized)
                return false;

            var world = _session.World;
            ResolveLoadedStrategicBounds(world);
            var result = LoadedStrategicPopulationMaterializer.ReconcileLoadedStrategicPopulation(
                world,
                _session.PlayerParty,
                _loadedStrategicWildernessBounds,
                _loadedStrategicSiteBounds);
            return result.Changed;
        }

        /// <summary>Phase 5S-B2-3.1：reconcile + 条件视图刷新（Changed 才 Refresh/Spawn/Prune）。</summary>
        public void RefreshLoadedStrategicPopulation()
        {
            if (!_session.IsInitialized || entityViewSpawner == null)
                return;
            if (!ReconcileLoadedStrategicPopulation())
                return;

            _session.RefreshViewableEntityIds();
            entityViewSpawner.SpawnMissingVisibleViews(_session);
            entityViewSpawner.PruneHiddenViews(_session);
        }

        public void StepTick()
        {
            if (!_session.IsInitialized)
                return;
            if (StrategicClockFreezeService.IsWorldTickFrozen(_session.World))
            {
                RefreshStatus();
                return;
            }

            var tick = _session.TickOnce();
            if (tick.IsFailure)
            {
                _status = "TICK FAILED: " + tick.Error;
                Debug.LogError("[PlayableHost] " + tick.Error, this);
                return;
            }

            // Phase 5S-B2-3.5：PlayerParty pursuit tick（Core 无 party runtime，故在 Host 驱动）。
            // TickOnce 内 ArmyHexTravelService.AdvanceAll 与 PlayerPartyHexTravelService.AdvanceAll
            // 均已推进 → target.CurrentHex 为最新；先检查 contact（进入 SupportArea 即接战），
            // 未接触则 target 移动 / Player 停下时自动 retarget。
            PlayerPartyHexPursuitService.AfterTravelTick(_session.World, _session.PlayerParty);

            // Phase 5S-B2-3.1：FormalArmy 世界旅行在 TickOnce 内推进 → 移入 / 移出当前 Hex
            // 后下一 tick 战略人口自动出现 / 消失（只 changed 才刷新视图）。
            var strategicPopulationChanged = ReconcileLoadedStrategicPopulation();
            if (strategicPopulationChanged)
            {
                _session.RefreshViewableEntityIds();
                entityViewSpawner?.SpawnMissingVisibleViews(_session);
            }

            // 尸体腐烂后立刻从 LocalMap 卸表现（大地图靠 WorldPresence 已抹
            entityViewSpawner?.PruneHiddenViews(_session);
            FlushLoadedDestinationArrivals();

            DispatchDrainedEvents();
            RefreshStatus();
        }

        /// <summary>Host 表现层触发的 Content／Quest 事件立即送给打断呈现/summary>
        public void DispatchDrainedEvents()
        {
            if (_session?.World?.Events == null)
                return;
            var drained = _session.World.Events.Drain();
            for (var i = 0; i < drained.Count; i++)
            {
                var evt = drained[i];
                if (evt?.Type == XianXia.Core.Events.EventType.CombatantDefeated &&
                    evt.Target.HasValue)
                {
                    // 只同步遭遇敌军伤亡；敌清FieldCleared（无结算弹窗、不卸图、不弹大地图
                    StrategicEncounterSpawner.OnCombatantDefeated(
                        _session.World,
                        evt.Target.Value);
                }
            }

            if (contentInterrupt != null)
                contentInterrupt.Ingest(drained);
            if (questJournal != null)
                questJournal.Ingest(drained);
            if (eventFeed != null)
                eventFeed.Ingest(drained);
        }

        public void Resume()
        {
            if (!_session.IsInitialized)
                return;
            if (contentInterrupt != null && contentInterrupt.HasBlockingInterrupt)
                return;
            if (strategicInterrupt != null && strategicInterrupt.HasBlockingInterrupt)
                return;
            _session.IsPaused = false;
            RefreshStatus();
        }

        public void Pause()
        {
            if (!_session.IsInitialized)
                return;
            _session.IsPaused = true;
            RefreshStatus();
        }

        void RefreshStatus()
        {
            if (!_session.IsInitialized)
            {
                _status = "Not initialized";
                return;
            }

            var day = _session.CurrentDayClock;
            var selected = selectionController != null ? selectionController.State.Count : 0;
            var cmd = commandBridge != null ? commandBridge.LastStatus : "-";
            var speed = debugHud != null ? debugHud.SpeedMultiplier : 1;
            _status = "tick=" + _session.World.Tick.Value +
                      " day=" + day.DayIndex +
                      " tickInDay=" + day.TickInDay +
                      " hour=" + day.HourOfDay +
                      " paused=" + _session.IsPaused +
                      " speed=" + speed + "x" +
                      " chars=" + _session.CharacterIds.Count +
                      " selected=" + selected +
                      " cmd=" + cmd;
        }

        /// <summary>
        /// Phase 5R-B3C1：NewGame 初始 Site 的第一次 Bootstrap 在启动链（TryInitialize）真正执行。
        /// Authored StartLocation → WorldSiteSpatialMapping.LocalToWorld → Canonical WorldPosition。
        /// 复用 Materialize BootstrapFromAuthoredLocal（不复制 mapping）；成功才消费 token，失败不消费。
        /// </summary>
        void TryRunInitialSiteBootstrap()
        {
            if (!_session.IsInitialized || !_session.InitialBootstrapPending)
                return;
            var world = _session.World;
            var motion = world?.PlayerPartyTravel;
            if (motion == null)
                return;
            var atWorldSite = motion.LocationKind == PlayerPartyLocationKind.AtWorldSite;
            if (!SiteMaterializeModeResolver.ShouldRunStartupBootstrap(
                    _session.InitialBootstrapPending,
                    _session.InitialBootstrapSiteId,
                    world.PartyWorld?.SiteId ?? string.Empty,
                    atWorldSite))
                return;

            var mapId = world.PartyWorld?.LocalMapId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(mapId))
            {
                Debug.LogError(
                    "[PlayableHost] Initial site bootstrap: no PartyWorld.LocalMapId", this);
                return; // 不消费
            }
            world.LocalMap.ActiveMapLayoutId = mapId;
            world.LocalMap.OverworldMapLayoutId = mapId;

            var walk = ResolveWalkGrid();
            if (walk == null)
            {
                Debug.LogError(
                    "[PlayableHost] Initial site bootstrap: no walk grid for " + mapId, this);
                return; // 不消费
            }
            var siteBounds = WorldSiteSpatialMapping.WorldSiteLocalMapBounds.FromOriginSize(
                walk.OriginX, walk.OriginY, walk.CellSize, walk.Width, walk.Height);

            var result = PlayerPartyLocalMapMaterializationService.MaterializePartyOnResolvedLocalMap(
                world,
                _session.PlayerParty.Members,
                null,
                siteBounds,
                PlayerPartySiteMaterializeMode.BootstrapFromAuthoredLocal);
            if (SiteMaterializeModeResolver.ShouldConsumeBootstrap(true, result.IsSuccess))
            {
                _session.ConsumeInitialBootstrap();
                PlayerPartySiteIngressTrace.Log(
                    "StartupBootstrapCommitted",
                    "site=" + (world.PartyWorld?.SiteId ?? string.Empty) +
                    " world=" + motion.WorldPosition);
                _session.RefreshViewableEntityIds();
                entityViewSpawner?.Rebuild(_session);
                if (selectionController != null && _session.PlayerParty.HasActive)
                    selectionController.SelectEntity(_session.PlayerParty.ActiveCharacterId, false);
            }
            else
            {
                Debug.LogError(
                    "[PlayableHost] Initial site bootstrap FAILED (token NOT consumed): " + result.Error,
                    this);
                PlayerPartySiteIngressTrace.Log(
                    "StartupBootstrapFailed",
                    "site=" + (world.PartyWorld?.SiteId ?? string.Empty) +
                    " error=" + result.Error);
            }
        }

        WalkGrid ResolveWalkGrid()
        {
            if (MapLayoutPick.TryGet(_session, out var preferred) && preferred != null)
            {
                var grid = MapLayoutWalkGridBuilder.Create(preferred);
                Debug.Log(
                    "[PlayableHost] WalkGrid from mapLayout " + preferred.Id +
                    " " + preferred.Width + "x" + preferred.Height +
                    " origin=(" + preferred.OriginX + "," + preferred.OriginY + ")" +
                    " blockedCells=" + grid.BlockedCount,
                    this);
                return grid;
            }

            Debug.Log("[PlayableHost] WalkGrid fallback Ch01ReferenceWalkGrid", this);
            return Ch01ReferenceWalkGrid.Create();
        }

        bool ApplyMapLayoutOverrides(out string error)
        {
            error = string.Empty;
            if (_session?.Registry == null)
            {
                error = "Session registry missing.";
                return false;
            }

            Result<MapLayoutDefinition> loaded = default;
            var hasOverride = false;

            if (!string.IsNullOrWhiteSpace(mapLayoutFilePath))
            {
                var path = ResolveMapLayoutPath(mapLayoutFilePath.Trim());
                if (!File.Exists(path))
                {
                    // Pre-subdir scenes pointed at Data/ch01_*.json; true source is Data/Maps/.
                    var mapsSibling = Path.Combine(
                        Path.GetDirectoryName(path) ?? string.Empty,
                        "Maps",
                        Path.GetFileName(path));
                    if (File.Exists(mapsSibling))
                        path = mapsSibling;
                }

                loaded = MapLayoutJsonLoader.LoadFromFile(path, preferredMapLayoutId);
                hasOverride = true;
            }
            else if (mapLayoutJsonOverride != null && !string.IsNullOrWhiteSpace(mapLayoutJsonOverride.text))
            {
                loaded = MapLayoutJsonLoader.LoadFromText(
                    mapLayoutJsonOverride.text,
                    preferredMapLayoutId,
                    mapLayoutJsonOverride.name);
                hasOverride = true;
            }

            if (!hasOverride)
                return true;

            if (loaded.IsFailure)
            {
                error = loaded.Error.ToString();
                return false;
            }

            var upsert = _session.Registry.UpsertMapLayout(loaded.Value);
            if (upsert.IsFailure)
            {
                error = upsert.Error.ToString();
                return false;
            }

            _session.PreferredMapLayoutId = loaded.Value.Id.ToString();
            if (string.IsNullOrWhiteSpace(preferredMapLayoutId))
                preferredMapLayoutId = _session.PreferredMapLayoutId;

            Debug.Log(
                "[PlayableHost] mapLayout override → " + loaded.Value.Id +
                " " + loaded.Value.Width + "x" + loaded.Value.Height +
                " placements=" + (loaded.Value.Placements?.Count ?? 0),
                this);
            return true;
        }

        static string ResolveMapLayoutPath(string raw)
        {
            if (Path.IsPathRooted(raw))
                return Path.GetFullPath(raw);
#if UNITY_EDITOR
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, raw.Replace('/', Path.DirectorySeparatorChar)));
#else
            return Path.GetFullPath(Path.Combine(Application.dataPath, raw));
#endif
        }

        public bool TryResolveContentPackageDirectory(out string path, out string error)
        {
            if (!string.IsNullOrWhiteSpace(contentPackageDirectoryOverride))
            {
                path = Path.GetFullPath(contentPackageDirectoryOverride.Trim());
                if (Directory.Exists(path) && File.Exists(Path.Combine(path, "manifest.json")))
                {
                    error = string.Empty;
                    return true;
                }

                error =
                    "Content override path invalid or missing manifest.json: " + path +
                    ". Host initialization stopped (no silent empty data).";
                return false;
            }

            return TryResolveEditorBaseGamePath(out path, out error);
        }

        /// <summary>Editor-only default: repository Content/BaseGame next to Assets/.</summary>
        public static bool TryResolveEditorBaseGamePath(out string path, out string error)
        {
            path = string.Empty;
            error = string.Empty;

#if UNITY_EDITOR
            var editorPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Content", "BaseGame"));
            if (Directory.Exists(editorPath) && File.Exists(Path.Combine(editorPath, "manifest.json")))
            {
                path = editorPath;
                return true;
            }

            error =
                "Editor Content/BaseGame not found or missing manifest.json. Expected: " + editorPath +
                ". Host initialization stopped (no silent empty data).";
            return false;
#else
            error =
                "VS0.4 Phase A supports Editor PlayMode Content/BaseGame only. " +
                "StreamingAssets player packaging is out of this phase.";
            return false;
#endif
        }
    }
}
