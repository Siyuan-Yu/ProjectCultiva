using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;

namespace XianXia.Core.Cultivation
{
    /// <summary>
    /// Runtime cultivation state. Progress caps at BreakthroughProgressRequired until player breakthrough.
    /// </summary>
    public sealed class CultivationComponent : IComponent
    {
        public RealmStage Realm { get; set; } = RealmStage.Mortal;

        /// <summary>感应境 0/1/2；炼气 1–10；筑基 0。</summary>
        public int MinorStage { get; set; }

        public int Progress { get; set; }

        public int BreakthroughProgressRequired { get; set; }

        public int CultivationSpeed { get; set; }

        public DefinitionId? LearnedManualId { get; set; }

        public string RequiredRealmName { get; set; } = nameof(RealmStage.Mortal);

        public bool HasLearnedManual => LearnedManualId.HasValue;

        public bool IsAtBottleneck =>
            BreakthroughProgressRequired > 0 && Progress >= BreakthroughProgressRequired;
    }
}
