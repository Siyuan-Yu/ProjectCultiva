using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    public sealed class FacilityDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; }
        public string LaborResourceId { get; set; }
        public int LaborAmountPerWorker { get; set; }
        public string GatherResourceId { get; set; }
        public int GatherAmountPerWorker { get; set; }
        public int CultivateProgressBonusPerWorker { get; set; }
    }
}
