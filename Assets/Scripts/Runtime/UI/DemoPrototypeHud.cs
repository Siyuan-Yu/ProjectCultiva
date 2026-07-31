using System.Collections.Generic;
using UnityEngine;
using XianXia.Unity.Cultivation;
using XianXia.Unity.Input;
using XianXia.Unity.Obligation;
using XianXia.Unity.Presentation;
using XianXia.Unity.Resources;
using XianXia.Unity.Tasks;
using XianXia.Unity.Time;
using XianXia.Unity.World;

namespace XianXia.Unity.UI
{
    /// <summary>
    /// Demo HUD：1920×1080 逻辑布局缩放；时间表为环世界式小时网格（测试可改）；地块悬停显示灵气。
    /// </summary>
    public sealed class DemoPrototypeHud : MonoBehaviour
    {
        private const float RefWidth = 1920f;
        private const float RefHeight = 1080f;
        private const float PanelWidth = 280f;
        private const float SideBtnW = 44f;
        private const float SideBtnH = 28f;
        private const float CellW = 28f;
        private const float CellH = 22f;

        [SerializeField] private GameClock clock;
        [SerializeField] private ScheduleService scheduleService;
        [SerializeField] private ScheduleComplianceTracker complianceTracker;
        [SerializeField] private DemoUnitController[] partyUnits;
        [SerializeField] private ResourceInventory resourceInventory;
        [SerializeField] private DailyTaskSystem dailyTaskSystem;
        [SerializeField] private SupervisorAngerSystem supervisorAnger;
        [SerializeField] private CultivationSystem cultivationSystem;
        [SerializeField] private PartyCommandController partyCommands;
        [SerializeField] private TileHoverProbe tileHoverProbe;

        private bool _showHelp;
        private bool _showClock;
        private bool _showSchedule;
        private bool _showCompliance;
        private bool _showTasks;
        private bool _showResources;
        private bool _showAnger;
        private bool _showCultivation;

        private float _uiScale = 1f;
        private float _logicalW = RefWidth;
        private float _logicalH = RefHeight;

        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _tinyStyle;
        private GUIStyle _toggleOnStyle;
        private GUIStyle _toggleOffStyle;
        private GUIStyle _cellStyle;
        private Matrix4x4 _previousMatrix;

        public void Configure(
            GameClock gameClock,
            ScheduleService schedule,
            ScheduleComplianceTracker tracker,
            DemoUnitController[] units,
            ResourceInventory inventory,
            DailyTaskSystem tasks,
            SupervisorAngerSystem anger,
            CultivationSystem cultivation,
            PartyCommandController commands,
            TileHoverProbe hoverProbe)
        {
            clock = gameClock;
            scheduleService = schedule;
            complianceTracker = tracker;
            partyUnits = units;
            resourceInventory = inventory;
            dailyTaskSystem = tasks;
            supervisorAnger = anger;
            cultivationSystem = cultivation;
            partyCommands = commands;
            tileHoverProbe = hoverProbe;
        }

