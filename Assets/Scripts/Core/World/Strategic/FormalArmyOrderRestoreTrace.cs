#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Diagnostics;

namespace XianXia.Core.World.Strategic
{
    public static class FormalArmyOrderRestoreTrace
    {
        public static void LogRestored(string armyId, string targetArmyId)
        {
            Debug.WriteLine(
                "[FormalArmyOrderRestore] ArmyId=" + (armyId ?? string.Empty) +
                " Order=AttackFormalArmy Target=" + (targetArmyId ?? string.Empty) +
                " RESTORED");
        }

        public static void LogFailed(string armyId, string targetArmyId, string reason)
        {
            Debug.WriteLine(
                "[FormalArmyOrderRestore] ArmyId=" + (armyId ?? string.Empty) +
                " Order=AttackFormalArmy Target=" + (targetArmyId ?? string.Empty) +
                " FAILED: " + (reason ?? string.Empty) +
                " → Idle");
        }
    }
}
#else
namespace XianXia.Core.World.Strategic
{
    public static class FormalArmyOrderRestoreTrace
    {
        public static void LogRestored(string armyId, string targetArmyId) { }
        public static void LogFailed(string armyId, string targetArmyId, string reason) { }
    }
}
#endif
