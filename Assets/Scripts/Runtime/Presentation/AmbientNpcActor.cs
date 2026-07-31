using UnityEngine;
using XianXia.Unity.Npc;
using XianXia.Unity.Time;

namespace XianXia.Unity.Presentation
{
    /// <summary>
    /// 不可控氛围 NPC：按可配置日程在巡逻／休息（或劳工点）之间切换。
    /// 不做发现、追捕、潜行判定。
    /// </summary>
    public sealed class AmbientNpcActor : MonoBehaviour
    {
        public enum BehaviorKind
        {
            Patrol = 0,
            ScheduleLabor = 1,
            ScheduledRoute = 2
        }

        [SerializeField] private BehaviorKind behavior = BehaviorKind.Patrol;
        [SerializeField] private float moveSpeed = 1.2f;
        [SerializeField] private Vector2[] waypoints = System.Array.Empty<Vector2>();
        [SerializeField] private Vector2 homePoint;
        [SerializeField] private Vector2 workPoint;
        [SerializeField] private Vector2 mealPoint;
        [SerializeField] private float arriveDistance = 0.25f;
        [SerializeField] private float waitSecondsAtWaypoint = 1.2f;
        [SerializeField] private NpcScheduleConfig scheduleConfig;
        [SerializeField] private bool showOverheadStatus = true;

        private GameClock _clock;
        private ScheduleService _scheduleService;
        private int _waypointIndex;
        private int _waypointDirection = 1;
        private Vector3 _destination;
        private bool _hasDestination;
        private float _waitRemaining;
        private SpriteRenderer _visual;
        private ScheduleActivity _lastActivity = (ScheduleActivity)(-1);
        private NpcDutyPhase _lastDuty = (NpcDutyPhase)(-1);
        private NpcRuntimeState _runtimeState = NpcRuntimeState.Rest;
        private static GUIStyle _overheadStyle;

        public NpcRuntimeState RuntimeState => _runtimeState;
        public NpcScheduleConfig Schedule => scheduleConfig;
        public float MoveSpeed => moveSpeed;
        public Vector2[] PatrolWaypoints => waypoints;
        public Vector2 HomePoint => homePoint;

        public string CurrentActivityLabel
        {
            get
            {
                if (behavior == BehaviorKind.ScheduledRoute)
                {
                    return _runtimeState == NpcRuntimeState.Patrol ? "巡视中" : "休息中";
                }

                if (behavior == BehaviorKind.Patrol)
                {
                    return _hasDestination ? "巡视中" : "休息中";
                }

                ScheduleActivity activity = ResolveVillageActivity();
                return activity switch
                {
                    ScheduleActivity.Work => "工作中",
                    ScheduleActivity.Meal => "休息中",
                    ScheduleActivity.Sleep => "休息中",
                    ScheduleActivity.WakePrepare => "休息中",
                    _ => "休息中"
                };
            }
        }

        public void ConfigurePatrol(GameClock clock, float speed, params Vector2[] points)
        {
            behavior = BehaviorKind.Patrol;
            _clock = clock;
            moveSpeed = speed;
            waypoints = points ?? System.Array.Empty<Vector2>();
            scheduleConfig = null;
            _runtimeState = NpcRuntimeState.Patrol;
            _waypointIndex = 0;
            _waypointDirection = 1;
            if (waypoints.Length > 0)
            {
                SetDestination(waypoints[0]);
            }
        }

        /// <summary>守卫／主管：日程驱动的巡逻点路线 + 休息点。</summary>
        public void ConfigureScheduledRoute(
            GameClock clock,
            NpcScheduleConfig schedule,
            float speed,
            Vector2 home,
            params Vector2[] patrolPoints)
        {
            behavior = BehaviorKind.ScheduledRoute;
            _clock = clock;
            scheduleConfig = schedule;
            moveSpeed = Mathf.Max(0.1f, speed);
            homePoint = home;
            waypoints = patrolPoints ?? System.Array.Empty<Vector2>();
            _waypointIndex = 0;
            _waypointDirection = 1;
            _lastDuty = (NpcDutyPhase)(-1);
            ApplyDuty(ResolveDuty());
        }

        public void ConfigureScheduleLabor(
            GameClock clock,
            ScheduleService schedule,
            float speed,
            Vector2 home,
            Vector2 work,
            Vector2 meal)
        {
            behavior = BehaviorKind.ScheduleLabor;
            _clock = clock;
            _scheduleService = schedule;
            moveSpeed = speed;
            homePoint = home;
            workPoint = work;
            mealPoint = meal;
            _lastActivity = (ScheduleActivity)(-1);
            _runtimeState = NpcRuntimeState.Rest;
            SetDestination(home);
        }

        private void Awake()
        {
            _visual = GetComponentInChildren<SpriteRenderer>();
            if (_clock == null)
            {
                _clock = GameClock.Instance;
            }

            if (_scheduleService == null)
            {
                _scheduleService = FindObjectOfType<ScheduleService>();
            }
        }

