using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>Playable 路径：从 Content 包加载 HexWorld 并应用 Ch01 战略默认值。</summary>
    public static class StrategicContentBootstrap
    {
        public static Result ApplyCh01Defaults(
            SimulationWorld world,
            DefinitionRegistry registry,
            OpeningScenarioDefinition scenario)
        {
            Ch01ScenarioStrategicSetup.Apply(world);
            var hex = HexStrategicMapContentBootstrap.TryApplyToSession(world, registry, scenario);
            if (hex.IsFailure)
                return hex;
            Ch01ScenarioStrategicSetup.EnsureLevelTesterFixtures(world);
            var armies = FormalArmyContentBootstrap.Apply(world, registry, scenario);
            if (armies.IsFailure)
                return armies;
            Ch01ScenarioStrategicSetup.PositionPrototypeTestBanditArmies(world);
            return Result.Success();
        }
    }
}
