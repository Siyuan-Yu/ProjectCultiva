using System.IO;
using UnityEngine;
using XianXia.Core.Navigation;
using XianXia.Core.Results;
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
        [Tooltip("空则用默认 openingScenario。")]
        [SerializeField] string openingScenarioId = "";
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
        [SerializeField] HostQuestJournal questJournal;
        [SerializeField] HostInventoryPanel inventoryPanel;
        [SerializeField] HostInteractSpotPresenter interactSpotPresenter;
        [SerializeField] HostNpcScheduleMover npcScheduleMover;

        [Header("Tick debug")]
        [SerializeField] bool initializeOnPlay = true;
        [SerializeField] bool autoTickWhenUnpaused = true;
        [Tooltip("1x 下每 Tick 现实秒数。288 Tick/日×5 游戏分；默认 3s → 约 14 分钟现实 = 1 游戏日。")]
        [SerializeField] float secondsPerAutoTickAt1x = 3f;
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

        public HostQuestJournal QuestJournal => questJournal;

        public HostInventoryPanel InventoryPanel => inventoryPanel;

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
            if (questJournal == null)
                questJournal = GetComponent<HostQuestJournal>() ??
                              GetComponentInChildren<HostQuestJournal>();
            if (inventoryPanel == null)
                inventoryPanel = GetComponent<HostInventoryPanel>() ??
                                GetComponentInChildren<HostInventoryPanel>();
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
                    (contentInterrupt == null || !contentInterrupt.HasBlockingInterrupt))
                    _session.IsPaused = !_session.IsPaused;
                RefreshStatus();
            }

            if (Input.GetKeyDown(stepTickKey) || Input.GetKeyDown(stepTickAltKey))
            {
                if ((questJournal == null || !questJournal.IsOpen) &&
                    (inventoryPanel == null || !inventoryPanel.IsOpen))
                    StepTick();
            }

            if (Input.GetKeyDown(cycleSpeedKey) || Input.GetKeyDown(cycleSpeedAltKey))
            {
                if (debugHud != null)
                    debugHud.CycleSpeed();
                RefreshStatus();
            }

            if (Input.GetKeyDown(rebuildKey))
                TryInitialize();

            if (!_session.IsPaused && autoTickWhenUnpaused)
            {
                var speed = debugHud != null ? debugHud.SpeedMultiplier : 1;
                if (speed < 1)
                    speed = 1;
                _autoTickAccumulator += Time.unscaledDeltaTime * speed;
                var interval = Mathf.Max(0.01f, secondsPerAutoTickAt1x);
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

        public string PreferredMapLayoutId => preferredMapLayoutId ?? "";

        public string OpeningScenarioId => openingScenarioId ?? "";

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
            if (questJournal == null)
                questJournal = GetComponent<HostQuestJournal>() ??
                              gameObject.AddComponent<HostQuestJournal>();
            if (inventoryPanel == null)
                inventoryPanel = GetComponent<HostInventoryPanel>() ??
                                gameObject.AddComponent<HostInventoryPanel>();
            if (GetComponent<HostWorkLoop>() == null)
                gameObject.AddComponent<HostWorkLoop>();
            if (interactSpotPresenter == null)
                interactSpotPresenter = GetComponent<HostInteractSpotPresenter>() ??
                                       gameObject.AddComponent<HostInteractSpotPresenter>();
            if (npcScheduleMover == null)
                npcScheduleMover = GetComponent<HostNpcScheduleMover>() ??
                                  gameObject.AddComponent<HostNpcScheduleMover>();

            selectionController.ClearSelection();
            entityViewSpawner.Clear();
            eventFeed.Clear();
            contentInterrupt.ClearSessionState();
            if (questJournal != null)
                questJournal.ClearSessionState();
            if (inventoryPanel != null)
                inventoryPanel.ClearSessionState();
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
                    ? null
                    : openingScenarioId.Trim()
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
            if (_session.CharacterIds.Count > 0)
                selectionController.SelectEntity(_session.CharacterIds[0], false);
            feedbackOverlay.Bind(cam);
            commandBridge.Bind(_session, selectionController, feedbackOverlay);
            var workLoop = GetComponent<HostWorkLoop>();
            if (workLoop != null)
                workLoop.Bind(this, commandBridge, moveController);
            debugHud.Bind(this, selectionController);
            contentDebugPanel.Bind(this, selectionController);
            moveController.Bind(this, selectionController, entityViewSpawner, commandBridge);
            moveController.SetWalkGrid(ResolveWalkGrid());
            actionMenu.Bind(this, selectionController, commandBridge);
            formalHud.Bind(this, selectionController, eventFeed);
            activityPresenter.Bind(this, entityViewSpawner);
            crowdPresenter.Bind(this);
            workTargetMode.Bind(this, selectionController, commandBridge);
            contentInterrupt.Bind(this, commandBridge, selectionController);
            questJournal.Bind(this, commandBridge, selectionController);
            inventoryPanel.Bind(this);
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

        public void StepTick()
        {
            if (!_session.IsInitialized)
                return;

            var tick = _session.TickOnce();
            if (tick.IsFailure)
            {
                _status = "TICK FAILED: " + tick.Error;
                Debug.LogError("[PlayableHost] " + tick.Error, this);
                return;
            }

            DispatchDrainedEvents();
            RefreshStatus();
        }

        /// <summary>Host 表现层触发的 Content／Quest 事件立即送给打断呈现。</summary>
        public void DispatchDrainedEvents()
        {
            if (_session?.World?.Events == null)
                return;
            var drained = _session.World.Events.Drain();
            if (contentInterrupt != null)
                contentInterrupt.Ingest(drained);
            if (eventFeed != null)
                eventFeed.Ingest(drained);
        }

        public void Resume()
        {
            if (!_session.IsInitialized)
                return;
            if (contentInterrupt != null && contentInterrupt.HasBlockingInterrupt)
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
                Debug.Log(
                    "[PlayableHost] WalkGrid from mapLayout " + preferred.Id +
                    " " + preferred.Width + "x" + preferred.Height +
                    " origin=(" + preferred.OriginX + "," + preferred.OriginY + ")",
                    this);
                return MapLayoutWalkGridBuilder.Create(preferred);
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
