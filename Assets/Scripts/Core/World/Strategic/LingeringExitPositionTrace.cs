#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Diagnostics;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Development：Lingering Battlefield Exit 战略 Hex 写入追踪。</summary>
    public static class LingeringExitPositionTrace
    {
        public static Action<string> LogInfo { get; set; }
        public static Action<string> LogWarning { get; set; }

        public static void LogArmyHexMutation(
            SimulationWorld world,
            FormalArmy army,
            HexCoord oldHex,
            HexCoord newHex,
            string caller,
            string reason)
        {
            if (army == null)
                return;
            Emit(
                "[ LINGERING-EXIT-POSITION-TRACE ]\n" +
                "Kind: FormalArmy\n" +
                "Entity/Army Id: " + army.ArmyId + "\n" +
                "Old Hex: " + oldHex + "\n" +
                "New Hex: " + newHex + "\n" +
                "Caller: " + caller + "\n" +
                "Reason: " + reason + "\n" +
                "Tick: " + (world?.Tick.Value ?? 0UL));
        }

        public static void LogResidualHexMutation(
            SimulationWorld world,
            EntityId characterId,
            HexCoord oldHex,
            HexCoord newHex,
            string caller,
            string reason)
        {
            if (characterId.IsNone)
                return;
            Emit(
                "[ LINGERING-EXIT-POSITION-TRACE ]\n" +
                "Kind: ResidualCharacter\n" +
                "Entity Id: " + characterId + "\n" +
                "Old Hex: " + oldHex + "\n" +
                "New Hex: " + newHex + "\n" +
                "Caller: " + caller + "\n" +
                "Reason: " + reason + "\n" +
                "Tick: " + (world?.Tick.Value ?? 0UL));
        }

        public static void LogMissingHexAnchor(SimulationWorld world, string caller)
        {
            EmitWarning(
                "[ LINGERING-EXIT-POSITION-TRACE ]\n" +
                "Missing canonical BattleAnchorHex in Hex mode\n" +
                "Caller: " + caller + "\n" +
                "Tick: " + (world?.Tick.Value ?? 0UL));
        }

        static void Emit(string message)
        {
            if (LogInfo != null)
                LogInfo(message);
            else
                Debug.WriteLine(message);
        }

        static void EmitWarning(string message)
        {
            if (LogWarning != null)
                LogWarning(message);
            else
                Debug.WriteLine(message);
        }
    }
}
#endif
