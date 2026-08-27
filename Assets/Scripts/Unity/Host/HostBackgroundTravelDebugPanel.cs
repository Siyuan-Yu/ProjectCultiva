using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>Phase 2D：Background Character World Travel 开发验收（非正式 Gameplay）。</summary>
    public sealed class HostBackgroundTravelDebugPanel : MonoBehaviour
    {
        const int WindowId = 0xB6D712;
        const float PanelWidth = 560f;
        const float PanelMinHeight = 480f;
        const float PanelMaxHeight = 640f;
        const float TitleBarHeight = 22f;
        const float ActionLogHeight = 56f;
        const float LineH = 18f;

        struct CharacterEntry
        {
            public EntityId Id;
            public string DisplayName;
            public CharacterWorldMovementAuthority Authority;
            public string DropdownLabel;
        }

        struct SiteEntry
        {
            public string SiteId;
            public string DisplayName;
        }

        [SerializeField] PlayableHostBootstrap bootstrap;
        [SerializeField] HostSelectionController selectionController;
        [SerializeField] bool visible;
        [SerializeField] KeyCode toggleKey = KeyCode.F12;
        [SerializeField] int targetHexQ = 10;
        [SerializeField] int targetHexR = 4;

        readonly List<CharacterEntry> _characters = new List<CharacterEntry>(32);
        readonly List<SiteEntry> _sites = new List<SiteEntry>(64);

        EntityId _selectedCharacterId = EntityId.None;
        int _selectedSiteIndex;
        bool _characterMenuOpen;
        bool _siteMenuOpen;
        string _actionLog = "F12 · 拖标题栏移动";
        Rect _panelRect;
        Vector2 _actionScroll;
        bool _panelRectInitialized;
        static GUIStyle _wrapLabel;

        public void Bind(PlayableHostBootstrap host, HostSelectionController selection)
        {
            bootstrap = host;
            selectionController = selection;
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

            EnsurePanelRect();
            _panelRect = GUI.Window(WindowId, _panelRect, DrawPanelWindow, "Background Travel DEBUG (F12)");
            HostUiHitTest.Block(_panelRect);
            ClampPanelToScreen();
        }

        void EnsurePanelRect()
        {
            if (_panelRectInitialized)
                return;

            var x = Mathf.Max(560f, Screen.width - PanelWidth - 8f);
            var y = HostFormalHud.HeaderReservedHeight + 8f;
            _panelRect = new Rect(x, y, PanelWidth, PanelMinHeight);
            _panelRectInitialized = true;
        }

        static GUIStyle WrapLabel()
        {
            if (_wrapLabel != null)
                return _wrapLabel;
            _wrapLabel = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                richText = false,
            };
            return _wrapLabel;
        }

        void ClampPanelToScreen()
        {
            _panelRect.x = Mathf.Clamp(_panelRect.x, 0f, Mathf.Max(0f, Screen.width - _panelRect.width));
            _panelRect.y = Mathf.Clamp(_panelRect.y, 0f, Mathf.Max(0f, Screen.height - _panelRect.height));
        }

        void DrawPanelWindow(int windowId)
        {
            var session = bootstrap?.Session;
            var world = session?.World;
            var party = session?.PlayerParty;
            if (world == null || !session.IsInitialized)
            {
                GUI.Label(new Rect(8f, TitleBarHeight + 4f, _panelRect.width - 16f, 22f), "Session not ready.");
                GUI.DragWindow(new Rect(0f, 0f, 10000f, TitleBarHeight));
                return;
            }

            RebuildCharacterCache(world, party);
            RebuildSiteCache(world);
            EnsureSelectedCharacter();
            EnsureSelectedSiteIndex();

            const float pad = 8f;
            var innerW = _panelRect.width - pad * 2f;
            var y = TitleBarHeight + 4f;

            // Character selector
            GUI.Label(new Rect(pad, y, 110f, 22f), "Character:");
            var charBtnRect = new Rect(pad + 112f, y, innerW - 112f, 22f);
            if (DrawDropdownButton(charBtnRect, GetSelectedCharacterLabel(), ref _characterMenuOpen))
            {
                _siteMenuOpen = false;
                DrawCharacterMenu(new Rect(charBtnRect.x, charBtnRect.yMax + 2f, charBtnRect.width, 0f));
            }

            y += 28f;
            y = DrawStatusBlock(world, party, pad, y, innerW);
            y += 4f;

            var travelEligible = TryGetTravelEligibility(world, party, out var eligibilityReason);
            if (!travelEligible && !string.IsNullOrEmpty(eligibilityReason))
            {
                GUI.Label(new Rect(pad, y, innerW, LineH), eligibilityReason, WrapLabel());
                y += LineH + 2f;
            }

            GUI.Label(new Rect(pad, y, 150f, 22f), "Destination WorldSite:");
            var siteBtnRect = new Rect(pad + 152f, y, innerW - 152f, 22f);
            if (DrawDropdownButton(siteBtnRect, GetSelectedSiteLabel(), ref _siteMenuOpen))
            {
                _characterMenuOpen = false;
                DrawSiteMenu(new Rect(siteBtnRect.x, siteBtnRect.yMax + 2f, siteBtnRect.width, 0f));
            }

            y += 28f;
            GUI.Label(new Rect(pad, y, 80f, 22f), "Hex Q/R");
            var qText = GUI.TextField(new Rect(pad + 84f, y, 60f, 22f), targetHexQ.ToString());
            var rText = GUI.TextField(new Rect(pad + 150f, y, 60f, 22f), targetHexR.ToString());
            int.TryParse(qText, out targetHexQ);
            int.TryParse(rText, out targetHexR);
            y += 30f;

            GUI.enabled = travelEligible;
            if (GUI.Button(new Rect(pad, y, 150f, 24f), "Travel To WorldSite"))
                TravelToSite();
            if (GUI.Button(new Rect(pad + 158f, y, 120f, 24f), "Travel To Hex"))
                TravelToHex();
            GUI.enabled = true;
            if (GUI.Button(new Rect(pad + 286f, y, 100f, 24f), "Cancel"))
                CancelTravel();
            y += 30f;

            if (GUI.Button(new Rect(pad, y, 120f, 24f), "Advance 8 ticks"))
                AdvanceTicks(8);
            if (GUI.Button(new Rect(pad + 128f, y, 120f, 24f), "Advance 32 ticks"))
                AdvanceTicks(32);
            y += 32f;

            var logH = MeasureLogHeight(_actionLog, innerW - 16f);
            var scrollRect = new Rect(pad, y, innerW, ActionLogHeight);
            _actionScroll = GUI.BeginScrollView(scrollRect, _actionScroll, new Rect(0f, 0f, innerW - 16f, logH));
            GUI.Label(new Rect(0f, 0f, innerW - 16f, logH), _actionLog, WrapLabel());
            GUI.EndScrollView();
            y += ActionLogHeight + pad;

            _panelRect.height = Mathf.Clamp(y + TitleBarHeight, PanelMinHeight, PanelMaxHeight);
            GUI.DragWindow(new Rect(0f, 0f, 10000f, TitleBarHeight));
        }

        static float MeasureLogHeight(string text, float width)
        {
            if (string.IsNullOrEmpty(text))
                return 24f;
            return Mathf.Max(24f, WrapLabel().CalcHeight(new GUIContent(text), width));
        }

        static bool DrawDropdownButton(Rect rect, string label, ref bool menuOpen)
        {
            var clicked = GUI.Button(rect, label + "  ▼");
            if (clicked)
                menuOpen = !menuOpen;
            return menuOpen;
        }

        void DrawCharacterMenu(Rect anchor)
        {
            const float rowH = 22f;
            var h = _characters.Count * rowH + 4f;
            GUI.Box(new Rect(anchor.x, anchor.y, anchor.width, h), GUIContent.none);
            for (var i = 0; i < _characters.Count; i++)
            {
                var row = new Rect(anchor.x + 2f, anchor.y + 2f + i * rowH, anchor.width - 4f, rowH);
                if (GUI.Button(row, _characters[i].DropdownLabel))
                {
                    _selectedCharacterId = _characters[i].Id;
                    _characterMenuOpen = false;
                }
            }
        }

        void DrawSiteMenu(Rect anchor)
        {
            const float rowH = 22f;
            var h = _sites.Count * rowH + 4f;
            GUI.Box(new Rect(anchor.x, anchor.y, anchor.width, h), GUIContent.none);
            for (var i = 0; i < _sites.Count; i++)
            {
                var row = new Rect(anchor.x + 2f, anchor.y + 2f + i * rowH, anchor.width - 4f, rowH);
                if (GUI.Button(row, _sites[i].DisplayName))
                {
                    _selectedSiteIndex = i;
                    _siteMenuOpen = false;
                }
            }
        }

        float DrawStatusBlock(
            SimulationWorld world,
            PlayerPartyRuntime party,
            float pad,
            float y,
            float innerW)
        {
            var id = _selectedCharacterId;
            if (id.IsNone)
            {
                GUI.Label(new Rect(pad, y, innerW, LineH), "Character: (none)");
                return y + LineH;
            }

            world.Entities.TryGet(id, out var entity);
            var displayName = entity != null ? entity.DisplayName : id.Value.ToString();

            if (party != null && party.HasActive && party.ActiveCharacterId == id &&
                world.PlayerPartyTravel != null)
            {
                return DrawPlayerPartyAuthorityBlock(world, party, displayName, id, pad, y, innerW);
            }

            if (!BackgroundCharacterTravelService.TryDescribeTravel(
                    world, id, party,
                    out var authority,
                    out var kind,
                    out var siteId,
                    out var pos,
                    out var hex,
                    out var travelKind,
                    out var destHex,
                    out var destSite,
                    out var seg,
                    out var segProgress))
            {
                GUI.Label(new Rect(pad, y, innerW, LineH), "Character: " + displayName + "  Id=" + id.Value);
                y += LineH;
                GUI.Label(new Rect(pad, y, innerW, LineH), "(no world location)");
                return y + LineH;
            }

            var destText = !string.IsNullOrEmpty(destSite)
                ? ResolveSiteDisplayName(world, destSite)
                : destHex.ToString();
            var routeProgress = travelKind == BackgroundCharacterTravelMovementKind.Traveling
                ? "Seg " + seg + "  P=" + segProgress.ToString("F2")
                : "-";

            DrawStatusLine(pad, ref y, innerW, "Character:", displayName + "  Id=" + id.Value);
            DrawStatusLine(pad, ref y, innerW, "Authority:", FormatAuthorityLabel(authority));
            if (kind == BackgroundCharacterLocationKind.AtWorldSite)
            {
                DrawStatusLine(pad, ref y, innerW, "Location:", "AtWorldSite(" + ResolveSiteDisplayName(world, siteId) + ")");
                DrawStatusLine(pad, ref y, innerW, "PresenceHex:", hex.ToString());
            }
            else
            {
                DrawStatusLine(pad, ref y, innerW, "Location:", kind.ToString());
                DrawStatusLine(pad, ref y, innerW, "CurrentHex:", hex.ToString());
            }

            DrawStatusLine(pad, ref y, innerW, "Site:", ResolveSiteDisplayName(world, siteId));
            DrawStatusLine(pad, ref y, innerW, "WorldPosition:",
                "(" + pos.X.ToString("F2") + ", " + pos.Y.ToString("F2") + ")");
            DrawStatusLine(pad, ref y, innerW, "TravelState:", travelKind.ToString());
            DrawStatusLine(pad, ref y, innerW, "Destination:", destText);
            DrawStatusLine(pad, ref y, innerW, "Route Progress:", routeProgress);
            return y;
        }

        static float DrawPlayerPartyAuthorityBlock(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string displayName,
            EntityId id,
            float pad,
            float y,
            float innerW)
        {
            var motion = world.PlayerPartyTravel;
            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;
            var travelPresentation = motion.IsMoving
                ? motion.ResolveTravelPresentationWorld(hexSize)
                : motion.WorldPosition;
            var insideSite = WorldSiteFootprintLocationAuthority.TryGetSiteAtHex(
                world,
                motion.CurrentHex,
                out var footprintSite) &&
                footprintSite != null;
            var travelState = motion.IsMoving ? "AutoTravel" : "Idle";
            var destText = !string.IsNullOrEmpty(motion.DestinationSiteId)
                ? ResolveSiteDisplayName(world, motion.DestinationSiteId)
                : motion.DestinationHex.ToString();
            var routeProgress = motion.IsMoving
                ? "Seg " + motion.SegmentIndex + "  P=" + motion.SegmentProgress.ToString("F2")
                : "-";

            DrawStatusLine(pad, ref y, innerW, "Character:", displayName + "  Id=" + id.Value + "  [PlayerParty]");
            DrawStatusLine(pad, ref y, innerW, "WorldLocationKind:", motion.LocationKind.ToString());
            DrawStatusLine(pad, ref y, innerW, "WorldLocationSiteId:",
                string.IsNullOrEmpty(motion.SiteId) ? "-" : ResolveSiteDisplayName(world, motion.SiteId));
            DrawStatusLine(pad, ref y, innerW, "CurrentHex:", motion.CurrentHex.ToString());
            DrawStatusLine(pad, ref y, innerW, "InsideWorldSite:", insideSite ? "true" : "false");
            DrawStatusLine(pad, ref y, innerW, "InsideSiteId:",
                insideSite ? ResolveSiteDisplayName(world, footprintSite.SiteId) : "-");
            DrawStatusLine(pad, ref y, innerW, "TravelPresentationPosition:",
                "(" + travelPresentation.X.ToString("F2") + ", " + travelPresentation.Y.ToString("F2") + ")");
            DrawStatusLine(pad, ref y, innerW, "TravelState:", travelState);
            DrawStatusLine(pad, ref y, innerW, "SiteDeparturePending:", motion.IsSiteDeparturePending.ToString());
            DrawStatusLine(pad, ref y, innerW, "UsesTravelPresentation:", motion.UsesTravelPresentation.ToString());
            DrawStatusLine(pad, ref y, innerW, "Destination:", destText);
            DrawStatusLine(pad, ref y, innerW, "Route Progress:", routeProgress);
            return y;
        }

        static void DrawStatusLine(float pad, ref float y, float innerW, string key, string value)
        {
            GUI.Label(new Rect(pad, y, 120f, LineH), key);
            GUI.Label(new Rect(pad + 122f, y, innerW - 122f, LineH), value ?? "-");
            y += LineH;
        }

        void RebuildCharacterCache(SimulationWorld world, PlayerPartyRuntime party)
        {
            _characters.Clear();
            foreach (var entity in world.Entities.All)
            {
                if (entity == null || (entity.Tags & EntityTag.Character) == 0)
                    continue;
                CharacterWorldMovementAuthorityQuery.TryGetAuthority(
                    world, entity.Id, party, out var authority);
                var authLabel = FormatAuthorityLabel(authority);
                var name = string.IsNullOrEmpty(entity.DisplayName)
                    ? "Id=" + entity.Id.Value
                    : entity.DisplayName;
                _characters.Add(new CharacterEntry
                {
                    Id = entity.Id,
                    DisplayName = name,
                    Authority = authority,
                    DropdownLabel = name + " / " + authLabel,
                });
            }

            _characters.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
        }

        void RebuildSiteCache(SimulationWorld world)
        {
            _sites.Clear();
            foreach (var kv in world.Strategic.Sites.Sites)
            {
                var site = kv.Value;
                if (site == null || string.IsNullOrEmpty(site.SiteId))
                    continue;
                _sites.Add(new SiteEntry
                {
                    SiteId = site.SiteId,
                    DisplayName = string.IsNullOrEmpty(site.DisplayName) ? site.SiteId : site.DisplayName,
                });
            }

            _sites.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
        }

        void EnsureSelectedCharacter()
        {
            if (!_selectedCharacterId.IsNone)
            {
                for (var i = 0; i < _characters.Count; i++)
                {
                    if (_characters[i].Id == _selectedCharacterId)
                        return;
                }
            }

            _selectedCharacterId = EntityId.None;
            EntityId fallback = EntityId.None;
            for (var i = 0; i < _characters.Count; i++)
            {
                var id = _characters[i].Id;
                if (_characters[i].Authority == CharacterWorldMovementAuthority.PlayerParty)
                    continue;
                if (fallback.IsNone)
                    fallback = id;
                var session = bootstrap?.Session;
                if (session != null &&
                    CharacterWorldMovementAuthorityQuery.CanStartBackgroundTravelDebug(
                        session.World, id, session.PlayerParty, out _))
                {
                    _selectedCharacterId = id;
                    return;
                }
            }

            if (!fallback.IsNone)
                _selectedCharacterId = fallback;
            else if (_characters.Count > 0)
                _selectedCharacterId = _characters[0].Id;
        }

        void EnsureSelectedSiteIndex()
        {
            if (_sites.Count == 0)
            {
                _selectedSiteIndex = 0;
                return;
            }

            if (_selectedSiteIndex >= 0 && _selectedSiteIndex < _sites.Count)
                return;

            _selectedSiteIndex = 0;
            for (var i = 0; i < _sites.Count; i++)
            {
                if (_sites[i].SiteId == "base:site_chengzhen")
                {
                    _selectedSiteIndex = i;
                    return;
                }
            }
        }

        string GetSelectedCharacterLabel()
        {
            for (var i = 0; i < _characters.Count; i++)
            {
                if (_characters[i].Id == _selectedCharacterId)
                    return _characters[i].DropdownLabel;
            }

            return "(select character)";
        }

        string GetSelectedSiteLabel()
        {
            if (_sites.Count == 0)
                return "(no sites)";
            var idx = Mathf.Clamp(_selectedSiteIndex, 0, _sites.Count - 1);
            return _sites[idx].DisplayName;
        }

        string GetSelectedSiteId()
        {
            if (_sites.Count == 0)
                return string.Empty;
            var idx = Mathf.Clamp(_selectedSiteIndex, 0, _sites.Count - 1);
            return _sites[idx].SiteId;
        }

        bool TryGetTravelEligibility(SimulationWorld world, PlayerPartyRuntime party, out string reason)
        {
            reason = string.Empty;
            if (_selectedCharacterId.IsNone)
            {
                reason = "Not eligible: no character selected.";
                return false;
            }

            if (CharacterWorldMovementAuthorityQuery.CanStartBackgroundTravelDebug(
                    world, _selectedCharacterId, party, out var err))
                return true;

            reason = FormatEligibilityReason(err);
            return false;
        }

        static string FormatEligibilityReason(string err)
        {
            if (string.IsNullOrEmpty(err))
                return "Not eligible.";
            if (err.Contains("PlayerParty"))
                return "Not eligible: PlayerParty authority";
            if (err.Contains("FormalArmy"))
                return "Not eligible: FormalArmy authority";
            if (err.Contains("Already background"))
                return "Not eligible: already background traveling";
            return "Not eligible: " + err;
        }

        static string FormatAuthorityLabel(CharacterWorldMovementAuthority authority)
        {
            switch (authority)
            {
                case CharacterWorldMovementAuthority.PlayerParty:
                    return "PlayerParty";
                case CharacterWorldMovementAuthority.FormalArmy:
                    return "FormalArmy";
                case CharacterWorldMovementAuthority.LoadedLocalRealtime:
                    return "LocalRealtime";
                case CharacterWorldMovementAuthority.BackgroundTravel:
                    return "Background";
                default:
                    return "Background";
            }
        }

        static string ResolveSiteDisplayName(SimulationWorld world, string siteId)
        {
            if (string.IsNullOrEmpty(siteId))
                return "-";
            if (world.Strategic.Sites.TryGet(siteId, out var site) &&
                site != null &&
                !string.IsNullOrEmpty(site.DisplayName))
                return site.DisplayName;
            return siteId;
        }

        void TravelToSite()
        {
            var session = bootstrap?.Session;
            var id = _selectedCharacterId;
            var siteId = GetSelectedSiteId();
            if (session == null || id.IsNone || string.IsNullOrEmpty(siteId))
            {
                _actionLog = "Select character and destination site.";
                return;
            }

            var result = BackgroundCharacterTravelService.BeginTravelToWorldSite(
                session.World,
                id,
                siteId,
                session.PlayerParty,
                debugOverrideLocalOccupant: true);
            _actionLog = result.IsSuccess
                ? "Started site travel → " + ResolveSiteDisplayName(session.World, siteId)
                : result.Error.ToString();
            if (result.IsSuccess)
                bootstrap?.FlushLoadedDestinationArrivals();
        }

        void TravelToHex()
        {
            var session = bootstrap?.Session;
            var id = _selectedCharacterId;
            if (session == null || id.IsNone)
            {
                _actionLog = "Select character.";
                return;
            }

            var requestedHex = new HexCoord(targetHexQ, targetHexR);
            string resolvedSiteId = null;
            if (session.World.Strategic?.Sites != null &&
                session.World.Strategic.Sites.TryGetAtHex(requestedHex, out var resolvedSite) &&
                resolvedSite != null)
            {
                resolvedSiteId = resolvedSite.SiteId;
            }

            var result = BackgroundCharacterTravelService.BeginTravelToHex(
                session.World,
                id,
                requestedHex,
                session.PlayerParty,
                debugOverrideLocalOccupant: true);
            if (result.IsSuccess)
            {
                _actionLog = !string.IsNullOrEmpty(resolvedSiteId)
                    ? "Started hex travel → (" + targetHexQ + "," + targetHexR + ") → Resolved WorldSite(" +
                      ResolveSiteDisplayName(session.World, resolvedSiteId) + ")"
                    : "Started hex travel → (" + targetHexQ + "," + targetHexR + ")";
            }
            else
            {
                _actionLog = result.Error.ToString();
            }

            if (result.IsSuccess)
                bootstrap?.FlushLoadedDestinationArrivals();
        }

        void CancelTravel()
        {
            var session = bootstrap?.Session;
            var id = _selectedCharacterId;
            if (session == null || id.IsNone)
            {
                _actionLog = "Select character.";
                return;
            }

            var result = BackgroundCharacterTravelService.CancelTravel(session.World, id);
            _actionLog = result.IsSuccess ? "Canceled background travel." : result.Error.ToString();
        }

        void AdvanceTicks(int ticks)
        {
            var session = bootstrap?.Session;
            if (session?.Loop == null)
            {
                _actionLog = "Loop missing.";
                return;
            }

            for (var i = 0; i < ticks; i++)
                session.Loop.TickOnce();
            bootstrap?.FlushLoadedDestinationArrivals();
            _actionLog = "Advanced " + ticks + " ticks.";
        }
    }
}
