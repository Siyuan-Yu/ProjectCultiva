using UnityEngine;
using XianXia.Unity.Time;
using XianXia.Unity.World;

namespace XianXia.Unity.Presentation
{
    [DisallowMultipleComponent]
    public sealed class DemoUnitController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 1.5f;
        [SerializeField] private float visualScale = 0.6f;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private SpriteRenderer selectionRing;
        [SerializeField] private BoxCollider2D bodyCollider;

        private Vector3 _destination;
        private bool _hasDestination;
        private bool _scheduleCompliant = true;
        private bool _requireWork;
        private bool _inWorkZone;
        private bool _isWorking;
        private WorkZone _assignedWorkZone;
        private WorkSpot _assignedWorkSpot;

        public bool IsSelected { get; private set; }
        public bool HasActiveOrder => _hasDestination || _isWorking;
        public bool IsScheduleCompliant => _scheduleCompliant;
        public bool RequireWorkPeriod => _requireWork;
        public bool IsInWorkZone => _inWorkZone;
        public float VisualScale => visualScale;
        public WorkZone AssignedWorkZone => _assignedWorkZone;
        public WorkSpot AssignedWorkSpot => _assignedWorkSpot;
        public bool HasWorkOrder => _isWorking;
        public bool IsWorking => _isWorking;

        public UnitActivityState ActivityState
        {
            get
            {
                if (_isWorking && IsAtAssignedWorkPosition())
                {
                    return UnitActivityState.Working;
                }

                if (_hasDestination)
                {
                    return UnitActivityState.Moving;
                }

                return UnitActivityState.Idle;
            }
        }

        public bool IsActivelyWorking => ActivityState == UnitActivityState.Working;

        private void Awake()
        {
            _destination = transform.position;
            ApplyVisualScale();
            SetSelected(false);
        }

        private void Update()
        {
            float delta = GameClock.Instance != null
                ? GameClock.Instance.ScaledDeltaTime
                : UnityEngine.Time.deltaTime;

            if (_hasDestination && delta > 0f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    _destination,
                    moveSpeed * delta);

                if ((transform.position - _destination).sqrMagnitude < 0.001f)
                {
                    transform.position = _destination;
                    _hasDestination = false;
                }
            }

            // 已开工但离开工位：取消工作，不自动跑回去。
            if (_isWorking && !_hasDestination && !IsAtAssignedWorkPosition())
            {
                _isWorking = false;
            }

            if (visual != null)
            {
                visual.sortingOrder = Mathf.RoundToInt(-transform.position.y * 100f);
            }
        }

        public void Configure(
            Transform visualTransform,
            SpriteRenderer visualRenderer,
            SpriteRenderer ringRenderer,
            BoxCollider2D collider,
            float scale,
            float speed)
        {
            visualRoot = visualTransform;
            visual = visualRenderer;
            selectionRing = ringRenderer;
            bodyCollider = collider;
            visualScale = scale;
            moveSpeed = speed;
            ApplyVisualScale();
            SetSelected(false);
        }

        public void ApplyVisualScale()
        {
            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.one * visualScale;
            }
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (selectionRing != null)
            {
                selectionRing.enabled = selected;
            }
        }

        /// <summary>自由移动：取消工作。</summary>
        public void MoveTo(Vector2 worldPosition)
        {
            ClearWorkOrder();
            SetDestination(worldPosition);
        }

        /// <summary>前往工位，但不自动开始工作。</summary>
        public void MoveToWorkSpot(WorkSpot spot)
        {
            if (spot == null)
            {
                return;
            }

            ClearWorkOrder();
            _assignedWorkSpot = spot;
            _assignedWorkZone = spot.OwnerZone;
            SetDestination(spot.Position);
        }

        /// <summary>显式开始在指定工位工作（可先走过去，到达后进入 Working）。</summary>
        public void StartWorkAt(WorkSpot spot)
        {
            if (spot == null)
            {
                return;
            }

            _assignedWorkSpot = spot;
            _assignedWorkZone = spot.OwnerZone;
            _isWorking = true;
            if (!spot.IsInRange(transform.position))
            {
                SetDestination(spot.Position);
            }
            else
            {
                _hasDestination = false;
                _destination = transform.position;
            }
        }

        /// <summary>若已在工位旁，直接开工；否则返回 false。</summary>
        public bool TryStartWorkHere()
        {
            if (_assignedWorkSpot != null && _assignedWorkSpot.IsInRange(transform.position))
            {
                _isWorking = true;
                _hasDestination = false;
                _assignedWorkZone = _assignedWorkSpot.OwnerZone;
                return true;
            }

            return false;
        }

        public void CancelOrder()
        {
            ClearWorkOrder();
            _hasDestination = false;
            _destination = transform.position;
        }

        public void SetScheduleCompliance(bool compliant, bool requireWork, bool inWorkZone)
        {
            _scheduleCompliant = compliant;
            _requireWork = requireWork;
            _inWorkZone = inWorkZone;
        }

        private bool IsAtAssignedWorkPosition()
        {
            if (_assignedWorkSpot != null)
            {
                return _assignedWorkSpot.IsInRange(transform.position);
            }

            return _assignedWorkZone != null && _assignedWorkZone.Contains(transform.position);
        }

        private void ClearWorkOrder()
        {
            _isWorking = false;
            _assignedWorkZone = null;
            _assignedWorkSpot = null;
        }

        private void SetDestination(Vector2 worldPosition)
        {
            _destination = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
            _hasDestination = true;
        }
    }
}
