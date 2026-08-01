using System.Text;
using XianXia.Core.Concealment;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Labor;
using XianXia.Core.Opportunity;
using XianXia.Core.Schedule;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// VS0.4 Phase E: read-only HUD snapshot from Core. No mutations.
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
        public string ActionLine { get; private set; } = "none";
        public string ScheduleLine { get; private set; } = "none";
        public string QuotaLine { get; private set; } = "-";
        public int Risk { get; private set; }
        public string RealmLine { get; private set; } = "-";
        public bool PendingReprimand { get; private set; }
        public int KnownSites { get; private set; }

        public static HostHudSnapshot Capture(
            PlayableHostSession session,
            EntityId focusId,
            int speedMultiplier)
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

            if (focusId.IsNone || !session.World.Entities.TryGet(focusId, out var entity))
            {
                snap.FocusName = "(no selection)";
                return snap;
            }

            snap.FocusId = focusId.ToString();
            snap.FocusName = string.IsNullOrEmpty(entity.DisplayName) ? snap.FocusId : entity.DisplayName;
            snap.ActionLine = FormatAction(session, entity);
            snap.ScheduleLine = FormatSchedule(session, entity, session.World.Tick);
            FormatQuota(entity, snap);
            snap.Risk = entity.TryGet<PersonalConcealmentRiskComponent>(out var risk) ? risk.Value : 0;
            snap.RealmLine = FormatRealm(entity);
            snap.KnownSites = entity.TryGet<KnownSitesComponent>(out var sites) ? sites.KnownIds.Count : 0;
            return snap;
        }

        public string ToDebugText()
        {
            if (!Ready)
                return "HUD: session not ready";

            var sb = new StringBuilder(256);
            sb.Append("Day ").Append(DayIndex)
                .Append(" Hour ").Append(HourOfDay)
                .Append(" (tickInDay=").Append(TickInDay).Append(')')
                .Append(Paused ? " PAUSED" : " RUN")
                .Append(' ').Append(SpeedMultiplier).Append('x').Append('\n');
            sb.Append("Focus: ").Append(FocusName).Append(" [").Append(FocusId).Append("]\n");
            sb.Append("Action: ").Append(ActionLine).Append('\n');
            sb.Append("Schedule: ").Append(ScheduleLine).Append('\n');
            sb.Append("Quota: ").Append(QuotaLine).Append('\n');
            sb.Append("Risk: ").Append(Risk)
                .Append("  Realm: ").Append(RealmLine)
                .Append("  Sites: ").Append(KnownSites);
            if (PendingReprimand)
                sb.Append("  REPRIMAND");
            return sb.ToString();
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
