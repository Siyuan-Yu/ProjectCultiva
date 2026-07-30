using UnityEngine;
using XianXia.Unity.Time;

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

        public bool IsSelected { get; private set; }
        public bool HasActiveOrder => _hasDestination;
        public bool IsScheduleCompliant => _scheduleCompliant;
        public bool RequireWorkPeriod => _requireWork;
        public bool IsInWorkZone => _inWorkZone;
        public float VisualScale => visualScale;

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

        public void MoveTo(Vector2 worldPosition)
        {
            // 玩家命令优先于时间表：只记录与执行命令，时间表不打断。
            _destination = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
            _hasDestination = true;
        }

        public void CancelOrder()
        {
            _hasDestination = false;
            _destination = transform.position;
        }

        public void SetScheduleCompliance(bool compliant, bool requireWork, bool inWorkZone)
        {
            _scheduleCompliant = compliant;
            _requireWork = requireWork;
            _inWorkZone = inWorkZone;
        }
    }
}
