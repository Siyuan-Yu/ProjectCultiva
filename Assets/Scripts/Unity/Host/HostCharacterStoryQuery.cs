using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Unity.Host
{
    /// <summary>人物故事页的只读 RelationshipLedger 投影；不拥有、不修改任何故事状态。</summary>
    public static class HostCharacterStoryQuery
    {
        const int MaxEntries = 60;

        public static void Collect(
            SimulationWorld world,
            EntityId subject,
            EntityId activeCharacter,
            bool onlyActiveRelated,
            List<RelationshipEvent> output)
        {
            output.Clear();
            if (world?.Relationships == null || subject.IsNone)
                return;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var events = world.Relationships.Events;
            for (var i = events.Count - 1; i >= 0 && output.Count < MaxEntries; i--)
            {
                var evt = events[i];
                if (!Involves(evt, subject))
                    continue;
                if (onlyActiveRelated && !activeCharacter.IsNone && !Involves(evt, activeCharacter))
                    continue;

                // 同一社会原因可能写入多个 attitude axis；故事时间线只显示一次经历。
                var key = evt.Tick.Value + ":" + evt.From.Value + ":" + evt.To.Value + ":" +
                          evt.ReasonTag + ":" + (evt.CauseEventId?.Value ?? 0UL) + ":" +
                          (evt.ContextEntityId?.Value ?? 0UL);
                if (seen.Add(key))
                    output.Add(evt);
            }

            output.Sort((a, b) => a.Tick.Value.CompareTo(b.Tick.Value));
        }

        static bool Involves(RelationshipEvent evt, EntityId id) =>
            evt != null && (evt.From == id || evt.To == id ||
                            (evt.ContextEntityId.HasValue && evt.ContextEntityId.Value == id));
    }
}
