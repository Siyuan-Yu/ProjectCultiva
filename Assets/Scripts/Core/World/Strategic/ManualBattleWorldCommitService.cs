using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 5S-B2-3.2：Manual WORLD_COMBAT 入场时，把所有实际参战战略单位（PlayerParty +
    /// 参战 FormalArmy）统一 commit 到 frozen BattleAnchorHex。
    /// 「同一 BattleHex」只指战略世界位置；LocalMap tactical coordinates 仍由各 materializer
    /// 分侧摆位（PlayerParty friendly side / Friendly Army formation / Enemy formation）。
    /// FormalArmy 复用 ArmyHexBattleAnchorService.CommitParticipantFormalArmiesAtBattleAnchor；
    /// 本 service 只补 PlayerParty 与 PartyWorld battle focus 的 WORLD physical authority。
    /// 选择 Manual Battle 本身代表 PlayerParty 从 SupportArea 正式加入 BattleHex ——
    /// 这是正式改变旧的「Active 不 teleport / PlayerParty 保持 SupportArea」policy。
    /// </summary>
    public static class ManualBattleWorldCommitService
    {
        /// <summary>
        /// Phase 5S-B2-3.3：Manual 与 Auto WORLD_COMBAT 共用同一 strategic world commit。
        /// partyMembers = PlayerPartyRuntime.Members（Core 侧经 PlayerPartyContext 获取）。
        /// 只有 snapshot 真的包含 PlayerParty member（实际参战）时才 commit PlayerPartyWorldMotion /
        /// PartyWorld —— 第三方 Army vs Army 或 Player 未参与时，只 commit FormalArmies，
        /// 绝不把远处主控 teleport 到 BattleHex。
        /// </summary>
        public static Result CommitWorldCombatParticipants(
            SimulationWorld world,
            IReadOnlyList<EntityId> partyMembers,
            BattleParticipantSnapshot snap,
            BattleLocalMapResolution resolution)
        {
            if (world == null || snap == null || resolution == null)
                return Result.Failure(ErrorCode.InvalidArgument, "CommitWorldCombatParticipants args required.");
            if (!ArmyHexBattleAnchorService.TryGetBattleAnchorHex(snap, out var battleHex))
                return Result.Failure(ErrorCode.InvalidArgument, "Snapshot 缺少冻结的 BattleAnchorHex。");
            if (world.HexWorld == null || !world.HexWorld.Contains(battleHex))
                return Result.Failure(ErrorCode.InvalidArgument, "BattleAnchorHex 越界。");

            // FormalArmy：既有 authority（MandatoryFriendly / selected OptionalFriendly /
            // EnemyPrimary / EnemyReinforcement + Attacker/Defender fallback）—— 不重复逻辑。
            ArmyHexBattleAnchorService.CommitParticipantFormalArmiesAtBattleAnchor(world, snap);

            // PlayerParty 只在“实际参战”时 commit；未参战不动 PlayerPartyWorldMotion / PartyWorld。
            if (!HasActualPlayerPartyParticipant(snap, partyMembers) ||
                world.PlayerPartyTravel == null)
                return Result.Success();

            var motion = world.PlayerPartyTravel;
            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            HexMath.ToWorldPosition(battleHex, hexSize, out var worldX, out var worldY);
            var battleWorld = new WorldVec2(worldX, worldY);

            // 加入战斗 = 当前 PlayerParty AutoTravel 终止（正确行为）。
            // partyMembers 是 battle 进入时刻的 living participant 快照（参战者必能战斗）；
            // 战斗中弥留/死亡由 StrategicEncounter 生命周期负责，battle 结束后离开战场
            // LocalMap 的 transition 会以 CaptureTravelingMembersForPartyTransition（过滤
            // 非 Alive）重新 capture —— 这里不重复过滤，保留 participant commit 语义。
            motion.CaptureTravelingMembers(partyMembers);

            if (resolution.Kind == BattleLocalMapResolutionKind.WorldSite &&
                !string.IsNullOrEmpty(resolution.SiteId))
            {
                // WorldSite：保持 exact BattleHex，禁止 snap 到 PresenceHex / AnchorHex。
                // 不用 PlayerPartyHexTravelService.ApplyMembersAtSite（会把 actual BattleHex 丢掉）。
                motion.SetAtWorldPosition(battleWorld, battleHex);
                if (!motion.TrySetAtWorldSitePreservingWorldPosition(resolution.SiteId, battleWorld))
                    return Result.Failure(ErrorCode.InvalidOperation, "TrySetAtWorldSitePreservingWorldPosition 失败（不 silent snap）。");
                motion.AlignCurrentHex(battleHex);

                for (var i = 0; i < partyMembers.Count; i++)
                    world.WorldPresence.SetAtSite(partyMembers[i], resolution.SiteId);

                world.PartyWorld.SiteId = resolution.SiteId;
                world.PartyWorld.FocusFormalArmyId = string.Empty;
                world.PartyWorld.LocalMapId = resolution.LocalMapId;
                world.PartyWorld.Mode = PartyWorldPresenceMode.AtSite;
            }
            else
            {
                // Wilderness（或未知 → 按 Wilderness 处理）：PlayerParty exact commit 到 BattleHex。
                motion.SetAtWorldPosition(battleWorld, battleHex);
                for (var i = 0; i < partyMembers.Count; i++)
                    world.WorldPresence.SetAtWorldPosition(partyMembers[i], battleWorld, battleHex);

                world.PartyWorld.ClearSiteFocus();
                world.PartyWorld.LocalMapId = resolution.LocalMapId;
                world.PartyWorld.Mode = PartyWorldPresenceMode.AtHex;
            }

            return Result.Success();
        }

        /// <summary>
        /// snapshot（frozen authority）是否真的包含 PlayerParty member（实际参战）。
        /// 只认 EntityId 匹配，不因 PlayerPartyContext 存在就 teleport 主控。
        /// </summary>
        public static bool HasActualPlayerPartyParticipant(
            BattleParticipantSnapshot snap,
            IReadOnlyList<EntityId> partyMembers)
        {
            if (snap == null || partyMembers == null || partyMembers.Count == 0)
                return false;
            for (var i = 0; i < snap.Records.Count; i++)
            {
                var id = snap.Records[i].EntityId;
                if (id.IsNone)
                    continue;
                for (var j = 0; j < partyMembers.Count; j++)
                {
                    if (id == partyMembers[j])
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 是否真正换了 physical LocalMap surface。多个 Wilderness Hex 可共用同一 MapLayout asset，
        /// 不能用 currentMapId == battleMapId 判断；same-surface 语义：
        /// Wilderness：previous.Kind==WildernessHex 且 previous.WildernessHex == battleHex；
        /// WorldSite：previous.Kind==WorldSite 且 previous.Site.SiteId == resolution.SiteId。
        /// </summary>
        public static bool PhysicalSurfaceChanged(
            LoadedLocalMapBelongingQuery.LoadedLocalMapContext previous,
            BattleLocalMapResolution resolution)
        {
            if (resolution == null)
                return true;
            switch (resolution.Kind)
            {
                case BattleLocalMapResolutionKind.Wilderness:
                    return !(previous.Kind == LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WildernessHex &&
                             previous.WildernessHex.Equals(resolution.BattleHex));
                case BattleLocalMapResolutionKind.WorldSite:
                    return !(previous.Kind == LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WorldSite &&
                             previous.Site != null &&
                             !string.IsNullOrEmpty(previous.Site.SiteId) &&
                             string.Equals(
                                 previous.Site.SiteId,
                                 resolution.SiteId,
                                 System.StringComparison.Ordinal));
                default:
                    return true;
            }
        }
    }
}
