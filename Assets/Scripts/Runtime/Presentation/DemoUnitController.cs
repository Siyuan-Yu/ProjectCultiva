using UnityEngine;
using XianXia.Unity.Actions;
using XianXia.Unity.Time;
using XianXia.Unity.World;

namespace XianXia.Unity.Presentation
{
    [DisallowMultipleComponent]
    public sealed class DemoUnitController : MonoBehaviour
    {
        private const float AttackEngageRadius = 1.15f;

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
        private bool _isAttacking;
        private WorkZone _assignedWorkZone;
        private WorkSpot _assignedWorkSpot;
        private Transform _attackTarget;
        private float _attackPulse;

        public bool IsSelected { get; private set; }
        public bool HasActiveOrder => _hasDestination || _isWorking || _isAttacking;
        public bool IsScheduleCompliant => _scheduleCompliant;
        public bool RequireWorkPeriod => _requireWork;
        public bool IsInWorkZone => _inWorkZone;
        public float VisualScale => visualScale;
        public WorkZone AssignedWorkZone => _assignedWorkZone;
        public WorkSpot AssignedWorkSpot => _assignedWorkSpot;
        public bool HasWorkOrder => _isWorking;
        public bool IsWorking => _isWorking;
        public bool IsAttacking => _isAttacking && _attackTarget != null;
        public Transform AttackTarget => _attackTarget;
        public Vector3 CurrentDestination => _destination;
        public bool HasDestination => _hasDestination;

        public UnitActivityState ActivityState
        {
            get
            {
                if (_isAttacking && _attackTarget != null && IsInAttackRange())
                {
                    return UnitActivityState.Attacking;
                }

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
            if (GetComponent<UnitActivityOverhead>() == null)
            {
                gameObject.AddComponent<UnitActivityOverhead>();
            }

            if (GetComponent<UnitOrderPathPreview>() == null)
            {
                gameObject.AddComponent<UnitOrderPathPreview>();
            }

            if (GetComponent<CharacterActionController>() == null)
            {
                gameObject.AddComponent<CharacterActionController>();
            }
        }

        private void Update()
        {
            float delta = GameClock.Instance != null
                ? GameClock.Instance.ScaledDeltaTime
                : UnityEngine.Time.deltaTime;

            UpdateAttackChase(delta);

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

            // 已开工但离开工位（且没有正在赶往工位）：取消工作。
            if (_isWorking && !_hasDestination && !IsAtAssignedWorkPosition())
            {
                ClearWorkOrder();
            }

            // 交战中离开目标：停战。
            if (_isAttacking && _attackTarget != null && !_hasDestination && !IsInAttackRange())
            {
                ClearAttackOrder();
            }

            if (_isAttacking && IsInAttackRange())
            {
                _attackPulse += delta;
                if (visual != null)
                {
                    float flash = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(_attackPulse * 8f));
                    visual.color = new Color(1f, flash, flash, 1f);
                }
            }
            else if (visual != null && visual.color != Color.white)
            {
                visual.color = Color.white;
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
            if (GetComponent<UnitActivityOverhead>() == null)
            {
                gameObject.AddComponent<UnitActivityOverhead>();
            }

            if (GetComponent<UnitOrderPathPreview>() == null)
            {
                gameObject.AddComponent<UnitOrderPathPreview>();
            }

            if (GetComponent<CharacterActionController>() == null)
            {
                gameObject.AddComponent<CharacterActionController>();
            }
        }

        public void ApplyVisualScale()
        {
            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.one * visualScale;
            }
        }

        /// <summary>入定打坐：略压扁视觉，表示收敛坐下（非选目标指令）。</summary>
        public void SetMeditationPose(bool meditating)
        {
            if (visualRoot == null)
            {
                return;
            }

            if (meditating)
            {
                visualRoot.localScale = new Vector3(visualScale * 1.05f, visualScale * 0.68f, visualScale);
            }
            else
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

        /// <summary>自由移动：取消工作／攻击。</summary>
        public void MoveTo(Vector2 worldPosition)
        {
            ClearWorkOrder();
            ClearAttackOrder();
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
            ClearAttackOrder();
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

            ClearAttackOrder();
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

        /// <summary>攻击：追到 NPC 旁进入交战；离开或另下指令则停止（伤害待战斗系统）。</summary>
        public void StartAttack(Transform target)
        {
            if (target == null)
            {
                return;
            }

            ClearWorkOrder();
            _attackTarget = target;
            _isAttacking = true;
            _attackPulse = 0f;
            if (!IsInAttackRange())
            {
                SetDestination(target.position);
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
            ClearAttackOrder();
            _hasDestination = false;
            _destination = transform.position;
            if (visual != null)
            {
                visual.color = Color.white;
            }
        }

        public void SetScheduleCompliance(bool compliant, bool requireWork, bool inWorkZone)
        {
            _scheduleCompliant = compliant;
            _requireWork = requireWork;
            _inWorkZone = inWorkZone;
        }

        private void UpdateAttackChase(float delta)
        {
            if (!_isAttacking)
            {
                return;
            }

            if (_attackTarget == null)
            {
                ClearAttackOrder();
                return;
            }

            if (!IsInAttackRange())
            {
                SetDestination(_attackTarget.position);
            }
            else if (_hasDestination)
            {
                _hasDestination = false;
                _destination = transform.position;
            }
        }

        private bool IsInAttackRange()
        {
            if (_attackTarget == null)
            {
                return false;
            }

            float r = AttackEngageRadius;
            return ((Vector2)transform.position - (Vector2)_attackTarget.position).sqrMagnitude <= r * r;
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

        private void ClearAttackOrder()
        {
            _isAttacking = false;
            _attackTarget = null;
            _attackPulse = 0f;
        }

        private void SetDestination(Vector2 worldPosition)
        {
            _destination = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
            _hasDestination = true;
        }
    }
}
