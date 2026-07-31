using System.Collections.Generic;
using UnityEngine;
using XianXia.Unity.Presentation;
using XianXia.Unity.Resources;
using XianXia.Unity.Time;
using XianXia.Unity.World;

namespace XianXia.Unity.Cultivation
{
    public sealed class CultivationSystem : MonoBehaviour
    {
        [SerializeField] private GameClock clock;
        [SerializeField] private ResourceInventory inventory;
        [SerializeField] private SpiritSiteZone spiritSite;
        [SerializeField] private CultivationConfig config;
        [SerializeField] private Transform supervisorAnchor;
        [SerializeField] private DemoUnitController[] units;

        private readonly Dictionary<DemoUnitController, float> _grassProgress = new();

        public SpiritSiteZone SpiritSite => spiritSite;
        public CultivationConfig Config => config;
        public bool AnyUnitInSpiritSite { get; private set; }
        public bool IsNight => clock != null && config != null && config.IsNightHour(clock.Hour);

        public void Configure(
            GameClock gameClock,
            ResourceInventory resourceInventory,
            SpiritSiteZone site,
            CultivationConfig cultivationConfig,
            Transform supervisor,
            DemoUnitController[] partyUnits)
        {
            clock = gameClock;
            inventory = resourceInventory;
            spiritSite = site;
            config = cultivationConfig;
            supervisorAnchor = supervisor;
            units = partyUnits;
            _grassProgress.Clear();
        }

        private void Update()
        {
            if (clock == null || config == null || units == null)
            {
                AnyUnitInSpiritSite = false;
                return;
            }

            float gameHours = clock.DeltaGameMinutes / 60f;
            bool anyInSite = false;

            foreach (DemoUnitController unit in units)
            {
                if (unit == null)
                {
                    continue;
                }

                UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
                if (cultivation == null)
                {
                    continue;
                }

                bool inSite = spiritSite != null && spiritSite.Contains(unit.transform.position);
                if (inSite)
                {
                    anyInSite = true;
                }

                if (cultivation.IsCultivating)
                {
                    if (!inSite || unit.HasActiveOrder)
                    {
                        StopCultivation(unit);
                        continue;
                    }

                    if (gameHours > 0f)
                    {
                        cultivation.AddProgress(config.ProgressPerGameHour * gameHours);
                        ApplyExposure(cultivation, unit.transform.position, gameHours);
                    }
                }
                else if (inSite && inventory != null && gameHours > 0f)
                {
                    GatherConcealGrass(unit, gameHours);
                }
            }

            AnyUnitInSpiritSite = anyInSite;
        }

        public bool TryStartCultivation(DemoUnitController unit)
        {
            if (unit == null || spiritSite == null || config == null)
            {
                return false;
            }

            UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
            if (cultivation == null || cultivation.IsCultivating)
            {
                return false;
            }

            if (!spiritSite.Contains(unit.transform.position))
            {
                return false;
            }

            unit.CancelOrder();
            cultivation.SetCultivating(true);
            return true;
        }

        public void StopCultivation(DemoUnitController unit)
        {
            if (unit == null)
            {
                return;
            }

            UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
            cultivation?.SetCultivating(false);
        }

        public int StartCultivationForUnits(IReadOnlyList<DemoUnitController> selected)
        {
            int started = 0;
            if (selected == null)
            {
                return 0;
            }

            foreach (DemoUnitController unit in selected)
            {
                if (TryStartCultivation(unit))
                {
                    started++;
                }
            }

            return started;
        }

        public void StopCultivationForUnits(IReadOnlyList<DemoUnitController> selected)
        {
            if (selected == null)
            {
                return;
            }

            foreach (DemoUnitController unit in selected)
            {
                StopCultivation(unit);
            }
        }

        public bool TryUseConcealGrass(DemoUnitController unit)
        {
            if (unit == null || inventory == null || config == null)
            {
                return false;
            }

            UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
            if (cultivation == null)
            {
                return false;
            }

            if (!inventory.TrySpend(ResourceType.ConcealGrass, 1))
            {
                return false;
            }

            cultivation.ReduceExposure(config.ConcealGrassExposureReduction);
            return true;
        }

        public int UseConcealGrassForUnits(IReadOnlyList<DemoUnitController> selected)
        {
            int used = 0;
            if (selected == null)
            {
                return 0;
            }

            foreach (DemoUnitController unit in selected)
            {
                if (TryUseConcealGrass(unit))
                {
                    used++;
                }
            }

            return used;
        }

        public bool IsUnitInSpiritSite(DemoUnitController unit)
        {
            return unit != null
                && spiritSite != null
                && spiritSite.Contains(unit.transform.position);
        }

        private void ApplyExposure(UnitCultivation cultivation, Vector3 position, float gameHours)
        {
            float rate = IsNight
                ? config.NightExposurePerGameHour
                : config.DayExposurePerGameHour;

            if (supervisorAnchor != null)
            {
                float distance = Vector2.Distance(position, supervisorAnchor.position);
                if (distance <= config.SupervisorProximityRadius)
                {
                    rate += config.NearSupervisorExtraExposurePerGameHour;
                }
            }

            cultivation.AddExposure(rate * gameHours);
        }

        private void GatherConcealGrass(DemoUnitController unit, float gameHours)
        {
            if (!_grassProgress.TryGetValue(unit, out float progress))
            {
                progress = 0f;
            }

            progress += spiritSite.ConcealGrassPerGameHour * gameHours;
            int whole = Mathf.FloorToInt(progress);
            if (whole > 0)
            {
                progress -= whole;
                inventory.Add(ResourceType.ConcealGrass, whole);
            }

            _grassProgress[unit] = progress;
        }
    }
}
