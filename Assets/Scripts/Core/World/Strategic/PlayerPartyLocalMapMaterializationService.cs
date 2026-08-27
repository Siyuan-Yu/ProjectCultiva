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
            IReadOnlyList<EntityId> partyMembers) =>
            MaterializePartyOnResolvedLocalMap(world, partyMembers, null);

        /// <param name="wildernessPlayableBounds">
        /// Wilderness 展开时用于 WorldPosition→Local 投影的可玩矩形；Site 展开可省略。
        /// </param>
        public static void MaterializePartyOnResolvedLocalMap(
            SimulationWorld world,
            IReadOnlyList<EntityId> partyMembers,
            WildernessLocalWorldProjection.WildernessLocalMapBounds? wildernessPlayableBounds)
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

            var motion = world.PlayerPartyTravel;
            var useWildernessProjection = IsWildernessLocalExpand(world) &&
                                          motion != null &&
                                          motion.HasPosition &&
                                          motion.LocationKind == PlayerPartyLocationKind.AtWorldPosition &&
                                          wildernessPlayableBounds.HasValue;
            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;
            if (useWildernessProjection &&
                WildernessLocalWorldProjection.TryProjectWorldToLocal(
                    motion.WorldPosition,
                    wildernessPlayableBounds.Value,
                    hexSize,
                    out var activeLocalX,
                    out var activeLocalY))
            {
                px = activeLocalX;
                pz = activeLocalY;
                hasStart = false;

                // Edge Transition 后：若投影仍落在近缘带，推到 Entry Interior Inset。
                var gate = motion.SurfaceEdgeGate;
                if (gate != null &&
                    (gate.TransitionInProgress || !gate.EdgeArmed) &&
                    gate.LastExitDirection >= 0 &&
                    !WildernessLocalWorldProjection.IsInSafeInterior(
                        px, pz, wildernessPlayableBounds.Value))
                {
                    var currentHex = motion.CurrentHex;
                    var cameFromHex = HexMath.Neighbor(
                        currentHex,
                        WildernessLocalWorldProjection.OppositeDirection(gate.LastExitDirection));
                    var depth = SurfaceExitZoneCalculator.ResolveDepthFromSession(
                        world, wildernessPlayableBounds.Value);
                    WildernessLocalWorldProjection.GetLocalPositionNearEdge(
                        wildernessPlayableBounds.Value,
                        currentHex,
                        cameFromHex,
                        hexSize,
                        depth,
                        out px,
                        out pz);
                }
            }
            else
            {
                useWildernessProjection = false;
            }

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

                var memberX = px;
                var memberZ = pz;
                if (useWildernessProjection)
                    ApplyFollowerPresentationOffset(i, ref memberX, ref memberZ);

                loc.LocationId = hasStart ? startId : string.Empty;
                loc.SetPresentationOverride(memberX, memberZ);

                var isFollower = i > 0;
                PlayerPartyTransitionMembership.LogMaterializeMember(
                    id,
                    mapId,
                    spawned: true,
                    followReboundHint: isFollower);
            }

            // 保持 TravelingMembers 与当前展开队伍对齐，供可见性／后续 Close→Expand 复用。
            if (world.PlayerPartyTravel != null)
                world.PlayerPartyTravel.CaptureTravelingMembers(partyMembers);

            // Edge Gate：Materialize 完成后 Disarm（不改 WorldPosition）。
            if (useWildernessProjection &&
                wildernessPlayableBounds.HasValue &&
                world.PlayerPartyTravel?.SurfaceEdgeGate != null &&
                world.PlayerPartyTravel.SurfaceEdgeGate.TransitionInProgress)
            {
                PlayerPartyWildernessTransitionService.CompleteEdgeTransitionPresentation(
                    world, wildernessPlayableBounds.Value, px, pz);
            }
            else if (world.PlayerPartyTravel?.SurfaceEdgeGate != null &&
                     world.PlayerPartyTravel.SurfaceEdgeGate.TransitionInProgress)
            {
                // Site 展开：用 start / 原点完成 Gate。
                PlayerPartyWildernessTransitionService.CompleteEdgeTransitionPresentation(
                    world,
                    WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                        -20f, -20f, 1f, 40, 40),
                    px,
                    pz);
            }
        }

        /// <summary>
        /// Active 在当前已展开 LocalMap 上应恰好有一份 presentation，且落点在可玩矩形内。
        /// </summary>
        public static bool TryAssertActiveMaterializedOnce(
            SimulationWorld world,
            EntityId activeId,
            WildernessLocalWorldProjection.WildernessLocalMapBounds? playableBounds,
            out string error)
        {
            error = string.Empty;
            if (world?.LocalMap == null || activeId.IsNone)
            {
                error = "Invalid world/active.";
                return false;
            }

            var loaded = world.LocalMap.ActiveMapLayoutId?.Trim() ?? string.Empty;
            var focus = world.PartyWorld?.LocalMapId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(loaded) || !string.Equals(loaded, focus, System.StringComparison.Ordinal))
            {
                error = "Active LocalMapId mismatch: loaded=" + loaded + " focus=" + focus;
                return false;
            }

            if (!world.LocalMap.ContainsOccupant(activeId))
            {
                error = "Active not occupant of resolved LocalMap.";
                return false;
            }

            if (!world.Entities.TryGet(activeId, out var ent) ||
                ent == null ||
                !ent.TryGet<EntityLocationComponent>(out var loc) ||
                loc == null ||
                !loc.HasPresentationOverride)
            {
                error = "Active missing LocalMap presentation override.";
                return false;
            }

            if (playableBounds.HasValue)
            {
                var b = playableBounds.Value;
                var x = loc.PresentationOverrideX;
                var z = loc.PresentationOverrideZ;
                if (x < b.MinX - 0.01f || x > b.MaxX + 0.01f ||
                    z < b.MinY - 0.01f || z > b.MaxY + 0.01f)
                {
                    error = "Active presentation outside playable bounds.";
                    return false;
                }
            }

            return true;
        }

        static void ApplyFollowerPresentationOffset(int memberIndex, ref float x, ref float z)
        {
            if (memberIndex == 0)
                return;
            switch (memberIndex % 4)
            {
                case 1:
                    x -= 0.9f;
                    break;
                case 2:
                    x += 0.9f;
                    break;
                case 3:
                    z -= 0.9f;
                    break;
                default:
                    z += 0.9f;
                    break;
            }
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
