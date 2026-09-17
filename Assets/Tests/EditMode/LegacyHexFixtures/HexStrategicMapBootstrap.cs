using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Hex 战略世界引导：测试/fixture 路径与加载错误状态。</summary>
    public static class HexStrategicMapBootstrap
    {
        public const string DefaultHexWorldContentId = "base:hex_world_ch01";

        public static bool UseHexStrategicMap { get; set; } = true;

        public static string LastLoadError { get; private set; } = string.Empty;

        public static void ClearLoadError() => LastLoadError = string.Empty;

        public static void ReportLoadError(string message) =>
            LastLoadError = string.IsNullOrWhiteSpace(message) ? string.Empty : message;

        /// <summary>测试/fixture：无 Content 包时使用 Ch01HexPrototypeMapBuilder。</summary>
        public static Result TryApplyFromFixture(SimulationWorld world)
        {
            ClearLoadError();
            if (!UseHexStrategicMap || world == null)
                return Result.Success();
            if (world.HexWorld.HasGrid)
                return Result.Success();

            Ch01HexPrototypeMapBuilder.Build(world);
            ArmyHexMigrationHelper.MigrateFormalArmies(world);
            return Result.Success();
        }
    }
}
