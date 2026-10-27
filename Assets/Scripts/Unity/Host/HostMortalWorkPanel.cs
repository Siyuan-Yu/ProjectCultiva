using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;
using XianXia.Core.Schedule;
using XianXia.Core.Social;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Formal current-Site civilian work overview and profession command surface.</summary>
    public sealed class HostMortalWorkPanel : MonoBehaviour
    {
        const string PauseOwner = "MortalWorkPanel";
        static readonly MortalProfession[] Professions = (MortalProfession[])Enum.GetValues(typeof(MortalProfession));
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] bool open;
        readonly List<Entity> _people = new List<Entity>();
        SimulationWorld _peopleWorld;
        string _peopleSiteId;
        int _peopleFrame = -1;
        Vector2 _scroll;
        bool _holdingPause;
        bool _patrolTab;
        string _status = string.Empty;
        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _small;

        public bool IsOpen => open;
        public void Bind(PlayableHostBootstrap host) => bootstrap = host;
        public void Toggle() { if (open) Close(); else Open(); }

        public void Open()
        {
            if (bootstrap?.Session == null || !bootstrap.Session.IsInitialized) return;
            bootstrap.InventoryPanel?.Close();
            bootstrap.ConstructionPanel?.Close();
            bootstrap.WorldMapPanel?.Close();
            bootstrap.QuestJournal?.Close();
            open = true;
            _peopleFrame = -1;
            bootstrap.Session.AcquireModalPause(PauseOwner);
            _holdingPause = true;
            HostInputGate.Acquire(PauseOwner);
        }

        public void Close() { open = false; ReleasePause(); }
        public void ClearSessionState()
        {
            Close(); _scroll = Vector2.zero; _status = string.Empty; _patrolTab = false;
            _people.Clear(); _peopleWorld = null; _peopleSiteId = null; _peopleFrame = -1;
        }
        void OnDisable() => ReleasePause();
        void ReleasePause()
        {
            if (_holdingPause && bootstrap?.Session != null) bootstrap.Session.ReleaseModalPause(PauseOwner);
            _holdingPause = false;
            HostInputGate.Release(PauseOwner);
        }

        void Update()
        {
            if (!open) return;
            if (bootstrap.InventoryPanel?.IsOpen == true || bootstrap.ConstructionPanel?.IsOpen == true ||
                bootstrap.WorldMapPanel?.IsOpen == true || bootstrap.QuestJournal?.IsOpen == true)
            { Close(); return; }
            HostInputGate.Acquire(PauseOwner);
            if (!_holdingPause && bootstrap?.Session != null)
            { bootstrap.Session.AcquireModalPause(PauseOwner); _holdingPause = true; }
        }

        void OnGUI()
        {
            if (!open || bootstrap?.Session == null || !bootstrap.Session.IsInitialized) return;
            EnsureStyles();
            var world = bootstrap.Session.World;
            var width = Mathf.Min(1040f, Screen.width - 40f);
            var height = Mathf.Min(700f, Screen.height - 40f);
            var window = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
            HostUiHitTest.Block(window);
            GUI.Box(window, GUIContent.none);
            HostImguiStyles.DrawPanelFrame(window);
            GUI.Label(new Rect(window.x + 18f, window.y + 12f, width - 120f, 30f), "工作", _title);
            if (GUI.Button(new Rect(window.xMax - 94f, window.y + 10f, 76f, 30f), "关闭")) { Close(); return; }

            var siteId = ResolveViewedSite(world);
            var siteName = "无当前聚落";
            if (!string.IsNullOrEmpty(siteId) && world.Strategic.Sites.TryGet(siteId, out var site))
                siteName = string.IsNullOrWhiteSpace(site.DisplayName) ? siteId : site.DisplayName;
            GUI.Label(new Rect(window.x + 18f, window.y + 46f, width - 36f, 24f),
                "当前聚落：" + siteName + (string.IsNullOrEmpty(_status) ? string.Empty : "    " + _status), _body);

            if (GUI.Button(new Rect(window.x + 18f, window.y + 76f, 120f, 28f), "凡人职业"))
            { _patrolTab = false; _peopleFrame = -1; _scroll = Vector2.zero; }
            if (GUI.Button(new Rect(window.x + 146f, window.y + 76f, 120f, 28f), "巡卫排班"))
            { _patrolTab = true; _peopleFrame = -1; _scroll = Vector2.zero; }
            if (_patrolTab && GUI.Button(new Rect(window.x + 274f, window.y + 76f, 120f, 28f), "巡视点编辑"))
            {
                var editor = bootstrap.GetComponent<HostPatrolRouteEditor>() ?? bootstrap.gameObject.AddComponent<HostPatrolRouteEditor>();
                Close(); editor.Open(bootstrap, siteId); return;
            }
            GUI.Label(new Rect(window.x + 408f, window.y + 79f, width - 428f, 24f),
                _patrolTab ? "白班 00–12点 · 晚班 12–24点 · 非当班时休息" : "按本城人员安排生产与照料", _small);
            Collect(world, siteId);
            if (_patrolTab)
            {
                var dayCount = 0; var nightCount = 0;
                foreach (var person in _people)
                { var shift = SettlementPatrolService.Shift(person); if (shift == 1) dayCount++; else if (shift == 2) nightCount++; }
                GUI.Label(new Rect(window.x + 18f, window.y + 106f, width - 36f, 20f),
                    "本城已排班：白班 " + dayCount + " 人 · 晚班 " + nightCount + " 人", _small);
            }
            var viewTop = _patrolTab ? 132f : 112f;
            var view = new Rect(window.x + 14f, window.y + viewTop, width - 28f, height - viewTop - 16f);
            const float cardStride = 168f;
            var contentHeight = Mathf.Max(view.height, _people.Count * cardStride + 12f);
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, view.width - 18f, contentHeight));
            if (_people.Count == 0) GUI.Label(new Rect(8f, 8f, view.width - 28f, 26f), "当前聚落没有可显示的人员。", _body);
            // Fixed-height cards use absolute rects, so offscreen rows need no queries or controls.
            var first = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / cardStride) - 1);
            var end = Mathf.Min(_people.Count, Mathf.CeilToInt((_scroll.y + view.height) / cardStride) + 1);
            for (var i = first; i < end; i++)
                if (_patrolTab) DrawPatrolPerson(world, siteId, _people[i], i * cardStride + 4f, view.width - 32f);
                else DrawPerson(world, _people[i], i * cardStride + 4f, view.width - 32f);
            GUI.EndScrollView();
        }

        string ResolveViewedSite(SimulationWorld world)
        {
            var selected = bootstrap.SelectionController?.State?.SelectedIds;
            if (selected != null && selected.Count > 0)
            {
                var selectedSite = MortalCivilianQuery.ResolveCurrentSiteId(world, selected[0]);
                if (!string.IsNullOrEmpty(selectedSite)) return selectedSite;
            }
            return bootstrap.Session.PlayerParty.HasActive
                ? MortalCivilianQuery.ResolveCurrentSiteId(world, bootstrap.Session.PlayerParty.ActiveCharacterId)
                : string.Empty;
        }

        void Collect(SimulationWorld world, string siteId)
        {
            if (_peopleFrame == Time.frameCount && ReferenceEquals(_peopleWorld, world) &&
                string.Equals(_peopleSiteId, siteId, StringComparison.Ordinal)) return;
            _peopleFrame = Time.frameCount;
            _peopleWorld = world;
            _peopleSiteId = siteId;
            _people.Clear();
            if (string.IsNullOrEmpty(siteId)) return;
            if (_patrolTab)
            {
                foreach (var entity in world.Entities.All)
                    if ((entity.Tags & (EntityTag.Character | EntityTag.Npc)) != 0 &&
                        MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id) == siteId)
                        _people.Add(entity);
                _people.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
                return;
            }
            foreach (var pair in world.Civilians.All)
                if (world.Entities.TryGet(pair.Key, out var entity) && MortalCivilianQuery.IsManagedCivilian(world, entity) &&
                    string.Equals(MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id), siteId, StringComparison.Ordinal))
                    _people.Add(entity);
            _people.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
        }

        void DrawPatrolPerson(SimulationWorld world, string siteId, Entity entity, float y, float width)
        {
            var card = new Rect(4f, y, width - 8f, 160f);
            GUI.Box(card, GUIContent.none);
            var name = string.IsNullOrWhiteSpace(entity.DisplayName) ? entity.Id.ToString() : entity.DisplayName;
            var shift = SettlementPatrolService.Shift(entity);
            var assigned = shift == 1 ? "白班 00:00–12:00" : shift == 2 ? "晚班 12:00–24:00" : "未安排巡卫";
            var manageable = SettlementPatrolService.CanAssign(world, siteId, entity, out var reason);
            GUI.Label(new Rect(card.x + 10f, card.y + 8f, card.width - 20f, 24f), name + " · " + assigned, _body);
            GUI.Label(new Rect(card.x + 10f, card.y + 36f, card.width - 20f, 22f),
                "当前：" + (SettlementPatrolService.IsOnDuty(world, entity) ? "当班巡视" : "未巡视／休息或行动受限"), _small);
            var status = !manageable ? reason : "可安排本城巡卫；撤下后恢复普通日程，原职业保留。";
            if (manageable && world.Civilians.TryGet(entity.Id, out var state) && !string.IsNullOrEmpty(state.ScheduleStatus))
                status = state.ScheduleStatus;
            GUI.Label(new Rect(card.x + 10f, card.y + 65f, card.width - 20f, 46f), status, _small);
            var oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && manageable;
            for (var choice = 0; choice < 3; choice++)
            {
                GUI.enabled = oldEnabled && manageable && choice != shift;
                var label = choice == 0 ? "撤下巡卫" : choice == 1 ? "安排白班" : "安排晚班";
                if (!GUI.Button(new Rect(card.x + 10f + choice * 138f, card.y + 120f, 130f, 28f), label)) continue;
                var result = SettlementPatrolService.Assign(world, bootstrap.Session.Loop, siteId, entity.Id, choice);
                _status = result.IsSuccess ? name + "的巡卫排班已更新。" : result.Error.Message;
            }
            GUI.enabled = oldEnabled;
        }

        void DrawPerson(SimulationWorld world, Entity entity, float y, float width)
        {
            var state = MortalCivilianService.Ensure(world, entity);
            var card = new Rect(4f, y, width - 8f, 160f);
            GUI.Box(card, GUIContent.none);
            var name = string.IsNullOrWhiteSpace(entity.DisplayName) ? entity.Id.ToString() : entity.DisplayName;
            var faction = entity.TryGet<FactionMembershipComponent>(out var membership) && membership.IsAffiliated
                ? StrategicFactionCatalog.DisplayName(membership.FactionId) : "无势力";
            var siteName = state.CurrentSiteId;
            if (world.Strategic.Sites.TryGet(state.CurrentSiteId, out var site) && !string.IsNullOrWhiteSpace(site.DisplayName)) siteName = site.DisplayName;
            var loyalty = CharacterFactionLoyaltyService.TryGetLoyalty(world, entity.Id, out var value) ? value.ToString() : "—";
            var manageable = MortalCivilianService.CanSetProfession(world, entity.Id, out var reason);
            var activity = HostMortalActivityPresentation.Describe(world, entity, state);
            FactionMortalSchedule.Current(world, entity, out _, out var scheduleBlock, out var scheduleReason);
            var targetWork = "暂无指定工区";
            if (entity.TryGet<MovementIntentComponent>(out var movement) &&
                !string.IsNullOrEmpty(movement.TargetWorkAreaId) &&
                world.TryGetWorkArea(movement.TargetWorkAreaId, out var targetArea))
                targetWork = string.IsNullOrWhiteSpace(targetArea.Name) ? "已指定工区" : targetArea.Name;
            var targetBed = RoomBedFacilityService.TryGetAssignedBed(world, entity.Id, out _)
                ? "已分配" : "未分配";
            GUI.Label(new Rect(card.x + 10f, card.y + 7f, card.width - 20f, 22f),
                name + " · 凡人 · " + faction + " · " + siteName, _body);
            GUI.Label(new Rect(card.x + 10f, card.y + 30f, card.width - 20f, 20f),
                "职业：" + HostMortalActivityPresentation.Profession(state.Profession) +
                "    当前行动：" + activity + "    当前时段：" + ScheduleName(scheduleBlock?.Activity), _small);
            GUI.Label(new Rect(card.x + 10f, card.y + 51f, card.width - 20f, 20f),
                "饱食 " + state.Satiety + "    精力 " + state.Energy + "    忠诚 " + loyalty +
                "    可管理：" + (manageable ? "是" : "否 · " + reason), _small);
            GUI.Label(new Rect(card.x + 10f, card.y + 72f, card.width - 20f, 20f),
                "前往地点：" + targetWork + "    住处床位：" + targetBed, _small);
            GUI.Label(new Rect(card.x + 10f, card.y + 92f, card.width - 20f, 20f),
                "状态：" + (state.Profession == MortalProfession.Warden ? state.WardenStatus :
                    string.IsNullOrEmpty(state.ScheduleStatus) ? scheduleReason : state.ScheduleStatus), _small);
            var buttonW = Mathf.Min(112f, (card.width - 20f) / Professions.Length);
            GUI.enabled = manageable;
            for (var p = 0; p < Professions.Length; p++)
                if (GUI.Button(new Rect(card.x + 10f + p * buttonW, card.y + 122f, buttonW - 4f, 28f),
                    HostMortalActivityPresentation.Profession(Professions[p])))
                {
                    var result = MortalCivilianService.SetProfession(world, entity.Id, Professions[p]);
                    _status = result.IsSuccess ? name + "的职业已更新。" : result.Error.Message;
                    if (result.IsSuccess) HostAudioFeedback.Play(bootstrap.gameObject, HostAudioFeedback.Cue.Confirm);
                }
            GUI.enabled = true;
        }

        static string ScheduleName(ScheduleActivity? activity)
        {
            switch (activity)
            {
                case ScheduleActivity.Labor: return "工作";
                case ScheduleActivity.Rest: return "休息";
                case ScheduleActivity.Eat: return "用餐";
                case ScheduleActivity.Cultivate: return "修炼";
                case ScheduleActivity.Explore: return "外出";
                case ScheduleActivity.Patrol: return "巡逻";
                case ScheduleActivity.Inspect: return "巡视";
                case ScheduleActivity.Idle: return "自由活动";
                default: return "暂无日程";
            }
        }

        void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        }
    }
}
