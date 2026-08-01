using System.IO;
using UnityEngine;
using XianXia.Data.Bootstrap;

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

        [Header("Session options (Host config only; does not change Core defaults when unused)")]
        [SerializeField] bool overrideObservationDiscoverChance;
        [Range(0, 100)]
        [SerializeField] int observationDiscoverChancePercent = 100;
        [SerializeField] int dailyRequiredAmount = 10;
        [Tooltip("Chapter 01 Reference default. Empty = scenario_playable_day.")]
        [SerializeField] string openingScenarioId = "base:scenario_ch01_reference";

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

        [Header("Tick debug")]
        [SerializeField] bool initializeOnPlay = true;
        [SerializeField] bool autoTickWhenUnpaused = true;
        [SerializeField] float secondsPerAutoTickAt1x = 0.25f;
        [SerializeField] KeyCode togglePauseKey = KeyCode.Space;
        [SerializeField] KeyCode stepTickKey = KeyCode.Period;
        [SerializeField] KeyCode stepTickAltKey = KeyCode.N;
        [SerializeField] KeyCode cycleSpeedKey = KeyCode.RightBracket;
        [SerializeField] KeyCode cycleSpeedAltKey = KeyCode.LeftBracket;
        [SerializeField] KeyCode rebuildKey = KeyCode.R;

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
        }

        void Start()
        {
            if (initializeOnPlay)
                TryInitialize();
        }

        void Update()
        {
            if (!_session.IsInitialized)
                return;

            if (Input.GetKeyDown(togglePauseKey))
            {
                _session.IsPaused = !_session.IsPaused;
                RefreshStatus();
            }

            if (Input.GetKeyDown(stepTickKey) || Input.GetKeyDown(stepTickAltKey))
                StepTick();

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

            selectionController.ClearSelection();
            entityViewSpawner.Clear();
            eventFeed.Clear();
            mapGraybox.Clear();

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

            entityViewSpawner.Rebuild(_session);
            mapGraybox.Rebuild(_session);
            var cam = Camera.main;
            selectionController.Bind(entityViewSpawner, cam);
            selectionController.SetPartyFilter(_session.CharacterIds);
            commandBridge.Bind(_session, selectionController);
            debugHud.Bind(this, selectionController);
            contentDebugPanel.Bind(this, selectionController);
            moveController.Bind(this, selectionController, entityViewSpawner, commandBridge);
            actionMenu.Bind(this, selectionController, commandBridge);
            formalHud.Bind(this, selectionController, eventFeed);
            activityPresenter.Bind(this, entityViewSpawner);
            snapshotPanel.Bind(this);
            // Bootstrap already published WorldInitialized／EntityCreated — capture once.
            eventFeed.PullFrom(_session.World.Events);
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
            entityViewSpawner.Rebuild(_session);
            var cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
            selectionController.Bind(entityViewSpawner, cam);
            selectionController.SetPartyFilter(_session.CharacterIds);
            commandBridge.Bind(_session, selectionController);
            debugHud.Bind(this, selectionController);
            contentDebugPanel.Bind(this, selectionController);
            eventFeed.Clear();
            eventFeed.PullFrom(_session.World.Events);
            FrameCameraOnSlots();
            _autoTickAccumulator = 0f;
            RefreshStatus();
        }

        void FrameCameraOnSlots()
        {
            if (cameraRig == null || entityViewSpawner == null)
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

            if (eventFeed != null)
                eventFeed.PullFrom(_session.World.Events);

            RefreshStatus();
        }

        public void Resume()
        {
            if (!_session.IsInitialized)
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
