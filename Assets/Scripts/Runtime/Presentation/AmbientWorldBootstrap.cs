using UnityEngine;
using XianXia.Unity.Npc;
using XianXia.Unity.Time;
using XianXia.Unity.World;

namespace XianXia.Unity.Presentation
{
    /// <summary>
    /// Milestone 5：按可配置 NPC 日程驱动主管／守卫；村民以群体状态展示。
    /// </summary>
    public sealed class AmbientWorldBootstrap : MonoBehaviour
    {
        [SerializeField] private GameClock clock;
        [SerializeField] private ScheduleService scheduleService;
        [SerializeField] private NpcScheduleConfig guardSchedule;
        [SerializeField] private NpcScheduleConfig supervisorSchedule;
        [SerializeField] private NpcScheduleConfig villagerGroupSchedule;

        public void Configure(
            GameClock gameClock,
            ScheduleService villageSchedule = null,
            NpcScheduleConfig guard = null,
            NpcScheduleConfig supervisor = null,
            NpcScheduleConfig villagerGroup = null)
        {
            clock = gameClock;
            scheduleService = villageSchedule;
            if (guard != null)
            {
                guardSchedule = guard;
            }

            if (supervisor != null)
            {
                supervisorSchedule = supervisor;
            }

            if (villagerGroup != null)
            {
                villagerGroupSchedule = villagerGroup;
            }

            EnsureBehaviors();
        }

        private void Start()
        {
            if (scheduleService == null)
            {
                scheduleService = FindObjectOfType<ScheduleService>();
            }

            EnsureBehaviors();
        }

        private void EnsureBehaviors()
        {
            if (clock == null)
            {
                clock = GameClock.Instance;
            }

            if (scheduleService == null)
            {
                scheduleService = FindObjectOfType<ScheduleService>();
            }

            EnsureDefaultSchedules();

            // 主管：白天巡视，晚上回主管府附近住所
            Vector2 supervisorHome = new(18f, 10f);
            EnsureScheduledRoute(
                "Supervisor",
                supervisorSchedule,
                1.0f,
                supervisorHome,
                new Vector2(18f, 7f),
                new Vector2(14f, 4f),
                new Vector2(22f, 4f),
                new Vector2(18f, 11f),
                new Vector2(12f, 8f));
            AttachPatrolInspectable("Supervisor");

            // 守卫：巡逻点列表 + 路线 + 休息点
            EnsureScheduledRoute(
                "Guard_01",
                guardSchedule,
                1.35f,
                new Vector2(14f, 9f),
                new Vector2(14f, 8f),
                new Vector2(8f, 2f),
                new Vector2(20f, -8f),
                new Vector2(14f, 8f));
            AttachPatrolInspectable("Guard_01");

            EnsureScheduledRoute(
                "Guard_02",
                guardSchedule,
                1.35f,
                new Vector2(22f, 9f),
                new Vector2(22f, 8f),
                new Vector2(28f, 2f),
                new Vector2(24f, -10f),
                new Vector2(22f, 8f));
            AttachPatrolInspectable("Guard_02");

            // 商人仍简单游荡（无发现逻辑）
            EnsurePatrol(
                "Merchant",
                0.9f,
                new Vector2(0f, 4f),
                new Vector2(-4f, 2f),
                new Vector2(4f, 2f),
                new Vector2(0f, 6f));
            AttachPatrolInspectable("Merchant");

            EnsureVillageCrowd();
            // 少量氛围劳工仅作点缀；群体状态由 VillageCrowdPresenter 表达
            EnsureLaborers();
        }

        private void EnsureDefaultSchedules()
        {
            if (guardSchedule == null)
            {
                guardSchedule = NpcScheduleConfig.CreateDefaultGuard();
            }

            if (supervisorSchedule == null)
            {
                supervisorSchedule = NpcScheduleConfig.CreateDefaultSupervisor();
            }

            if (villagerGroupSchedule == null)
            {
                villagerGroupSchedule = NpcScheduleConfig.CreateDefaultVillagerGroup();
            }
        }

        private void EnsureScheduledRoute(
            string objectName,
            NpcScheduleConfig schedule,
            float speed,
            Vector2 home,
            params Vector2[] points)
        {
            GameObject go = GameObject.Find(objectName);
            if (go == null)
            {
                return;
            }

            AmbientNpcActor actor = go.GetComponent<AmbientNpcActor>();
            if (actor == null)
            {
                actor = go.AddComponent<AmbientNpcActor>();
            }

            actor.ConfigureScheduledRoute(clock, schedule, speed, home, points);
        }

        private void EnsurePatrol(string objectName, float speed, params Vector2[] points)
        {
            GameObject go = GameObject.Find(objectName);
            if (go == null)
            {
                return;
            }

            AmbientNpcActor actor = go.GetComponent<AmbientNpcActor>();
            if (actor == null)
            {
                actor = go.AddComponent<AmbientNpcActor>();
            }

            actor.ConfigurePatrol(clock, speed, points);
        }

