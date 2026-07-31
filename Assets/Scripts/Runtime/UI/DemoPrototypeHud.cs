using System.Collections.Generic;
using UnityEngine;
using XianXia.Unity.Actions;
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
        private const float UiScaleMultiplier = 1.35f;
        private const float PanelWidth = 320f;
        private const float SideBtnW = 52f;
        private const float SideBtnH = 32f;
        private const float CellW = 34f;
        private const float CellH = 28f;

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
        private bool _showTasks;
        private bool _showResources;
        private bool _showAnger;
        private bool _showCultivation;
        private bool _showInspect;

        private float _uiScale = 1f;
        private float _logicalW = RefWidth;
        private float _logicalH = RefHeight;
        private readonly List<Rect> _uiBlockRects = new();

        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _tinyStyle;
        private GUIStyle _toggleOnStyle;
        private GUIStyle _toggleOffStyle;
        private GUIStyle _cellStyle;
        private Matrix4x4 _previousMatrix;
        private Texture2D _barBg;
        private Texture2D _barFillCultivation;
        private Texture2D _barFillExposure;
        private Texture2D _barFillQi;

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
            if (partyCommands != null)
            {
                partyCommands.IsPointerOverUi = IsPointerOverInteractiveUi;
            }
        }

        public bool IsPointerOverInteractiveUi(Vector2 screenPosition)
        {
            if (_uiBlockRects.Count == 0 || _uiScale <= 0f)
            {
                return false;
            }

            float logicalX = screenPosition.x / _uiScale;
            float logicalY = (Screen.height - screenPosition.y) / _uiScale;
            Vector2 logical = new(logicalX, logicalY);
            for (int i = 0; i < _uiBlockRects.Count; i++)
            {
                if (_uiBlockRects[i].Contains(logical))
                {
                    return true;
                }
            }

            return false;
        }

        private void RegisterUiBlock(Rect logicalRect)
        {
            _uiBlockRects.Add(logicalRect);
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

            if (partyCommands != null && UnityEngine.Input.GetKeyDown(KeyCode.S))
            {
                partyCommands.StopSelectedOrders();
            }

            if (cultivationSystem == null || partyCommands == null)
            {
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.C))
            {
                partyCommands.CancelCommandTargeting();
                // 修炼走统一行动：右键灵地优先；快捷键对选中单位下令前往灵地入定。
                if (cultivationSystem != null && cultivationSystem.SpiritSite != null)
                {
                    foreach (DemoUnitController u in partyCommands.SelectedUnits)
                    {
                        if (u == null)
                        {
                            continue;
                        }

                        CharacterActionController actions = u.GetComponent<CharacterActionController>();
                        if (actions == null)
                        {
                            actions = u.gameObject.AddComponent<CharacterActionController>();
                        }

                        actions.IssueCultivate(cultivationSystem.SpiritSite);
                    }
                }
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.X))
            {
                foreach (DemoUnitController u in partyCommands.SelectedUnits)
                {
                    if (u == null)
                    {
                        continue;
                    }

                    CharacterActionController actions = u.GetComponent<CharacterActionController>();
                    if (actions != null && actions.IsActivelyCultivating())
                    {
                        actions.Cancel("玩家出定");
                    }
                }

                cultivationSystem.StopCultivationForUnits(partyCommands.SelectedUnits);
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.G))
            {
                cultivationSystem.UseConcealGrassForUnits(ResolveTargetUnits());
            }
        }

        private void Start()
        {
            EnsureZoneMapLabels();
            EnsureAmbientWorld();
            if (partyCommands != null)
            {
                partyCommands.IsPointerOverUi = IsPointerOverInteractiveUi;
            }
        }

        private void EnsureAmbientWorld()
        {
            AmbientWorldBootstrap ambient = FindObjectOfType<AmbientWorldBootstrap>();
            if (ambient == null)
            {
                ambient = gameObject.AddComponent<AmbientWorldBootstrap>();
            }

            ambient.Configure(clock, scheduleService);
        }

        private void EnsureZoneMapLabels()
        {
            ZoneMapLabelOverlay overlay = FindObjectOfType<ZoneMapLabelOverlay>();
            if (overlay == null)
            {
                overlay = gameObject.AddComponent<ZoneMapLabelOverlay>();
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                overlay.Configure(cam);
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
            DrawUnitActionBar();

            EndScaledGui();
        }

        private void BeginScaledGui()
        {
            float heightScale = Screen.height / RefHeight;
            float widthScale = Screen.width / RefWidth;
            _uiScale = Mathf.Clamp(Mathf.Min(heightScale, widthScale) * UiScaleMultiplier, 0.55f, 1.55f);
            _logicalW = Screen.width / _uiScale;
            _logicalH = Screen.height / _uiScale;
            _uiBlockRects.Clear();
            _previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(_uiScale, _uiScale, 1f));
        }

        private void EndScaledGui()
        {
            GUI.matrix = _previousMatrix;
        }

        private void DrawTopStatus()
        {
            const float barH = 44f;
            RegisterUiBlock(new Rect(0f, 0f, _logicalW, barH));
            GUI.Box(new Rect(0f, 0f, _logicalW, barH), GUIContent.none);

            string scaleText = clock == null
                ? "--"
                : clock.IsPaused ? "暂停" : $"{clock.TimeScale:0}x";
            string dayText = clock == null ? "Day ?" : $"Day {clock.DayNumber}";
            string timeText = clock == null ? "--:--" : clock.FormattedClock;
            string phase = cultivationSystem != null && cultivationSystem.IsNight ? "夜" : "昼";
            GUI.Label(
                new Rect(52f, 8f, 400f, 28f),
                $"{dayText}  {timeText}  [{scaleText}]  {phase}",
                _statusStyle);

            float x = 460f;
            if (clock != null)
            {
                if (GUI.Button(new Rect(x, 6f, 56f, 32f), clock.IsPaused ? "继续" : "暂停"))
                {
                    clock.TogglePause();
                }

                x += 60f;
                if (GUI.Button(new Rect(x, 6f, 40f, 32f), "1x"))
                {
                    clock.SetTimeScale(1f);
                }

                x += 44f;
                if (GUI.Button(new Rect(x, 6f, 40f, 32f), "2x"))
                {
                    clock.SetTimeScale(2f);
                }

                x += 44f;
                if (GUI.Button(new Rect(x, 6f, 40f, 32f), "5x"))
                {
                    clock.SetTimeScale(5f);
                }

                x += 52f;
            }

            if (cultivationSystem != null)
            {
                if (GUI.Button(new Rect(x, 6f, 64f, 32f), "入定"))
                {
                    partyCommands?.CancelCommandTargeting();
                    if (cultivationSystem.SpiritSite != null)
                    {
                        foreach (DemoUnitController u in ResolveTargetUnits())
                        {
                            if (u == null)
                            {
                                continue;
                            }

                            CharacterActionController actions = u.GetComponent<CharacterActionController>()
                                ?? u.gameObject.AddComponent<CharacterActionController>();
                            actions.IssueCultivate(cultivationSystem.SpiritSite);
                        }
                    }
                }

                x += 68f;
                if (GUI.Button(new Rect(x, 6f, 48f, 32f), "出定"))
                {
                    foreach (DemoUnitController u in ResolveTargetUnits())
                    {
                        if (u == null)
                        {
                            continue;
                        }

                        CharacterActionController actions = u.GetComponent<CharacterActionController>();
                        if (actions != null && actions.IsActivelyCultivating())
                        {
                            actions.Cancel("玩家出定");
                        }
                    }

                    cultivationSystem.StopCultivationForUnits(ResolveTargetUnits());
                }

                x += 52f;
                if (GUI.Button(new Rect(x, 6f, 64f, 32f), "敛息草"))
                {
                    cultivationSystem.UseConcealGrassForUnits(ResolveTargetUnits());
                }

                x += 72f;
            }

            if (GUI.Button(new Rect(x, 6f, 64f, 32f), "全关"))
            {
                CloseAllPanels();
            }

            GUI.Label(
                new Rect(_logicalW - 360f, 8f, 348f, 22f),
                "框选/点查 | 中键拖地图 | S停止 | Tab全关",
                _bodyStyle);
        }

        private void DrawLeftRail()
        {
            float x = 4f;
            float y = 44f;
            _showHelp = SideToggle(x, ref y, "帮助", _showHelp);
            _showInspect = SideToggle(x, ref y, "详情", _showInspect);
            _showClock = SideToggle(x, ref y, "时间", _showClock);
        }

        private void DrawRightRail()
        {
            float x = _logicalW - SideBtnW - 4f;
            float y = 44f;
            _showSchedule = SideToggle(x, ref y, "课表", _showSchedule);
            _showTasks = SideToggle(x, ref y, "任务", _showTasks);
            _showResources = SideToggle(x, ref y, "资源", _showResources);
            _showAnger = SideToggle(x, ref y, "愤怒", _showAnger);
            _showCultivation = SideToggle(x, ref y, "修炼", _showCultivation);
        }

        private bool SideToggle(float x, ref float y, string label, bool on)
        {
            Rect rect = new(x, y, SideBtnW, SideBtnH);
            RegisterUiBlock(rect);
            bool next = ToggleButton(rect, label, on);
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
                    210f,
                    maxBottom,
                    "操作",
                    "左键点选 / 拖拽框选 / 双击全选\n右键地面=移动\n右键工位=采集/耕作（自动走近）\n右键灵地=开始修炼\nW=工作选目标 A=攻击\nC=令选中角色前往灵地修炼\nS停止 · 暂停/倍速影响行动进度",
                    ref _showHelp);
            }

            if (_showInspect)
            {
                y = DrawPanel(
                    x,
                    y,
                    EstimateInspectHeight(),
                    maxBottom,
                    "详情",
                    BuildInspectText(),
                    ref _showInspect);
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
        }

        private void DrawRightPanels()
        {
            // 课表竖栏在右侧时，任务等面板往左让一点。
            float scheduleShift = _showSchedule ? 156f : 0f;
            float x = _logicalW - SideBtnW - PanelWidth - 10f - scheduleShift;
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
            if (scheduleService == null)
            {
                return;
            }

            const float scheduleWidth = 148f;
            const float rowH = 26f;
            const float headerH = 78f;
            float height = Mathf.Min(
                _logicalH - 52f,
                headerH + HourlySchedule.HoursPerDay * rowH + 10f);
            float x = _logicalW - SideBtnW - scheduleWidth - 8f;
            float y = 44f;
            RegisterUiBlock(new Rect(x, y, scheduleWidth, height));
            GUI.Box(new Rect(x, y, scheduleWidth, height), GUIContent.none);

            GUI.Label(new Rect(x + 8f, y + 4f, scheduleWidth - 70f, 20f), "劳役表（全村）", _titleStyle);
            if (GUI.Button(new Rect(x + scheduleWidth - 62f, y + 4f, 28f, 20f), "重置"))
            {
                scheduleService.ResetVillageToDefaultLaborer();
            }

            if (GUI.Button(new Rect(x + scheduleWidth - 30f, y + 4f, 22f, 20f), "×"))
            {
                _showSchedule = false;
            }

            string editHint = scheduleService.AllowEditForTesting ? "测试可改" : "仅查看";
            GUI.Label(
                new Rect(x + 8f, y + 24f, scheduleWidth - 16f, 32f),
                $"{editHint} · 点格子循环\n工时偷懒被发现→愤怒",
                _tinyStyle);

            float legendX = x + 6f;
            float legendY = y + 56f;
            DrawLegendCompact(legendX, legendY);

            int currentHour = clock == null ? -1 : clock.Hour;
            float listY = y + headerH;
            float maxListBottom = y + height - 6f;

            for (int hour = 0; hour < HourlySchedule.HoursPerDay; hour++)
            {
                float rowY = listY + hour * rowH;
                if (rowY + rowH > maxListBottom)
                {
                    break;
                }

                ScheduleActivity activity = scheduleService.GetVillageActivity(hour);
                bool isNow = hour == currentHour;

                if (isNow)
                {
                    Color previousBox = GUI.color;
                    GUI.color = new Color(1f, 0.92f, 0.45f, 0.35f);
                    GUI.DrawTexture(new Rect(x + 4f, rowY, scheduleWidth - 8f, rowH - 1f), Texture2D.whiteTexture);
                    GUI.color = previousBox;
                }

                GUI.Label(new Rect(x + 8f, rowY + 2f, 28f, rowH - 2f), hour.ToString("00"), _tinyStyle);

                Rect cell = new(x + 38f, rowY + 1f, scheduleWidth - 48f, rowH - 3f);
                Color previous = GUI.backgroundColor;
                GUI.backgroundColor = ActivityColor(activity);
                string mark = isNow
                    ? $"{ActivityShort(activity)}◀"
                    : ActivityShort(activity);
                if (GUI.Button(cell, mark, _cellStyle))
                {
                    scheduleService.TryCycleVillageActivity(hour);
                }

                GUI.backgroundColor = previous;
            }
        }

        private void DrawLegendCompact(float x, float y)
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
            GUI.Box(new Rect(x, y, 12f, 12f), GUIContent.none);
            GUI.backgroundColor = previous;
            GUI.Label(new Rect(x + 13f, y - 2f, 16f, 16f), label, _tinyStyle);
            x += 28f;
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

            RegisterUiBlock(new Rect(x, y, PanelWidth, height));
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
            _showInspect = false;
            _showClock = false;
            _showSchedule = false;
            _showTasks = false;
            _showResources = false;
            _showAnger = false;
            _showCultivation = false;
        }

        private string BuildInspectText()
        {
            if (partyCommands == null)
            {
                return "点选角色、NPC、建筑、工作区或灵地";
            }

            if (partyCommands.SelectedUnits.Count > 0)
            {
                return partyCommands.SelectedUnits.Count == 1
                    ? "可控角色 · 详见底部状态栏"
                    : $"已选中 {partyCommands.SelectedUnits.Count} 人 · 详见底部状态栏";
            }

            if (partyCommands.Inspection != null && partyCommands.Inspection.HasTarget)
            {
                WorldInspection inspection = partyCommands.Inspection;
                switch (inspection.Kind)
                {
                    case WorldInspectKind.Unit:
                        return "可控角色 · 详见底部状态栏";
                    case WorldInspectKind.NpcCharacter:
                        return "NPC · 详见底部状态栏（不可操控）";
                    case WorldInspectKind.Structure:
                        return BuildStructureInspectText(inspection.Structure);
                    case WorldInspectKind.WorkZone:
                        return BuildWorkZoneInspectText(inspection.WorkZone);
                    case WorldInspectKind.SpiritSite:
                        return BuildSpiritSiteInspectText(inspection.SpiritSite);
                    default:
                        return "无目标";
                }
            }

            return "点选角色、NPC、建筑、工作区或灵地";
        }

        private static string BuildStructureInspectText(StructureInspectable structure)
        {
            if (structure == null)
            {
                return "建筑已消失";
            }

            return $"{structure.DisplayName}\n用途：{structure.Purpose}\n状态：{structure.StatusNote}";
        }

        private string BuildWorkZoneInspectText(WorkZone zone)
        {
            if (zone == null)
            {
                return "工作区已消失";
            }

            int inside = partyCommands.CountUnitsInside(zone);
            int working = partyCommands.CountWorkingInside(zone);
            string resource = zone.ResourceType switch
            {
                ResourceType.Wood => "木材",
                ResourceType.Food => "粮食",
                ResourceType.Herb => "草药",
                ResourceType.ConcealGrass => "敛息草",
                _ => zone.ResourceType.ToString()
            };
            return $"{zone.DisplayName}\n产出：{resource}（{zone.UnitsPerGameHour:0.#}/游戏时）\n工位数：{zone.Spots.Count}\n区内人数：{inside}\n正在工作：{working}\n黄色圈=工位\n选人→工作(W)→再点工位寻路开工";
        }

        private string BuildSpiritSiteInspectText(SpiritSiteZone site)
        {
            if (site == null)
            {
                return "灵地已消失";
            }

            int inside = partyCommands.CountUnitsInside(site);
            int cultivating = partyCommands.CountCultivatingInside(site);
            return $"{site.DisplayName}\n位置：地图东南角（青色菱形标记）\n修炼=停下就地入定（非选目标）\n未入定时可采敛息草\n采草：{site.ConcealGrassPerGameHour:0.#}/游戏时\n区内人数：{inside}\n正在修炼：{cultivating}\n人选中后按 C 入定／X 出定";
        }

        private float EstimateInspectHeight()
        {
            if (partyCommands == null)
            {
                return 64f;
            }

            if (partyCommands.SelectedUnits.Count > 0)
            {
                return 56f;
            }

            if (partyCommands.Inspection != null && partyCommands.Inspection.HasTarget)
            {
                return partyCommands.Inspection.Kind switch
                {
                    WorldInspectKind.Unit => 56f,
                    WorldInspectKind.NpcCharacter => 56f,
                    WorldInspectKind.WorkZone => 118f,
                    WorldInspectKind.SpiritSite => 118f,
                    _ => 86f
                };
            }

            return 64f;
        }

        private static string ActivityStateLabel(UnitActivityState state)
        {
            return state switch
            {
                UnitActivityState.Idle => "空闲",
                UnitActivityState.Moving => "移动中",
                UnitActivityState.Working => "正在工作",
                UnitActivityState.Attacking => "交战中",
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

        private void DrawUnitActionBar()
        {
            if (partyCommands == null)
            {
                return;
            }

            if (partyCommands.SelectedUnits.Count > 0)
            {
                DrawControllableUnitBar();
                return;
            }

            if (partyCommands.Inspection != null
                && partyCommands.Inspection.Kind == WorldInspectKind.NpcCharacter
                && partyCommands.Inspection.NpcCharacter != null)
            {
                DrawReadOnlyNpcBar(partyCommands.Inspection.NpcCharacter);
            }
        }

        private void DrawControllableUnitBar()
        {
            bool single = partyCommands.SelectedUnits.Count == 1;
            float barWidth = single ? 680f : 620f;
            float barHeight = single ? 168f : 110f;
            float x = (_logicalW - barWidth) * 0.5f;
            float bottomPad = 10f;
            float y = _logicalH - barHeight - bottomPad;
            Rect panelRect = new(x, y, barWidth, barHeight);
            RegisterUiBlock(panelRect);
            GUI.Box(panelRect, GUIContent.none);

            if (single)
            {
                DrawSingleUnitPanel(partyCommands.SelectedUnits[0], x, y, barWidth, barHeight);
            }
            else
            {
                DrawMultiUnitPanel(x, y, barWidth, barHeight);
            }
        }

        private void DrawReadOnlyNpcBar(WorldCharacterInspectable npc)
        {
            const float barWidth = 620f;
            const float barHeight = 152f;
            float x = (_logicalW - barWidth) * 0.5f;
            float bottomPad = 10f;
            float y = _logicalH - barHeight - bottomPad;
            Rect panelRect = new(x, y, barWidth, barHeight);
            RegisterUiBlock(panelRect);
            GUI.Box(panelRect, GUIContent.none);

            AmbientNpcActor actor = npc.GetComponent<AmbientNpcActor>();
            string activity = actor != null ? actor.CurrentActivityLabel : "待命";
            GUI.Label(
                new Rect(x + 14f, y + 6f, barWidth * 0.58f, 24f),
                $"{npc.DisplayName}（{activity}）",
                _statusStyle);
            GUI.Label(
                new Rect(x + barWidth - 200f, y + 8f, 188f, 22f),
                $"{npc.RoleTitle} · {npc.Realm}",
                _titleStyle);

            DrawStatBar(
                x + 14f,
                y + 36f,
                barWidth - 28f,
                "身份",
                1f,
                1f,
                _barFillQi,
                npc.RoleTitle);
            if (npc.ThreatLevel > 0.01f)
            {
                DrawStatBar(
                    x + 14f,
                    y + 62f,
                    barWidth - 28f,
                    "威胁",
                    npc.ThreatLevel * 100f,
                    100f,
                    _barFillExposure,
                    $"{npc.ThreatLevel * 100f:0}/100");
            }

            DrawStatBar(
                x + 14f,
                y + npc.ThreatLevel > 0.01f ? 88f : 62f,
                barWidth - 28f,
                "当前",
                0.65f,
                1f,
                _barFillCultivation,
                activity);

            GUI.Label(
                new Rect(x + 14f, y + barHeight - 44f, barWidth - 28f, 18f),
                npc.StatusNote,
                _tinyStyle);
            GUI.Label(
                new Rect(x + 14f, y + barHeight - 24f, barWidth - 28f, 18f),
                "不可操控 · 点空白或其他角色切换",
                _tinyStyle);
        }

        private void DrawSingleUnitPanel(DemoUnitController unit, float x, float y, float width, float height)
        {
            if (unit == null)
            {
                GUI.Label(new Rect(x + 12f, y + 8f, width - 24f, 24f), "单位已消失", _statusStyle);
                return;
            }

            UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
            CharacterActionController actions = unit.GetComponent<CharacterActionController>();
            string activity = DescribeUnitOrder(unit, cultivation);
            string realm = "感应境";
            GUI.Label(
                new Rect(x + 14f, y + 6f, width * 0.62f, 24f),
                $"{ShortName(unit.name)}（{activity}）",
                _statusStyle);
            GUI.Label(
                new Rect(x + width - 120f, y + 8f, 108f, 22f),
                realm,
                _titleStyle);

            float progress = cultivation == null ? 0f : cultivation.CultivationProgress;
            float exposure = cultivation == null ? 0f : cultivation.ExposureRisk;
            bool cultivating = actions != null
                ? actions.IsActivelyCultivating()
                : cultivation != null && cultivation.IsCultivating;
            bool inSpirit = cultivationSystem != null && cultivationSystem.IsUnitInSpiritSite(unit);
            float qiRate = 0f;
            if (cultivating)
            {
                ActionSettings settings = FindObjectOfType<ActionSettings>();
                qiRate = settings != null
                    ? settings.CultivateProgressPerGameHour
                    : (cultivationSystem != null && cultivationSystem.Config != null
                        ? cultivationSystem.Config.ProgressPerGameHour
                        : 80f);
            }

            string qiLabel = cultivating
                ? $"灵气吸收  +{qiRate:0.#}/游戏时"
                : inSpirit
                    ? "灵气环境  浓郁（右键开始修炼）"
                    : "灵气环境  普通";

            DrawStatBar(
                x + 14f,
                y + 36f,
                width - 28f,
                "修为",
                progress,
                UnitCultivation.MaxProgress,
                _barFillCultivation,
                $"{progress:0}/{UnitCultivation.MaxProgress:0}");
            DrawStatBar(
                x + 14f,
                y + 62f,
                width - 28f,
                "暴露",
                exposure,
                UnitCultivation.MaxExposure,
                _barFillExposure,
                $"{exposure:0}/{UnitCultivation.MaxExposure:0}");

            float actionProgress = actions != null && actions.IsBusy ? actions.Progress : 0f;
            string actionRight = actions == null || !actions.IsBusy
                ? (actions != null && !string.IsNullOrEmpty(actions.CancelReason)
                    ? actions.CancelReason
                    : "无行动")
                : $"{Mathf.RoundToInt(actionProgress * 100f)}%"
                  + (actions.IsMovingToAction ? " 移动中" : " 执行中");
            DrawStatBar(
                x + 14f,
                y + 88f,
                width - 28f,
                "行动",
                actions != null && actions.IsBusy ? Mathf.Max(0.05f, actionProgress) : 0f,
                1f,
                _barFillQi,
                actionRight);

            string targetName = actions != null && !string.IsNullOrEmpty(actions.TargetName)
                ? actions.TargetName
                : (unit.AssignedWorkZone != null ? unit.AssignedWorkZone.DisplayName : "无");
            int grass = resourceInventory == null ? 0 : resourceInventory.GetAmount(ResourceType.ConcealGrass);
            string schedule = scheduleService == null
                ? "-"
                : ActivityShort(scheduleService.GetVillageActivity());
            GUI.Label(
                new Rect(x + 14f, y + 114f, width - 28f, 18f),
                $"村规:{schedule}  行动:{activity}  目标:{targetName}  敛息草:{grass}",
                _tinyStyle);

            DrawUnitActionButtons(x + 14f, y + height - 40f);
        }

        private void DrawMultiUnitPanel(float x, float y, float width, float height)
        {
            GUI.Label(
                new Rect(x + 12f, y + 6f, width - 24f, 24f),
                BuildSelectionStatusLine(),
                _statusStyle);
            GUI.Label(
                new Rect(x + 12f, y + 34f, width - 24f, 18f),
                "多人选中：操作对全部生效；点空白取消选中",
                _tinyStyle);
            DrawUnitActionButtons(x + 12f, y + height - 42f);
        }

        private void DrawUnitActionButtons(float x, float y)
        {
            const float buttonWidth = 78f;
            const float buttonHeight = 32f;
            const float gap = 6f;
            float buttonX = x;

            if (GUI.Button(new Rect(buttonX, y, buttonWidth, buttonHeight), "停止(S)"))
            {
                partyCommands.StopSelectedOrders();
            }

            buttonX += buttonWidth + gap;
            string workLabel = partyCommands != null && partyCommands.IsWorkTargeting
                ? "选工位…"
                : "工作(W)";
            if (GUI.Button(new Rect(buttonX, y, buttonWidth, buttonHeight), workLabel))
            {
                partyCommands.BeginWorkCommand();
            }

            buttonX += buttonWidth + gap;
            string attackLabel = partyCommands != null && partyCommands.IsAttackTargeting
                ? "选目标…"
                : "攻击(A)";
            if (GUI.Button(new Rect(buttonX, y, buttonWidth, buttonHeight), attackLabel))
            {
                partyCommands.BeginAttackCommand();
            }

            buttonX += buttonWidth + gap;
            if (GUI.Button(new Rect(buttonX, y, buttonWidth, buttonHeight), "入定(C)"))
            {
                partyCommands.CancelCommandTargeting();
                if (cultivationSystem != null && cultivationSystem.SpiritSite != null)
                {
                    foreach (DemoUnitController u in ResolveTargetUnits())
                    {
                        if (u == null)
                        {
                            continue;
                        }

                        CharacterActionController actions = u.GetComponent<CharacterActionController>()
                            ?? u.gameObject.AddComponent<CharacterActionController>();
                        actions.IssueCultivate(cultivationSystem.SpiritSite);
                    }
                }
            }

            buttonX += buttonWidth + gap;
            if (GUI.Button(new Rect(buttonX, y, buttonWidth, buttonHeight), "出定(X)"))
            {
                foreach (DemoUnitController u in ResolveTargetUnits())
                {
                    if (u == null)
                    {
                        continue;
                    }

                    CharacterActionController actions = u.GetComponent<CharacterActionController>();
                    if (actions != null && actions.IsActivelyCultivating())
                    {
                        actions.Cancel("玩家出定");
                    }
                }

                cultivationSystem?.StopCultivationForUnits(ResolveTargetUnits());
            }

            buttonX += buttonWidth + gap;
            if (GUI.Button(new Rect(buttonX, y, buttonWidth, buttonHeight), "敛息(G)"))
            {
                cultivationSystem?.UseConcealGrassForUnits(ResolveTargetUnits());
            }

            buttonX += buttonWidth + gap;
            string tip;
            if (partyCommands != null && partyCommands.IsWorkTargeting)
            {
                tip = "黄指针：点工位开工 · 右键/Esc取消";
            }
            else if (partyCommands != null && partyCommands.IsAttackTargeting)
            {
                tip = "红指针：点NPC交战 · 右键/Esc取消";
            }
            else
            {
                tip = "右键工位/灵地下令 · W/A选目标";
            }

            GUI.Label(new Rect(buttonX, y + 6f, 280f, 22f), tip, _tinyStyle);
        }

        private void DrawStatBar(
            float x,
            float y,
            float width,
            string label,
            float value,
            float max,
            Texture2D fill,
            string rightText)
        {
            const float labelW = 42f;
            const float barH = 16f;
            GUI.Label(new Rect(x, y - 2f, labelW, 20f), label, _tinyStyle);

            float barX = x + labelW;
            float barW = width - labelW;
            if (_barBg != null)
            {
                GUI.DrawTexture(new Rect(barX, y, barW, barH), _barBg);
            }

            float ratio = max <= 0f ? 0f : Mathf.Clamp01(value / max);
            if (fill != null && ratio > 0.001f)
            {
                GUI.DrawTexture(new Rect(barX, y, barW * ratio, barH), fill);
            }

            GUI.Label(new Rect(barX + 6f, y - 2f, barW - 12f, 20f), rightText, _tinyStyle);
        }

        private string BuildSelectionStatusLine()
        {
            int count = partyCommands.SelectedUnits.Count;
            if (count == 1)
            {
                DemoUnitController unit = partyCommands.SelectedUnits[0];
                if (unit == null)
                {
                    return "已选中 1 人";
                }

                UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
                return $"已选中 {ShortName(unit.name)} · {DescribeUnitOrder(unit, cultivation)}";
            }

            var parts = new List<string> { $"已选中 {count} 人" };
            for (int i = 0; i < partyCommands.SelectedUnits.Count && i < 3; i++)
            {
                DemoUnitController unit = partyCommands.SelectedUnits[i];
                if (unit == null)
                {
                    continue;
                }

                UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
                parts.Add($"{ShortName(unit.name)}:{DescribeUnitOrder(unit, cultivation)}");
            }

            return string.Join(" | ", parts);
        }

        private static string DescribeUnitOrder(DemoUnitController unit, UnitCultivation cultivation)
        {
            CharacterActionController actions = unit.GetComponent<CharacterActionController>();
            if (actions != null && (actions.IsBusy || !string.IsNullOrEmpty(actions.CancelReason)))
            {
                string line = actions.StatusLabel;
                if (actions.IsBusy)
                {
                    line += $" {Mathf.RoundToInt(actions.Progress * 100f)}%";
                    if (!string.IsNullOrEmpty(actions.TargetName))
                    {
                        line += $" → {actions.TargetName}";
                    }
                }
                else if (!string.IsNullOrEmpty(actions.CancelReason))
                {
                    line += $"（{actions.CancelReason}）";
                }

                return line;
            }

            if (cultivation != null && cultivation.IsCultivating)
            {
                return "修炼中";
            }

            return unit.ActivityState switch
            {
                UnitActivityState.Attacking =>
                    $"交战中({unit.AttackTarget?.name ?? "目标"})",
                UnitActivityState.Working when unit.AssignedWorkSpot != null =>
                    $"工作中({unit.AssignedWorkSpot.SpotName})",
                UnitActivityState.Working when unit.AssignedWorkZone != null =>
                    $"工作中({unit.AssignedWorkZone.DisplayName})",
                UnitActivityState.Moving when unit.IsAttacking =>
                    $"追击({unit.AttackTarget?.name ?? "目标"})",
                UnitActivityState.Moving when unit.IsWorking =>
                    $"前往开工({unit.AssignedWorkSpot?.SpotName ?? unit.AssignedWorkZone?.DisplayName})",
                UnitActivityState.Moving when unit.AssignedWorkSpot != null =>
                    $"前往({unit.AssignedWorkSpot.SpotName})",
                UnitActivityState.Moving => "移动中",
                UnitActivityState.Idle => "空闲",
                _ => ActivityStateLabel(unit.ActivityState)
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

            _barBg = MakeColorTexture(new Color(0.12f, 0.12f, 0.12f, 0.85f));
            _barFillCultivation = MakeColorTexture(new Color(0.35f, 0.75f, 0.95f, 0.95f));
            _barFillExposure = MakeColorTexture(new Color(0.9f, 0.45f, 0.25f, 0.95f));
            _barFillQi = MakeColorTexture(new Color(0.35f, 0.85f, 0.7f, 0.95f));

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.92f, 0.86f, 0.68f) }
            };

            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.92f, 0.82f) }
            };

            _tinyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.85f, 0.85f, 0.8f) }
            };

            _cellStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0)
            };

            _toggleOffStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };

            _toggleOnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
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
