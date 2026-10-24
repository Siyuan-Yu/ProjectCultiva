using UnityEngine;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Simulation speed 状态真源 + 可选只读 Runtime 诊断（LevelTester）。
    /// </summary>
    public sealed class HostDebugHud : MonoBehaviour
    {
        // Named scopes make frame callback owners measurable without Deep Profile.
        // ProfilerMarker.Auto is inactive when neither a recorder nor the Profiler enables it.
        internal static readonly ProfilerMarker SurfaceUpdate = new ProfilerMarker("XianXia.Surface.Update");
        internal static readonly ProfilerMarker SurfaceBuildTerrain = new ProfilerMarker("XianXia.Surface.BuildTerrain");
        internal static readonly ProfilerMarker SurfaceBuildPlacements = new ProfilerMarker("XianXia.Surface.BuildPlacements");
        internal static readonly ProfilerMarker SurfaceCommit = new ProfilerMarker("XianXia.Surface.CommitNavigation");
        internal static readonly ProfilerMarker SurfaceMaterialize = new ProfilerMarker("XianXia.Surface.Materialize");
        internal static readonly ProfilerMarker SurfaceOverlay = new ProfilerMarker("XianXia.Surface.Overlay");
        internal static readonly ProfilerMarker SurfaceRetire = new ProfilerMarker("XianXia.Surface.Retire");
        internal static readonly ProfilerMarker FlagsUpdate = new ProfilerMarker("XianXia.Flags.Update");
        internal static readonly ProfilerMarker WorkTargetUpdate = new ProfilerMarker("XianXia.WorkTarget.Update");
        internal static readonly ProfilerMarker WorkTargetPick = new ProfilerMarker("XianXia.WorkTarget.Pick");
        internal static readonly ProfilerMarker WorkTargetWorkSpot = new ProfilerMarker("XianXia.WorkTarget.WorkSpot");
        internal static readonly ProfilerMarker WorkTargetNpc = new ProfilerMarker("XianXia.WorkTarget.Npc");
        internal static readonly ProfilerMarker WorldPickerWorkArea = new ProfilerMarker("XianXia.WorldPicker.WorkArea");
        internal static readonly ProfilerMarker PartyUpdate = new ProfilerMarker("XianXia.Party.Update");
        internal static readonly ProfilerMarker NpcScheduleUpdate = new ProfilerMarker("XianXia.NpcSchedule.Update");
        internal static readonly ProfilerMarker NpcSquadUpdate = new ProfilerMarker("XianXia.NpcSquad.Update");
        internal static readonly ProfilerMarker DestructibleUpdate = new ProfilerMarker("XianXia.Destructible.Update");
        internal static readonly ProfilerMarker MoveUpdate = new ProfilerMarker("XianXia.Move.Update");
        internal static readonly ProfilerMarker SimulationUpdate = new ProfilerMarker("XianXia.Simulation.Update");
        internal static readonly ProfilerMarker SimulationTickOnce = new ProfilerMarker("XianXia.Simulation.TickOnce");
        internal static readonly ProfilerMarker ViewsUpdate = new ProfilerMarker("XianXia.Views.Update");
        internal static readonly ProfilerMarker FlightUpdate = new ProfilerMarker("XianXia.Flight.Update");
        internal static readonly ProfilerMarker EntityLateUpdate = new ProfilerMarker("XianXia.Entity.LateUpdate");
        internal static readonly ProfilerMarker EntitySleep = new ProfilerMarker("XianXia.Entity.Sleep");
        internal static readonly ProfilerMarker EntityArt = new ProfilerMarker("XianXia.Entity.Art");
        internal static readonly ProfilerMarker EntityBounds = new ProfilerMarker("XianXia.Entity.Bounds");
        internal static readonly ProfilerMarker EntityDepth = new ProfilerMarker("XianXia.Entity.Depth");
        internal static readonly ProfilerMarker PartyLateUpdate = new ProfilerMarker("XianXia.Party.LateUpdate");
        internal static readonly ProfilerMarker NpcLateUpdate = new ProfilerMarker("XianXia.Npc.LateUpdate");
        internal static readonly ProfilerMarker ActivityLateUpdate = new ProfilerMarker("XianXia.Activity.LateUpdate");
        internal static readonly ProfilerMarker OpportunityLateUpdate = new ProfilerMarker("XianXia.Opportunity.LateUpdate");
        internal static readonly ProfilerMarker WorkLoopLateUpdate = new ProfilerMarker("XianXia.WorkLoop.LateUpdate");
        internal static readonly ProfilerMarker PathPreviewLateUpdate = new ProfilerMarker("XianXia.PathPreview.LateUpdate");
        internal static readonly ProfilerMarker CrowdLateUpdate = new ProfilerMarker("XianXia.Crowd.LateUpdate");
        internal static readonly ProfilerMarker ChessLateUpdate = new ProfilerMarker("XianXia.Chess.LateUpdate");
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] bool visible;

        int _speedMultiplier = 1;
        HostHudSnapshot _last = new HostHudSnapshot();

        // Counters are opt-in. Uncapped capture is a separate, temporary presentation override.
        bool _performanceVisible;
        bool _uncappedCapture;
        int _savedVSyncCount;
        int _savedTargetFrameRate;
        ProfilerRecorder _drawCalls;
        ProfilerRecorder _gcAllocated;
        SimulationLoop _profiledLoop;
        readonly Dictionary<string, TickPhaseMetric> _tickPhaseMetrics =
            new Dictionary<string, TickPhaseMetric>(System.StringComparer.Ordinal);
        double _previousFrameAt = -1d;
        double _windowSeconds;
        double _slowestFrameMs;
        int _windowFrames;
        int _windowPausedFrames;
        int _windowWasdHeldFrames;
        int _slowFrames;
        long _drawCallTotal;
        long _allocationTotal;
        int _drawCallSamples;
        int _allocationSamples;
        GUIStyle _performanceStyle;
        string _performanceSummary = "F9：开启行走帧耗时显示";
        readonly Queue<string> _performanceWindows = new Queue<string>(30);
        readonly List<TimingMetric> _scriptTimings = new List<TimingMetric>(128);
        string _scriptMarkerInventory = string.Empty;
        readonly TimingMetric[] _timings =
        {
            new TimingMetric("PlayerLoop", "主循环"),
            new TimingMetric("EditorLoop", "编辑器"),
            new TimingMetric("BehaviourUpdate", "Update"),
            new TimingMetric("PreLateUpdate.ScriptRunBehaviourLateUpdate", "LateUpdate"),
            new TimingMetric("Camera.Render", "相机提交"),
            new TimingMetric("Gfx.WaitForPresentOnGfxThread", "渲染等待"),
            new TimingMetric("WaitForTargetFPS", "帧率等待"),
            new TimingMetric("GC.Collect", "GC")
        };

        sealed class TimingMetric
        {
            public readonly string Marker;
            public readonly string Label;
            public ProfilerRecorder Recorder;
            long _totalNanoseconds;
            long _maxNanoseconds;
            int _samples;

            public TimingMetric(string marker, string label) { Marker = marker; Label = label; }
            public void Sample()
            {
                if (!Recorder.Valid || Recorder.Count == 0) return;
                var value = Recorder.LastValue;
                _totalNanoseconds += value;
                if (value > _maxNanoseconds) _maxNanoseconds = value;
                _samples++;
            }
            public string Mean => _samples > 0 ? (_totalNanoseconds / (double)_samples * 1e-6d).ToString("0.00") : "—";
            public double MeanNanoseconds => _samples > 0 ? _totalNanoseconds / (double)_samples : -1d;
            public string Peak => _samples > 0 ? (_maxNanoseconds * 1e-6d).ToString("0.00") : "—";
            public void ResetWindow() { _totalNanoseconds = _maxNanoseconds = 0L; _samples = 0; }
            public void Release() { Recorder.Dispose(); Recorder = default; }
        }

        sealed class TickPhaseMetric
        {
            public long Total;
            public long Peak;
            public int Count;
            public void Add(long ticks)
            {
                Total += ticks;
                if (ticks > Peak) Peak = ticks;
                Count++;
            }
            public void Reset() { Total = Peak = 0L; Count = 0; }
        }

        void BindTickProfiling()
        {
            var loop = _performanceVisible ? bootstrap?.Session?.Loop : null;
            if (ReferenceEquals(_profiledLoop, loop)) return;
            if (_profiledLoop != null) _profiledLoop.TickPhaseSample = null;
            _profiledLoop = loop;
            if (_profiledLoop != null) _profiledLoop.TickPhaseSample = RecordTickPhase;
        }

        void RecordTickPhase(string name, long elapsedTicks)
        {
            if (!_tickPhaseMetrics.TryGetValue(name, out var metric))
                _tickPhaseMetrics.Add(name, metric = new TickPhaseMetric());
            metric.Add(elapsedTicks);
        }

        public bool PerformanceVisible => _performanceVisible;
        public bool UncappedCapture => _uncappedCapture;
        public string PerformanceSummary => _performanceSummary;
        bool PerformanceToolsAllowed => HostPlayerPaths.DevelopmentToolsAllowed || Debug.isDebugBuild;

        public int SpeedMultiplier => _speedMultiplier;

        public HostHudSnapshot LastSnapshot => _last;

        public void SetPerformanceVisible(bool show)
        {
            show &= PerformanceToolsAllowed;
            if (_performanceVisible == show) return;
            if (!show && _windowFrames > 0) PublishPerformanceWindow();
            if (!show) RestoreFramePacing();
            _performanceVisible = show;
            BindTickProfiling();
            ReleasePerformanceCounters();
            ResetFrameWindow();
            _previousFrameAt = -1d;
            _performanceSummary = show ? "正在采样行走帧耗时…" : "F9：开启行走帧耗时显示";
            if (!show)
            {
                SavePerformanceReport();
                return;
            }
            _performanceWindows.Clear();
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
            _gcAllocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            // Resolve only once on opt-in; unavailable/platform-specific markers stay unavailable.
            var handles = new List<ProfilerRecorderHandle>();
            var scriptHandles = new List<(ProfilerRecorderHandle Handle, string Name)>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                if (description.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds) continue;
                foreach (var timing in _timings)
                {
                    if (timing.Recorder.Valid || description.Name != timing.Marker) continue;
                    timing.Recorder = new ProfilerRecorder(handle, 1,
                        ProfilerRecorderOptions.Default | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                    timing.Recorder.Start();
                }
                // Also capture Unity's own component callbacks when exposed by this player.
                // The explicit XianXia scopes remain available when automatic names are absent.
                var name = description.Name;
                if (!(name.StartsWith("XianXia.", System.StringComparison.Ordinal) ||
                      name.Contains(".Update()") || name.Contains(".LateUpdate()") ||
                      name.Contains(".FixedUpdate()"))) continue;
                scriptHandles.Add((handle, name));
            }
            // Keep our explicitly named scopes even when the Editor exposes many callbacks.
            scriptHandles.Sort((a, b) =>
            {
                var priority = b.Name.StartsWith("XianXia.", System.StringComparison.Ordinal).CompareTo(
                    a.Name.StartsWith("XianXia.", System.StringComparison.Ordinal));
                return priority != 0 ? priority : string.CompareOrdinal(a.Name, b.Name);
            });
            for (var i = 0; i < Mathf.Min(128, scriptHandles.Count); i++)
            {
                var entry = scriptHandles[i];
                var script = new TimingMetric(entry.Name, ScriptLabel(entry.Name));
                script.Recorder = new ProfilerRecorder(entry.Handle, 1,
                    ProfilerRecorderOptions.Default | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                script.Recorder.Start();
                _scriptTimings.Add(script);
            }
            var inventory = new StringBuilder();
            foreach (var timing in _scriptTimings) inventory.AppendLine(timing.Marker);
            _scriptMarkerInventory = inventory.ToString();
        }

        public void SetUncappedCapture(bool enabled)
        {
            enabled &= PerformanceToolsAllowed && _performanceVisible;
            if (_uncappedCapture == enabled) return;
            // Keep synchronized and uncapped measurements in separate windows.
            if (_windowFrames > 0) PublishPerformanceWindow();
            ResetFrameWindow();
            _previousFrameAt = -1d;
            if (enabled)
            {
                _savedVSyncCount = QualitySettings.vSyncCount;
                _savedTargetFrameRate = Application.targetFrameRate;
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
                _uncappedCapture = true;
            }
            else RestoreFramePacing();
            _performanceSummary = enabled ? "正在不限帧采样…" : "已恢复原同步/限帧设置，继续采样…";
        }

        void RestoreFramePacing()
        {
            if (!_uncappedCapture) return;
            QualitySettings.vSyncCount = _savedVSyncCount;
            Application.targetFrameRate = _savedTargetFrameRate;
            _uncappedCapture = false;
        }

        static string ScriptLabel(string name)
        {
            switch (name)
            {
                case "XianXia.Surface.Update": return "地图流式";
                case "XianXia.Flags.Update": return "势力旗显示";
                case "XianXia.WorkTarget.Update": return "鼠标目标/光标";
                case "XianXia.Party.Update": return "主控/队伍";
                case "XianXia.NpcSchedule.Update": return "NPC日程移动";
                case "XianXia.NpcSquad.Update": return "NPC小队投影";
                case "XianXia.Destructible.Update": return "伐木/破坏";
                case "XianXia.Move.Update": return "路径移动";
                case "XianXia.Simulation.Update": return "世界Tick/启动";
                case "XianXia.Views.Update": return "人物View同步";
                case "XianXia.Flight.Update": return "飞行";
                case "XianXia.Entity.LateUpdate": return "人物表现总计";
                case "XianXia.Entity.Sleep": return "人物睡眠/床铺";
                case "XianXia.Entity.Art": return "人物骨架/动画";
                case "XianXia.Entity.Bounds": return "人物拾取边界";
                case "XianXia.Entity.Depth": return "人物/床层排序";
                case "XianXia.Party.LateUpdate": return "队伍坐标/镜头/朝向";
                case "XianXia.Npc.LateUpdate": return "搬运人物同步";
                case "XianXia.Activity.LateUpdate": return "人物活动标签";
                case "XianXia.Opportunity.LateUpdate": return "机缘物体显示";
                case "XianXia.WorkLoop.LateUpdate": return "连续工作";
                case "XianXia.PathPreview.LateUpdate": return "路径预览";
                case "XianXia.Crowd.LateUpdate": return "村民标签";
                case "XianXia.Chess.LateUpdate": return "棋位显示";
                default: return name;
            }
        }

        void Update()
        {
            if (!PerformanceToolsAllowed) return;
            if (!HostInputGate.TextInputFocused && Input.GetKeyDown(KeyCode.F9))
            {
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                    SetUncappedCapture(!_uncappedCapture);
                else SetPerformanceVisible(!_performanceVisible);
            }
            if (!_performanceVisible) return;
            BindTickProfiling();
            if (bootstrap?.Session?.IsInitialized != true || !Application.isFocused)
            {
                _previousFrameAt = -1d;
                ResetFrameWindow();
                return;
            }

            // Wall-clock spacing: independent of game pause, 1x/20x speed and deltaTime clamping.
            var now = Time.realtimeSinceStartupAsDouble;
            var seconds = _previousFrameAt < 0d ? 0d : now - _previousFrameAt;
            _previousFrameAt = now;
            if (seconds <= 0d) return;
            _windowSeconds += seconds;
            _windowFrames++;
            if (bootstrap.Session.IsPaused) _windowPausedFrames++;
            else if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) ||
                     Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D)) _windowWasdHeldFrames++;
            var milliseconds = seconds * 1000d;
            if (milliseconds > _slowestFrameMs) _slowestFrameMs = milliseconds;
            if (milliseconds > 1000d / 30d) _slowFrames++;
            if (_drawCalls.Valid && _drawCalls.Count > 0)
            {
                _drawCallTotal += _drawCalls.LastValue;
                _drawCallSamples++;
            }
            if (_gcAllocated.Valid && _gcAllocated.Count > 0)
            {
                _allocationTotal += _gcAllocated.LastValue;
                _allocationSamples++;
            }
            foreach (var timing in _timings) timing.Sample();
            foreach (var timing in _scriptTimings) timing.Sample();
            if (_windowSeconds < 1d) return;
            PublishPerformanceWindow();
            ResetFrameWindow();
        }

        void PublishPerformanceWindow()
        {
            // Build strings once per window, not once per IMGUI event or rendered frame.
            _performanceSummary = "FPS " + (_windowFrames / _windowSeconds).ToString("0.0") +
                "  平均 " + (_windowSeconds * 1000d / _windowFrames).ToString("0.0") + " ms" +
                "\n最慢 " + _slowestFrameMs.ToString("0.0") + " ms  >33.3ms：" + _slowFrames + " 帧" +
                "\n绘制提交/帧 " + (_drawCallSamples > 0 ? (_drawCallTotal / (double)_drawCallSamples).ToString("0") : "不可用") +
                "  分配/帧 " + (_allocationSamples > 0 ? (_allocationTotal / (1024d * _allocationSamples)).ToString("0.0") + " KB" : "不可用") +
                "\n120/200 FPS预算：8.33/5ms  VSync=" + QualitySettings.vSyncCount + "  上限=" + Application.targetFrameRate +
                "\nCPU标记均值(ms，父子项有重叠，不能相加)：";
            for (var i = 0; i < _timings.Length; i += 2)
                _performanceSummary += "\n" + _timings[i].Label + " " + _timings[i].Mean +
                    "  |  " + _timings[i + 1].Label + " " + _timings[i + 1].Mean;

            _scriptTimings.Sort((a, b) => b.MeanNanoseconds.CompareTo(a.MeanNanoseconds));
            for (var i = 0; i < Mathf.Min(2, _scriptTimings.Count); i++)
                _performanceSummary += "\n热点 " + (i + 1) + "：" + _scriptTimings[i].Label +
                    " " + _scriptTimings[i].Mean + " ms";

            var sample = new StringBuilder(768);
            sample.Append(System.DateTime.UtcNow.ToString("O")).Append(" windowSeconds=")
                .Append(_windowSeconds.ToString("0.000")).Append(" frames=").Append(_windowFrames)
                .Append(" speed=").Append(_speedMultiplier)
                .Append(" paused=").Append(bootstrap?.Session?.IsPaused == true)
                .Append(" pausedFrames=").Append(_windowPausedFrames)
                .Append(" wasdHeldFrames=").Append(_windowWasdHeldFrames)
                .Append(" uncappedCapture=").Append(_uncappedCapture).AppendLine();
            sample.AppendLine(_performanceSummary);
            foreach (var timing in _timings)
                sample.Append(timing.Marker).Append(" meanMs=").Append(timing.Mean)
                    .Append(" peakMs=").Append(timing.Peak).AppendLine();
            sample.AppendLine("Core Tick phases (mean/peak per actual WorldTick, not per rendered frame):");
            foreach (var pair in _tickPhaseMetrics)
            {
                var metric = pair.Value;
                if (metric.Count == 0) continue;
                var scale = 1000d / System.Diagnostics.Stopwatch.Frequency;
                sample.Append(pair.Key).Append(" count=").Append(metric.Count)
                    .Append(" meanMs=").Append((metric.Total * scale / metric.Count).ToString("0.00"))
                    .Append(" peakMs=").Append((metric.Peak * scale).ToString("0.00")).AppendLine();
            }
            sample.AppendLine("Script scopes (all captured; automatic/explicit scopes may overlap):");
            for (var i = 0; i < _scriptTimings.Count; i++)
            {
                var timing = _scriptTimings[i];
                sample.Append(timing.Marker).Append(" meanMs=").Append(timing.Mean)
                    .Append(" peakMs=").Append(timing.Peak).AppendLine();
            }
            if (_performanceWindows.Count >= 30) _performanceWindows.Dequeue();
            _performanceWindows.Enqueue(sample.ToString());
        }

        void SavePerformanceReport()
        {
            if (_performanceWindows.Count == 0) return;
            try
            {
                var directory = Application.isEditor
                    ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp"))
                    : Application.persistentDataPath;
                var path = Path.Combine(directory, "339-performance-latest.txt");
                var report = new StringBuilder(24576);
                report.AppendLine("XianXia movement performance — recent manual F9 capture")
                    .Append("Unity=").Append(Application.unityVersion).Append(" Editor=").Append(Application.isEditor).AppendLine()
                    .Append("CPU=").Append(SystemInfo.processorType).Append(" GPU=").Append(SystemInfo.graphicsDeviceName)
                    .Append(" RAM_MB=").Append(SystemInfo.systemMemorySize).AppendLine()
                    .Append("GameView=").Append(Screen.width).Append('x').Append(Screen.height)
                    .Append(" displayHz=").Append(Screen.currentResolution.refreshRateRatio.value.ToString("0.0")).AppendLine()
                    .AppendLine("CPU marker scopes may overlap/nest. Wait markers are not GPU execution time. — means unavailable/no sample.")
                    .AppendLine("paused is the end-of-window state; pausedFrames counts paused samples. wasdHeldFrames counts unpaused held keys, not verified movement.")
                    .AppendLine("120/200 FPS are reference budgets, not frame caps. uncappedCapture temporarily sets VSync=0 and targetFrameRate=-1; F9 close/disable restores prior settings.")
                    .AppendLine("Script markers discovered at capture start (up to 128):")
                    .AppendLine(_scriptMarkerInventory);
                foreach (var window in _performanceWindows) report.AppendLine(window);
                Directory.CreateDirectory(directory);
                File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
                _performanceSummary = "已保存行走采样：\n" + path + "\nF9可重新采样。";
                Debug.Log("[MovementPerformance] Saved " + path, this);
            }
            catch (System.Exception error) when (error is IOException || error is System.UnauthorizedAccessException)
            {
                _performanceSummary = "行走采样保存失败：" + error.Message;
                Debug.LogWarning("[MovementPerformance] " + _performanceSummary, this);
            }
        }

        void ResetFrameWindow()
        {
            _windowSeconds = 0d;
            _slowestFrameMs = 0d;
            _windowFrames = _slowFrames = _drawCallSamples = _allocationSamples = 0;
            _windowPausedFrames = _windowWasdHeldFrames = 0;
            _drawCallTotal = _allocationTotal = 0L;
            foreach (var timing in _timings) timing.ResetWindow();
            foreach (var timing in _scriptTimings) timing.ResetWindow();
            foreach (var metric in _tickPhaseMetrics.Values) metric.Reset();
        }

        void ReleasePerformanceCounters()
        {
            _drawCalls.Dispose();
            _gcAllocated.Dispose();
            _drawCalls = default;
            _gcAllocated = default;
            foreach (var timing in _timings) timing.Release();
            foreach (var timing in _scriptTimings) timing.Release();
            _scriptTimings.Clear();
        }

        void OnDisable()
        {
            if (_performanceVisible) SetPerformanceVisible(false);
            RestoreFramePacing();
            ReleasePerformanceCounters();
            _performanceVisible = false;
            _previousFrameAt = -1d;
            ResetFrameWindow();
        }

        void OnApplicationFocus(bool focused)
        {
            // Returning from another app must not count the unfocused interval as a game hitch.
            _previousFrameAt = -1d;
            ResetFrameWindow();
        }

        public void Bind(PlayableHostBootstrap hostBootstrap, HostSelectionController selection)
        {
            bootstrap = hostBootstrap;
            selectionController = selection;
        }

        public void SetSpeedMultiplier(int multiplier)
        {
            if (multiplier != 1 && multiplier != 2 && multiplier != 5 && multiplier != 20)
                return;
            _speedMultiplier = multiplier;
            bootstrap?.ResetAutoTickAccumulator();
        }

        public void CycleSpeed()
        {
            _speedMultiplier = _speedMultiplier == 1 ? 2
                : _speedMultiplier == 2 ? 5
                : _speedMultiplier == 5 ? 20
                : 1;
            bootstrap?.ResetAutoTickAccumulator();
        }

        public HostHudSnapshot Refresh()
        {
            var focus = EntityId.None;
            var peer = EntityId.None;
            if (selectionController != null && selectionController.State.Count > 0)
            {
                focus = selectionController.State.SelectedIds[0];
                if (selectionController.State.Count > 1)
                    peer = selectionController.State.SelectedIds[1];
            }

            var session = bootstrap != null ? bootstrap.Session : null;
            _last = HostHudSnapshot.Capture(session, focus, _speedMultiplier, peer);
            return _last;
        }

        void OnGUI()
        {
            if (_performanceVisible && PerformanceToolsAllowed && Event.current.type == EventType.Repaint)
                DrawPerformance();
            if (!HostPlayerPaths.DevelopmentToolsAllowed) return;
            if (!visible)
                return;

            Refresh();
            var text = _last.ToDebugText();
            const float pad = 8f;
            var width = 460f;
            var height = 250f;
            var rect = new Rect(pad, Screen.height - height - pad, width, height);
            GUI.Box(rect, "Runtime Diagnostics");
            GUI.Label(new Rect(rect.x + 8f, rect.y + 22f, rect.width - 16f, rect.height - 28f), text);
        }

        void DrawPerformance()
        {
            if (_performanceStyle == null)
            {
                _performanceStyle = HostImguiStyles.InkLabel(13, ink: Color.white);
                _performanceStyle.alignment = TextAnchor.UpperLeft;
            }
            const float pad = 8f;
            var rect = new Rect(pad, HostFormalHud.HeaderReservedHeight + pad,
                Mathf.Min(610f, Screen.width - pad * 2f), 270f);
            var previousColor = GUI.color;
            var previousDepth = GUI.depth;
            GUI.depth = -1000;
            GUI.color = new Color(.06f, .07f, .08f, .94f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 20f),
                _uncappedCapture ? "行走性能 · 不限帧试采 · 每约1秒统计" : "行走性能 · 原同步/限帧设置 · 每约1秒统计", _performanceStyle);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 28f, rect.width - 16f, 214f),
                _performanceSummary, _performanceStyle);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 248f, rect.width - 16f, 18f),
                "Ctrl+F9切换不限帧 · F9保存并恢复设置 · —无样本", _performanceStyle);
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }
    }
}
