using System.IO;
using UnityEngine;
using XianXia.Data.Bootstrap;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// VS0.4 Playable Host entry. Loads BaseGame, builds session, EntityViews, minimal Tick control.
    /// No commands／HUD／Demo migration in Phase B.
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

        [Header("Presentation")]
        [SerializeField] EntityViewSpawner entityViewSpawner;
        [SerializeField] PlayableHostCameraRig cameraRig;

        [Header("Tick debug")]
        [SerializeField] bool initializeOnPlay = true;
        [SerializeField] bool autoTickWhenUnpaused;
        [SerializeField] float secondsPerAutoTick = 0.25f;
        [SerializeField] KeyCode stepTickKey = KeyCode.Space;
        [SerializeField] KeyCode togglePauseKey = KeyCode.P;
        [SerializeField] KeyCode rebuildKey = KeyCode.R;

        PlayableHostSession _session = new PlayableHostSession();
        float _autoTickAccumulator;
        string _resolvedContentPath = string.Empty;
        string _status = "Idle";

        public PlayableHostSession Session => _session;

        public EntityViewSpawner ViewSpawner => entityViewSpawner;

        public string StatusLine => _status;

        public string ResolvedContentPath => _resolvedContentPath;

        void Awake()
        {
            if (entityViewSpawner == null)
                entityViewSpawner = GetComponent<EntityViewSpawner>() ?? GetComponentInChildren<EntityViewSpawner>();
            if (cameraRig == null)
                cameraRig = GetComponent<PlayableHostCameraRig>() ?? GetComponentInChildren<PlayableHostCameraRig>();
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

            if (Input.GetKeyDown(stepTickKey))
                StepTick();

            if (Input.GetKeyDown(rebuildKey))
                TryInitialize();

            if (!_session.IsPaused && autoTickWhenUnpaused)
            {
                _autoTickAccumulator += Time.unscaledDeltaTime;
                var interval = Mathf.Max(0.01f, secondsPerAutoTick);
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

            entityViewSpawner.Clear();

            if (!TryResolveContentPackageDirectory(out _resolvedContentPath, out var pathError))
            {
                _status = "INIT FAILED: " + pathError;
                Debug.LogError("[PlayableHost] " + pathError, this);
                _session.Clear();
                return false;
            }

            var options = new PlayableDayOptions
            {
                DailyRequiredAmount = Mathf.Max(1, dailyRequiredAmount)
            };
            if (overrideObservationDiscoverChance)
                options.ObservationDiscoverChancePercent = observationDiscoverChancePercent;

            var init = _session.Initialize(_resolvedContentPath, options);
            if (init.IsFailure)
            {
                _status = "INIT FAILED: " + init.Error;
                Debug.LogError("[PlayableHost] " + init.Error, this);
                entityViewSpawner.Clear();
                return false;
            }

            entityViewSpawner.Rebuild(_session);
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
            _status = "tick=" + _session.World.Tick.Value +
                      " day=" + day.DayIndex +
                      " tickInDay=" + day.TickInDay +
                      " hour=" + day.HourOfDay +
                      " paused=" + _session.IsPaused +
                      " chars=" + _session.CharacterIds.Count;
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