        private void EnsureVillageCrowd()
        {
            VillageCrowdPresenter presenter = GetComponent<VillageCrowdPresenter>();
            if (presenter == null)
            {
                presenter = gameObject.AddComponent<VillageCrowdPresenter>();
            }

            Vector3 anchor = new(-14f, 10f, 0f);
            GameObject house = GameObject.Find("House_01");
            if (house != null)
            {
                anchor = house.transform.position + new Vector3(0f, 1.2f, 0f);
            }

            presenter.Configure(clock, scheduleService, villagerGroupSchedule, anchor);
        }

        private void EnsureLaborers()
        {
            if (GameObject.Find("Laborer_01") != null)
            {
                return;
            }

            GameObject template = GameObject.Find("Merchant");
            Transform parent = template != null && template.transform.parent != null
                ? template.transform.parent
                : transform;

            WorkZone farm = FindZoneByName("WorkZone_Farm");
            WorkZone forest = FindZoneByName("WorkZone_Forest");
            Vector2 meal = new(12f, 5f);

            CreateLaborer(
                "Laborer_01",
                parent,
                template,
                new Vector2(-18f, 9f),
                farm != null ? farm.transform.position : new Vector2(20f, -12f),
                meal,
                1.1f);
            CreateLaborer(
                "Laborer_02",
                parent,
                template,
                new Vector2(-12f, 11f),
                forest != null ? forest.transform.position : new Vector2(-34f, 0f),
                meal,
                1.05f);
        }

        private void CreateLaborer(
            string name,
            Transform parent,
            GameObject template,
            Vector2 home,
            Vector2 work,
            Vector2 meal,
            float speed)
        {
            GameObject go;
            if (template != null)
            {
                go = Instantiate(template, parent);
                go.name = name;
            }
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
                GameObject visual = new("Visual");
                visual.transform.SetParent(go.transform, false);
                SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
                renderer.color = new Color(0.72f, 0.62f, 0.45f, 1f);
            }

            go.transform.position = new Vector3(home.x, home.y, 0f);
            AmbientNpcActor actor = go.GetComponent<AmbientNpcActor>();
            if (actor == null)
            {
                actor = go.AddComponent<AmbientNpcActor>();
            }

            actor.ConfigureScheduleLabor(clock, scheduleService, speed, home, work, meal);
            AttachLaborerInspectable(go, name);
        }

        private static void AttachLaborerInspectable(GameObject go, string objectName)
        {
            WorldCharacterInspectable inspectable = go.GetComponent<WorldCharacterInspectable>();
            if (inspectable == null)
            {
                inspectable = go.AddComponent<WorldCharacterInspectable>();
            }

            string display = objectName switch
            {
                "Laborer_01" => "村民甲",
                "Laborer_02" => "村民乙",
                _ => "村民"
            };
            inspectable.Configure(display, "村民", "凡人", "氛围点缀；群体状态见住宅旁标签", 0f);
            EnsureThreatMarker(go);
        }

        private static void AttachPatrolInspectable(string objectName)
        {
            GameObject go = GameObject.Find(objectName);
            if (go == null)
            {
                return;
            }

            WorldCharacterInspectable inspectable = go.GetComponent<WorldCharacterInspectable>();
            if (inspectable == null)
            {
                inspectable = go.AddComponent<WorldCharacterInspectable>();
            }

            switch (objectName)
            {
                case "Supervisor":
                    inspectable.Configure("主管", "村主管", "筑基", "白天巡视，晚上回住所（日程驱动）", 0.95f);
                    break;
                case "Guard_01":
                    inspectable.Configure("守卫甲", "守卫", "炼气", "按日程巡逻／休息（无发现逻辑）", 0.55f);
                    break;
                case "Guard_02":
                    inspectable.Configure("守卫乙", "守卫", "炼气", "按日程巡逻／休息（无发现逻辑）", 0.55f);
                    break;
                case "Merchant":
                    inspectable.Configure("行商", "商人", "凡人", "在村中走动交易（占位）", 0f);
                    break;
            }

            EnsureThreatMarker(go);
        }

        private static void EnsureThreatMarker(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            WorldCharacterInspectable inspectable = go.GetComponent<WorldCharacterInspectable>();
            if (inspectable == null || inspectable.ThreatLevel < 0.2f)
            {
                return;
            }

            ThreatOverheadMarker marker = go.GetComponent<ThreatOverheadMarker>();
            if (marker == null)
            {
                marker = go.AddComponent<ThreatOverheadMarker>();
            }

            marker.RefreshColor();
        }

        private static WorkZone FindZoneByName(string objectName)
        {
            GameObject go = GameObject.Find(objectName);
            return go != null ? go.GetComponent<WorkZone>() : null;
        }
    }
}
