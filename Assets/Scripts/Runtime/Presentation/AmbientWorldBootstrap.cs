using UnityEngine;
using XianXia.Unity.Time;
using XianXia.Unity.World;

namespace XianXia.Unity.Presentation
{
    /// <summary>
    /// 兼容未重建场景：给主管／守卫挂巡逻，并在缺劳工时用现有 Merchant 素材生成氛围劳工。
    /// </summary>
    public sealed class AmbientWorldBootstrap : MonoBehaviour
    {
        [SerializeField] private GameClock clock;
        [SerializeField] private ScheduleService scheduleService;

        public void Configure(GameClock gameClock, ScheduleService villageSchedule = null)
        {
            clock = gameClock;
            scheduleService = villageSchedule;
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

            EnsurePatrol(
                "Supervisor",
                1.0f,
                new Vector2(18f, 7f),
                new Vector2(14f, 4f),
                new Vector2(22f, 4f),
                new Vector2(18f, 11f),
                new Vector2(12f, 8f));
            AttachPatrolInspectable("Supervisor");

            EnsurePatrol(
                "Guard_01",
                1.35f,
                new Vector2(14f, 8f),
                new Vector2(8f, 2f),
                new Vector2(20f, -8f),
                new Vector2(14f, 8f));
            AttachPatrolInspectable("Guard_01");

            EnsurePatrol(
                "Guard_02",
                1.35f,
                new Vector2(22f, 8f),
                new Vector2(28f, 2f),
                new Vector2(24f, -10f),
                new Vector2(22f, 8f));
            AttachPatrolInspectable("Guard_02");

            EnsurePatrol(
                "Merchant",
                0.9f,
                new Vector2(0f, 4f),
                new Vector2(-4f, 2f),
                new Vector2(4f, 2f),
                new Vector2(0f, 6f));
            AttachPatrolInspectable("Merchant");

            EnsureLaborers();
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
            Vector2 meal = new(12f, 5f); // 仓库附近当食堂占位

            CreateLaborer("Laborer_01", parent, template, new Vector2(-18f, 9f), farm != null ? farm.transform.position : new Vector2(20f, -12f), meal, 1.1f);
            CreateLaborer("Laborer_02", parent, template, new Vector2(-12f, 11f), forest != null ? forest.transform.position : new Vector2(-34f, 0f), meal, 1.05f);
            CreateLaborer("Laborer_03", parent, template, new Vector2(-8f, 7f), farm != null ? (Vector2)farm.transform.position + new Vector2(-3f, 2f) : new Vector2(18f, -10f), meal, 1.15f);
            CreateLaborer("Laborer_04", parent, template, new Vector2(-16f, 5f), forest != null ? (Vector2)forest.transform.position + new Vector2(2f, -4f) : new Vector2(-30f, -4f), meal, 1.0f);
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
                "Laborer_03" => "村民丙",
                "Laborer_04" => "村民丁",
                _ => "村民"
            };
            inspectable.Configure(display, "村民", "凡人", "按课表去工作区／吃饭／睡觉", 0f);
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
                    inspectable.Configure("主管", "村主管", "筑基", "管辖配额、愤怒与最终夺权目标", 0.95f);
                    break;
                case "Guard_01":
                    inspectable.Configure("守卫甲", "守卫", "炼气", "巡视工作区与主管府周边", 0.55f);
                    break;
                case "Guard_02":
                    inspectable.Configure("守卫乙", "守卫", "炼气", "巡视工作区与主管府周边", 0.55f);
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
