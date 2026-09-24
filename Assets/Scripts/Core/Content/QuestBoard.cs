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
        public ulong NextInstanceSequence { get; private set; } = 1;

        public IReadOnlyDictionary<string, QuestSpec> Specs => _specs;
        public IReadOnlyDictionary<string, QuestRuntime> Runtime => _runtime;

        public void Register(QuestSpec spec)
        {
            if (spec == null || string.IsNullOrEmpty(spec.Id))
                throw new ArgumentException("QuestSpec requires Id.");
            _specs[spec.Id] = spec;
            if (!spec.IsCharacterCommission && !_runtime.ContainsKey(spec.Id))
            {
                _runtime[spec.Id] = new QuestRuntime
                {
                    QuestInstanceId = spec.Id,
                    QuestId = spec.Id,
                    Status = QuestStatus.Inactive
                };
            }
        }

        public bool TryGetSpec(string id, out QuestSpec spec) =>
            _specs.TryGetValue(id ?? string.Empty, out spec);

        public bool TryGet(string id, out QuestRuntime runtime) =>
            _runtime.TryGetValue(id ?? string.Empty, out runtime);

        public bool TryGetForIssuer(string questDefinitionId, XianXia.Core.Domain.Ids.EntityId issuer, out QuestRuntime runtime)
        {
            foreach (var candidate in _runtime.Values)
                if (candidate.IssuerEntityId == issuer &&
                    string.Equals(candidate.QuestId, questDefinitionId, StringComparison.Ordinal))
                { runtime = candidate; return true; }
            runtime = null;
            return false;
        }

        public QuestRuntime CreateCommission(string questDefinitionId, XianXia.Core.Domain.Ids.EntityId issuer,
            string opportunityInstanceId, string issuerDisplayName)
        {
            var id = "quest-instance:" + NextInstanceSequence++;
            var runtime = new QuestRuntime
            {
                QuestInstanceId = id,
                QuestId = questDefinitionId ?? string.Empty,
                IssuerEntityId = issuer,
                SourceOpportunityInstanceId = opportunityInstanceId ?? string.Empty,
                IssuerDisplayName = issuerDisplayName ?? string.Empty,
                Status = QuestStatus.Inactive
            };
            _runtime.Add(id, runtime);
            return runtime;
        }

        internal RuntimeState CaptureRuntime()
        {
            var copy = new Dictionary<string, QuestRuntime>(StringComparer.Ordinal);
            foreach (var pair in _runtime) copy[pair.Key] = Clone(pair.Value);
            return new RuntimeState(copy, NextInstanceSequence);
        }

        internal void RestoreRuntime(IReadOnlyDictionary<string, QuestRuntime> state, ulong nextInstanceSequence = 1)
        {
            _runtime.Clear();
            if (state != null) foreach (var pair in state) _runtime[pair.Key] = Clone(pair.Value);
            NextInstanceSequence = nextInstanceSequence == 0 ? 1 : nextInstanceSequence;
        }

        internal void RestoreRuntime(RuntimeState state) =>
            RestoreRuntime(state?.Runtime, state?.NextInstanceSequence ?? 1);

        internal sealed class RuntimeState
        {
            public RuntimeState(Dictionary<string, QuestRuntime> runtime, ulong nextInstanceSequence)
            { Runtime = runtime; NextInstanceSequence = nextInstanceSequence; }
            public Dictionary<string, QuestRuntime> Runtime { get; }
            public ulong NextInstanceSequence { get; }
        }

        static QuestRuntime Clone(QuestRuntime q) => new QuestRuntime
        {
            QuestInstanceId = q.QuestInstanceId, QuestId = q.QuestId,
            IssuerEntityId = q.IssuerEntityId, SourceOpportunityInstanceId = q.SourceOpportunityInstanceId,
            IssuerDisplayName = q.IssuerDisplayName, AcceptedByEntityId = q.AcceptedByEntityId,
            Status = q.Status, ProgressCount = q.ProgressCount,
            ProgressMax = q.ProgressMax, AcceptedAtDayIndex = q.AcceptedAtDayIndex,
            DeadlineDayIndexExclusive = q.DeadlineDayIndexExclusive,
            DeliveryCompleted = q.DeliveryCompleted, FailureReason = q.FailureReason
        };
    }
}
