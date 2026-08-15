using System.Collections.Generic;
using XianXia.Core.Attributes;

namespace XianXia.Core.Cultivation
{
    /// <summary>One configurable breakthrough step on the realm ladder.</summary>
    public sealed class RealmLadderStep
    {
        public RealmStage FromRealm { get; set; }
        public int FromMinor { get; set; }
        public RealmStage ToRealm { get; set; }
        public int ToMinor { get; set; }
        public int ProgressRequired { get; set; }
        /// <summary>0–100 base success chance before comprehension tweak.</summary>
        public int SuccessPercent { get; set; } = 95;
        public bool MajorRealmJump { get; set; }
        /// <summary>Set SpiritPower base to this on success when &gt; 0 (炼气起灵力).</summary>
        public int GrantSpiritPower { get; set; }
        public Dictionary<AttributeId, int> AttributeBonuses { get; } =
            new Dictionary<AttributeId, int>();
    }
}
