using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    public sealed class WorkAreaContentDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; }
        public string LocationId { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public List<string> AllowedActivities { get; set; } = new List<string>();
        public float OffsetX { get; set; }
        public float OffsetZ { get; set; }
        /// <summary>Max concurrent soft slots; default 4 when unset.</summary>
        public int Capacity { get; set; } = 4;
        /// <summary>Housing admission tags (mortal／guard／supervisor…); empty = open.</summary>
        public List<string> ResidentTags { get; set; } = new List<string>();
        public bool IsControlCore { get; set; }
        public int MaxDurability { get; set; }
        /// <summary>Control-core flat defense (damage reduction per strike).</summary>
        public int Defense { get; set; }
        public float OccupyHoldSeconds { get; set; } = 10f;
        public List<string> GrantsPrivileges { get; set; } = new List<string>();
    }
}
