using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    public sealed class JobActivityBindingEntry
    {
        public string Activity { get; set; }
        public List<string> WorkAreaIds { get; set; } = new List<string>();
        /// <summary>single | route</summary>
        public string Mode { get; set; } = "single";
    }

    public sealed class JobContentDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; }
        public string PrimaryWorkAreaId { get; set; }
        public List<JobActivityBindingEntry> ActivityBindings { get; set; } = new List<JobActivityBindingEntry>();
    }
}
