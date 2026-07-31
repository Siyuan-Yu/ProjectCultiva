using UnityEngine;

namespace XianXia.Unity.Cultivation
{
    /// <summary>
    /// 单角色修为与暴露风险。本阶段只做数值与状态，不接突破／惩罚。
    /// </summary>
    public sealed class UnitCultivation : MonoBehaviour
    {
        public const float MaxProgress = 1000f;
        public const float MaxExposure = 100f;

        [SerializeField] private float cultivationProgress;
        [SerializeField] private float exposureRisk;
        [SerializeField] private bool isCultivating;

        public float CultivationProgress => cultivationProgress;
        public float ExposureRisk => exposureRisk;
        public bool IsCultivating => isCultivating;

        public void Configure(float progress, float risk)
        {
            cultivationProgress = Mathf.Clamp(progress, 0f, MaxProgress);
            exposureRisk = Mathf.Clamp(risk, 0f, MaxExposure);
            isCultivating = false;
        }

        public void SetCultivating(bool cultivating)
        {
            isCultivating = cultivating;
        }

        public void AddProgress(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            cultivationProgress = Mathf.Min(MaxProgress, cultivationProgress + amount);
        }

        public void AddExposure(float amount)
        {
            if (Mathf.Approximately(amount, 0f))
            {
                return;
            }

            exposureRisk = Mathf.Clamp(exposureRisk + amount, 0f, MaxExposure);
        }

        public void ReduceExposure(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            exposureRisk = Mathf.Max(0f, exposureRisk - amount);
        }
    }
}
