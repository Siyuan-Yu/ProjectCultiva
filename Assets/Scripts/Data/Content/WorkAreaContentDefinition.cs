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
    }
}
