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
        public static Result CommitWorldCombatParticipants(
            SimulationWorld world,
            PlayerPartyRuntime party,
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

            if (party == null || party.Count == 0 || world.PlayerPartyTravel == null)
            {
                world.PartyWorld.LocalMapId = resolution.LocalMapId;
                return Result.Success();
            }

            var motion = world.PlayerPartyTravel;
            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            HexMath.ToWorldPosition(battleHex, hexSize, out var worldX, out var worldY);
            var battleWorld = new WorldVec2(worldX, worldY);

            // Manual Battle 加入 = 当前 PlayerParty AutoTravel 终止（正确行为）。
            motion.CaptureTravelingMembers(party.Members);

            if (resolution.Kind == BattleLocalMapResolutionKind.WorldSite &&
                !string.IsNullOrEmpty(resolution.SiteId))
            {
                // WorldSite：保持 exact BattleHex，禁止 snap 到 PresenceHex / AnchorHex。
                // 不用 PlayerPartyHexTravelService.ApplyMembersAtSite（会把 actual BattleHex 丢掉）。
                motion.SetAtWorldPosition(battleWorld, battleHex);
                if (!motion.TrySetAtWorldSitePreservingWorldPosition(resolution.SiteId, battleWorld))
                    return Result.Failure(ErrorCode.InvalidOperation, "TrySetAtWorldSitePreservingWorldPosition 失败（不 silent snap）。");
                motion.AlignCurrentHex(battleHex);

                for (var i = 0; i < party.Members.Count; i++)
                    world.WorldPresence.SetAtSite(party.Members[i], resolution.SiteId);

                world.PartyWorld.SiteId = resolution.SiteId;
                world.PartyWorld.FocusFormalArmyId = string.Empty;
                world.PartyWorld.LocalMapId = resolution.LocalMapId;
                world.PartyWorld.Mode = PartyWorldPresenceMode.AtSite;
            }
            else
            {
                // Wilderness（或未知 → 按 Wilderness 处理）：PlayerParty exact commit 到 BattleHex。
                motion.SetAtWorldPosition(battleWorld, battleHex);
                for (var i = 0; i < party.Members.Count; i++)
                    world.WorldPresence.SetAtWorldPosition(party.Members[i], battleWorld, battleHex);

                world.PartyWorld.ClearSiteFocus();
                world.PartyWorld.LocalMapId = resolution.LocalMapId;
                world.PartyWorld.Mode = PartyWorldPresenceMode.AtHex;
            }

            return Result.Success();
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
