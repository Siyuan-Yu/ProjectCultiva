using UnityEngine;
using XianXia.Unity.Cultivation;
using XianXia.Unity.Presentation;
using XianXia.Unity.Resources;
using XianXia.Unity.Time;
using XianXia.Unity.World;

namespace XianXia.Unity.Actions
{
    /// <summary>
    /// 单角色统一行动控制器：移动到交互距离 → 执行 → 进度／产出 → 可中断。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DemoUnitController))]
    public sealed class CharacterActionController : MonoBehaviour
    {
        [SerializeField] private DemoUnitController unit;
        [SerializeField] private ActionSettings settings;

        private readonly CharacterAction _action = new();
        private ResourceInventory _inventory;
        private float _approachElapsedGameMinutes;
        private float _cycleAccumGameHours;
        private float _cultivateFeedbackBucket;
        private WorkSpot _gatherSpot;
        private WorkZone _gatherZone;
        private SpiritSiteZone _spiritSite;
        private ResourceType _gatherResource = ResourceType.Food;
        private float _unitsPerGameHour = 4f;

        public CharacterAction Current => _action;
        public ActionStatus Status => _action.Status;
        public ActionType ActionType => _action.ActionType;
        public bool IsBusy => _action.IsActive;
        public string StatusLabel => _action.StatusLabel;
        public string CancelReason => _action.CancelReason;
        public float Progress => _action.Progress;
        public string TargetName => _action.TargetName;
        public bool IsMovingToAction => _action.Status == ActionStatus.MovingToAction;

        private void Awake()
        {
            if (unit == null)
            {
                unit = GetComponent<DemoUnitController>();
            }

            if (settings == null)
            {
                settings = ActionSettings.Ensure();
            }

            _inventory = FindObjectOfType<ResourceInventory>();
        }

        private void Update()
        {
            if (unit == null || !_action.IsActive)
            {
                return;
            }

            GameClock clock = GameClock.Instance;
            float deltaGameMinutes = clock != null ? clock.DeltaGameMinutes : 0f;
            // 暂停时 ScaledDeltaTime/DeltaGameMinutes 为 0，自然停止进度与产出。

            switch (_action.Status)
            {
                case ActionStatus.MovingToAction:
                    TickApproach(deltaGameMinutes);
                    break;
                case ActionStatus.Working:
                    TickGather(deltaGameMinutes);
                    break;
                case ActionStatus.Cultivating:
                    TickCultivate(deltaGameMinutes);
                    break;
            }
        }

