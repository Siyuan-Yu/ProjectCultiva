using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Content-only cultivation / manual template. Modifier grants are config for Core to apply later.
    /// Data must not compute Final attributes.
    /// </summary>
    public sealed class CultivationDefinition
    {
        public DefinitionId Id { get; set; }
        /// <summary>Author-facing display name (not full localization).</summary>
        public string Name { get; set; }
        public string DisplayNameKey { get; set; }
        public string NameKey { get; set; }
        /// <summary>Required realm label (Mortal / 凡人 / 炼气).</summary>
        public string RequiredRealm { get; set; }
        /// <summary>品阶，如黄阶中级。</summary>
        public string Grade { get; set; }
        /// <summary>效果摘要文案。</summary>
        public string EffectSummary { get; set; }
        /// <summary>Progress gained per cultivate ActionClock tick. Interpreted by Core.</summary>
        public int CultivationSpeed { get; set; }
        /// <summary>Progress threshold for Mortal → QiRefining breakthrough. Interpreted by Core.</summary>
        public int BreakthroughProgress { get; set; }
        public List<ModifierGrantDefinition> GrantedModifiers { get; set; } = new List<ModifierGrantDefinition>();
        public SkillMasteryProfileDefinition Mastery { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }

    /// <summary>
    /// Serializable modifier grant config. Interpreted by Core AttributePipe, not by Data.
    /// </summary>
    public sealed class ModifierGrantDefinition
    {
        public string TargetAttribute { get; set; }
        public string Operation { get; set; }
        /// <summary>Fixed＝加算；Percentage＝比例（0.06＝+6%）。</summary>
        public double Value { get; set; }
        public string StackingKey { get; set; }
    }
}
