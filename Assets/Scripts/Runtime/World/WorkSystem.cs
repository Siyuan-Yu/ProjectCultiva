using System.Collections.Generic;
using UnityEngine;
using XianXia.Unity.Actions;
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

                // Milestone 3.5：有统一行动控制器时，由 CharacterActionController 负责产出，避免双计。
                CharacterActionController actions = unit.GetComponent<CharacterActionController>();
                if (actions != null)
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
                WorldFeedbackOverlay.Ensure().SpawnFloatingText(
                    unit.transform.position,
                    $"+{wholeUnits}{ResourceShortName(zone.ResourceType)}",
                    ResourceColor(zone.ResourceType));
            }
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

        public bool IsUnitWorking(DemoUnitController unit)
        {
            if (unit == null)
            {
                return false;
            }

            CharacterActionController actions = unit.GetComponent<CharacterActionController>();
            if (actions != null)
            {
                return actions.IsActivelyWorking();
            }

            return unit.IsActivelyWorking;
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

        public void SetWorkTargetingVisuals(bool targetingActive, WorkSpot hoveredSpot)
        {
            EnsureAllZoneSpots();
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

                IReadOnlyList<WorkSpot> spots = zone.Spots;
                for (int s = 0; s < spots.Count; s++)
                {
                    WorkSpot spot = spots[s];
                    if (spot == null)
                    {
                        continue;
                    }

                    spot.SetTargetingVisual(targetingActive, targetingActive && spot == hoveredSpot);
                }
            }
        }
    }
}
