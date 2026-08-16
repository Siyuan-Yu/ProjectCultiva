using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Combat
{
    /// <summary>斗技／技能规格（session 注册；不进 Snapshot v1）。</summary>
    public sealed class CombatArtSpec
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Grade { get; set; } = string.Empty;
        public string EffectSummary { get; set; } = string.Empty;

        /// <summary>装备后普攻被动加成（如 0.12＝+12%）。主动技通常为 0。入门档缺省。</summary>
        public double AttackBonusPercent { get; set; }

        public int DamageFlat { get; set; }

        /// <summary>
        /// 主动技：单段伤害＝攻击力 × 该倍率（2＝200%，5＝500%）。入门档缺省。
        /// ≤0 表示非主动伤害技。
        /// </summary>
        public double DamageAttackMult { get; set; }

        /// <summary>主动技连击段数（裂爪击＝3，开山拳＝1）。</summary>
        public int HitCount { get; set; } = 1;

        /// <summary>释放冷却（秒，Host 表现层）。</summary>
        public float CooldownSeconds { get; set; } = 2f;

        /// <summary>熟练档绝对值效果／突破表。</summary>
        public SkillMasteryProfile Mastery { get; set; }

        public bool IsActiveSkill => DamageAttackMult > 0.0;
    }
}
