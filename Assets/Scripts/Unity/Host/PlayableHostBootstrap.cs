using System.Collections.Generic;
using System.IO;
using UnityEngine;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Events;
using XianXia.Core.Navigation;
using XianXia.Core.Results;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
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
        [Tooltip("1x 下每 Tick 现实秒数。1 tick=5 游戏分；默认 1s → 1 现实秒=5 游戏分，5x=25 游戏分/秒。")]
        [SerializeField] string mapLayoutFilePath = "";
        [Tooltip("1x 下每 Tick 现实秒数。1 tick=5 游戏分；默认 1s → 1 现实秒=5 游戏分，5x=25 游戏分/秒。")]
        [SerializeField] string openingScenarioId = "base:scenario_ch01_reference";
        [Header("Level Tester · 人物名册")]
        [Tooltip("1x 下每 Tick 现实秒数。1 tick=5 游戏分；默认 1s → 1 现实秒=5 游戏分，5x=25 游戏分/秒。")]
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
        [SerializeField] HostLevelTesterCheatPanel levelTesterCheatPanel;
        [SerializeField] HostEventFeed eventFeed;
        [SerializeField] HostMapGraybox mapGraybox;
        [SerializeField] HostMoveController moveController;
        [SerializeField] HostActionMenu actionMenu;
        [SerializeField] HostFormalHud formalHud;
        [SerializeField] HostActivityPresenter activityPresenter;
        [SerializeField] HostCrowdPresenter crowdPresenter;
        [SerializeField] HostFeedbackOverlay feedbackOverlay;
        [SerializeField] HostSocialNotificationOverlay socialNotificationOverlay;
        [SerializeField] HostWorkTargetMode workTargetMode;
        [SerializeField] HostContentInterruptPresenter contentInterrupt;
        [SerializeField] HostStrategicInterruptPresenter strategicInterrupt;
        [SerializeField] HostDialoguePresenter dialoguePresenter;
        [SerializeField] HostQuestJournal questJournal;
        [SerializeField] HostWorldActivityPanel worldActivityPanel;
        [SerializeField] HostInventoryPanel inventoryPanel;
        [SerializeField] HostConstructionPanel constructionPanel;
        [SerializeField] HostWorldMapPanel worldMapPanel;
        [SerializeField] HostManualLearnPrompt manualLearnPrompt;
        [SerializeField] HostCombatArtLearnPrompt combatArtLearnPrompt;
        [SerializeField] HostCombatArtsPanel combatArtsPanel;
        [SerializeField] HostCultivationPanel cultivationPanel;
        [SerializeField] HostCharacterSheetPanel characterSheetPanel;
        [SerializeField] HostCultivateConfirmPrompt cultivateConfirm;
        [SerializeField] HostBreakthroughRitual breakthroughRitual;
        [SerializeField] HostSkillStudyRitual skillStudyRitual;
        [SerializeField] HostTicTacToePanel ticTacToePanel;
        [SerializeField] HostCaveSurveyPresenter caveSurveyPresenter;
        [SerializeField] HostInteractSpotPresenter interactSpotPresenter;
        [SerializeField] HostNpcScheduleMover npcScheduleMover;
        [SerializeField] HostNpcSquadContinuousPresenter npcSquadContinuousPresenter;
        [SerializeField] HostNpcContextMenu npcContextMenu;

        [Header("Tick debug")]
        [SerializeField] bool initializeOnPlay = true;
        [SerializeField] bool autoTickWhenUnpaused = true;
        [Tooltip("1x 下每 Tick 现实秒数。1 tick=5 游戏分；默认 1s → 1 现实秒=5 游戏分，5x=25 游戏分/秒。")]
        [SerializeField] float secondsPerAutoTickAt1x = SimulationTickPacing.SecondsPerTickAt1x;
        [SerializeField] KeyCode togglePauseKey = KeyCode.Space;

        PlayableHostSession _session = new PlayableHostSession();
        ContinuousOutdoorSurfaceRuntime _continuousOutdoorSurfaceRuntime;
        HostDynamicOpportunityObjectPresenter _dynamicOpportunityObjectPresenter;
        readonly OutdoorEntityReconcileGate _outdoorEntityReconcileGate =
            new OutdoorEntityReconcileGate();
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

        /// <summary>NPC 日程寻路驱动（诊断面板读性能计数用）。</summary>
        public HostNpcScheduleMover NpcScheduleMover => npcScheduleMover;

        public HostSelectionController SelectionController => selectionController;

        public HostCommandBridge CommandBridge => commandBridge;

        public HostDebugHud DebugHud => debugHud;

        public HostLevelTesterCheatPanel LevelTesterCheatPanel => levelTesterCheatPanel;

        public HostEventFeed EventFeed => eventFeed;

        public HostMoveController MoveController => moveController;

        public ContinuousOutdoorSurfaceRuntime ContinuousOutdoorSurfaceRuntime => _continuousOutdoorSurfaceRuntime;
        public HostDynamicOpportunityObjectPresenter DynamicOpportunityObjectPresenter => _dynamicOpportunityObjectPresenter;

        public HostPlayerPartyController PlayerPartyController =>
            GetComponent<HostPlayerPartyController>();

        public HostWorkTargetMode WorkTargetMode => workTargetMode;

        public HostContentInterruptPresenter ContentInterrupt => contentInterrupt;

        public HostStrategicInterruptPresenter StrategicInterrupt => strategicInterrupt;

        public HostDialoguePresenter DialoguePresenter => dialoguePresenter;

        public HostQuestJournal QuestJournal => questJournal;

        public HostWorldActivityPanel WorldActivityPanel => worldActivityPanel;

        public HostInventoryPanel InventoryPanel => inventoryPanel;

        public HostConstructionPanel ConstructionPanel => constructionPanel;

        public HostWorldMapPanel WorldMapPanel => worldMapPanel;

        public HostManualLearnPrompt ManualLearnPrompt => manualLearnPrompt;
        public HostCombatArtLearnPrompt CombatArtLearnPrompt => combatArtLearnPrompt;

        public HostCombatArtsPanel CombatArtsPanel => combatArtsPanel;

        public HostCultivationPanel CultivationPanel => cultivationPanel;

        public HostCharacterSheetPanel CharacterSheetPanel => characterSheetPanel;

        public HostCultivateConfirmPrompt CultivateConfirm => cultivateConfirm;

        public HostBreakthroughRitual BreakthroughRitual => breakthroughRitual;

        public HostSkillStudyRitual SkillStudyRitual => skillStudyRitual;

        public HostTicTacToePanel TicTacToePanel => ticTacToePanel;

        public HostCaveSurveyPresenter CaveSurveyPresenter => caveSurveyPresenter;

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
            if (worldActivityPanel == null)
                worldActivityPanel = GetComponent<HostWorldActivityPanel>() ??
                                     GetComponentInChildren<HostWorldActivityPanel>();
            if (inventoryPanel == null)
                inventoryPanel = GetComponent<HostInventoryPanel>() ??
                                GetComponentInChildren<HostInventoryPanel>();
            if (constructionPanel == null)
                constructionPanel = GetComponent<HostConstructionPanel>() ??
                                    GetComponentInChildren<HostConstructionPanel>();
            if (worldMapPanel == null)
                worldMapPanel = GetComponent<HostWorldMapPanel>() ??
                               GetComponentInChildren<HostWorldMapPanel>();
            if (manualLearnPrompt == null)
                manualLearnPrompt = GetComponent<HostManualLearnPrompt>() ??
                                   GetComponentInChildren<HostManualLearnPrompt>();
            if (combatArtLearnPrompt == null)
                combatArtLearnPrompt = GetComponent<HostCombatArtLearnPrompt>() ??
                                      GetComponentInChildren<HostCombatArtLearnPrompt>();
            if (npcSquadContinuousPresenter == null)
                npcSquadContinuousPresenter = GetComponent<HostNpcSquadContinuousPresenter>() ??
                                              gameObject.AddComponent<HostNpcSquadContinuousPresenter>();
            EnsureSocialNotificationOverlay();

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

        // Continuous Outdoor startup 恢复：preflight／activation 失败时 InitialBootstrap token 不消费，
        // 逐帧 retry；禁止留下 Continuous presentation 未建立的半初始化状态。
        const int ContinuousStartupRecoveryMaxAttempts = 5;
        bool _continuousStartupRecoveryPending;
        int _continuousStartupRecoveryAttempts;
        float _continuousStartupRetryAt;

        /// <summary>最近一次 Continuous startup postcondition 诊断（空 = 无问题）。</summary>
        public string ContinuousStartupPostconditionDiagnostic { get; private set; } = string.Empty;

        /// <summary>Continuous startup 失败可恢复：留下 token + 完整日志 + 下一帧 retry。</summary>
        void ScheduleContinuousStartupRecovery(string reason)
        {
            _continuousStartupRecoveryPending = true;
            _continuousStartupRetryAt = Time.unscaledTime + 0.25f;
            _status = "CONTINUOUS STARTUP PENDING: " + reason;
            Debug.LogError(
                reason + " — legacy/startup authority 保持完整，InitialBootstrap token 未消费，将重试。", this);
        }

        void TickContinuousStartupRecovery()
        {
            if (!_continuousStartupRecoveryPending)
                return;
            if (Time.unscaledTime < _continuousStartupRetryAt)
                return;
            if (_continuousStartupRecoveryAttempts >= ContinuousStartupRecoveryMaxAttempts)
            {
                _continuousStartupRecoveryPending = false;
                Debug.LogError(
                    "[ContinuousStartupRecoveryGaveUp] attempts=" + _continuousStartupRecoveryAttempts +
                    " InitialBootstrapPending=" + _session.InitialBootstrapPending, this);
                return;
            }

            _continuousStartupRecoveryAttempts++;
            _continuousStartupRetryAt = Time.unscaledTime + 1f;

            // token 仍 pending（preflight 从未通过）→ 重跑 prepare + preflight 再 commit。
            if (_session.InitialBootstrapPending &&
                TryPrepareInitialContinuousOutdoorStartup(out var plan, out _) &&
                _continuousOutdoorSurfaceRuntime != null &&
                _continuousOutdoorSurfaceRuntime.TryPreflightStartupActivation(
                    plan.Surface, plan.Chunk, out _))
                CommitInitialContinuousOutdoorStartup(plan);

            ActivateContinuousOutdoorPresentation();
            if (_continuousOutdoorSurfaceRuntime == null || !_continuousOutdoorSurfaceRuntime.IsActive)
                return;

            _continuousStartupRecoveryPending = false;
            ConsumeInitialBootstrapAfterContinuousActivation(
                startupCommitted: true, surfaceActivated: true);
            moveController?.SetWalkGrid(ResolveWalkGrid());
            // 与 normal startup 同一条 FINAL OPENING POPULATION BARRIER（但不重复绑定收尾）。
            FinalizeContinuousOutdoorOpeningPopulation();
            FrameCameraOnActiveCharacter();
            if (!_continuousOutdoorSurfaceRuntime.TryValidateOpeningPostconditions(out var postFailure))
            {
                ContinuousStartupPostconditionDiagnostic = postFailure;
                Debug.LogError("[ContinuousStartupInvariantFailure] " + postFailure, this);
            }
            Debug.Log("[ContinuousStartupRecovered] attempts=" + _continuousStartupRecoveryAttempts, this);
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

            // Continuous startup 未完成（preflight／activation 失败）：token 仍 pending，逐帧重试。
            TickContinuousStartupRecovery();


            // Phase 5R-B6.5-B：Modal 强制暂停期间 Space 不能切换（ModalHardPaused 分层）。
            if (Input.GetKeyDown(togglePauseKey) &&
                !_session.ModalHardPaused &&
                (questJournal == null || !questJournal.IsOpen) &&
                (inventoryPanel == null || !inventoryPanel.IsOpen) &&
                (constructionPanel == null || !constructionPanel.IsOpen) &&
                (manualLearnPrompt == null || !manualLearnPrompt.IsOpen) &&
                (combatArtLearnPrompt == null || !combatArtLearnPrompt.IsOpen) &&
                (combatArtsPanel == null || !combatArtsPanel.IsOpen) &&
                (cultivationPanel == null || !cultivationPanel.IsOpen) &&
                (characterSheetPanel == null || !characterSheetPanel.IsOpen) &&
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

        /// <summary>
        /// 正常 NewGame 的 Continuous Outdoor 启动事务：Prepare → Preflight → Commit → Activate。
        ///
        /// Prepare 只解析 candidate Site／Surface／Chunk／canonical anchor，**一个 Runtime 字段都不写**；
        /// Preflight 确认 neighborhood 可加载；Commit 才提交 canonical position 并清掉 legacy LocalMap
        /// authority；InitialBootstrap token 只在 Continuous Surface 真正激活之后才消费。
        ///
        /// 这样 activation 失败不会留下 Surface 未建立但 token 已消费的不可恢复 half-state。
        /// </summary>
        bool TryPrepareInitialContinuousOutdoorStartup(
            out ContinuousOutdoorStartupPlanner.StartupPlan plan, out string failure)
        {
            plan = default;
            failure = string.Empty;
            if (!_session.InitialBootstrapPending || !_session.PlayerParty.HasActive)
                return false;
            var world = _session.World;
            var motion = world?.PlayerPartyTravel;
            var siteId = _session.InitialBootstrapSiteId;
            if (motion == null || string.IsNullOrEmpty(siteId) ||
                !world.Strategic.Sites.TryGet(siteId, out var site) ||
                !WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(site))
                return false;

            if (!ContinuousOutdoorStartupPlanner.TryResolveSurfaceForSite(
                    _session.Registry, siteId, out var surface, out _))
            {
                failure = "SiteId=" + siteId + " ContinuousSurface=unresolved";
                return false;
            }
            if (surface.AcceptanceOnly)
            {
                failure = "SiteId=" + siteId + " SurfaceId=" + surface.SurfaceId + " AcceptanceOnly=true";
                return false;
            }

            // A／B：优先使用 checked-in Continuous truth（baked SitePlace → SiteRegion arrival）。
            // Normal NewGame 不再每次重算一套可能与 bake 不一致的 geometry。
            var openingLocationId = ResolveOpeningStartLocationId(world);
            if (ContinuousOutdoorStartupPlanner.TryResolveBakedAnchor(
                    surface, siteId, openingLocationId,
                    out var bakedX, out var bakedY, out var anchorSource))
            {
                plan = new ContinuousOutdoorStartupPlanner.StartupPlan(
                    siteId, surface,
                    ContinuousOutdoorStartupPlanner.WorldToChunk(surface, bakedX, bakedY),
                    bakedX, bakedY, anchorSource);
                return true;
            }

            failure = "SiteId=" + siteId + " BakedContinuousAnchor=missing";
            return false;
        }

        /// <summary>开局 canonical position 的 legacy LocationId hint；不读取或激活 WorldRegion runtime。</summary>
        string ResolveOpeningStartLocationId(SimulationWorld world)
        {
            if (world == null)
                return string.Empty;
            if (_session.PlayerParty.HasActive &&
                world.Entities.TryGet(_session.PlayerParty.ActiveCharacterId, out var active) &&
                active.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc) &&
                loc.HasLocation && !string.IsNullOrEmpty(loc.LocationId))
                return loc.LocationId;
            return world.LocalPlaces.StartLocationId ?? string.Empty;
        }

        /// <summary>
        /// 提交 canonical WorldPosition 并清掉 legacy Site／LocalMap authority。
        /// 这是 startup 事务里唯一 destructive 的一步，只能在 preflight 成功之后调用。
        /// </summary>
        void CommitInitialContinuousOutdoorStartup(in ContinuousOutdoorStartupPlanner.StartupPlan plan)
        {
            var world = _session.World;
            var motion = world?.PlayerPartyTravel;
            if (motion == null)
                return;
            var canonical = new WorldVec2(plan.CanonicalWorldX, plan.CanonicalWorldY);
            motion.SetAtSurfacePosition(plan.SurfaceId, canonical);
            motion.SetCurrentOutdoorWorldSiteContext(plan.SiteId);
            for (var i = 0; i < _session.PlayerParty.Members.Count; i++)
                world.WorldPresence.SetAtWorldPosition(
                    _session.PlayerParty.Members[i], canonical, plan.SurfaceId);
            world.PartyWorld.ClearSiteFocus();
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtWorldPosition;
            world.PartyWorld.LocalMapId = string.Empty;
            world.PartyWorld.EncounterId = string.Empty;
            world.LocalMap.ActiveMapLayoutId = string.Empty;
            world.LocalMap.OverworldMapLayoutId = string.Empty;
            _session.PreferredMapLayoutId = string.Empty;
        }

        /// <summary>
        /// InitialBootstrap token 只在 Continuous Surface 真正激活成功后消费（predicate 与
        /// ContinuousOutdoorStartupPlanner 同源）；preflight／activation 失败时 token 保留。
        /// </summary>
        void ConsumeInitialBootstrapAfterContinuousActivation(bool startupCommitted, bool surfaceActivated)
        {
            if (!ContinuousOutdoorStartupPlanner.ShouldConsumeInitialBootstrap(
                    startupCommitted, surfaceActivated))
                return;
            if (!_session.InitialBootstrapPending)
                return;
            _session.ConsumeInitialBootstrap();
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

        void EnsureSocialNotificationOverlay()
        {
            if (socialNotificationOverlay == null)
            {
                socialNotificationOverlay = GetComponent<HostSocialNotificationOverlay>() ??
                                            GetComponentInChildren<HostSocialNotificationOverlay>();
                if (socialNotificationOverlay == null)
                    socialNotificationOverlay = gameObject.AddComponent<HostSocialNotificationOverlay>();
            }
            socialNotificationOverlay.Bind(this);
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

        bool _openingPopulationBarrierApplied;

        public string MapLayoutFilePath => mapLayoutFilePath ?? "";

        public TextAsset MapLayoutJsonOverride => mapLayoutJsonOverride;

        public bool TryInitialize()
        {
            _outdoorEntityReconcileGate.Reset();
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
            EnsureSocialNotificationOverlay();
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
            if (GetComponent<HostFactionFlagAssault>() == null)
                gameObject.AddComponent<HostFactionFlagAssault>();
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
            if (worldActivityPanel == null)
                worldActivityPanel = GetComponent<HostWorldActivityPanel>() ??
                                     gameObject.AddComponent<HostWorldActivityPanel>();
            if (inventoryPanel == null)
                inventoryPanel = GetComponent<HostInventoryPanel>() ??
                                gameObject.AddComponent<HostInventoryPanel>();
            if (constructionPanel == null)
                constructionPanel = GetComponent<HostConstructionPanel>() ??
                                    gameObject.AddComponent<HostConstructionPanel>();
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
            if (GetComponent<HostSeparateSpaceExitTrigger>() == null)
                gameObject.AddComponent<HostSeparateSpaceExitTrigger>();
            if (GetComponent<HostConstructionController>() == null)
                gameObject.AddComponent<HostConstructionController>();
            if (GetComponent<HostFarmFieldConstructionPresenter>() == null)
                gameObject.AddComponent<HostFarmFieldConstructionPresenter>();
            if (GetComponent<HostRecoverySpotConstructionPresenter>() == null)
                gameObject.AddComponent<HostRecoverySpotConstructionPresenter>();
            if (GetComponent<HostFactionFlagPresenter>() == null)
                gameObject.AddComponent<HostFactionFlagPresenter>();
            if (GetComponent<HostPartyPathPreview>() == null)
                gameObject.AddComponent<HostPartyPathPreview>();

            selectionController.ClearSelection();
            entityViewSpawner.Clear();
            eventFeed.Clear();
            socialNotificationOverlay.Clear();
            contentInterrupt.ClearSessionState();
            if (strategicInterrupt != null)
                strategicInterrupt.ClearSessionState();
            if (dialoguePresenter != null)
                dialoguePresenter.ClearSessionState();
            if (npcContextMenu != null)
                npcContextMenu.ClearSessionState();
            if (questJournal != null)
                questJournal.ClearSessionState();
            if (worldActivityPanel != null)
                worldActivityPanel.ClearSessionState();
            if (inventoryPanel != null)
                inventoryPanel.ClearSessionState();
            if (constructionPanel != null)
                constructionPanel.ClearSessionState();
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
            mapGraybox.Clear();
            interactSpotPresenter.Clear();

            _openingPopulationBarrierApplied = false;
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
            if (_session.CharacterIds.Count > 0 && !_session.PlayerParty.HasActive)
                _session.PlayerParty.TryInitialize(_session.CharacterIds[0], out _);
            // A migrated Outdoor Site runs a startup **transaction**: Prepare（只读解析 candidate）
            // → Preflight（neighborhood 可加载）→ Commit（提 canonical + 清 legacy authority）。
            // token 直到 Continuous Surface 真正激活后才消费。
            _continuousOutdoorSurfaceRuntime = GetComponent<ContinuousOutdoorSurfaceRuntime>() ??
                                               gameObject.AddComponent<ContinuousOutdoorSurfaceRuntime>();
            _continuousOutdoorSurfaceRuntime.Bind(this);
            _dynamicOpportunityObjectPresenter = GetComponent<HostDynamicOpportunityObjectPresenter>() ??
                                                 gameObject.AddComponent<HostDynamicOpportunityObjectPresenter>();
            _dynamicOpportunityObjectPresenter.Bind(this);
            (GetComponent<HostCharacterEncounter>() ?? gameObject.AddComponent<HostCharacterEncounter>()).Bind(this);
            var continuousOutdoorStartup = false;
            var continuousStartupPlan = default(ContinuousOutdoorStartupPlanner.StartupPlan);
            if (TryPrepareInitialContinuousOutdoorStartup(out var preparedStartup, out var prepareFailure))
            {
                var preflightFailure = string.Empty;
                var preflightPassed = _continuousOutdoorSurfaceRuntime.TryPreflightStartupActivation(
                    preparedStartup.Surface, preparedStartup.Chunk, out preflightFailure);
                // Commit 决策与 ContinuousOutdoorStartupPlanner 同源（可无头测试）：
                // preflight 未过 → 一个 Runtime authority 字段都不改。
                if (ContinuousOutdoorStartupPlanner.ShouldCommitStartupAuthority(
                        prepareSucceeded: true, preflightPassed: preflightPassed))
                {
                    CommitInitialContinuousOutdoorStartup(preparedStartup);
                    continuousStartupPlan = preparedStartup;
                    continuousOutdoorStartup = true;
                }
                else
                {
                    ScheduleContinuousStartupRecovery(
                        "[ContinuousStartupPreflightFailure] SiteId=" + preparedStartup.SiteId +
                        " SurfaceId=" + preparedStartup.SurfaceId + " Chunk=" + preparedStartup.Chunk +
                        " Failure=" + preflightFailure);
                }
            }
            else if (!string.IsNullOrEmpty(prepareFailure))
            {
                ScheduleContinuousStartupRecovery("[ContinuousStartupPrepareFailure] " + prepareFailure);
            }
            if (!continuousOutdoorStartup && !string.IsNullOrWhiteSpace(_session.PreferredMapLayoutId))
                _session.World.LocalMap.EnsureOverworld(_session.PreferredMapLayoutId);
            else if (!continuousOutdoorStartup && MapLayoutPick.TryGet(_session, out var picked) && picked != null)
            {
                _session.PreferredMapLayoutId = picked.Id.ToString();
                _session.World.LocalMap.EnsureOverworld(_session.PreferredMapLayoutId);
            }

            var synced = continuousOutdoorStartup ? 0 : MapLayoutPresentationSync.Apply(_session);
            if (synced > 0)
                Debug.Log("[PlayableHost] Synced " + synced + " location presentation(s) from mapLayout", this);
            entityViewSpawner.Rebuild(_session);
            if (!continuousOutdoorStartup)
                mapGraybox.Rebuild(_session);
            if (!continuousOutdoorStartup && interactSpotPresenter != null)
                interactSpotPresenter.Rebuild();
            var cam = Camera.main;
            selectionController.Bind(entityViewSpawner, cam);
            selectionController.SetPartyFilter(_session.CharacterIds);
            // 开局权威位置愈合：禁止 AtWorldPosition 漂移冒充 Site。
            PlayerPartyWorldLocationQuery.TryResolve(
                _session.World, _session.PlayerParty, out _, healDrift: true);
            var playerPartyController = GetComponent<HostPlayerPartyController>() ??
                                        gameObject.AddComponent<HostPlayerPartyController>();
            playerPartyController.Bind(this);
            var housingSel = GetComponent<HostHousingAreaSelection>();
            if (housingSel != null)
                housingSel.Bind(this, selectionController, cam);
            var assault = GetComponent<HostControlCoreAssault>();
            if (assault != null)
                assault.Bind(this);
            var flagAssault = GetComponent<HostFactionFlagAssault>();
            if (flagAssault != null)
                flagAssault.Bind(this);
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
            moveController.BindLocalMapContext(_session.World.LocalMap.ActiveMapLayoutId);
            // Host-side Safe+Walkable fallback：materialize+Rebuild 之后、OnLocalMapMaterialized
            // （→RebindAllFollowers）之前 —— WalkGrid 已 ready、EntityView 已就位。
            if (continuousOutdoorStartup)
                ActivateContinuousOutdoorPresentation();
            else
                PlayerPartyController?.OnLocalMapMaterialized(
                    _session.World.LocalMap.ActiveMapLayoutId);
            if (npcContextMenu != null)
                npcContextMenu.Bind(this, selectionController, moveController, dialoguePresenter);
            var constructionController = GetComponent<HostConstructionController>();
            if (constructionController != null)
                constructionController.Bind(this);
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
            worldActivityPanel?.Bind(this);
            inventoryPanel.Bind(this);
            constructionPanel.Bind(this);
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
            cultivateConfirm.Bind(this, selectionController, commandBridge);
            if (breakthroughRitual != null)
                breakthroughRitual.Bind(this);
            if (skillStudyRitual != null)
                skillStudyRitual.Bind(this);
            if (ticTacToePanel != null)
                ticTacToePanel.Bind(this);
            if (caveSurveyPresenter != null)
                caveSurveyPresenter.Bind(this, selectionController, commandBridge);
            npcScheduleMover.Bind(this, moveController, entityViewSpawner);
            npcSquadContinuousPresenter.Bind(this);
            ActivateContinuousOutdoorPresentation();
            if (continuousOutdoorStartup)
            {
                if (_continuousOutdoorSurfaceRuntime != null && _continuousOutdoorSurfaceRuntime.IsActive)
                {
                    // 真正激活成功才消费 InitialBootstrap token。
                    ConsumeInitialBootstrapAfterContinuousActivation(
                        startupCommitted: true, surfaceActivated: true);
                }
                else
                {
                    // 真正 activation 失败（不是诊断 postcondition）：不回退成半初始化 session，
                    // 保留 token 并在下一帧 retry。
                    ScheduleContinuousStartupRecovery(
                        "[ContinuousStartupActivationFailure] SurfaceId=" + continuousStartupPlan.SurfaceId +
                        " Chunk=" + continuousStartupPlan.Chunk + " ActiveSurface=" +
                        (_continuousOutdoorSurfaceRuntime?.ActiveSurfaceId ?? string.Empty));
                }
            }
            // Bootstrap already published WorldInitialized／EntityCreated capture once.
            // §12 FINAL OPENING POPULATION BARRIER：所有 Host binding 完成后，对 opening
            // population 做唯一一次显式 reconcile + 视图补齐（绝不回到 per-tick reconcile）。
            if (continuousOutdoorStartup)
                FinalizeContinuousOutdoorOpeningPopulation();
            DispatchDrainedEvents();
            FrameCameraOnSlots();
            if (continuousOutdoorStartup)
            {
                // Normal continuous startup 不依赖 legacy LocalMap 取景 fallback：明确对准主控。
                FrameCameraOnActiveCharacter();
                // postcondition 诊断放在 Host finalize + Camera 之后：只报告，不中途 abort
                // （避免留下 IsInitialized 一半且 Camera 未定位的 poisoned session）。
                if (_continuousOutdoorSurfaceRuntime != null &&
                    !_continuousOutdoorSurfaceRuntime.TryValidateOpeningPostconditions(out var postFailure))
                {
                    ContinuousStartupPostconditionDiagnostic = postFailure;
                    Debug.LogError("[ContinuousStartupInvariantFailure] " + postFailure, this);
                }
            }

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

        /// <summary>
        /// §12：NewGame Continuous Outdoor 的 FINAL OPENING POPULATION BARRIER。
        /// 在全部 Host binding 完成后显式 reconcile 一次（loaded neighborhood → intersecting Site →
        /// nearby population），然后 RefreshViewableEntityIds / PruneHiddenViews / SpawnMissingVisibleViews。
        /// 只做一次：不恢复「every world tick → full population reconcile」（会重新引入 NPC 拖动与性能问题）。
        /// </summary>
        public void FinalizeContinuousOutdoorOpeningPopulation()
        {
            if (_session == null || !_session.IsInitialized)
                return;
            _continuousOutdoorSurfaceRuntime?.ReconcileOutdoorEntityMaterializationForScopeChange();
            CommitOutdoorEntityReconcileGate();
            if (entityViewSpawner == null)
            {
                _openingPopulationBarrierApplied = true;
                return;
            }

            _session.RefreshViewableEntityIds();
            entityViewSpawner.PruneHiddenViews(_session);
            entityViewSpawner.SpawnMissingVisibleViews(_session);
            EnsureActiveSelectionAfterPresentationMaterialized();
            _openingPopulationBarrierApplied = true;
        }

        /// <summary>
        /// Finalizes the New Game Active/View/Selection invariant after Continuous presentation exists.
        /// Existing explicit selections are preserved; only an empty selection is initialized.
        /// </summary>
        public bool EnsureActiveSelectionAfterPresentationMaterialized()
        {
            var party = _session?.PlayerParty;
            if (selectionController == null || entityViewSpawner == null ||
                party?.HasActive != true)
                return false;
            var active = party.ActiveCharacterId;
            if (selectionController.State.Contains(active))
                return true;
            if (selectionController.State.Count > 0 ||
                !entityViewSpawner.Registry.Contains(active))
                return false;
            return selectionController.SelectEntity(active, false);
        }

        /// <summary>
        /// Producer 诊断：opening population census。
        /// §18：摘要行不含 17|18 人全文；SpatialInvalid 非空时才在下一行展开。
        /// </summary>
        public string OpeningPopulationDiagnostic
        {
            get
            {
                if (_continuousOutdoorSurfaceRuntime == null)
                    return "Barrier=" + (_openingPopulationBarrierApplied ? "applied" : "pending");
                var runtime = _continuousOutdoorSurfaceRuntime;
                var text = runtime.OpeningSpatialCensusSummary +
                           " Barrier=" + (_openingPopulationBarrierApplied ? "applied" : "pending") +
                           " InvalidSpawn=" + runtime.InvalidSpawnEntityCount +
                           " RealignedViews=" + runtime.RealignedViewCount +
                           " AnchorBake=" + runtime.OpeningAnchorBakeStatus;
                if (!string.IsNullOrEmpty(runtime.OpeningPopulationSpatialInvalid))
                    text += "\n[OpeningSpatialInvalid] " + runtime.OpeningPopulationSpatialInvalid;
                if (!string.IsNullOrEmpty(runtime.OpeningAnchorBakeFailure))
                    text += "\n[OpeningAnchorBakeInvalid] " + runtime.OpeningAnchorBakeFailure;
                return text;
            }
        }

        /// <summary>§12 barrier 是否已在本次启动中执行过（诊断用）。</summary>
        public bool OpeningPopulationBarrierApplied => _openingPopulationBarrierApplied;

        /// <summary>Producer 诊断：当前真实 presentation authority。</summary>
        public string OutdoorAuthorityDiagnostic
        {
            get
            {
                var surface = _continuousOutdoorSurfaceRuntime;
                if (surface != null && surface.IsActive)
                {
                    var acceptanceOnly = false;
                    var parsed = XianXia.Core.Domain.Ids.DefinitionId.Parse(surface.ActiveSurfaceId);
                    if (parsed.IsSuccess && _session?.Registry != null &&
                        _session.Registry.TryGetOutdoorSurface(parsed.Value, out var definition) &&
                        definition != null)
                        acceptanceOnly = definition.AcceptanceOnly;
                    return "Authority=ContinuousOutdoorSurface" +
                           " SurfaceId=" + surface.ActiveSurfaceId +
                           " AcceptanceOnly=" + acceptanceOnly +
                           " CurrentChunk=" + surface.CurrentChunk +
                           " LoadedChunks=" + surface.LoadedChunkCount +
                           " CurrentOutdoorWorldSiteId=" +
                           (_session?.World?.PlayerPartyTravel?.CurrentOutdoorWorldSiteId ?? string.Empty);
                }

                if (_session?.World?.LocalMap?.IsInInterior == true)
                    return "Authority=SeparateSpace";
                if (CharacterEncounterService.BlocksOrdinaryContinuousSurface(
                        _session?.World))
                    return "Authority=CharacterEncounter";
                return "Authority=Unavailable";
            }
        }

        /// <summary>After Snapshot restore: rebuild views and rebind Host adapters.</summary>
        public void RebindHostControlAfterSnapshotRestore()
        {
            if (!_session.IsInitialized)
                return;

            HostInputGate.Clear();

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
            EnsureSocialNotificationOverlay();
            socialNotificationOverlay.Clear();

            // All snapshot/content/world authorities are now restored. A saved wiped party may
            // retry succession here, before PartyWorld resolution and any presentation rebuild.
            PlayerPartyController?.TryResolveExternalHandoffAfterWorldShellRestore();

            // SPACE-01：Active Separate Space 时禁止 Outdoor ActiveControlled resolver 抢先改 PartyWorld。
            if (_session.World?.LocalMap != null && _session.World.LocalMap.IsActive)
            {
                _session.World.PartyWorld.LocalMapId = _session.World.LocalMap.ActiveMapLayoutId ?? string.Empty;
                _session.World.PartyWorld.SiteId = string.Empty;
                _session.World.PartyWorld.Mode = PartyWorldPresenceMode.InSeparateSpace;
            }
            else
            {
                HostSnapshotSessionRehydration.ResolvePartyWorldFromActiveControlledCharacter(
                    _session.World,
                    _session.PlayerParty);
            }

            selectionController.ClearSelection();
            if (inventoryPanel != null)
                inventoryPanel.ClearSessionState();
            if (constructionPanel != null)
                constructionPanel.ClearSessionState();
            if (worldMapPanel != null)
                worldMapPanel.ClearSessionState();
            if (worldActivityPanel != null)
            {
                worldActivityPanel.ClearSessionState();
                worldActivityPanel.Bind(this);
            }
            if (strategicInterrupt != null)
                strategicInterrupt.ClearSessionState();
            entityViewSpawner.Clear();
            if (moveController != null)
                moveController.ResetPresentationMovementState();
            PlayerPartyController?.ResetTransientStateAfterSnapshotRestore();
            commandBridge.Bind(_session, selectionController);
            debugHud.Bind(this, selectionController);
            if (levelTesterCheatPanel != null)
                levelTesterCheatPanel.Bind(this, selectionController);
            eventFeed.Clear();

            HostInputGate.ResetSession();

            // SPACE-01 presentation rebuild 优先级：
            // 1) Active Separate Space  2) Independent Encounter  3) Continuous Outdoor
            if (_session.World?.LocalMap != null && _session.World.LocalMap.IsActive)
            {
                RebuildSeparateSpacePresentationAfterLoad();
            }
            else if (CharacterEncounterService.BlocksOrdinaryContinuousSurface(
                         _session.World))
            {
                var continuousOutdoorRestored = _continuousOutdoorSurfaceRuntime != null &&
                                                _continuousOutdoorSurfaceRuntime.RebuildAfterWorldRestore();
                if (continuousOutdoorRestored)
                    RefreshContinuousOutdoorOverlaysOnce();
                else
                {
                    _session.AcquireModalPause("EncounterRestoreFailure");
                    Debug.LogError("Independent encounter source restore failed; ordinary-map fallback is prohibited.");
                    return;
                }
            }
            else
            {
                var continuousOutdoorRestored = _continuousOutdoorSurfaceRuntime != null &&
                                                _continuousOutdoorSurfaceRuntime.RebuildAfterWorldRestore();
                if (continuousOutdoorRestored)
                    RefreshContinuousOutdoorOverlaysOnce();
                else
                {
                    _session.AcquireModalPause("ContinuousRestoreFailure");
                    Debug.LogError(
                        "Snapshot has no restorable Continuous Surface, Separate Space, or CharacterEncounter presentation.");
                    return;
                }
            }

            _session.ReleaseModalPause("EncounterRestoreFailure");

            RebindHostControlAfterSnapshotRestore();
            GetComponent<HostSeparateSpaceExitTrigger>()?.NotifyPresentationRestored();
            RefreshLoadedStrategicPopulation();
            CommitOutdoorEntityReconcileGate();

            DispatchDrainedEvents();
            _autoTickAccumulator = 0f;
            RefreshStatus();
        }

        /// <summary>
        /// Snapshot Load：在已恢复的 SeparateSpaceSession 上重建洞内 presentation。
        /// 禁止重新 Enter，禁止 Outdoor Site／WorldPosition 解析。
        /// </summary>
        public void RebuildSeparateSpacePresentationAfterLoad()
        {
            var world = _session?.World;
            if (world?.LocalMap == null || !world.LocalMap.IsActive)
                return;

            var mapId = world.LocalMap.ActiveMapLayoutId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(mapId))
            {
                Debug.LogError("[SeparateSpaceRestoreInvariantFailure] ActiveMapLayoutId empty.");
                return;
            }

            var parsed = DefinitionId.Parse(mapId);
            if (parsed.IsFailure || !_session.Registry.TryGetMapLayout(parsed.Value, out _))
            {
                Debug.LogError(
                    "[SeparateSpaceRestoreInvariantFailure] MapLayout missing in registry: " + mapId);
                return;
            }

            if (_continuousOutdoorSurfaceRuntime != null && _continuousOutdoorSurfaceRuntime.IsActive)
                _continuousOutdoorSurfaceRuntime.DeactivateForInteriorTransition();

            preferredMapLayoutId = mapId;
            _session.PreferredMapLayoutId = mapId;
            ConfigurePreferredMapLayout(mapId);

            var places = InteriorLocalPlaceBootstrap.ActivatePlacesForMapLayout(
                world, _session.Registry, mapId);
            if (places.IsFailure)
            {
                Debug.LogError(
                    "[SeparateSpaceRestoreInvariantFailure] ActivatePlaces failed: " + places.Error);
                return;
            }

            world.LocalMap.ActiveLocalPlaceSetId = world.LocalPlaces.RegionId ?? string.Empty;
            LoadedLocalMapPlacementSnapshotRestore.ApplySavedPlacementsToDomain(world, mapId);
            _session.RefreshViewableEntityIds();
            ReloadLocalMapPresentation(frameCamera: false);

            var presented = entityViewSpawner != null ? entityViewSpawner.SpawnedCount : 0;
            var savedPlacements = LoadedLocalMapPlacementSnapshotRestore.PendingCount;
            var occupants = world.LocalMap.OccupantIds;
            Debug.Log(
                "[SeparateSpaceRestore] map=" + mapId +
                " spaceKind=" + world.LocalMap.SpaceKind +
                " occupants=" + occupants.Count +
                " savedPlacements=" + savedPlacements +
                " presented=" + presented +
                " outdoorRuntimeActive=" +
                (_continuousOutdoorSurfaceRuntime != null && _continuousOutdoorSurfaceRuntime.IsActive),
                this);

            ValidateSeparateSpaceRestoreInvariant(mapId);

            LoadedLocalMapPlacementSnapshotRestore.FinishRestorePresentation();
        }

        void ValidateSeparateSpaceRestoreInvariant(string mapId)
        {
            var world = _session.World;
            var failed = false;
            if (_continuousOutdoorSurfaceRuntime != null && _continuousOutdoorSurfaceRuntime.IsActive)
            {
                Debug.LogError(
                    "[SeparateSpaceRestoreInvariantFailure] ContinuousOutdoor still active after Cave restore.");
                failed = true;
            }

            if (world.PartyWorld == null ||
                world.PartyWorld.Mode != PartyWorldPresenceMode.InSeparateSpace ||
                !string.Equals(
                    world.PartyWorld.LocalMapId?.Trim(),
                    mapId,
                    System.StringComparison.Ordinal))
            {
                Debug.LogError(
                    "[SeparateSpaceRestoreInvariantFailure] PartyWorld not InSeparateSpace map=" + mapId +
                    " mode=" + (world.PartyWorld?.Mode.ToString() ?? "null") +
                    " localMapId=" + (world.PartyWorld?.LocalMapId ?? string.Empty));
                failed = true;
            }

            if (string.IsNullOrEmpty(world.LocalMap.ActiveMapLayoutId) ||
                !string.Equals(
                    _session.PreferredMapLayoutId?.Trim(),
                    mapId,
                    System.StringComparison.Ordinal))
            {
                Debug.LogError(
                    "[SeparateSpaceRestoreInvariantFailure] PreferredMapLayoutId mismatch map=" + mapId +
                    " preferred=" + _session.PreferredMapLayoutId);
                failed = true;
            }

            if (string.IsNullOrEmpty(world.LocalMap.ActiveLocalPlaceSetId) &&
                string.IsNullOrEmpty(world.LocalPlaces?.RegionId))
            {
                Debug.LogError(
                    "[SeparateSpaceRestoreInvariantFailure] Active LocalPlaceSet not activated.");
                failed = true;
            }

            var party = _session.PlayerParty;
            if (party == null)
                return;

            for (var i = 0; i < party.Members.Count; i++)
            {
                var id = party.Members[i];
                if (!world.LocalMap.ContainsOccupant(id))
                    continue;
                if (!world.Entities.TryGet(id, out var ent) ||
                    !CombatLifeStateService.CanFight(ent))
                    continue;

                if (!LocalMapVisibility.IsEntityVisible(world, id))
                {
                    Debug.LogError(
                        "[SeparateSpaceRestoreInvariantFailure] Occupant not visible id=" + id.Value);
                    failed = true;
                }

                if (entityViewSpawner == null ||
                    !entityViewSpawner.Registry.TryGet(id, out var view) ||
                    view == null)
                {
                    Debug.LogError(
                        "[SeparateSpaceRestoreInvariantFailure] Occupant missing EntityView id=" +
                        id.Value);
                    failed = true;
                    continue;
                }

                if (LoadedLocalMapPlacementSnapshotRestore.TryGetPlacement(
                        id, mapId, out var sx, out var sz))
                {
                    var p = HostPresentationSpace.ToPresentation(view.transform.position);
                    var dx = p.x - sx;
                    var dz = p.y - sz;
                    if (dx * dx + dz * dz > 0.35f * 0.35f)
                    {
                        Debug.LogError(
                            "[SeparateSpaceRestoreInvariantFailure] Saved placement mismatch id=" +
                            id.Value + " view=(" + p.x + "," + p.y + ") saved=(" + sx + "," + sz + ")");
                        failed = true;
                    }
                }
            }

            if (!failed)
                return;
            // 失败时禁止 fallback Outdoor；仅诊断，保持 Separate Space 状态。
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

        /// <summary>LocalMap 进出后：PreferredMapLayout、重建灰盒／实体／寻路/summary>
        /// <param name="frameCamera">勘查显形等轻量刷新应false，避免镜头乱跳/param>
        public void ReloadLocalMapPresentation(bool frameCamera = true)
        {
            if (!_session.IsInitialized)
                return;

            var interior = _session.World.LocalMap.IsInInterior;
            var handoffMotion = _session.World?.PlayerPartyTravel;
            if (_continuousOutdoorSurfaceRuntime != null && _continuousOutdoorSurfaceRuntime.IsActive &&
                (interior || handoffMotion == null ||
                 handoffMotion.LocationKind != PlayerPartyLocationKind.AtWorldPosition))
            {
                if (interior) _continuousOutdoorSurfaceRuntime.DeactivateForInteriorTransition();
                else _continuousOutdoorSurfaceRuntime.DeactivatePresentationOnly();
            }
            if (!interior && _continuousOutdoorSurfaceRuntime != null &&
                _continuousOutdoorSurfaceRuntime.IsActive)
            {
                FlushLoadedDestinationArrivals();
                ReloadContinuousSurfaceOverlaysOnly(frameCamera);
                return;
            }

            if (!interior && _continuousOutdoorSurfaceRuntime != null &&
                handoffMotion?.LocationKind == PlayerPartyLocationKind.AtWorldPosition &&
                _continuousOutdoorSurfaceRuntime.TryActivateAtCurrentWorldPosition())
            {
                FlushLoadedDestinationArrivals();
                ReloadContinuousSurfaceOverlaysOnly(frameCamera);
                return;
            }

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
            {
                moveController.SetWalkGrid(ResolveWalkGrid());
                moveController.BindLocalMapContext(active?.Trim() ?? string.Empty);
            }
            if (frameCamera)
                FrameCameraOnSlots();
            RefreshStatus();
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

        /// <summary>按精确世界位置激活当前 Continuous Surface 表现。</summary>
        public void ActivateContinuousOutdoorPresentation()
        {
            if (!_session.IsInitialized || _session.World.LocalMap.IsInInterior)
                return;

            var continuousWasActive = _continuousOutdoorSurfaceRuntime != null &&
                                      _continuousOutdoorSurfaceRuntime.IsActive;
            if (_continuousOutdoorSurfaceRuntime != null && _continuousOutdoorSurfaceRuntime.TryActivateAtCurrentWorldPosition())
            {
                moveController?.SetWalkGrid(ResolveWalkGrid());
                if (continuousWasActive)
                    RefreshContinuousOutdoorOverlaysOnce();
            }
        }

        /// <summary>Acceptance tooling hook；Startup 也用它明确对准主控。</summary>
        public void FrameCameraOnActiveCharacter()
        {
            if (cameraRig == null)
                return;
            var active = _session.PlayerParty != null
                ? _session.PlayerParty.ActiveCharacterId
                : EntityId.None;
            if (!active.IsNone && entityViewSpawner != null &&
                entityViewSpawner.Registry.TryGet(active, out var view) && view != null)
            {
                cameraRig.FrameSlots(view.transform.position);
                return;
            }
            Debug.LogWarning(
                "[ContinuousStartup] ActiveCharacter EntityView missing when framing camera; " +
                "falling back to party／slots framing.", this);
            FrameCameraOnSlots();
        }

        void ReloadContinuousSurfaceOverlaysOnly(bool frameCamera)
        {
            if (mapGraybox != null)
                mapGraybox.RebuildOverlaysOnly(_session);
            if (interactSpotPresenter != null)
                interactSpotPresenter.Rebuild();
            if (moveController != null)
            {
                moveController.SetWalkGrid(ResolveWalkGrid());
                moveController.BindLocalMapContext("ContinuousSurface:" + _continuousOutdoorSurfaceRuntime.ActiveSurfaceId);
            }
            if (frameCamera) FrameCameraOnSlots();
            RefreshStatus();
        }

        /// <summary>Continuous Surface authority handoff. A continuous outdoor surface has no Active LocalMap:
        /// clear legacy site-only context without selecting a chunk source as a replacement map.</summary>
        public void FinalizeContinuousWildernessPresentationHandoff()
        {
            if (!_session.IsInitialized)
                return;
            InteriorLocalPlaceBootstrap.ActivatePlacesForMapLayout(_session.World, _session.Registry, string.Empty);
            mapGraybox?.Clear();
            interactSpotPresenter?.Clear();
        }

        public void RefreshContinuousOutdoorOverlaysOnce() =>
            ReloadContinuousSurfaceOverlaysOnly(frameCamera: false);

        /// <summary>Refreshes the real current visibility/materialization view when explicitly requested.</summary>
        public void RefreshLoadedStrategicPopulation()
        {
            if (!_session.IsInitialized || entityViewSpawner == null)
                return;

            _session.RefreshViewableEntityIds();
            entityViewSpawner.SpawnMissingVisibleViews(_session);
            entityViewSpawner.PruneHiddenViews(_session);
        }

        public void NotifyOutdoorEntityScopeChanged() =>
            _outdoorEntityReconcileGate.MarkDirty(
                _continuousOutdoorSurfaceRuntime?.EntityReconcileGeneration ?? 0);

        public bool HasOutdoorEntityReconcileBaseline =>
            _outdoorEntityReconcileGate.HasBaseline;

        public void FlushLoadedDestinationArrivals()
        {
            if (!_session.IsInitialized || entityViewSpawner == null)
                return;
            var fingerprint = CaptureOutdoorEntityScopeFingerprint();
            var generation = _continuousOutdoorSurfaceRuntime?.EntityReconcileGeneration ?? 0;
            var decision = _outdoorEntityReconcileGate.Decide(fingerprint, generation);
            if (decision == OutdoorEntityReconcileDecision.None)
                return;
            if (decision == OutdoorEntityReconcileDecision.ReconcileAndRefresh)
                _continuousOutdoorSurfaceRuntime?.ReconcileOutdoorEntityMaterializationForScopeChange();
            _session.RefreshViewableEntityIds();
            entityViewSpawner.SpawnMissingVisibleViews(_session);
            entityViewSpawner.PruneHiddenViews(_session);
            CommitOutdoorEntityReconcileGate();
        }

        void CommitOutdoorEntityReconcileGate()
        {
            if (_session == null || !_session.IsInitialized)
            {
                _outdoorEntityReconcileGate.Reset();
                return;
            }
            _outdoorEntityReconcileGate.Commit(
                CaptureOutdoorEntityScopeFingerprint(),
                _continuousOutdoorSurfaceRuntime?.EntityReconcileGeneration ?? 0);
        }

        ulong CaptureOutdoorEntityScopeFingerprint() =>
            OutdoorEntityReconcileGate.CombineFingerprint(
                OutdoorEntityReconcileGate.CaptureFingerprint(
                    _session.World, _session.PlayerParty),
                _continuousOutdoorSurfaceRuntime?.CaptureMovingEntityLoadedScopeFingerprint() ?? 0);

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

            DispatchDrainedEvents();
            // Arrival/member/lifecycle changes are coalesced after event-owned spatial handoff.
            // Ordinary stationary ticks do not rebuild the materialized population.
            FlushLoadedDestinationArrivals();
            RefreshStatus();
        }

        /// <summary>Host 表现层触发的 Content／Quest 事件立即送给打断呈现/summary>
        /// <summary>Focuses the existing gameplay camera only; never changes party motion or world state.</summary>
        public bool TryFocusContinuousWorldPosition(string surfaceId, WorldVec2 worldPosition, out string message)
        {
            message = string.Empty;
            if (_session?.World == null || _continuousOutdoorSurfaceRuntime == null || cameraRig == null)
            {
                message = "当前无法定位目标。";
                return false;
            }
            if (!PlayerPartyWorldLocationQuery.TryResolve(
                    _session.World, _session.PlayerParty, out var playerLocation) ||
                !string.Equals(playerLocation.SurfaceId, surfaceId ?? string.Empty, System.StringComparison.Ordinal))
            {
                message = "目标位于其它区域。";
                return false;
            }
            if (!_continuousOutdoorSurfaceRuntime.IsActive ||
                !string.Equals(_continuousOutdoorSurfaceRuntime.ActiveSurfaceId, surfaceId,
                    System.StringComparison.Ordinal) ||
                !_continuousOutdoorSurfaceRuntime.TryWorldToPresentation(worldPosition, out var presentation))
            {
                message = "当前区域尚未准备好定位。";
                return false;
            }
            cameraRig.FrameWorldPoint(presentation);
            return true;
        }

        public void DispatchDrainedEvents()
        {
            if (_session?.World?.Events == null)
                return;
            var drained = _session.World.Events.Drain();
            var nonEncounterStrategicPopulationChanged = false;
            for (var i = 0; i < drained.Count; i++)
            {
                var evt = drained[i];
                if (evt?.Type == XianXia.Core.Events.EventType.WorldOpportunityNotice)
                {
                    strategicInterrupt?.ShowTransientToast(evt.Payload);
                    nonEncounterStrategicPopulationChanged = true;
                }
                else if (evt?.Type == XianXia.Core.Events.EventType.PlayerSuccessionResolved ||
                         evt?.Type == XianXia.Core.Events.EventType.PlayerEmergencyControlTransferred)
                {
                    strategicInterrupt?.ShowTransientToast(evt.Payload);
                    nonEncounterStrategicPopulationChanged = true;
                }
                else if (evt?.Type == XianXia.Core.Events.EventType.EntityCreated)
                {
                    // Domain-created entities rely on the existing Continuous Outdoor reconcile path.
                    nonEncounterStrategicPopulationChanged = true;
                }
                if (evt?.Type == XianXia.Core.Events.EventType.CombatantDefeated &&
                    evt.Target.HasValue)
                {
                    var defenderId = evt.Target.Value;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    var spatialBefore = CaptureLifecycleSpatialDiagnostic(defenderId);
#endif
                    _session.World.Entities.TryGet(defenderId, out var defeatedEntity);
                    var transition = DefeatSpatialTransitionResolver.Resolve(evt, defeatedEntity);
                    var handledByCharacterEncounter =
                        CharacterEncounterService.OwnsParticipantSpatialState(
                            _session.World, defenderId);
                    var handoffAction = handledByCharacterEncounter
                        ? "PreserveEncounterTactical"
                        : string.Empty;
                    var spatialHandled = handledByCharacterEncounter;
                    if (handledByCharacterEncounter)
                    {
                        if (entityViewSpawner != null &&
                            entityViewSpawner.Registry.TryGet(defenderId, out var encounterView) &&
                            encounterView != null)
                            _continuousOutdoorSurfaceRuntime
                                ?.TryCaptureIndependentParticipantPosition(
                                    defenderId, encounterView.transform.position);
                        HostLifeStatePresentationSync.RefreshDownedOrDead(
                            _session.World,
                            entityViewSpawner,
                            moveController,
                            defenderId,
                            captureOrdinaryPlacement: false);
                    }
                    else
                    {
                        // Death confirmation is a lifecycle-only change. Existing personal or
                        // encounter authority is a successful spatial result and must not fall
                        // through to legacy casualty placement.
                        if (transition == DefeatSpatialTransitionKind.DeathConfirmation &&
                            ResidualSpatialAuthorityService.TryResolveStableResidualSpatialAuthority(
                                _session.World, defenderId, out _))
                        {
                            spatialHandled = true;
                            handoffAction = "Preserve";
                        }
                        else
                        {
                            var gotLocal = TryGetCurrentLocalPresentation(
                                defenderId,
                                out var localX,
                                out var localZ);

                            // New Continuous path: the defeated character's own View and active
                            // mapper establish the exact point. Keep its original presence mode.
                            if (transition == DefeatSpatialTransitionKind.InitialIncapacitation &&
                                gotLocal && _continuousOutdoorSurfaceRuntime?.IsActive == true &&
                                _session.World.ContinuousOutdoorMaterialization.IsMaterialized(defenderId) &&
                                _continuousOutdoorSurfaceRuntime.PresentationToWorld(
                                    localX, localZ, out var worldX, out var worldY) &&
                                ResidualSpatialAuthorityService.TryFreezeAtPreciseWorldPosition(
                                    _session.World,
                                    defenderId,
                                    new WorldVec2(worldX, worldY),
                                    _continuousOutdoorSurfaceRuntime.ActiveSurfaceId))
                            {
                                spatialHandled = true;
                                handoffAction = "FreezeCurrent";
                                if (defeatedEntity != null &&
                                    defeatedEntity.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(
                                        out var currentLocation) && currentLocation != null)
                                    currentLocation.SetPresentationOverride(localX, localZ);
                            }

                            // No View is not a reason to relocate: an existing stable personal
                            // authority is already the correct result.
                            if (!spatialHandled &&
                                ResidualSpatialAuthorityService.TryResolveStableResidualSpatialAuthority(
                                    _session.World, defenderId, out _))
                            {
                                spatialHandled = true;
                                handoffAction = "Preserve";
                            }

                            if (spatialHandled)
                                nonEncounterStrategicPopulationChanged = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            LogLocalCombatDefeatDiagnostics(
                                defenderId, handledByCharacterEncounter, gotLocal, localX, localZ);
#endif
                        }

                        if (transition == DefeatSpatialTransitionKind.DeathConfirmation)
                            nonEncounterStrategicPopulationChanged = true;
                    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    LogLifecycleSpatialDiagnostic(
                        defenderId, transition, handoffAction, spatialBefore);
#endif
                }
            }

            if (nonEncounterStrategicPopulationChanged)
            {
                NotifyOutdoorEntityScopeChanged();
                FlushLoadedDestinationArrivals();
            }

            if (contentInterrupt != null)
                contentInterrupt.Ingest(drained);
            if (questJournal != null)
                questJournal.Ingest(drained);
            if (eventFeed != null)
                eventFeed.Ingest(drained);
            socialNotificationOverlay?.Ingest(drained);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        readonly struct LifecycleSpatialDiagnosticSnapshot
        {
            public LifecycleSpatialDiagnosticSnapshot(
                string mode, string siteId, string surfaceId, bool hasPrecise,
                string worldPosition, string viewPosition, string owner)
            {
                Mode = mode; SiteId = siteId; SurfaceId = surfaceId; HasPrecise = hasPrecise;
                WorldPosition = worldPosition;
                ViewPosition = viewPosition; Owner = owner;
            }
            public string Mode { get; }
            public string SiteId { get; }
            public string SurfaceId { get; }
            public bool HasPrecise { get; }
            public string WorldPosition { get; }
            public string ViewPosition { get; }
            public string Owner { get; }
        }

        LifecycleSpatialDiagnosticSnapshot CaptureLifecycleSpatialDiagnostic(EntityId id)
        {
            var world = _session?.World;
            var mode = "Missing";
            var siteId = string.Empty;
            var surfaceId = string.Empty;
            var hasPrecise = false;
            var worldPosition = "None";
            if (world?.WorldPresence != null && world.WorldPresence.TryGet(id, out var presence) &&
                presence != null)
            {
                mode = presence.Mode.ToString();
                siteId = presence.SiteId ?? string.Empty;
                surfaceId = presence.PersonalSurfaceId ?? string.Empty;
                hasPrecise = presence.HasContinuousWorldPosition;
                if (hasPrecise)
                    worldPosition = "(" + presence.WorldPosX.ToString("0.###") + "," +
                                    presence.WorldPosY.ToString("0.###") + ")";
            }
            var viewPosition = "None";
            if (entityViewSpawner?.Registry.TryGet(id, out var view) == true && view != null)
                viewPosition = "(" + view.transform.position.x.ToString("0.###") + "," +
                               view.transform.position.y.ToString("0.###") + ")";
            var owner = ResidualSpatialAuthorityService.TryResolveStableResidualSpatialAuthority(
                world, id, out var authority)
                ? authority.Owner
                : "Missing";
            return new LifecycleSpatialDiagnosticSnapshot(
                mode, siteId, surfaceId, hasPrecise, worldPosition, viewPosition, owner);
        }

        void LogLifecycleSpatialDiagnostic(
            EntityId id,
            DefeatSpatialTransitionKind transition,
            string handoffAction,
            LifecycleSpatialDiagnosticSnapshot before)
        {
            var world = _session.World;
            var after = CaptureLifecycleSpatialDiagnostic(id);
            world.Entities.TryGet(id, out var entity);
            var squadId = world.Strategic.Squads.TryGetForCharacter(id, out var squad) && squad != null
                ? squad.SquadId
                : string.Empty;
            var encounterId = world.Strategic.CharacterEncounter?.EncounterId ?? string.Empty;
            var changed = before.Mode != after.Mode || before.SiteId != after.SiteId ||
                          before.SurfaceId != after.SurfaceId || before.HasPrecise != after.HasPrecise ||
                          before.WorldPosition != after.WorldPosition;
            var message = "[ResidualLifecycleSpatial]" +
                          " CharacterId=" + id.Value +
                          " Name=" + (entity?.DisplayName ?? string.Empty) +
                          " Transition=" + transition +
                          " SquadId=" + squadId +
                          " EncounterId=" + encounterId +
                          " Mode=" + before.Mode + "->" + after.Mode +
                          " SiteId=" + before.SiteId + "->" + after.SiteId +
                          " PersonalSurfaceId=" + before.SurfaceId + "->" + after.SurfaceId +
                          " HasPrecise=" + before.HasPrecise + "->" + after.HasPrecise +
                          " WorldPosition=" + before.WorldPosition + "->" + after.WorldPosition +
                          " ViewPosition=" + before.ViewPosition + "->" + after.ViewPosition +
                          " SpatialOwner=" + before.Owner + "->" + after.Owner +
                          " HandoffAction=" + (handoffAction ?? string.Empty);
            if (transition == DefeatSpatialTransitionKind.DeathConfirmation && changed)
                Debug.LogWarning(message + " UnexpectedDeathConfirmationSpatialMutation=true", this);
            else
                Debug.Log(message, this);
        }

        /// <summary>
        /// Development 诊断：Local Combat 倒下者分类确认（普通 Local Combat 非 Encounter 路径）。
        /// 输出 EntityId / LifeState / 是否 PlayerParty member / SquadId / WorldPresence /
        /// 是否 Traveling member，用于确认消失者归属哪一层 owner。
        /// </summary>
        void LogLocalCombatDefeatDiagnostics(
            EntityId defenderId,
            bool handledByCharacterEncounter,
            bool gotLocal,
            float localX,
            float localZ)
        {
            var world = _session.World;
            var party = _session.PlayerParty;
            var name = defenderId.ToString();
            var lifeState = "(entity missing)";
            var isPartyMember = false;
            var isTraveling = false;
            var squadId = "(none)";
            var presenceMode = "(none)";
            var presenceSiteId = string.Empty;
            if (world != null && world.Entities.TryGet(defenderId, out var entity) && entity != null)
            {
                name = string.IsNullOrEmpty(entity.DisplayName)
                    ? defenderId.ToString()
                    : entity.DisplayName;
                lifeState = XianXia.Core.Combat.CombatLifeStateService.ResolveLifeStateLabel(entity);
            }

            if (party != null)
                isPartyMember = party.IsMember(defenderId);
            if (world?.PlayerPartyTravel != null)
            {
                for (var i = 0; i < world.PlayerPartyTravel.TravelingMembers.Count; i++)
                {
                    if (world.PlayerPartyTravel.TravelingMembers[i] == defenderId)
                    {
                        isTraveling = true;
                        break;
                    }
                }
            }

            if (world != null && CharacterStrategicQuery.TryGetSquad(world, defenderId, out var squad) && squad != null)
                squadId = squad.SquadId;
            if (world?.WorldPresence != null &&
                world.WorldPresence.TryGet(defenderId, out var wp) && wp != null)
            {
                presenceMode = wp.Mode.ToString();
                presenceSiteId = wp.SiteId ?? string.Empty;
            }

            Debug.Log(
                "[LocalCombatDefeat]" +
                " EntityId=" + defenderId +
                " Name=" + name +
                " LifeState=" + lifeState +
                " HandledByCharacterEncounter=" + handledByCharacterEncounter +
                " IsPlayerPartyMember=" + isPartyMember +
                " IsTravelingPartyMember=" + isTraveling +
                " SquadId=" + squadId +
                " WorldPresenceMode=" + presenceMode +
                " WorldPresenceSiteId=" + presenceSiteId +
                " GotViewLocal=" + gotLocal +
                " ViewLocal=(" + localX.ToString("0.###") + "," + localZ.ToString("0.###") + ")",
                this);
        }
#endif

        /// <summary>
        /// 从 EntityViewSpawner.Registry 捕获角色当前真实 Local transform 位置
        /// （仅当该实体正有 view；无 view → false）。不能读 EntityLocationComponent
        /// PresentationOverride —— 它是 Domain persistence 状态，未必等于当前已物化 View 的
        /// 实时位置；有 View 时必须捕获 transform，避免保存 stale presentation。
        /// </summary>
        bool TryGetCurrentLocalPresentation(
            EntityId id,
            out float localX,
            out float localZ)
        {
            localX = 0f;
            localZ = 0f;
            if (entityViewSpawner == null ||
                !entityViewSpawner.Registry.TryGet(id, out var view) ||
                view == null)
                return false;

            var p = HostPresentationSpace.ToPresentation(view.transform.position);
            localX = p.x;
            localZ = p.y;
            return true;
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

        public void RefreshFactionFlagWalkGrid()
        {
            _continuousOutdoorSurfaceRuntime?.RefreshCompositeWalkGrid();
            if (moveController != null)
                moveController.SetWalkGrid(ResolveWalkGrid());
        }

        WalkGrid ResolveWalkGrid()
        {
            if (_continuousOutdoorSurfaceRuntime != null &&
                _continuousOutdoorSurfaceRuntime.TryGetCompositeWalkGrid(out var continuousComposite))
                return continuousComposite;
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
