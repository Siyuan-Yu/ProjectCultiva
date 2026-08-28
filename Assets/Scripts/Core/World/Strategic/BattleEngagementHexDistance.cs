using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Debug-only：Engagement 成立瞬间 BattleInitiator 位置快照；不参与 Participant 空间资格。
    /// </summary>
    public readonly struct InitiatorEngagementLocation
    {
        public HexCoord Hex { get; }
        public string SiteId { get; }
        public bool HasValue { get; }

        public InitiatorEngagementLocation(HexCoord hex, string siteId, bool hasValue)
        {
            Hex = hex;
            SiteId = siteId ?? string.Empty;
            HasValue = hasValue;
        }
    }

    /// <summary>
    /// Phase 4 接战空间：Defender PresenceHex / WorldSite Footprint 解析；Presentation 用 BattleLocationHex。
    /// Participant 空间资格由 <see cref="BattleEngagementSupportArea"/> 冻结集合判定。
    /// </summary>
    public static class BattleEngagementHexDistance
    {
        public static int HexDistanceToBattleLocation(HexCoord battleLocationHex, HexCoord unitPresenceHex) =>
            HexMath.Distance(unitPresenceHex, battleLocationHex);

        public static int HexDistanceToBattleLocation(
            SimulationWorld world,
            HexCoord battleLocationHex,
            FormalArmy army) =>
            TryResolveFormalArmyPresenceHex(world, army, out var hex)
                ? HexDistanceToBattleLocation(battleLocationHex, hex)
                : int.MaxValue;

        public static int HexDistanceToBattleLocation(
            SimulationWorld world,
            HexCoord battleLocationHex,
            PlayerPartyRuntime party) =>
            CharacterWorldPresenceQuery.TryGetPartyWorldHex(world, party, out var hex)
                ? HexDistanceToBattleLocation(battleLocationHex, hex)
                : int.MaxValue;

        /// <summary>
        /// 接战创建瞬间冻结的 BattleLocationHex：Defender 在 Engagement 成立瞬间的 PresenceHex。
        /// 普通「A 攻 B」必须取 Defender Hex，不取 Initiator / 中点 / WorldPosition。
        /// </summary>
        public static HexCoord ResolveBattleLocationHex(
            SimulationWorld world,
            string attackerArmyId,
            string defenderArmyId)
        {
            if (TryResolveDefenderEngagementHex(world, defenderArmyId, out var defenderHex))
                return defenderHex;

            if (TryResolveFormalArmyPresenceHexById(world, attackerArmyId, out var attackerHex))
                return attackerHex;

            return default;
        }

        /// <summary>Defender 在接战成立瞬间的 FormalArmy PresenceHex。</summary>
        public static bool TryResolveDefenderEngagementHex(
            SimulationWorld world,
            string defenderFormalArmyId,
            out HexCoord hex) =>
            TryResolveFormalArmyPresenceHexById(world, defenderFormalArmyId, out hex);

        public static bool TryResolveFormalArmyPresenceHex(
            SimulationWorld world,
            FormalArmy army,
            out HexCoord hex) =>
            BattleEngagementSpatialQuery.TryGetCommittedArmyHex(world, army, out hex);

        static bool TryResolveFormalArmyPresenceHexById(
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

            return TryResolveFormalArmyPresenceHex(world, army, out hex);
        }

        /// <summary>Debug / 测试：按 ArmyId 解析 PresenceHex。</summary>
        public static bool TryResolveFormalArmyPresenceHex(
            SimulationWorld world,
            string formalArmyId,
            out HexCoord hex) =>
            TryResolveFormalArmyPresenceHexById(world, formalArmyId, out hex);

        /// <summary>Debug-only：BattleInitiator 在 Engagement 创建瞬间的正式位置。</summary>
        public static InitiatorEngagementLocation ResolveInitiatorEngagementLocation(
            SimulationWorld world,
            string initiatorFormalArmyId)
        {
            if (!TryResolveFormalArmyPresenceHex(world, initiatorFormalArmyId, out var hex))
                return default;

            if (world.Strategic.FormalArmies.TryGet(initiatorFormalArmyId, out var army) &&
                army?.WorldMotion != null &&
                army.WorldMotion.LocationKind == FormalArmyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(army.WorldMotion.SiteId))
            {
                return new InitiatorEngagementLocation(
                    hex,
                    army.WorldMotion.SiteId,
                    true);
            }

            return new InitiatorEngagementLocation(hex, string.Empty, true);
        }
    }
}
