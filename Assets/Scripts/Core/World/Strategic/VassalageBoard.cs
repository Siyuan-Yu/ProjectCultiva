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

        /// <summary>
        /// 正式解除一条附庸关系。可选指定宗主，避免调用方误解除已经变化的关系。
        /// 这是最小的通用 mutation；不附带宣战、联盟或其它外交副作用。
        /// </summary>
        public bool TryReleaseVassalage(string vassalFactionId, string expectedOverlordFactionId = null)
        {
            if (string.IsNullOrEmpty(vassalFactionId) ||
                !_vassalToOverlord.TryGetValue(vassalFactionId, out var overlordFactionId))
                return false;

            if (!string.IsNullOrEmpty(expectedOverlordFactionId) &&
                !string.Equals(overlordFactionId, expectedOverlordFactionId, StringComparison.Ordinal))
                return false;

            return _vassalToOverlord.Remove(vassalFactionId);
        }

        public void Clear() => _vassalToOverlord.Clear();
    }
}
