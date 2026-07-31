using System.Collections.Generic;
using UnityEngine;
using XianXia.Unity.Actions;
using XianXia.Unity.Presentation;
using XianXia.Unity.Resources;
using XianXia.Unity.Time;
using XianXia.Unity.World;

namespace XianXia.Unity.Cultivation
{
    public enum CultivateAttemptResult
    {
        Started = 0,
        AlreadyCultivating = 1,
        /// <summary>已停下当前行动，但不在灵地，未能入定。</summary>
        SettledOutsideSite = 2,
        Failed = 3
    }

    /// <summary>
    /// 修炼：停下当前工作／移动／交战，就地入定（打坐）。
    /// 不是 RTS 选目标指令；须身在灵地才能真正入定涨修为。
    /// </summary>
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
                spiritSite?.SetPartyPresence(false, 0);
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

                CharacterActionController actions = unit.GetComponent<CharacterActionController>();
                bool actionCultivating = actions != null && actions.IsActivelyCultivating();

                if (actionCultivating)
                {
                    // 修为进度由 CharacterActionController 推进；此处只补暴露风险（昼夜／主管）。
                    if (!cultivation.IsCultivating)
                    {
                        cultivation.SetCultivating(true);
                    }

                    if (inSite && gameHours > 0f)
                    {
                        ApplyExposure(cultivation, unit.transform.position, gameHours);
                    }

                    continue;
                }

                if (cultivation.IsCultivating)
                {
                    // 入定中若又被下令移动／工作／攻击，或离开灵地 → 出定。
                    if (!inSite || unit.HasActiveOrder)
                    {
                        StopCultivation(unit);
                        WorldFeedbackOverlay.Ensure().SpawnFloatingText(
                            unit.transform.position,
                            "出定",
                            new Color(0.7f, 0.8f, 0.9f),
                            0.8f);
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
            SyncSpiritSitePresence();
        }

        private void SyncSpiritSitePresence()
        {
            if (spiritSite == null || units == null)
            {
                spiritSite?.SetPartyPresence(false, 0);
                return;
            }

            int cultivating = 0;
            bool anyInside = false;
            foreach (DemoUnitController unit in units)
            {
                if (unit == null || !spiritSite.Contains(unit.transform.position))
                {
                    continue;
                }

                anyInside = true;
                CharacterActionController actions = unit.GetComponent<CharacterActionController>();
                UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
                if ((actions != null && actions.IsActivelyCultivating())
                    || (cultivation != null && cultivation.IsCultivating))
                {
                    cultivating++;
                }
            }

            spiritSite.SetPartyPresence(anyInside, cultivating);
        }

        /// <summary>
        /// 停下当前事并尝试入定。始终先 CancelOrder；仅在灵地内才真正修炼。
        /// </summary>
        public CultivateAttemptResult TryStartCultivation(DemoUnitController unit)
        {
            if (unit == null || config == null)
            {
                return CultivateAttemptResult.Failed;
            }

            UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
            if (cultivation == null)
            {
                return CultivateAttemptResult.Failed;
            }

            // 收敛：无论能否入定，先停掉工作／移动／交战。
            unit.CancelOrder();

            if (cultivation.IsCultivating)
            {
                return CultivateAttemptResult.AlreadyCultivating;
            }

            bool inSite = spiritSite != null && spiritSite.Contains(unit.transform.position);
            if (!inSite)
            {
                WorldFeedbackOverlay.Ensure().SpawnFloatingText(
                    unit.transform.position,
                    "已停下·需在灵地入定",
                    new Color(0.75f, 0.8f, 0.9f),
                    1.2f);
                return CultivateAttemptResult.SettledOutsideSite;
            }

            cultivation.SetCultivating(true);
            WorldFeedbackOverlay.Ensure().SpawnFloatingText(
                unit.transform.position,
                "入定",
                new Color(0.45f, 0.85f, 1f),
                1.0f);
            return CultivateAttemptResult.Started;
        }

        public void StopCultivation(DemoUnitController unit)
        {
            if (unit == null)
            {
                return;
            }

            UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
            if (cultivation != null && cultivation.IsCultivating)
            {
                cultivation.SetCultivating(false);
            }
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
                if (TryStartCultivation(unit) == CultivateAttemptResult.Started)
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
                if (unit == null)
                {
                    continue;
                }

                UnitCultivation cultivation = unit.GetComponent<UnitCultivation>();
                bool was = cultivation != null && cultivation.IsCultivating;
                StopCultivation(unit);
                if (was)
                {
                    WorldFeedbackOverlay.Ensure().SpawnFloatingText(
                        unit.transform.position,
                        "出定",
                        new Color(0.7f, 0.8f, 0.9f),
                        0.8f);
                }
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
            WorldFeedbackOverlay.Ensure().SpawnFloatingText(
                unit.transform.position,
                "敛息",
                new Color(0.55f, 0.9f, 0.7f),
                0.9f);
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

        /// <summary>当前时段在指定位置的暴露增速（/游戏时）；只数值，不触发惩罚。</summary>
        public float GetExposureRateAt(Vector3 position)
        {
            if (config == null)
            {
                return 0f;
            }

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

            return rate;
        }

        private void ApplyExposure(UnitCultivation cultivation, Vector3 position, float gameHours)
        {
            cultivation.AddExposure(GetExposureRateAt(position) * gameHours);
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
                WorldFeedbackOverlay.Ensure().SpawnFloatingText(
                    unit.transform.position,
                    $"+{whole}敛息草",
                    new Color(0.55f, 0.9f, 0.7f));
            }

            _grassProgress[unit] = progress;
        }
    }
}
