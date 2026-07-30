using System.Collections.Generic;
using UnityEngine;
using XianXia.Unity.Presentation;

namespace XianXia.Unity.Input
{
    public sealed class PartyCommandController : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private float formationSpacing = 1.25f;

        private readonly List<DemoUnitController> _selectedUnits = new();

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
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
                MoveSelection();
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

        private void MoveSelection()
        {
            if (_selectedUnits.Count == 0)
            {
                return;
            }

            Vector2 center = worldCamera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            int columns = Mathf.CeilToInt(Mathf.Sqrt(_selectedUnits.Count));

            for (int i = 0; i < _selectedUnits.Count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                float x = (column - (columns - 1) * 0.5f) * formationSpacing;
                float y = -row * formationSpacing;
                _selectedUnits[i].MoveTo(center + new Vector2(x, y));
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
