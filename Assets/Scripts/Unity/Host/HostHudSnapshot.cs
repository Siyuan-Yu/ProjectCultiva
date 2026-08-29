using System.Text;
using XianXia.Core.Concealment;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Labor;
using XianXia.Core.Opportunity;
using XianXia.Core.Schedule;
using XianXia.Core.Exploration;
using XianXia.Core.Settlement;
using XianXia.Core.Social;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Read-only HUD snapshot from Core. VS0.6 adds thin social readout.
    /// </summary>
    public sealed class HostHudSnapshot
    {
        public bool Ready { get; private set; }
        public ulong DayIndex { get; private set; }
        public int HourOfDay { get; private set; }
        public int TickInDay { get; private set; }
        public bool Paused { get; private set; }
        public int SpeedMultiplier { get; private set; }
        public string FocusName { get; private set; } = "-";
        public string FocusId { get; private set; } = "-";
        public string FocusKind { get; private set; } = "-";
        public string ActionLine { get; private set; } = "none";
        public string ScheduleLine { get; private set; } = "none";
        public string QuotaLine { get; private set; } = "-";
        public int Risk { get; private set; }
        public string RealmLine { get; private set; } = "-";
        public bool PendingReprimand { get; private set; }
        public int KnownSites { get; private set; }
        public string PersonalityLine { get; private set; } = "-";
        public string FactionLine { get; private set; } = "-";
        public string RelationLine { get; private set; } = "-";
        public string SettlementLine { get; private set; } = "-";
        public string WorkRoleLine { get; private set; } = "-";
        public string LocationLine { get; private set; } = "-";
        public string TravelLine { get; private set; } = "-";

        public static HostHudSnapshot Capture(
            PlayableHostSession session,
            EntityId focusId,
            int speedMultiplier,
            EntityId relationPeerId = default)
        {
            var snap = new HostHudSnapshot
            {
                SpeedMultiplier = speedMultiplier <= 0 ? 1 : speedMultiplier
            };

            if (session == null || !session.IsInitialized)
                return snap;

            snap.Ready = true;
            snap.Paused = session.IsPaused;
            var day = session.CurrentDayClock;
            snap.DayIndex = day.DayIndex;
            snap.HourOfDay = day.HourOfDay;
            snap.TickInDay = day.TickInDay;
            snap.TravelLine = FormatTravel(session);

            if (focusId.IsNone || !session.World.Entities.TryGet(focusId, out var entity))
            {
                snap.FocusName = "(no selection)";
                return snap;
            }

            snap.FocusId = focusId.ToString();
            snap.FocusName = string.IsNullOrEmpty(entity.DisplayName) ? snap.FocusId : entity.DisplayName;
            snap.FocusKind = (entity.Tags & EntityTag.Npc) != 0 ? "Npc" : "Character";
            snap.ActionLine = FormatAction(session, entity);
            snap.ScheduleLine = FormatSchedule(session, entity, session.World.Tick);
            FormatQuota(entity, snap);
            snap.Risk = entity.TryGet<PersonalConcealmentRiskComponent>(out var risk) ? risk.Value : 0;
            snap.RealmLine = FormatRealm(entity);
            snap.KnownSites = entity.TryGet<KnownSitesComponent>(out var sites) ? sites.KnownIds.Count : 0;
            snap.PersonalityLine = FormatPersonality(entity);
            snap.FactionLine = FormatFaction(entity);
            snap.RelationLine = FormatRelation(session, focusId, relationPeerId);
            snap.SettlementLine = FormatSettlement(session);
            snap.WorkRoleLine = FormatWorkRole(entity);
            snap.LocationLine = FormatLocation(session, entity);
            return snap;
        }

        /// <summary>Phase 5C-W2 Travel diagnostics（Host 驱动写入真源字段，此处只读）。</summary>
        static string FormatTravel(PlayableHostSession session)
        {
            if (session?.World == null)
                return "(no world)";
            var world = session.World;
            var motion = world.PlayerPartyTravel;
            if (motion == null)
                return "(no travel)";

            var path = motion.HexPath;
            var nextHex = "-";
            if (motion.IsMoving && path != null &&
                motion.SegmentIndex >= 0 && motion.SegmentIndex + 1 < path.Count)
                nextHex = path[motion.SegmentIndex + 1].ToString();

            var gate = motion.SurfaceEdgeGate;
            var activeId = session.PlayerParty != null ? session.PlayerParty.ActiveCharacterId : default;
            var activeExists = !activeId.IsNone && world.Entities.TryGet(activeId, out _);
            return "hex=" + motion.CurrentHex +
                   " next=" + nextHex +
                   " seg=" + motion.SegmentIndex + "/" + (path != null ? path.Count : 0) +
                   " gateArmed=" + (gate != null ? gate.EdgeArmed.ToString() : "n/a") +
                   " canAttempt=" + (gate != null ? gate.CanAttemptEdgeTransition.ToString() : "n/a") +
                   " tInProg=" + (gate != null ? gate.TransitionInProgress.ToString() : "n/a") +
                   " | exitSrc=" + HostPlayerPartyController.LastExitSourceHex +
                   " exitDst=" + HostPlayerPartyController.LastExitDestinationHex +
                   " slot=" + HostPlayerPartyController.LastExitSlotRect +
                   " insideSlot=" + HostPlayerPartyController.LastActiveInsideExitSlot +
                   " | map=" + (world.LocalMap != null ? world.LocalMap.ActiveMapLayoutId : "-") +
                   " ent=" + activeExists +
                   " | " + HostPlayerPartyController.LastTransitionStatus +
                   (string.IsNullOrEmpty(HostPlayerPartyController.LastTransitionFailureReason)
                       ? string.Empty
                       : " fail=" + HostPlayerPartyController.LastTransitionFailureReason);
        }

        public string ToDebugText()
        {
            if (!Ready)
                return "HUD: session not ready";

            var sb = new StringBuilder(384);
            sb.Append("Day ").Append(DayIndex)
                .Append(" Hour ").Append(HourOfDay)
                .Append(" (tickInDay=").Append(TickInDay).Append(')')
                .Append(Paused ? " PAUSED" : " RUN")
                .Append(' ').Append(SpeedMultiplier).Append('x').Append('\n');
            sb.Append("Focus: ").Append(FocusName)
                .Append(" [").Append(FocusId).Append("] ")
                .Append(FocusKind).Append('\n');
            sb.Append("Personality: ").Append(PersonalityLine).Append('\n');
            sb.Append("Relation: ").Append(RelationLine).Append('\n');
            sb.Append("Faction: ").Append(FactionLine).Append('\n');
            sb.Append("Settlement: ").Append(SettlementLine).Append('\n');
            sb.Append("Work: ").Append(WorkRoleLine).Append('\n');
            sb.Append("Location: ").Append(LocationLine).Append('\n');
            sb.Append("Action: ").Append(ActionLine).Append('\n');
            sb.Append("Schedule: ").Append(ScheduleLine).Append('\n');
            sb.Append("Quota: ").Append(QuotaLine).Append('\n');
            sb.Append("Travel: ").Append(TravelLine).Append('\n');
            sb.Append("Risk: ").Append(Risk)
                .Append("  Realm: ").Append(RealmLine)
                .Append("  Sites: ").Append(KnownSites);
            if (PendingReprimand)
                sb.Append("  REPRIMAND");
            return sb.ToString();
        }

        static string FormatPersonality(Entity entity)
        {
            if (!entity.TryGet<PersonalityProfileComponent>(out var profile) || profile.Count == 0)
                return "(none)";
            var sb = new StringBuilder();
            foreach (var tag in profile.Tags)
            {
                if (sb.Length > 0)
                    sb.Append(", ");
                sb.Append(tag);
            }

            return sb.ToString();
        }

        static string FormatFaction(Entity entity)
        {
            if (!entity.TryGet<FactionMembershipComponent>(out var mem) || !mem.IsAffiliated)
                return "(none)";
            return mem.FactionId + " / " + mem.Role;
        }

        static string FormatSettlement(PlayableHostSession session)
        {
            if (session?.World == null || !session.World.Settlements.TryGetPrimary(out var s))
                return "(none)";
            var wood = s.GetStock("base:resource_rough_wood");
            var herb = s.GetStock("base:resource_spirit_herb");
            return s.Name + " wood=" + wood + " herb=" + herb + " fac=" + s.Facilities.Count;
        }

        static string FormatWorkRole(Entity entity)
        {
            if (!entity.TryGet<WorkAssignmentComponent>(out var work) || !work.IsAssigned)
                return "(none)";
            return work.Role + "@" + work.SettlementId;
        }

        static string FormatLocation(PlayableHostSession session, Entity entity)
        {
            if (!entity.TryGet<EntityLocationComponent>(out var loc) || !loc.HasLocation)
                return "(none)";
            if (session?.World != null &&
                session.World.WorldRegion.TryGet(loc.LocationId, out var location))
                return location.Name + " [" + location.Kind + "]";
            return loc.LocationId;
        }

        static string FormatRelation(PlayableHostSession session, EntityId focusId, EntityId peerId)
        {
            if (peerId.IsNone || peerId == focusId)
                return "(select peer to compare)";

            var towardPeer = session.World.Relationships.Score(focusId, peerId);
            var towardFocus = session.World.Relationships.Score(peerId, focusId);
            return "me→peer=" + towardPeer + " peer→me=" + towardFocus + " peer=" + peerId.Value;
        }

        static string FormatAction(PlayableHostSession session, Entity entity)
        {
            if (!entity.TryGet<ActionStateComponent>(out var state) || !state.HasActiveAction)
                return "idle";

            if (!session.World.ActiveActions.TryGetValue(state.ActiveActionId, out var action))
                return "missing#" + state.ActiveActionId;

            var remain = state.ActiveClock.HasValue ? state.ActiveClock.Value.RemainingTicks : 0UL;
            var total = state.ActiveClock.HasValue ? state.ActiveClock.Value.TotalDurationTicks : 0UL;
            return action.GetType().Name + " " + remain + "/" + total + " src=" + state.ActiveOrderSource;
        }

        static string FormatSchedule(PlayableHostSession session, Entity entity, WorldTick tick)
        {
            if (!entity.TryGet<ScheduleComponent>(out var schedule) ||
                string.IsNullOrEmpty(schedule.DefinitionId))
                return "unbound";

            if (!session.World.Schedules.TryGetValue(schedule.DefinitionId, out var def))
                return schedule.DefinitionId + " (missing def)";

            if (!def.TryResolve(tick, out var block))
                return schedule.DefinitionId + " (gap)";

            return schedule.DefinitionId + " → " + block.Activity;
        }

        static void FormatQuota(Entity entity, HostHudSnapshot snap)
        {
            if (!entity.TryGet<DailyTaskComponent>(out var daily))
            {
                snap.QuotaLine = "n/a";
                return;
            }

            snap.PendingReprimand = daily.PendingReprimand;
            snap.QuotaLine = daily.CompletedAmount + "/" + daily.RequiredAmount +
                             " dev=" + daily.Deviation;
        }

        static string FormatRealm(Entity entity)
        {
            if (!entity.TryGet<CultivationComponent>(out var cult))
                return "n/a";
            return cult.Realm + " prog=" + cult.Progress;
        }
    }
}