        private void Update()
        {
            if (_clock == null)
            {
                _clock = GameClock.Instance;
            }

            if (_scheduleService == null && behavior == BehaviorKind.ScheduleLabor)
            {
                _scheduleService = FindObjectOfType<ScheduleService>();
            }

            float delta = _clock != null ? _clock.ScaledDeltaTime : UnityEngine.Time.deltaTime;
            if (delta <= 0f)
            {
                return;
            }

            if (behavior == BehaviorKind.ScheduleLabor)
            {
                RefreshScheduleDestination();
            }
            else if (behavior == BehaviorKind.ScheduledRoute)
            {
                RefreshScheduledRoute();
            }

            if (_waitRemaining > 0f)
            {
                _waitRemaining -= delta;
                if (_waitRemaining <= 0f && behavior == BehaviorKind.ScheduledRoute
                    && _runtimeState == NpcRuntimeState.Patrol
                    && !_hasDestination)
                {
                    AdvancePatrolWaypoint();
                }

                return;
            }

            if (!_hasDestination)
            {
                return;
            }

            transform.position = Vector3.MoveTowards(transform.position, _destination, moveSpeed * delta);
            if ((transform.position - _destination).sqrMagnitude <= arriveDistance * arriveDistance)
            {
                transform.position = _destination;
                _hasDestination = false;
                _waitRemaining = waitSecondsAtWaypoint;
                if (behavior == BehaviorKind.Patrol
                    || (behavior == BehaviorKind.ScheduledRoute && _runtimeState == NpcRuntimeState.Patrol))
                {
                    AdvancePatrolWaypoint();
                }
            }

            if (_visual != null)
            {
                _visual.sortingOrder = Mathf.RoundToInt(-transform.position.y * 100f);
            }
        }

        private void OnGUI()
        {
            if (!showOverheadStatus)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.05f);
            if (screen.z < 0f)
            {
                return;
            }

            EnsureOverheadStyle();
            float guiX = screen.x - 36f;
            float guiY = Screen.height - screen.y - 18f;
            GUI.Label(new Rect(guiX, guiY, 72f, 18f), CurrentActivityLabel, _overheadStyle);
        }

        private void RefreshScheduledRoute()
        {
            NpcDutyPhase duty = ResolveDuty();
            if (duty == _lastDuty)
            {
                return;
            }

            ApplyDuty(duty);
        }

        private void ApplyDuty(NpcDutyPhase duty)
        {
            _lastDuty = duty;
            if (duty == NpcDutyPhase.Patrol)
            {
                _runtimeState = NpcRuntimeState.Patrol;
                if (waypoints != null && waypoints.Length > 0)
                {
                    _waypointIndex = Mathf.Clamp(_waypointIndex, 0, waypoints.Length - 1);
                    SetDestination(waypoints[_waypointIndex]);
                }
                else
                {
                    SetDestination(homePoint);
                }

                _waitRemaining = 0f;
            }
            else
            {
                _runtimeState = NpcRuntimeState.Rest;
                SetDestination(homePoint);
                _waitRemaining = 0f;
            }
        }

        private NpcDutyPhase ResolveDuty()
        {
            int hour = _clock != null ? _clock.Hour : 12;
            if (scheduleConfig != null)
            {
                return scheduleConfig.GetDuty(hour);
            }

            // 无配置时：白天巡逻，夜晚休息
            return hour >= 7 && hour <= 18 ? NpcDutyPhase.Patrol : NpcDutyPhase.Rest;
        }

        private ScheduleActivity ResolveVillageActivity()
        {
            if (_scheduleService != null)
            {
                return _scheduleService.GetVillageActivity(_clock != null ? _clock.Hour : 12);
            }

            int hour = _clock != null ? _clock.Hour : 12;
            if (hour >= 7 && hour < 12 || hour >= 13 && hour < 18)
            {
                return ScheduleActivity.Work;
            }

            if (hour == 12 || hour == 18)
            {
                return ScheduleActivity.Meal;
            }

            if (hour >= 23 || hour < 6)
            {
                return ScheduleActivity.Sleep;
            }

            if (hour == 6)
            {
                return ScheduleActivity.WakePrepare;
            }

            return ScheduleActivity.Free;
        }

        private void RefreshScheduleDestination()
        {
            ScheduleActivity activity = ResolveVillageActivity();
            if (activity == _lastActivity)
            {
                return;
            }

            _lastActivity = activity;
            _runtimeState = activity == ScheduleActivity.Work
                ? NpcRuntimeState.Patrol
                : NpcRuntimeState.Rest;
            Vector2 target = activity switch
            {
                ScheduleActivity.Work => workPoint,
                ScheduleActivity.Meal => mealPoint,
                ScheduleActivity.Sleep => homePoint,
                ScheduleActivity.WakePrepare => homePoint,
                _ => homePoint + new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(-1.2f, 1.2f))
            };

            SetDestination(target);
            _waitRemaining = 0f;
        }

        private void AdvancePatrolWaypoint()
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                return;
            }

            if (waypoints.Length == 1)
            {
                SetDestination(waypoints[0]);
                return;
            }

            int next = _waypointIndex + _waypointDirection;
            if (next < 0 || next >= waypoints.Length)
            {
                _waypointDirection *= -1;
                next = _waypointIndex + _waypointDirection;
            }

            _waypointIndex = Mathf.Clamp(next, 0, waypoints.Length - 1);
            SetDestination(waypoints[_waypointIndex]);
        }

        private void SetDestination(Vector2 world)
        {
            _destination = new Vector3(world.x, world.y, transform.position.z);
            _hasDestination = true;
        }

        private static void EnsureOverheadStyle()
        {
            if (_overheadStyle != null)
            {
                return;
            }

            _overheadStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.92f, 0.9f, 0.78f) }
            };
        }
    }
}
