using System;
using System.Collections.Generic;

namespace XianXia.Core.World
{
    public static class WorldActivitySourceKind
    {
        public const string WorldOpportunity = "worldOpportunity";
    }

    public static class WorldActivityState
    {
        public const string Active = "active";
        public const string History = "history";
    }

    public sealed class WorldActivityEntry
    {
        public string ActivityId { get; set; } = string.Empty;
        public string SourceKind { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public ulong CreatedDayIndex { get; set; }
        public string State { get; set; } = WorldActivityState.Active;
        public bool IsRead { get; set; }
        public ulong? ResolvedDayIndex { get; set; }
    }

    public sealed class WorldActivityBoard
    {
        public const int HistoryCapacity = 100;

        readonly Dictionary<string, WorldActivityEntry> _entries =
            new Dictionary<string, WorldActivityEntry>(StringComparer.Ordinal);
        readonly Dictionary<string, string> _activeBySource =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, WorldActivityEntry> Entries => _entries;
        public ulong NextActivitySequence { get; private set; } = 1;

        public WorldActivityEntry CreateActive(
            string sourceKind,
            string sourceId,
            string title,
            string body,
            ulong createdDayIndex)
        {
            var sourceKey = SourceKey(sourceKind, sourceId);
            if (_activeBySource.TryGetValue(sourceKey, out var existingId) &&
                _entries.TryGetValue(existingId, out var existing))
                return existing;

            var entry = new WorldActivityEntry
            {
                ActivityId = "activity:" + NextActivitySequence++,
                SourceKind = sourceKind ?? string.Empty,
                SourceId = sourceId ?? string.Empty,
                Title = title ?? string.Empty,
                Body = body ?? string.Empty,
                CreatedDayIndex = createdDayIndex,
                State = WorldActivityState.Active,
                IsRead = false
            };
            _entries.Add(entry.ActivityId, entry);
            _activeBySource.Add(sourceKey, entry.ActivityId);
            return entry;
        }

        public bool TryGet(string activityId, out WorldActivityEntry entry) =>
            _entries.TryGetValue(activityId ?? string.Empty, out entry);

        public bool TryGetActiveBySource(string sourceKind, string sourceId, out WorldActivityEntry entry)
        {
            entry = null;
            return _activeBySource.TryGetValue(SourceKey(sourceKind, sourceId), out var activityId) &&
                   _entries.TryGetValue(activityId, out entry);
        }

        public bool MarkRead(string activityId)
        {
            if (!TryGet(activityId, out var entry)) return false;
            entry.IsRead = true;
            return true;
        }

        public bool ResolveSource(string sourceKind, string sourceId, ulong resolvedDayIndex)
        {
            var key = SourceKey(sourceKind, sourceId);
            if (!_activeBySource.TryGetValue(key, out var activityId) ||
                !_entries.TryGetValue(activityId, out var entry)) return false;
            _activeBySource.Remove(key);
            entry.State = WorldActivityState.History;
            entry.ResolvedDayIndex = resolvedDayIndex;
            TrimHistory();
            return true;
        }

        public bool RestoreEntry(WorldActivityEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.ActivityId) ||
                _entries.ContainsKey(entry.ActivityId)) return false;
            if (entry.State == WorldActivityState.Active)
            {
                var key = SourceKey(entry.SourceKind, entry.SourceId);
                if (_activeBySource.ContainsKey(key)) return false;
                _activeBySource.Add(key, entry.ActivityId);
            }
            _entries.Add(entry.ActivityId, entry);
            return true;
        }

        public void RestoreSequence(ulong next) => NextActivitySequence = next == 0 ? 1UL : next;

        void TrimHistory()
        {
            while (HistoryCount() > HistoryCapacity)
            {
                WorldActivityEntry oldest = null;
                foreach (var entry in _entries.Values)
                {
                    if (entry.State != WorldActivityState.History) continue;
                    if (oldest == null ||
                        (entry.ResolvedDayIndex ?? 0) < (oldest.ResolvedDayIndex ?? 0) ||
                        ((entry.ResolvedDayIndex ?? 0) == (oldest.ResolvedDayIndex ?? 0) &&
                         (entry.CreatedDayIndex < oldest.CreatedDayIndex ||
                          (entry.CreatedDayIndex == oldest.CreatedDayIndex &&
                           SequenceOf(entry.ActivityId) < SequenceOf(oldest.ActivityId)))))
                        oldest = entry;
                }
                if (oldest == null) break;
                _entries.Remove(oldest.ActivityId);
            }
        }

        int HistoryCount()
        {
            var count = 0;
            foreach (var entry in _entries.Values)
                if (entry.State == WorldActivityState.History) count++;
            return count;
        }

        static string SourceKey(string sourceKind, string sourceId) =>
            (sourceKind ?? string.Empty) + "\n" + (sourceId ?? string.Empty);

        static ulong SequenceOf(string activityId) =>
            activityId != null && activityId.StartsWith("activity:", StringComparison.Ordinal) &&
            ulong.TryParse(activityId.Substring("activity:".Length), out var sequence)
                ? sequence
                : ulong.MaxValue;
    }
}
