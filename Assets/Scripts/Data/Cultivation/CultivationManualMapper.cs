using System;
using XianXia.Core.Attributes;
using XianXia.Core.Cultivation;
using XianXia.Core.Results;
using XianXia.Data.Content;

namespace XianXia.Data.Cultivation
{
    /// <summary>
    /// Maps Content CultivationDefinition → Core CultivationManualSpec. No Final calculation.
    /// </summary>
    public static class CultivationManualMapper
    {
        public static Result<CultivationManualSpec> ToManualSpec(CultivationDefinition definition)
        {
            if (definition == null)
                return Result.Fail<CultivationManualSpec>(ErrorCode.InvalidArgument, "CultivationDefinition is null.");

            var spec = new CultivationManualSpec
            {
                Id = definition.Id,
                RequiredRealm = definition.RequiredRealm ?? string.Empty,
                CultivationSpeed = definition.CultivationSpeed,
                BreakthroughProgress = definition.BreakthroughProgress
            };

            if (definition.GrantedModifiers != null)
            {
                foreach (var grant in definition.GrantedModifiers)
                {
                    if (!Enum.TryParse(grant.TargetAttribute, false, out AttributeId attr))
                        return Result.Fail<CultivationManualSpec>(
                            ErrorCode.InvalidArgument,
                            "Illegal targetAttribute.",
                            grant.TargetAttribute);

                    if (!Enum.TryParse(grant.Operation, false, out ModifierOperation op))
                        return Result.Fail<CultivationManualSpec>(
                            ErrorCode.InvalidArgument,
                            "Illegal modifier operation.",
                            grant.Operation);

                    spec.GrantedModifiers.Add(new ModifierGrantSpec
                    {
                        TargetAttribute = attr,
                        Operation = op,
                        Value = grant.Value,
                        StackingKey = grant.StackingKey ?? string.Empty
                    });
                }
            }

            return Result.Ok(spec);
        }
    }
}
