using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Development-only??? FormalArmy ?? Hex ????????</summary>
    public static class FormalArmyStrategicMutationDiagnostics
    {
        public enum MutationAllowance
        {
            Unrestricted = 0,
            CommandPlanning,
            TravelTick,
            BattleRestore,
            ScenarioTeleport,
            SnapshotLoad,
        }

        public readonly struct StrategicSnapshot
        {
            public readonly FormalArmyState State;
            public readonly HexCoord CurrentHex;
            public readonly float StepProgress;

            public StrategicSnapshot(FormalArmyState state, HexCoord currentHex, float stepProgress)
            {
                State = state;
                CurrentHex = currentHex;
                StepProgress = stepProgress;
            }

            public static StrategicSnapshot Capture(FormalArmy army)
            {
                if (army == null)
                    return default;
                return new StrategicSnapshot(army.State, army.CurrentHex, army.StepProgress);
            }

            public string FormatBlock(string prefix)
            {
                var sb = new StringBuilder(128);
                sb.Append(prefix).AppendLine();
                sb.Append("  State: ").Append(State).AppendLine();
                sb.Append("  Hex: ").Append(CurrentHex).AppendLine();
                sb.Append("  StepProgress: ").Append(StepProgress.ToString("0.###"));
                return sb.ToString();
            }
        }

        sealed class MutationScope : IDisposable
        {
            public MutationScope(MutationAllowance allowance, string operation)
            {
                _allowanceStack.Push(allowance);
                _operationStack.Push(operation ?? string.Empty);
            }

            public void Dispose()
            {
                if (_operationStack.Count > 0)
                    _operationStack.Pop();
                if (_allowanceStack.Count > 0)
                    _allowanceStack.Pop();
            }
        }

        static readonly Stack<MutationAllowance> _allowanceStack = new Stack<MutationAllowance>(8);
        static readonly Stack<string> _operationStack = new Stack<string>(8);

        public static bool Enabled { get; set; }
        public static int UnityFrame { get; set; }
        public static Func<ulong> WorldTickProvider { get; set; }
        public static Action<string> LogInfo { get; set; }
        public static Action<string> LogError { get; set; }

        public static void BindPresentationWorld(SimulationWorld world) { }

        public static void RecordPresentation(
            FormalArmy army,
            float worldX,
            float worldY,
            bool fromRender,
            bool afterSync) { }

        public static IDisposable Scope(MutationAllowance allowance, string operation) =>
            Enabled ? new MutationScope(allowance, operation) : NoopDisposable.Instance;

        public static void ExecuteMutation(FormalArmy army, string operation, Action mutate) => mutate?.Invoke();

        internal static void WriteState(FormalArmy army, ref FormalArmyState field, FormalArmyState value) =>
            field = value;

        internal static void WriteString(FormalArmy army, string name, ref string field, string value) =>
            field = value ?? string.Empty;

        internal static void WriteInt(FormalArmy army, string name, ref int field, int value) =>
            field = value;

        internal static void WriteFloat(FormalArmy army, string name, ref float field, float value) =>
            field = value;

        sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new NoopDisposable();
            public void Dispose() { }
        }
    }
}
