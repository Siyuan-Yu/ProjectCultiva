using XianXia.Core.Entities;

namespace XianXia.Core.Npc
{
    /// <summary>
    /// Runtime place-routing state (RouteIndex). JobId is legacy／optional and not required for WorkArea resolve.
    /// </summary>
    public sealed class JobComponent : IComponent
    {
        public string JobId { get; set; } = string.Empty;
        /// <summary>Index into candidate WorkAreas for patrol／inspect／explore.</summary>
        public int RouteIndex { get; set; }

        public bool HasJob => !string.IsNullOrEmpty(JobId);

        public void Assign(string jobId)
        {
            JobId = jobId ?? string.Empty;
            RouteIndex = 0;
        }
    }
}
