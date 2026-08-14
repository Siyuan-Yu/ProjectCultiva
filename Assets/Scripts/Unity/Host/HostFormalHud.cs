using System.Text;
using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Attributes;
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
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Play HUD (IMGUI)：顶栏资源／时间 + 右栏任务／课表 + 底部 ACS 风格角色面板。
    /// 非产品 UGUI 皮肤；布局对齐了不起的修仙模拟器底栏信息密度，数据仅绑现有 Core。
    /// </summary>
    public sealed class HostFormalHud : MonoBehaviour
    {
        const float TopH = 48f;
        const float BottomH = 210f;
        const float RailW = 260f;
        const float Pad = 8f;
        const float PanelW = 560f;
        const float ActionOrb = 44f;

        enum UnitTab
        {
            Overview = 0,
            Relation = 1
        }

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostEventFeed eventFeed;
        [SerializeField] HostCommandBridge commandBridge;
        [SerializeField] HostDebugHud debugHud;
        [SerializeField] bool visible = true;
        [SerializeField] KeyCode toggleKey = KeyCode.F10;

        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _parchmentTitle;
        GUIStyle _parchmentBody;
        GUIStyle _small;
        bool _stylesReady;
        UnitTab _unitTab = UnitTab.Overview;
        Texture2D _px;

        static readonly Color Parchment = new Color(0.90f, 0.84f, 0.72f, 0.96f);
        static readonly Color ParchmentDark = new Color(0.72f, 0.62f, 0.48f, 1f);
        static readonly Color Ink = new Color(0.18f, 0.14f, 0.10f, 1f);
        static readonly Color BarOrange = new Color(0.92f, 0.62f, 0.22f, 1f);
        static readonly Color BarBlue = new Color(0.30f, 0.55f, 0.85f, 1f);
        static readonly Color AccentGold = new Color(0.95f, 0.78f, 0.28f, 1f);

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

            var session = bootstrap != null ? bootstrap.Session : null;
            if (!visible || session == null || !session.IsInitialized)
                return;
            if (bootstrap.ContentInterrupt != null && bootstrap.ContentInterrupt.HasBlockingInterrupt)
                return;
            if (bootstrap.QuestJournal != null && bootstrap.QuestJournal.IsOpen)
                return;
            if (session.World.ContentEvents.HasActive)
                return;

            HandleActionHotkeys(session);
        }

        void OnGUI()
        {
            if (!visible)
                return;
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session == null || !session.IsInitialized)
                return;

            EnsureStyles();
            HostUiHitTest.BeginFrame();
            if (bootstrap != null &&
                bootstrap.ContentInterrupt != null &&
                bootstrap.ContentInterrupt.HasBlockingInterrupt)
            {
                HostUiHitTest.Block(new Rect(0f, 0f, Screen.width, Screen.height));
            }

            DrawTopBar(session);
            DrawOpsLegend(session);
            DrawRightRail(session);
            if (!ShouldHideUnitPanelForDialogue())
                DrawAcsUnitPanel(session);
            HostUiHitTest.EndFrame();
        }

        bool ShouldHideUnitPanelForDialogue() =>
            bootstrap != null &&
            bootstrap.DialoguePresenter != null &&
            bootstrap.DialoguePresenter.IsActive;

        void DrawOpsLegend(PlayableHostSession session)
        {
            if (bootstrap != null &&
                bootstrap.ContentInterrupt != null &&
                bootstrap.ContentInterrupt.HasBlockingInterrupt)
                return;

            var tip = BuildContextTip(session);
            var r = new Rect(Pad, TopH + 2f, Screen.width - RailW - Pad * 3f, 36f);
            Fill(r, new Color(0.10f, 0.12f, 0.14f, 0.82f));
            HostUiHitTest.Block(r);
            GUI.Label(
                new Rect(r.x + 8f, r.y + 2f, r.width - 16f, r.height - 4f),
                tip,
                _body);
        }

        string BuildContextTip(PlayableHostSession session)
        {
            var baseOps =
                "操作：左键选人 · 悬停黄/青点可交互 · 右键空地移动／热点交互 · 右键 NPC 对话/攻击 · Space暂停 · F10显隐HUD";
            var focus = ResolveFocus(session);
            if (!focus.IsNone &&
                session.World.Entities.TryGet(focus, out var e) &&
                e.TryGet<EntityLocationComponent>(out var loc) &&
                loc.HasLocation)
            {
                foreach (var kv in session.World.Quests.Runtime)
                {
                    if (kv.Value.Status != QuestStatus.Active)
                        continue;
                    if (!session.World.Quests.TryGetSpec(kv.Key, out var spec))
                        continue;
                    if (QuestLooksLikeExploreHere(spec, loc.LocationId))
                        return "下一步：走进／再走进目标区域（首次入区自动勘察）｜" + baseOps;
                    if (QuestLooksLikeLabor(spec))
                        return "下一步：点「交互」再左键工区（麦田／树林／药田／矿洞）；可分派三人｜" + baseOps;
                    if (QuestLooksLikeCultivate(spec))
                        return "下一步：点「修炼」再左键灵泉／洞府｜" + baseOps;
                    if (!string.IsNullOrEmpty(spec.Name))
                        return "当前任务：" + spec.Name + "｜" + baseOps;
                }
            }

            return baseOps;
        }

        static bool QuestLooksLikeExploreHere(QuestSpec spec, string locationId)
        {
            if (spec?.CompleteConditions == null)
                return false;
            for (var i = 0; i < spec.CompleteConditions.Count; i++)
            {
                var c = spec.CompleteConditions[i];
                if (c == null)
                    continue;
                if (string.Equals(c.Kind, "exploredLocation", System.StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrEmpty(c.Id) || string.Equals(c.Id, locationId, System.StringComparison.Ordinal)))
                    return true;
            }

            return false;
        }

        static bool QuestLooksLikeLabor(QuestSpec spec)
        {
            if (spec?.CompleteConditions == null)
                return false;
            for (var i = 0; i < spec.CompleteConditions.Count; i++)
            {
                var c = spec.CompleteConditions[i];
                if (c == null)
                    continue;
                if (string.Equals(c.Kind, "stockAtLeast", System.StringComparison.OrdinalIgnoreCase))
                    return true;
                if (string.Equals(c.Kind, "laborAtLocation", System.StringComparison.OrdinalIgnoreCase))
                    return true;
                if (string.Equals(c.Kind, "uniqueLaborAtLocation", System.StringComparison.OrdinalIgnoreCase))
                    return true;
                if (string.Equals(c.Kind, "uniqueHarvestAtLocation", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static bool QuestLooksLikeCultivate(QuestSpec spec)
        {
            if (spec?.CompleteConditions == null)
                return false;
            for (var i = 0; i < spec.CompleteConditions.Count; i++)
            {
                var c = spec.CompleteConditions[i];
                if (c == null)
                    continue;
                if (string.Equals(c.Kind, "hasManual", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Kind, "realmAtLeast", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        void EnsureStyles()
        {
            if (_stylesReady)
                return;
            if (_px == null)
            {
                _px = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _px.SetPixel(0, 0, Color.white);
                _px.Apply();
            }

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
            _parchmentTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Ink }
            };
            _parchmentBody = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = Ink }
            };
            _small = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Ink }
            };
            _stylesReady = true;
        }

        void DrawTopBar(PlayableHostSession session)
        {
            var day = session.CurrentDayClock;
            var night = ConcealmentExposureRules.IsNight(session.World.Tick);
            var speed = bootstrap != null ? bootstrap.EffectiveSpeedMultiplier() : (debugHud != null ? debugHud.SpeedMultiplier : 1);
            var paused = session.IsPaused;
            var pace = bootstrap != null ? bootstrap.EffectiveGameMinutesPerRealSecond() : speed * SimulationTickPacing.GameMinutesPerRealSecondAt1x;

            Fill(new Rect(0f, 0f, Screen.width, TopH), new Color(0.12f, 0.12f, 0.14f, 0.92f));
            HostUiHitTest.Block(new Rect(0f, 0f, Screen.width, TopH));

            var clock = "第" + day.DayIndex + "天  " +
                        day.HourOfDay.ToString("00") + ":" +
                        day.MinuteOfHour.ToString("00") + "  " +
                        (night ? "夜" : "昼") + "  " +
                        (paused ? "暂停" : speed + "x·" + pace + "分/秒");
            GUI.Label(new Rect(Pad, 12f, 280f, 24f), clock, _title);

            var x = 300f;
            if (GUI.Button(new Rect(x, 8f, 56f, 32f), paused ? "继续" : "暂停"))
            {
                var blocking = bootstrap != null &&
                               bootstrap.ContentInterrupt != null &&
                               bootstrap.ContentInterrupt.HasBlockingInterrupt;
                if (!blocking && !session.World.ContentEvents.HasActive)
                    session.IsPaused = !session.IsPaused;
            }

            x += 60f;
            if (GUI.Button(new Rect(x, 8f, 40f, 32f), "1x") && debugHud != null)
                debugHud.SetSpeedMultiplier(1);
            x += 44f;
            if (GUI.Button(new Rect(x, 8f, 40f, 32f), "2x") && debugHud != null)
                debugHud.SetSpeedMultiplier(2);
            x += 44f;
            if (GUI.Button(new Rect(x, 8f, 40f, 32f), "5x") && debugHud != null)
                debugHud.SetSpeedMultiplier(5);

            var bag = session.World.Inventory;
            var wood = bag.GetCount("base:resource_rough_wood");
            var herb = bag.GetCount("base:resource_spirit_herb");
            var grain = bag.GetCount("base:resource_grain");
            var grass = bag.GetCount("base:resource_conceal_grass");

            var anger = session.World.SupervisorAnger != null ? session.World.SupervisorAnger.Value : 0;
            var exposure = ResolvePartyExposure(session);
            var used = bag.UsedSlotCount;
            var cap = bag.SlotCapacity;
            var res = "背包 " + used + "/" + cap + "   木 " + wood + "   粮 " + grain + "   药 " + herb + "   敛息草 " + grass;
            GUI.Label(new Rect(Screen.width - RailW - 480f, 4f, 390f, 18f), res, _body);
            if (GUI.Button(new Rect(Screen.width - RailW - 82f, 6f, 70f, 28f), "背包"))
            {
                var panel = bootstrap != null ? bootstrap.InventoryPanel : null;
                panel?.Toggle();
            }

            // 暴露／主管压：全局条（非每人一条）
            DrawInlineMeter(
                Screen.width - RailW - 380f, 24f, 170f,
                "暴露", exposure, 100, new Color(0.85f, 0.35f, 0.25f));
            DrawInlineMeter(
                Screen.width - RailW - 200f, 24f, 170f,
                "主管压", anger, 100, new Color(0.75f, 0.28f, 0.22f));
        }

        static int ResolvePartyExposure(PlayableHostSession session)
        {
            var max = 0;
            for (var i = 0; i < session.CharacterIds.Count; i++)
            {
                if (!session.World.Entities.TryGet(session.CharacterIds[i], out var e))
                    continue;
                if (!e.TryGet<PersonalConcealmentRiskComponent>(out var risk))
                    continue;
                if (risk.Value > max)
                    max = risk.Value;
            }

            return max;
        }

        void DrawInlineMeter(float x, float y, float w, string label, int cur, int max, Color fill)
        {
            var labelStyle = new GUIStyle(_body) { fontSize = 11, alignment = TextAnchor.MiddleLeft };
            GUI.Label(new Rect(x, y - 2f, 48f, 18f), label, labelStyle);
            var bar = new Rect(x + 44f, y + 2f, w - 48f, 12f);
            Fill(bar, new Color(0.25f, 0.25f, 0.28f, 0.9f));
            var pct = max > 0 ? Mathf.Clamp01(cur / (float)max) : 0f;
            Fill(new Rect(bar.x + 1f, bar.y + 1f, (bar.width - 2f) * pct, bar.height - 2f), fill);
            DrawFrame(bar, new Color(0.7f, 0.7f, 0.75f, 0.8f));
            var valueStyle = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            GUI.Label(bar, cur + "/" + max, valueStyle);
        }

        void DrawRightRail(PlayableHostSession session)
        {
            var x = Screen.width - RailW - Pad;
            var y = TopH + Pad;
            var h = (Screen.height - TopH - BottomH - Pad * 3f) / 3f;
            if (h < 80f)
                h = 80f;

            DrawPanel(new Rect(x, y, RailW, h), "课表（只读·己方不自动）", BuildScheduleText(session));
            HostUiHitTest.Block(new Rect(x, y, RailW, h));
            y += h + Pad;
            DrawPanel(new Rect(x, y, RailW, h), "任务", BuildQuestText(session));
            HostUiHitTest.Block(new Rect(x, y, RailW, h));
            y += h + Pad;
            DrawPanel(new Rect(x, y, RailW, h), "事件", BuildEventText(session));
            HostUiHitTest.Block(new Rect(x, y, RailW, h));
        }

        void DrawAcsUnitPanel(PlayableHostSession session)
        {
            if (ShouldHideUnitPanelForDialogue())
                return;

            var focus = ResolveFocus(session);
            // 点选任意单位打开信息面板；指令钮只对己方出现在面板上方。
            if (focus.IsNone ||
                selectionController == null ||
                !session.World.Entities.TryGet(focus, out var entity))
            {
                DrawClosedUnitHint();
                return;
            }

            var isParty = selectionController.IsPartyUnit(focus);
            var panelH = BottomH - 18f;
            var panelX = (Screen.width - PanelW) * 0.5f;
            var panelY = Screen.height - panelH - 10f;
            var actionY = panelY - ActionOrb - 10f;

            if (isParty)
            {
                DrawActionOrbRow(panelX, actionY, PanelW, focus);
                HostUiHitTest.Block(new Rect(panelX, actionY, PanelW, ActionOrb + 8f));
            }

            var main = new Rect(panelX, panelY, PanelW, panelH);
            Fill(main, Parchment);
            DrawFrame(main, ParchmentDark);
            HostUiHitTest.Block(main);
            DrawUnitTabs(new Rect(main.xMax - 2f, main.y + 18f, 36f, main.height - 28f));
            HostUiHitTest.Block(new Rect(main.xMax - 2f, main.y + 18f, 36f, main.height - 28f));

            var name = string.IsNullOrEmpty(entity.DisplayName) ? focus.ToString() : entity.DisplayName;
            var activity = DescribeAction(session, entity);
            var realm = entity.TryGet<CultivationComponent>(out var cult) ? RealmName(cult.Realm) : "—";
            var subtitle = isParty ? "己方 · 上方可下令" : "查看 · 非己方不可下令";
            GUI.Label(
                new Rect(main.x + 14f, main.y + 8f, 400f, 24f),
                name + "（" + activity + "）· " + subtitle,
                _parchmentTitle);
            GUI.Label(new Rect(main.xMax - 150f, main.y + 8f, 120f, 24f), realm, _parchmentTitle);

            var content = new Rect(main.x + 12f, main.y + 36f, main.width - 52f, main.height - 88f);
            if (_unitTab == UnitTab.Relation)
                GUI.Label(content, BuildRelationText(session, focus), _parchmentBody);
            else
                DrawOverviewBars(session, entity, cult, content);

            if (isParty && selectionController.State.Count > 1)
            {
                GUI.Label(
                    new Rect(main.x + 14f, main.y + 30f, main.width - 60f, 18f),
                    "框选 " + selectionController.State.Count + " 人时：指令只令「" + name + "」；群体移动请右键",
                    _small);
            }
        }

        void DrawClosedUnitHint()
        {
            var r = new Rect((Screen.width - 520f) * 0.5f, Screen.height - 36f, 520f, 28f);
            Fill(r, new Color(0.12f, 0.12f, 0.14f, 0.75f));
            GUI.Label(r, "点选角色查看 · 己方上方下令（默认不自动行动）· 右键也可移动", _body);
        }

        void DrawActionOrbRow(float x, float y, float width, EntityId focus)
        {
            var labels = new[]
            {
                "Q\n移动", "F1\n停止", "E\n交互", "F8\n战斗", "F6\n修炼"
            };

            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            btnStyle.normal.textColor = Ink;
            btnStyle.hover.textColor = Ink;
            btnStyle.active.textColor = Ink;

            var mode = bootstrap != null ? bootstrap.WorkTargetMode : null;
            var gap = 10f;
            var total = ActionOrb * labels.Length + gap * (labels.Length - 1);
            var startX = x + (width - total) * 0.5f;
            for (var i = 0; i < labels.Length; i++)
            {
                var r = new Rect(startX + i * (ActionOrb + gap), y, ActionOrb, ActionOrb);
                var armedMatch = mode != null && (
                    (i == 0 && mode.Armed == HostWorkTargetMode.ArmKind.Move) ||
                    (i == 2 && mode.Armed == HostWorkTargetMode.ArmKind.Interact) ||
                    (i == 3 && mode.Armed == HostWorkTargetMode.ArmKind.Combat) ||
                    (i == 4 && mode.Armed == HostWorkTargetMode.ArmKind.Cultivate));
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = armedMatch ? AccentGold : new Color(0.95f, 0.93f, 0.88f, 1f);
                GUI.enabled = commandBridge != null || mode != null;
                if (GUI.Button(r, labels[i], btnStyle))
                {
                    Event.current.Use();
                    InvokeActionIndex(focus, i);
                }

                GUI.enabled = true;
                GUI.backgroundColor = prev;
            }
        }

        void DrawUnitTabs(Rect strip)
        {
            var names = new[] { "况", "系" };
            var h = 34f;
            for (var i = 0; i < names.Length; i++)
            {
                var r = new Rect(strip.x, strip.y + i * (h + 6f), strip.width, h);
                var on = (int)_unitTab == i;
                Fill(r, on ? AccentGold : ParchmentDark);
                if (GUI.Button(r, names[i], _small))
                    _unitTab = (UnitTab)i;
            }
        }

        void DrawOverviewBars(PlayableHostSession session, Entity entity, CultivationComponent cult, Rect area)
        {
            var progress = cult != null ? cult.Progress : 0;
            const int progressMax = 100;

            var hpCur = 0;
            var hpMax = 100;
            if (entity.TryGet<AttributesComponent>(out var attrs))
            {
                hpMax = Mathf.Max(1, attrs.GetBase(AttributeId.MaxHp));
                // 暂无独立当前生命时用满值展示上限；条满表示体魄上限。
                hpCur = hpMax;
            }

            var left = new Rect(area.x, area.y, area.width * 0.48f, area.height);
            var right = new Rect(area.x + area.width * 0.52f, area.y, area.width * 0.48f, area.height);

            DrawStatBar(left.x, left.y + 4f, left.width, "体魄", hpCur, hpMax, BarOrange);

            DrawStatBar(right.x, right.y + 4f, right.width, "修为", progress, progressMax, BarBlue);

            var manualName = "未得功法";
            if (cult != null && cult.HasLearnedManual && cult.LearnedManualId.HasValue)
            {
                var mid = cult.LearnedManualId.Value.ToString();
                var slash = mid.LastIndexOf(':');
                manualName = slash >= 0 && slash < mid.Length - 1 ? mid.Substring(slash + 1) : mid;
            }

            GUI.Label(
                new Rect(right.x, right.y + 30f, right.width, 22f),
                "功法 " + manualName,
                _parchmentBody);

            if (entity.TryGet<EntityLocationComponent>(out var loc) && loc.HasLocation &&
                session.World.WorldRegion.TryGet(loc.LocationId, out var place))
            {
                var placeName = string.IsNullOrEmpty(place.Name) ? place.Id : place.Name;
                GUI.Label(
                    new Rect(left.x, left.y + 30f, area.width, 22f),
                    "地点 " + placeName,
                    _parchmentBody);
            }
        }

        void DrawStatBar(float x, float y, float w, string label, int cur, int max, Color fill)
        {
            GUI.Label(new Rect(x, y, 48f, 20f), label, _parchmentBody);
            var bar = new Rect(x + 50f, y + 4f, w - 54f, 14f);
            Fill(bar, new Color(0.55f, 0.48f, 0.38f, 0.55f));
            var pct = max > 0 ? Mathf.Clamp01(cur / (float)max) : 0f;
            var inner = new Rect(bar.x + 1f, bar.y + 1f, (bar.width - 2f) * pct, bar.height - 2f);
            Fill(inner, fill);
            DrawFrame(bar, Ink);
            var valueStyle = new GUIStyle(_parchmentBody)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = Ink }
            };
            GUI.Label(bar, cur + "/" + max, valueStyle);
        }

        void DrawPanel(Rect rect, string title, string body)
        {
            Fill(rect, new Color(0.14f, 0.14f, 0.16f, 0.88f));
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 22f), title, _title);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 30f, rect.width - 16f, rect.height - 38f), body, _body);
        }

        void HandleActionHotkeys(PlayableHostSession session)
        {
            var focus = ResolveFocus(session);
            if (focus.IsNone || selectionController == null || !selectionController.IsPartyUnit(focus))
                return;

            // F5／F9 留给存读档。Q/E/R＝移动/交互/战斗点选；F1＝停止；F6＝修炼；G＝敛息（道具）。
            if (Input.GetKeyDown(KeyCode.Q) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
                InvokeActionIndex(focus, 0);
            else if (Input.GetKeyDown(KeyCode.F1))
                InvokeActionIndex(focus, 1);
            else if (Input.GetKeyDown(KeyCode.E) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
                InvokeActionIndex(focus, 2);
            else if (Input.GetKeyDown(KeyCode.F8))
                InvokeActionIndex(focus, 3);
            else if (Input.GetKeyDown(KeyCode.F6))
                InvokeActionIndex(focus, 4);
            else if (Input.GetKeyDown(KeyCode.G))
                IssueFocus(focus, PlayerCommandKind.UseConcealGrass);
        }

        void InvokeActionIndex(EntityId focus, int index)
        {
            if (focus.IsNone)
                return;
            if (selectionController != null && !selectionController.IsPartyUnit(focus))
                return;
            EnsureFocusSelected(focus);
            var mode = bootstrap != null ? bootstrap.WorkTargetMode : null;
            switch (index)
            {
                case 0:
                    if (mode != null) mode.ArmMove();
                    break;
                case 1:
                    IssueFocus(focus, PlayerCommandKind.Stop);
                    if (mode != null) mode.Cancel();
                    break;
                case 2:
                    if (mode != null) mode.ArmInteract();
                    break;
                case 3:
                    if (mode != null) mode.ArmCombat();
                    break;
                case 4:
                    ArmCultivateSmart(focus, mode);
                    break;
            }
        }

        void ArmCultivateSmart(EntityId focus, HostWorkTargetMode mode)
        {
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session != null &&
                session.World.Entities.TryGet(focus, out var e) &&
                e.TryGet<EntityLocationComponent>(out var loc) &&
                HostZoneQuery.LocationIsCultivate(session.World, loc.LocationId))
            {
                if (!session.World.ContentEvents.HasActive)
                    session.IsPaused = false;
                IssueFocus(focus, PlayerCommandKind.Cultivate);
                return;
            }

            if (mode != null)
                mode.ArmCultivate();
            else
                IssueFocus(focus, PlayerCommandKind.Cultivate);
        }

        void EnsureFocusSelected(EntityId focus)
        {
            if (selectionController == null || focus.IsNone)
                return;
            if (!selectionController.State.Contains(focus))
                selectionController.SelectEntity(focus, false);
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
            var sb = new StringBuilder(320);
            var journal = bootstrap != null ? bootstrap.QuestJournal : null;
            var trackedId = journal != null ? journal.TrackedQuestId : string.Empty;

            if (!string.IsNullOrEmpty(trackedId) &&
                session.World.Quests.TryGet(trackedId, out var rt) &&
                rt.Status != QuestStatus.Failed &&
                rt.Status != QuestStatus.Completed &&
                session.World.Quests.TryGetSpec(trackedId, out var spec))
            {
                var title = string.IsNullOrEmpty(spec.Name) ? trackedId : spec.Name;
                sb.AppendLine("追踪：" + title);
                sb.AppendLine("状态：" + QuestStatusName(rt.Status));
                if (!string.IsNullOrEmpty(spec.Description))
                {
                    var desc = spec.Description;
                    if (desc.Length > 140)
                        desc = desc.Substring(0, 140) + "…";
                    sb.AppendLine(desc);
                }

                sb.AppendLine("---");
                if (rt.ProgressMax > 0)
                    sb.AppendLine("进度：" + rt.ProgressCount + "/" + rt.ProgressMax);
                var deadline = QuestDeadline.FormatRemaining(session.World, rt);
                if (!string.IsNullOrEmpty(deadline))
                    sb.AppendLine("时限：" + deadline);
                var failHint = QuestJournalQuery.SummarizeOutcomes(spec.FailResults, "（无失败后果）");
                if (!string.IsNullOrEmpty(failHint) &&
                    !string.Equals(failHint, "（无失败后果）", System.StringComparison.Ordinal))
                    sb.AppendLine("失败后果：" + failHint);
                sb.AppendLine("目标：" + SummarizeTrackedObjectives(session.World, spec, rt));
                if (rt.Status == QuestStatus.ReadyToClaim)
                    sb.AppendLine("● 可领奖 — 按 J 打开任务日志领取");
            }
            else
            {
                sb.AppendLine("未追踪任务");
                sb.Append("按 J 打开日志（进行中／已失败）");
            }

            return sb.ToString();
        }

        static string SummarizeTrackedObjectives(
            SimulationWorld world,
            QuestSpec spec,
            QuestRuntime rt)
        {
            if (spec?.CompleteConditions == null || spec.CompleteConditions.Count == 0)
                return "（无）";
            for (var i = 0; i < spec.CompleteConditions.Count; i++)
            {
                var c = spec.CompleteConditions[i];
                if (c == null)
                    continue;
                if (!string.Equals(c.Kind, "uniqueLaborAtLocation", System.StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(c.Kind, "uniqueHarvestAtLocation", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                var max = c.Amount > 0 ? c.Amount : 1;
                var cur = rt != null ? rt.ProgressCount : 0;
                return string.Equals(c.Kind, "uniqueHarvestAtLocation", System.StringComparison.OrdinalIgnoreCase)
                    ? "农田采集 " + cur + "/" + max + "（每人×1）"
                    : "农田劳作 " + cur + "/" + max + "（每人约3秒）";
            }

            var stockParts = new System.Collections.Generic.List<string>();
            for (var i = 0; i < spec.CompleteConditions.Count; i++)
            {
                var c = spec.CompleteConditions[i];
                if (c == null ||
                    !string.Equals(c.Kind, "stockAtLeast", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                var need = c.Amount > 0 ? c.Amount : 1;
                var have = world != null ? world.Inventory.GetCount(c.Id) : 0;
                if (have > need)
                    have = need;
                stockParts.Add(QuestJournalQuery.ResourceLabel(c.Id) + " " + have + "/" + need);
            }

            if (stockParts.Count > 0)
                return string.Join("；", stockParts);

            return SummarizeTrackedObjectivesLegacy(spec);
        }

        static string SummarizeTrackedObjectivesLegacy(QuestSpec spec)
        {
            if (spec?.CompleteConditions == null || spec.CompleteConditions.Count == 0)
                return "（无）";
            // 复用查询层摘要逻辑的轻量版
            var parts = new System.Collections.Generic.List<string>(spec.CompleteConditions.Count);
            for (var i = 0; i < spec.CompleteConditions.Count; i++)
            {
                var c = spec.CompleteConditions[i];
                if (c == null || string.IsNullOrEmpty(c.Kind))
                    continue;
                switch (c.Kind.Trim().ToLowerInvariant())
                {
                    case "exploredlocation":
                        parts.Add("探索 " + ShortId(c.Id));
                        break;
                    case "atlocation":
                        parts.Add("抵达 " + ShortId(c.Id));
                        break;
                    case "questcompleted":
                        parts.Add("完成 " + ShortId(c.Id));
                        break;
                    case "hasflag":
                    case "storyflag":
                        parts.Add("标记 " + ShortId(c.Id));
                        break;
                    case "laboratlocation":
                        parts.Add(ShortId(c.CharacterId) + "农田≥" + c.Amount + "秒");
                        break;
                    case "uniquelaboratlocation":
                        parts.Add("劳作人数≥" + c.Amount);
                        break;
                    case "uniqueharvestatlocation":
                        parts.Add("采集人数≥" + c.Amount);
                        break;
                    case "characteratlocation":
                        parts.Add(ShortId(c.CharacterId) + "→集合");
                        break;
                    case "stockatleast":
                        parts.Add(ShortId(c.Id) + "≥" + c.Amount);
                        break;
                    default:
                        parts.Add(c.Kind + (string.IsNullOrEmpty(c.Id) ? "" : " " + ShortId(c.Id)));
                        break;
                }
            }

            return parts.Count == 0 ? "（无）" : string.Join("；", parts);
        }

        string BuildRelationText(PlayableHostSession session, EntityId focus)
        {
            var sb = new StringBuilder(256);
            if (!session.World.Entities.TryGet(focus, out var self) ||
                !self.TryGet<RelationshipComponent>(out var rel))
            {
                sb.Append("无关系数据");
                return sb.ToString();
            }

            var n = 0;
            foreach (var e in session.World.Entities.All)
            {
                if (e.Id == focus)
                    continue;
                if (!rel.TryGetCachedToward(e.Id, out var score))
                    continue;
                var nm = string.IsNullOrEmpty(e.DisplayName) ? e.Id.ToString() : e.DisplayName;
                sb.Append("· ").Append(nm).Append("  ").Append(score).Append('\n');
                if (++n >= 10)
                    break;
            }

            if (n == 0)
                sb.Append("暂无显著关系");
            return sb.ToString();
        }

        string BuildEventText(PlayableHostSession session)
        {
            var sb = new StringBuilder(256);
            if (session.World.ContentEvents.HasActive)
            {
                sb.AppendLine("进行中：" + ShortId(session.World.ContentEvents.ActiveEventId));
                sb.AppendLine("（弹窗已关，将自动选第一条可用选项）");
            }

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

        void IssueFocus(EntityId focus, PlayerCommandKind kind)
        {
            if (commandBridge == null || focus.IsNone)
                return;
            EnsureFocusSelected(focus);
            var session = bootstrap != null ? bootstrap.Session : null;
            if (session != null && !session.World.ContentEvents.HasActive &&
                kind != PlayerCommandKind.Stop)
                session.IsPaused = false;
            var dur = kind == PlayerCommandKind.Stop || kind == PlayerCommandKind.UseConcealGrass
                ? 0UL
                : HostCommandBridge.DefaultDurationTicks;
            commandBridge.IssueOne(focus, kind, dur);
        }

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _px != null ? _px : Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void DrawFrame(Rect r, Color c)
        {
            var t = 1f;
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        static string DescribeAction(PlayableHostSession session, Entity entity)
        {
            if (!entity.TryGet<ActionStateComponent>(out var st) || !st.HasActiveAction)
                return "空闲";
            if (!session.World.ActiveActions.TryGetValue(st.ActiveActionId, out var action))
                return "行动中";
            if (action is MoveAction) return "移动中";
            if (action is WorkAction work)
            {
                switch (work.Activity)
                {
                    case ScheduleActivity.Rest:
                    case ScheduleActivity.Eat:
                        return "休息中";
                    case ScheduleActivity.Patrol:
                    case ScheduleActivity.Inspect:
                        return "巡查中";
                    case ScheduleActivity.Cultivate:
                        return "修炼中";
                    default:
                        return "工作中";
                }
            }
            if (action is LaborAction) return "工作中";
            if (action is CultivateAction) return "修炼中";
            if (action is RestAction) return "休息中";
            if (action is ObserveAction) return "观察中";
            if (action is WaitAction) return "待命";
            return "行动中";
        }

        static string RealmName(RealmStage realm)
        {
            switch (realm)
            {
                case RealmStage.Mortal: return "感应境";
                case RealmStage.QiRefining: return "炼气期";
                default: return realm.ToString();
            }
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
                case QuestStatus.ReadyToClaim: return "待领奖";
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
            return line.Length > 64 ? line.Substring(0, 61) + "…" : line;
        }
    }
}
