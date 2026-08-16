using System.Collections.Generic;
using System.Text;
using UnityEngine;
using XianXia.Core.Actions;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Concealment;
using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Input;
using XianXia.Core.Labor;
using XianXia.Core.Npc;
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
        const float UnitTabStripW = 36f;
        const float CombatArtRailW = 58f;
        const float CombatArtRailGap = 4f;

        enum UnitTab
        {
            Overview = 0,
            Attributes = 1,
            SpiritRoots = 2,
            Cultivation = 3,
            Personality = 4,
            Tendency = 5,
            Relation = 6
        }

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] HostHousingAreaSelection housingAreaSelection;
        [SerializeField] HostControlCoreAssault controlCoreAssault;
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
        EntityId _unitPanelFocus = EntityId.None;

        static readonly Color Parchment = new Color(0.90f, 0.84f, 0.72f, 0.96f);
        static readonly Color ParchmentDark = new Color(0.72f, 0.62f, 0.48f, 1f);
        static readonly Color Ink = new Color(0.18f, 0.14f, 0.10f, 1f);
        static readonly Color BarOrange = new Color(0.92f, 0.62f, 0.22f, 1f);
        static readonly Color BarBlue = new Color(0.30f, 0.55f, 0.85f, 1f);
        static readonly Color BarTeal = new Color(0.28f, 0.68f, 0.58f, 1f);
        static readonly Color BarViolet = new Color(0.62f, 0.42f, 0.72f, 1f);
        static readonly Color AccentGold = new Color(0.95f, 0.78f, 0.28f, 1f);

        static readonly AttributeId[] AttributeDisplayOrder =
        {
            AttributeId.Physique,
            AttributeId.MaxHp,
            AttributeId.Attack,
            AttributeId.Defense,
            AttributeId.Speed,
            AttributeId.Stamina,
            AttributeId.SpiritSense,
            AttributeId.Comprehension,
            AttributeId.SpiritPower,
            AttributeId.Cultivation,
            AttributeId.MindState
        };

        static readonly SpiritRootKind[] SpiritRootDisplayOrder =
        {
            SpiritRootKind.Fire,
            SpiritRootKind.Metal,
            SpiritRootKind.Earth,
            SpiritRootKind.Wood,
            SpiritRootKind.Thunder,
            SpiritRootKind.Wind,
            SpiritRootKind.Ice,
            SpiritRootKind.Poison
        };

        readonly List<EntityId> _housingResidentsScratch = new List<EntityId>(16);
        readonly List<EntityId> _housingCandidatesScratch = new List<EntityId>(32);
        readonly List<(ScheduleActivity Activity, int Priority)> _tendencyScratch =
            new List<(ScheduleActivity Activity, int Priority)>(16);
        Vector2 _housingAssignScroll;
        Vector2 _scheduleEditScroll;
        Vector2 _unitPanelScroll;

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
                housingAreaSelection = host.GetComponent<HostHousingAreaSelection>() ??
                                       host.gameObject.GetComponent<HostHousingAreaSelection>();
                controlCoreAssault = host.GetComponent<HostControlCoreAssault>() ??
                                     host.gameObject.GetComponent<HostControlCoreAssault>();
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
            if (bootstrap.WorldMapPanel != null && bootstrap.WorldMapPanel.IsOpen)
                return;
            if (bootstrap.CultivationPanel != null && bootstrap.CultivationPanel.IsOpen)
                return;
            if (bootstrap.CharacterSheetPanel != null && bootstrap.CharacterSheetPanel.IsOpen)
                return;
            if (bootstrap.RelationPanel != null && bootstrap.RelationPanel.IsOpen)
                return;
            if (bootstrap.CultivateConfirm != null && bootstrap.CultivateConfirm.IsOpen)
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
            if (bootstrap != null &&
                ((bootstrap.CultivationPanel != null && bootstrap.CultivationPanel.IsOpen) ||
                 (bootstrap.CharacterSheetPanel != null && bootstrap.CharacterSheetPanel.IsOpen) ||
                 (bootstrap.RelationPanel != null && bootstrap.RelationPanel.IsOpen) ||
                 (bootstrap.CultivateConfirm != null && bootstrap.CultivateConfirm.IsOpen) ||
                 (bootstrap.WorldTravelConfirm != null && bootstrap.WorldTravelConfirm.IsOpen) ||
                 (bootstrap.WorldMapPanel != null && bootstrap.WorldMapPanel.IsOpen)))
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
            DrawWorldObjectInspectPanel(session);
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
                "操作：左键选人 · 右键 NPC→攻击／对话 · 交战中 S 停战／右键地面脱离 · 1–6 斗技 · Space暂停 · F10显隐HUD";
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

            _title = HostImguiStyles.InkLabel(14, bold: true, ink: Color.white);
            _body = HostImguiStyles.InkLabel(12, wordWrap: true, ink: new Color(0.92f, 0.92f, 0.92f));
            _parchmentTitle = HostImguiStyles.InkLabel(15, bold: true, ink: Ink);
            _parchmentBody = HostImguiStyles.InkLabel(12, wordWrap: true, ink: Ink);
            _small = HostImguiStyles.InkLabel(11, ink: Ink);
            _small.alignment = TextAnchor.MiddleCenter;
            _stylesReady = true;
        }

        void SetHostSpeed(int multiplier)
        {
            if (bootstrap != null)
                bootstrap.SetSpeedMultiplier(multiplier);
            else
                debugHud?.SetSpeedMultiplier(multiplier);
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
            if (GUI.Button(new Rect(x, 8f, 40f, 32f), "1x"))
                SetHostSpeed(1);
            x += 44f;
            if (GUI.Button(new Rect(x, 8f, 40f, 32f), "2x"))
                SetHostSpeed(2);
            x += 44f;
            if (GUI.Button(new Rect(x, 8f, 40f, 32f), "5x"))
                SetHostSpeed(5);
            x += 44f;
            if (GUI.Button(new Rect(x, 8f, 44f, 32f), "20x"))
                SetHostSpeed(20);
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
            if (GUI.Button(new Rect(Screen.width - RailW - 158f, 6f, 70f, 28f), "地图"))
            {
                var map = bootstrap != null ? bootstrap.WorldMapPanel : null;
                map?.Toggle();
            }
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

        void DrawWorldObjectInspectPanel(PlayableHostSession session)
        {
            if (housingAreaSelection == null && bootstrap != null)
                housingAreaSelection = bootstrap.GetComponent<HostHousingAreaSelection>();
            if (housingAreaSelection == null)
                return;

            var inspect = housingAreaSelection.Inspect;
            if (inspect == null || !inspect.HasTarget)
                return;

            // 己方已选中且不是主管府／可破坏物突击中：物况栏让位给人物面板
            var partyFocus = selectionController != null &&
                             selectionController.State.Count > 0 &&
                             selectionController.IsPartyUnit(selectionController.State.SelectedIds[0]);
            if (partyFocus &&
                inspect.Kind != WorldObjectInspectKind.ControlCore &&
                inspect.Kind != WorldObjectInspectKind.Destructible)
                return;

            switch (inspect.Kind)
            {
                case WorldObjectInspectKind.ControlCore:
                    DrawInspectControlCore(session, inspect.WorkAreaId);
                    break;
                case WorldObjectInspectKind.Housing:
                    DrawInspectHousing(session, inspect.WorkAreaId);
                    break;
                case WorldObjectInspectKind.WorkArea:
                    DrawInspectWorkArea(session, inspect.WorkAreaId);
                    break;
                case WorldObjectInspectKind.Plot:
                    DrawInspectPlot(inspect.Plot);
                    break;
                case WorldObjectInspectKind.Destructible:
                    DrawInspectDestructible(inspect.Destructible);
                    break;
            }
        }

        void DrawInspectShell(float height, string title, System.Action drawBody)
        {
            var r = new Rect(Pad, TopH + 42f, 320f, height);
            Fill(r, new Color(0.12f, 0.12f, 0.14f, 0.94f));
            DrawFrame(r, ParchmentDark);
            HostUiHitTest.Block(r);

            GUI.Label(new Rect(r.x + 10f, r.y + 8f, r.width - 50f, 22f), title, _title);
            if (GUI.Button(new Rect(r.xMax - 36f, r.y + 6f, 28f, 24f), "×"))
            {
                housingAreaSelection.Clear();
                return;
            }

            drawBody?.Invoke();
        }

        void DrawInspectControlCore(PlayableHostSession session, string coreId)
        {
            if (string.IsNullOrEmpty(coreId) ||
                !session.World.ControlCores.TryGet(coreId, out var core))
                return;

            DrawInspectShell(168f, "主管府 · " + core.Name, () =>
            {
                var r = new Rect(Pad, TopH + 42f, 320f, 168f);
                DrawInlineMeter(
                    r.x + 10f, r.y + 40f, r.width - 20f,
                    "耐久", core.CurrentDurability, core.MaxDurability,
                    new Color(0.85f, 0.32f, 0.28f));

                string status;
                if (core.PlayerControlled)
                    status = "状态：已占领（住房／课表可管）";
                else if (core.CaptureAvailable)
                    status = "状态：已破门 · 站立占领 " +
                             core.OccupyProgressSeconds.ToString("0.0") + "/" +
                             core.OccupyHoldSeconds.ToString("0") + " 秒";
                else
                    status = "状态：防守中（选中己方→右键攻击拆耐久）";

                GUI.Label(new Rect(r.x + 10f, r.y + 68f, r.width - 20f, 48f), status, _body);
                if (core.PlayerControlled && core.GrantsPrivileges.Count > 0)
                {
                    GUI.Label(
                        new Rect(r.x + 10f, r.y + 118f, r.width - 20f, 36f),
                        "权限：" + string.Join("、", core.GrantsPrivileges),
                        _body);
                }
            });
        }

        void DrawInspectHousing(PlayableHostSession session, string areaId)
        {
            if (string.IsNullOrEmpty(areaId) ||
                !session.World.TryGetWorkArea(areaId, out var area))
                return;

            var title = string.IsNullOrEmpty(area.Name) ? areaId : area.Name;
            DrawInspectShell(130f, "住房 · " + title, () =>
            {
                var r = new Rect(Pad, TopH + 42f, 320f, 130f);
                var ownerName = "（未指定）";
                if (session.World.HousingAssignments.TryGetOwner(areaId, out var ownerId) &&
                    session.World.Entities.TryGet(ownerId, out var ownerEnt))
                    ownerName = HousingAssignmentService.EntityDisplayName(ownerEnt);

                HousingAssignmentService.CollectResidents(session.World, areaId, _housingResidentsScratch);
                var residents = _housingResidentsScratch.Count == 0
                    ? "—"
                    : FormatEntityNames(session, _housingResidentsScratch);

                GUI.Label(new Rect(r.x + 10f, r.y + 36f, r.width - 20f, 20f), "归属：" + ownerName, _body);
                GUI.Label(new Rect(r.x + 10f, r.y + 58f, r.width - 20f, 36f), "入住：" + residents, _body);
                GUI.Label(
                    new Rect(r.x + 10f, r.y + 96f, r.width - 20f, 24f),
                    "只读况栏 · 改归属另开管理入口",
                    _body);
            });
        }

        void DrawInspectWorkArea(PlayableHostSession session, string areaId)
        {
            if (string.IsNullOrEmpty(areaId) ||
                !session.World.TryGetWorkArea(areaId, out var area))
                return;

            var title = string.IsNullOrEmpty(area.Name) ? areaId : area.Name;
            DrawInspectShell(120f, "工区 · " + title, () =>
            {
                var r = new Rect(Pad, TopH + 42f, 320f, 120f);
                var tags = area.Tags != null && area.Tags.Count > 0
                    ? string.Join("、", area.Tags)
                    : "—";
                var acts = area.AllowedActivities != null && area.AllowedActivities.Count > 0
                    ? string.Join("、", area.AllowedActivities)
                    : "—";
                GUI.Label(new Rect(r.x + 10f, r.y + 36f, r.width - 20f, 20f),
                    "容量 " + area.Capacity + " · 地点 " + ShortId(area.LocationId), _body);
                GUI.Label(new Rect(r.x + 10f, r.y + 58f, r.width - 20f, 20f), "标签：" + tags, _body);
                GUI.Label(new Rect(r.x + 10f, r.y + 80f, r.width - 20f, 28f), "活动：" + acts, _body);
            });
        }

        void DrawInspectPlot(HostMapPlotCell plot)
        {
            if (plot == null)
                return;

            DrawInspectShell(128f, plot.KindDisplayName(), () =>
            {
                var r = new Rect(Pad, TopH + 42f, 320f, 128f);
                GUI.Label(
                    new Rect(r.x + 10f, r.y + 36f, r.width - 20f, 20f),
                    "格 (" + plot.GridX + "," + plot.GridY + ")",
                    _body);
                if (plot.IsPlantableField)
                {
                    GUI.Label(
                        new Rect(r.x + 10f, r.y + 58f, r.width - 20f, 40f),
                        plot.DescribeCropStatus(),
                        _body);
                    GUI.Label(
                        new Rect(r.x + 10f, r.y + 100f, r.width - 20f, 20f),
                        "交互→田区自动播种／照料／收获",
                        _body);
                }
                else
                {
                    GUI.Label(
                        new Rect(r.x + 10f, r.y + 58f, r.width - 20f, 40f),
                        "交互：" + plot.InteractKind +
                        (string.IsNullOrEmpty(plot.Label) ? "" : " · " + plot.Label),
                        _body);
                }
            });
        }

        void DrawInspectDestructible(HostMapDestructible d)
        {
            if (d == null || d.IsDestroyed)
                return;

            var kindLabel = d.IsTree ? "树木" : d.IsWall ? "墙体" : "可破坏物";
            DrawInspectShell(132f, kindLabel + " · " + d.DisplayName, () =>
            {
                var r = new Rect(Pad, TopH + 42f, 320f, 132f);
                DrawInlineMeter(
                    r.x + 10f, r.y + 40f, r.width - 20f,
                    "耐久", d.CurrentHp, d.MaxHp,
                    new Color(0.45f, 0.75f, 0.35f));
                var yield = d.IsTree
                    ? ("伐倒后获粗木 ×" + d.ResolveWoodYield())
                    : "耐久归零后摧毁";
                GUI.Label(new Rect(r.x + 10f, r.y + 68f, r.width - 20f, 28f), yield, _body);
                GUI.Label(
                    new Rect(r.x + 10f, r.y + 98f, r.width - 20f, 24f),
                    "选中己方 → 右键／F8 战斗点选可砍拆",
                    _body);
            });
        }

        static void ResumeSession(PlayableHostSession session)
        {
            if (session == null)
                return;
            if (!session.World.ContentEvents.HasActive)
                session.IsPaused = false;
        }

        static string FormatEntityNames(PlayableHostSession session, List<EntityId> ids)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < ids.Count; i++)
            {
                if (i > 0)
                    sb.Append("、");
                if (session.World.Entities.TryGet(ids[i], out var e))
                    sb.Append(HousingAssignmentService.EntityDisplayName(e));
                else
                    sb.Append("?");
            }

            return sb.ToString();
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
            var valueStyle = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
            HostImguiStyles.LockTextColor(valueStyle, Color.white);
            GUI.Label(bar, cur + "/" + max, valueStyle);
        }

        void DrawRightRail(PlayableHostSession session)
        {
            var x = Screen.width - RailW - Pad;
            var y = TopH + Pad;
            var h = (Screen.height - TopH - BottomH - Pad * 3f) / 3f;
            if (h < 80f)
                h = 80f;

            var canManageSchedules = HousingAssignmentService.CanManageSchedules(session.World);
            var scheduleTitle = canManageSchedules
                ? "课表（可改·点活动切换）"
                : "课表（只读·占领后可改）";
            if (canManageSchedules)
                DrawEditableSchedulePanel(new Rect(x, y, RailW, h), scheduleTitle, session);
            else
                DrawPanel(new Rect(x, y, RailW, h), scheduleTitle, BuildScheduleText(session));
            HostUiHitTest.Block(new Rect(x, y, RailW, h));
            y += h + Pad;
            DrawPanel(new Rect(x, y, RailW, h), "任务", BuildQuestText(session));
            HostUiHitTest.Block(new Rect(x, y, RailW, h));
            y += h + Pad;
            DrawPanel(new Rect(x, y, RailW, h), "事件", BuildEventText(session));
            HostUiHitTest.Block(new Rect(x, y, RailW, h));
        }

        void DrawEditableSchedulePanel(Rect rect, string title, PlayableHostSession session)
        {
            Fill(rect, new Color(0.11f, 0.13f, 0.16f, 0.92f));
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 20f), title, _title);

            var focus = ResolveFocus(session);
            if (focus.IsNone && session.CharacterIds.Count > 0)
                focus = session.CharacterIds[0];
            if (focus.IsNone ||
                !session.World.Entities.TryGet(focus, out var e) ||
                !e.TryGet<ScheduleComponent>(out var sched) ||
                !session.World.TryGetSchedule(sched.DefinitionId, out var def))
            {
                GUI.Label(new Rect(rect.x + 8f, rect.y + 30f, rect.width - 16f, 40f), "无日程", _body);
                return;
            }

            var tickInDay = (int)(session.World.Tick.Value % (ulong)WorldTick.TicksPerDay);
            GUI.Label(
                new Rect(rect.x + 8f, rect.y + 28f, rect.width - 16f, 18f),
                ShortId(def.Id),
                _body);

            var listRect = new Rect(rect.x + 6f, rect.y + 48f, rect.width - 12f, rect.height - 56f);
            var contentH = Mathf.Max(listRect.height, def.Blocks.Count * 28f + 4f);
            _scheduleEditScroll = GUI.BeginScrollView(
                listRect,
                _scheduleEditScroll,
                new Rect(0f, 0f, listRect.width - 16f, contentH));
            var y = 2f;
            for (var i = 0; i < def.Blocks.Count; i++)
            {
                var b = def.Blocks[i];
                var mark = tickInDay >= b.StartTickInDay && tickInDay < b.EndTickInDay ? "►" : " ";
                var label = mark + " " + TickToClock(b.StartTickInDay) + "-" + TickToClock(b.EndTickInDay) +
                            "  " + ActivityName(b.Activity);
                if (GUI.Button(new Rect(0f, y, listRect.width - 18f, 24f), label))
                {
                    var next = NextEditableActivity(b.Activity);
                    if (def.TryReplaceBlockActivity(i, next))
                        Debug.Log("[Host] 课表已改: " + ActivityName(next));
                }

                y += 28f;
            }

            GUI.EndScrollView();
        }

        static ScheduleActivity NextEditableActivity(ScheduleActivity current)
        {
            switch (current)
            {
                case ScheduleActivity.Labor: return ScheduleActivity.Rest;
                case ScheduleActivity.Rest: return ScheduleActivity.Eat;
                case ScheduleActivity.Eat: return ScheduleActivity.Cultivate;
                case ScheduleActivity.Cultivate: return ScheduleActivity.Explore;
                case ScheduleActivity.Explore: return ScheduleActivity.Patrol;
                case ScheduleActivity.Patrol: return ScheduleActivity.Inspect;
                case ScheduleActivity.Inspect: return ScheduleActivity.Idle;
                default: return ScheduleActivity.Labor;
            }
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

            if (_unitPanelFocus != focus)
            {
                _unitPanelFocus = focus;
                _unitPanelScroll = Vector2.zero;
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

            // 修仙模拟器式：主面板右侧＝斗技 1–6（化伤术位），再右＝详情入口
            var artRail = new Rect(
                main.xMax + CombatArtRailGap,
                main.y + 8f,
                CombatArtRailW,
                main.height - 16f);
            DrawCombatArtSideRail(artRail, focus, entity, isParty);
            HostUiHitTest.Block(artRail);

            var tabStrip = new Rect(
                artRail.xMax + CombatArtRailGap,
                main.y + 12f,
                UnitTabStripW,
                main.height - 20f);
            DrawDetailSideTabs(tabStrip, focus);
            HostUiHitTest.Block(tabStrip);

            var name = string.IsNullOrEmpty(entity.DisplayName) ? focus.ToString() : entity.DisplayName;
            var activity = DescribeAction(session, entity);
            if (bootstrap != null &&
                bootstrap.BreakthroughRitual != null &&
                bootstrap.BreakthroughRitual.IsChannelingSubject(focus))
                activity = "冲击瓶颈";
            else if (bootstrap != null &&
                     bootstrap.SkillStudyRitual != null &&
                     bootstrap.SkillStudyRitual.IsChannelingSubject(focus))
                activity = "参悟中";
            entity.TryGet<CultivationComponent>(out var cult);
            var realm = cult != null ? RealmName(cult.Realm, cult.MinorStage) : "—";
            var subtitle = isParty ? "己方 · 上方可下令" : "查看 · 非己方不可下令";
            GUI.Label(
                new Rect(main.x + 14f, main.y + 8f, main.width - 140f, 24f),
                name + "（" + activity + "）· " + subtitle,
                _parchmentTitle);
            GUI.Label(
                new Rect(main.xMax - 128f, main.y + 8f, 120f, 24f),
                realm,
                _parchmentTitle);

            var headerExtra = 0f;
            if (isParty && selectionController.State.Count > 1)
            {
                GUI.Label(
                    new Rect(main.x + 14f, main.y + 30f, main.width - 24f, 18f),
                    "框选 " + selectionController.State.Count + " 人时：指令只令「" + name + "」；群体移动请右键",
                    _small);
                headerExtra = 16f;
            }

            var content = new Rect(
                main.x + 12f,
                main.y + 36f + headerExtra,
                main.width - 24f,
                main.height - 48f - headerExtra);
            DrawOverviewBars(session, entity, cult, content);
            GUI.Label(
                new Rect(main.x + 14f, main.yMax - 22f, main.width - 28f, 18f),
                "右侧 1–6 斗技可点放 · 详情（人物／境界／斗技／关系）· 打坐：F6",
                _small);
        }

        void DrawCombatArtSideRail(Rect strip, EntityId focus, Entity entity, bool isParty)
        {
            Fill(strip, Parchment);
            DrawFrame(strip, ParchmentDark);

            if (!entity.TryGet<CombatArtsComponent>(out var arts))
            {
                arts = new CombatArtsComponent();
                entity.AddComponent(arts);
            }

            var world = bootstrap?.Session?.World;
            var skillBar = bootstrap != null ? bootstrap.GetComponent<HostCombatSkillBar>() : null;
            var slots = CombatArtsComponent.MaxEquippedSlots;
            var pad = 4f;
            var gap = 3f;
            var innerH = strip.height - pad * 2f - 16f;
            var slotH = (innerH - gap * (slots - 1)) / slots;
            if (slotH < 22f)
                slotH = 22f;

            GUI.Label(
                new Rect(strip.x + 2f, strip.y + 2f, strip.width - 4f, 14f),
                "斗技·" + arts.Learned.Count,
                _small);

            for (var i = 0; i < slots; i++)
            {
                var r = new Rect(
                    strip.x + pad,
                    strip.y + 16f + pad + i * (slotH + gap),
                    strip.width - pad * 2f,
                    slotH);
                var artId = arts.GetEquipped(i);
                string name;
                var active = false;
                if (!artId.HasValue)
                {
                    name = "—";
                }
                else if (world != null &&
                         world.TryGetCombatArt(artId.Value, out var art) &&
                         art != null)
                {
                    name = string.IsNullOrEmpty(art.Name) ? art.Id.ToString() : art.Name;
                    if (name.Length > 4)
                        name = name.Substring(0, 4);
                    active = art.IsActiveSkill;
                }
                else
                    name = "?";

                var cd = skillBar != null ? skillBar.GetSlotCooldown(focus, i) : 0f;
                string label;
                if (cd > 0.05f)
                    label = cd.ToString("0.0") + "s";
                else
                    label = name;

                var prev = GUI.color;
                if (!artId.HasValue)
                    GUI.color = new Color(0.85f, 0.8f, 0.72f, 1f);
                else if (cd > 0.05f)
                    GUI.color = new Color(0.75f, 0.72f, 0.65f, 1f);
                else if (active)
                    GUI.color = new Color(0.98f, 0.92f, 0.78f, 1f);
                else
                    GUI.color = new Color(0.88f, 0.84f, 0.76f, 1f);

                var canClick = isParty && artId.HasValue && active && cd <= 0.05f && skillBar != null;
                GUI.enabled = canClick || (!isParty && artId.HasValue);
                if (HostImguiStyles.ParchmentBtn(r, label))
                {
                    Event.current.Use();
                    if (isParty && skillBar != null)
                    {
                        EnsureFocusSelected(focus);
                        skillBar.TryCastEquippedSlot(i);
                    }
                }

                GUI.enabled = true;
                GUI.color = prev;

                // 键位角标 1–6
                Fill(new Rect(r.x + 1f, r.y + 1f, 13f, 13f), AccentGold);
                var keyStyle = _small ?? GUI.skin.label;
                GUI.Label(new Rect(r.x + 1f, r.y, 13f, 13f), (i + 1).ToString(), keyStyle);
            }
        }

        void DrawDetailSideTabs(Rect strip, EntityId focus)
        {
            var names = new[] { "人物", "境界", "斗技", "关系" };
            var h = Mathf.Min(36f, (strip.height - 6f * (names.Length - 1)) / names.Length);
            for (var i = 0; i < names.Length; i++)
            {
                var r = new Rect(strip.x, strip.y + i * (h + 6f), strip.width, h);
                if (!HostImguiStyles.SideTabBtn(r, names[i]))
                    continue;
                Event.current.Use();
                switch (i)
                {
                    case 0:
                        bootstrap?.CharacterSheetPanel?.OpenFor(focus);
                        break;
                    case 1:
                        bootstrap?.CultivationPanel?.OpenFor(focus);
                        break;
                    case 2:
                        bootstrap?.CombatArtsPanel?.OpenFor(focus);
                        break;
                    case 3:
                        bootstrap?.RelationPanel?.OpenFor(focus);
                        break;
                }
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
            // 交互／战斗横排；纱衣紧挨战斗，F2（事件流水改为 Ctrl+F2）
            var showVeil = CanShowSpiritVeilOrb(focus);
            var labels = showVeil
                ? new[]
                {
                    "Q\n移动", "F1\n停止", "E\n交互", "F8\n战斗", "F2\n纱衣", "F7\n勘查", "F6\n修炼"
                }
                : new[]
                {
                    "Q\n移动", "F1\n停止", "E\n交互", "F8\n战斗", "F7\n勘查", "F6\n修炼"
                };

            var mode = bootstrap != null ? bootstrap.WorkTargetMode : null;
            var gap = showVeil ? 8f : 10f;
            var total = ActionOrb * labels.Length + gap * (labels.Length - 1);
            var startX = x + (width - total) * 0.5f;
            for (var i = 0; i < labels.Length; i++)
            {
                var r = new Rect(startX + i * (ActionOrb + gap), y, ActionOrb, ActionOrb);
                var isVeil = showVeil && i == 4;
                GUI.enabled = commandBridge != null || mode != null || isVeil;
                if (HostImguiStyles.ParchmentBtn(r, labels[i]))
                {
                    Event.current.Use();
                    if (isVeil)
                        ToggleSpiritVeilOrb(focus);
                    else
                        InvokeActionIndex(focus, MapOrbIndexToAction(i, showVeil));
                }

                GUI.enabled = true;
            }
        }

        static int MapOrbIndexToAction(int orbIndex, bool showVeil)
        {
            if (!showVeil)
                return orbIndex;
            // 0–3 同原；4＝纱衣；5→F7(4)；6→F6(5)
            if (orbIndex < 4)
                return orbIndex;
            return orbIndex - 1;
        }

        bool CanShowSpiritVeilOrb(EntityId focus)
        {
            if (bootstrap?.Session?.World == null || focus.IsNone)
                return false;
            if (!bootstrap.Session.World.Entities.TryGet(focus, out var entity))
                return false;
            return entity.TryGet<CultivationComponent>(out var cult) &&
                   cult.Realm >= RealmStage.Foundation;
        }

        void ToggleSpiritVeilOrb(EntityId focus)
        {
            if (bootstrap == null || focus.IsNone)
                return;
            EnsureFocusSelected(focus);
            var veil = bootstrap.GetComponent<HostSpiritVeilController>();
            if (veil == null)
                return;
            var ok = veil.TryToggle(focus, out var msg);
            veil.ToastToggleResult(focus, ok, msg);
        }

        void DrawUnitTabs(Rect strip)
        {
            var names = new[] { "况", "属", "灵", "修", "性", "事", "系" };
            var h = Mathf.Min(32f, (strip.height - 6f * (names.Length - 1)) / names.Length);
            for (var i = 0; i < names.Length; i++)
            {
                var r = new Rect(strip.x, strip.y + i * (h + 6f), strip.width, h);
                var on = (int)_unitTab == i;
                Fill(r, on ? AccentGold : ParchmentDark);
                if (GUI.Button(r, names[i], _small))
                {
                    _unitTab = (UnitTab)i;
                    _unitPanelScroll = Vector2.zero;
                }
            }
        }

        void DrawUnitTabContent(
            PlayableHostSession session,
            EntityId focus,
            Entity entity,
            CultivationComponent cult,
            Rect area)
        {
            switch (_unitTab)
            {
                case UnitTab.Overview:
                    DrawOverviewBars(session, entity, cult, area);
                    break;
                case UnitTab.Attributes:
                    DrawAttributesTab(entity, area);
                    break;
                case UnitTab.SpiritRoots:
                    DrawSpiritRootsTab(entity, area);
                    break;
                case UnitTab.Cultivation:
                    DrawCultivationTab(entity, cult, area);
                    break;
                case UnitTab.Personality:
                    DrawPersonalityTab(entity, area);
                    break;
                case UnitTab.Tendency:
                    DrawTendencyTab(entity, area);
                    break;
                case UnitTab.Relation:
                    DrawScrollText(area, BuildRelationText(session, focus));
                    break;
            }
        }

        void DrawOverviewBars(PlayableHostSession session, Entity entity, CultivationComponent cult, Rect area)
        {
            var leftW = area.width * 0.48f;
            var rightX = area.x + area.width * 0.52f;
            var rightW = area.width * 0.48f;
            var y = area.y;

            if (entity.TryGet<AttributesComponent>(out var attrs))
            {
                CombatDamageRules.EnsureVitals(entity);
                var maxHp = Mathf.Max(1, attrs.GetFinal(AttributeId.MaxHp));
                var curHp = maxHp;
                if (entity.TryGet<CombatVitalsComponent>(out var vitals))
                    curHp = Mathf.Clamp(vitals.CurrentHp, 0, maxHp);
                DrawStatBar(area.x, y, leftW, "生命", curHp, maxHp, BarOrange);
                y += 22f;
                var phy = attrs.GetFinal(AttributeId.Physique);
                DrawStatBar(area.x, y, leftW, "体魄", phy, Mathf.Max(50, phy), BarOrange);
                y += 22f;
                var sta = attrs.GetFinal(AttributeId.Stamina);
                DrawStatBar(area.x, y, leftW, "耐力", sta, Mathf.Max(100, sta), BarOrange);
                y += 22f;
                var sense = attrs.GetFinal(AttributeId.SpiritSense);
                DrawStatBar(area.x, y, leftW, "神识", sense, Mathf.Max(100, sense), BarViolet);
                y += 22f;
                var mind = attrs.GetFinal(AttributeId.MindState);
                DrawStatBar(area.x, y, leftW, "心境", mind, Mathf.Max(100, mind), BarBlue);
            }
            else
            {
                GUI.Label(new Rect(area.x, y, leftW, 22f), "无属性数据", _parchmentBody);
                y += 22f;
            }

            var ry = area.y;
            if (cult != null)
            {
                var req = Mathf.Max(1, cult.BreakthroughProgressRequired > 0
                    ? cult.BreakthroughProgressRequired
                    : 100);
                DrawStatBar(rightX, ry, rightW, "修为", cult.Progress, req, BarBlue);
                ry += 22f;
                if (entity.TryGet<AttributesComponent>(out var attrs2))
                {
                    CombatDamageRules.EnsureVitals(entity);
                    var maxSp = Mathf.Max(0, attrs2.GetFinal(AttributeId.SpiritPower));
                    var curSp = maxSp;
                    if (entity.TryGet<CombatVitalsComponent>(out var vitals2))
                        curSp = Mathf.Clamp(vitals2.CurrentSpiritPower, 0, Mathf.Max(1, maxSp));
                    if (cult.Realm >= RealmStage.QiRefining && maxSp > 0)
                    {
                        DrawStatBar(rightX, ry, rightW, "灵力护盾", curSp, Mathf.Max(1, maxSp), BarTeal);
                        ry += 22f;
                    }
                    else
                    {
                        DrawStatBar(rightX, ry, rightW, "灵力", maxSp, Mathf.Max(100, maxSp), BarTeal);
                        ry += 22f;
                    }

                    if (cult.Realm >= RealmStage.Foundation)
                    {
                        var veilOn = SpiritVeilService.IsActive(entity);
                        GUI.Label(
                            new Rect(rightX, ry, rightW, 18f),
                            veilOn
                                ? "斗气纱衣　已展开（普攻远程 " +
                                  SpiritVeilRules.FoundationRangedEngageRange.ToString("0") +
                                  "）· F2 收起"
                                : "斗气纱衣　未展开 · F2 召唤（耗灵力 " +
                                  SpiritVeilRules.FoundationActivateSpiritCost + "）",
                            _parchmentBody);
                        ry += 20f;
                    }
                }

                GUI.Label(
                    new Rect(rightX, ry, rightW, 20f),
                    "修炼速 每5游戏分+" + CultivationProgressRules.BaseProgressPerTick +
                    " · 功法 " + ManualShortName(cult, session.World),
                    _parchmentBody);
                ry += 22f;
            }

            var infoY = Mathf.Max(y, ry) + 6f;
            GUI.Label(
                new Rect(area.x, infoY, area.width, area.yMax - infoY),
                BuildOverviewFacts(session, entity),
                _parchmentBody);
        }

        string BuildOverviewFacts(PlayableHostSession session, Entity entity)
        {
            var sb = new StringBuilder(256);
            if (entity.TryGet<EntityLocationComponent>(out var loc) && loc.HasLocation &&
                session.World.WorldRegion.TryGet(loc.LocationId, out var place))
            {
                var placeName = string.IsNullOrEmpty(place.Name) ? place.Id : place.Name;
                sb.Append("地点 ").Append(placeName).Append('\n');
            }

            if (entity.TryGet<FactionMembershipComponent>(out var faction) && faction.IsAffiliated)
                sb.Append("阵营 ").Append(ShortId(faction.FactionId))
                    .Append(" · ").Append(FactionRoleName(faction.Role)).Append('\n');

            if (entity.TryGet<PersonalConcealmentRiskComponent>(out var risk))
                sb.Append("暴露 ").Append(risk.Value).Append("/100\n");

            if (entity.TryGet<ScheduleComponent>(out var sched) &&
                !string.IsNullOrEmpty(sched.DefinitionId))
                sb.Append("课表 ").Append(ShortId(sched.DefinitionId)).Append('\n');

            if (entity.TryGet<ActivityTendencyComponent>(out var tendency) &&
                !string.IsNullOrEmpty(tendency.HomeWorkAreaId))
                sb.Append("住房 ").Append(ShortId(tendency.HomeWorkAreaId)).Append('\n');

            if (entity.TryGet<IdentityComponent>(out var id))
                sb.Append("定义 ").Append(ShortId(id.DefinitionId.ToString()));

            return sb.Length == 0 ? "—" : sb.ToString();
        }

        void DrawAttributesTab(Entity entity, Rect area)
        {
            if (!entity.TryGet<AttributesComponent>(out var attrs))
            {
                GUI.Label(area, "无属性数据", _parchmentBody);
                return;
            }

            var viewH = AttributeDisplayOrder.Length * 24f + 8f;
            _unitPanelScroll = GUI.BeginScrollView(
                area,
                _unitPanelScroll,
                new Rect(0f, 0f, area.width - 18f, viewH));
            var colW = (area.width - 28f) * 0.5f;
            for (var i = 0; i < AttributeDisplayOrder.Length; i++)
            {
                var id = AttributeDisplayOrder[i];
                var col = i % 2;
                var row = i / 2;
                var x = col * (colW + 10f);
                var y = row * 24f;
                var v = attrs.GetFinal(id);
                DrawStatBar(x, y, colW, AttributeName(id), v, AttributeBarMax(id, v), AttributeBarColor(id));
            }

            GUI.EndScrollView();
        }

        void DrawSpiritRootsTab(Entity entity, Rect area)
        {
            if (!entity.TryGet<SpiritRootComponent>(out var roots))
            {
                GUI.Label(area, "无灵根数据", _parchmentBody);
                return;
            }

            var viewH = SpiritRootDisplayOrder.Length * 24f + 8f;
            _unitPanelScroll = GUI.BeginScrollView(
                area,
                _unitPanelScroll,
                new Rect(0f, 0f, area.width - 18f, viewH));
            var colW = (area.width - 28f) * 0.5f;
            for (var i = 0; i < SpiritRootDisplayOrder.Length; i++)
            {
                var kind = SpiritRootDisplayOrder[i];
                var col = i % 2;
                var row = i / 2;
                var x = col * (colW + 10f);
                var y = row * 24f;
                var v = roots.Get(kind);
                DrawStatBar(x, y, colW, SpiritRootName(kind), v, SpiritRootComponent.DefaultMax, BarTeal);
            }

            GUI.EndScrollView();
        }

        void DrawCultivationTab(Entity entity, CultivationComponent cult, Rect area)
        {
            if (cult == null)
            {
                GUI.Label(area, "无修炼数据", _parchmentBody);
                return;
            }

            var req = Mathf.Max(1, cult.BreakthroughProgressRequired > 0
                ? cult.BreakthroughProgressRequired
                : 100);
            var y = area.y;
            GUI.Label(
                new Rect(area.x, y, area.width, 22f),
                "境界 " + RealmName(cult.Realm, cult.MinorStage),
                _parchmentBody);
            y += 24f;
            DrawStatBar(area.x, y, area.width, "修为进度", cult.Progress, req, BarBlue);
            y += 26f;
            GUI.Label(
                new Rect(area.x, y, area.width, 22f),
                "突破所需 " + cult.BreakthroughProgressRequired +
                " · 修炼速度 " + cult.CultivationSpeed,
                _parchmentBody);
            y += 24f;
            GUI.Label(
                new Rect(area.x, y, area.width, 22f),
                "功法 " + ManualShortName(cult, bootstrap?.Session?.World),
                _parchmentBody);
            y += 24f;
            if (!string.IsNullOrEmpty(cult.RequiredRealmName))
            {
                GUI.Label(
                    new Rect(area.x, y, area.width, 22f),
                    "所需境界名 " + cult.RequiredRealmName,
                    _parchmentBody);
            }

            if (entity.TryGet<AttributesComponent>(out var attrs))
            {
                y += 28f;
                var cultAttr = attrs.GetFinal(AttributeId.Cultivation);
                DrawStatBar(
                    area.x,
                    y,
                    area.width,
                    "修为属性",
                    cultAttr,
                    Mathf.Max(100, cultAttr),
                    BarViolet);
            }
        }

        void DrawPersonalityTab(Entity entity, Rect area)
        {
            var sb = new StringBuilder(512);
            if (entity.TryGet<CharacterBioComponent>(out var bio))
            {
                if (!string.IsNullOrEmpty(bio.Hometown))
                    sb.Append("籍贯 ").Append(bio.Hometown).Append('\n');
                sb.Append("声望 ").Append(bio.Reputation).Append('\n');
                if (bio.Goals.Count > 0)
                {
                    sb.Append("目标\n");
                    for (var i = 0; i < bio.Goals.Count; i++)
                        sb.Append("· ").Append(bio.Goals[i]).Append('\n');
                }

                if (bio.Desires.Count > 0)
                {
                    sb.Append("欲求\n");
                    for (var i = 0; i < bio.Desires.Count; i++)
                        sb.Append("· ").Append(bio.Desires[i]).Append('\n');
                }
            }

            if (entity.TryGet<PersonalityProfileComponent>(out var profile) && profile.Count > 0)
            {
                sb.Append("标签\n");
                foreach (var tag in profile.Tags)
                    sb.Append("· ").Append(tag).Append('\n');
            }

            if (sb.Length == 0)
                sb.Append("无性格／履历数据");
            DrawScrollText(area, sb.ToString());
        }

        void DrawTendencyTab(Entity entity, Rect area)
        {
            if (!entity.TryGet<ActivityTendencyComponent>(out var tendency))
            {
                GUI.Label(area, "无活动倾向数据", _parchmentBody);
                return;
            }

            var sb = new StringBuilder(512);
            if (!string.IsNullOrEmpty(tendency.HomeWorkAreaId))
                sb.Append("住房工区 ").Append(ShortId(tendency.HomeWorkAreaId)).Append('\n');
            if (tendency.PreferredWorkAreaIds.Count > 0)
            {
                sb.Append("偏好工区 ");
                for (var i = 0; i < tendency.PreferredWorkAreaIds.Count; i++)
                {
                    if (i > 0) sb.Append('、');
                    sb.Append(ShortId(tendency.PreferredWorkAreaIds[i]));
                }

                sb.Append('\n');
            }

            sb.Append("可做活动（按优先级）\n");
            tendency.CopyPrioritiesTo(_tendencyScratch);
            if (_tendencyScratch.Count == 0)
            {
                sb.Append("· （未配置，默认均可）\n");
            }
            else
            {
                for (var i = 0; i < _tendencyScratch.Count; i++)
                {
                    var item = _tendencyScratch[i];
                    sb.Append("· ").Append(ActivityName(item.Activity))
                        .Append("  优先 ").Append(item.Priority).Append('\n');
                }
            }

            DrawScrollText(area, sb.ToString());
        }

        void DrawScrollText(Rect area, string text)
        {
            var viewH = Mathf.Max(area.height, _parchmentBody.CalcHeight(new GUIContent(text), area.width - 18f) + 8f);
            _unitPanelScroll = GUI.BeginScrollView(
                area,
                _unitPanelScroll,
                new Rect(0f, 0f, area.width - 18f, viewH));
            GUI.Label(new Rect(0f, 0f, area.width - 18f, viewH), text, _parchmentBody);
            GUI.EndScrollView();
        }

        void DrawStatBar(float x, float y, float w, string label, int cur, int max, Color fill)
        {
            GUI.Label(new Rect(x, y, 56f, 20f), label, _parchmentBody);
            var bar = new Rect(x + 58f, y + 4f, Mathf.Max(40f, w - 62f), 14f);
            Fill(bar, new Color(0.55f, 0.48f, 0.38f, 0.55f));
            var pct = max > 0 ? Mathf.Clamp01(cur / (float)max) : 0f;
            var inner = new Rect(bar.x + 1f, bar.y + 1f, (bar.width - 2f) * pct, bar.height - 2f);
            Fill(inner, fill);
            DrawFrame(bar, Ink);
            var valueStyle = new GUIStyle(_parchmentBody)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            };
            HostImguiStyles.LockTextColor(valueStyle, Ink);
            GUI.Label(bar, cur + "/" + max, valueStyle);
        }

        static string ManualShortName(CultivationComponent cult, SimulationWorld world)
        {
            if (cult != null && cult.HasLearnedManual && cult.LearnedManualId.HasValue)
            {
                var mid = cult.LearnedManualId.Value;
                if (world != null &&
                    world.TryGetManual(mid, out var manual) &&
                    manual != null &&
                    !string.IsNullOrEmpty(manual.Name))
                    return manual.Name;

                var text = mid.ToString();
                var slash = text.LastIndexOf(':');
                return slash >= 0 && slash < text.Length - 1 ? text.Substring(slash + 1) : text;
            }

            return "还没有学功法";
        }

        static string AttributeName(AttributeId id) => HostAttributeLabels.Name(id);

        static int AttributeBarMax(AttributeId id, int value)
        {
            switch (id)
            {
                case AttributeId.MaxHp:
                    return Mathf.Max(1, value);
                case AttributeId.MindState:
                case AttributeId.Stamina:
                case AttributeId.SpiritPower:
                case AttributeId.Cultivation:
                    return Mathf.Max(100, value);
                case AttributeId.Physique:
                default:
                    return Mathf.Max(50, value);
            }
        }

        static Color AttributeBarColor(AttributeId id)
        {
            switch (id)
            {
                case AttributeId.MaxHp:
                case AttributeId.Physique:
                case AttributeId.Attack:
                case AttributeId.Defense:
                case AttributeId.Speed:
                case AttributeId.Stamina:
                    return BarOrange;
                case AttributeId.SpiritSense:
                case AttributeId.Comprehension:
                case AttributeId.MindState:
                    return BarViolet;
                default:
                    return BarBlue;
            }
        }

        static string SpiritRootName(SpiritRootKind kind)
        {
            switch (kind)
            {
                case SpiritRootKind.Fire: return "火";
                case SpiritRootKind.Metal: return "金";
                case SpiritRootKind.Earth: return "土";
                case SpiritRootKind.Wood: return "木";
                case SpiritRootKind.Thunder: return "雷";
                case SpiritRootKind.Wind: return "风";
                case SpiritRootKind.Ice: return "冰";
                case SpiritRootKind.Poison: return "毒";
                default: return kind.ToString();
            }
        }

        static string FactionRoleName(FactionRoleKind role)
        {
            switch (role)
            {
                case FactionRoleKind.LaborDisciple: return "杂役";
                case FactionRoleKind.Member: return "门人";
                case FactionRoleKind.Supervisor: return "主管";
                default: return role.ToString();
            }
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

            // F5／F9 存读档；Q/E/F8＝移动/交互/战斗；F2＝斗气纱衣；F7＝勘查；F1＝停止；F6＝修炼；G＝敛息。
            if (Input.GetKeyDown(KeyCode.Q) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
                InvokeActionIndex(focus, 0);
            else if (Input.GetKeyDown(KeyCode.F1))
                InvokeActionIndex(focus, 1);
            else if (Input.GetKeyDown(KeyCode.E) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
                InvokeActionIndex(focus, 2);
            else if (Input.GetKeyDown(KeyCode.F8))
                InvokeActionIndex(focus, 3);
            else if (Input.GetKeyDown(KeyCode.F2))
                ToggleSpiritVeilOrb(focus);
            else if (Input.GetKeyDown(KeyCode.F7))
                InvokeActionIndex(focus, 4);
            else if (Input.GetKeyDown(KeyCode.F6))
                InvokeActionIndex(focus, 5);
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
                    if (mode != null) mode.Cancel();
                    bootstrap?.CaveSurveyPresenter?.TrySurvey();
                    break;
                case 5:
                    PromptCultivateHere(focus);
                    break;
            }
        }

        void PromptCultivateHere(EntityId focus)
        {
            if (bootstrap == null || focus.IsNone)
                return;
            EnsureFocusSelected(focus);
            var mode = bootstrap.WorkTargetMode;
            if (mode != null)
                mode.Cancel();
            if (bootstrap.CultivateConfirm != null)
                bootstrap.CultivateConfirm.OpenFor(focus);
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
                AppendTrackedProgressLine(sb, session.World, spec, rt);
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

        static void AppendTrackedProgressLine(
            StringBuilder sb,
            SimulationWorld world,
            QuestSpec spec,
            QuestRuntime rt)
        {
            var count = rt.ProgressCount;
            var max = rt.ProgressMax;
            if (rt.Status == QuestStatus.Active &&
                QuestJournalQuery.TryGetStockProgress(world, spec, out var liveCount, out var liveMax))
            {
                count = liveCount;
                max = liveMax;
            }

            if (max > 0)
                sb.AppendLine("进度：" + count + "/" + max);
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

        static string RealmName(RealmStage realm, int minor = 0) =>
            RealmDisplay.Format(realm, minor);

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
                case ScheduleActivity.Idle: return "发呆";
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
