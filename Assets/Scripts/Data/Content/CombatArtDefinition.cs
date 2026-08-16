using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    /// <summary>Content 斗技定义（映射到 Core CombatArtSpec）。</summary>
    public sealed class CombatArtDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; }
        public string Grade { get; set; }
        public string EffectSummary { get; set; }
        public double AttackBonusPercent { get; set; }
        public int DamageFlat { get; set; }
        public double DamageAttackMult { get; set; }
        public int HitCount { get; set; } = 1;
        public float CooldownSeconds { get; set; } = 2f;
        public SkillMasteryProfileDefinition Mastery { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }
}
