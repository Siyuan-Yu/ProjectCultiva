using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Phase J：战后 Captured / Escaped / RetreatingArmy（概率 DEFER）。</summary>
    public sealed class RetreatingArmy
    {
        public string RetreatingArmyId { get; set; } = string.Empty;
        public string SourceArmyId { get; set; } = string.Empty;
        public string FactionId { get; set; } = string.Empty;
        public int HexQ { get; set; } = ArmyHexBattleAnchorService.InvalidHexComponent;
        public int HexR { get; set; } = ArmyHexBattleAnchorService.InvalidHexComponent;
        readonly List<ulong> _memberCharacterIds = new List<ulong>(8);

        public bool UsesHexPosition =>
            HexQ != ArmyHexBattleAnchorService.InvalidHexComponent &&
            HexR != ArmyHexBattleAnchorService.InvalidHexComponent;

        public IReadOnlyList<ulong> MemberCharacterIds => _memberCharacterIds;

        public void SetMembers(IReadOnlyList<EntityId> members)
        {
            _memberCharacterIds.Clear();
            if (members == null)
                return;
            for (var i = 0; i < members.Count; i++)
            {
                if (!members[i].IsNone)
                    _memberCharacterIds.Add(members[i].Value);
            }
        }
    }

    public sealed class RetreatingArmyBoard
    {
        readonly Dictionary<string, RetreatingArmy> _byId =
            new Dictionary<string, RetreatingArmy>(StringComparer.Ordinal);

        ulong _nextSeq = 1;

        public string AllocateId()
        {
            var id = "retreat:" + _nextSeq;
            _nextSeq++;
            return id;
        }

        public void Register(RetreatingArmy army)
        {
            if (army == null || string.IsNullOrEmpty(army.RetreatingArmyId))
                return;
            _byId[army.RetreatingArmyId] = army;
        }

        public bool TryGet(string id, out RetreatingArmy army) =>
            _byId.TryGetValue(id ?? string.Empty, out army);

        public IReadOnlyDictionary<string, RetreatingArmy> All => _byId;

        public void Clear() => _byId.Clear();
    }
}
