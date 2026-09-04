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
        public bool PlayerControllable { get; set; }
        public Dictionary<string, bool> ActivityCapabilities { get; set; } = new Dictionary<string, bool>();
        public Dictionary<string, int> ActivityPriorities { get; set; } = new Dictionary<string, int>();
        public List<string> PreferredWorkAreaIds { get; set; } = new List<string>();
        /// <summary>Assigned housing work area for Rest／Eat.</summary>
        public string HomeWorkAreaId { get; set; } = string.Empty;
        /// <summary>Spirit-root affinities Fire/Metal/… → 0–30 (2B).</summary>
        public Dictionary<string, int> SpiritRoots { get; set; } = new Dictionary<string, int>();
        public string Hometown { get; set; } = string.Empty;
        public int Reputation { get; set; }
        public List<string> Goals { get; set; } = new List<string>();
        public List<string> Desires { get; set; } = new List<string>();
        /// <summary>击倒该角色时 setEncounterCleared 的遭遇 id。</summary>
        public string DefeatEncounterId { get; set; } = string.Empty;

        /// <summary>
        /// 人物「默认」初始势力（作者身份设定，不是 Runtime 当前势力真源）。
        /// Scenario/Roster Spawn 缺省继承；特殊场景用 Spawn.factionMode=Override 覆盖。
        /// 空 = 该人物默认无势力。运行后实际归属一律读 FactionMembershipComponent。
        /// </summary>
        public string DefaultFactionId { get; set; } = string.Empty;

        /// <summary>默认势力身份（FactionRoleKind 文本）；DefaultFactionId 非空时必须有非 None 值。</summary>
        public string DefaultFactionRole { get; set; } = string.Empty;

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
