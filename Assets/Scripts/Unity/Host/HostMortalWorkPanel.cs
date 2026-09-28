using System;
using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Formal current-Site civilian work overview and profession command surface.</summary>
    public sealed class HostMortalWorkPanel : MonoBehaviour
    {
        const string PauseOwner = "MortalWorkPanel";
        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] bool open;
        readonly List<Entity> _people = new List<Entity>();
        Vector2 _scroll;
        bool _holdingPause;
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
            bootstrap.Session.AcquireModalPause(PauseOwner);
            _holdingPause = true;
            HostInputGate.BlockWorldCamera = HostInputGate.BlockWorldInteraction = true;
        }

        public void Close() { open = false; ReleasePause(); }
        public void ClearSessionState() { Close(); _scroll = Vector2.zero; _status = string.Empty; }
        void OnDisable() => ReleasePause();
        void ReleasePause()
        {
            if (_holdingPause && bootstrap?.Session != null) bootstrap.Session.ReleaseModalPause(PauseOwner);
            _holdingPause = false;
            HostInputGate.Clear();
        }

        void Update()
        {
            if (!open) return;
            if (bootstrap.InventoryPanel?.IsOpen == true || bootstrap.ConstructionPanel?.IsOpen == true ||
                bootstrap.WorldMapPanel?.IsOpen == true || bootstrap.QuestJournal?.IsOpen == true)
            { Close(); return; }
            HostInputGate.BlockWorldCamera = HostInputGate.BlockWorldInteraction = true;
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
            GUI.Label(new Rect(window.x + 18f, window.y + 12f, width - 120f, 30f), "工作", _title);
            if (GUI.Button(new Rect(window.xMax - 94f, window.y + 10f, 76f, 30f), "关闭")) { Close(); return; }

            var siteId = ResolveViewedSite(world);
            var siteName = "无当前聚落";
            if (!string.IsNullOrEmpty(siteId) && world.Strategic.Sites.TryGet(siteId, out var site))
                siteName = string.IsNullOrWhiteSpace(site.DisplayName) ? siteId : site.DisplayName;
            GUI.Label(new Rect(window.x + 18f, window.y + 46f, width - 36f, 24f),
                "当前聚落：" + siteName + (string.IsNullOrEmpty(_status) ? string.Empty : "    " + _status), _body);

            Collect(world, siteId);
            var view = new Rect(window.x + 14f, window.y + 76f, width - 28f, height - 92f);
            var contentHeight = Mathf.Max(view.height, _people.Count * 132f + 12f);
            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, view.width - 18f, contentHeight));
            if (_people.Count == 0) GUI.Label(new Rect(8f, 8f, view.width - 28f, 26f), "当前聚落没有可显示的凡人人员。", _body);
            for (var i = 0; i < _people.Count; i++) DrawPerson(world, _people[i], i * 132f + 4f, view.width - 32f);
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
            _people.Clear();
            if (string.IsNullOrEmpty(siteId)) return;
            foreach (var pair in world.Civilians.All)
                if (world.Entities.TryGet(pair.Key, out var entity) && MortalCivilianQuery.IsManagedCivilian(world, entity) &&
                    string.Equals(MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id), siteId, StringComparison.Ordinal))
                    _people.Add(entity);
            _people.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
        }

        void DrawPerson(SimulationWorld world, Entity entity, float y, float width)
        {
            var state = MortalCivilianService.Ensure(world, entity);
            var card = new Rect(4f, y, width - 8f, 124f);
            GUI.Box(card, GUIContent.none);
            var name = string.IsNullOrWhiteSpace(entity.DisplayName) ? entity.Id.ToString() : entity.DisplayName;
            var faction = entity.TryGet<FactionMembershipComponent>(out var membership) && membership.IsAffiliated
                ? StrategicFactionCatalog.DisplayName(membership.FactionId) : "无势力";
            var siteName = state.CurrentSiteId;
            if (world.Strategic.Sites.TryGet(state.CurrentSiteId, out var site) && !string.IsNullOrWhiteSpace(site.DisplayName)) siteName = site.DisplayName;
            var loyalty = CharacterFactionLoyaltyService.TryGetLoyalty(world, entity.Id, out var value) ? value.ToString() : "—";
            var manageable = MortalCivilianService.CanSetProfession(world, entity.Id, out var reason);
            var activity = HostMortalActivityPresentation.Describe(state);
            GUI.Label(new Rect(card.x + 10f, card.y + 7f, card.width - 20f, 22f),
                name + " · 凡人 · " + faction + " · " + siteName, _body);
            GUI.Label(new Rect(card.x + 10f, card.y + 30f, card.width - 20f, 20f),
                "职业：" + HostMortalActivityPresentation.Profession(state.Profession) +
                "    活动：" + activity + "    日程阶段：" +
                (FactionMortalSchedule.Current(world.Tick) == FactionMortalSchedulePhase.Work ? "工作" : "休息"), _small);
            GUI.Label(new Rect(card.x + 10f, card.y + 51f, card.width - 20f, 20f),
                "饱食 " + state.Satiety + "    精力 " + state.Energy + "    忠诚 " + loyalty +
                "    可管理：" + (manageable ? "是" : "否 · " + reason), _small);
            var professions = (MortalProfession[])Enum.GetValues(typeof(MortalProfession));
            var buttonW = Mathf.Min(112f, (card.width - 20f) / professions.Length);
            GUI.enabled = manageable;
            for (var p = 0; p < professions.Length; p++)
                if (GUI.Button(new Rect(card.x + 10f + p * buttonW, card.y + 82f, buttonW - 4f, 28f),
                    HostMortalActivityPresentation.Profession(professions[p])))
                {
                    var result = MortalCivilianService.SetProfession(world, entity.Id, professions[p]);
                    _status = result.IsSuccess ? name + "的职业已更新。" : result.Error.Message;
                }
            GUI.enabled = true;
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
