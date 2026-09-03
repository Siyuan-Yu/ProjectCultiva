using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 战后 Downed / Visible Corpse 的战略 Hex Presence 收口（非 Residual Group Domain）。
    /// </summary>
    public static class StrategicResidualPresenceService
    {
        public static void PlaceCharacterAtResidualHex(
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            HexCoord oldHex = default;
            var hadOld = world.WorldPresence.TryGet(characterId, out var oldWp) &&
                         oldWp != null &&
                         oldWp.UsesHexPresence;
            if (hadOld)
                oldHex = oldWp.ResidualHex;
#endif
            world.WorldPresence.SetAtHex(characterId, encounterHex);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!hadOld || !oldHex.Equals(encounterHex))
            {
                LingeringExitPositionTrace.LogResidualHexMutation(
                    world,
                    characterId,
                    hadOld ? oldHex : default,
                    encounterHex,
                    nameof(PlaceCharacterAtResidualHex),
                    "PlaceCharacterAtResidualHex");
            }
#endif
        }

        /// <summary>
        /// AtHex Residual + 精确连续落点：ResidualHex 仍是战略归属 authority，
        /// HasContinuousWorldPosition/WorldPosX/Y 保存该角色在当前 LocalMap surface 内
        /// 倒下的精确物理位置（Host 在倒下瞬间从 EntityView local → surface mapping 得到）。
        /// Mode 保持 AtHex，不改成 BackgroundCharacter AtWorldPosition。
        /// </summary>
        public static void PlaceCharacterAtResidualWorldPosition(
            SimulationWorld world,
            EntityId characterId,
            HexCoord residualHex,
            WorldVec2 preciseWorldPosition)
        {
            if (world == null || characterId.IsNone)
                return;
            if (!world.Entities.TryGet(characterId, out var ent) || ent == null)
                return;
            if (!IsResidualLifeCandidate(world, characterId))
                return;

            world.WorldPresence.SetAtResidualWorldPosition(
                characterId,
                residualHex,
                preciseWorldPosition);
        }

        public static void ClearResidualPresence(SimulationWorld world, EntityId characterId)
        {
            if (world == null || characterId.IsNone)
                return;
            if (!world.WorldPresence.TryGet(characterId, out var wp) || wp == null)
                return;
            if (wp.Mode != PartyWorldPresenceMode.AtHex)
                return;
            world.WorldPresence.Remove(characterId);
        }

        /// <summary>从 BattleParticipantSnapshot 解析 EncounterHex 并放置（Hex 模式）。</summary>
        public static bool TryPlaceFromBattleSnapshot(
            SimulationWorld world,
            EntityId characterId,
            BattleParticipantSnapshot snap)
        {
            if (world == null || characterId.IsNone || snap == null)
                return false;
            if (!ArmyHexBattleAnchorService.IsHexAnchorMode(world))
                return false;
            if (!TryResolveEncounterHex(world, snap, out var hex))
                return false;
            PlaceCharacterAtResidualHex(world, characterId, hex);
            return true;
        }

        public static bool TryResolveEncounterHex(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            out HexCoord hex)
        {
            hex = default;
            // 本场 snap 优先：Active Encounter 结算不得被旧残留 Runtime 污染
            if (ArmyHexBattleAnchorService.TryGetBattleAnchorHex(snap, out hex) &&
                (world?.HexWorld == null || !world.HexWorld.HasGrid || world.HexWorld.Contains(hex)))
                return true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (ArmyHexBattleAnchorService.IsHexAnchorMode(world))
                LingeringExitPositionTrace.LogMissingHexAnchor(
                    world, nameof(TryResolveEncounterHex));
#endif
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
            if (!wp.UsesHexPresence)
                return false;
            hex = wp.ResidualHex;
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
            if (ArmyService.TryGetArmyForCharacter(world, characterId, out _))
            {
                System.Diagnostics.Debug.Assert(
                    false,
                    "Residual candidate still in FormalArmy: " + characterId.Value);
                return false;
            }

            if (IsRetreatingArmyMember(world, characterId))
                return false;
            if (!TryGetResidualHex(world, characterId, out _))
                return false;
            return true;
        }

        public static bool IsRetreatingArmyMember(SimulationWorld world, EntityId characterId)
        {
            if (world?.Strategic?.RetreatingArmies == null || characterId.IsNone)
                return false;
            foreach (var kv in world.Strategic.RetreatingArmies.All)
            {
                var retreat = kv.Value;
                if (retreat?.MemberCharacterIds == null)
                    continue;
                for (var i = 0; i < retreat.MemberCharacterIds.Count; i++)
                {
                    if (retreat.MemberCharacterIds[i] == characterId.Value)
                        return true;
                }
            }

            return false;
        }
    }
}
