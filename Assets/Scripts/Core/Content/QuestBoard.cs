using System;
using System.Collections.Generic;

namespace XianXia.Core.Content
{
    public sealed class QuestBoard
    {
        readonly Dictionary<string, QuestSpec> _specs =
            new Dictionary<string, QuestSpec>(StringComparer.Ordinal);
        readonly Dictionary<string, QuestRuntime> _runtime =
            new Dictionary<string, QuestRuntime>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, QuestSpec> Specs => _specs;
        public IReadOnlyDictionary<string, QuestRuntime> Runtime => _runtime;

        public void Register(QuestSpec spec)
        {
            if (spec == null || string.IsNullOrEmpty(spec.Id))
                throw new ArgumentException("QuestSpec requires Id.");
            _specs[spec.Id] = spec;
            if (!_runtime.ContainsKey(spec.Id))
            {
                _runtime[spec.Id] = new QuestRuntime
                {
                    QuestId = spec.Id,
                    Status = QuestStatus.Inactive
                };
            }
        }

        public bool TryGetSpec(string id, out QuestSpec spec) =>
            _specs.TryGetValue(id ?? string.Empty, out spec);

        public bool TryGet(string id, out QuestRuntime runtime) =>
            _runtime.TryGetValue(id ?? string.Empty, out runtime);
    }
}
