using System.Collections.Generic;
using UnityEngine;
using XianXia.Unity.Cultivation;
using XianXia.Unity.Presentation;
using XianXia.Unity.Resources;
using XianXia.Unity.Time;

namespace XianXia.Unity.World
{
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
                if (unit == null)
                {
                    continue;
                }

                UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
                if (cultivation != null && cultivation.IsCultivating)
                {
                    continue;
                }

                if (!TryGetZone(unit.transform.position, out WorkZone zone))
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
            if (unit == null)
            {
                return false;
            }

            UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
            if (cultivation != null && cultivation.IsCultivating)
            {
                return false;
            }

            return TryGetZone(unit.transform.position, out _);
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
    }
}
