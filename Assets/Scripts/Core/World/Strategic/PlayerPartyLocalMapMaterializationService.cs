using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// World Position → Resolve LocalMap 之后：把当前 PlayerParty Materialize 到该次 LocalMap 表现。
    /// Site / Wilderness 共用；不创建 Character／Army，不重置 Party Membership／Active。
    /// </summary>
    public static class PlayerPartyLocalMapMaterializationService
    {
        /// <summary>
        /// 将 party 成员落到当前 PartyWorld.LocalMapId 对应的近景（startLocation 或原点）。
        /// 调用前须已设置 PartyWorld.LocalMapId（以及 AtSite／AtHex WorldPresence）。
        /// </summary>
        public static void MaterializePartyOnResolvedLocalMap(
            SimulationWorld world,
            IReadOnlyList<EntityId> partyMembers)
        {
            if (world?.LocalMap == null || partyMembers == null || partyMembers.Count == 0)
                return;

            var mapId = world.PartyWorld?.LocalMapId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(mapId))
                return;

            world.LocalMap.ActiveMapLayoutId = mapId;
            world.LocalMap.OverworldMapLayoutId = mapId;

            var startId = world.WorldRegion != null ? world.WorldRegion.StartLocationId : string.Empty;
            WorldLocationState startLoc = null;
            var hasStart = !string.IsNullOrEmpty(startId) &&
                           world.WorldRegion != null &&
                           world.WorldRegion.TryGet(startId, out startLoc) &&
                           startLoc != null;
            var px = hasStart ? startLoc.PresentationX : 0f;
            var pz = hasStart ? startLoc.PresentationZ : 0f;

            for (var i = 0; i < partyMembers.Count; i++)
            {
                var id = partyMembers[i];
                if (id.IsNone)
                    continue;

                world.LocalMap.AddOccupant(id);

                if (!world.Entities.TryGet(id, out var ent) || ent == null)
                    continue;

                if (!ent.TryGet<EntityLocationComponent>(out var loc) || loc == null)
                {
                    loc = new EntityLocationComponent();
                    ent.AddComponent(loc);
                }

                loc.LocationId = hasStart ? startId : string.Empty;
                loc.SetPresentationOverride(px, pz);
            }

            // 保持 TravelingMembers 与当前展开队伍对齐，供可见性／后续 Close→Expand 复用。
            if (world.PlayerPartyTravel != null)
                world.PlayerPartyTravel.CaptureTravelingMembers(partyMembers);
        }

        /// <summary>
        /// Wilderness／Hex 展开近景：AtHex 成员是否应显示在当前 Active LocalMap。
        /// </summary>
        public static bool IsWildernessPartyMemberVisibleOnActiveLocalMap(
            SimulationWorld world,
            EntityId characterId,
            WorldAgentPresence presence)
        {
            if (world?.LocalMap == null || characterId.IsNone || presence == null)
                return false;
            if (presence.Mode != PartyWorldPresenceMode.AtHex || !presence.UsesHexPresence)
                return false;

            var activeMap = world.LocalMap.ActiveMapLayoutId?.Trim() ?? string.Empty;
            var focusMap = world.PartyWorld?.LocalMapId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(activeMap) || string.IsNullOrEmpty(focusMap))
                return false;
            if (!string.Equals(activeMap, focusMap, System.StringComparison.Ordinal))
                return false;

            // Wilderness expand：焦点不是 WorldSite（SiteId 空）。
            if (!string.IsNullOrEmpty(world.PartyWorld.SiteId))
                return false;

            var motion = world.PlayerPartyTravel;
            if (motion == null || !motion.HasPosition)
                return false;
            if (presence.ResidualHex != motion.CurrentHex)
                return false;

            var members = motion.TravelingMembers;
            if (members != null && members.Count > 0)
            {
                for (var i = 0; i < members.Count; i++)
                {
                    if (members[i] == characterId)
                        return true;
                }

                return false;
            }

            return world.LocalMap.ContainsOccupant(characterId);
        }

        /// <summary>
        /// 当前展开是否为 Wilderness Fallback（非 Site 焦点）。
        /// </summary>
        public static bool IsWildernessLocalExpand(SimulationWorld world)
        {
            if (world?.PartyWorld == null)
                return false;
            if (string.IsNullOrWhiteSpace(world.PartyWorld.LocalMapId))
                return false;
            return string.IsNullOrEmpty(world.PartyWorld.SiteId);
        }
    }
}
