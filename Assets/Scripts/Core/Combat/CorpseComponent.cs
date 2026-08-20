using XianXia.Core.Entities;

namespace XianXia.Core.Combat
{
    /// <summary>尸体留存至 RemoveAfterTick，到期后实体标记 Removed。</summary>
    public sealed class CorpseComponent : IComponent
    {
        public ulong RemoveAfterTick { get; set; }
    }
}
