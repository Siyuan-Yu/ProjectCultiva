using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Development-only：追踪 FormalArmy 战略位置写入与非法瞬移。默认关闭；Host 启动时启用。
    /// 不改变任何移动／追击语义，仅记录 mutation 与 stack trace。
    /// </summary>
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
            public readonly string NodeId;
            public readonly string RouteId;
            public readonly string DestNodeId;
            public readonly float Progress;
            public readonly int RemainingTravelTicks;
            public readonly int TravelTotalTicks;
            public readonly float RouteSegmentOriginProgress;
            public readonly float RouteSegmentEndProgress;

            public StrategicSnapshot(
                FormalArmyState state,
                string nodeId,
                string routeId,
                string destNodeId,
                float progress,
                int remainingTravelTicks,
                int travelTotalTicks,
                float routeSegmentOriginProgress,
                float routeSegmentEndProgress)
            {
                State = state;
                NodeId = nodeId ?? string.Empty;
                RouteId = routeId ?? string.Empty;
                DestNodeId = destNodeId ?? string.Empty;
                Progress = progress;
                RemainingTravelTicks = remainingTravelTicks;
                TravelTotalTicks = travelTotalTicks;
                RouteSegmentOriginProgress = routeSegmentOriginProgress;
                RouteSegmentEndProgress = routeSegmentEndProgress;
            }

            public static StrategicSnapshot Capture(FormalArmy army)
            {
                if (army == null)
                    return default;
                return new StrategicSnapshot(
                    army.State,
                    army.NodeId,
                    army.RouteId,
                    army.DestNodeId,
                    army.GetRouteDisplayProgress(),
                    army.RemainingTravelTicks,
                    army.TravelTotalTicks,
                    army.RouteSegmentOriginProgress,
                    army.RouteSegmentEndProgress);
            }

            public bool HasRoutePosition =>
                !string.IsNullOrEmpty(RouteId) &&
                (State == FormalArmyState.OnRoute || Progress >= 0f);

            public string FormatBlock(string prefix)
            {
                var sb = new StringBuilder(192);
                sb.Append(prefix).AppendLine();
                sb.Append("  State: ").Append(State).AppendLine();
                sb.Append("  NodeId: ").Append(NodeId).AppendLine();
                sb.Append("  RouteId: ").Append(RouteId).AppendLine();
                sb.Append("  DestNodeId: ").Append(DestNodeId).AppendLine();
                sb.Append("  Progress: ").Append(Progress.ToString("0.###")).AppendLine();
                sb.Append("  RemainingTravelTicks: ").Append(RemainingTravelTicks).AppendLine();
                sb.Append("  TravelTotalTicks: ").Append(TravelTotalTicks);
                return sb.ToString();
            }
        }

        public readonly struct PresentationSnapshot
        {
            public readonly float WorldX;
            public readonly float WorldY;
            public readonly bool Resolved;

            public PresentationSnapshot(float worldX, float worldY, bool resolved)
            {
                WorldX = worldX;
                WorldY = worldY;
                Resolved = resolved;
            }
        }

        sealed class MutationScope : IDisposable
        {
            readonly MutationAllowance _allowance;
            readonly string _operation;

            public MutationScope(MutationAllowance allowance, string operation)
            {
                _allowance = allowance;
                _operation = operation ?? string.Empty;
                _allowanceStack.Push(_allowance);
                _operationStack.Push(_operation);
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
        static readonly Dictionary<string, string> _lastMutationReason =
            new Dictionary<string, string>(StringComparer.Ordinal);

        static int _batchDepth;
        static StrategicSnapshot _batchBefore;
        static string _batchOperation = string.Empty;
        static FormalArmy _batchArmy;

        static string _attackSessionId = string.Empty;
        static string _attackArmyId = string.Empty;
        static StrategicSnapshot _attackBeginSnapshot;
        static bool _attackCommandFrameOpen;
        static bool _attackIllegalDetected;

        static PresentationSnapshot _lastDomainPresentation;
        static PresentationSnapshot _lastPortraitPresentation;
        static readonly HashSet<string> _illegalTeleportArmyIds =
            new HashSet<string>(StringComparer.Ordinal);

        public static bool Enabled { get; set; }

        public static int UnityFrame { get; set; }

        public static Func<ulong> WorldTickProvider { get; set; }

        public static Action<string> LogInfo { get; set; }

        public static Action<string> LogError { get; set; }

        public static string ActiveAttackSessionId => _attackSessionId;

        public static bool AttackCommandFrameOpen => _attackCommandFrameOpen;

        public static bool AttackIllegalDetected => _attackIllegalDetected;

        public static string GetLastMutationReason(string armyId)
        {
            if (string.IsNullOrEmpty(armyId))
                return string.Empty;
            return _lastMutationReason.TryGetValue(armyId, out var reason) ? reason : string.Empty;
        }

        public static bool HasIllegalTeleport(string armyId) =>
            !string.IsNullOrEmpty(armyId) && _illegalTeleportArmyIds.Contains(armyId);

        public static IDisposable Scope(MutationAllowance allowance, string operation) =>
            Enabled ? new MutationScope(allowance, operation) : NoopDisposable.Instance;

        public static void ExecuteMutation(FormalArmy army, string operation, Action mutate)
        {
            if (!Enabled || army == null)
            {
                mutate?.Invoke();
                return;
            }

            _batchDepth++;
            if (_batchDepth == 1)
            {
                _batchArmy = army;
                _batchOperation = operation ?? string.Empty;
                _batchBefore = StrategicSnapshot.Capture(army);
            }

            try
            {
                mutate?.Invoke();
            }
            finally
            {
                _batchDepth--;
                if (_batchDepth == 0 && _batchArmy != null)
                {
                    var after = StrategicSnapshot.Capture(_batchArmy);
                    EmitIfStrategicPositionChanged(_batchArmy, _batchOperation, _batchBefore, after);
                    _batchArmy = null;
                    _batchOperation = string.Empty;
                }
            }
        }

        internal static void WriteState(FormalArmy army, ref FormalArmyState field, FormalArmyState value) =>
            WriteField(army, nameof(FormalArmy.State), field, value, ref field);

        internal static void WriteString(FormalArmy army, string name, ref string field, string value) =>
            WriteField(army, name, field, value, ref field);

        internal static void WriteInt(FormalArmy army, string name, ref int field, int value) =>
            WriteField(army, name, field, value, ref field);

        internal static void WriteFloat(FormalArmy army, string name, ref float field, float value) =>
            WriteField(army, name, field, value, ref field);

        static void WriteField<T>(FormalArmy army, string name, T oldValue, T newValue, ref T field)
        {
            if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
                return;

            if (!Enabled || army == null || _batchDepth > 0)
            {
                field = newValue;
                return;
            }

            var before = StrategicSnapshot.Capture(army);
            field = newValue;
            var after = StrategicSnapshot.Capture(army);
            EmitIfStrategicPositionChanged(army, name, before, after);
        }

        public static string BeginAttackSession(
            SimulationWorld world,
            string attackerArmyId,
            string targetArmyId,
            string targetStackId,
            FormalArmy attackerArmy)
        {
            if (!Enabled || attackerArmy == null)
                return string.Empty;

            _attackSessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
            _attackArmyId = attackerArmyId ?? string.Empty;
            _attackBeginSnapshot = StrategicSnapshot.Capture(attackerArmy);
            _attackCommandFrameOpen = true;
            _attackIllegalDetected = false;

            var sb = new StringBuilder(512);
            sb.AppendLine("[ATTACK-BEGIN]");
            sb.Append("AttackDebugSessionId: ").Append(_attackSessionId).AppendLine();
            sb.Append("AttackerArmyId: ").Append(attackerArmyId).AppendLine();
            sb.Append("TargetArmyId: ").Append(targetArmyId).AppendLine();
            sb.Append("TargetStackId: ").Append(targetStackId).AppendLine();
            sb.Append(_attackBeginSnapshot.FormatBlock("Attacker BEFORE command:"));
            Log(sb.ToString());
            return _attackSessionId;
        }

        public static void LogAttackStage(string stage, FormalArmy army, PresentationSnapshot? presentation = null)
        {
            if (!Enabled || army == null || string.IsNullOrEmpty(_attackSessionId))
                return;

            var snap = StrategicSnapshot.Capture(army);
            var sb = new StringBuilder(384);
            sb.Append("[ATTACK-STAGE] ").Append(stage).AppendLine();
            sb.Append("AttackDebugSessionId: ").Append(_attackSessionId).AppendLine();
            sb.Append(snap.FormatBlock("Attacker position:"));
            if (presentation.HasValue)
            {
                var p = presentation.Value;
                sb.Append("Presentation: ").Append(p.Resolved ? $"({p.WorldX:0.##},{p.WorldY:0.##})" : "UNRESOLVED");
            }

            Log(sb.ToString());

            if (_attackCommandFrameOpen &&
                !string.IsNullOrEmpty(_attackArmyId) &&
                string.Equals(army.ArmyId, _attackArmyId, StringComparison.Ordinal) &&
                IsPhysicalTeleport(_attackBeginSnapshot, snap))
            {
                EmitIllegalTeleport(army, "AttackStage:" + stage, _attackBeginSnapshot, snap);
            }
        }

        public static void EndAttackCommandFrame(FormalArmy army, PresentationSnapshot? presentation = null)
        {
            if (!Enabled || string.IsNullOrEmpty(_attackSessionId) || army == null)
                return;

            var end = StrategicSnapshot.Capture(army);
            var sb = new StringBuilder(512);
            sb.AppendLine("[ATTACK-COMMAND-END]");
            sb.Append("AttackDebugSessionId: ").Append(_attackSessionId).AppendLine();
            sb.Append(_attackBeginSnapshot.FormatBlock("Attacker BEGIN:"));
            sb.Append(end.FormatBlock("Attacker END (before first travel tick):"));
            sb.Append("PositionContinuous: ")
                .Append(!IsPhysicalTeleport(_attackBeginSnapshot, end) ? "YES" : "NO")
                .AppendLine();
            if (presentation.HasValue)
            {
                var p = presentation.Value;
                sb.Append("PortraitPresentation: ")
                    .Append(p.Resolved ? $"({p.WorldX:0.##},{p.WorldY:0.##})" : "UNRESOLVED")
                    .AppendLine();
            }

            Log(sb.ToString());

            if (IsPhysicalTeleport(_attackBeginSnapshot, end))
                EmitIllegalTeleport(army, "AttackCommandEnd", _attackBeginSnapshot, end);

            _attackCommandFrameOpen = false;
        }

        public static void RecordPresentation(
            FormalArmy army,
            float worldX,
            float worldY,
            bool resolved,
            bool isPortrait)
        {
            if (!Enabled || army == null)
                return;

            var snap = new PresentationSnapshot(worldX, worldY, resolved);
            if (isPortrait)
                _lastPortraitPresentation = snap;
            else
                _lastDomainPresentation = snap;

            if (!_attackCommandFrameOpen ||
                string.IsNullOrEmpty(_attackArmyId) ||
                !string.Equals(army.ArmyId, _attackArmyId, StringComparison.Ordinal))
                return;

            var domain = StrategicSnapshot.Capture(army);
            if (!resolved)
                return;

            // Presentation-only jump: domain continuous but portrait at pure node coords while domain on route.
            if (!IsPhysicalTeleport(_attackBeginSnapshot, domain) &&
                domain.HasRoutePosition &&
                TryResolvePureNodeXY(army, out var nx, out var ny) &&
                (Math.Abs(worldX - nx) < 0.01f && Math.Abs(worldY - ny) < 0.01f) &&
                Math.Abs(domain.Progress - 0f) > 0.05f &&
                Math.Abs(domain.Progress - 1f) > 0.05f)
            {
                LogError?.Invoke(
                    "[PRESENTATION-TELEPORT-SUSPECT] AttackDebugSessionId=" + _attackSessionId +
                    " DomainProgress=" + domain.Progress.ToString("0.###") +
                    " PortraitAtNode=(" + nx.ToString("0.##") + "," + ny.ToString("0.##") + ")" +
                    " See [ATTACK-BEGIN]/[FORMAL-ARMY-POS-MUTATION].");
            }
        }

        static bool TryResolvePureNodeXY(FormalArmy army, out float x, out float y)
        {
            x = y = 0f;
            if (_presentationWorld == null || army == null || string.IsNullOrEmpty(army.NodeId))
                return false;
            if (!_presentationWorld.WorldGraph.TryGetNode(army.NodeId, out var node) || node == null)
                return false;
            x = node.WorldX;
            y = node.WorldY;
            return true;
        }

        public static void BindWorldGraphForPresentationCheck(SimulationWorld world) =>
            _presentationWorld = world;

        static SimulationWorld _presentationWorld;

        static void EmitIfStrategicPositionChanged(
            FormalArmy army,
            string operation,
            StrategicSnapshot before,
            StrategicSnapshot after)
        {
            if (!HasStrategicPositionDelta(before, after))
                return;

            var reason = BuildOperationReason(operation);
            if (!string.IsNullOrEmpty(army.ArmyId))
                _lastMutationReason[army.ArmyId] = reason;

            var sb = new StringBuilder(2048);
            sb.AppendLine("[FORMAL-ARMY-POS-MUTATION]");
            sb.Append("ArmyId: ").Append(army.ArmyId).AppendLine();
            sb.Append("WorldTick: ").Append(WorldTickProvider?.Invoke() ?? 0).AppendLine();
            sb.Append("UnityFrame: ").Append(UnityFrame).AppendLine();
            sb.Append("Operation / Reason: ").Append(reason).AppendLine();
            sb.Append(before.FormatBlock("BEFORE"));
            sb.Append(after.FormatBlock("AFTER"));
            sb.AppendLine("CALL STACK:");
            sb.Append(CaptureStackTrace());
            Log(sb.ToString());

            if (IsIllegalMutation(before, after))
                EmitIllegalTeleport(army, reason, before, after);
        }

        static void EmitIllegalTeleport(
            FormalArmy army,
            string reason,
            StrategicSnapshot before,
            StrategicSnapshot after)
        {
            _attackIllegalDetected = true;
            if (!string.IsNullOrEmpty(army?.ArmyId))
                _illegalTeleportArmyIds.Add(army.ArmyId);
            var sb = new StringBuilder(2048);
            sb.AppendLine("[ILLEGAL-ARMY-TELEPORT]");
            sb.Append("ArmyId: ").Append(army?.ArmyId).AppendLine();
            sb.Append("AttackDebugSessionId: ").Append(_attackSessionId).AppendLine();
            sb.Append("Reason: ").Append(reason).AppendLine();
            sb.Append("TeleportNodeGuess: ").Append(GuessTeleportNodeIdentity(before, after)).AppendLine();
            sb.Append(before.FormatBlock("BEFORE"));
            sb.Append(after.FormatBlock("AFTER"));
            sb.AppendLine("CALL STACK:");
            sb.Append(CaptureStackTrace());
            LogError?.Invoke(sb.ToString());
        }

        static bool IsIllegalMutation(StrategicSnapshot before, StrategicSnapshot after)
        {
            if (!IsPhysicalTeleport(before, after))
                return false;

            var allowance = _allowanceStack.Count > 0 ? _allowanceStack.Peek() : MutationAllowance.Unrestricted;
            if (allowance == MutationAllowance.TravelTick ||
                allowance == MutationAllowance.BattleRestore ||
                allowance == MutationAllowance.ScenarioTeleport ||
                allowance == MutationAllowance.SnapshotLoad)
                return false;

            if (allowance == MutationAllowance.CommandPlanning)
                return true;

            if (_attackCommandFrameOpen)
                return true;

            return false;
        }

        internal static bool IsPhysicalTeleport(StrategicSnapshot before, StrategicSnapshot after)
        {
            const float progressEps = 0.02f;

            var beforeOnRoute = before.HasRoutePosition;
            var afterOnRoute = after.HasRoutePosition;

            if (beforeOnRoute && afterOnRoute &&
                string.Equals(before.RouteId, after.RouteId, StringComparison.Ordinal))
            {
                if (before.State == FormalArmyState.OnRoute && after.State == FormalArmyState.OnRoute)
                    return false;
                if (Math.Abs(before.Progress - after.Progress) > progressEps &&
                    before.RemainingTravelTicks == after.RemainingTravelTicks)
                    return true;
                return false;
            }

            if (beforeOnRoute && !afterOnRoute && string.IsNullOrEmpty(after.RouteId))
                return true;

            if (!beforeOnRoute && !afterOnRoute &&
                !string.Equals(before.NodeId, after.NodeId, StringComparison.Ordinal))
                return true;

            if (beforeOnRoute && afterOnRoute &&
                !string.Equals(before.RouteId, after.RouteId, StringComparison.Ordinal) &&
                after.State != FormalArmyState.OnRoute)
                return true;

            if (beforeOnRoute && !afterOnRoute &&
                !string.Equals(before.NodeId, after.NodeId, StringComparison.Ordinal) &&
                after.State == FormalArmyState.AtNode)
                return true;

            return false;
        }

        static string GuessTeleportNodeIdentity(StrategicSnapshot before, StrategicSnapshot after)
        {
            if (!string.IsNullOrEmpty(after.NodeId))
            {
                if (string.Equals(after.NodeId, before.NodeId, StringComparison.Ordinal))
                    return "SameNodeIdAsBefore";
                if (!string.IsNullOrEmpty(before.RouteId))
                    return "Suspect:PathOrRouteEndpoint/NodeId=" + after.NodeId;
                return "TargetNodeOrOther/NodeId=" + after.NodeId;
            }

            return "Other";
        }

        static bool HasStrategicPositionDelta(StrategicSnapshot before, StrategicSnapshot after) =>
            before.State != after.State ||
            !string.Equals(before.NodeId, after.NodeId, StringComparison.Ordinal) ||
            !string.Equals(before.RouteId, after.RouteId, StringComparison.Ordinal) ||
            !string.Equals(before.DestNodeId, after.DestNodeId, StringComparison.Ordinal) ||
            Math.Abs(before.Progress - after.Progress) > 0.0001f ||
            before.RemainingTravelTicks != after.RemainingTravelTicks ||
            before.TravelTotalTicks != after.TravelTotalTicks;

        static string BuildOperationReason(string operation)
        {
            if (_operationStack.Count == 0)
                return operation ?? string.Empty;
            var top = _operationStack.Peek();
            if (string.IsNullOrEmpty(operation))
                return top;
            if (string.Equals(top, operation, StringComparison.Ordinal))
                return top;
            return top + " > " + operation;
        }

        static string CaptureStackTrace()
        {
            try
            {
                return new StackTrace(2, true).ToString();
            }
            catch
            {
                return new StackTrace().ToString();
            }
        }

        static void Log(string message)
        {
            if (LogInfo != null)
                LogInfo(message);
            else
                Debug.WriteLine(message);
        }

        sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new NoopDisposable();
            public void Dispose() { }
        }
    }
}
