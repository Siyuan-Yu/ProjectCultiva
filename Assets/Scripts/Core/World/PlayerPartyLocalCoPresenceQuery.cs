using System;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.World
{
    /// <summary>PlayerParty join 的空间判定模式。</summary>
    public enum PlayerPartyCoPresenceScope
    {
        /// <summary>无空间 gate（world 未建立；既有 null-world 语义）。</summary>
        Ungated = 0,

        /// <summary>Continuous Outdoor：同一个 active Continuous Surface presentation scope。</summary>
        ContinuousOutdoorPresentation = 1,

        /// <summary>Legacy LocalMap／Interior／Cave：仍要求同一 LocalMap occupant。</summary>
        LegacyLocalMap = 2
    }

    public readonly struct PlayerPartyCoPresenceResult
    {
        public PlayerPartyCoPresenceResult(
            bool isCoPresent,
            PlayerPartyCoPresenceScope scope,
            string reason,
            string playerMessage)
        {
            IsCoPresent = isCoPresent;
            Scope = scope;
            Reason = reason ?? string.Empty;
            PlayerMessage = playerMessage ?? string.Empty;
        }

        public bool IsCoPresent { get; }
        public PlayerPartyCoPresenceScope Scope { get; }
        /// <summary>诊断用（英文、含具体条件），不进玩家提示。</summary>
        public string Reason { get; }
        /// <summary>给玩家的空间中性提示（不再出现 LocalMap）。</summary>
        public string PlayerMessage { get; }
    }

    /// <summary>
    /// 「候选同伴与主控是否处于同一可交互空间」的统一判定（2K / 迁移 §B）。
    ///
    /// <para>
    /// Continuous Outdoor 已经没有 Outdoor LocalMap，因此 <c>ActiveMapLayoutId == ""</c>。
    /// 继续用 <see cref="PlayerPartyRuntime.IsOnSameLocalMap"/> 当 join gate 会把「主角刚出村界、
    /// 同伴还在村界内」的物理相邻两人判成不可加入 —— Site／Hex 只是 context，不是 co-presence 边界。
    /// </para>
    ///
    /// <para>
    /// Continuous Outdoor 语义（V1）：candidate 与 active 只需要属于<b>当前同一个 physical
    /// presentation scope</b>（当前 active Continuous Surface materialization scope），
    /// 不要求 same SiteId／same Hex／same LocalMap。WorldSpaceId 尚未引入，该 scope 就是 V1 的
    /// co-presence；未来引入真正 independent WorldSpace（遭遇／内景）时在此加一条拒绝即可。
    /// </para>
    ///
    /// <para>
    /// Legacy／Interior／Cave 保持原规则：必须同一 LocalMap occupant（真正独立空间不得隔墙跟随）。
    /// </para>
    /// </summary>
    public static class PlayerPartyLocalCoPresenceQuery
    {
        /// <summary>Continuous Outdoor join 被拒时的玩家提示（空间中性，不再提 LocalMap）。</summary>
        public const string DeniedPlayerMessage = "需要与主控处于同一可交互空间。";

        /// <summary>
        /// 当前是否由 Continuous Outdoor Surface presentation 主导。
        /// 需要三个条件同时成立：PlayerParty 处于 AtWorldPosition、不在 Interior、且 runtime
        /// 真的建立了连续呈现 scope（有 loaded continuous site 或已 materialize 实体）。
        /// 纯 Hex／Wilderness 旅行（没有 continuous scope）不走这条分支。
        /// </summary>
        public static bool IsContinuousOutdoorPresentationScope(SimulationWorld world)
        {
            if (world?.PlayerPartyTravel == null || world.LocalMap == null ||
                world.ContinuousOutdoorMaterialization == null)
                return false;
            var motion = world.PlayerPartyTravel;
            if (!motion.HasPosition || motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition)
                return false;
            if (world.LocalMap.IsInInterior)
                return false;
            return world.ContinuousOutdoorMaterialization.LoadedSiteIds.Count > 0 ||
                   world.ContinuousOutdoorMaterialization.Entities.Count > 0;
        }

        /// <summary>该 entity 是否属于当前 Continuous Outdoor presentation scope。</summary>
        public static bool IsInContinuousOutdoorPresentationScope(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone || !IsContinuousOutdoorPresentationScope(world))
                return false;
            if (IsIndependentSpacePresence(world, id))
                return false;
            if (world.ContinuousOutdoorMaterialization.IsMaterialized(id))
                return true;
            var motion = world.PlayerPartyTravel;
            for (var i = 0; i < motion.TravelingMembers.Count; i++)
                if (motion.TravelingMembers[i].Equals(id))
                    return true;
            return false;
        }

        /// <summary>
        /// 真正独立空间（遭遇／Interior）的 presence —— 与 Continuous Outdoor 不是同一个
        /// physical scope（WorldSpaceId 之前的 V1 判定）。
        /// </summary>
        public static bool IsIndependentSpacePresence(SimulationWorld world, EntityId id)
        {
            if (world?.WorldPresence == null || id.IsNone)
                return false;
            if (!world.WorldPresence.TryGet(id, out var presence) || presence == null)
                return false;
            return presence.Mode == PartyWorldPresenceMode.InEncounter ||
                   presence.Mode == PartyWorldPresenceMode.DepartingLocalMap;
        }

        /// <summary>
        /// 统一 co-presence 判定。Continuous Outdoor 只要求同一 presentation scope；
        /// Legacy／Interior 继续要求同一 LocalMap occupant。
        /// </summary>
        public static PlayerPartyCoPresenceResult Evaluate(
            SimulationWorld world,
            PlayerPartyRuntime party,
            EntityId candidate)
        {
            if (world == null)
                return new PlayerPartyCoPresenceResult(
                    true, PlayerPartyCoPresenceScope.Ungated, "no world", string.Empty);
            if (party == null || !party.HasActive)
                return new PlayerPartyCoPresenceResult(
                    false, PlayerPartyCoPresenceScope.Ungated, "no active character", DeniedPlayerMessage);

            if (!IsContinuousOutdoorPresentationScope(world))
            {
                var sameMap = PlayerPartyRuntime.IsOnSameLocalMap(world, candidate, party.ActiveCharacterId);
                return new PlayerPartyCoPresenceResult(
                    sameMap,
                    PlayerPartyCoPresenceScope.LegacyLocalMap,
                    sameMap ? "same LocalMap" : "different LocalMap (interior/cave rule)",
                    sameMap ? string.Empty : DeniedPlayerMessage);
            }

            var activeInScope = IsInContinuousOutdoorPresentationScope(world, party.ActiveCharacterId);
            if (!activeInScope)
                return new PlayerPartyCoPresenceResult(
                    false, PlayerPartyCoPresenceScope.ContinuousOutdoorPresentation,
                    "active character is not in the continuous presentation scope", DeniedPlayerMessage);

            if (!IsInContinuousOutdoorPresentationScope(world, candidate))
                return new PlayerPartyCoPresenceResult(
                    false, PlayerPartyCoPresenceScope.ContinuousOutdoorPresentation,
                    "candidate is not in the continuous presentation scope", DeniedPlayerMessage);

            if (!world.Entities.TryGet(candidate, out var entity) || entity == null ||
                !CombatLifeStateService.CanFight(entity))
                return new PlayerPartyCoPresenceResult(
                    false, PlayerPartyCoPresenceScope.ContinuousOutdoorPresentation,
                    "candidate cannot act (dying/dead/missing)", DeniedPlayerMessage);

            if (ArmyService.TryGetArmyForCharacter(world, candidate, out _))
                return new PlayerPartyCoPresenceResult(
                    false, PlayerPartyCoPresenceScope.ContinuousOutdoorPresentation,
                    "candidate is in a formal army", DeniedPlayerMessage);

            return new PlayerPartyCoPresenceResult(
                true, PlayerPartyCoPresenceScope.ContinuousOutdoorPresentation,
                "same continuous outdoor presentation scope", string.Empty);
        }
    }
}
