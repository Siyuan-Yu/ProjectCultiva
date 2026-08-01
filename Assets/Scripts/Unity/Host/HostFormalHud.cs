using System.Text;
using UnityEngine;
using XianXia.Core.Concealment;
using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Exploration;
using XianXia.Core.Labor;
using XianXia.Core.Schedule;
using XianXia.Core.Settlement;
using XianXia.Core.Social;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Reference Level 正式 UI 基础：角色／资源／时间／事件／任务（结构化 IMGUI，非正式皮肤）。
    /// </summary>
    public sealed class HostFormalHud : MonoBehaviour
    {
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostEventFeed eventFeed;
        [SerializeField] bool visible = true;
        [SerializeField] KeyCode toggleKey = KeyCode.F6;

        public void Bind(
            PlayableHostBootstrap host,
            HostSelectionController selection,
            HostEventFeed feed)
        {
            bootstrap = host;
            selectionController = selection;
            eventFeed = feed;
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                visible = !visible;
        }

        void OnGUI()
        {
            if (!visible)
                return;
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized)
                return;

            DrawTimeBar(session);
            DrawResourceBar(session);
            DrawCharacterPanel(session);
            DrawSchedulePanel(session);
            DrawQuestPanel(session);
            DrawEventPanel();
        }

        void DrawTimeBar(PlayableHostSession session)
        {
            var day = session.CurrentDayClock;
            var chapter = session.World.Chapters.HasActive
                ? session.World.Chapters.ActiveChapterId
                : "(no chapter)";
            var text = "Day " + day.DayIndex + "  Hour " + day.HourOfDay +
                       "  Tick " + day.TickInDay + "/" + WorldTick.TicksPerDay +
                       "  |  " + chapter +
                       (session.IsPaused ? "  PAUSED" : "");
            GUI.Box(new Rect(8f, 8f, Screen.width - 16f, 28f), text);
        }

        void DrawResourceBar(PlayableHostSession session)
        {
            var wood = 0;
            var herb = 0;
            var grain = 0;
            var grass = 0;
            var name = "-";
            if (session.World.Settlements.TryGetPrimary(out var s))
            {
                name = s.Name;
                wood = s.GetStock("base:resource_rough_wood");
                herb = s.GetStock("base:resource_spirit_herb");
                grain = s.GetStock("base:resource_grain");
                grass = s.GetStock("base:resource_conceal_grass");
            }

            var anger = session.World.SupervisorAnger != null ? session.World.SupervisorAnger.Value : 0;
            GUI.Box(
                new Rect(8f, 40f, 640f, 28f),
                "资源 | " + name + "  木=" + wood + "  粮=" + grain + "  药=" + herb +
                "  敛息草=" + grass + "  愤怒=" + anger);
        }

        void DrawCharacterPanel(PlayableHostSession session)
        {
            var focus = EntityId.None;
            if (selectionController != null && selectionController.State.Count > 0)
                focus = selectionController.State.SelectedIds[0];

            var sb = new StringBuilder(256);
            sb.AppendLine("角色面板");
            if (focus.IsNone || !session.World.Entities.TryGet(focus, out var e))
            {
                sb.Append("(未选择)");
            }
            else
            {
                sb.AppendLine(e.DisplayName + "  " + focus);
                if (e.TryGet<CultivationComponent>(out var c))
                    sb.AppendLine("境界=" + c.Realm + " Progress=" + c.Progress + " Manual=" + c.HasLearnedManual);
                if (e.TryGet<PersonalConcealmentRiskComponent>(out var risk))
                    sb.AppendLine(
                        "暴露=" + risk.Value +
                        (ConcealmentExposureRules.IsNight(session.World.Tick) ? " 夜" : " 昼"));
                if (e.TryGet<DailyTaskComponent>(out var daily))
                    sb.AppendLine("日课=" + daily.CompletedAmount + "/" + daily.RequiredAmount);
                if (e.TryGet<NpcAiRoleComponent>(out var ai))
                    sb.AppendLine("AI=" + ai.Role);
                if (e.TryGet<EntityLocationComponent>(out var loc))
                    sb.AppendLine("地点=" + loc.LocationId);
                if (e.TryGet<WorkAssignmentComponent>(out var work) && work.IsAssigned)
                    sb.AppendLine("分工=" + work.Role);
            }

            GUI.Box(new Rect(8f, 76f, 280f, 160f), sb.ToString());
        }

        void DrawSchedulePanel(PlayableHostSession session)
        {
            var sb = new StringBuilder(320);
            sb.AppendLine("课表(只读)");
            var tickInDay = (int)(session.World.Tick.Value % (ulong)WorldTick.TicksPerDay);
            var hour = DayClock.FromWorldTick(session.World.Tick).HourOfDay;
            sb.AppendLine("时=" + hour + " tickInDay=" + tickInDay);

            var focus = EntityId.None;
            if (selectionController != null && selectionController.State.Count > 0)
                focus = selectionController.State.SelectedIds[0];
            if (focus.IsNone && session.CharacterIds.Count > 0)
                focus = session.CharacterIds[0];

            if (!focus.IsNone &&
                session.World.Entities.TryGet(focus, out var e) &&
                e.TryGet<ScheduleComponent>(out var sched) &&
                session.World.TryGetSchedule(sched.DefinitionId, out var def))
            {
                sb.AppendLine(def.Id);
                for (var i = 0; i < def.Blocks.Count && i < 6; i++)
                {
                    var b = def.Blocks[i];
                    var mark = tickInDay >= b.StartTickInDay && tickInDay < b.EndTickInDay ? ">" : " ";
                    sb.AppendLine(mark + b.StartTickInDay + "-" + b.EndTickInDay + " " + b.Activity);
                }
            }
            else
            {
                sb.Append("(无日程)");
            }

            GUI.Box(new Rect(Screen.width - 300f, 76f, 290f, 160f), sb.ToString());
        }

        void DrawQuestPanel(PlayableHostSession session)
        {
            var sb = new StringBuilder(256);
            sb.AppendLine("任务");
            var n = 0;
            foreach (var kv in session.World.Quests.Runtime)
            {
                if (kv.Value.Status == QuestStatus.Inactive)
                    continue;
                sb.AppendLine(kv.Value.Status + "  " + kv.Key);
                if (++n >= 8)
                    break;
            }

            if (n == 0)
                sb.Append("(无进行中)");
            GUI.Box(new Rect(8f, 244f, 280f, 150f), sb.ToString());
        }

        void DrawEventPanel()
        {
            var feed = eventFeed != null ? eventFeed.LastStatusLine : "";
            var active = "";
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session != null && session.World.ContentEvents.HasActive)
                active = "进行中事件: " + session.World.ContentEvents.ActiveEventId + "\n";
            GUI.Box(
                new Rect(Screen.width - 360f, Screen.height - 160f, 350f, 150f),
                "事件窗口\n" + active + feed);
        }
    }
}
