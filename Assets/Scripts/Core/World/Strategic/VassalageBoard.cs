using System;
using System.Collections.Generic;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Phase I：Vassal 不可独立 Alliance；禁止套娃（第一版）。</summary>
    public sealed class VassalageBoard
    {
        readonly Dictionary<string, string> _vassalToOverlord =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, string> All => _vassalToOverlord;

        public bool TryGetOverlord(string vassalFactionId, out string overlordFactionId) =>
            _vassalToOverlord.TryGetValue(vassalFactionId ?? string.Empty, out overlordFactionId);

        public bool IsVassal(string factionId) =>
            !string.IsNullOrEmpty(factionId) && _vassalToOverlord.ContainsKey(factionId);

        public bool TryBindVassalage(string vassalFactionId, string overlordFactionId)
        {
            if (string.IsNullOrEmpty(vassalFactionId) || string.IsNullOrEmpty(overlordFactionId))
                return false;
            if (string.Equals(vassalFactionId, overlordFactionId, StringComparison.Ordinal))
                return false;
            if (_vassalToOverlord.ContainsKey(vassalFactionId))
                return false;
            if (_vassalToOverlord.ContainsKey(overlordFactionId))
                return false;
            _vassalToOverlord[vassalFactionId] = overlordFactionId;
            return true;
        }

        public void Clear() => _vassalToOverlord.Clear();
    }
}
