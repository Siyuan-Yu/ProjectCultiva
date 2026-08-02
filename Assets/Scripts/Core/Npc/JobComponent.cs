using XianXia.Core.Entities;

namespace XianXia.Core.Npc
{
    /// <summary>Session binding of an entity to a JobDefinition (not in Snapshot v1).</summary>
    public sealed class JobComponent : IComponent
    {
        public string JobId { get; set; } = string.Empty;
        /// <summary>Index into route WorkAreaIds for patrol／inspect.</summary>
        public int RouteIndex { get; set; }

        public bool HasJob => !string.IsNullOrEmpty(JobId);

        public void Assign(string jobId)
        {
            JobId = jobId ?? string.Empty;
            RouteIndex = 0;
        }
    }
}
