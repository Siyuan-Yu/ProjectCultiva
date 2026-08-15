using XianXia.Core.Entities;

namespace XianXia.Core.Combat
{
    /// <summary>击倒该实体时写入 encounter:{EncounterId} 旗标。</summary>
    public sealed class EncounterLinkComponent : IComponent
    {
        public string EncounterId { get; set; } = string.Empty;
    }
}
