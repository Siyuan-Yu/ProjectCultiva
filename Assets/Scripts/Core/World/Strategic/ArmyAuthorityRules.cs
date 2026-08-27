using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    public static class ArmyAuthorityRules
    {
        public static bool IsActiveControlledCharacter(PlayerPartyRuntime party, EntityId characterId) =>
            party != null && party.HasActive && party.ActiveCharacterId == characterId;

        public static bool TryValidateNotActive(PlayerPartyRuntime party, EntityId characterId, out string error)
        {
            error = string.Empty;
            if (!IsActiveControlledCharacter(party, characterId))
                return true;
            error = "Active controlled character cannot join FormalArmy.";
            return false;
        }
    }
}
