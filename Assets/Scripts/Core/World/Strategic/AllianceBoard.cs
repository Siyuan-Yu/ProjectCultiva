using System;
using System.Collections.Generic;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Phase I：独立 Faction 最多加入 1 个 Alliance；成员战争绑定。</summary>
    public sealed class AllianceBoard
    {
        readonly Dictionary<string, string> _factionToAlliance =
            new Dictionary<string, string>(StringComparer.Ordinal);

        readonly Dictionary<string, HashSet<string>> _allianceMembers =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        ulong _nextAllianceSeq = 1;

        public string AllocateAllianceId()
        {
            var id = "alliance:" + _nextAllianceSeq;
            _nextAllianceSeq++;
            return id;
        }

        public bool TryGetAllianceId(string factionId, out string allianceId) =>
            _factionToAlliance.TryGetValue(factionId ?? string.Empty, out allianceId);

        /// <summary>双方同属一个正式 Alliance（只读）。</summary>
        public bool AreAllied(string factionA, string factionB)
        {
            if (string.IsNullOrEmpty(factionA) || string.IsNullOrEmpty(factionB))
                return false;
            if (string.Equals(factionA, factionB, StringComparison.Ordinal))
                return false;
            if (!_factionToAlliance.TryGetValue(factionA, out var allianceA) ||
                string.IsNullOrEmpty(allianceA))
                return false;
            if (!_factionToAlliance.TryGetValue(factionB, out var allianceB) ||
                string.IsNullOrEmpty(allianceB))
                return false;
            return string.Equals(allianceA, allianceB, StringComparison.Ordinal);
        }

        public List<string> GetAllianceMembers(string factionId)
        {
            var list = new List<string>(4);
            if (string.IsNullOrEmpty(factionId))
                return list;
            if (!_factionToAlliance.TryGetValue(factionId, out var allianceId) ||
                !_allianceMembers.TryGetValue(allianceId, out var members))
                return list;
            foreach (var member in members)
                list.Add(member);
            return list;
        }

        public bool FormAlliance(string factionA, string factionB, out string allianceId)
        {
            allianceId = string.Empty;
            if (string.IsNullOrEmpty(factionA) || string.IsNullOrEmpty(factionB))
                return false;
            if (string.Equals(factionA, factionB, StringComparison.Ordinal))
                return false;
            if (_factionToAlliance.ContainsKey(factionA) || _factionToAlliance.ContainsKey(factionB))
                return false;

            allianceId = AllocateAllianceId();
            var members = new HashSet<string>(StringComparer.Ordinal) { factionA, factionB };
            _allianceMembers[allianceId] = members;
            _factionToAlliance[factionA] = allianceId;
            _factionToAlliance[factionB] = allianceId;
            return true;
        }

        public IReadOnlyDictionary<string, HashSet<string>> All => _allianceMembers;

        public void RestoreAlliance(string allianceId, IList<string> members)
        {
            if (string.IsNullOrEmpty(allianceId) || members == null || members.Count < 2)
                return;
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < members.Count; i++)
            {
                if (string.IsNullOrEmpty(members[i]))
                    continue;
                set.Add(members[i]);
                _factionToAlliance[members[i]] = allianceId;
            }

            _allianceMembers[allianceId] = set;
        }

        public void Clear()
        {
            _factionToAlliance.Clear();
            _allianceMembers.Clear();
        }
    }
}
