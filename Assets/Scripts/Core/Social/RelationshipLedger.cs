using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Social
{
    /// <summary>
    /// 五维角色态度唯一真源。事件流 append-only，并由 Snapshot 完整持久化。
    /// </summary>
    public sealed class RelationshipLedger
    {
        readonly List<RelationshipEvent> _events = new List<RelationshipEvent>();
        readonly Dictionary<(ulong, ulong), bool> _privateHostility =
            new Dictionary<(ulong, ulong), bool>();

        public int EventCount => _events.Count;

        public IReadOnlyList<RelationshipEvent> Events => _events;

        public void Append(RelationshipEvent evt)
        {
            if (evt == null)
                throw new System.ArgumentNullException(nameof(evt));
            _events.Add(evt);
            IndexPrivateHostility(evt);
        }

        public bool HasPrivateHostility(EntityId from, EntityId to) =>
            !from.IsNone && !to.IsNone &&
            _privateHostility.TryGetValue((from.Value, to.Value), out var active) && active;

        void IndexPrivateHostility(RelationshipEvent evt)
        {
            if (evt.ReasonTag == "private_hostility_start")
                _privateHostility[(evt.From.Value, evt.To.Value)] = true;
            else if (evt.ReasonTag == "private_hostility_end")
                _privateHostility[(evt.From.Value, evt.To.Value)] = false;
        }

        /// <summary>兼容 API：Score 正式等于 Affection。</summary>
        public int Score(EntityId from, EntityId to)
            => GetValue(from, to, SocialAttitudeAxis.Affection);

        public int GetValue(EntityId from, EntityId to, SocialAttitudeAxis axis)
        {
            if (from.IsNone || to.IsNone || from == to)
                return 0;

            var sum = 0;
            for (var i = 0; i < _events.Count; i++)
            {
                var e = _events[i];
                if (e.From == from && e.To == to && e.Axis == axis)
                    sum += e.Delta;
            }

            return SocialAttitudeRules.Clamp(axis, sum);
        }

        public SocialAttitude GetAttitude(EntityId from, EntityId to) =>
            new SocialAttitude(
                GetValue(from, to, SocialAttitudeAxis.Affection),
                GetValue(from, to, SocialAttitudeAxis.Trust),
                GetValue(from, to, SocialAttitudeAxis.Respect),
                GetValue(from, to, SocialAttitudeAxis.Fear),
                GetValue(from, to, SocialAttitudeAxis.Grudge));

        public void Clear()
        {
            _events.Clear();
            _privateHostility.Clear();
        }

        internal void Truncate(int eventCount)
        {
            if (eventCount < 0) eventCount = 0;
            if (eventCount < _events.Count)
            {
                _events.RemoveRange(eventCount, _events.Count - eventCount);
                _privateHostility.Clear();
                for (var i = 0; i < _events.Count; i++) IndexPrivateHostility(_events[i]);
            }
        }
    }
}
