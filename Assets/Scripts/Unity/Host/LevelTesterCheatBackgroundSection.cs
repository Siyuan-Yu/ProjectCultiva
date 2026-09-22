using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

using XianXia.Core.Exploration;
namespace XianXia.Unity.Host
{
    /// <summary>LevelTester Cheat · Background Character travel。</summary>
    public sealed class LevelTesterCheatBackgroundSection
    {
        struct CharacterEntry
        {
            public EntityId Id;
            public string DropdownLabel;
        }

        struct SiteEntry
        {
            public string SiteId;
            public string DisplayName;
        }

        readonly List<CharacterEntry> _characters = new List<CharacterEntry>(32);
        readonly List<SiteEntry> _sites = new List<SiteEntry>(64);

        EntityId _selectedCharacterId = EntityId.None;
        int _selectedSiteIndex;
        int _selectedCharacterIndex;
        bool _characterMenuOpen;
        bool _siteMenuOpen;
        string _sectionStatus = string.Empty;
        string _eligibilityReason = string.Empty;

        public string SectionStatus => _sectionStatus;

        public float Draw(
            PlayableHostBootstrap bootstrap,
            float x,
            float y,
            float width,
            GUIStyle body)
        {
            var lineH = 18f;
            var session = bootstrap?.Session;
            var world = session?.World;
            var party = session?.PlayerParty;

            GUI.Label(new Rect(x, y, width, lineH), "后台角色（调试覆盖：本地占用）", body);
            y += lineH + 4f;

            if (world == null || !session.IsInitialized)
            {
                GUI.Label(new Rect(x, y, width, lineH), "会话未就绪。");
                return y + lineH;
            }

            RebuildCharacterCache(world, party);
            RebuildSiteCache(world);
            EnsureSelectedCharacter();
            EnsureSelectedSiteIndex();

            GUI.Label(new Rect(x, y, 70f, lineH), "角色");
            var charBtn = new Rect(x + 72f, y, width - 72f, 22f);
            if (DrawDropdownButton(charBtn, GetSelectedCharacterLabel(), ref _characterMenuOpen))
            {
                _siteMenuOpen = false;
                DrawCharacterMenu(new Rect(charBtn.x, charBtn.yMax + 2f, charBtn.width, 0f));
            }

            y += 26f;
            y = DrawStatusBlock(world, party, x, y, width, lineH, body);
            y += 4f;

            var travelEligible = TryGetTravelEligibility(world, party, out _eligibilityReason);
            if (!travelEligible && !string.IsNullOrEmpty(_eligibilityReason))
            {
                GUI.Label(new Rect(x, y, width, lineH * 2f), _eligibilityReason, body);
                y += lineH * 2f;
            }

            GUI.Label(new Rect(x, y, 70f, lineH), "地点");
            var siteBtn = new Rect(x + 72f, y, width - 72f, 22f);
            if (DrawDropdownButton(siteBtn, GetSelectedSiteLabel(), ref _siteMenuOpen))
            {
                _characterMenuOpen = false;
                DrawSiteMenu(new Rect(siteBtn.x, siteBtn.yMax + 2f, siteBtn.width, 0f));
            }

            y += 26f;
            GUI.enabled = travelEligible;
            if (GUI.Button(new Rect(x, y, width * .68f, 24f), "前往世界地点"))
                TravelToSite(bootstrap);
            GUI.enabled = true;
            if (GUI.Button(new Rect(x + width * .70f, y, width * .30f, 24f), "取消"))
                CancelTravel(bootstrap);

            y += 30f;
            if (!string.IsNullOrEmpty(_sectionStatus))
            {
                GUI.Label(new Rect(x, y, width, lineH * 3f), _sectionStatus, body);
                y += lineH * 3f;
            }

            return y;
        }

        static bool DrawDropdownButton(Rect rect, string label, ref bool menuOpen)
        {
            var clicked = GUI.Button(rect, label + "  v");
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
                    _selectedCharacterIndex = i;
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
            float x,
            float y,
            float width,
            float lineH,
            GUIStyle body)
        {
            var id = _selectedCharacterId;
            if (id.IsNone)
            {
                GUI.Label(new Rect(x, y, width, lineH), "角色：（无）", body);
                return y + lineH;
            }

            world.Entities.TryGet(id, out var entity);
            var displayName = entity != null ? entity.DisplayName : id.Value.ToString();

            if (party != null && party.HasActive && party.ActiveCharacterId == id &&
                world.PlayerPartyTravel != null)
            {
                return DrawPlayerPartyBlock(world, party, displayName, x, y, width, lineH, body);
            }

            if (!BackgroundCharacterTravelService.TryResolveCharacterWorldLocation(
                    world, id, out var kind, out var siteId, out var pos, out var surfaceId))
            {
                GUI.Label(new Rect(x, y, width, lineH), displayName + " —（无世界位置）", body);
                return y + lineH;
            }

            y = DrawLine(x, y, width, lineH, body, "位置", kind.ToString());
            y = DrawLine(x, y, width, lineH, body, "Surface", surfaceId);
            y = DrawLine(x, y, width, lineH, body, "WorldPos", pos.ToString());
            y = DrawLine(x, y, width, lineH, body, "地点", ResolveSiteDisplayName(world, siteId));
            return y;
        }

