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
        private bool _hasWorkOrder;
        private WorkZone _assignedWorkZone;

        public bool IsSelected { get; private set; }
        public bool HasActiveOrder => _hasDestination || _hasWorkOrder;
        public bool IsScheduleCompliant => _scheduleCompliant;
        public bool RequireWorkPeriod => _requireWork;
        public bool IsInWorkZone => _inWorkZone;
        public float VisualScale => visualScale;
        public WorkZone AssignedWorkZone => _assignedWorkZone;
        public bool HasWorkOrder => _hasWorkOrder;

        public UnitActivityState ActivityState
        {
            get
            {
                if (_hasWorkOrder
                    && !_hasDestination
                    && _assignedWorkZone != null
                    && _assignedWorkZone.Contains(transform.position))
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

            if (_hasWorkOrder && !_hasDestination && _assignedWorkZone != null)
            {
                if (!_assignedWorkZone.Contains(transform.position))
                {
                    SetDestination(_assignedWorkZone.transform.position);
                }
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

        /// <summary>自由移动：取消工作指令。</summary>
        public void MoveTo(Vector2 worldPosition)
        {
            ClearWorkOrder();
            SetDestination(worldPosition);
        }

        /// <summary>指派到工作区持续工作。</summary>
        public void AssignWork(WorkZone zone, Vector2 gatherPoint)
        {
            if (zone == null)
            {
                return;
            }

            _hasWorkOrder = true;
            _assignedWorkZone = zone;
            SetDestination(gatherPoint);
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

        private void ClearWorkOrder()
        {
            _hasWorkOrder = false;
            _assignedWorkZone = null;
        }

        private void SetDestination(Vector2 worldPosition)
        {
            _destination = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
            _hasDestination = true;
        }
    }
}
