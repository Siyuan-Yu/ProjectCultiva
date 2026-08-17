using System;
using System.Collections.Generic;

namespace XianXia.Core.World.Strategic
{
    public sealed class FactionDiplomacyBoard
    {
        readonly Dictionary<string, FactionStance> _pairStance =
            new Dictionary<string, FactionStance>(StringComparer.Ordinal);

        static string PairKey(string a, string b)
        {
            if (string.CompareOrdinal(a, b) <= 0)
                return a + "|" + b;
            return b + "|" + a;
        }

        public FactionStance GetStance(string factionA, string factionB)
        {
            if (string.IsNullOrEmpty(factionA) || string.IsNullOrEmpty(factionB))
                return FactionStance.Neutral;
            if (string.Equals(factionA, factionB, StringComparison.Ordinal))
                return FactionStance.Friendly;
            return _pairStance.TryGetValue(PairKey(factionA, factionB), out var s) ? s : FactionStance.Neutral;
        }

        public void SetStance(string factionA, string factionB, FactionStance stance)
        {
            if (string.IsNullOrEmpty(factionA) || string.IsNullOrEmpty(factionB))
                return;
            _pairStance[PairKey(factionA, factionB)] = stance;
        }

        public bool IsHostile(string factionA, string factionB)
        {
            var s = GetStance(factionA, factionB);
            return s == FactionStance.Hostile || s == FactionStance.War;
        }
    }
}
