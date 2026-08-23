using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Development-only：追踪 FormalArmy 大地图 portrait 渲染坐标跳变（与 Domain mutation 独立）。</summary>
    public static class ArmyWorldMapRenderDiagnostics
    {
        public enum RenderOperation
        {
            None = 0,
            Move,
            Attack,
            Pursuit,
            Repath,
            Other,
        }

        struct RenderSnapshot
        {
            public float WorldX;
            public float WorldY;
            public FormalArmyWorldPositionResolver.RenderSourceType SourceType;
            public string SourceId;
            public string RenderReason;
        }

        static readonly Dictionary<string, RenderSnapshot> _lastByArmyId =
            new Dictionary<string, RenderSnapshot>(StringComparer.Ordinal);

        public static bool Enabled { get; set; }

        public static RenderOperation CurrentOperation { get; set; }

        public static Action<string> LogInfo { get; set; }

        public static void Record(
            SimulationWorld world,
            FormalArmy army,
            in FormalArmyWorldPositionResolver.WorldPositionInfo render)
        {
            if (!Enabled || world == null || army == null || string.IsNullOrEmpty(army.ArmyId) || !render.Resolved)
                return;

            var armyId = army.ArmyId;
            if (!_lastByArmyId.TryGetValue(armyId, out var before))
            {
                _lastByArmyId[armyId] = ToSnapshot(render);
                return;
            }

            const float jumpEps = 0.05f;
            var dx = render.WorldX - before.WorldX;
            var dy = render.WorldY - before.WorldY;
            var distSq = dx * dx + dy * dy;
            var sourceChanged = before.SourceType != render.SourceType ||
                                !string.Equals(before.SourceId, render.SourceId, StringComparison.Ordinal) ||
                                !string.Equals(before.RenderReason, render.RenderReason, StringComparison.Ordinal);

            if (distSq > jumpEps * jumpEps || sourceChanged)
            {
                EmitJump(world, army, before, render);
            }

            _lastByArmyId[armyId] = ToSnapshot(render);
        }

        public static void ClearArmy(string armyId)
        {
            if (string.IsNullOrEmpty(armyId))
                return;
            _lastByArmyId.Remove(armyId);
        }

        static RenderSnapshot ToSnapshot(in FormalArmyWorldPositionResolver.WorldPositionInfo render) =>
            new RenderSnapshot
            {
                WorldX = render.WorldX,
                WorldY = render.WorldY,
                SourceType = render.SourceType,
                SourceId = render.SourceId,
                RenderReason = render.RenderReason,
            };

        static void EmitJump(
            SimulationWorld world,
            FormalArmy army,
            RenderSnapshot before,
            in FormalArmyWorldPositionResolver.WorldPositionInfo after)
        {
            var sb = new StringBuilder(2048);
            sb.AppendLine("[ARMY-RENDER-POS-JUMP]");
            sb.Append("ArmyId: ").Append(army.ArmyId).AppendLine();
            sb.Append("Current Operation: ").Append(CurrentOperation).AppendLine();
            sb.AppendLine("DOMAIN");
            sb.Append("  State: ").Append(army.State).AppendLine();
            sb.Append("  Node: ").Append(army.NodeId).AppendLine();
            sb.Append("  Route: ").Append(army.RouteId).AppendLine();
            sb.Append("  Progress: ").Append(army.GetRouteDisplayProgress().ToString("0.###")).AppendLine();
            sb.Append("  Dest: ").Append(army.DestNodeId).AppendLine();
            sb.AppendLine("RENDER BEFORE");
            sb.Append("  WorldPosition: (").Append(before.WorldX.ToString("0.###"))
                .Append(',').Append(before.WorldY.ToString("0.###")).AppendLine(")");
            sb.Append("  Source: ").Append(before.SourceType)
                .Append(" / ").Append(before.SourceId)
                .Append(" / ").Append(before.RenderReason).AppendLine();
            sb.AppendLine("RENDER AFTER");
            sb.Append("  WorldPosition: (").Append(after.WorldX.ToString("0.###"))
                .Append(',').Append(after.WorldY.ToString("0.###")).AppendLine(")");
            sb.Append("  Source: ").Append(after.SourceType)
                .Append(" / ").Append(after.SourceId)
                .Append(" / ").Append(after.RenderReason).AppendLine();
            sb.AppendLine("CALL STACK:");
            sb.Append(CaptureStackTrace());

            if (LogInfo != null)
                LogInfo(sb.ToString());
            else
                Debug.WriteLine(sb.ToString());
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
    }
}
