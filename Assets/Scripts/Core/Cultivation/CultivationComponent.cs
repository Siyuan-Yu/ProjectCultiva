using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;

namespace XianXia.Core.Cultivation
{
    /// <summary>
    /// Runtime cultivation state. Progress / breakthrough rules live in Core, not Data.
    /// </summary>
    public sealed class CultivationComponent : IComponent
    {
        public RealmStage Realm { get; set; } = RealmStage.Mortal;

        public int Progress { get; set; }

        public int BreakthroughProgressRequired { get; set; }

        public int CultivationSpeed { get; set; }

        public DefinitionId? LearnedManualId { get; set; }

        public string RequiredRealmName { get; set; } = nameof(RealmStage.Mortal);

        public bool HasLearnedManual => LearnedManualId.HasValue;
    }
}
