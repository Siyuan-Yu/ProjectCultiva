using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Character 世界移动 Authority（Phase 2D）：同一时刻仅允许一种。</summary>
    public enum CharacterWorldMovementAuthority
    {
        None = 0,
        PlayerParty = 1,
        FormalArmy = 2,
        LoadedLocalRealtime = 3,
        BackgroundTravel = 4,
    }

    public static class CharacterWorldMovementAuthorityQuery
    {
        public static bool TryGetAuthority(
            SimulationWorld world,
            EntityId characterId,
            PlayerPartyRuntime party,
            out CharacterWorldMovementAuthority authority)
        {
            authority = CharacterWorldMovementAuthority.None;
            if (world == null || characterId.IsNone)
                return false;

            if (party != null && party.IsMember(characterId))
            {
                authority = CharacterWorldMovementAuthority.PlayerParty;
                return true;
            }

            if (ArmyService.TryGetArmyForCharacter(world, characterId, out _))
            {
                authority = CharacterWorldMovementAuthority.FormalArmy;
                return true;
            }

            if (world.LocalMap != null &&
                !string.IsNullOrEmpty(world.LocalMap.ActiveMapLayoutId) &&
                world.LocalMap.ContainsOccupant(characterId))
            {
                authority = CharacterWorldMovementAuthority.LoadedLocalRealtime;
                return true;
            }

            if (world.BackgroundCharacterTravel != null &&
                world.BackgroundCharacterTravel.IsTraveling(characterId))
            {
                authority = CharacterWorldMovementAuthority.BackgroundTravel;
                return true;
            }

            return true;
        }

        public static bool CanStartBackgroundTravel(
            SimulationWorld world,
            EntityId characterId,
            PlayerPartyRuntime party,
            out string error)
        {
            error = null;
            if (world == null || characterId.IsNone)
            {
                error = "Invalid character.";
                return false;
            }

            if (!world.Entities.TryGet(characterId, out var entity) ||
                !CombatLifeStateService.CanFight(entity))
            {
                error = "Character cannot travel (dead/dying/captured).";
                return false;
            }

            if (!TryGetAuthority(world, characterId, party, out var authority))
            {
                error = "Cannot resolve movement authority.";
                return false;
            }

            switch (authority)
            {
                case CharacterWorldMovementAuthority.None:
                    return true;
                case CharacterWorldMovementAuthority.BackgroundTravel:
                    error = "Already background traveling.";
                    return false;
                default:
                    error = "Character movement owned by " + authority + ".";
                    return false;
            }
        }

        /// <summary>DEBUG：允许从已加载 LocalMap 启动纯数据层旅行（移除 occupant）。</summary>
        public static bool CanStartBackgroundTravelDebug(
            SimulationWorld world,
            EntityId characterId,
            PlayerPartyRuntime party,
            out string error)
        {
            error = null;
            if (world == null || characterId.IsNone)
            {
                error = "Invalid character.";
                return false;
            }

            if (!world.Entities.TryGet(characterId, out var entity) ||
                !CombatLifeStateService.CanFight(entity))
            {
                error = "Character cannot travel.";
                return false;
            }

            if (party != null && party.IsMember(characterId))
            {
                error = "PlayerParty member cannot use background travel.";
                return false;
            }

            if (ArmyService.TryGetArmyForCharacter(world, characterId, out _))
            {
                error = "FormalArmy member cannot use background travel.";
                return false;
            }

            if (world.BackgroundCharacterTravel != null &&
                world.BackgroundCharacterTravel.IsTraveling(characterId))
            {
                error = "Already background traveling.";
                return false;
            }

            return true;
        }
    }
}
