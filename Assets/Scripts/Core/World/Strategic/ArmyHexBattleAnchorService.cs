using System;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Hex ??????????? / ?? / Presence ????? EncounterHex?</summary>
    public static class ArmyHexBattleAnchorService
    {
        public const int InvalidHexComponent = int.MinValue;

        public static bool IsHexAnchorMode(SimulationWorld world) =>
            ArmyHexCommandService.IsHexStrategicActive(world);

        public static bool HasBattleAnchorHex(BattleParticipantSnapshot snap) =>
            snap != null && snap.BattleAnchorHexQ != InvalidHexComponent &&
            snap.BattleAnchorHexR != InvalidHexComponent;

        public static bool TryGetBattleAnchorHex(BattleParticipantSnapshot snap, out HexCoord hex)
        {
            hex = default;
            if (!HasBattleAnchorHex(snap))
                return false;
            hex = new HexCoord(snap.BattleAnchorHexQ, snap.BattleAnchorHexR);
            return true;
        }

        public static void SetBattleAnchorHex(BattleParticipantSnapshot snap, HexCoord hex)
        {
            if (snap == null)
                return;
            snap.BattleAnchorHexQ = hex.Q;
            snap.BattleAnchorHexR = hex.R;
        }

        public static void ClearBattleAnchorHex(BattleParticipantSnapshot snap)
        {
            if (snap == null)
                return;
            snap.BattleAnchorHexQ = InvalidHexComponent;
            snap.BattleAnchorHexR = InvalidHexComponent;
        }

        public static void ApplyStackBattleAnchor(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            ArmyStack stack)
        {
            if (snap == null || stack == null)
                return;

            if (ArmyStackAdapter.TryGetFormalArmy(world, stack, out var formal) && formal != null &&
                formal.UsesHexStrategicPosition)
            {
                SetBattleAnchorHex(snap, formal.CurrentHex);
                return;
            }

            if (TryResolveHexForSite(world, stack.SiteId, out var nodeHex))
                SetBattleAnchorHex(snap, nodeHex);
            else
                ClearBattleAnchorHex(snap);
        }

        public static void ApplyFormalArmyBattleAnchor(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            FormalArmy army)
        {
            if (snap == null || army == null || !army.UsesHexStrategicPosition)
            {
                ClearBattleAnchorHex(snap);
                return;
            }

            SetBattleAnchorHex(snap, army.CurrentHex);
        }

        public static void ParkArmyAtBattleAnchor(
            SimulationWorld world,
            FormalArmy army,
            BattleParticipantSnapshot snap)
        {
            if (army == null || snap == null)
                return;

            if (TryResolveParkingHex(world, army, snap, out var hex))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var oldHex = army.UsesHexStrategicPosition ? army.CurrentHex : default;
                if (!oldHex.Equals(hex))
                {
                    LingeringExitPositionTrace.LogArmyHexMutation(
                        world,
                        army,
                        oldHex,
                        hex,
                        nameof(ParkArmyAtBattleAnchor),
                        "ParkArmyAtBattleAnchor");
                }
#endif
                CommitArmyAtExactBattleHex(world, army, hex);
                return;
            }

            army.State = FormalArmyState.Idle;
        }

        /// <summary>
        /// Phase 5S：把 FormalArmy 以具体 BattleHex 战略 commit（精确格，不落 WorldSite.AnchorHex）。
        /// WorldMotion 为 WorldMap 权威位置（WorldPosition + CurrentHex=exact hex），
        /// legacy CurrentHex/DestinationHex 同步，Army 停为 Idle，成员 Presence 同步到战场。
        /// 不使用 InitializeAtWorldSite —— 多格 WorldSite 不能吸回 AnchorHex。
        /// </summary>
        public static void CommitArmyAtExactBattleHex(
            SimulationWorld world,
            FormalArmy army,
            HexCoord hex)
        {
            if (world == null || army == null || army.WorldMotion == null)
                return;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            HexMath.ToWorldPosition(hex, hexSize, out var worldX, out var worldY);
            army.UsesHexStrategicPosition = true;
            army.WorldMotion.SetAtWorldPosition(new WorldVec2(worldX, worldY), hex);
            army.SyncLegacyFromWorldMotion();
            army.State = FormalArmyState.Idle;
            ArmyPresenceAdapter.SyncFromArmy(world, army);
        }

        /// <summary>
        /// Phase 5S：手动战正式进入时，把所有实际参战 FormalArmy 一次性 commit 到 BattleAnchorHex
        /// （MandatoryFriendly / selected OptionalFriendly / EnemyPrimary / EnemyReinforcement，
        /// 含 AttackerArmyId / DefenderArmyId safety fallback；FormalArmyId 缺失时经 ArmyStackId 解析）。
        /// Snapshot 是冻结 authority：不清扫 SupportArea、不重新 gather participant。
        /// 这一步代表战略意义的“确认参战 → 已进入 BattleHex”，可清除该 Army 原有 travel/order path。
        /// </summary>
        public static void CommitParticipantFormalArmiesAtBattleAnchor(
            SimulationWorld world,
            BattleParticipantSnapshot snap)
        {
            if (world == null || snap == null)
                return;
            if (!TryGetBattleAnchorHex(snap, out var anchorHex))
                return;
            if (world.HexWorld == null || !world.HexWorld.Contains(anchorHex))
                return;
            if (world.Strategic?.FormalArmies == null)
                return;

            var armyIds = new System.Collections.Generic.List<string>(8);
            CollectParticipantFormalArmyIds(world, snap, armyIds);
            for (var i = 0; i < armyIds.Count; i++)
            {
                var armyId = armyIds[i];
                if (string.IsNullOrEmpty(armyId))
                    continue;
                if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                    continue;
                CommitArmyAtExactBattleHex(world, army, anchorHex);
            }
        }

        /// <summary>从冻结 Snapshot 收集全部实际参战 FormalArmyId（去重，含 stack 兼容解析）。</summary>
        public static void CollectParticipantFormalArmyIds(
            SimulationWorld world,
            BattleParticipantSnapshot snap,
            System.Collections.Generic.List<string> into)
        {
            into?.Clear();
            if (into == null || snap == null)
                return;

            for (var i = 0; i < snap.Records.Count; i++)
            {
                var rec = snap.Records[i];
                if (rec.EntityId.IsNone)
                    continue;
                if (rec.Kind != BattleParticipantKind.MandatoryFriendly &&
                    !(rec.Kind == BattleParticipantKind.OptionalFriendly && rec.Selected) &&
                    rec.Kind != BattleParticipantKind.EnemyPrimary &&
                    rec.Kind != BattleParticipantKind.EnemyReinforcement)
                    continue;

                var armyId = ResolveParticipantFormalArmyId(world, rec);
                if (string.IsNullOrEmpty(armyId) || into.Contains(armyId))
                    continue;
                into.Add(armyId);
            }

            // Direct combatant safety fallback
            AddUnique(into, snap.AttackerArmyId);
            AddUnique(into, snap.DefenderArmyId);
        }

        static void AddUnique(System.Collections.Generic.List<string> into, string armyId)
        {
            if (string.IsNullOrEmpty(armyId) || into.Contains(armyId))
                return;
            into.Add(armyId);
        }

        static string ResolveParticipantFormalArmyId(
            SimulationWorld world,
            BattleParticipantRecord rec)
        {
            if (rec == null)
                return string.Empty;
            if (!string.IsNullOrEmpty(rec.FormalArmyId))
                return rec.FormalArmyId;
            if (string.IsNullOrEmpty(rec.ArmyStackId) || world == null || world.Strategic?.Armies == null)
                return string.Empty;
            if (!world.Strategic.Armies.TryGet(rec.ArmyStackId, out var stack) || stack == null)
                return string.Empty;
            return ArmyStackAdapter.TryGetFormalArmy(world, stack, out var army) && army != null
                ? army.ArmyId
                : string.Empty;
        }

        public static void ParkStackAtBattleAnchor(
            SimulationWorld world,
            ArmyStack stack,
            BattleParticipantSnapshot snap)
        {
            if (stack == null || snap == null)
                return;

            if (TryResolveParkingHex(world, null, snap, out var hex))
                stack.SiteId = ResolveSiteIdForHex(world, hex, stack.SiteId);
        }

        public static void PlacePresenceAtBattleAnchor(
            SimulationWorld world,
            WorldAgentPresence wp,
            BattleParticipantSnapshot snap)
        {
            if (wp == null || snap == null)
                return;

            if (TryResolveParkingHex(world, null, snap, out var hex))
            {
                wp.SetAtHex(hex);
                return;
            }

            wp.Mode = PartyWorldPresenceMode.AtSite;
            wp.SiteId = ResolveSiteIdForHex(world, default, wp.SiteId);
        }

        public static bool TryDetectHexContact(
            SimulationWorld world,
            FormalArmy pursuer,
            FormalArmy target)
        {
            if (world == null || pursuer == null || target == null)
                return false;

            return BattleEngagementTriggerService.TryDetectEngagementContact(
                world, pursuer, target);
        }

        public static bool TryResolveHexForSite(SimulationWorld world, string siteId, out HexCoord hex)
        {
            hex = default;
            if (world == null || string.IsNullOrEmpty(siteId))
                return false;

            if (world.Strategic.Sites.TryGet(siteId, out var site) && site != null)
            {
                hex = site.AnchorHex;
                return world.HexWorld.Contains(hex);
            }

            return false;
        }

        public static string ResolveSiteIdForHex(
            SimulationWorld world,
            HexCoord hex,
            string fallbackSiteId)
        {
            if (world?.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGetAtHex(hex, out var site) &&
                site != null &&
                !string.IsNullOrEmpty(site.SiteId))
                return site.SiteId;

            return fallbackSiteId ?? string.Empty;
        }

        static bool TryResolveParkingHex(
            SimulationWorld world,
            FormalArmy army,
            BattleParticipantSnapshot snap,
            out HexCoord hex)
        {
            hex = default;
            if (TryGetBattleAnchorHex(snap, out hex) && world.HexWorld.Contains(hex))
                return true;
            if (army != null && army.UsesHexStrategicPosition)
            {
                hex = army.CurrentHex;
                return world.HexWorld.Contains(hex);
            }

            return false;
        }
    }
}
