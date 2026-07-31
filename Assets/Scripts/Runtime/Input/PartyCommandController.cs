using System.Collections.Generic;
using UnityEngine;
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

    /// <summary>
    /// RTS 指令：框选／点选；点建筑／工作区／灵地查看详情；右键工作区下达工作，右键空地移动。
    /// </summary>
    public sealed class PartyCommandController : MonoBehaviour
    {
        private const float BoxSelectPixelThreshold = 8f;

        [SerializeField] private Camera worldCamera;
        [SerializeField] private float formationSpacing = 1.25f;
        [SerializeField] private WorkSystem workSystem;
        [SerializeField] private DemoUnitController[] partyUnits;

        private readonly List<DemoUnitController> _selectedUnits = new();
        private readonly WorldInspection _inspection = new();

        private bool _leftDragActive;
        private bool _boxSelecting;
        private bool _pointerStartedOverUi;
        private bool _workTargeting;
        private Vector2 _dragStartScreen;
        private Vector2 _dragCurrentScreen;

        public IReadOnlyList<DemoUnitController> SelectedUnits => _selectedUnits;
        public WorldInspection Inspection => _inspection;
        public bool IsBoxSelecting => _boxSelecting;
        public bool IsWorkTargeting => _workTargeting;

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
        }

        private void Update()
        {
            if (worldCamera == null)
            {
                return;
            }

            HandleLeftMouse();

            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                Vector2 screen = UnityEngine.Input.mousePosition;
                if (IsPointerOverUi == null || !IsPointerOverUi(screen))
                {
                    if (_workTargeting)
                    {
                        CommandWorkAtPointer();
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

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) && _workTargeting)
            {
                CancelWorkTargeting();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.S))
            {
                StopSelectedOrders();
                CancelWorkTargeting();
            }
        }

        /// <summary>
        /// 显式「工作」指令：已在工位旁则开工；否则进入选点模式，再点工位。
        /// </summary>
        public void BeginWorkCommand()
        {
            if (_selectedUnits.Count == 0)
            {
                return;
            }

            workSystem?.EnsureAllZoneSpots();
            int started = 0;
            for (int i = 0; i < _selectedUnits.Count; i++)
            {
                DemoUnitController unit = _selectedUnits[i];
                if (unit == null)
                {
                    continue;
                }

                WorkSpot near = FindNearestSpotTo(unit.transform.position);
                if (near != null && near.IsInRange(unit.transform.position))
                {
                    UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
                    cultivation?.SetCultivating(false);
                    unit.StartWorkAt(near);
                    started++;
                }
            }

            if (started == _selectedUnits.Count)
            {
                _workTargeting = false;
                return;
            }

            _workTargeting = true;
        }

        public void CancelWorkTargeting()
        {
            _workTargeting = false;
        }

        public void StopSelectedOrders()
        {
            CancelWorkTargeting();
            for (int i = 0; i < _selectedUnits.Count; i++)
            {
                DemoUnitController unit = _selectedUnits[i];
                if (unit == null)
                {
                    continue;
                }

                unit.CancelOrder();
                UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
                cultivation?.SetCultivating(false);
            }
        }

        private void CommandWorkAtPointer()
        {
            if (_selectedUnits.Count == 0)
            {
                _workTargeting = false;
                return;
            }

            Vector2 worldPoint = worldCamera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            WorkSpot spot = ResolveWorkSpot(worldPoint);
            if (spot == null)
            {
                return;
            }

            AssignWorkToSpot(spot);
            _workTargeting = false;
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

        private WorkSpot FindNearestSpotTo(Vector2 worldPosition)
        {
            if (workSystem == null || workSystem.WorkZones == null)
            {
                return null;
            }

            workSystem.EnsureAllZoneSpots();
            WorkSpot best = null;
            float bestDist = float.MaxValue;
            IReadOnlyList<WorkZone> zones = workSystem.WorkZones;
            for (int i = 0; i < zones.Count; i++)
            {
                WorkZone zone = zones[i];
                if (zone == null)
                {
                    continue;
                }

                WorkSpot spot = zone.FindNearestSpot(worldPosition);
                if (spot == null)
                {
                    continue;
                }

                float d = (spot.Position - worldPosition).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = spot;
                }
            }

            return best;
        }

        private void CommandSelection()
        {
            if (_selectedUnits.Count == 0)
            {
                return;
            }

            Vector2 worldPoint = worldCamera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            WorkSpot spot = ResolveWorkSpot(worldPoint);
            if (spot != null)
            {
                MoveSelectionToSpot(spot);
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

                UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
                cultivation?.SetCultivating(false);

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

                unit.StartWorkAt(target);
                index++;
            }
        }

        private void MoveSelectionToSpot(WorkSpot spot)
        {
            int index = 0;
            for (int i = 0; i < _selectedUnits.Count; i++)
            {
                DemoUnitController unit = _selectedUnits[i];
                if (unit == null)
                {
                    continue;
                }

                UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
                cultivation?.SetCultivating(false);

                WorkSpot target = spot;
                if (_selectedUnits.Count > 1 && spot.OwnerZone != null && workSystem != null)
                {
                    WorkSpot alt = workSystem.GetSpot(spot.OwnerZone, index);
                    if (alt != null)
                    {
                        target = alt;
                    }
                }

                unit.MoveToWorkSpot(target);
                index++;
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

                UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
                cultivation?.SetCultivating(false);

                int row = i / columns;
                int column = i % columns;
                float x = (column - (columns - 1) * 0.5f) * formationSpacing;
                float y = -row * formationSpacing;
                unit.MoveTo(center + new Vector2(x, y));
            }
        }

        private void ClearSelection()
        {
            CancelWorkTargeting();
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
            if (!_boxSelecting)
            {
                return;
            }

            Rect screenRect = CurrentBoxScreenRect;
            float guiY = Screen.height - screenRect.yMax;
            Rect guiRect = new(screenRect.xMin, guiY, screenRect.width, screenRect.height);

            Color previous = GUI.color;
            GUI.color = new Color(0.3f, 0.85f, 0.35f, 0.18f);
            GUI.DrawTexture(guiRect, Texture2D.whiteTexture);
            GUI.color = new Color(0.35f, 0.95f, 0.4f, 0.9f);
            DrawRectBorder(guiRect, 2f);
            GUI.color = previous;
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
                else if (_workTargeting)
                {
                    CommandWorkAtPointer();
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

                if (unit.IsActivelyWorking && unit.AssignedWorkZone == zone)
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