        static float DrawPlayerPartyBlock(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string displayName,
            float x,
            float y,
            float width,
            float lineH,
            GUIStyle body)
        {
            var motion = world.PlayerPartyTravel;
            var dest = !string.IsNullOrEmpty(motion.DestinationSiteId)
                ? motion.DestinationSiteId
                : motion.HasContinuousPhysicalDestination
                    ? motion.ContinuousPhysicalDestination.ToString()
                    : "-";
            var localMap = world.PartyWorld?.LocalMapId ?? "-";
            var localPos = "-";
            if (party.HasActive &&
                world.Entities.TryGet(party.ActiveCharacterId, out var ent) &&
                ent != null &&
                ent.TryGet<EntityLocationComponent>(out var loc) &&
                loc != null &&
                loc.HasPresentationOverride)
            {
                localPos = loc.PresentationOverrideX.ToString("F1") + "," +
                           loc.PresentationOverrideZ.ToString("F1");
            }

            y = DrawLine(x, y, width, lineH, body, "角色", displayName + " [玩家队伍]");
            y = DrawLine(x, y, width, lineH, body, "MovementKind", motion.MovementKind.ToString());
            y = DrawLine(x, y, width, lineH, body, "ExecutionMode", motion.ExecutionMode.ToString());
            y = DrawLine(x, y, width, lineH, body, "Destination", dest);
            y = DrawLine(x, y, width, lineH, body, "Surface", motion.SurfaceId);
            y = DrawLine(x, y, width, lineH, body, "Route",
                motion.ContinuousSurfaceRouteIndex + " / " + motion.ContinuousSurfaceRoute.Count);
            y = DrawLine(x, y, width, lineH, body, "WorldPos", motion.WorldPosition.ToString());
            y = DrawLine(x, y, width, lineH, body, "LocalMapId", localMap);
            y = DrawLine(x, y, width, lineH, body, "LocalPos", localPos);
            return y;
        }

        static float DrawLine(float x, float y, float width, float lineH, GUIStyle body, string key, string value)
        {
            GUI.Label(new Rect(x, y, 100f, lineH), key, body);
            GUI.Label(new Rect(x + 102f, y, width - 102f, lineH), value ?? "-", body);
            return y + lineH;
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
                var name = string.IsNullOrEmpty(entity.DisplayName)
                    ? "Id=" + entity.Id.Value
                    : entity.DisplayName;
                _characters.Add(new CharacterEntry
                {
                    Id = entity.Id,
                    DropdownLabel = name + " / " + authority,
                });
            }

            _characters.Sort((a, b) => string.CompareOrdinal(a.DropdownLabel, b.DropdownLabel));
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
            if (_characters.Count == 0)
            {
                _selectedCharacterId = EntityId.None;
                _selectedCharacterIndex = 0;
                return;
            }

            if (_selectedCharacterIndex >= 0 && _selectedCharacterIndex < _characters.Count &&
                _characters[_selectedCharacterIndex].Id == _selectedCharacterId)
                return;

            for (var i = 0; i < _characters.Count; i++)
            {
                if (_characters[i].Id == _selectedCharacterId)
                {
                    _selectedCharacterIndex = i;
                    return;
                }
            }

            _selectedCharacterIndex = 0;
            _selectedCharacterId = _characters[0].Id;
        }

        void EnsureSelectedSiteIndex()
        {
            if (_sites.Count == 0)
            {
                _selectedSiteIndex = 0;
                return;
            }

            _selectedSiteIndex = Mathf.Clamp(_selectedSiteIndex, 0, _sites.Count - 1);
        }

        string GetSelectedCharacterLabel()
        {
            if (_characters.Count == 0 || _selectedCharacterIndex < 0 || _selectedCharacterIndex >= _characters.Count)
                return "（选择角色）";
            return _characters[_selectedCharacterIndex].DropdownLabel;
        }

        string GetSelectedSiteLabel()
        {
            if (_sites.Count == 0)
                return "（无地点）";
            return _sites[Mathf.Clamp(_selectedSiteIndex, 0, _sites.Count - 1)].DisplayName;
        }

        string GetSelectedSiteId()
        {
            if (_sites.Count == 0)
                return string.Empty;
            return _sites[Mathf.Clamp(_selectedSiteIndex, 0, _sites.Count - 1)].SiteId;
        }

        bool TryGetTravelEligibility(SimulationWorld world, PlayerPartyRuntime party, out string reason)
        {
            reason = string.Empty;
            if (_selectedCharacterId.IsNone)
            {
                reason = "不可行：未选择角色。";
                return false;
            }

            if (CharacterWorldMovementAuthorityQuery.CanStartBackgroundTravelDebug(
                    world, _selectedCharacterId, party, out var err))
                return true;

            reason = string.IsNullOrEmpty(err) ? "不可行。" : err;
            return false;
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

        void TravelToSite(PlayableHostBootstrap bootstrap)
        {
            var session = bootstrap?.Session;
            var siteId = GetSelectedSiteId();
            if (session == null || _selectedCharacterId.IsNone || string.IsNullOrEmpty(siteId))
            {
                _sectionStatus = "请选择角色和地点。";
                return;
            }

            var result = BackgroundCharacterTravelService.BeginTravelToWorldSite(
                session.World,
                _selectedCharacterId,
                siteId,
                session.PlayerParty,
                debugOverrideLocalOccupant: true);
            _sectionStatus = result.IsSuccess
                ? "成功：前往地点 -> " + ResolveSiteDisplayName(session.World, siteId)
                : "失败：" + result.Error;
            if (result.IsSuccess)
                bootstrap.FlushLoadedDestinationArrivals();
        }

        void CancelTravel(PlayableHostBootstrap bootstrap)
        {
            var session = bootstrap?.Session;
            if (session == null || _selectedCharacterId.IsNone)
            {
                _sectionStatus = "请选择角色。";
                return;
            }

            var result = BackgroundCharacterTravelService.CancelTravel(session.World, _selectedCharacterId);
            _sectionStatus = result.IsSuccess ? "成功：已取消旅行。" : "失败：" + result.Error;
        }
    }
}
