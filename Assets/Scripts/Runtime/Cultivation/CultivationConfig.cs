using UnityEngine;

namespace XianXia.Unity.Cultivation
{
    [CreateAssetMenu(
        menuName = "XianXia/Cultivation/Cultivation Config",
        fileName = "Cultivation_Default")]
    public sealed class CultivationConfig : ScriptableObject
    {
        [SerializeField] private float progressPerGameHour = 80f;
        [SerializeField] private float nightExposurePerGameHour = 0.5f;
        [SerializeField] private float dayExposurePerGameHour = 3f;
        [SerializeField] private float nearSupervisorExtraExposurePerGameHour = 2f;
        [SerializeField] private float supervisorProximityRadius = 8f;
        [SerializeField] private float concealGrassExposureReduction = 15f;
        [SerializeField] private int nightStartHour = 19;
        [SerializeField] private int nightEndHour = 6;

        public float ProgressPerGameHour => Mathf.Max(0f, progressPerGameHour);
        public float NightExposurePerGameHour => Mathf.Max(0f, nightExposurePerGameHour);
        public float DayExposurePerGameHour => Mathf.Max(0f, dayExposurePerGameHour);
        public float NearSupervisorExtraExposurePerGameHour =>
            Mathf.Max(0f, nearSupervisorExtraExposurePerGameHour);
        public float SupervisorProximityRadius => Mathf.Max(0.5f, supervisorProximityRadius);
        public float ConcealGrassExposureReduction => Mathf.Max(0f, concealGrassExposureReduction);
        public int NightStartHour => Mathf.Clamp(nightStartHour, 0, 23);
        public int NightEndHour => Mathf.Clamp(nightEndHour, 0, 23);

        public bool IsNightHour(int hour)
        {
            hour = Mathf.Clamp(hour, 0, 23);
            if (NightStartHour == NightEndHour)
            {
                return true;
            }

            if (NightStartHour < NightEndHour)
            {
                return hour >= NightStartHour && hour < NightEndHour;
            }

            return hour >= NightStartHour || hour < NightEndHour;
        }

        public static CultivationConfig CreateDefault()
        {
            return CreateInstance<CultivationConfig>();
        }
    }
}
