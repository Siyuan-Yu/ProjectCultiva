namespace XianXia.Core.Settlement
{
    /// <summary>Runtime facility installed on a settlement.</summary>
    public sealed class FacilityRuntime
    {
        public string FacilityId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        /// <summary>Resource produced by Gather role at day end (optional).</summary>
        public string GatherResourceId { get; set; } = string.Empty;
        public int GatherAmountPerWorker { get; set; }
        /// <summary>Resource produced by Labor role at day end (optional).</summary>
        public string LaborResourceId { get; set; } = string.Empty;
        public int LaborAmountPerWorker { get; set; }
        /// <summary>Extra cultivation Progress granted to Cultivate-role members at day end.</summary>
        public int CultivateProgressBonusPerWorker { get; set; }
    }
}
