using System;
using System.Collections.Generic;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Phase H：CaptureObjective 运行时板；ControlCore 为第一实例。</summary>
    public sealed class CaptureObjectiveState
    {
        public string ObjectiveId { get; set; } = string.Empty;
        public string SiteId { get; set; } = string.Empty;
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

        readonly Dictionary<string, List<string>> _siteObjectives =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, CaptureObjectiveState> All => _byObjectiveId;

        public void Register(CaptureObjectiveState objective)
        {
            if (objective == null || string.IsNullOrEmpty(objective.ObjectiveId))
                return;

            if (_byObjectiveId.TryGetValue(objective.ObjectiveId, out var previous) && previous != null)
                RemoveReverse(previous.SiteId, objective.ObjectiveId);
            _byObjectiveId[objective.ObjectiveId] = objective;
            AddReverse(objective.SiteId, objective.ObjectiveId);
        }

        /// <summary>
        /// CaptureObjective 与 WorldSite 的唯一写入口。正向字段和反向索引在同一调用内更新。
        /// 空 SiteId 用于恢复尚未完成静态 Content 重绑的 Objective。
        /// </summary>
        public bool BindSite(string objectiveId, string siteId)
        {
            if (string.IsNullOrEmpty(objectiveId) ||
                !_byObjectiveId.TryGetValue(objectiveId, out var objective) || objective == null)
                return false;

            siteId = siteId ?? string.Empty;
            if (string.Equals(objective.SiteId ?? string.Empty, siteId, StringComparison.Ordinal))
            {
                AddReverse(siteId, objectiveId);
                return true;
            }

            RemoveReverse(objective.SiteId, objectiveId);
            objective.SiteId = siteId;
            AddReverse(siteId, objectiveId);
            return true;
        }

        public bool TryGet(string objectiveId, out CaptureObjectiveState objective) =>
            _byObjectiveId.TryGetValue(objectiveId ?? string.Empty, out objective);

        public bool AllCompletedForSite(string siteId)
        {
            if (string.IsNullOrEmpty(siteId) ||
                !_siteObjectives.TryGetValue(siteId, out var ids) ||
                ids.Count == 0)
                return false;
            for (var i = 0; i < ids.Count; i++)
            {
                if (!_byObjectiveId.TryGetValue(ids[i], out var obj) || obj == null || !obj.Completed)
                    return false;
            }

            return true;
        }

        public IReadOnlyList<string> GetObjectiveIdsForSite(string siteId)
        {
            if (string.IsNullOrEmpty(siteId) ||
                !_siteObjectives.TryGetValue(siteId, out var list))
                return Array.Empty<string>();
            return list;
        }

        public void Clear()
        {
            _byObjectiveId.Clear();
            _siteObjectives.Clear();
        }

        void AddReverse(string siteId, string objectiveId)
        {
            if (string.IsNullOrEmpty(siteId)) return;
            if (!_siteObjectives.TryGetValue(siteId, out var list))
            {
                list = new List<string>(2);
                _siteObjectives[siteId] = list;
            }
            if (!list.Contains(objectiveId)) list.Add(objectiveId);
        }

        void RemoveReverse(string siteId, string objectiveId)
        {
            if (string.IsNullOrEmpty(siteId) ||
                !_siteObjectives.TryGetValue(siteId, out var list)) return;
            list.Remove(objectiveId);
            if (list.Count == 0) _siteObjectives.Remove(siteId);
        }
    }
}