        private void Update()
        {
            if (clock == null)
            {
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                clock.TogglePause();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad1))
            {
                clock.SetTimeScale(1f);
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad2))
            {
                clock.SetTimeScale(2f);
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha5) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad5))
            {
                clock.SetTimeScale(5f);
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.P))
            {
                clock.SetPaused(true);
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.H))
            {
                _showHelp = !_showHelp;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab))
            {
                CloseAllPanels();
            }

            if (cultivationSystem == null || partyCommands == null)
            {
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.C))
            {
                cultivationSystem.StartCultivationForUnits(partyCommands.SelectedUnits);
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.X))
            {
                cultivationSystem.StopCultivationForUnits(partyCommands.SelectedUnits);
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.G))
            {
                cultivationSystem.UseConcealGrassForUnits(ResolveTargetUnits());
            }
        }

        private void OnGUI()
        {
            BeginScaledGui();
            EnsureStyles();

            DrawTopStatus();
            DrawLeftRail();
            DrawRightRail();
            DrawLeftPanels();
            DrawRightPanels();
            if (_showSchedule)
            {
                DrawScheduleGrid();
            }

            DrawTileHoverTooltip();

            EndScaledGui();
        }

        private void BeginScaledGui()
        {
            float heightScale = Screen.height / RefHeight;
            float widthScale = Screen.width / RefWidth;
            _uiScale = Mathf.Clamp(Mathf.Min(heightScale, widthScale), 0.45f, 1.25f);
            _logicalW = Screen.width / _uiScale;
            _logicalH = Screen.height / _uiScale;
            _previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(_uiScale, _uiScale, 1f));
        }

        private void EndScaledGui()
        {
            GUI.matrix = _previousMatrix;
        }

        private void DrawTopStatus()
        {
            const float barH = 36f;
            GUI.Box(new Rect(0f, 0f, _logicalW, barH), GUIContent.none);

            string scaleText = clock == null
                ? "--"
                : clock.IsPaused ? "暂停" : $"{clock.TimeScale:0}x";
            string dayText = clock == null ? "Day ?" : $"Day {clock.DayNumber}";
            string timeText = clock == null ? "--:--" : clock.FormattedClock;
            string phase = cultivationSystem != null && cultivationSystem.IsNight ? "夜" : "昼";
            GUI.Label(
                new Rect(52f, 6f, 360f, 24f),
                $"{dayText}  {timeText}  [{scaleText}]  {phase}",
                _statusStyle);

            float x = 420f;
            if (clock != null)
            {
                if (GUI.Button(new Rect(x, 5f, 48f, 26f), clock.IsPaused ? "继续" : "暂停"))
                {
                    clock.TogglePause();
                }

                x += 52f;
                if (GUI.Button(new Rect(x, 5f, 32f, 26f), "1x"))
                {
                    clock.SetTimeScale(1f);
                }

                x += 36f;
                if (GUI.Button(new Rect(x, 5f, 32f, 26f), "2x"))
                {
                    clock.SetTimeScale(2f);
                }

                x += 36f;
                if (GUI.Button(new Rect(x, 5f, 32f, 26f), "5x"))
                {
                    clock.SetTimeScale(5f);
                }

                x += 44f;
            }

            if (cultivationSystem != null)
            {
                if (GUI.Button(new Rect(x, 5f, 56f, 26f), "修炼"))
                {
                    cultivationSystem.StartCultivationForUnits(ResolveTargetUnits());
                }

                x += 60f;
                if (GUI.Button(new Rect(x, 5f, 40f, 26f), "停止"))
                {
                    cultivationSystem.StopCultivationForUnits(ResolveTargetUnits());
                }

                x += 44f;
                if (GUI.Button(new Rect(x, 5f, 56f, 26f), "敛息草"))
                {
                    cultivationSystem.UseConcealGrassForUnits(ResolveTargetUnits());
                }

                x += 64f;
            }

            if (GUI.Button(new Rect(x, 5f, 56f, 26f), "全关"))
            {
                CloseAllPanels();
            }

            GUI.Label(
                new Rect(_logicalW - 320f, 8f, 308f, 22f),
                "Tab全关 | 悬停看地块 | 课表可改(测试)",
                _bodyStyle);
        }

        private void DrawLeftRail()
        {
            float x = 4f;
            float y = 44f;
            _showHelp = SideToggle(x, ref y, "帮助", _showHelp);
            _showClock = SideToggle(x, ref y, "时间", _showClock);
            _showSchedule = SideToggle(x, ref y, "课表", _showSchedule);
            _showCompliance = SideToggle(x, ref y, "状态", _showCompliance);
        }

        private void DrawRightRail()
        {
            float x = _logicalW - SideBtnW - 4f;
            float y = 44f;
            _showTasks = SideToggle(x, ref y, "任务", _showTasks);
            _showResources = SideToggle(x, ref y, "资源", _showResources);
            _showAnger = SideToggle(x, ref y, "愤怒", _showAnger);
            _showCultivation = SideToggle(x, ref y, "修炼", _showCultivation);
        }

        private bool SideToggle(float x, ref float y, string label, bool on)
        {
            bool next = ToggleButton(new Rect(x, y, SideBtnW, SideBtnH), label, on);
            y += SideBtnH + 4f;
            return next;
        }

        private void DrawLeftPanels()
        {
            float x = SideBtnW + 10f;
            float y = 44f;
            float maxBottom = _logicalH - 8f;

            if (_showHelp)
            {
                y = DrawPanel(
                    x,
                    y,
                    132f,
                    maxBottom,
                    "操作",
                    "左键选 / Shift多选\n右键工作区=下达工作\n右键空地=自由移动\n滚轮缩放  空格暂停  1/2/5倍速\nC修炼 X停 G敛息草\n悬停地块看灵气",
                    ref _showHelp);
            }

            if (_showClock)
            {
                float realMinutes = clock == null ? 8f : clock.RealMinutesPerGameDay;
                y = DrawPanel(
                    x,
                    y,
                    64f,
                    maxBottom,
                    "时间",
                    $"现实日长：{realMinutes:0.#} 分／游戏日\n配置范围 5～10 分",
                    ref _showClock);
            }

            if (_showCompliance)
            {
                DrawPanel(x, y, EstimateComplianceHeight(), maxBottom, "角色状态", BuildComplianceText(), ref _showCompliance);
            }
        }

        private void DrawRightPanels()
        {
            float x = _logicalW - SideBtnW - PanelWidth - 10f;
            float y = 44f;
            float maxBottom = _logicalH - 8f;

            if (_showTasks)
            {
                y = DrawPanel(x, y, EstimateTaskHeight(), maxBottom, "任务", BuildTaskText(), ref _showTasks);
            }

            if (_showResources)
            {
                int wood = resourceInventory == null ? 0 : resourceInventory.GetAmount(ResourceType.Wood);
                int food = resourceInventory == null ? 0 : resourceInventory.GetAmount(ResourceType.Food);
                int herb = resourceInventory == null ? 0 : resourceInventory.GetAmount(ResourceType.Herb);
                int grass = resourceInventory == null ? 0 : resourceInventory.GetAmount(ResourceType.ConcealGrass);
                y = DrawPanel(
                    x,
                    y,
                    78f,
                    maxBottom,
                    "资源",
                    $"木 {wood}  粮 {food}\n药 {herb}  敛息草 {grass}",
                    ref _showResources);
            }

            if (_showAnger)
            {
                float anger = supervisorAnger == null ? 0f : supervisorAnger.CurrentAnger;
                y = DrawPanel(x, y, 56f, maxBottom, "愤怒", $"{anger:0} / 100", ref _showAnger);
            }

            if (_showCultivation)
            {
                DrawPanel(
                    x,
                    y,
                    EstimateCultivationHeight(),
                    maxBottom,
                    "修炼",
                    BuildCultivationText(),
                    ref _showCultivation);
            }
        }

        private void DrawScheduleGrid()
        {
            if (scheduleService == null || partyUnits == null)
            {
                return;
            }

            int rows = Mathf.Max(1, partyUnits.Length);
            float gridW = 64f + HourlySchedule.HoursPerDay * CellW;
            float gridH = 56f + rows * (CellH + 4f) + 36f;
            float width = Mathf.Min(_logicalW - 100f, Mathf.Max(760f, gridW + 24f));
            float height = gridH;
            float x = (_logicalW - width) * 0.5f;
            float y = _logicalH - height - 10f;

            GUI.Box(new Rect(x, y, width, height), GUIContent.none);
            string editHint = scheduleService.AllowEditForTesting
                ? "测试可改：点击格子循环 睡→起→工→饭→闲"
                : "正式锁定：仅查看";
            GUI.Label(new Rect(x + 12f, y + 6f, width - 160f, 20f), $"时间表（每小时）  {editHint}", _titleStyle);
            if (GUI.Button(new Rect(x + width - 120f, y + 4f, 52f, 22f), "重置"))
            {
                scheduleService.ResetAllToDefaultLaborer();
            }

            if (GUI.Button(new Rect(x + width - 60f, y + 4f, 40f, 22f), "×"))
            {
                _showSchedule = false;
            }

            float legendX = x + 12f;
            float legendY = y + 30f;
            DrawLegend(legendX, legendY);

            float gridX = x + 12f;
            float gridY = y + 52f;
            int currentHour = clock == null ? -1 : clock.Hour;

            for (int hour = 0; hour < HourlySchedule.HoursPerDay; hour++)
            {
                float hx = gridX + 64f + hour * CellW;
                GUI.Label(new Rect(hx, gridY, CellW, 16f), hour.ToString("00"), _tinyStyle);
                if (hour == currentHour)
                {
                    GUI.Box(new Rect(hx, gridY + 14f, CellW - 1f, rows * (CellH + 4f) + 2f), GUIContent.none);
                }
            }

            for (int row = 0; row < partyUnits.Length; row++)
            {
                DemoUnitController unit = partyUnits[row];
                if (unit == null)
                {
                    continue;
                }

                float rowY = gridY + 18f + row * (CellH + 4f);
                GUI.Label(new Rect(gridX, rowY, 60f, CellH), ShortName(unit.name), _bodyStyle);

                for (int hour = 0; hour < HourlySchedule.HoursPerDay; hour++)
                {
                    ScheduleActivity activity = scheduleService.GetActivity(unit, hour);
                    Rect cell = new(gridX + 64f + hour * CellW, rowY, CellW - 1f, CellH);
                    Color previous = GUI.backgroundColor;
                    GUI.backgroundColor = ActivityColor(activity);
                    string mark = ActivityShort(activity);
                    if (GUI.Button(cell, mark, _cellStyle))
                    {
                        scheduleService.TryCycleActivity(unit, hour);
                    }

                    GUI.backgroundColor = previous;
                }
            }
        }

        private void DrawLegend(float x, float y)
        {
            DrawLegendItem(ref x, y, ScheduleActivity.Sleep, "睡");
            DrawLegendItem(ref x, y, ScheduleActivity.WakePrepare, "起");
            DrawLegendItem(ref x, y, ScheduleActivity.Work, "工");
            DrawLegendItem(ref x, y, ScheduleActivity.Meal, "饭");
            DrawLegendItem(ref x, y, ScheduleActivity.Free, "闲");
        }

        private void DrawLegendItem(ref float x, float y, ScheduleActivity activity, string label)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = ActivityColor(activity);
            GUI.Box(new Rect(x, y, 18f, 14f), GUIContent.none);
            GUI.backgroundColor = previous;
            GUI.Label(new Rect(x + 20f, y - 2f, 28f, 18f), label, _tinyStyle);
            x += 48f;
        }

        private void DrawTileHoverTooltip()
        {
            if (tileHoverProbe == null || !tileHoverProbe.HasHover)
            {
                return;
            }

            TileAmbientData tile = tileHoverProbe.HoveredTile;
            Vector3 screen = UnityEngine.Input.mousePosition;
            float mx = screen.x / _uiScale;
            float my = (_logicalH * _uiScale - screen.y) / _uiScale;
            float tipW = 210f;
            float tipH = 86f;
            float tipX = Mathf.Clamp(mx + 16f, 8f, _logicalW - tipW - 8f);
            float tipY = Mathf.Clamp(my + 16f, 40f, _logicalH - tipH - 8f);

            GUI.Box(new Rect(tipX, tipY, tipW, tipH), GUIContent.none);
            GUI.Label(new Rect(tipX + 10f, tipY + 6f, tipW - 20f, 20f), $"地块 ({tile.TileX},{tile.TileY})", _titleStyle);
            GUI.Label(
                new Rect(tipX + 10f, tipY + 28f, tipW - 20f, 50f),
                $"属性能量：{tile.AttributeEnergy:0.0}\n灵气：{tile.SpiritQi:0.0}\n浓郁：{tile.DensityLabel}",
                _bodyStyle);
        }

        private float DrawPanel(
            float x,
            float y,
            float preferredHeight,
            float maxBottom,
            string title,
            string body,
            ref bool visible)
        {
            float height = Mathf.Min(preferredHeight, Mathf.Max(48f, maxBottom - y));
            if (height < 48f)
            {
                return y;
            }

            GUI.Box(new Rect(x, y, PanelWidth, height), GUIContent.none);
            GUI.Label(new Rect(x + 10f, y + 6f, PanelWidth - 48f, 20f), title, _titleStyle);
            if (GUI.Button(new Rect(x + PanelWidth - 34f, y + 4f, 26f, 22f), "×"))
            {
                visible = false;
            }

            GUI.Label(new Rect(x + 10f, y + 28f, PanelWidth - 20f, height - 36f), body, _bodyStyle);
            return y + height + 6f;
        }

        private bool ToggleButton(Rect rect, string label, bool on)
        {
            GUIStyle style = on ? _toggleOnStyle : _toggleOffStyle;
            if (GUI.Button(rect, label, style))
            {
                return !on;
            }

            return on;
        }

        private void CloseAllPanels()
        {
            _showHelp = false;
            _showClock = false;
            _showSchedule = false;
            _showCompliance = false;
            _showTasks = false;
            _showResources = false;
            _showAnger = false;
            _showCultivation = false;
        }

        private string BuildComplianceText()
        {
            if (partyUnits == null || partyUnits.Length == 0)
            {
                return "无角色";
            }

            var lines = new List<string>();
            foreach (DemoUnitController unit in partyUnits)
            {
                if (unit == null)
                {
                    continue;
                }

                string activity = ActivityStateLabel(unit.ActivityState);
                string workDetail = unit.ActivityState == UnitActivityState.Working
                    && unit.AssignedWorkZone != null
                    ? unit.AssignedWorkZone.DisplayName
                    : "-";
                string scheduleBit;
                if (unit.ActivityState == UnitActivityState.Working)
                {
                    scheduleBit = "工作中";
                }
                else if (!unit.RequireWorkPeriod)
                {
                    scheduleBit = "非工时";
                }
                else if (unit.IsScheduleCompliant)
                {
                    scheduleBit = "遵守";
                }
                else
                {
                    scheduleBit = "未工作";
                }

                lines.Add($"{ShortName(unit.name)}: {activity} ({workDetail}) | {scheduleBit}");
            }

            return string.Join("\n", lines);
        }

        private static string ActivityStateLabel(UnitActivityState state)
        {
            return state switch
            {
                UnitActivityState.Idle => "空闲",
                UnitActivityState.Moving => "移动中",
                UnitActivityState.Working => "正在工作",
                _ => state.ToString()
            };
        }

        private string BuildTaskText()
        {
            if (dailyTaskSystem == null || dailyTaskSystem.CurrentTasks.Count == 0)
            {
                return "等待 06:00 任务";
            }

            float remain = dailyTaskSystem.RemainingGameMinutes;
            int remainH = Mathf.FloorToInt(remain / 60f);
            int remainM = Mathf.FloorToInt(remain) % 60;
            var lines = new List<string> { $"剩余 {remainH:00}:{remainM:00}" };
            foreach (DailyTaskState task in dailyTaskSystem.CurrentTasks)
            {
                string state = task.IsComplete ? "完" : "中";
                lines.Add(
                    $"{task.Definition.DisplayName} {task.Progress}/{task.Definition.RequiredAmount}[{state}]");
            }

            return string.Join("\n", lines);
        }

        private string BuildCultivationText()
        {
            bool inSite = cultivationSystem != null && cultivationSystem.AnyUnitInSpiritSite;
            string siteHint = inSite ? "灵地：可修炼" : "灵地：去东南角";
            string riskHint = cultivationSystem != null && cultivationSystem.IsNight
                ? "夜晚：低暴露"
                : "白天：暴露↑";
            var lines = new List<string> { siteHint, riskHint };
            if (partyUnits == null)
            {
                return string.Join("\n", lines);
            }

            foreach (DemoUnitController unit in partyUnits)
            {
                if (unit == null)
                {
                    continue;
                }

                UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
                if (cultivation == null)
                {
                    continue;
                }

                bool unitInSite = cultivationSystem != null && cultivationSystem.IsUnitInSpiritSite(unit);
                string state = cultivation.IsCultivating
                    ? "修炼中"
                    : unitInSite ? "可修" : "待机";
                lines.Add(
                    $"{ShortName(unit.name)} 修{cultivation.CultivationProgress:0} 露{cultivation.ExposureRisk:0} [{state}]");
            }

            return string.Join("\n", lines);
        }

        private float EstimateComplianceHeight()
        {
            int count = partyUnits == null ? 1 : Mathf.Max(1, partyUnits.Length);
            return 44f + count * 18f;
        }

        private float EstimateTaskHeight()
        {
            int count = dailyTaskSystem == null ? 0 : dailyTaskSystem.CurrentTasks.Count;
            return 52f + Mathf.Max(1, count) * 18f;
        }

        private float EstimateCultivationHeight()
        {
            int count = partyUnits == null ? 1 : Mathf.Max(1, partyUnits.Length);
            return 68f + count * 18f;
        }

        private static string ShortName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "?";
            }

            return name.Replace("Player_", "P");
        }

        private static string ActivityShort(ScheduleActivity activity)
        {
            return activity switch
            {
                ScheduleActivity.WakePrepare => "起",
                ScheduleActivity.Work => "工",
                ScheduleActivity.Meal => "饭",
                ScheduleActivity.Free => "闲",
                ScheduleActivity.Sleep => "睡",
                _ => "?"
            };
        }

        private static Color ActivityColor(ScheduleActivity activity)
        {
            return activity switch
            {
                ScheduleActivity.WakePrepare => new Color(0.45f, 0.65f, 0.85f, 1f),
                ScheduleActivity.Work => new Color(0.85f, 0.55f, 0.25f, 1f),
                ScheduleActivity.Meal => new Color(0.9f, 0.8f, 0.3f, 1f),
                ScheduleActivity.Free => new Color(0.4f, 0.75f, 0.45f, 1f),
                ScheduleActivity.Sleep => new Color(0.3f, 0.35f, 0.7f, 1f),
                _ => Color.gray
            };
        }

        private IReadOnlyList<DemoUnitController> ResolveTargetUnits()
        {
            if (partyCommands != null && partyCommands.SelectedUnits.Count > 0)
            {
                return partyCommands.SelectedUnits;
            }

            return partyUnits;
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.92f, 0.86f, 0.68f) }
            };

            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.92f, 0.82f) }
            };

            _tinyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.85f, 0.8f) }
            };

            _cellStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0)
            };

            _toggleOffStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter
            };

            _toggleOnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    textColor = new Color(0.12f, 0.12f, 0.1f),
                    background = MakeColorTexture(new Color(0.88f, 0.8f, 0.42f, 0.95f))
                },
                hover =
                {
                    textColor = Color.black,
                    background = MakeColorTexture(new Color(0.95f, 0.88f, 0.52f, 1f))
                },
                active =
                {
                    textColor = Color.black,
                    background = MakeColorTexture(new Color(0.75f, 0.68f, 0.32f, 1f))
                }
            };
        }

        private static Texture2D MakeColorTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
