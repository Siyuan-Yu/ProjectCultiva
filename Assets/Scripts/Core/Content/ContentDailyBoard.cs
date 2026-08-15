using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Time;

namespace XianXia.Core.Content
{
    /// <summary>
    /// Per-game-day marks（今日已对弈／今日已拜访）. Key is author id; value is day index.
    /// Session-only; not in Snapshot v1.
    /// </summary>
    public sealed class ContentDailyBoard
    {
        readonly Dictionary<string, int> _markedDay = new Dictionary<string, int>(StringComparer.Ordinal);

        public static int DayIndex(WorldTick tick) =>
            (int)(tick.Value / (ulong)WorldTick.TicksPerDay);

        public bool IsMarkedToday(string key, WorldTick tick)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            return _markedDay.TryGetValue(key, out var day) && day == DayIndex(tick);
        }

        public void MarkToday(string key, WorldTick tick)
        {
            if (string.IsNullOrEmpty(key))
                return;
            _markedDay[key] = DayIndex(tick);
        }

        public void Clear(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;
            _markedDay.Remove(key);
        }

        public void ClearAll() => _markedDay.Clear();
    }
}
