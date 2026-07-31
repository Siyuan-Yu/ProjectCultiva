using UnityEngine;
using XianXia.Unity.Npc;
using XianXia.Unity.Time;

namespace XianXia.Unity.Presentation
{
    /// <summary>
    /// 村民群体状态：不逐人模拟，只按日程显示当前群体状态文字。
    /// </summary>
    public sealed class VillageCrowdPresenter : MonoBehaviour
    {
        [SerializeField] private GameClock clock;
        [SerializeField] private ScheduleService villageSchedule;
        [SerializeField] private NpcScheduleConfig groupSchedule;
        [SerializeField] private Vector3 worldAnchor = new(-14f, 10f, 0f);
        [SerializeField] private string crowdTitle = "村民";

        private static GUIStyle _titleStyle;
        private static GUIStyle _statusStyle;

        public string CurrentGroupStatusLabel
        {
            get
            {
                int hour = clock != null ? clock.Hour : 12;
                if (groupSchedule != null)
                {
                    return groupSchedule.GetDuty(hour) switch
                    {
                        NpcDutyPhase.Work => "工作中",
                        NpcDutyPhase.Patrol => "巡视中",
                        _ => "休息中"
                    };
                }

                if (villageSchedule != null)
                {
                    return villageSchedule.GetVillageActivity(hour) switch
                    {
                        ScheduleActivity.Work => "工作中",
                        _ => "休息中"
                    };
                }

                return hour >= 7 && hour < 18 ? "工作中" : "休息中";
            }
        }

        public void Configure(
            GameClock gameClock,
            ScheduleService schedule,
            NpcScheduleConfig villagerGroupSchedule,
            Vector3 anchor)
        {
            clock = gameClock;
            villageSchedule = schedule;
            groupSchedule = villagerGroupSchedule;
            worldAnchor = anchor;
        }

        private void Update()
        {
            if (clock == null)
            {
                clock = GameClock.Instance;
            }

            if (villageSchedule == null)
            {
                villageSchedule = FindObjectOfType<ScheduleService>();
            }
        }

        private void OnGUI()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector3 screen = cam.WorldToScreenPoint(worldAnchor + Vector3.up * 0.8f);
            if (screen.z < 0f)
            {
                return;
            }

            EnsureStyles();
            float guiX = screen.x - 48f;
            float guiY = Screen.height - screen.y - 28f;
            GUI.Label(new Rect(guiX, guiY, 96f, 18f), crowdTitle, _titleStyle);
            GUI.Label(new Rect(guiX, guiY + 16f, 96f, 18f), CurrentGroupStatusLabel, _statusStyle);
        }

        private static void EnsureStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.78f, 0.55f) }
            };
            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.95f, 0.92f, 0.8f) }
            };
        }
    }
}
