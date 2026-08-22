using System;
using System.Collections.Generic;

namespace XianXia.Core.World.Strategic
{
    public sealed class FormalArmyBoard
    {
        readonly Dictionary<string, FormalArmy> _armies =
            new Dictionary<string, FormalArmy>(StringComparer.Ordinal);

        int _nextSequence = 1;

        public IReadOnlyDictionary<string, FormalArmy> Armies => _armies;

        public void Clear()
        {
            _armies.Clear();
            _nextSequence = 1;
        }

        public string AllocateArmyId()
        {
            while (true)
            {
                var id = "army:formal_" + _nextSequence;
                _nextSequence++;
                if (!_armies.ContainsKey(id))
                    return id;
            }
        }

        internal FormalArmy Register(FormalArmy army)
        {
            if (army == null || string.IsNullOrEmpty(army.ArmyId))
                throw new ArgumentException("FormalArmy requires ArmyId.");
            _armies[army.ArmyId] = army;
            return army;
        }

        public bool TryGet(string armyId, out FormalArmy army) =>
            _armies.TryGetValue(armyId ?? string.Empty, out army);

        internal void Remove(string armyId)
        {
            if (!string.IsNullOrEmpty(armyId))
                _armies.Remove(armyId);
        }

        public bool TryGetArmyForCharacter(ulong characterId, out FormalArmy army)
        {
            army = null;
            if (characterId == 0)
                return false;
            foreach (var kv in _armies)
            {
                var a = kv.Value;
                if (a != null && a.ContainsMember(new Domain.Ids.EntityId(characterId)))
                {
                    army = a;
                    return true;
                }
            }

            return false;
        }
    }
}
