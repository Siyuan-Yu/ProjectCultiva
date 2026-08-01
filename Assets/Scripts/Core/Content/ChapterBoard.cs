using System;
using System.Collections.Generic;

namespace XianXia.Core.Content
{
    public sealed class ChapterBoard
    {
        readonly Dictionary<string, ChapterSpec> _specs =
            new Dictionary<string, ChapterSpec>(StringComparer.Ordinal);
        readonly HashSet<string> _appliedBeats = new HashSet<string>(StringComparer.Ordinal);

        public string ActiveChapterId { get; private set; } = string.Empty;
        public ulong ChapterStartDayIndex { get; private set; }
        public bool HasActive => !string.IsNullOrEmpty(ActiveChapterId);

        public IReadOnlyDictionary<string, ChapterSpec> Specs => _specs;

        public void Register(ChapterSpec spec)
        {
            if (spec == null || string.IsNullOrEmpty(spec.Id))
                throw new ArgumentException("ChapterSpec requires Id.");
            _specs[spec.Id] = spec;
        }

        public bool TryGet(string id, out ChapterSpec spec) =>
            _specs.TryGetValue(id ?? string.Empty, out spec);

        public bool TryGetActive(out ChapterSpec spec)
        {
            spec = null;
            return HasActive && _specs.TryGetValue(ActiveChapterId, out spec);
        }

        public void Activate(string chapterId, ulong startDayIndex)
        {
            ActiveChapterId = chapterId ?? string.Empty;
            ChapterStartDayIndex = startDayIndex;
            _appliedBeats.Clear();
        }

        public bool HasAppliedBeat(string chapterId, int dayIndex) =>
            _appliedBeats.Contains(BeatKey(chapterId, dayIndex));

        public void MarkBeatApplied(string chapterId, int dayIndex) =>
            _appliedBeats.Add(BeatKey(chapterId, dayIndex));

        static string BeatKey(string chapterId, int dayIndex) =>
            (chapterId ?? string.Empty) + ":" + dayIndex;
    }
}
