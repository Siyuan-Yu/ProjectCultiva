using System;
using System.Collections.Generic;

namespace XianXia.Core.Content
{
    public sealed class ContentEventBoard
    {
        readonly Dictionary<string, ContentEventSpec> _specs =
            new Dictionary<string, ContentEventSpec>(StringComparer.Ordinal);
        readonly HashSet<string> _fired = new HashSet<string>(StringComparer.Ordinal);

        public string ActiveEventId { get; private set; } = string.Empty;

        public IReadOnlyDictionary<string, ContentEventSpec> Specs => _specs;

        public bool HasActive => !string.IsNullOrEmpty(ActiveEventId);

        public void Register(ContentEventSpec spec)
        {
            if (spec == null || string.IsNullOrEmpty(spec.Id))
                throw new ArgumentException("ContentEventSpec requires Id.");
            _specs[spec.Id] = spec;
        }

        public bool TryGet(string id, out ContentEventSpec spec) =>
            _specs.TryGetValue(id ?? string.Empty, out spec);

        public bool HasFired(string id) => _fired.Contains(id ?? string.Empty);

        public void MarkFired(string id)
        {
            if (!string.IsNullOrEmpty(id))
                _fired.Add(id);
        }

        public void SetActive(string id) => ActiveEventId = id ?? string.Empty;

        public void ClearActive() => ActiveEventId = string.Empty;

        internal void CaptureState(out string active, out List<string> fired)
        {
            active = ActiveEventId;
            fired = new List<string>(_fired);
        }

        internal void RestoreState(string active, IEnumerable<string> fired)
        {
            ActiveEventId = active ?? string.Empty;
            _fired.Clear();
            if (fired != null) foreach (var id in fired) _fired.Add(id);
        }
    }
}

