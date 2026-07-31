using System.Collections.Generic;
using UnityEngine;
using XianXia.Unity.Actions;
using XianXia.Unity.Cultivation;
using XianXia.Unity.Presentation;
using XianXia.Unity.World;

namespace XianXia.Unity.Input
{
    public enum WorldInspectKind
    {
        None = 0,
        Unit = 1,
        Structure = 2,
        WorkZone = 3,
        SpiritSite = 4,
        NpcCharacter = 5
    }

    /// <summary>
    /// 当前点选查看目标。只读展示，不对建筑下指令。
    /// </summary>
    public sealed class WorldInspection
    {
        public WorldInspectKind Kind { get; set; }
        public DemoUnitController Unit { get; set; }
        public WorldCharacterInspectable NpcCharacter { get; set; }
        public StructureInspectable Structure { get; set; }
        public WorkZone WorkZone { get; set; }
        public SpiritSiteZone SpiritSite { get; set; }

        public bool HasTarget => Kind != WorldInspectKind.None;

        public void Clear()
        {
            Kind = WorldInspectKind.None;
            Unit = null;
            NpcCharacter = null;
            Structure = null;
            WorkZone = null;
            SpiritSite = null;
        }
    }

    public enum CommandTargetingMode
    {
        None = 0,
        Work = 1,
        Attack = 2
    }

    /// <summary>
    /// RTS 指令：框选／点选；工作／攻击进入选目标模式后再点目标；右键空地／工位只移动。
    /// </summary>
    public sealed class PartyCommandController : MonoBehaviour
    {
        private const float BoxSelectPixelThreshold = 8f;
        private const float DoubleClickSeconds = 0.35f;

        [SerializeField] private Camera worldCamera;
        [SerializeField] private float formationSpacing = 1.25f;
        [SerializeField] private WorkSystem workSystem;
        [SerializeField] private DemoUnitController[] partyUnits;

        private readonly List<DemoUnitController> _selectedUnits = new();
        private readonly WorldInspection _inspection = new();

        private bool _leftDragActive;
        private bool _boxSelecting;
        private bool _pointerStartedOverUi;
        private CommandTargetingMode _targetingMode;
        private bool _customCursorApplied;
        private Texture2D _workCursorTexture;
        private Texture2D _attackCursorTexture;
        private Vector2 _dragStartScreen;
        private Vector2 _dragCurrentScreen;
        private WorkSpot _hoveredWorkSpot;
        private WorldCharacterInspectable _hoveredAttackTarget;
        private float _lastUnitClickTime = -999f;
        private DemoUnitController _lastClickedUnit;

        public IReadOnlyList<DemoUnitController> SelectedUnits => _selectedUnits;
        public WorldInspection Inspection => _inspection;
        public bool IsBoxSelecting => _boxSelecting;
        public bool IsWorkTargeting => _targetingMode == CommandTargetingMode.Work;
        public bool IsAttackTargeting => _targetingMode == CommandTargetingMode.Attack;
        public bool IsCommandTargeting => _targetingMode != CommandTargetingMode.None;
        public WorkSpot HoveredWorkSpot => _hoveredWorkSpot;
        public WorldCharacterInspectable HoveredAttackTarget => _hoveredAttackTarget;

        /// <summary>
        /// 由 HUD 设置：指针落在交互 UI 上时，世界点选／框选应忽略。
        /// </summary>
        public System.Func<Vector2, bool> IsPointerOverUi { get; set; }

        public Rect CurrentBoxScreenRect
        {
            get
            {
                float xMin = Mathf.Min(_dragStartScreen.x, _dragCurrentScreen.x);
                float xMax = Mathf.Max(_dragStartScreen.x, _dragCurrentScreen.x);
                float yMin = Mathf.Min(_dragStartScreen.y, _dragCurrentScreen.y);
                float yMax = Mathf.Max(_dragStartScreen.y, _dragCurrentScreen.y);
                return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            }
        }

        public void Configure(Camera camera, WorkSystem work, DemoUnitController[] units)
        {
            worldCamera = camera;
            workSystem = work;
            partyUnits = units;
        }

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (workSystem == null)
            {
                workSystem = FindObjectOfType<WorkSystem>();
            }

