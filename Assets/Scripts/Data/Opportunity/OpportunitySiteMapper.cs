using XianXia.Core.Domain.Ids;
using XianXia.Core.Opportunity;
using XianXia.Core.Results;
using XianXia.Data.Content;

namespace XianXia.Data.Opportunity
{
    public static class OpportunitySiteMapper
    {
        public static Result<OpportunitySite> ToRuntime(OpportunitySiteDefinition definition)
        {
            if (definition == null)
                return Result.Fail<OpportunitySite>(ErrorCode.InvalidArgument, "OpportunitySiteDefinition is null.");

            DefinitionId? manualId = null;
            if (!string.IsNullOrEmpty(definition.OfferedManualId))
            {
                if (!DefinitionId.TryParse(definition.OfferedManualId, out var parsed))
                    return Result.Fail<OpportunitySite>(
                        ErrorCode.InvalidDefinitionId,
                        "OfferedManualId invalid.",
                        definition.OfferedManualId);
                manualId = parsed;
            }

            return Result.Ok(new OpportunitySite(
                definition.Id,
                definition.AllowsCultivation,
                manualId,
                definition.NameKey ?? string.Empty,
                definition.Description ?? definition.Name ?? string.Empty));
        }
    }
}
