using System;
using System.Collections.Generic;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Phase G：最小 Active War 运行时实体（无 War Score／Goal）。</summary>
    public sealed class War
    {
        public string WarId { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        readonly HashSet<string> _attackers = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _defenders = new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlyCollection<string> Attackers => _attackers;
        public IReadOnlyCollection<string> Defenders => _defenders;

        public void AddAttacker(string factionId)
        {
            if (!string.IsNullOrEmpty(factionId))
                _attackers.Add(factionId);
        }

        public void AddDefender(string factionId)
        {
            if (!string.IsNullOrEmpty(factionId))
                _defenders.Add(factionId);
        }

        public bool Involves(string factionId)
        {
            if (string.IsNullOrEmpty(factionId))
                return false;
            return _attackers.Contains(factionId) || _defenders.Contains(factionId);
        }

        public bool IsAttacker(string factionId) =>
            !string.IsNullOrEmpty(factionId) && _attackers.Contains(factionId);

        public bool IsDefender(string factionId) =>
            !string.IsNullOrEmpty(factionId) && _defenders.Contains(factionId);
    }
}
