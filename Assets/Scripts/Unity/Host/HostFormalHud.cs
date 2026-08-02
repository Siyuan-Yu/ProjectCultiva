using System.Text;
using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Concealment;
using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Input;
using XianXia.Core.Labor;
using XianXia.Core.Schedule;
using XianXia.Core.Settlement;
using XianXia.Core.Social;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Demo-aligned play HUD (IMGUI): top status, right rails, bottom unit bar.
    /// Not product UGUI skin — layout／信息对齐 [49] 可验收密度。
    /// </summary>
    public sealed class HostFormalHud : MonoBehaviour
    {
        const float TopH = 48f;
        const float BottomH = 110f;
        const float RailW = 260f;
        const float Pad = 8f;

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostEventFeed eventFeed;
        [SerializeField] HostCommandBridge commandBridge;
        [SerializeField] HostDebugHud debugHud;
        [SerializeField] bool visible = true;
        [SerializeField] KeyCode toggleKey = KeyCode.F6;

        GUIStyle _title;
        GUIStyle _body;
        bool _stylesReady;

        public void Bind(
            PlayableHostBootstrap host,
            HostSelectionController selection,
            HostEventFeed feed)
        {
            bootstrap = host;
            selectionController = selection;
            eventFeed = feed;
            if (host != null)
            {
                commandBridge = host.CommandBridge;
                debugHud = host.DebugHud;
            }
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

            EnsureStyles();
            DrawTopBar(session);
            DrawRightRail(session);
            DrawBottomBar(session);
        }

        void EnsureStyles()
        {
            if (_stylesReady)
                return;
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = new Color(0.92f, 0.92f, 0.92f) }
            };
            _stylesReady = true;
        }

        void DrawTopBar(PlayableHostSession session)
        {
            var day = session.CurrentDayClock;
            var night = ConcealmentExposureRules.IsNight(session.World.Tick);
            var speed = debugHud != null ? debugHud.SpeedMultiplier : 1;
            var paused = session.IsPaused;

            GUI.Box(new Rect(0f, 0f, Screen.width, TopH), GUIContent.none);

            var clock = "第" + day.DayIndex + "天  " +
                        day.HourOfDay.ToString("00") + ":00  " +
                        (night ? "夜" : "昼") + "  " +
                        (paused ? "暂停" : speed + "x");
            GUI.Label(new Rect(Pad, 12f, 280f, 24f), clock, _title);

            var x = 300f;
            if (GUI.Button(new Rect(x, 8f, 56f, 32f), paused ? "继续" : "暂停"))
                session.IsPaused = !session.IsPaused;
            x += 60f;
            if (GUI.Button(new Rect(x, 8f, 40f, 32f), "1x") && debugHud != null)
                debugHud.SetSpeedMultiplier(1);
            x += 44f;
            if (GUI.Button(new Rect(x, 8f, 40f, 32f), "2x") && debugHud != null)
                debugHud.SetSpeedMultiplier(2);
            x += 44f;
            if (GUI.Button(new Rect(x, 8f, 40f, 32f), "5x") && debugHud != null)
                debugHud.SetSpeedMultiplier(5);
            x += 52f;

            if (GUI.Button(new Rect(x, 8f, 52f, 32f), "入定"))
                Issue(PlayerCommandKind.Cultivate);
            x += 56f;
            if (GUI.Button(new Rect(x, 8f, 52f, 32f), "出定"))
                Issue(PlayerCommandKind.Stop);
            x += 56f;
            if (GUI.Button(new Rect(x, 8f, 64f, 32f), "敛息草"))
                Issue(PlayerCommandKind.UseConcealGrass);

            var wood = 0;
            var herb = 0;
            var grain = 0;
            var grass = 0;
            if (session.World.Settlements.TryGetPrimary(out var s))
            {
                wood = s.GetStock("base:resource_rough_wood");
                herb = s.GetStock("base:resource_spirit_herb");
                grain = s.GetStock("base:resource_grain");
                grass = s.GetStock("base:resource_conceal_grass");
            }

            var anger = session.World.SupervisorAnger != null ? session.World.SupervisorAnger.Value : 0;
            var res = "木 " + wood + "   粮 " + grain + "   药 " + herb + "   敛息草 " + grass +
                      "   愤怒 " + anger;
            GUI.Label(new Rect(Screen.width - 520f, 14f, 508f, 24f), res, _body);
        }

        void DrawRightRail(PlayableHostSession session)
        {
            var x = Screen.width - RailW - Pad;
            var y = TopH + Pad;
            var h = (Screen.height - TopH - BottomH - Pad * 3f) / 3f;

            DrawPanel(new Rect(x, y, RailW, h), "课表（只读）", BuildScheduleText(session));
            y += h + Pad;
            DrawPanel(new Rect(x, y, RailW, h), "任务", BuildQuestText(session));
            y += h + Pad;
            DrawPanel(new Rect(x, y, RailW, h), "事件", BuildEventText(session));
        }

        void DrawBottomBar(PlayableHostSession session)
        {
            var rect = new Rect(0f, Screen.height - BottomH, Screen.width - RailW - Pad * 2f, BottomH);
            GUI.Box(rect, GUIContent.none);

            var focus = ResolveFocus(session);
            var left = rect.x + Pad;
            var top = rect.y + 8f;
            var colW = 320f;

            if (focus.IsNone || !session.World.Entities.TryGet(focus, out var entity))
            {
                GUI.Label(new Rect(left, top, colW, 24f), "未选择我方角色 — 左键点选三人小队", _title);
                GUI.Label(
                    new Rect(left, top + 28f, rect.width - Pad * 2f, 60f),
                    "框选／点选己方 · 右键移动／工区／灵地 · W 工区模式 · S 停止 · C 入定 · X 出定 · G 敛息",
                    _body);
                return;
            }

            var party = selectionController != null && selectionController.IsPartyUnit(focus);
            var name = string.IsNullOrEmpty(entity.DisplayName) ? focus.ToString() : entity.DisplayName;
            var title = party ? name : name + "（查看）";
            GUI.Label(new Rect(left, top, colW, 22f), title, _title);

            var sb = new StringBuilder(192);
            if (entity.TryGet<CultivationComponent>(out var cult))
                sb.Append("修为 ").Append(cult.Progress).Append("  境界 ").Append(cult.Realm).Append("   ");
            if (entity.TryGet<PersonalConcealmentRiskComponent>(out var risk))
                sb.Append("暴露 ").Append(risk.Value).Append("   ");
            if (entity.TryGet<DailyTaskComponent>(out var daily))
                sb.Append("日课 ").Append(daily.CompletedAmount).Append('/').Append(daily.RequiredAmount).Append("   ");
            sb.Append(DescribeAction(session, entity));
            if (entity.TryGet<EntityLocationComponent>(out var loc) && loc.HasLocation &&
                session.World.WorldRegion.TryGet(loc.LocationId, out var place))
                sb.Append("   地点 ").Append(string.IsNullOrEmpty(place.Name) ? place.Id : place.Name);

            GUI.Label(new Rect(left, top + 26f, rect.width - 220f, 40f), sb.ToString(), _body);

            if (!party)
                return;

            var bx = rect.xMax - 210f;
            var by = rect.y + 16f;
            if (GUI.Button(new Rect(bx, by, 90f, 28f), "劳动"))
                Issue(PlayerCommandKind.Labor);
            if (GUI.Button(new Rect(bx + 96f, by, 90f, 28f), "停止"))
                Issue(PlayerCommandKind.Stop);
            by += 34f;
            if (GUI.Button(new Rect(bx, by, 90f, 28f), "休息"))
                Issue(PlayerCommandKind.Rest);
            if (GUI.Button(new Rect(bx + 96f, by, 90f, 28f), "修炼"))
                Issue(PlayerCommandKind.Cultivate);

            if (selectionController != null && selectionController.State.Count > 1)
            {
                GUI.Label(
                    new Rect(left, top + 68f, 280f, 22f),
                    "已选 " + selectionController.State.Count + " 人",
                    _body);
            }
        }

        void DrawPanel(Rect rect, string title, string body)
        {
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 22f), title, _title);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 30f, rect.width - 16f, rect.height - 38f), body, _body);
        }

        string BuildScheduleText(PlayableHostSession session)
        {
            var focus = ResolveFocus(session);
            if (focus.IsNone && session.CharacterIds.Count > 0)
                focus = session.CharacterIds[0];

            var sb = new StringBuilder(256);
            var tickInDay = (int)(session.World.Tick.Value % (ulong)WorldTick.TicksPerDay);
            if (focus.IsNone ||
                !session.World.Entities.TryGet(focus, out var e) ||
                !e.TryGet<ScheduleComponent>(out var sched) ||
                !session.World.TryGetSchedule(sched.DefinitionId, out var def))
            {
                sb.Append("无日程");
                return sb.ToString();
            }

            var shortId = ShortId(def.Id);
            sb.AppendLine(shortId);
            for (var i = 0; i < def.Blocks.Count; i++)
            {
                var b = def.Blocks[i];
                var mark = tickInDay >= b.StartTickInDay && tickInDay < b.EndTickInDay ? "► " : "  ";
                sb.Append(mark)
                    .Append(TickToClock(b.StartTickInDay))
                    .Append('-')
                    .Append(TickToClock(b.EndTickInDay))
                    .Append(' ')
                    .Append(ActivityName(b.Activity))
                    .Append('\n');
            }

            return sb.ToString();
        }

        string BuildQuestText(PlayableHostSession session)
        {
            var sb = new StringBuilder(256);
            var n = 0;
            foreach (var kv in session.World.Quests.Runtime)
            {
                if (kv.Value.Status == QuestStatus.Inactive)
                    continue;
                sb.Append("· ")
                    .Append(QuestStatusName(kv.Value.Status))
                    .Append(' ')
                    .Append(ShortId(kv.Key))
                    .Append('\n');
                if (++n >= 8)
                    break;
            }

            if (session.CharacterIds.Count > 0 &&
                session.World.Entities.TryGet(session.CharacterIds[0], out var e) &&
                e.TryGet<DailyTaskComponent>(out var daily))
            {
                sb.Append('\n')
                    .Append("日课进度 ")
                    .Append(daily.CompletedAmount)
                    .Append('/')
                    .Append(daily.RequiredAmount);
            }

            if (n == 0 && sb.Length < 8)
                sb.Append("暂无进行中任务");
            return sb.ToString();
        }

        string BuildEventText(PlayableHostSession session)
        {
            var sb = new StringBuilder(256);
            if (session.World.ContentEvents.HasActive)
                sb.AppendLine("进行中：" + ShortId(session.World.ContentEvents.ActiveEventId));
            if (eventFeed != null && eventFeed.Count > 0)
            {
                var lines = eventFeed.Lines;
                var start = lines.Count > 5 ? lines.Count - 5 : 0;
                for (var i = start; i < lines.Count; i++)
                    sb.AppendLine(SimplifyEventLine(lines[i]));
            }
            else if (!session.World.ContentEvents.HasActive)
            {
                sb.Append("暂无事件");
            }

            return sb.ToString();
        }

        EntityId ResolveFocus(PlayableHostSession session)
        {
            if (selectionController != null && selectionController.State.Count > 0)
                return selectionController.State.SelectedIds[0];
            return EntityId.None;
        }

        void Issue(PlayerCommandKind kind)
        {
            if (commandBridge == null)
                return;
            var dur = kind == PlayerCommandKind.Stop || kind == PlayerCommandKind.UseConcealGrass
                ? 0UL
                : HostCommandBridge.DefaultDurationTicks;
            commandBridge.IssueSelected(kind, dur);
        }

        static string DescribeAction(PlayableHostSession session, Entity entity)
        {
            if (!entity.TryGet<ActionStateComponent>(out var st) || !st.HasActiveAction)
                return "空闲";
            if (!session.World.ActiveActions.TryGetValue(st.ActiveActionId, out var action))
                return "行动中";
            if (action is LaborAction) return "工作中";
            if (action is CultivateAction) return "修炼中";
            if (action is RestAction) return "休息中";
            if (action is ObserveAction) return "巡查中";
            return "行动中";
        }

        static string ActivityName(ScheduleActivity a)
        {
            switch (a)
            {
                case ScheduleActivity.Labor: return "工作";
                case ScheduleActivity.Rest: return "休息";
                case ScheduleActivity.Eat: return "吃饭";
                case ScheduleActivity.Cultivate: return "修炼";
                case ScheduleActivity.Explore: return "探索";
                case ScheduleActivity.Patrol: return "巡视";
                case ScheduleActivity.Inspect: return "检查";
                default: return a.ToString();
            }
        }

        static string QuestStatusName(QuestStatus s)
        {
            switch (s)
            {
                case QuestStatus.Active: return "进行";
                case QuestStatus.Completed: return "完成";
                case QuestStatus.Failed: return "失败";
                default: return s.ToString();
            }
        }

        static string TickToClock(int tickInDay)
        {
            var hour = tickInDay * WorldTick.GameMinutesPerTick / 60;
            if (hour < 0) hour = 0;
            if (hour > 24) hour = 24;
            return hour.ToString("00") + ":00";
        }

        static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "-";
            var i = id.LastIndexOf('_');
            if (i >= 0 && i + 1 < id.Length)
                return id.Substring(i + 1);
            i = id.IndexOf(':');
            return i >= 0 && i + 1 < id.Length ? id.Substring(i + 1) : id;
        }

        static string SimplifyEventLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return line;
            // Drop noisy id prefixes for play HUD.
            return line.Length > 64 ? line.Substring(0, 61) + "…" : line;
        }
    }
}
