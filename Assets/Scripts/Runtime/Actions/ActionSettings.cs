using UnityEngine;
using XianXia.Unity.Resources;

namespace XianXia.Unity.Actions
{
    /// <summary>
    /// 行动数值配置（不写死在角色脚本）。可挂在 Systems 上或用默认值。
    /// </summary>
    public sealed class ActionSettings : MonoBehaviour
    {
        [SerializeField] private float defaultInteractRange = 0.85f;
        [SerializeField] private float cultivateProgressPerGameHour = 80f;
        [SerializeField] private float approachTimeoutGameMinutes = 180f;
        [SerializeField] private float spiritSiteInteractPadding = 0.4f;

        public float DefaultInteractRange => Mathf.Max(0.3f, defaultInteractRange);
        public float CultivateProgressPerGameHour => Mathf.Max(0f, cultivateProgressPerGameHour);
        public float ApproachTimeoutGameMinutes => Mathf.Max(15f, approachTimeoutGameMinutes);
        public float SpiritSiteInteractPadding => Mathf.Max(0f, spiritSiteInteractPadding);

        public static ActionSettings Ensure()
        {
            ActionSettings existing = FindObjectOfType<ActionSettings>();
            if (existing != null)
            {
                return existing;
            }

            GameObject go = new("ActionSettings");
            return go.AddComponent<ActionSettings>();
        }

        public static string LabelForGather(ResourceType type)
        {
            return type switch
            {
                ResourceType.Wood => "采集木材",
                ResourceType.Herb => "采集草药",
                ResourceType.Food => "耕作",
                _ => "工作"
            };
        }

        public static ActionType GatherTypeFor(ResourceType type)
        {
            return type switch
            {
                ResourceType.Wood => ActionType.GatherWood,
                ResourceType.Herb => ActionType.GatherHerb,
                ResourceType.Food => ActionType.Farm,
                _ => ActionType.Farm
            };
        }

        public static string MovingLabelFor(ActionType type, string targetName)
        {
            return type switch
            {
                ActionType.GatherWood => string.IsNullOrEmpty(targetName) ? "前往森林" : $"前往{targetName}",
                ActionType.GatherHerb => string.IsNullOrEmpty(targetName) ? "前往草药区" : $"前往{targetName}",
                ActionType.Farm => string.IsNullOrEmpty(targetName) ? "前往农田" : $"前往{targetName}",
                ActionType.Cultivate => "前往灵地",
                ActionType.Move => "移动中",
                _ => "前往目标"
            };
        }

        public static string ActiveLabelFor(ActionType type)
        {
            return type switch
            {
                ActionType.GatherWood => "采集木材",
                ActionType.GatherHerb => "采集草药",
                ActionType.Farm => "耕作",
                ActionType.Cultivate => "修炼中",
                ActionType.Move => "移动中",
                _ => "执行中"
            };
        }
    }
}
