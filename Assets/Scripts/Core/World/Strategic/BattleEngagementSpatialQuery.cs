using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    public enum PartyHexAuthoritySource
    {
        None = 0,
        PartyTravel = 1,
        CharacterPresence = 2,
        FormalArmy = 3,
    }

    /// <summary>
    /// Phase 4 空间 Authority：使用已提交 Hex Step（非 ContinuousWorldPosition 派生格）。
    /// PlayerParty 与 WorldMap Marker 共用 PlayerPartyTravel，禁止回退到 stale WorldPresence。
    /// </summary>
    public static class BattleEngagementSpatialQuery
    {
        public static bool TryGetCommittedArmyHex(
            SimulationWorld world,
            FormalArmy army,
            out HexCoord hex)
        {
            hex = default;
            if (army == null)
                return false;

            var motion = army.WorldMotion;
            if (motion != null && motion.HasPosition)
            {
                if (motion.IsMoving && motion.TryGetActiveStepHexes(out var from, out _))
                {
                    hex = from;
                    return true;
                }

                hex = motion.CurrentHex;
                return true;
            }

            if (army.UsesHexStrategicPosition)
            {
                hex = army.CurrentHex;
                return true;
            }

            return FormalArmyWorldLocationQuery.TryResolve(
                world, army, out _, out _, out _, out hex);
        }

        public static bool TryGetCommittedArmyHex(
            SimulationWorld world,
            string formalArmyId,
            out HexCoord hex)
        {
            hex = default;
            if (world?.Strategic?.FormalArmies == null ||
                string.IsNullOrEmpty(formalArmyId) ||
                !world.Strategic.FormalArmies.TryGet(formalArmyId, out var army) ||
                army == null)
                return false;

            return TryGetCommittedArmyHex(world, army, out hex);
        }

        public static bool TryGetCommittedPartyHex(
            SimulationWorld world,
            PlayerPartyRuntime party,
            out HexCoord hex) =>
            TryGetCommittedPartyHex(world, party, out hex, out _);

        public static bool TryGetCommittedPartyHex(
            SimulationWorld world,
            PlayerPartyRuntime party,
            out HexCoord hex,
            out PartyHexAuthoritySource source)
        {
            hex = default;
            source = PartyHexAuthoritySource.None;
            if (world == null || party == null || !party.HasActive)
                return false;

            var activeId = party.ActiveCharacterId;
            if (ArmyService.TryGetArmyForCharacter(world, activeId, out var army) &&
                army != null &&
                TryGetCommittedArmyHex(world, army, out hex))
            {
                source = PartyHexAuthoritySource.FormalArmy;
                return true;
            }

            var motion = world.PlayerPartyTravel;
            if (motion != null && motion.HasPosition &&
                TryGetCommittedPartyTravelHex(world, motion, out hex))
            {
                source = PartyHexAuthoritySource.PartyTravel;
                return true;
            }

            if (!TryGetCommittedCharacterHex(world, activeId, out hex))
                return false;

            source = PartyHexAuthoritySource.CharacterPresence;
            return true;
        }

        static bool TryGetCommittedPartyTravelHex(
            SimulationWorld world,
            PlayerPartyWorldMotion motion,
            out HexCoord hex)
        {
            hex = default;
            if (motion == null || !motion.HasPosition)
                return false;

            if (motion.IsMoving && motion.TryGetActiveStepHexes(out var from, out _))
            {
                hex = from;
                return true;
            }

            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(motion.SiteId) &&
                world?.Strategic?.Sites != null)
            {
                // Phase 5S-B2-3.2：WorldSite 内具体 footprint Hex 必须从 motion.WorldPosition
                // 即时派生（canonical 权威），不再把 Site PresenceHex 当作正常 Player battle
                // eligibility authority（避免 Active 实际在 Battle SupportArea 但 PresenceHex
                // 不在 SupportArea → PlayerPartyIncluded=false → 手动按钮消失）。
                if (world.Strategic.Sites.TryGet(motion.SiteId, out var site) && site != null)
                {
                    var footprintHexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                        ? world.HexWorld.HexSize
                        : 1f;
                    if (WorldSiteSpatialMapping.TryResolveDerivedFootprintHex(
                            site, motion.WorldPosition, footprintHexSize, out var footprintHex))
                    {
                        hex = footprintHex;
                        return true;
                    }
                }

                // 仅 legacy / canonical 数据缺失时 fallback（PresenceHex），不作正常 authority。
                if (world.Strategic.Sites.TryResolveSitePresenceHex(motion.SiteId, out var siteHex))
                {
                    hex = siteHex;
                    return true;
                }
            }

            var hexSize = world?.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;
            hex = HexMath.WorldToHex(motion.WorldPosition.X, motion.WorldPosition.Y, hexSize);
            return true;
        }

        public static bool TryGetCommittedCharacterHex(
            SimulationWorld world,
            EntityId characterId,
            out HexCoord hex)
        {
            hex = default;
            if (world == null || characterId.IsNone)
                return false;

            var motion = world.PlayerPartyTravel;
            if (motion != null &&
                motion.HasPosition &&
                IsTravelingMember(motion, characterId))
            {
                if (motion.IsMoving && motion.TryGetActiveStepHexes(out var from, out _))
                {
                    hex = from;
                    return true;
                }

                hex = motion.CurrentHex;
                return true;
            }

            if (ArmyService.TryGetArmyForCharacter(world, characterId, out var army) &&
                army != null &&
                army.UsesHexStrategicPosition)
                return TryGetCommittedArmyHex(world, army, out hex);

            if (!world.WorldPresence.TryGet(characterId, out var presence) || presence == null)
                return false;

            if (presence.UsesHexPresence)
            {
                hex = presence.ResidualHex;
                return true;
            }

            if (presence.Mode == PartyWorldPresenceMode.AtSite &&
                !string.IsNullOrEmpty(presence.SiteId))
                return world.Strategic.Sites.TryResolveSitePresenceHex(presence.SiteId, out hex);

            if (presence.Mode == PartyWorldPresenceMode.AtWorldPosition &&
                presence.DerivedHexFromWorldPosition.Q != ArmyHexBattleAnchorService.InvalidHexComponent)
            {
                hex = presence.DerivedHexFromWorldPosition;
                return true;
            }

            return false;
        }

        public static bool TryGetDerivedArmyHexForDebug(
            SimulationWorld world,
            FormalArmy army,
            out HexCoord hex)
        {
            hex = default;
            if (army?.WorldMotion == null || !army.WorldMotion.HasPosition)
                return army != null && army.UsesHexStrategicPosition && (hex = army.CurrentHex) == hex;
            hex = army.WorldMotion.CurrentHex;
            return true;
        }

        static bool IsTravelingMember(PlayerPartyWorldMotion motion, EntityId characterId)
        {
            var members = motion.TravelingMembers;
            if (members == null || members.Count == 0)
                return false;
            for (var i = 0; i < members.Count; i++)
            {
                if (members[i] == characterId)
                    return true;
            }

            return false;
        }
    }
}