            if (partyUnits == null || partyUnits.Length == 0)
            {
                partyUnits = FindObjectsOfType<DemoUnitController>();
            }
        }

        private void Start()
        {
            EnsureDefaultStructureInspectables();
            EnsureSpiritSiteMapMarker();
            EnsureWorldCharacterInspectables();
            workSystem?.EnsureAllZoneSpots();
            WorldFeedbackOverlay.Ensure();
            EnsurePartyOverheads();
        }

        private void OnDisable()
        {
            CancelCommandTargeting();
        }

        private void OnDestroy()
        {
            ApplyCustomCursor(CommandTargetingMode.None);
            if (_workCursorTexture != null)
            {
                Destroy(_workCursorTexture);
                _workCursorTexture = null;
            }

            if (_attackCursorTexture != null)
            {
                Destroy(_attackCursorTexture);
                _attackCursorTexture = null;
            }
        }

        private void Update()
        {
            if (worldCamera == null)
            {
                return;
            }

            HandleLeftMouse();
            UpdateCommandTargetingFeedback();

            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                Vector2 screen = UnityEngine.Input.mousePosition;
                if (IsPointerOverUi == null || !IsPointerOverUi(screen))
                {
                    if (_targetingMode != CommandTargetingMode.None)
                    {
                        CancelCommandTargeting();
                    }
                    else
                    {
                        CommandSelection();
                    }
                }
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.W))
            {
                BeginWorkCommand();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.A))
            {
                BeginAttackCommand();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) && _targetingMode != CommandTargetingMode.None)
            {
                CancelCommandTargeting();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.S))
            {
                StopSelectedOrders();
            }
        }

        /// <summary>
        /// 显式「工作」指令：进入选目标模式。再点工位／工作区 → 寻路过去并开工。
        /// </summary>
        public void BeginWorkCommand()
        {
            if (_selectedUnits.Count == 0)
            {
                return;
            }

            workSystem?.EnsureAllZoneSpots();
            _targetingMode = CommandTargetingMode.Work;
            ApplyCustomCursor(CommandTargetingMode.Work);
        }

        /// <summary>
        /// 攻击指令：进入选目标模式。再点 NPC → 寻路过去进入交战（尚无伤害结算）。
        /// </summary>
        public void BeginAttackCommand()
        {
            if (_selectedUnits.Count == 0)
            {
                return;
            }

            _targetingMode = CommandTargetingMode.Attack;
            workSystem?.SetWorkTargetingVisuals(false, null);
            ApplyCustomCursor(CommandTargetingMode.Attack);
        }

        public void CancelWorkTargeting()
        {
            CancelCommandTargeting();
        }

        public void CancelCommandTargeting()
        {
            _targetingMode = CommandTargetingMode.None;
            _hoveredWorkSpot = null;
            _hoveredAttackTarget = null;
            workSystem?.SetWorkTargetingVisuals(false, null);
            ApplyCustomCursor(CommandTargetingMode.None);
        }

        public void StopSelectedOrders()
        {
            CancelCommandTargeting();
            for (int i = 0; i < _selectedUnits.Count; i++)
            {
                DemoUnitController unit = _selectedUnits[i];
                if (unit == null)
                {
                    continue;
                }

                CharacterActionController actions = EnsureActions(unit);
                actions.Cancel("玩家停止");
                unit.CancelOrder();
                UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
                cultivation?.SetCultivating(false);
                unit.SetMeditationPose(false);
            }
        }

        public void SelectAllPartyUnits()
        {
            ClearSelection();
            ClearInspection();
            DemoUnitController[] units = ResolvePartyUnits();
            for (int i = 0; i < units.Length; i++)
            {
                DemoUnitController unit = units[i];
                if (unit == null)
                {
                    continue;
                }

                _selectedUnits.Add(unit);
                unit.SetSelected(true);
            }
        }

        private void UpdateCommandTargetingFeedback()
        {
            if (_targetingMode == CommandTargetingMode.None)
            {
                if (_customCursorApplied)
                {
                    ApplyCustomCursor(CommandTargetingMode.None);
                }

                return;
            }

            Vector2 worldPoint = worldCamera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            if (_targetingMode == CommandTargetingMode.Work)
            {
                _hoveredAttackTarget = null;
                _hoveredWorkSpot = ResolveWorkSpot(worldPoint);
                workSystem?.SetWorkTargetingVisuals(true, _hoveredWorkSpot);
            }
            else if (_targetingMode == CommandTargetingMode.Attack)
            {
                _hoveredWorkSpot = null;
                workSystem?.SetWorkTargetingVisuals(false, null);
                ResolveClickTargets(
                    worldPoint,
                    out _,
                    out WorldCharacterInspectable npc,
                    out _,
                    out _,
                    out _);
                _hoveredAttackTarget = npc;
            }

            ApplyCustomCursor(_targetingMode);
        }

        private void ApplyCustomCursor(CommandTargetingMode mode)
        {
            if (mode == CommandTargetingMode.Work)
            {
                if (_workCursorTexture == null)
                {
                    _workCursorTexture = CreateRingCursorTexture(new Color(1f, 0.85f, 0.2f, 1f));
                }

                Cursor.SetCursor(_workCursorTexture, new Vector2(8f, 8f), CursorMode.Auto);
                _customCursorApplied = true;
            }
            else if (mode == CommandTargetingMode.Attack)
            {
                if (_attackCursorTexture == null)
                {
                    _attackCursorTexture = CreateCrossCursorTexture(new Color(1f, 0.25f, 0.25f, 1f));
                }

                Cursor.SetCursor(_attackCursorTexture, new Vector2(8f, 8f), CursorMode.Auto);
                _customCursorApplied = true;
            }
            else if (_customCursorApplied)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                _customCursorApplied = false;
            }
        }

        private static Texture2D CreateRingCursorTexture(Color ring)
        {
            const int size = 24;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color clear = new(0f, 0f, 0f, 0f);
            Color fill = new(ring.r, ring.g, ring.b, 0.35f);
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= 3.2f)
                    {
                        texture.SetPixel(x, y, ring);
                    }
                    else if (d <= 8.5f && d >= 6.5f)
                    {
                        texture.SetPixel(x, y, ring);
                    }
                    else if (d < 6.5f)
                    {
                        texture.SetPixel(x, y, fill);
                    }
                    else
                    {
                        texture.SetPixel(x, y, clear);
                    }
                }
            }

            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateCrossCursorTexture(Color color)
        {
            const int size = 24;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color clear = new(0f, 0f, 0f, 0f);
            int mid = size / 2;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool arm =
                        (Mathf.Abs(x - mid) <= 1 && (y < mid - 3 || y > mid + 3))
                        || (Mathf.Abs(y - mid) <= 1 && (x < mid - 3 || x > mid + 3));
                    bool center = Mathf.Abs(x - mid) <= 1 && Mathf.Abs(y - mid) <= 1;
                    texture.SetPixel(x, y, arm || center ? color : clear);
                }
            }

            texture.Apply(false, true);
            return texture;
        }

        private void CommandWorkAtPointer()
        {
            if (_selectedUnits.Count == 0)
            {
                CancelCommandTargeting();
                return;
            }

            Vector2 worldPoint = worldCamera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            WorkSpot spot = ResolveWorkSpot(worldPoint);
            if (spot == null)
            {
                return;
            }

            AssignWorkToSpot(spot);
            CancelCommandTargeting();
        }

        private void CommandAttackAtPointer()
        {
            if (_selectedUnits.Count == 0)
            {
                CancelCommandTargeting();
                return;
            }

            Vector2 worldPoint = worldCamera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            ResolveClickTargets(
                worldPoint,
                out _,
                out WorldCharacterInspectable npc,
                out _,
                out _,
                out _);
            if (npc == null)
            {
                return;
            }

            AssignAttackToTarget(npc);
            WorldFeedbackOverlay.Ensure().SpawnOrderMarker(npc.transform.position);
            WorldFeedbackOverlay.Ensure().SpawnFloatingText(
                npc.transform.position,
                "交战!",
                new Color(1f, 0.35f, 0.3f),
                0.9f);
            CancelCommandTargeting();
        }

        private WorkSpot ResolveWorkSpot(Vector2 worldPoint)
        {
            workSystem?.EnsureAllZoneSpots();
            if (workSystem != null && workSystem.TryGetSpot(worldPoint, out WorkSpot spot))
            {
                return spot;
            }

            if (workSystem != null && workSystem.TryGetZone(worldPoint, out WorkZone zone))
            {
                return zone.FindNearestSpot(worldPoint);
            }

            return null;
        }

        private void CommandSelection()
        {
            if (_selectedUnits.Count == 0)
            {
                return;
            }

            Vector2 worldPoint = worldCamera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);

            // 优先：工位／工作区 → 唯一可用工作行动。
            WorkSpot spot = ResolveWorkSpot(worldPoint);
            if (spot != null)
            {
                AssignWorkToSpot(spot);
                return;
            }

            // 灵地 → 开始修炼。
            ResolveClickTargets(
                worldPoint,
                out _,
                out _,
                out _,
                out _,
                out SpiritSiteZone spiritSite);
            if (spiritSite == null && workSystem != null)
            {
                // ResolveClickTargets 未命中时再扫一遍灵地包围盒。
                SpiritSiteZone[] sites = FindObjectsOfType<SpiritSiteZone>();
                for (int i = 0; i < sites.Length; i++)
                {
                    if (sites[i] != null && sites[i].Contains(worldPoint))
                    {
                        spiritSite = sites[i];
                        break;
                    }
                }
            }

            if (spiritSite != null)
            {
                AssignCultivate(spiritSite);
                return;
            }

            MoveSelection(worldPoint);
        }

        private void AssignWorkToSpot(WorkSpot spot)
        {
            int index = 0;
            int total = 0;
            for (int i = 0; i < _selectedUnits.Count; i++)
            {
                if (_selectedUnits[i] != null)
                {
                    total++;
                }
            }

            for (int i = 0; i < _selectedUnits.Count; i++)
            {
                DemoUnitController unit = _selectedUnits[i];
                if (unit == null)
                {
                    continue;
                }

                WorkSpot target = spot;
                if (total > 1 && spot.OwnerZone != null)
                {
                    WorkSpot alt = workSystem != null
                        ? workSystem.GetSpot(spot.OwnerZone, index)
                        : spot.OwnerZone.FindNearestSpot(spot.Position);
                    if (alt != null)
                    {
                        target = alt;
                    }
                }

                EnsureActions(unit).IssueGather(target);
                index++;
            }
        }

        private void AssignCultivate(SpiritSiteZone site)
        {
            if (site == null)
            {
                return;
            }

            for (int i = 0; i < _selectedUnits.Count; i++)
            {
                DemoUnitController unit = _selectedUnits[i];
                if (unit == null)
                {
                    continue;
                }

                EnsureActions(unit).IssueCultivate(site);
            }
        }

        private void AssignAttackToTarget(WorldCharacterInspectable target)
        {
            if (target == null)
            {
                return;
            }

            for (int i = 0; i < _selectedUnits.Count; i++)
            {
                DemoUnitController unit = _selectedUnits[i];
                if (unit == null)
                {
                    continue;
                }

                CharacterActionController actions = EnsureActions(unit);
                actions.Cancel("新命令：攻击");
                UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
                cultivation?.SetCultivating(false);
                unit.StartAttack(target.transform);
            }
        }

        private void MoveSelection(Vector2 center)
        {
            int columns = Mathf.CeilToInt(Mathf.Sqrt(_selectedUnits.Count));

            for (int i = 0; i < _selectedUnits.Count; i++)
            {
                DemoUnitController unit = _selectedUnits[i];
                if (unit == null)
                {
                    continue;
                }

                int row = i / columns;
                int column = i % columns;
                float x = (column - (columns - 1) * 0.5f) * formationSpacing;
                float y = -row * formationSpacing;
                EnsureActions(unit).IssueMove(center + new Vector2(x, y));
            }
        }

        private static CharacterActionController EnsureActions(DemoUnitController unit)
        {
            CharacterActionController actions = unit.GetComponent<CharacterActionController>();
            if (actions == null)
            {
                actions = unit.gameObject.AddComponent<CharacterActionController>();
            }

            return actions;
        }

        private void ClearSelection()
        {
            CancelCommandTargeting();
            foreach (DemoUnitController unit in _selectedUnits)
            {
                if (unit != null)
                {
                    unit.SetSelected(false);
                }
            }

            _selectedUnits.Clear();
        }

        public void ClearInspection()
        {
            _inspection.Clear();
        }

        private void OnGUI()
        {
            if (_targetingMode == CommandTargetingMode.Work)
            {
                Color previous = GUI.color;
                GUI.color = new Color(1f, 0.92f, 0.35f, 1f);
                string hoverName = _hoveredWorkSpot != null
                    ? $" → {_hoveredWorkSpot.SpotName}"
                    : "（点黄色工位）";
                GUI.Label(
                    new Rect(12f, 12f, 560f, 28f),
                    $"工作指令：选择目标工位{hoverName} · 左键确认 · 右键/Esc取消");
                GUI.color = previous;
            }
            else if (_targetingMode == CommandTargetingMode.Attack)
            {
                Color previous = GUI.color;
                GUI.color = new Color(1f, 0.4f, 0.35f, 1f);
                string hoverName = _hoveredAttackTarget != null
                    ? $" → {_hoveredAttackTarget.DisplayName}"
                    : "（点 NPC）";
                GUI.Label(
                    new Rect(12f, 12f, 560f, 28f),
                    $"攻击指令：选择目标{hoverName} · 左键确认 · 右键/Esc取消");
                GUI.color = previous;
            }

            if (!_boxSelecting)
            {
                return;
            }

            Rect screenRect = CurrentBoxScreenRect;
            float guiY = Screen.height - screenRect.yMax;
            Rect guiRect = new(screenRect.xMin, guiY, screenRect.width, screenRect.height);

            Color prev = GUI.color;
            GUI.color = new Color(0.3f, 0.85f, 0.35f, 0.18f);
            GUI.DrawTexture(guiRect, Texture2D.whiteTexture);
            GUI.color = new Color(0.35f, 0.95f, 0.4f, 0.9f);
            DrawRectBorder(guiRect, 2f);
            GUI.color = prev;
        }

        private static void DrawRectBorder(Rect rect, float thickness)
        {
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), Texture2D.whiteTexture);
        }

        private void HandleLeftMouse()
        {
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                Vector2 screen = UnityEngine.Input.mousePosition;
                _pointerStartedOverUi = IsPointerOverUi != null && IsPointerOverUi(screen);
                if (_pointerStartedOverUi)
                {
                    _leftDragActive = false;
                    _boxSelecting = false;
                    return;
                }

                _leftDragActive = true;
                _boxSelecting = false;
                _dragStartScreen = screen;
                _dragCurrentScreen = _dragStartScreen;
            }

            if (_pointerStartedOverUi)
            {
                if (UnityEngine.Input.GetMouseButtonUp(0))
                {
                    _pointerStartedOverUi = false;
                }

                return;
            }

            if (_leftDragActive && UnityEngine.Input.GetMouseButton(0))
            {
                _dragCurrentScreen = UnityEngine.Input.mousePosition;
                if (!_boxSelecting
                    && Vector2.Distance(_dragStartScreen, _dragCurrentScreen) >= BoxSelectPixelThreshold)
                {
                    _boxSelecting = true;
                }
            }

            if (_leftDragActive && UnityEngine.Input.GetMouseButtonUp(0))
            {
                _dragCurrentScreen = UnityEngine.Input.mousePosition;
                bool additive = UnityEngine.Input.GetKey(KeyCode.LeftShift)
                    || UnityEngine.Input.GetKey(KeyCode.RightShift);

                if (_boxSelecting)
                {
                    ApplyBoxSelection(additive);
                }
                else if (_targetingMode == CommandTargetingMode.Work)
                {
                    CommandWorkAtPointer();
                }
                else if (_targetingMode == CommandTargetingMode.Attack)
                {
                    CommandAttackAtPointer();
                }
                else
                {
                    HandleClick(additive);
                }

                _leftDragActive = false;
                _boxSelecting = false;
            }
        }

        private void ApplyBoxSelection(bool additive)
        {
            if (!additive)
            {
                ClearSelection();
            }

            ClearInspection();

            Rect box = CurrentBoxScreenRect;
            DemoUnitController[] units = ResolvePartyUnits();
            for (int i = 0; i < units.Length; i++)
            {
                DemoUnitController unit = units[i];
                if (unit == null)
                {
                    continue;
                }

                Vector3 screen = worldCamera.WorldToScreenPoint(unit.transform.position);
                if (screen.z < 0f)
                {
                    continue;
                }

                if (!box.Contains(new Vector2(screen.x, screen.y)))
                {
                    continue;
                }

                if (!_selectedUnits.Contains(unit))
                {
                    _selectedUnits.Add(unit);
                    unit.SetSelected(true);
                }
            }
        }

        private void HandleClick(bool additive)
        {
            Vector2 worldPoint = worldCamera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            ResolveClickTargets(
                worldPoint,
                out DemoUnitController unit,
                out WorldCharacterInspectable npc,
                out StructureInspectable structure,
                out WorkZone workZone,
                out SpiritSiteZone spiritSite);

            if (unit != null)
            {
                float now = UnityEngine.Time.unscaledTime;
                bool doubleClick = !additive
                    && unit == _lastClickedUnit
                    && now - _lastUnitClickTime <= DoubleClickSeconds;
                _lastUnitClickTime = now;
                _lastClickedUnit = unit;

                if (doubleClick)
                {
                    SelectAllPartyUnits();
                    InspectUnit(unit);
                    WorldFeedbackOverlay.Ensure().SpawnFloatingText(
                        unit.transform.position,
                        "全选",
                        new Color(0.6f, 0.95f, 0.7f),
                        0.7f);
                    return;
                }

                SelectUnitClick(unit, additive);
                if (!additive)
                {
                    InspectUnit(unit);
                }

                return;
            }

            if (!additive)
            {
                ClearSelection();
            }

            if (npc != null)
            {
                InspectNpcCharacter(npc);
                return;
            }

            if (structure != null)
            {
                InspectStructure(structure);
                return;
            }

            if (workZone != null)
            {
                InspectWorkZone(workZone);
                return;
            }

            if (spiritSite != null)
            {
                InspectSpiritSite(spiritSite);
                return;
            }

            ClearInspection();
        }

        private void SelectUnitClick(DemoUnitController unit, bool additive)
        {
            if (!additive)
            {
                ClearSelection();
            }

            if (additive && _selectedUnits.Contains(unit))
            {
                unit.SetSelected(false);
                _selectedUnits.Remove(unit);
                return;
            }

            if (!_selectedUnits.Contains(unit))
            {
                _selectedUnits.Add(unit);
                unit.SetSelected(true);
            }
        }

        private void ResolveClickTargets(
            Vector2 worldPoint,
            out DemoUnitController unit,
            out WorldCharacterInspectable npc,
            out StructureInspectable structure,
            out WorkZone workZone,
            out SpiritSiteZone spiritSite)
        {
            unit = null;
            npc = null;
            structure = null;
            workZone = null;
            spiritSite = null;

            Collider2D[] hits = Physics2D.OverlapPointAll(worldPoint);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                if (unit == null)
                {
                    DemoUnitController candidate = hit.GetComponentInParent<DemoUnitController>();
                    if (candidate != null && IsPartyUnit(candidate))
                    {
                        unit = candidate;
                    }
                }

                if (npc == null)
                {
                    npc = hit.GetComponentInParent<WorldCharacterInspectable>();
                }

                if (structure == null)
                {
                    structure = hit.GetComponentInParent<StructureInspectable>();
                }

                if (workZone == null)
                {
                    workZone = hit.GetComponentInParent<WorkZone>();
                }

                if (spiritSite == null)
                {
                    spiritSite = hit.GetComponentInParent<SpiritSiteZone>();
                }
            }

            if (workZone == null && workSystem != null)
            {
                workSystem.TryGetZone(worldPoint, out workZone);
            }

            if (spiritSite == null)
            {
                SpiritSiteZone[] sites = FindObjectsOfType<SpiritSiteZone>();
                for (int i = 0; i < sites.Length; i++)
                {
                    if (sites[i] != null && sites[i].Contains(worldPoint))
                    {
                        spiritSite = sites[i];
                        break;
                    }
                }
            }
        }

        private bool IsPartyUnit(DemoUnitController unit)
        {
            DemoUnitController[] units = ResolvePartyUnits();
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] == unit)
                {
                    return true;
                }
            }

            return false;
        }

        private DemoUnitController[] ResolvePartyUnits()
        {
            if (partyUnits != null && partyUnits.Length > 0)
            {
                return partyUnits;
            }

            partyUnits = FindObjectsOfType<DemoUnitController>();
            return partyUnits;
        }

        private void EnsurePartyOverheads()
        {
            DemoUnitController[] units = ResolvePartyUnits();
            for (int i = 0; i < units.Length; i++)
            {
                DemoUnitController unit = units[i];
                if (unit == null)
                {
                    continue;
                }

                if (unit.GetComponent<UnitActivityOverhead>() == null)
                {
                    unit.gameObject.AddComponent<UnitActivityOverhead>();
                }

                if (unit.GetComponent<UnitOrderPathPreview>() == null)
                {
                    unit.gameObject.AddComponent<UnitOrderPathPreview>();
                }

                if (unit.GetComponent<CharacterActionController>() == null)
                {
                    unit.gameObject.AddComponent<CharacterActionController>();
                }
            }
        }

        private void InspectUnit(DemoUnitController unit)
        {
            _inspection.Clear();
            _inspection.Kind = WorldInspectKind.Unit;
            _inspection.Unit = unit;
        }

        private void InspectNpcCharacter(WorldCharacterInspectable character)
        {
            _inspection.Clear();
            _inspection.Kind = WorldInspectKind.NpcCharacter;
            _inspection.NpcCharacter = character;
        }

        private void InspectStructure(StructureInspectable structure)
        {
            _inspection.Clear();
            _inspection.Kind = WorldInspectKind.Structure;
            _inspection.Structure = structure;
        }

        private void InspectWorkZone(WorkZone zone)
        {
            _inspection.Clear();
            _inspection.Kind = WorldInspectKind.WorkZone;
            _inspection.WorkZone = zone;
        }

        private void InspectSpiritSite(SpiritSiteZone site)
        {
            _inspection.Clear();
            _inspection.Kind = WorldInspectKind.SpiritSite;
            _inspection.SpiritSite = site;
        }

        /// <summary>
        /// 兼容未重建场景：按对象名给已知建筑补上可点选详情。
        /// </summary>
        private static void EnsureDefaultStructureInspectables()
        {
            EnsureStructureByName("House_01", "民宅", "凡人住宅：人口容量与群体表现占位", "可居住占位（未接人口系统）");
            EnsureStructureByName("House_02", "民宅", "凡人住宅：人口容量与群体表现占位", "可居住占位（未接人口系统）");
            EnsureStructureByName("House_03", "民宅", "凡人住宅：人口容量与群体表现占位", "可居住占位（未接人口系统）");
            EnsureStructureByName("House_04", "民宅", "凡人住宅：人口容量与群体表现占位", "可居住占位（未接人口系统）");
            EnsureStructureByName("SupervisorHouse", "主管府", "控制核心；最终夺取目标", "控制核心（未实装）");
            EnsureStructureByName("Warehouse", "仓库", "资源存放占位", "库存由 HUD 资源面板显示");
        }

        private static void EnsureStructureByName(string objectName, string displayName, string purpose, string note)
        {
            GameObject go = GameObject.Find(objectName);
            if (go == null)
            {
                return;
            }

            StructureInspectable inspectable = go.GetComponent<StructureInspectable>();
            if (inspectable == null)
            {
                inspectable = go.AddComponent<StructureInspectable>();
            }

            inspectable.Configure(displayName, purpose, note);

            if (go.GetComponent<Collider2D>() == null)
            {
                BoxCollider2D box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(1.6f, 1.2f);
                box.offset = new Vector2(0f, 0.6f);
            }
        }

        private static void EnsureSpiritSiteMapMarker()
        {
            SpiritSiteZone site = FindObjectOfType<SpiritSiteZone>();
            if (site == null)
            {
                return;
            }

            if (site.GetComponent<SpiritSiteMapMarker>() == null)
            {
                site.gameObject.AddComponent<SpiritSiteMapMarker>();
            }
        }

        private static void EnsureWorldCharacterInspectables()
        {
            EnsureCharacterByName(
                "Supervisor",
                "主管",
                "村主管",
                "筑基",
                "管辖配额、愤怒与最终夺权目标",
                0.95f);
            EnsureCharacterByName(
                "Guard_01",
                "守卫甲",
                "守卫",
                "炼气",
                "巡视工作区与主管府周边",
                0.55f);
            EnsureCharacterByName(
                "Guard_02",
                "守卫乙",
                "守卫",
                "炼气",
                "巡视工作区与主管府周边",
                0.55f);
            EnsureCharacterByName(
                "Merchant",
                "行商",
                "商人",
                "凡人",
                "在村中走动交易（占位）",
                0f);
            EnsureCharacterByName("Laborer_01", "村民甲", "村民", "凡人", "按课表去农田／吃饭／睡觉", 0f);
            EnsureCharacterByName("Laborer_02", "村民乙", "村民", "凡人", "按课表去森林／吃饭／睡觉", 0f);
            EnsureCharacterByName("Laborer_03", "村民丙", "村民", "凡人", "按课表去农田／吃饭／睡觉", 0f);
            EnsureCharacterByName("Laborer_04", "村民丁", "村民", "凡人", "按课表去森林／吃饭／睡觉", 0f);
        }

        private static void EnsureCharacterByName(
            string objectName,
            string displayName,
            string role,
            string realm,
            string note,
            float threat)
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

            inspectable.Configure(displayName, role, realm, note, threat);
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

        public int CountUnitsInside(WorkZone zone)
        {
            if (zone == null)
            {
                return 0;
            }

            int count = 0;
            DemoUnitController[] units = ResolvePartyUnits();
            for (int i = 0; i < units.Length; i++)
            {
                DemoUnitController unit = units[i];
                if (unit != null && zone.Contains(unit.transform.position))
                {
                    count++;
                }
            }

            return count;
        }

        public int CountWorkingInside(WorkZone zone)
        {
            if (zone == null)
            {
                return 0;
            }

            int count = 0;
            DemoUnitController[] units = ResolvePartyUnits();
            for (int i = 0; i < units.Length; i++)
            {
                DemoUnitController unit = units[i];
                if (unit == null)
                {
                    continue;
                }

                CharacterActionController actions = unit.GetComponent<CharacterActionController>();
                bool working = actions != null ? actions.IsActivelyWorking() : unit.IsActivelyWorking;
                if (working && zone.Contains(unit.transform.position))
                {
                    count++;
                }
            }

            return count;
        }

        public int CountUnitsInside(SpiritSiteZone site)
        {
            if (site == null)
            {
                return 0;
            }

            int count = 0;
            DemoUnitController[] units = ResolvePartyUnits();
            for (int i = 0; i < units.Length; i++)
            {
                DemoUnitController unit = units[i];
                if (unit != null && site.Contains(unit.transform.position))
                {
                    count++;
                }
            }

            return count;
        }

        public int CountCultivatingInside(SpiritSiteZone site)
        {
            if (site == null)
            {
                return 0;
            }

            int count = 0;
            DemoUnitController[] units = ResolvePartyUnits();
            for (int i = 0; i < units.Length; i++)
            {
                DemoUnitController unit = units[i];
                if (unit == null || !site.Contains(unit.transform.position))
                {
                    continue;
                }

                CharacterActionController actions = unit.GetComponent<CharacterActionController>();
                if (actions != null && actions.IsActivelyCultivating())
                {
                    count++;
                    continue;
                }

                UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
                if (cultivation != null && cultivation.IsCultivating)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
