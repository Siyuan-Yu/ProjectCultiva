using UnityEngine;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 树／墙等带血量的地图物。血量归零后销毁；粗木由 <see cref="HostDestructibleAssault"/> 入包。
    /// </summary>
    public sealed class HostMapDestructible : MonoBehaviour
    {
        public const string RoughWoodItemId = "base:resource_rough_wood";

        [SerializeField] string placementId;
        [SerializeField] string kind;
        [SerializeField] string displayName;
        [SerializeField] int maxHp = 40;
        [SerializeField] int currentHp = 40;
        [SerializeField] int woodYield;
        [SerializeField] bool destroyed;

        public string PlacementId => placementId;
        public string Kind => kind;
        public string DisplayName =>
            string.IsNullOrEmpty(displayName) ? kind : displayName;
        public int MaxHp => maxHp;
        public int CurrentHp => currentHp;
        public int WoodYield => woodYield;
        public bool IsDestroyed => destroyed;
        public bool IsTree =>
            string.Equals(kind, "treeS", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "treeM", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "treeL", System.StringComparison.OrdinalIgnoreCase);
        public bool IsWall =>
            string.Equals(kind, "wall", System.StringComparison.OrdinalIgnoreCase);

        public void Configure(
            string placementIdValue,
            string kindValue,
            string label,
            int maxHpValue,
            int woodYieldValue)
        {
            placementId = placementIdValue ?? string.Empty;
            kind = kindValue ?? string.Empty;
            displayName = string.IsNullOrEmpty(label) ? ResolveDefaultName(kind) : label;
            maxHp = maxHpValue < 1 ? 1 : maxHpValue;
            currentHp = maxHp;
            woodYield = woodYieldValue < 0 ? 0 : woodYieldValue;
            destroyed = false;
            HostMapObjectRegistry.Register(this);
        }

        /// <summary>砍伐结算用：即使序列化产量为 0，也按 kind 回落默认产量。</summary>
        public int ResolveWoodYield()
        {
            if (woodYield > 0)
                return woodYield;
            var fromKind = DefaultWoodYield(kind);
            if (fromKind > 0)
                return fromKind;
            // 无 kind 时按显示名兜底
            if (!string.IsNullOrEmpty(displayName))
            {
                if (displayName.IndexOf("大树", System.StringComparison.Ordinal) >= 0)
                    return DefaultWoodYield("treeL");
                if (displayName.IndexOf("中树", System.StringComparison.Ordinal) >= 0)
                    return DefaultWoodYield("treeM");
                if (displayName.IndexOf("小树", System.StringComparison.Ordinal) >= 0)
                    return DefaultWoodYield("treeS");
            }

            return 0;
        }

        public static int DefaultMaxHp(string kindValue)
        {
            switch ((kindValue ?? string.Empty).ToLowerInvariant())
            {
                case "trees": return 30;
                case "treem": return 55;
                case "treel": return 90;
                case "wall": return 80;
                default: return 40;
            }
        }

        public static int DefaultWoodYield(string kindValue)
        {
            switch ((kindValue ?? string.Empty).ToLowerInvariant())
            {
                case "trees": return 3;
                case "treem": return 10;
                case "treel": return 40;
                default: return 0;
            }
        }

        static string ResolveDefaultName(string kindValue)
        {
            switch ((kindValue ?? string.Empty).ToLowerInvariant())
            {
                case "trees": return "小树";
                case "treem": return "中树";
                case "treel": return "大树";
                case "wall": return "墙";
                default: return kindValue ?? "物体";
            }
        }

        /// <summary>造成伤害；归零时销毁。掉落由砍伐组件结算。</summary>
        public int ApplyDamage(int rawDamage, out bool justDestroyed)
        {
            justDestroyed = false;
            if (destroyed || rawDamage <= 0)
                return 0;

            var dmg = rawDamage < 1 ? 1 : rawDamage;
            var before = currentHp;
            currentHp -= dmg;
            if (currentHp > 0)
                return before - currentHp;

            currentHp = 0;
            destroyed = true;
            justDestroyed = true;
            HostMapObjectRegistry.Unregister(this);
            Destroy(gameObject);
            return before;
        }

        void OnDestroy()
        {
            HostMapObjectRegistry.Unregister(this);
        }
    }
}
