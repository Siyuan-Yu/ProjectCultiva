using System.Collections.Generic;
using XianXia.Core.Entities;

namespace XianXia.Core.Social
{
    /// <summary>
    /// VS0.5 Phase A: personality／trait tags on a character. Presentation of differences only;
    /// mechanical biases arrive in later phases. Not Snapshot-backed yet.
    /// </summary>
    public sealed class PersonalityProfileComponent : IComponent
    {
        readonly HashSet<string> _tags = new HashSet<string>(System.StringComparer.Ordinal);

        public IReadOnlyCollection<string> Tags => _tags;

        public int Count => _tags.Count;

        public bool HasTag(string tag) =>
            !string.IsNullOrEmpty(tag) && _tags.Contains(tag);

        public void SetTags(IEnumerable<string> tags)
        {
            _tags.Clear();
            if (tags == null)
                return;
            foreach (var tag in tags)
            {
                if (!string.IsNullOrWhiteSpace(tag))
                    _tags.Add(tag.Trim());
            }
        }

        public bool AddTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return false;
            return _tags.Add(tag.Trim());
        }
    }
}
