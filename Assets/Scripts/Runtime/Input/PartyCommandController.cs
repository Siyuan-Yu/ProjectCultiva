using System.Collections.Generic;
using UnityEngine;
using XianXia.Unity.Cultivation;
using XianXia.Unity.Presentation;
using XianXia.Unity.World;

namespace XianXia.Unity.Input
{
    /// <summary>
    /// RTS 指令：左键选择；右键点工作区下达工作；右键点空地自由移动。
    /// </summary>
    public sealed class PartyCommandController : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private float formationSpacing = 1.25f;
        [SerializeField] private WorkSystem workSystem;

        private readonly List<DemoUnitController> _selectedUnits = new();

        public IReadOnlyList<DemoUnitController> SelectedUnits => _selectedUnits;

        public void Configure(Camera camera, WorkSystem work)
        {
            worldCamera = camera;
            workSystem = work;
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
        }

        private void Update()
        {
            if (worldCamera == null)
            {
                return;
            }

            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                SelectAtPointer();
            }

            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                CommandSelection();
            }
        }

        private void SelectAtPointer()
        {
            Vector2 worldPoint = worldCamera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            Collider2D hit = Physics2D.OverlapPoint(worldPoint);
            DemoUnitController unit = hit == null ? null : hit.GetComponentInParent<DemoUnitController>();
            bool additive = UnityEngine.Input.GetKey(KeyCode.LeftShift)
                || UnityEngine.Input.GetKey(KeyCode.RightShift);

            if (!additive)
            {
                ClearSelection();
            }

            if (unit == null)
            {
                return;
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

        private void CommandSelection()
        {
            if (_selectedUnits.Count == 0)
            {
                return;
            }

            Vector2 worldPoint = worldCamera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            if (workSystem != null && workSystem.TryGetZone(worldPoint, out WorkZone zone))
            {
                AssignWork(zone);
                return;
            }

            MoveSelection(worldPoint);
        }

        private void AssignWork(WorkZone zone)
        {
            int total = 0;
            for (int i = 0; i < _selectedUnits.Count; i++)
            {
                if (_selectedUnits[i] != null)
                {
                    total++;
                }
            }

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

                Vector2 gatherPoint = workSystem.GetGatherPoint(zone, index, total);
                unit.AssignWork(zone, gatherPoint);
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
            foreach (DemoUnitController unit in _selectedUnits)
            {
                if (unit != null)
                {
                    unit.SetSelected(false);
                }
            }

            _selectedUnits.Clear();
        }
    }
}
