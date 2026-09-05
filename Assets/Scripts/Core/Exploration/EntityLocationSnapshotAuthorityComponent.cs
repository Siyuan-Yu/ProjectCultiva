using XianXia.Core.Entities;

namespace XianXia.Core.Exploration
{
    /// <summary>读档临时标记：新格式 Snapshot 是否明确表达了 EntityLocation 的存在与否。</summary>
    public sealed class EntityLocationSnapshotAuthorityComponent : IComponent
    {
        public bool SnapshotFieldPresent { get; set; }
    }
}
