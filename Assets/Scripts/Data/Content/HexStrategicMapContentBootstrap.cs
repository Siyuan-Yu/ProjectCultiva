using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Data.Content
{
    /// <summary>从 Content 包加载 HexWorld JSON 并应用到会话。</summary>
    public static class HexStrategicMapContentBootstrap
    {
        public static Result TryApplyToSession(
            SimulationWorld world,
            DefinitionRegistry registry,
            OpeningScenarioDefinition scenario)
        {
            HexStrategicMapBootstrap.ClearLoadError();
            if (!HexStrategicMapBootstrap.UseHexStrategicMap || world == null)
                return Result.Success();
            if (world.HexWorld.HasGrid)
                return Result.Success();

            if (registry == null)
                return HexStrategicMapBootstrap.TryApplyFromFixture(world);

            var hexWorldId = scenario?.OpeningHexWorldId;
            if (string.IsNullOrWhiteSpace(hexWorldId))
                hexWorldId = HexStrategicMapBootstrap.DefaultHexWorldContentId;

            var parsed = DefinitionId.Parse(hexWorldId.Trim());
            if (parsed.IsFailure)
            {
                HexStrategicMapBootstrap.ReportLoadError("World Content Load Failed: invalid hex world id.");
                return Result.Failure(parsed.Error);
            }

            if (!registry.TryGetHexWorldContent(parsed.Value, out var definition) || definition == null)
            {
                HexStrategicMapBootstrap.ReportLoadError("World Content Load Failed: " + hexWorldId);
                return Result.Failure(
                    ErrorCode.NotFound,
                    "World Content Load Failed",
                    hexWorldId);
            }

            var applied = HexWorldContentLoader.Apply(world, definition);
            if (applied.IsFailure)
            {
                HexStrategicMapBootstrap.ReportLoadError(applied.Error.Message);
                return applied;
            }

            ArmyHexMigrationHelper.MigrateFormalArmies(world);
            return Result.Success();
        }
    }
}
