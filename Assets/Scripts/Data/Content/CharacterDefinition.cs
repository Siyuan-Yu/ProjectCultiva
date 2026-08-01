using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Content-only character template. No gameplay calculation.
    /// </summary>
    public sealed class CharacterDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; }
        public string DisplayNameKey { get; set; }
        public string NameKey { get; set; }
        public Dictionary<string, int> BaseAttributes { get; set; } = new Dictionary<string, int>();
        /// <summary>Legacy／misc tags (still loaded).</summary>
        public List<string> Tags { get; set; } = new List<string>();
        public List<string> PersonalityTags { get; set; } = new List<string>();
        public List<string> BackgroundTags { get; set; } = new List<string>();
        public List<string> TalentTags { get; set; } = new List<string>();
        public string SpiritRootPlaceholder { get; set; }
        public string InitialRealmPlaceholder { get; set; }

        /// <summary>Merged content tags applied to PersonalityProfile on spawn.</summary>
        public IEnumerable<string> EnumerateProfileTags()
        {
            foreach (var t in PersonalityTags)
                yield return t;
            foreach (var t in BackgroundTags)
                yield return t;
            foreach (var t in TalentTags)
                yield return t;
            foreach (var t in Tags)
                yield return t;
        }
    }
}
