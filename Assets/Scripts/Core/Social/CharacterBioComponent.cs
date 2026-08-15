using System.Collections.Generic;
using XianXia.Core.Entities;

namespace XianXia.Core.Social
{
    /// <summary>
    /// Content bio fields from character JSON (hometown／reputation／goals／desires).
    /// Session presentation; not Snapshot v1.
    /// </summary>
    public sealed class CharacterBioComponent : IComponent
    {
        readonly List<string> _goals = new List<string>();
        readonly List<string> _desires = new List<string>();

        public string Hometown { get; set; } = string.Empty;

        public int Reputation { get; set; }

        public IReadOnlyList<string> Goals => _goals;

        public IReadOnlyList<string> Desires => _desires;

        public void SetGoals(IEnumerable<string> goals)
        {
            _goals.Clear();
            if (goals == null)
                return;
            foreach (var g in goals)
            {
                if (!string.IsNullOrWhiteSpace(g))
                    _goals.Add(g.Trim());
            }
        }

        public void SetDesires(IEnumerable<string> desires)
        {
            _desires.Clear();
            if (desires == null)
                return;
            foreach (var d in desires)
            {
                if (!string.IsNullOrWhiteSpace(d))
                    _desires.Add(d.Trim());
            }
        }
    }
}
