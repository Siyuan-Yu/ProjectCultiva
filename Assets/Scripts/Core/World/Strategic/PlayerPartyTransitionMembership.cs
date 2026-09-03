using System.Collections.Generic;
using System.Text;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// PlayerParty LocalMap / Hex 边界 Transition：哪些成员随 Active 一起转移。
    /// Membership 为真源；FormalArmy 成员排除；已 Stop Follow 者不在 party.Members。
    /// 生命状态 gate：Incapacitated / Corpse（非 Alive）不再属于「当前正随队旅行的人」——
    /// 逻辑 membership 保留（绝不 TryRemoveMember），但 physical traveling membership 排除，
    /// 弥留/尸体由 StrategicResidual 在倒下 hex 负责，绝不跟随主控移动。
    /// </summary>
    public static class PlayerPartyTransitionMembership
    {
        static readonly List<EntityId> Scratch = new List<EntityId>(8);

        public static bool ShouldMemberTransitionWithParty(
            SimulationWorld world,
            PlayerPartyRuntime party,
            EntityId characterId)
        {
            if (world == null || party == null || characterId.IsNone)
                return false;
            if (!party.IsMember(characterId))
                return false;
            if (ArmyService.TryGetArmyForCharacter(world, characterId, out _))
                return false;
            if (!world.Entities.TryGet(characterId, out var entity) || entity == null)
                return false;
            if (!CombatLifeStateService.CanFight(entity))
                return false;
            return true;
        }

        /// <summary>
        /// 刷新 TravelingMembers，供 ApplyTravelingMembersAtHex/AtSite 与 Wilderness 可见性使用。
        /// </summary>
        public static void CaptureTravelingMembersForPartyTransition(
            SimulationWorld world,
            PlayerPartyRuntime party)
        {
            if (world?.PlayerPartyTravel == null || party == null)
                return;

            Scratch.Clear();
            for (var i = 0; i < party.Members.Count; i++)
            {
                var id = party.Members[i];
                if (!ShouldMemberTransitionWithParty(world, party, id))
                    continue;
                Scratch.Add(id);
            }

            world.PlayerPartyTravel.CaptureTravelingMembers(Scratch);
        }

        public static void LogPartyTransition(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string phase,
            HexCoord destinationHex,
            string destinationMapId)
        {
            if (PlayerPartyWorldLocationDebug.Sink == null || world == null || party == null)
                return;

            var motion = world.PlayerPartyTravel;
            var active = party.HasActive ? party.ActiveCharacterId.Value.ToString() : "none";
            var sb = new StringBuilder(256);
            sb.Append("[PartyTransition] phase=").Append(phase ?? "?");
            sb.Append(" ActiveId=").Append(active);
            sb.Append(" DestinationHex=").Append(destinationHex);
            sb.Append(" DestinationMap=").Append(destinationMapId ?? string.Empty);
            sb.Append(" TravelingMembers=[");
            if (motion != null)
            {
                for (var i = 0; i < motion.TravelingMembers.Count; i++)
                {
                    if (i > 0)
                        sb.Append(',');
                    sb.Append(motion.TravelingMembers[i].Value);
                }
            }

            sb.Append("] PartyMembers=[");
            for (var i = 0; i < party.Members.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(party.Members[i].Value);
            }

            sb.Append("] Details:");
            for (var i = 0; i < party.Members.Count; i++)
            {
                var id = party.Members[i];
                CharacterWorldMovementAuthorityQuery.TryGetAuthority(
                    world, id, party, out var authority);
                ArmyService.TryGetArmyForCharacter(world, id, out var army);
                var included = ShouldMemberTransitionWithParty(world, party, id);
                var reason = included
                    ? "PlayerPartyMember"
                    : army != null
                        ? "FormalArmyMember"
                        : !party.IsMember(id)
                            ? "NotInParty"
                            : "Excluded";
                sb.Append("\n  CharacterId=").Append(id.Value);
                sb.Append(" IsFollower=").Append(party.IsFollower(id));
                sb.Append(" ArmyId=").Append(army != null ? army.ArmyId : "—");
                sb.Append(" Authority=").Append(authority);
                sb.Append(" Included=").Append(included);
                sb.Append(" Reason=").Append(reason);
            }

            PlayerPartyWorldLocationDebug.Sink(sb.ToString());
        }

        public static void LogMaterializeMember(
            EntityId characterId,
            string destinationMap,
            bool spawned,
            bool followReboundHint)
        {
            if (PlayerPartyWorldLocationDebug.Sink == null || characterId.IsNone)
                return;

            PlayerPartyWorldLocationDebug.Sink(
                "[Materialize] CharacterId=" + characterId.Value +
                " DestinationMap=" + (destinationMap ?? string.Empty) +
                " Spawned=" + spawned +
                " FollowRebound=" + followReboundHint);
        }

        /// <summary>
        /// PlayerParty member WorldPresence 单向 consistency guard：motion（PlayerPartyWorldMotion）
        /// 是 strategic truth，individual member presence 只是兼容/查询状态。
        /// 只允许 motion → member presence 单向 repair；绝对禁止 member presence → motion
        /// （SiteId / CurrentHex / WorldPosition）反向覆盖。
        /// 调用点：成功 EnterWorldSiteAsParty / surface LocalMap materialize / final arrival 后。
        /// 实际发生 repair 时打一次 diagnostics（member id / old / new / motion context / phase）。
        /// </summary>
        public static void ReconcilePlayerPartyMemberWorldPresenceFromMotion(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string phase)
        {
            if (world?.PlayerPartyTravel == null || party == null || world.WorldPresence == null)
                return;

            var motion = world.PlayerPartyTravel;
            if (!motion.HasPosition)
                return;

            var atSite = motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                         !string.IsNullOrEmpty(motion.SiteId);
            var atHex = !atSite &&
                        motion.LocationKind == PlayerPartyLocationKind.AtWorldPosition;
            if (!atSite && !atHex)
                return;

            for (var i = 0; i < party.Members.Count; i++)
            {
                var id = party.Members[i];
                if (id.IsNone || !ShouldMemberTransitionWithParty(world, party, id))
                    continue;
                if (!world.Entities.TryGet(id, out var ent) || ent == null)
                    continue;

                var changed = false;
                if (atSite)
                {
                    if (!world.WorldPresence.TryGet(id, out var wp) ||
                        wp == null ||
                        wp.Mode != PartyWorldPresenceMode.AtSite ||
                        !string.Equals(wp.SiteId, motion.SiteId, System.StringComparison.Ordinal))
                    {
                        world.WorldPresence.SetAtSite(id, motion.SiteId);
                        changed = true;
                    }
                }
                else if (atHex)
                {
                    if (!world.WorldPresence.TryGet(id, out var wp) ||
                        wp == null ||
                        wp.Mode != PartyWorldPresenceMode.AtHex ||
                        wp.UsesHexPresence && !wp.ResidualHex.Equals(motion.CurrentHex))
                    {
                        world.WorldPresence.SetAtHex(id, motion.CurrentHex);
                        changed = true;
                    }
                }

                if (changed && PlayerPartyWorldLocationDebug.Sink != null)
                {
                    PlayerPartyWorldLocationDebug.Sink(
                        "[PresenceReconcile] phase=" + (phase ?? "?") +
                        " member=" + id.Value +
                        " kind=" + motion.LocationKind +
                        " site=" + (motion.SiteId ?? string.Empty) +
                        " hex=" + motion.CurrentHex +
                        " -> At" +
                        (atSite ? "Site(" + motion.SiteId + ")" : "Hex(" + motion.CurrentHex + ")"));
                }
            }
        }
    }
}
