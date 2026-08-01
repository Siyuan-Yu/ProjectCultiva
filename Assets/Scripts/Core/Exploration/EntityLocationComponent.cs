using XianXia.Core.Entities;

namespace XianXia.Core.Exploration
{
    /// <summary>Current abstract world location (session; not Snapshot v1).</summary>
    public sealed class EntityLocationComponent : IComponent
    {
        public string LocationId { get; set; } = string.Empty;

        public bool HasLocation => !string.IsNullOrEmpty(LocationId);
    }
}
