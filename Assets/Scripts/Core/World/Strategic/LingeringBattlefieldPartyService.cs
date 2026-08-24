using System;
using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 残留战场再入队伍收集与锚点解析（Core 真源；Host 只负责点击与菜单）�?
    /// </summary>
    public static class LingeringBattlefieldPartyService
    {
        public static bool IsIncapacitated(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone)
                return false;
            if (!world.Entities.TryGet(id, out var ent) || ent == null)
                return false;
            return ent.TryGet<LifecycleComponent>(out var life) && life.IsIncapacitated;
        }

        /// <summary>可见尸体（未腐烂）：与弥留同属「倒下可交互」——可选中／进残留，不可下令�?/summary>
        public static bool IsVisibleCorpse(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone)
                return false;
            if (!world.Entities.TryGet(id, out var ent) || ent == null)
                return false;
            return CombatLifeStateService.HasVisibleCorpse(ent);
        }

        /// <summary>弥留或可见尸体：残留战场交互（点选／右键进入／探望）同一套�?/summary>
        public static bool IsLingeringDowned(SimulationWorld world, EntityId id) =>
            IsIncapacitated(world, id) || IsVisibleCorpse(world, id);

        /// <summary>
        /// 我方弥留／尸体：可右键「进入残留战场」或「前往并进入」�?
        /// 敌方也可经残留栈菜单进入；仍可用「追击／再攻」走进攻接战�?
        /// </summary>
        public static bool IsFriendlyLingeringDowned(SimulationWorld world, EntityId id)
        {
            if (!IsLingeringDowned(world, id))
                return false;
            return IsFriendlyCharacterForLingeringVisit(world, id);
        }

        public static bool IsFriendlyCharacterForLingeringVisit(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone || !world.Entities.TryGet(id, out var ent) || ent == null)
                return false;
            if ((ent.Tags & EntityTag.Npc) != 0)
                return false;
            var playerFaction = world.Strategic?.PlayerFactionId ?? StrategicFactionCatalog.PlayerFactionId;
            var faction = ArmyService.ResolveCharacterFactionId(world, id);
            return !string.IsNullOrEmpty(faction) &&
                   string.Equals(faction, playerFaction, StringComparison.Ordinal);
        }

        public static bool IsLivingForMacroOrder(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone || !world.Entities.TryGet(id, out var ent) || ent == null)
                return false;
            if (!ent.TryGet<LifecycleComponent>(out var life) || life == null)
                return true;
            return !life.IsIncapacitated && !life.IsDead && !life.IsRemoved;
        }

        /// <summary>
        /// 残留战场再入：半径内我方弥留／尸�?+ 当前行动决定人（mandatoryLiving）强制纳入；
        /// 其余支援半径内活人由接战�?Optional 名单勾选，不在此强制�?
        /// </summary>
        public static bool CollectViewParty(
            SimulationWorld world,
            IReadOnlyList<EntityId> roster,
            EntityId focusIncap,
            List<EntityId> into,
            IReadOnlyList<EntityId> mandatoryLiving = null)
        {
            into.Clear();
            if (world?.Strategic == null || roster == null || into == null)
                return false;
            if (!BattleOfferService.HasLingeringBattlefield(world))
                return false;

            if (!TryResolveBattleAnchorHex(world, focusIncap, out var anchorHex))
            {
                if (!focusIncap.IsNone && IsFriendlyLingeringDowned(world, focusIncap))
                {
                    into.Add(focusIncap);
                    return true;
                }

                return false;
            }

            AppendFriendlyLingeringAtHex(world, roster, anchorHex, into);
            AppendMandatoryLivingAtHex(world, mandatoryLiving, anchorHex, into);
            AppendIncapacitatedAtHex(world, roster, anchorHex, into);
            ArmyMacroPartyQueries.ExpandMandatoryLivingToFormalArmies(world, into);
            EnsureFocusIncapInParty(world, focusIncap, into);
            return into.Count > 0;
        }

        static void AppendFriendlyLingeringAtHex(
            SimulationWorld world,
            IReadOnlyList<EntityId> roster,
            HexCoord anchorHex,
            List<EntityId> into)
        {
            if (world == null || roster == null || into == null)
                return;
            for (var i = 0; i < roster.Count; i++)
            {
                var id = roster[i];
                if (id.IsNone || !IsFriendlyLingeringDowned(world, id))
                    continue;
                if (!StrategicResidualPresenceService.TryGetResidualHex(world, id, out var hex) ||
                    !hex.Equals(anchorHex))
                    continue;
                var exists = false;
                for (var j = 0; j < into.Count; j++)
                {
                    if (into[j] == id)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    into.Add(id);
            }
        }

        static void AppendMandatoryLivingAtHex(
            SimulationWorld world,
            IReadOnlyList<EntityId> mandatoryLiving,
            HexCoord anchorHex,
            List<EntityId> into)
        {
            if (world == null || mandatoryLiving == null || into == null)
                return;
            for (var i = 0; i < mandatoryLiving.Count; i++)
            {
                var id = mandatoryLiving[i];
                if (id.IsNone || !IsLivingForMacroOrder(world, id))
                    continue;
                if (!world.WorldPresence.TryGet(id, out var wp) || wp == null)
                    continue;
                if (!ReinforcementRangeService.IsWithinReinforcementRange(world, wp, anchorHex))
                    continue;
                for (var j = 0; j < into.Count; j++)
                {
                    if (into[j] == id)
                        goto nextLivingHex;
                }

                into.Add(id);
                nextLivingHex: ;
            }
        }

        static void AppendIncapacitatedAtHex(
            SimulationWorld world,
            IReadOnlyList<EntityId> roster,
            HexCoord anchorHex,
            List<EntityId> into)
        {
            if (world == null || roster == null || into == null)
                return;
            for (var i = 0; i < roster.Count; i++)
            {
                var id = roster[i];
                if (id.IsNone || !IsLingeringDowned(world, id))
                    continue;
                if (!StrategicResidualPresenceService.TryGetResidualHex(world, id, out var hex) ||
                    !hex.Equals(anchorHex))
                    continue;
                var exists = false;
                for (var j = 0; j < into.Count; j++)
                {
                    if (into[j] == id)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    into.Add(id);
            }
        }

        static void EnsureFocusIncapInParty(
            SimulationWorld world,
            EntityId focusIncap,
            List<EntityId> into)
        {
            if (into == null || focusIncap.IsNone || !IsFriendlyLingeringDowned(world, focusIncap))
                return;
            for (var i = 0; i < into.Count; i++)
            {
                if (into[i] == focusIncap)
                    return;
            }

            // 用户从该倒下头像进入：本人始终纳入进场名�?
            into.Add(focusIncap);
        }

        public static bool CanEnterLingeringBattlefield(
            SimulationWorld world,
            IReadOnlyList<EntityId> roster,
            EntityId focusIncap,
            List<EntityId> scratch,
            IReadOnlyList<EntityId> mandatoryLiving = null)
        {
            if (scratch == null || !IsFriendlyLingeringDowned(world, focusIncap))
                return false;
            return CollectViewParty(world, roster, focusIncap, scratch, mandatoryLiving) &&
                   scratch.Count > 0;
        }

        public static bool TryResolveBattleAnchorHex(
            SimulationWorld world,
            EntityId focusIncap,
            out HexCoord anchorHex)
        {
            anchorHex = default;
            if (TryResolveBattleAnchorHexFromParticipants(world, out anchorHex))
                return true;

            if (focusIncap.IsNone ||
                !world.WorldPresence.TryGet(focusIncap, out var wp) ||
                wp == null)
                return false;

            return TryResolveBattleAnchorHexFromPresence(world, wp, out anchorHex);
        }

        static bool TryResolveBattleAnchorHexFromParticipants(
            SimulationWorld world,
            out HexCoord anchorHex)
        {
            anchorHex = default;
            var snap = world?.Strategic?.Participants;
            return snap != null &&
                   ArmyHexBattleAnchorService.TryGetBattleAnchorHex(snap, out anchorHex);
        }

        static bool TryResolveBattleAnchorHexFromPresence(
            SimulationWorld world,
            WorldAgentPresence wp,
            out HexCoord anchorHex)
        {
            anchorHex = default;
            if (wp == null)
                return false;

            if (wp.UsesHexPresence)
            {
                anchorHex = wp.ResidualHex;
                return true;
            }

            if (!string.IsNullOrEmpty(wp.SiteId) &&
                ArmyHexBattleAnchorService.TryResolveHexForSite(world, wp.SiteId, out anchorHex))
                return true;

            return false;
        }
    }
}
