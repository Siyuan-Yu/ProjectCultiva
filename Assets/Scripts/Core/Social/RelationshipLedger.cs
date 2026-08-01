using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Social
{
    /// <summary>
    /// Relationship unique source of truth. Append-only event history; scores are aggregates.
    /// Not Snapshot-backed in VS0.5-B (schema hard-stop).
    /// </summary>
    public sealed class RelationshipLedger
    {
        readonly List<RelationshipEvent> _events = new List<RelationshipEvent>();

        public int EventCount => _events.Count;

        public IReadOnlyList<RelationshipEvent> Events => _events;

        public void Append(RelationshipEvent evt)
        {
            if (evt == null)
                throw new System.ArgumentNullException(nameof(evt));
            _events.Add(evt);
        }

        /// <summary>Directed score: sum of deltas from → to.</summary>
        public int Score(EntityId from, EntityId to)
        {
            if (from.IsNone || to.IsNone || from == to)
                return 0;

            var sum = 0;
            for (var i = 0; i < _events.Count; i++)
            {
                var e = _events[i];
                if (e.From == from && e.To == to)
                    sum += e.Delta;
            }

            return sum;
        }

        public void Clear()
        {
            _events.Clear();
        }
    }
}
