using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Results;
using XianXia.Data.Content;

namespace XianXia.Data.Combat
{
    public static class CombatArtMapper
    {
        public static Result<CombatArtSpec> ToSpec(CombatArtDefinition definition)
        {
            if (definition == null)
                return Result.Fail<CombatArtSpec>(ErrorCode.InvalidArgument, "CombatArtDefinition is null.");

            var spec = new CombatArtSpec
            {
                Id = definition.Id,
                Name = definition.Name ?? string.Empty,
                Grade = definition.Grade ?? string.Empty,
                EffectSummary = definition.EffectSummary ?? string.Empty,
                AttackBonusPercent = definition.AttackBonusPercent,
                DamageFlat = definition.DamageFlat,
                DamageAttackMult = definition.DamageAttackMult,
                HitCount = definition.HitCount < 1 ? 1 : definition.HitCount,
                CooldownSeconds = definition.CooldownSeconds <= 0f ? 2f : definition.CooldownSeconds
            };
            if (definition.Mastery != null)
                spec.Mastery = SkillMasteryProfileParser.ToCore(definition.Mastery);
            else
                spec.Mastery = SkillMasteryLookup.EnsureOrDefaultArt(spec);
            return Result.Ok(spec);
        }
    }
}
