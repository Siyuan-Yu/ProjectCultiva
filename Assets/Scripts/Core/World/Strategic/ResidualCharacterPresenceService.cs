using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 战后 Downed / Visible Corpse 的个人空间收口。现代 Continuous 使用精确 Surface 位置。
    /// </summary>
    public static class ResidualCharacterPresenceService
    {
        public static void PlaceLegacyCharacterAtResidualHex(
            SimulationWorld world,
            EntityId characterId,
            HexCoord encounterHex)
        {
            if (world == null || characterId.IsNone)
                return;
            if (!world.Entities.TryGet(characterId, out var ent) || ent == null)
                return;
            if (!IsResidualLifeCandidate(world, characterId))
                return;


            world.WorldPresence.SetLegacyAtHex(characterId, encounterHex);

        }

        public static void ClearResidualPresence(SimulationWorld world, EntityId characterId)
        {
            if (world == null || characterId.IsNone)
                return;
            if (!world.WorldPresence.TryGet(characterId, out var wp) || wp == null)
                return;
            if (!IsResidualLifeCandidate(world, characterId))
                return;
            world.WorldPresence.Remove(characterId);
        }

        /// <summary>从 BattleParticipantSnapshot 解析 EncounterHex 并放置（Hex 模式）。</summary>
        public static bool TryMigrateFromLegacyBattleSnapshot(
            SimulationWorld world,
            EntityId characterId,
            BattleParticipantSnapshot snap)
        {
            if (world == null || characterId.IsNone || snap == null)
                return false;
            if (snap.HasBattleAnchorWorldPosition ||
                ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world))
                return false;
            if (!TryResolveEncounterHex(world, snap, out var hex))
                return false;
            PlaceLegacyCharacterAtResidualHex(world, characterId, hex);
            return true;
        }

        public static bool TryResolveEncounterHex(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            out HexCoord hex)
        {
            hex = default;
            // 本场 snap 优先：Active Encounter 结算不得被旧残留 Runtime 污染
            if (snap.BattleAnchorHexQ != StrategicHexConstants.InvalidHexComponent &&
                snap.BattleAnchorHexR != StrategicHexConstants.InvalidHexComponent)
            {
                hex = new HexCoord(snap.BattleAnchorHexQ, snap.BattleAnchorHexR);
                return world?.HexWorld == null || !world.HexWorld.HasGrid || world.HexWorld.Contains(hex);
            }
            return false;
        }

        public static bool TryGetResidualHex(
            SimulationWorld world,
            EntityId characterId,
            out HexCoord hex)
        {
            hex = default;
            if (world == null || characterId.IsNone)
                return false;
            if (!world.WorldPresence.TryGet(characterId, out var wp) || wp == null)
                return false;
            if (wp.HasContinuousWorldPosition)
            {
                var size = world.HexWorld != null && world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
                hex = HexMath.WorldToHex(wp.WorldPosX, wp.WorldPosY, size);
            }
            else if (wp.UsesHexPresence)
                hex = wp.ResidualHex;
            else
                return false;
            if (world.HexWorld != null && world.HexWorld.HasGrid && !world.HexWorld.Contains(hex))
                return false;
            return true;
        }

        /// <summary>LifeState 候选：Incapacitated 或 Dead+VisibleCorpse；排除 Removed / Captured。</summary>
        public static bool IsResidualLifeCandidate(SimulationWorld world, EntityId characterId)
        {
            if (world == null || characterId.IsNone)
                return false;
            if (!world.Entities.TryGet(characterId, out var ent) || ent == null)
                return false;
            if (!ent.TryGet<LifecycleComponent>(out var life) || life == null || life.IsRemoved)
                return false;
            if (life.State == LifecycleState.Captured)
                return false;
            if (life.IsIncapacitated)
                return true;
            return Combat.CombatLifeStateService.HasVisibleCorpse(ent);
        }

        /// <summary>
        /// 正式 Residual Candidate：Life + 非 FormalArmy + 非 Captured/Escaped/Retreating + 合法 Hex Presence。
        /// </summary>
        public static bool IsStrategicResidualCandidate(SimulationWorld world, EntityId characterId)
        {
            if (!IsResidualLifeCandidate(world, characterId))
                return false;
            // Squad/legacy Army identity does not authorize moving an incapacitated body.
            if (ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world) &&
                ResidualSpatialAuthorityService.TryResolveStableResidualSpatialAuthority(
                    world, characterId, out var authority) && authority.HasPrecisePosition &&
                !string.IsNullOrEmpty(authority.SurfaceId))
                return true;
            return TryGetResidualHex(world, characterId, out _);
        }

    }
}


