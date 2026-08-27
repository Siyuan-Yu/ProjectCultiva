using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    public static class ArmyAuthorityRules
    {
        public static EntityId ResolveActiveControlledCharacterId(
            PlayerPartyRuntime party,
            EntityId activeControlledCharacterId = default)
        {
            if (party != null && party.HasActive)
                return party.ActiveCharacterId;
            return activeControlledCharacterId;
        }

        public static bool IsActiveControlledCharacter(
            PlayerPartyRuntime party,
            EntityId characterId,
            EntityId activeControlledCharacterId = default)
        {
            if (characterId.IsNone)
                return false;
            var activeId = ResolveActiveControlledCharacterId(party, activeControlledCharacterId);
            return !activeId.IsNone && activeId == characterId;
        }

        public static bool IsPlayerPartyMember(PlayerPartyRuntime party, EntityId characterId) =>
            party != null && !characterId.IsNone && party.IsMember(characterId);

        public static bool TryValidateNotActive(
            PlayerPartyRuntime party,
            EntityId characterId,
            out string error,
            EntityId activeControlledCharacterId = default)
        {
            error = string.Empty;
            if (!IsActiveControlledCharacter(party, characterId, activeControlledCharacterId))
                return true;
            error = "Active controlled character cannot join FormalArmy.";
            return false;
        }

        /// <summary>PlayerParty 成员不得被 FormalArmy 征召；须先由玩家主动 Leave Party。</summary>
        public static bool TryValidateNotPlayerPartyMember(
            PlayerPartyRuntime party,
            EntityId characterId,
            out string error)
        {
            error = string.Empty;
            if (!IsPlayerPartyMember(party, characterId))
                return true;
            error = "PlayerParty member must leave the party before joining FormalArmy.";
            return false;
        }
    }
}
