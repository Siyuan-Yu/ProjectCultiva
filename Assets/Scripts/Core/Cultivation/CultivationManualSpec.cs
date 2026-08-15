using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Cultivation
{
    /// <summary>
    /// Core-side manual grant payload (mapped from Content CultivationDefinition).
    /// </summary>
    public sealed class CultivationManualSpec
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; } = string.Empty;
        /// <summary>品阶展示，如「黄阶中级」。</summary>
        public string Grade { get; set; } = string.Empty;
        /// <summary>效果摘要，供背包／面板展示。</summary>
        public string EffectSummary { get; set; } = string.Empty;
        public string RequiredRealm { get; set; }
        public int CultivationSpeed { get; set; }
        public int BreakthroughProgress { get; set; }
        public List<ModifierGrantSpec> GrantedModifiers { get; set; } = new List<ModifierGrantSpec>();
    }

    public sealed class ModifierGrantSpec
    {
        public AttributeId TargetAttribute { get; set; }
        public ModifierOperation Operation { get; set; }
        public double Value { get; set; }
        public string StackingKey { get; set; }
    }
}
