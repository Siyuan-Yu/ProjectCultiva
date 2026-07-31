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

            zone.EnsureDefaultSpots(Mathf.Max(3, total));
            IReadOnlyList<WorkSpot> spots = zone.Spots;
            if (spots != null && spots.Count > 0)
            {
                WorkSpot spot = spots[index % spots.Count];
                return spot != null ? spot.Position : (Vector2)zone.Bounds.center;
            }

            return zone.Bounds.center;
        }

        public WorkSpot GetSpot(WorkZone zone, int index)
        {
            if (zone == null)
            {
                return null;
            }

            zone.EnsureDefaultSpots(4);
            IReadOnlyList<WorkSpot> spots = zone.Spots;
            if (spots == null || spots.Count == 0)
            {
                return null;
            }

            return spots[index % spots.Count];
        }

        public bool TryGetSpot(Vector2 worldPosition, out WorkSpot spot)
        {
            if (workZones != null)
            {
                for (int i = 0; i < workZones.Length; i++)
                {
                    WorkZone zone = workZones[i];
                    if (zone == null)
                    {
                        continue;
                    }

                    zone.EnsureDefaultSpots(3);
                    spot = zone.FindSpotContaining(worldPosition);
                    if (spot != null)
                    {
                        return true;
                    }
                }
            }

            spot = null;
            return false;
        }

        public void EnsureAllZoneSpots()
        {
            if (workZones == null)
            {
                return;
            }

            for (int i = 0; i < workZones.Length; i++)
            {
                WorkZone zone = workZones[i];
                if (zone == null)
                {
                    continue;
                }

                int count = zone.ResourceType switch
                {
                    ResourceType.Food => 5,
                    ResourceType.Wood => 4,
                    ResourceType.Herb => 3,
                    _ => 3
                };
                zone.EnsureDefaultSpots(count);
            }
        }
    }
}
