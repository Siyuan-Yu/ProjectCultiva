using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Settlement;
using XianXia.Core.Simulation;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>Maps Content settlement／facility definitions into Core SettlementBoard.</summary>
    public static class SettlementBootstrap
    {
        public static Result ApplyOpening(
            SimulationWorld world,
            DefinitionRegistry registry,
            OpeningScenarioDefinition scenario,
            GameStartLookup lookup)
        {
            if (world == null || registry == null || scenario == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Settlement bootstrap args null.");

            if (string.IsNullOrWhiteSpace(scenario.OpeningSettlementId))
                return Result.Success();

            var parsed = DefinitionId.Parse(scenario.OpeningSettlementId);
            if (parsed.IsFailure)
                return Result.Failure(parsed.Error);

            if (!registry.TryGetSettlement(parsed.Value, out var def))
            {
                return Result.Failure(
                    ErrorCode.NotFound,
                    "Opening settlement definition missing.",
                    scenario.OpeningSettlementId);
            }

            var state = new SettlementState
            {
                Id = def.Id.ToString(),
                Name = string.IsNullOrEmpty(def.Name) ? def.Id.ToString() : def.Name
            };

            foreach (var stock in def.InitialStock)
            {
                if (stock == null || string.IsNullOrWhiteSpace(stock.ResourceId) || stock.Amount <= 0)
                    continue;
                state.AddStock(stock.ResourceId, stock.Amount);
            }

            foreach (var facilityIdText in def.FacilityIds)
            {
                var fid = DefinitionId.Parse(facilityIdText);
                if (fid.IsFailure)
                    return Result.Failure(fid.Error);
                if (!registry.TryGetFacility(fid.Value, out var facilityDef))
                {
                    return Result.Failure(
                        ErrorCode.NotFound,
                        "Facility definition missing for settlement.",
                        facilityIdText);
                }

                state.Facilities.Add(new FacilityRuntime
                {
                    FacilityId = facilityDef.Id.ToString(),
                    Name = facilityDef.Name ?? string.Empty,
                    LaborResourceId = facilityDef.LaborResourceId ?? string.Empty,
                    LaborAmountPerWorker = facilityDef.LaborAmountPerWorker,
                    GatherResourceId = facilityDef.GatherResourceId ?? string.Empty,
                    GatherAmountPerWorker = facilityDef.GatherAmountPerWorker,
                    CultivateProgressBonusPerWorker = facilityDef.CultivateProgressBonusPerWorker
                });
            }

            world.Settlements.Register(state, asPrimary: true);

            var service = new SettlementService();
            foreach (var spawn in scenario.Spawns)
            {
                if (string.IsNullOrWhiteSpace(spawn.WorkRole))
                    continue;
                if (!TryParseWorkRole(spawn.WorkRole, out var role))
                {
                    return Result.Failure(
                        ErrorCode.InvalidArgument,
                        "Unknown workRole in opening spawn.",
                        spawn.DefinitionId + ":" + spawn.WorkRole);
                }

                if (!lookup.TryGetEntity(spawn.DefinitionId, out var entityId))
                {
                    return Result.Failure(
                        ErrorCode.NotFound,
                        "WorkRole spawn entity missing.",
                        spawn.DefinitionId);
                }

                var assigned = service.AssignWork(world, entityId, role, state.Id);
                if (assigned.IsFailure)
                    return assigned;
            }

            return Result.Success();
        }

        static bool TryParseWorkRole(string text, out WorkRoleKind role)
        {
            return Enum.TryParse(text.Trim(), ignoreCase: true, out role) && role != WorkRoleKind.None;
        }
    }
}
