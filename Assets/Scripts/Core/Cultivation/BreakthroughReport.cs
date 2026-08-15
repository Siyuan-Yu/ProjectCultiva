using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Cultivation
{
    /// <summary>一次冲击瓶颈的结算（含成败与属性差分，供弹窗展示）。</summary>
    public sealed class BreakthroughReport
    {
        public EntityId Subject { get; set; }
        public string ActorName { get; set; }
        public bool Succeeded { get; set; }
        public string FromRealmLabel { get; set; }
        public string ToRealmLabel { get; set; }
        public string Detail { get; set; }
        public int ProgressLost { get; set; }
        public List<AttributeDelta> AttributeChanges { get; } = new List<AttributeDelta>(8);

        public readonly struct AttributeDelta
        {
            public AttributeDelta(AttributeId id, int before, int after)
            {
                Id = id;
                Before = before;
                After = after;
            }

            public AttributeId Id { get; }
            public int Before { get; }
            public int After { get; }
            public int Delta => After - Before;
        }
    }
}
