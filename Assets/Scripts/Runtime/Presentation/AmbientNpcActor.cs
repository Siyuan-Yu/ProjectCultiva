using UnityEngine;
using XianXia.Unity.Time;

namespace XianXia.Unity.Presentation
{
    /// <summary>
    /// 不可控氛围 NPC：巡逻，或按全村劳役表在住宅／工作区之间走动。
    /// </summary>
    public sealed class AmbientNpcActor : MonoBehaviour
    {
        public enum BehaviorKind
        {
            Patrol = 0,
            ScheduleLabor = 1
        }

        [SerializeField] private BehaviorKind behavior = BehaviorKind.Patrol;
        [SerializeField] private float moveSpeed = 1.2f;
        [SerializeField] private Vector2[] waypoints = System.Array.Empty<Vector2>();
        [SerializeField] private Vector2 homePoint;
        [SerializeField] private Vector2 workPoint;
        [SerializeField] private Vector2 mealPoint;
        [SerializeField] private float arriveDistance = 0.25f;
        [SerializeField] private float waitSecondsAtWaypoint = 1.2f;

        private GameClock _clock;
        private ScheduleService _scheduleService;
        private int _waypointIndex;
        private int _waypointDirection = 1;
        private Vector3 _destination;
        private bool _hasDestination;
        private float _waitRemaining;
        private SpriteRenderer _visual;
        private ScheduleActivity _lastActivity = (ScheduleActivity)(-1);

        public void ConfigurePatrol(GameClock clock, float speed, params Vector2[] points)
        {
            behavior = BehaviorKind.Patrol;
            _clock = clock;
            moveSpeed = speed;
            waypoints = points ?? System.Array.Empty<Vector2>();
            _waypointIndex = 0;
            _waypointDirection = 1;
            if (waypoints.Length > 0)
            {
                SetDestination(waypoints[0]);
            }
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
            SetDestination(home);
        }

        public string CurrentActivityLabel
        {
            get
            {
                if (behavior == BehaviorKind.Patrol)
                {
                    return _hasDestination ? "巡逻中" : "待命";
                }

                ScheduleActivity activity = ResolveVillageActivity();
                return activity switch
                {
                    ScheduleActivity.Work => "工作中",
                    ScheduleActivity.Meal => "吃饭",
                    ScheduleActivity.Sleep => "睡觉",
                    ScheduleActivity.WakePrepare => "起床",
                    _ => "空闲"
                };
            }
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

            if (_waitRemaining > 0f)
            {
                _waitRemaining -= delta;
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
                if (behavior == BehaviorKind.Patrol)
                {
                    AdvancePatrolWaypoint();
                }
            }

            if (_visual != null)
            {
                _visual.sortingOrder = Mathf.RoundToInt(-transform.position.y * 100f);
            }
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
    }
}
