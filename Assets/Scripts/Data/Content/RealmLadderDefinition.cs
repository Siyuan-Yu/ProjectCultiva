using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    public sealed class RealmLadderDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<RealmLadderStepDefinition> Steps { get; } = new List<RealmLadderStepDefinition>();
    }

    public sealed class RealmLadderStepDefinition
    {
        public string FromRealm { get; set; } = string.Empty;
        public int FromMinor { get; set; }
        public string ToRealm { get; set; } = string.Empty;
        public int ToMinor { get; set; }
        public int ProgressRequired { get; set; }
        public int SuccessPercent { get; set; } = 95;
        public bool MajorRealmJump { get; set; }
        public int GrantSpiritPower { get; set; }
        public Dictionary<string, int> Bonuses { get; } = new Dictionary<string, int>();
    }
}
