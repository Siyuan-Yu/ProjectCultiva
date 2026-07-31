using System.Collections.Generic;
using UnityEngine;
using XianXia.Unity.Cultivation;
using XianXia.Unity.Presentation;
using XianXia.Unity.Resources;
using XianXia.Unity.Time;

namespace XianXia.Unity.World
{
    /// <summary>
    /// 仅对处于 Working 状态（已指派工作且位于工作区）的角色产出资源。
    /// </summary>
    public sealed class WorkSystem : MonoBehaviour
    {
        private sealed class UnitProgress
        {
            public ResourceType ResourceType;
            public float Progress;
        }

        [SerializeField] private GameClock clock;
        [SerializeField] private ResourceInventory inventory;
        [SerializeField] private DemoUnitController[] units;
        [SerializeField] private WorkZone[] workZones;

        private readonly Dictionary<DemoUnitController, UnitProgress> _progressByUnit = new();

        public IReadOnlyList<WorkZone> WorkZones => workZones;

        public void Configure(
            GameClock gameClock,
            ResourceInventory resourceInventory,
            DemoUnitController[] trackedUnits,
            WorkZone[] zones)
        {
            clock = gameClock;
            inventory = resourceInventory;
            units = trackedUnits;
            workZones = zones;
            _progressByUnit.Clear();
        }

        private void Update()
        {
            if (clock == null || inventory == null || units == null || workZones == null)
            {
                return;
            }

            float gameHours = clock.DeltaGameMinutes / 60f;
            if (gameHours <= 0f)
            {
                return;
            }

            foreach (DemoUnitController unit in units)
            {
                if (unit == null || !unit.IsActivelyWorking)
                {
                    continue;
                }

                UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
                if (cultivation != null && cultivation.IsCultivating)
                {
                    continue;
                }

                WorkZone zone = unit.AssignedWorkZone;
                if (zone == null && !TryGetZone(unit.transform.position, out zone))
                {
                    continue;
                }

                if (!_progressByUnit.TryGetValue(unit, out UnitProgress state))
                {
                    state = new UnitProgress { ResourceType = zone.ResourceType };
                    _progressByUnit.Add(unit, state);
                }

                if (state.ResourceType != zone.ResourceType)
                {
                    state.ResourceType = zone.ResourceType;
                    state.Progress = 0f;
                }

                state.Progress += zone.UnitsPerGameHour * gameHours;
                int wholeUnits = Mathf.FloorToInt(state.Progress);
                if (wholeUnits <= 0)
                {
                    continue;
                }

                state.Progress -= wholeUnits;
                inventory.Add(zone.ResourceType, wholeUnits);
            }
        }

        public bool IsUnitWorking(DemoUnitController unit)
        {
            return unit != null && unit.IsActivelyWorking;
        }

        public bool TryGetZone(Vector2 worldPosition, out WorkZone zone)
        {
            if (workZones != null)
            {
                foreach (WorkZone candidate in workZones)
                {
                    if (candidate != null && candidate.Contains(worldPosition))
                    {
                        zone = candidate;
                        return true;
                    }
                }
            }

            zone = null;
            return false;
        }

        public Vector2 GetGatherPoint(WorkZone zone, int index, int total)
        {
            if (zone == null)
            {
                return Vector2.zero;
            }

            Bounds bounds = zone.Bounds;
            if (total <= 1)
            {
                return bounds.center;
            }

            int columns = Mathf.CeilToInt(Mathf.Sqrt(total));
            int row = index / columns;
            int column = index % columns;
            float spacingX = Mathf.Min(1.2f, bounds.size.x / (columns + 1));
            float spacingY = Mathf.Min(1.2f, bounds.size.y / (columns + 1));
            float x = bounds.center.x + (column - (columns - 1) * 0.5f) * spacingX;
            float y = bounds.center.y + (row - (columns - 1) * 0.5f) * spacingY;
            x = Mathf.Clamp(x, bounds.min.x + 0.25f, bounds.max.x - 0.25f);
            y = Mathf.Clamp(y, bounds.min.y + 0.25f, bounds.max.y - 0.25f);
            return new Vector2(x, y);
        }
    }
}