        private void OnGUI()
        {
            if (unit == null)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector3 screen = cam.WorldToScreenPoint(unit.transform.position + Vector3.up * 1.05f);
            if (screen.z < 0f)
            {
                return;
            }

            string label = string.IsNullOrEmpty(_action.StatusLabel) ? "空闲" : _action.StatusLabel;
            float guiY = Screen.height - screen.y;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.95f, 0.93f, 0.82f) }
            };
            GUI.Label(new Rect(screen.x - 60f, guiY - 10f, 120f, 20f), label, style);
        }

        public void IssueMove(Vector2 worldPoint)
        {
            InterruptCurrent("新命令：移动");
            _action.ResetToIdle();
            _action.ActionType = ActionType.Move;
            _action.TargetPoint = worldPoint;
            _action.TargetName = "地面";
            _action.Status = ActionStatus.MovingToAction;
            _action.StatusLabel = "移动中";
            _action.CanInterrupt = true;
            _approachElapsedGameMinutes = 0f;
            ClearGatherRefs();
            ClearSpiritRef();
            unit.CancelOrder();
            UnitCultivation cult = unit.GetComponent<UnitCultivation>();
            cult?.SetCultivating(false);
            unit.SetMeditationPose(false);
            unit.MoveTo(worldPoint);
            // Move 到达后 DemoUnitController 会清 destination；这里用轻量跟踪。
            _action.Status = ActionStatus.MovingToAction;
        }

        public void IssueGather(WorkSpot spot)
        {
            if (spot == null)
            {
                FailToIdle("目标工位无效");
                return;
            }

            InterruptCurrent("新命令：工作");
            WorkZone zone = spot.OwnerZone;
            ResourceType resource = spot.ResourceType;
            ActionType type = ActionSettings.GatherTypeFor(resource);

            _gatherSpot = spot;
            _gatherZone = zone;
            _gatherResource = resource;
            _unitsPerGameHour = zone != null ? Mathf.Max(0.1f, zone.UnitsPerGameHour) : 4f;
            ClearSpiritRef();

            _action.ResetToIdle();
            _action.ActionType = type;
            _action.Target = spot.transform;
            _action.TargetPoint = spot.Position;
            _action.TargetName = zone != null ? zone.DisplayName : spot.SpotName;
            _action.InteractionRange = spot.InteractRadius;
            _action.CycleGameHours = 1f / _unitsPerGameHour;
            _action.CanInterrupt = true;
            _action.Status = ActionStatus.MovingToAction;
            _action.StatusLabel = ActionSettings.MovingLabelFor(type, _action.TargetName);
            _approachElapsedGameMinutes = 0f;
            _cycleAccumGameHours = 0f;
            _action.Progress = 0f;

            UnitCultivation cult = unit.GetComponent<UnitCultivation>();
            cult?.SetCultivating(false);
            unit.SetMeditationPose(false);
            unit.CancelOrder();
            unit.MoveTo(spot.Position);

            WorldFeedbackOverlay.Ensure().SpawnOrderMarker(spot.Position);
            WorldFeedbackOverlay.Ensure().SpawnFloatingText(
                unit.transform.position,
                ActionSettings.LabelForGather(resource),
                new Color(1f, 0.85f, 0.35f),
                0.85f);
        }

        public void IssueCultivate(SpiritSiteZone site)
        {
            if (site == null)
            {
                FailToIdle("灵地无效");
                return;
            }

            InterruptCurrent("新命令：修炼");
            ClearGatherRefs();
            _spiritSite = site;

            Vector2 point = site.Bounds.center;
            float range = Mathf.Max(
                settings.DefaultInteractRange,
                Mathf.Min(site.Bounds.extents.x, site.Bounds.extents.y) - settings.SpiritSiteInteractPadding);

            _action.ResetToIdle();
            _action.ActionType = ActionType.Cultivate;
            _action.Target = site.transform;
            _action.TargetPoint = point;
            _action.TargetName = site.DisplayName;
            _action.InteractionRange = Mathf.Max(0.5f, range);
            _action.CycleGameHours = 1f;
            _action.CanInterrupt = true;
            _action.Status = ActionStatus.MovingToAction;
            _action.StatusLabel = ActionSettings.MovingLabelFor(ActionType.Cultivate, site.DisplayName);
            _approachElapsedGameMinutes = 0f;
            _cycleAccumGameHours = 0f;
            _cultivateFeedbackBucket = 0f;
            _action.Progress = 0f;

            UnitCultivation cult = unit.GetComponent<UnitCultivation>();
            cult?.SetCultivating(false);
            unit.SetMeditationPose(false);
            unit.CancelOrder();
            unit.MoveTo(point);

            WorldFeedbackOverlay.Ensure().SpawnOrderMarker(point);
            WorldFeedbackOverlay.Ensure().SpawnFloatingText(
                unit.transform.position,
                "开始修炼",
                new Color(0.45f, 0.85f, 1f),
                0.85f);
        }

        public void Cancel(string reason)
        {
            if (!_action.IsActive && _action.Status != ActionStatus.Interrupted)
            {
                unit?.CancelOrder();
                return;
            }

            if (!_action.CanInterrupt && _action.IsActive)
            {
                return;
            }

            InterruptCurrent(reason);
            _action.Status = ActionStatus.Interrupted;
            _action.CancelReason = reason;
            _action.StatusLabel = "已中断";
            _action.Progress = 0f;
        }

        public bool IsActivelyWorking()
        {
            return _action.Status == ActionStatus.Working;
        }

        public bool IsActivelyCultivating()
        {
            return _action.Status == ActionStatus.Cultivating;
        }

        private void TickApproach(float deltaGameMinutes)
        {
            if (_action.ActionType == ActionType.Move)
            {
                float distSq = ((Vector2)unit.transform.position - _action.TargetPoint).sqrMagnitude;
                if (!unit.HasDestination || distSq <= 0.04f)
                {
                    CompleteMove();
                }

                return;
            }

            if (!IsTargetValid())
            {
                FailToIdle("目标已消失");
                return;
            }

            _approachElapsedGameMinutes += deltaGameMinutes;
            if (_approachElapsedGameMinutes >= settings.ApproachTimeoutGameMinutes)
            {
                FailToIdle("无法到达目标");
                return;
            }

            RefreshTargetPoint();
            if (!IsInInteractionRange())
            {
                if (!unit.HasDestination)
                {
                    unit.MoveTo(_action.TargetPoint);
                }

                return;
            }

            // 到达
            unit.CancelOrder();
            EnterActivePhase();
        }

        private void EnterActivePhase()
        {
            _cycleAccumGameHours = 0f;
            _action.Progress = 0f;

            if (_action.ActionType == ActionType.Cultivate)
            {
                _action.Status = ActionStatus.Cultivating;
                _action.StatusLabel = ActionSettings.ActiveLabelFor(ActionType.Cultivate);
                unit.SetMeditationPose(true);
                WorldFeedbackOverlay.Ensure().SpawnFloatingText(
                    unit.transform.position,
                    "入定",
                    new Color(0.45f, 0.85f, 1f),
                    0.9f);
                return;
            }

            _action.Status = ActionStatus.Working;
            _action.StatusLabel = ActionSettings.ActiveLabelFor(_action.ActionType);
            // 同步旧 Working 标志，供课表遵守等系统读取。
            if (_gatherSpot != null)
            {
                unit.StartWorkAt(_gatherSpot);
            }

            WorldFeedbackOverlay.Ensure().SpawnFloatingText(
                unit.transform.position,
                _action.StatusLabel,
                new Color(1f, 0.85f, 0.3f),
                0.8f);
        }

        private void TickGather(float deltaGameMinutes)
        {
            if (!IsTargetValid())
            {
                FailToIdle("目标已消失");
                return;
            }

            if (!IsInInteractionRange())
            {
                // 被挪开：停工并尝试走回。
                unit.CancelOrder();
                _action.Status = ActionStatus.MovingToAction;
                _action.StatusLabel = ActionSettings.MovingLabelFor(_action.ActionType, _action.TargetName);
                _approachElapsedGameMinutes = 0f;
                unit.MoveTo(_action.TargetPoint);
                return;
            }

            // StartWorkAt 会保持 _isWorking；若被外部 CancelOrder 清掉则重建。
            if (!unit.IsWorking && _gatherSpot != null)
            {
                unit.StartWorkAt(_gatherSpot);
            }

            float gameHours = deltaGameMinutes / 60f;
            if (gameHours <= 0f)
            {
                return;
            }

            _cycleAccumGameHours += gameHours;
            float cycle = Mathf.Max(0.05f, _action.CycleGameHours);
            _action.Progress = Mathf.Clamp01(_cycleAccumGameHours / cycle);

            // 产出由本控制器负责，避免与旧 WorkSystem 双计。
            while (_cycleAccumGameHours >= cycle)
            {
                _cycleAccumGameHours -= cycle;
                _action.Progress = Mathf.Clamp01(_cycleAccumGameHours / cycle);
                if (_inventory != null)
                {
                    _inventory.Add(_gatherResource, 1);
                    WorldFeedbackOverlay.Ensure().SpawnFloatingText(
                        unit.transform.position,
                        $"+1{ResourceShortName(_gatherResource)}",
                        ResourceColor(_gatherResource));
                }
            }
        }

        private void TickCultivate(float deltaGameMinutes)
        {
            if (_spiritSite == null)
            {
                FailToIdle("灵地已消失");
                return;
            }

            if (!_spiritSite.Contains(unit.transform.position))
            {
                unit.SetMeditationPose(false);
                _action.Status = ActionStatus.MovingToAction;
                _action.StatusLabel = ActionSettings.MovingLabelFor(ActionType.Cultivate, _action.TargetName);
                _approachElapsedGameMinutes = 0f;
                unit.MoveTo(_spiritSite.Bounds.center);
                return;
            }

            float gameHours = deltaGameMinutes / 60f;
            if (gameHours <= 0f)
            {
                return;
            }

            UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
            if (cultivation == null)
            {
                FailToIdle("缺少修为组件");
                return;
            }

            float before = cultivation.CultivationProgress;
            float rate = settings.CultivateProgressPerGameHour;
            cultivation.AddProgress(rate * gameHours);
            float gained = cultivation.CultivationProgress - before;
            _cultivateFeedbackBucket += gained;
            _action.Progress = Mathf.Clamp01((cultivation.CultivationProgress % 100f) / 100f);

            if (_cultivateFeedbackBucket >= 10f)
            {
                WorldFeedbackOverlay.Ensure().SpawnFloatingText(
                    unit.transform.position,
                    $"+{_cultivateFeedbackBucket:0}修为",
                    new Color(0.45f, 0.85f, 1f),
                    0.9f);
                _cultivateFeedbackBucket = 0f;
            }
        }

        private void CompleteMove()
        {
            _action.Status = ActionStatus.Completed;
            _action.StatusLabel = "空闲";
            _action.Progress = 1f;
            _action.ResetToIdle();
        }

        private void InterruptCurrent(string reason)
        {
            if (!_action.IsActive)
            {
                return;
            }

            unit.CancelOrder();
            unit.SetMeditationPose(false);
            UnitCultivation cult = unit.GetComponent<UnitCultivation>();
            cult?.SetCultivating(false);
            _action.CancelReason = reason;
        }

        private void FailToIdle(string reason)
        {
            InterruptCurrent(reason);
            _action.Status = ActionStatus.Interrupted;
            _action.CancelReason = reason;
            _action.StatusLabel = "失败";
            _action.Progress = 0f;
            WorldFeedbackOverlay.Ensure().SpawnFloatingText(
                unit != null ? unit.transform.position : Vector3.zero,
                reason,
                new Color(1f, 0.55f, 0.45f),
                1.2f);
            ClearGatherRefs();
            ClearSpiritRef();
            // 短暂展示失败后回到 Idle 数据，但保留 CancelReason 供 HUD 读。
            string keepReason = reason;
            _action.ActionType = ActionType.None;
            _action.Target = null;
            _action.Status = ActionStatus.Idle;
            _action.StatusLabel = "空闲";
            _action.CancelReason = keepReason;
        }

        private bool IsTargetValid()
        {
            if (_action.ActionType == ActionType.Cultivate)
            {
                return _spiritSite != null;
            }

            if (_action.ActionType == ActionType.GatherWood
                || _action.ActionType == ActionType.GatherHerb
                || _action.ActionType == ActionType.Farm)
            {
                return _gatherSpot != null;
            }

            return true;
        }

        private void RefreshTargetPoint()
        {
            if (_gatherSpot != null)
            {
                _action.TargetPoint = _gatherSpot.Position;
                _action.InteractionRange = _gatherSpot.InteractRadius;
            }
            else if (_spiritSite != null)
            {
                _action.TargetPoint = _spiritSite.Bounds.center;
            }
        }

        private bool IsInInteractionRange()
        {
            if (_action.ActionType == ActionType.Cultivate && _spiritSite != null)
            {
                return _spiritSite.Contains(unit.transform.position);
            }

            if (_gatherSpot != null)
            {
                return _gatherSpot.IsInRange(unit.transform.position);
            }

            float r = _action.InteractionRange;
            return ((Vector2)unit.transform.position - _action.TargetPoint).sqrMagnitude <= r * r;
        }

        private void ClearGatherRefs()
        {
            _gatherSpot = null;
            _gatherZone = null;
        }

        private void ClearSpiritRef()
        {
            _spiritSite = null;
        }

        private static string ResourceShortName(ResourceType type)
        {
            return type switch
            {
                ResourceType.Food => "粮",
                ResourceType.Wood => "木",
                ResourceType.Herb => "药",
                _ => type.ToString()
            };
        }

        private static Color ResourceColor(ResourceType type)
        {
            return type switch
            {
                ResourceType.Food => new Color(0.95f, 0.85f, 0.35f),
                ResourceType.Wood => new Color(0.7f, 0.9f, 0.45f),
                ResourceType.Herb => new Color(0.55f, 0.9f, 0.7f),
                _ => Color.white
            };
        }
    }
}
