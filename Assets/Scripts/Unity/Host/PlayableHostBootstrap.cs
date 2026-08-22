using System.Collections.Generic;
using System.IO;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Events;
using XianXia.Core.Navigation;
using XianXia.Core.Results;
using XianXia.Core.World;
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
        [Tooltip("选中的 mapLayout JSON（相对工程根或绝对路径）。点 Inspector 里「选择关卡地图 JSON」浏览。")]
        [SerializeField] string mapLayoutFilePath = "";
        [Tooltip("空则用 base:scenario_ch01_reference（Level Tester 默认第一章，洞府残影在洞内）。")]
        [SerializeField] string openingScenarioId = "base:scenario_ch01_reference";
        [Header("Level Tester · 人物名册")]
        [Tooltip("人物编辑器导出的 characterRoster id。有则按名册刷人（非 Unity 场景摆放）。空＝用剧本 spawns。")]
        [SerializeField] string characterRosterId = "base:roster_level_tester";
        [HideInInspector]
        [SerializeField] string preferredMapLayoutId = "";
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
        [SerializeField] HostContentDebugPanel contentDebugPanel;
        [SerializeField] HostEventFeed eventFeed;
        [SerializeField] HostSnapshotPanel snapshotPanel;
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
        [SerializeField] HostWorldTravelConfirmPrompt worldTravelConfirm;
        [SerializeField] HostWorldTravelDeparture worldTravelDeparture;
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
        [SerializeField] HostNpcScheduleMover npcScheduleMover;
        [SerializeField] HostNpcContextMenu npcContextMenu;

        [Header("Tick debug")]
        [SerializeField] bool initializeOnPlay = true;
        [SerializeField] bool autoTickWhenUnpaused = true;
        [Tooltip("1x 下每 Tick 现实秒数。1 tick=5 游戏分；默认 1s → 1 现实秒=5 游戏分，5x=25 游戏分/秒。")]
        [SerializeField] float secondsPerAutoTickAt1x = SimulationTickPacing.SecondsPerTickAt1x;
        [SerializeField] KeyCode togglePauseKey = KeyCode.Space;
        [SerializeField] KeyCode stepTickKey = KeyCode.Period;
        [SerializeField] KeyCode stepTickAltKey = KeyCode.N;
        [SerializeField] KeyCode cycleSpeedKey = KeyCode.RightBracket;
        [SerializeField] KeyCode cycleSpeedAltKey = KeyCode.LeftBracket;
        [SerializeField] KeyCode rebuildKey = KeyCode.F12;

        PlayableHostSession _session = new PlayableHostSession();
        float _autoTickAccumulator;
        string _resolvedContentPath = string.Empty;
        string _status = "Idle";

        public PlayableHostSession Session => _session;

        public EntityViewSpawner ViewSpawner => entityViewSpawner;

        public HostSelectionController SelectionController => selectionController;

        public HostCommandBridge CommandBridge => commandBridge;

        public HostDebugHud DebugHud => debugHud;

        public HostContentDebugPanel ContentDebugPanel => contentDebugPanel;

        public HostEventFeed EventFeed => eventFeed;

        public HostSnapshotPanel SnapshotPanel => snapshotPanel;

        public HostMoveController MoveController => moveController;

        public HostWorkTargetMode WorkTargetMode => workTargetMode;

        public HostContentInterruptPresenter ContentInterrupt => contentInterrupt;

        public HostStrategicInterruptPresenter StrategicInterrupt => strategicInterrupt;

        public HostDialoguePresenter DialoguePresenter => dialoguePresenter;

        public HostQuestJournal QuestJournal => questJournal;

        public HostInventoryPanel InventoryPanel => inventoryPanel;

        public HostWorldMapPanel WorldMapPanel => worldMapPanel;

        public HostWorldTravelConfirmPrompt WorldTravelConfirm => worldTravelConfirm;

        public HostWorldTravelDeparture WorldTravelDeparture => worldTravelDeparture;

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
            if (contentDebugPanel == null)
                contentDebugPanel = GetComponent<HostContentDebugPanel>() ??
                                   GetComponentInChildren<HostContentDebugPanel>();
            if (eventFeed == null)
                eventFeed = GetComponent<HostEventFeed>() ?? GetComponentInChildren<HostEventFeed>();
            if (snapshotPanel == null)
                snapshotPanel = GetComponent<HostSnapshotPanel>() ?? GetComponentInChildren<HostSnapshotPanel>();
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
            if (worldTravelConfirm == null)
                worldTravelConfirm = GetComponent<HostWorldTravelConfirmPrompt>() ??
                                    GetComponentInChildren<HostWorldTravelConfirmPrompt>();
            if (worldTravelDeparture == null)
                worldTravelDeparture = GetComponent<HostWorldTravelDeparture>() ??
                                      GetComponentInChildren<HostWorldTravelDeparture>();
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
            if (GetComponent<LevelTesterHud>() == null &&
                (!string.IsNullOrWhiteSpace(preferredMapLayoutId) ||
                 !string.IsNullOrWhiteSpace(mapLayoutFilePath) ||
                 mapLayoutJsonOverride != null))
                gameObject.AddComponent<LevelTesterHud>();

            if (initializeOnPlay)
                TryInitialize();
        }

        void Update()
        {
            if (!_session.IsInitialized)
                return;

            if (Input.GetKeyDown(togglePauseKey))
            {
                if ((questJournal == null || !questJournal.IsOpen) &&
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
                    _session.IsPaused = !_session.IsPaused;
                RefreshStatus();
            }

            if (Input.GetKeyDown(stepTickKey) || Input.GetKeyDown(stepTickAltKey))
            {
                if ((questJournal == null || !questJournal.IsOpen) &&
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
                    StepTick();
            }

            if (Input.GetKeyDown(cycleSpeedKey) || Input.GetKeyDown(cycleSpeedAltKey))
            {
                if (contentInterrupt == null || !contentInterrupt.HasBlockingInterrupt)
                {
                    if (debugHud != null)
                        debugHud.CycleSpeed();
                    RefreshStatus();
                }
            }

            if (Input.GetKeyDown(rebuildKey))
                TryInitialize();

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

        /// <summary>ADR-0023：Resolve 后恢复开战前倍速。</summary>
        public void ApplySavedSpeedMultiplier(int multiplier)
        {
            EnsureDebugHud();
            if (debugHud == null)
                return;
            var m = multiplier < 1 ? 1 : multiplier;
            debugHud.SetSpeedMultiplier(m);
        }

        /// <summary>
        /// 顶栏 1x／2x／5x／20x：统一改 Host 倍速。
        /// Tick 驱动的工作／休息／吃饭／修炼／作息与表现层移动共用此倍率。
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

        /// <summary>
        /// 表现层帧间隔：受暂停与 Host 倍速影响（移动／分离等）。
        /// Core 行动进度靠 Tick（已按倍速推进）；连续位移必须用同一倍率。
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
            if (contentDebugPanel == null)
                contentDebugPanel = GetComponent<HostContentDebugPanel>() ??
                                   gameObject.AddComponent<HostContentDebugPanel>();
            if (eventFeed == null)
                eventFeed = GetComponent<HostEventFeed>() ?? gameObject.AddComponent<HostEventFeed>();
            if (snapshotPanel == null)
                snapshotPanel = GetComponent<HostSnapshotPanel>() ?? gameObject.AddComponent<HostSnapshotPanel>();
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
            if (worldTravelConfirm == null)
                worldTravelConfirm = GetComponent<HostWorldTravelConfirmPrompt>() ??
                                    gameObject.AddComponent<HostWorldTravelConfirmPrompt>();
            if (worldTravelDeparture == null)
                worldTravelDeparture = GetComponent<HostWorldTravelDeparture>() ??
                                      gameObject.AddComponent<HostWorldTravelDeparture>();
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
            if (worldTravelConfirm != null)
                worldTravelConfirm.ClearSessionState();
            if (worldTravelDeparture != null)
                worldTravelDeparture.ClearSessionState();
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
                selectionController.SelectEntity(_session.CharacterIds[0], false);
            feedbackOverlay.Bind(cam);
            commandBridge.Bind(_session, selectionController, feedbackOverlay);
            var workLoop = GetComponent<HostWorkLoop>();
            if (workLoop != null)
                workLoop.Bind(this, commandBridge, moveController);
            debugHud.Bind(this, selectionController);
            contentDebugPanel.Bind(this, selectionController);
            moveController.Bind(this, selectionController, entityViewSpawner, commandBridge, npcContextMenu);
            var pathPreview = GetComponent<HostPartyPathPreview>();
            if (pathPreview != null)
                pathPreview.Bind(this, moveController, selectionController, cam);
            moveController.SetWalkGrid(ResolveWalkGrid());
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
            if (worldTravelConfirm != null)
                worldTravelConfirm.Bind(this);
            if (worldTravelDeparture != null)
                worldTravelDeparture.Bind(this);
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
            snapshotPanel.Bind(this);
            // Bootstrap already published WorldInitialized／EntityCreated — capture once.
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
            return true;
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
            if (contentDebugPanel == null)
                contentDebugPanel = GetComponent<HostContentDebugPanel>() ??
                                   gameObject.AddComponent<HostContentDebugPanel>();
            if (eventFeed == null)
                eventFeed = GetComponent<HostEventFeed>() ?? gameObject.AddComponent<HostEventFeed>();

            selectionController.ClearSelection();
            entityViewSpawner.Clear();
            MapLayoutPresentationSync.Apply(_session);
            entityViewSpawner.Rebuild(_session);
            var cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
            selectionController.Bind(entityViewSpawner, cam);
            selectionController.SetPartyFilter(_session.CharacterIds);
            commandBridge.Bind(_session, selectionController);
            debugHud.Bind(this, selectionController);
            contentDebugPanel.Bind(this, selectionController);
            eventFeed.Clear();
            DispatchDrainedEvents();
            FrameCameraOnSlots();
            _autoTickAccumulator = 0f;
            RefreshStatus();
        }

        void FrameCameraOnSlots()
        {
            if (cameraRig == null)
                return;

            // 进出洞府：优先对准可见己方，避免整图中心与落点错位
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

        /// <summary>LocalMap 进出后：切 PreferredMapLayout、重建灰盒／实体／寻路。</summary>
        /// <param name="frameCamera">勘查显形等轻量刷新应传 false，避免镜头乱跳。</param>
        public void ReloadLocalMapPresentation(bool frameCamera = true)
        {
            if (!_session.IsInitialized)
                return;

            var active = _session.World.LocalMap.ActiveMapLayoutId;
            if (!string.IsNullOrWhiteSpace(active))
                _session.PreferredMapLayoutId = active.Trim();

            MapLayoutPresentationSync.Apply(_session);
            if (entityViewSpawner != null)
                entityViewSpawner.Rebuild(_session);
            if (mapGraybox != null)
                mapGraybox.Rebuild(_session);
            if (interactSpotPresenter != null)
                interactSpotPresenter.Rebuild();
            if (moveController != null)
                moveController.SetWalkGrid(ResolveWalkGrid());
            if (frameCamera)
                FrameCameraOnSlots();
            RefreshStatus();
        }

        /// <summary>显式清空 Active LocalMap 表现（进入场景失败／无目标图时用）。全员上路时不要调用。</summary>
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

            world.LocalMap.ActiveMapLayoutId = string.Empty;
            world.LocalMap.OverworldMapLayoutId = string.Empty;
            preferredMapLayoutId = string.Empty;
            _session.PreferredMapLayoutId = string.Empty;
            if (entityViewSpawner != null)
                entityViewSpawner.Clear();
            if (mapGraybox != null)
                mapGraybox.Rebuild(_session);
            if (interactSpotPresenter != null)
                interactSpotPresenter.Rebuild();
            if (moveController != null)
                moveController.SetWalkGrid(null);
            RefreshStatus();
        }

        /// <summary>
        /// WorldGraph 到站后：按 PartyWorld.LocalMapId 卸／装实体图；切换 localPlaceSet；队伍落到 startLocation。
        /// </summary>
        /// <param name="closeWorldMap">从大地图「进入场景」时应为 true，关掉全屏地图页。</param>
        public void ApplyPartyWorldNodePresentation(bool closeWorldMap = false)
        {
            if (closeWorldMap && worldMapPanel != null)
                worldMapPanel.Close();

            if (!_session.IsInitialized)
                return;

            var world = _session.World;
            var targetMap = world.PartyWorld.LocalMapId ?? string.Empty;
            if (BattleOfferService.HasActiveManualEncounter(world))
            {
                targetMap = BattleOfferService.ResolveActiveEncounterLocalMapId(world);
                world.PartyWorld.LocalMapId = targetMap;
            }
            var inStrategicEncounter = world.Strategic?.Encounter != null &&
                                         !world.Strategic.Encounter.BattlefieldLingering &&
                                         (world.Strategic.Encounter.SpawnOnNextMapLoad ||
                                          world.Strategic.Encounter.SpawnedEntityIds.Count > 0 ||
                                          !string.IsNullOrEmpty(world.PartyWorld.EncounterId));
            var onEncounterMap = BattleOfferService.HasActiveManualEncounter(world) &&
                                 !string.IsNullOrWhiteSpace(targetMap) &&
                                 string.Equals(
                                     targetMap.Trim(),
                                     StrategicEncounterCatalog.DefaultEncounterLocalMapId,
                                     System.StringComparison.Ordinal);

            // 目标图上暂无我方（例如全员已上路）：保持当前 LocalMap 画面，禁止卸图把视线带走
            if (!string.IsNullOrWhiteSpace(targetMap) &&
                !(world.Strategic?.Encounter != null && world.Strategic.Encounter.SpawnOnNextMapLoad) &&
                !LocalMapVisibility.CanLoadMapLayoutForParty(
                    world, _session.CharacterIds, targetMap.Trim()))
            {
                RefreshStatus();
                return;
            }

            // 目标图必须在内容包里，否则禁止带着荒村图「假装切换」
            if (!string.IsNullOrWhiteSpace(targetMap))
            {
                var parsedMap = XianXia.Core.Domain.Ids.DefinitionId.Parse(targetMap.Trim());
                if (parsedMap.IsFailure ||
                    !_session.Registry.TryGetMapLayout(parsedMap.Value, out _))
                {
                    Debug.LogError(
                        "[PlayableHost] LocalMap missing in registry: " + targetMap +
                        " — 无法进入该节点场景（请确认 Content 已含保底图）。",
                        this);
                    RefreshStatus();
                    return;
                }
            }

            var places = WorldRegionBootstrap.ActivatePlacesForMapLayout(
                world, _session.Registry, targetMap);
            if (places.IsFailure)
                Debug.LogWarning("[PlayableHost] ActivatePlaces: " + places.Error, this);

            // 仅把仍在当前焦点 Node 上的己方落到该图 startLocation（已去别处的人不动）
            var focusNode = world.PartyWorld.NodeId;
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
                     wp.Mode == XianXia.Core.World.PartyWorldPresenceMode.Traveling))
                    continue;
                var engagedInEncounter = filterEngaged && encounter.IsEngaged(id);
                if (!engagedInEncounter &&
                    wp != null &&
                    !string.IsNullOrEmpty(focusNode) &&
                    !string.Equals(wp.NodeId, focusNode, System.StringComparison.Ordinal))
                    continue;
                if (wp != null && wp.Mode == XianXia.Core.World.PartyWorldPresenceMode.Traveling)
                {
                    if (!onEncounterMap)
                        continue;
                    wp.Mode = XianXia.Core.World.PartyWorldPresenceMode.InEncounter;
                }
                else if (wp != null &&
                         wp.Mode == XianXia.Core.World.PartyWorldPresenceMode.RouteAnchored &&
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
                    // 无地点表时仍落在表现原点，保证能刷出实体
                    loc.LocationId = string.Empty;
                    loc.SetPresentationOverride(0f, 0f);
                }
            }

            if (string.IsNullOrWhiteSpace(targetMap))
            {
                // 焦点图为空但画面仍在：保留当前 LocalMap（全员上路时视线不带走）
                if (!string.IsNullOrWhiteSpace(world.LocalMap.ActiveMapLayoutId))
                {
                    RefreshStatus();
                    return;
                }

                UnloadActiveLocalMapPresentation(clearEmptyEncounter: false);
                return;
            }

            preferredMapLayoutId = targetMap;
            _session.PreferredMapLayoutId = targetMap;
            world.LocalMap.ActiveMapLayoutId = targetMap;
            world.LocalMap.OverworldMapLayoutId = targetMap;
            ReloadLocalMapPresentation(frameCamera: true);

            var spawned = StrategicEncounterSpawner.ApplyPending(world);
            if (spawned.IsFailure)
                Debug.LogWarning("[PlayableHost] Strategic encounter spawn: " + spawned.Error, this);
            if (onEncounterMap)
            {
                StrategicEncounterSpawner.EnsureTrackedSpawnsLocalPresentation(world);
                _session.RefreshViewableEntityIds();
                entityViewSpawner?.Rebuild(_session);
            }
            else if (world.Strategic.Encounter.SpawnedEntityIds.Count > 0)
            {
                _session.RefreshViewableEntityIds();
                entityViewSpawner?.Rebuild(_session);
            }

            // 切图后再对齐一次地点坐标（MapLayout sync 之后）并选中在场角色
            if (!string.IsNullOrEmpty(startId) && world.WorldRegion.TryGet(startId, out var syncedStart))
            {
                for (var i = 0; i < _session.CharacterIds.Count; i++)
                {
                    var id = _session.CharacterIds[i];
                    if (filterEngaged && !encounter.IsEngaged(id))
                        continue;
                    if (!LocalMapVisibility.IsEntityVisible(world, id))
                        continue;
                    if (!world.Entities.TryGet(id, out var ent) ||
                        !ent.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc))
                        continue;
                    loc.LocationId = startId;
                    loc.SetPresentationOverride(syncedStart.PresentationX, syncedStart.PresentationZ);
                }

                entityViewSpawner?.SyncLocations(_session);
                if (selectionController != null)
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

                TryFrameCameraOnParty();
            }
        }

        /// <summary>残留战场：存活角色「查看」弥留同伴／再入接战 LocalMap。</summary>
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

            var rt = world.Strategic.Encounter;
            var mapId = BattleOfferService.ResolveActiveEncounterLocalMapId(world);
            StrategicEncounterSpawner.PlanManualEncounter(
                world,
                rt.ArmyStackId,
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
            ApplyPartyWorldNodePresentation(closeWorldMap: true);
        }

        /// <summary>仅重刷地表戳（如勘查显形），不重建实体、不挪镜头。</summary>
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

            // 尸体腐烂后立刻从 LocalMap 卸表现（大地图靠 WorldPresence 已抹）
            entityViewSpawner?.PruneHiddenViews(_session);

            DispatchDrainedEvents();
            RefreshStatus();
        }

        /// <summary>Host 表现层触发的 Content／Quest 事件立即送给打断呈现。</summary>
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
                    // 只同步遭遇敌军伤亡；敌清空 → FieldCleared（无结算弹窗、不卸图、不弹大地图）
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
