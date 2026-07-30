using UnityEngine;
using XianXia.Unity.Presentation;
using XianXia.Unity.Time;

namespace XianXia.Unity.UI
{
    public sealed class DemoPrototypeHud : MonoBehaviour
    {
        [SerializeField] private GameClock clock;
        [SerializeField] private ScheduleService scheduleService;
        [SerializeField] private ScheduleComplianceTracker complianceTracker;
        [SerializeField] private DemoUnitController[] partyUnits;

        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _warnStyle;

        public void Configure(
            GameClock gameClock,
            ScheduleService schedule,
            ScheduleComplianceTracker tracker,
            DemoUnitController[] units)
        {
            clock = gameClock;
            scheduleService = schedule;
            complianceTracker = tracker;
            partyUnits = units;
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
        }

        private void OnGUI()
        {
            EnsureStyles();

            GUI.Box(new Rect(16f, 16f, 420f, 168f), GUIContent.none);
            GUI.Label(new Rect(30f, 24f, 390f, 28f), "Demo v0.1 灰盒 · 时间表验证", _titleStyle);
            GUI.Label(
                new Rect(30f, 56f, 390f, 110f),
                "左键选择 / Shift 多选 / 右键移动\n滚轮缩放镜头\n空格暂停  1=1x  2=2x  5=5x\n时间表只可查看，玩家命令优先",
                _bodyStyle);

            DrawClockPanel();
            DrawSchedulePanel();
            DrawCompliancePanel();
            DrawSpeedButtons();
        }

        private void DrawClockPanel()
        {
            string scaleText = clock == null
                ? "--"
                : clock.IsPaused ? "暂停" : $"{clock.TimeScale:0}x";
            string dayText = clock == null ? "Day ?" : $"Day {clock.DayNumber}";
            string timeText = clock == null ? "--:--" : clock.FormattedClock;
            float realMinutes = clock == null ? 8f : clock.RealMinutesPerGameDay;

            GUI.Box(new Rect(16f, 196f, 420f, 78f), GUIContent.none);
            GUI.Label(new Rect(30f, 204f, 390f, 24f), $"时间  {dayText}  {timeText}  [{scaleText}]", _titleStyle);
            GUI.Label(
                new Rect(30f, 236f, 390f, 24f),
                $"现实日长：{realMinutes:0.#} 分钟／游戏日（可配 5～10）",
                _bodyStyle);
        }

        private void DrawSchedulePanel()
        {
            ScheduleSegment current = scheduleService != null ? scheduleService.CurrentSegment : null;
            ScheduleSegment next = scheduleService != null ? scheduleService.NextSegment : null;
            float remain = scheduleService != null ? scheduleService.MinutesUntilNextSegment : 0f;
            int remainH = Mathf.FloorToInt(remain / 60f);
            int remainM = Mathf.FloorToInt(remain) % 60;

            GUI.Box(new Rect(16f, 286f, 420f, 140f), GUIContent.none);
            GUI.Label(new Rect(30f, 294f, 390f, 24f), "时间表（只读）", _titleStyle);
            GUI.Label(
                new Rect(30f, 324f, 390f, 90f),
                $"当前：{(current == null ? "-" : $"{current.DisplayName}（{current.FormatRange()}）")}\n" +
                $"活动：{(current == null ? "-" : ActivityLabel(current.Activity))}\n" +
                $"下一段：{(next == null ? "-" : $"{next.DisplayName}（{next.FormatRange()}）")}\n" +
                $"距切换：{remainH:00}:{remainM:00}",
                _bodyStyle);
        }

        private void DrawCompliancePanel()
        {
            GUI.Box(new Rect(16f, 438f, 420f, 118f), GUIContent.none);
            GUI.Label(new Rect(30f, 446f, 390f, 24f), "时间表遵守（调试）", _titleStyle);

            if (partyUnits == null || partyUnits.Length == 0)
            {
                GUI.Label(new Rect(30f, 478f, 390f, 24f), "无追踪角色", _bodyStyle);
                return;
            }

            float y = 476f;
            foreach (DemoUnitController unit in partyUnits)
            {
                if (unit == null)
                {
                    continue;
                }

                string order = unit.HasActiveOrder ? "有玩家命令" : "待机";
                string status;
                GUIStyle style = _bodyStyle;
                if (!unit.RequireWorkPeriod)
                {
                    status = "非工作时段";
                }
                else if (unit.IsScheduleCompliant)
                {
                    status = "遵守（在工作区）";
                }
                else
                {
                    status = "违反时间表（离开工作区）";
                    style = _warnStyle;
                }

                GUI.Label(new Rect(30f, y, 390f, 22f), $"{unit.name}: {status} | {order}", style);
                y += 22f;
            }
        }

        private void DrawSpeedButtons()
        {
            if (clock == null)
            {
                return;
            }

            float x = 450f;
            float y = 196f;
            if (GUI.Button(new Rect(x, y, 72f, 28f), clock.IsPaused ? "继续" : "暂停"))
            {
                clock.TogglePause();
            }

            if (GUI.Button(new Rect(x + 80f, y, 48f, 28f), "1x"))
            {
                clock.SetTimeScale(1f);
            }

            if (GUI.Button(new Rect(x + 136f, y, 48f, 28f), "2x"))
            {
                clock.SetTimeScale(2f);
            }

            if (GUI.Button(new Rect(x + 192f, y, 48f, 28f), "5x"))
            {
                clock.SetTimeScale(5f);
            }
        }

        private static string ActivityLabel(ScheduleActivity activity)
        {
            return activity switch
            {
                ScheduleActivity.WakePrepare => "起床/准备",
                ScheduleActivity.Work => "工作",
                ScheduleActivity.Meal => "吃饭",
                ScheduleActivity.Free => "自由时间",
                ScheduleActivity.Sleep => "睡觉",
                _ => activity.ToString()
            };
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.92f, 0.86f, 0.68f) }
            };

            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };

            _warnStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(1f, 0.55f, 0.45f) }
            };
        }
    }
}
