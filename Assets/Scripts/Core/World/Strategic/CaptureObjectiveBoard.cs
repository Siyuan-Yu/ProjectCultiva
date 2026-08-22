using System;
using System.Collections.Generic;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Phase H：CaptureObjective 运行时板；ControlCore 为第一实例。</summary>
    public sealed class CaptureObjectiveState
    {
        public string ObjectiveId { get; set; } = string.Empty;
        public string NodeId { get; set; } = string.Empty;
        public string WorkAreaId { get; set; } = string.Empty;
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }
        public bool Completed { get; set; }
        public float OccupyProgressSeconds { get; set; }
        public float OccupyHoldSeconds { get; set; }
    }

    public sealed class CaptureObjectiveBoard
    {
        readonly Dictionary<string, CaptureObjectiveState> _byObjectiveId =
            new Dictionary<string, CaptureObjectiveState>(StringComparer.Ordinal);

        readonly Dictionary<string, List<string>> _nodeObjectives =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, CaptureObjectiveState> All => _byObjectiveId;

        public void Register(CaptureObjectiveState objective)
        {
            if (objective == null || string.IsNullOrEmpty(objective.ObjectiveId))
                return;
            _byObjectiveId[objective.ObjectiveId] = objective;
            if (string.IsNullOrEmpty(objective.NodeId))
                return;
            if (!_nodeObjectives.TryGetValue(objective.NodeId, out var list))
            {
                list = new List<string>(2);
                _nodeObjectives[objective.NodeId] = list;
            }

            if (!list.Contains(objective.ObjectiveId))
                list.Add(objective.ObjectiveId);
        }

        public bool TryGet(string objectiveId, out CaptureObjectiveState objective) =>
            _byObjectiveId.TryGetValue(objectiveId ?? string.Empty, out objective);

        public bool AllCompletedForNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) ||
                !_nodeObjectives.TryGetValue(nodeId, out var ids) ||
                ids.Count == 0)
                return false;
            for (var i = 0; i < ids.Count; i++)
            {
                if (!_byObjectiveId.TryGetValue(ids[i], out var obj) || obj == null || !obj.Completed)
                    return false;
            }

            return true;
        }

        public IReadOnlyList<string> GetObjectiveIdsForNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) ||
                !_nodeObjectives.TryGetValue(nodeId, out var list))
                return Array.Empty<string>();
            return list;
        }

        public void Clear()
        {
            _byObjectiveId.Clear();
            _nodeObjectives.Clear();
        }
    }
}
