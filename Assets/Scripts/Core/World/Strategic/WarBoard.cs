using System;
using System.Collections.Generic;

namespace XianXia.Core.World.Strategic
{
    public sealed class WarBoard
    {
        readonly Dictionary<string, War> _wars = new Dictionary<string, War>(StringComparer.Ordinal);
        ulong _nextWarSeq = 1;

        public IReadOnlyDictionary<string, War> All => _wars;

        public string AllocateWarId()
        {
            var id = "war:" + _nextWarSeq;
            _nextWarSeq++;
            return id;
        }

        public void Register(War war)
        {
            if (war == null || string.IsNullOrEmpty(war.WarId))
                return;
            _wars[war.WarId] = war;
        }

        public bool TryGet(string warId, out War war) =>
            _wars.TryGetValue(warId ?? string.Empty, out war);

        public void Clear() => _wars.Clear();

        public IEnumerable<War> EnumerateActive()
        {
            foreach (var kv in _wars)
            {
                if (kv.Value != null && kv.Value.Active)
                    yield return kv.Value;
            }
        }
    }
}
