using System;
using System.Diagnostics;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Local Combat casualty handoff。角色进入 residual life state（Incapacitated / visible Corpse）后，
    /// 把自己的 WorldPresence 固定到倒下位置，不建立第二套 residual 数据。
    /// 规则：任何角色一旦 Incapacitated / visible Corpse，即停止跟随其原移动 owner，并在倒下的
    /// 真实位置获得稳定 WorldPresence；LocalMap 离开/重进只按该 authority 重建。
    /// 不改 Lifecycle；不 TryRemoveMember；不写 PresentationOverride。
    /// </summary>
    public static class LocalCombatCasualtyHandoffService
    {
        /// <summary>
        /// 尝试把 defeated residual 角色钉到当前 Loaded LocalMap 对应的真实物理位置。
        /// 返回 true 表示本 service 接管并完成 presence 收口（可能无需实际写 —— 已在该 hex）。
        /// WorldSite 无 local point 可用时不再用主控位置派生 —— 明确失败（由调用方回退 hex-only）。
        /// </summary>
        public static bool TryHandleResidualDefeat(
            SimulationWorld world,
            EntityId characterId)
        {
            if (world?.Strategic == null || characterId.IsNone ||
                !ResidualCharacterPresenceService.IsResidualLifeCandidate(world, characterId))
                return false;

            // Idempotent delayed/repeated event: an existing personal authority is already a
            // successful spatial result. Never replace it with the player's loaded-map context.
            if (ResidualSpatialAuthorityService.TryResolveStableResidualSpatialAuthority(
                    world, characterId, out _))
                return true;

            // 只有当前 Host 正停留某个 Surface LocalMap 时才有 Local Combat 语义；
            // 纯 WorldMap 战略态（无 loaded surface）由其它路径负责，这里不猜。
            if (!LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap(world, out var context))
                return false;

            // A loaded Wilderness hex is a player/Host fact, not this character's position.
            // Legacy fallback is legal only when the character's own presence proves membership.
            if (!LoadedLocalMapBelongingQuery.DoesWorldLocationBelongToLoadedLocalMap(
                    world, characterId, out context))
                return false;

            // Hex-only fallback（无 EntityView local point）：仅 Wilderness 可用（Context Hex 即权威）；
            // WorldSite multi-hex 下无法从"没有 local point"推出角色自己的 footprint hex ——
            // 不再用主控 WorldPosition 派生（那会把 Follower 的 residual hex 按主控位置决定）。
            if (ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world) ||
                context.Kind != LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WildernessHex)
                return false;
            if (!world.HexWorld.Contains(context.WildernessHex))
                return false;

            ResidualCharacterPresenceService.PlaceLegacyCharacterAtResidualHex(
                world, characterId, context.WildernessHex);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogHandoff(world, characterId, context, context.WildernessHex, true, null);
#endif
            return true;
        }

        /// <summary>
        /// 带精确 local point 的版本：Host 在倒下瞬间从 EntityView 捕获真实 Local Position
        /// （不能读 PresentationOverride，可能 stale），连同当前 surface bounds 传入；
        /// 经当前 Surface mapping 转成 precise WorldPosition，随 ResidualHex 一起保存 ——
        /// 重新进入 LocalMap 时从该 precise 位置反向映射回来，而不是 Hex 中心 + formation offset。
        /// Wilderness：ResidualHex = Context WildernessHex（不重新 WorldToHex）；
        /// WorldSite：用角色自己的 localX/localZ 映射，derived hex 必须 OccupiesHex。
        /// </summary>
        public static bool TryHandleResidualDefeat(
            SimulationWorld world,
            EntityId characterId,
            float localX,
            float localZ,
            WildernessLocalWorldProjection.WildernessLocalMapBounds? wildernessBounds,
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds? siteBounds)
        {
            if (world?.Strategic == null || characterId.IsNone ||
                !ResidualCharacterPresenceService.IsResidualLifeCandidate(world, characterId))
                return false;

            return TryPlacePreciseResidualFromLoadedLocalPosition(
                world,
                characterId,
                localX,
                localZ,
                wildernessBounds,
                siteBounds);
        }

        /// <summary>
        /// 把 residual 角色自己的当前 LocalMap 精确落点写回 WorldPresence。
        /// 这里不修改 Squad membership；现代 Continuous 统一为 AtWorldPosition。
        /// </summary>
        public static bool TryPlacePreciseResidualFromLoadedLocalPosition(
            SimulationWorld world,
            EntityId characterId,
            float localX,
            float localZ,
            WildernessLocalWorldProjection.WildernessLocalMapBounds? wildernessBounds,
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds? siteBounds)
        {
            if (world?.Strategic == null || characterId.IsNone ||
                !ResidualCharacterPresenceService.IsResidualLifeCandidate(world, characterId))
                return false;

            if (!LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap(world, out var context))
                return false;

            if (!LoadedLocalMapBelongingQuery.DoesWorldLocationBelongToLoadedLocalMap(
                    world, characterId, out context))
                return false;

            world.WorldPresence.TryGet(characterId, out var priorPresence);
            var surfaceId = priorPresence?.PersonalSurfaceId ?? string.Empty;

            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;

            switch (context.Kind)
            {
                case LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WildernessHex:
                {
                    if (!world.HexWorld.Contains(context.WildernessHex))
                        return false;
                    var hex = context.WildernessHex;
                    WorldVec2 precise;
                    if (!wildernessBounds.HasValue ||
                        wildernessBounds.Value.MaxX <= wildernessBounds.Value.MinX ||
                        wildernessBounds.Value.MaxY <= wildernessBounds.Value.MinY ||
                        !WildernessLocalWorldProjection.TryProjectLocalToWorld(
                            hex,
                            localX,
                            localZ,
                            wildernessBounds.Value,
                            hexSize,
                            out precise))
                    {
                        // 无精确 bounds → hex-only（Context Hex 仍是权威）。
                        if (ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world))
                            return false;
                        ResidualCharacterPresenceService.PlaceLegacyCharacterAtResidualHex(world, characterId, hex);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        LogHandoff(world, characterId, context, hex, true, null);
#endif
                        return true;
                    }

                    if (!ResidualSpatialAuthorityService.TryFreezeAtPreciseWorldPosition(
                            world, characterId, precise, surfaceId))
                        return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    LogHandoff(world, characterId, context, hex, true, precise);
#endif
                    return true;
                }

                case LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WorldSite:
                {
                    if (context.Site == null ||
                        !siteBounds.HasValue ||
                        !siteBounds.Value.IsValid)
                        return false;
                    if (!WorldSiteSpatialMapping.TryLocalToWorldSurface(
                            context.Site,
                            siteBounds.Value,
                            new WorldVec2(localX, localZ),
                            hexSize,
                            out var precise))
                        return false;

                    var derived = HexMath.WorldToHex(precise.X, precise.Y, hexSize);
                    if (!context.Site.OccupiesHex(derived))
                    {
                        // footprint 边界数值歧义：近邻格；仍不属于 Site → 明确失败（不猜 Anchor）。
                        if (!TryResolveNeighborFootprintHex(context.Site, precise, hexSize, out derived))
                            return false;
                    }

                    if (!ResidualSpatialAuthorityService.TryFreezeAtPreciseWorldPosition(
                            world, characterId, precise, surfaceId))
                        return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    LogHandoff(world, characterId, context, derived, true, precise);
#endif
                    return true;
                }

                default:
                    return false;
            }
        }

        /// <summary>
        /// Boundary 数值误差落到 footprint 邻格时，在相邻 hex 中找唯一属于该 Site 的格。
        /// 多个相邻格同属 Site（multi-hex footprint）时取最近者；仍无 → false。
        /// </summary>
        static bool TryResolveNeighborFootprintHex(
            WorldSite site,
            WorldVec2 worldPos,
            float hexSize,
            out HexCoord hex)
        {
            hex = default;
            if (site == null)
                return false;

            var center = HexMath.WorldToHex(worldPos.X, worldPos.Y, hexSize);
            HexCoord? best = null;
            float bestDist = float.MaxValue;
            for (var d = 0; d < 6; d++)
            {
                var neighbor = HexMath.Neighbor(center, d);
                if (!site.OccupiesHex(neighbor))
                    continue;
                HexMath.ToWorldPosition(neighbor, hexSize, out var nx, out var ny);
                var dist = Math.Abs(nx - worldPos.X) + Math.Abs(ny - worldPos.Y);
                if (dist >= bestDist)
                    continue;
                bestDist = dist;
                best = neighbor;
            }

            if (!best.HasValue)
                return false;
            hex = best.Value;
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static void LogHandoff(
            SimulationWorld world,
            EntityId characterId,
            LoadedLocalMapBelongingQuery.LoadedLocalMapContext context,
            HexCoord residualHex,
            bool changed,
            WorldVec2? precise)
        {
            var name = characterId.ToString();
            var lifeState = "(entity missing)";
            var armyId = "(none)";
            if (world.Entities.TryGet(characterId, out var entity) && entity != null)
            {
                name = string.IsNullOrEmpty(entity.DisplayName) ? characterId.ToString() : entity.DisplayName;
                lifeState = CombatLifeStateService.ResolveLifeStateLabel(entity);
            }

            if (CharacterStrategicQuery.TryGetSquad(world, characterId, out var squad) && squad != null)
                armyId = squad.SquadId;

            var presenceMode = "(none)";
            var presenceSiteId = string.Empty;
            var presenceHex = "(none)";
            if (world.WorldPresence.TryGet(characterId, out var wp) && wp != null)
            {
                presenceMode = wp.Mode.ToString();
                presenceSiteId = wp.SiteId ?? string.Empty;
                if (wp.UsesHexPresence)
                    presenceHex = wp.ResidualHex.ToString();
            }

            var traveling = false;
            if (world.PlayerPartyTravel != null)
            {
                for (var i = 0; i < world.PlayerPartyTravel.TravelingMembers.Count; i++)
                {
                    if (world.PlayerPartyTravel.TravelingMembers[i] == characterId)
                    {
                        traveling = true;
                        break;
                    }
                }
            }

            Debug.WriteLine(
                "[LocalCombatCasualtyHandoff]" +
                " EntityId=" + characterId +
                " Name=" + name +
                " LifeState=" + lifeState +
                " SquadId=" + armyId +
                " SurfaceKind=" + context.Kind +
                " SurfaceSiteId=" + (context.Site != null ? context.Site.SiteId : string.Empty) +
                " SurfaceWildernessHex=" + context.WildernessHex +
                " OldWorldPresenceMode=" + presenceMode +
                " OldWorldPresenceSiteId=" + presenceSiteId +
                " OldWorldPresenceHex=" + presenceHex +
                " IsTravelingMember=" + traveling +
                " ResidualHex=" + residualHex +
                " HasPrecise=" + (precise.HasValue) +
                " PreciseWorld=" + (precise.HasValue ? precise.Value.ToString() : "(none)") +
                " Changed=" + changed);
        }
#endif
    }
}
