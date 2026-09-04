using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 5S-B2-3.1：World → LocalMap 正常人口桥。
    /// 只负责「当前已 Loaded 的 surface LocalMap」中两类战略人口的 materialize / dematerialize：
    ///   1) FormalArmy living members（位置真源 = FormalArmy.WorldMotion，绝不用 member.WorldPresence 猜）；
    ///   2) Strategic Residual（Incapacitated / visible Corpse，非 FormalArmy；位置真源 = ResidualHex）。
    /// 不创建 clone、不加入 PlayerParty、不修改 WorldMotion / WorldPresence world authority、
    /// 不触碰 PlayerParty 与正常 authored NPC / Background Character 归属。
    /// Battle Encounter / ParticipantSnapshot / BattlefieldSpawnScope 不参与本 service 判定——
    /// 战斗结束后实体继续作为普通 LocalMap population 显示。
    /// </summary>
    public static class LoadedStrategicPopulationMaterializer
    {
        const float FormationSpacing = 1.25f;
        const float BoundsInset = 0.4f;

        public struct ReconcileResult
        {
            public int AddedCount;
            public int RemovedCount;
            public int ScannedCount;

            public bool Changed => AddedCount > 0 || RemovedCount > 0;
        }

        /// <summary>
        /// 对当前 Loaded surface LocalMap 上的 FormalArmy / Residual 战略人口做一次 reconcile。
        /// 返回 Added / Removed / Scanned 计数；Changed 时调用方再刷新视图（不每帧无条件重建）。
        /// Explicit EncounterMap（Dedicated Encounter）不参与——那是特殊遭遇图，不得混入普通战略人口。
        /// </summary>
        public static ReconcileResult ReconcileLoadedStrategicPopulation(
            SimulationWorld world,
            PlayerPartyRuntime playerParty,
            WildernessLocalWorldProjection.WildernessLocalMapBounds? wildernessBounds,
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds? siteBounds)
        {
            var result = default(ReconcileResult);
            if (world?.Strategic == null || world.LocalMap == null || world.WorldPresence == null)
                return result;

            var activeMap = world.LocalMap.ActiveMapLayoutId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(activeMap) ||
                (BattleOfferService.HasActiveManualEncounter(world) &&
                 string.Equals(
                     activeMap,
                     StrategicEncounterCatalog.DefaultEncounterLocalMapId,
                     System.StringComparison.Ordinal)))
                return result;

            if (!LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap(world, out var context))
                return result;

            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;

            // 1) FormalArmy living members（稳定顺序 = MemberCharacterIds）
            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null || !army.WorldMotion.HasPosition)
                    continue;

                for (var i = 0; i < army.MemberCharacterIds.Count; i++)
                {
                    var memberId = new EntityId(army.MemberCharacterIds[i]);
                    if (memberId.IsNone ||
                        (playerParty != null && playerParty.IsMember(memberId)))
                        continue;
                    // 非 living 的 Army member 由战后退役（DetachNonLivingMembersAtBattlefield）
                    // 转为 Residual candidate，走下方 residual 路径。
                    if (!LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, memberId))
                        continue;

                    if (BelongsArmyToLoadedMap(world, context, army))
                    {
                        if (MaterializeArmyMember(
                                world, memberId, army, context,
                                wildernessBounds, siteBounds, hexSize, slot: i))
                            result.AddedCount++;
                    }
                    else
                    {
                        if (ReleaseManagedEntity(world, memberId))
                            result.RemovedCount++;
                    }

                    result.ScannedCount++;
                }
            }

            // 2) Strategic Residual（非 FormalArmy 的 incapacitated / visible corpse）
            foreach (var kv in world.WorldPresence.All)
            {
                var id = new EntityId(kv.Key);
                if (id.IsNone)
                    continue;
                // Living / transitionable PlayerParty member 由 PlayerParty materializer 管；
                // 弥留/尸体的 party member（ShouldMemberTransitionWithParty == false）不属于随队
                // 旅行者 → 若满足 residual candidate（WorldPresence.AtHex 倒下格）则允许走本
                // service 重新生成（主控回到其倒下 Site/Hex 时正确显示，不 double materialize ——
                // 活着的 party member 不在 WorldPresence.AtHex residual 扫描中冲突，因为 party
                // materializer 生成的是 occupant，且本 loop 已排除 transitionable member）。
                if (playerParty != null &&
                    playerParty.IsMember(id) &&
                    PlayerPartyTransitionMembership.ShouldMemberTransitionWithParty(
                        world, playerParty, id))
                    continue;
                if (ArmyService.TryGetArmyForCharacter(world, id, out _))
                    continue; // Army member 不在此列（避免 IsStrategicResidualCandidate 的 assert）

                if (!StrategicResidualPresenceService.IsStrategicResidualCandidate(world, id))
                    continue;

                if (BelongsResidualToLoadedMap(world, context, id))
                {
                    if (MaterializeResidual(world, id, context, wildernessBounds, siteBounds, hexSize))
                        result.AddedCount++;
                }
                else
                {
                    if (ReleaseManagedEntity(world, id))
                        result.RemovedCount++;
                }

                result.ScannedCount++;
            }

            return result;
        }

        /// <summary>FormalArmy 是否物理属于当前 Loaded LocalMap（WildernessHex 相等 / WorldSite footprint 含 WorldMotion.CurrentHex）。</summary>
        public static bool BelongsArmyToLoadedMap(
            SimulationWorld world,
            LoadedLocalMapBelongingQuery.LoadedLocalMapContext context,
            FormalArmy army)
        {
            if (world == null || army == null || !army.WorldMotion.HasPosition)
                return false;

            switch (context.Kind)
            {
                case LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WildernessHex:
                    return army.WorldMotion.CurrentHex.Equals(context.WildernessHex);

                case LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WorldSite:
                    return context.Site != null && context.Site.OccupiesHex(army.WorldMotion.CurrentHex);

                default:
                    return false;
            }
        }

        /// <summary>Residual 是否物理属于当前 Loaded LocalMap（ResidualHex 相等 / footprint 含 ResidualHex）。</summary>
        public static bool BelongsResidualToLoadedMap(
            SimulationWorld world,
            LoadedLocalMapBelongingQuery.LoadedLocalMapContext context,
            EntityId characterId)
        {
            if (world == null || characterId.IsNone)
                return false;
            if (!StrategicResidualPresenceService.TryGetResidualHex(world, characterId, out var hex))
                return false;

            switch (context.Kind)
            {
                case LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WildernessHex:
                    return hex.Equals(context.WildernessHex);

                case LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WorldSite:
                    return context.Site != null && context.Site.OccupiesHex(hex);

                default:
                    return false;
            }
        }

        static bool MaterializeArmyMember(
            SimulationWorld world,
            EntityId memberId,
            FormalArmy army,
            LoadedLocalMapBelongingQuery.LoadedLocalMapContext context,
            WildernessLocalWorldProjection.WildernessLocalMapBounds? wildernessBounds,
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds? siteBounds,
            float hexSize,
            int slot)
        {
            if (world.LocalMap.ContainsOccupant(memberId))
                return false;

            world.LocalMap.AddOccupant(memberId);
            if (!world.Entities.TryGet(memberId, out var entity) || entity == null)
                return true;

            if (!entity.TryGet<EntityLocationComponent>(out var loc) || loc == null)
            {
                loc = new EntityLocationComponent();
                entity.AddComponent(loc);
            }

            // 已有有效落点（如战斗刚结束保留的真实战斗位置）：不覆盖、不 teleport。
            if (loc.HasPresentationOverride)
                return true;

            if (!TryResolveArmyLocalPlacement(
                    world, army, context, wildernessBounds, siteBounds, hexSize, slot,
                    out var lx, out var ly))
                return true;

            loc.SetPresentationOverride(lx, ly);
            return true;
        }

        static bool MaterializeResidual(
            SimulationWorld world,
            EntityId characterId,
            LoadedLocalMapBelongingQuery.LoadedLocalMapContext context,
            WildernessLocalWorldProjection.WildernessLocalMapBounds? wildernessBounds,
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds? siteBounds,
            float hexSize)
        {
            if (world.LocalMap.ContainsOccupant(characterId))
                return false;

            world.LocalMap.AddOccupant(characterId);
            if (!world.Entities.TryGet(characterId, out var entity) || entity == null)
                return true;

            if (!entity.TryGet<EntityLocationComponent>(out var loc) || loc == null)
            {
                loc = new EntityLocationComponent();
                entity.AddComponent(loc);
            }

            // 刚结束战斗时已有 battle PresentationOverride：保留，不 teleport corpse。
            if (loc.HasPresentationOverride)
                return true;

            if (!world.WorldPresence.TryGet(characterId, out var presence) || presence == null)
                return true;
            if (!presence.UsesHexPresence)
                return true;
            if (!TryResolveResidualLocalPlacement(
                    world,
                    characterId,
                    presence,
                    context,
                    wildernessBounds,
                    siteBounds,
                    hexSize,
                    out var lx,
                    out var ly))
                return true;

            loc.SetPresentationOverride(lx, ly);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            System.Diagnostics.Debug.WriteLine(
                "[ResidualMaterialize]" +
                " Entity=" + characterId +
                " PlacementSource=" + (presence.HasContinuousWorldPosition
                    ? "PreciseWorldPosition"
                    : "HexFallback"));
#endif
            return true;
        }

        /// <summary>
        /// 释放本 service 管理的实体表现（RemoveOccupant + 清 override）。
        /// 只清表现，绝对不修改 WorldMotion / WorldPresence / Army ownership。
        /// </summary>
        static bool ReleaseManagedEntity(SimulationWorld world, EntityId id)
        {
            var changed = false;
            if (world.LocalMap.ContainsOccupant(id))
            {
                world.LocalMap.RemoveOccupant(id);
                changed = true;
            }

            if (world.Entities.TryGet(id, out var entity) && entity != null &&
                entity.TryGet<EntityLocationComponent>(out var loc) && loc != null &&
                loc.HasPresentationOverride)
            {
                loc.HasPresentationOverride = false;
                loc.PresentationOverrideX = 0f;
                loc.PresentationOverrideZ = 0f;
                changed = true;
            }

            return changed;
        }

        static bool TryResolveArmyLocalPlacement(
            SimulationWorld world,
            FormalArmy army,
            LoadedLocalMapBelongingQuery.LoadedLocalMapContext context,
            WildernessLocalWorldProjection.WildernessLocalMapBounds? wildernessBounds,
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds? siteBounds,
            float hexSize,
            int slot,
            out float localX,
            out float localY)
        {
            localX = 0f;
            localY = 0f;
            var motion = army.WorldMotion;

            switch (context.Kind)
            {
                case LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WildernessHex:
                    if (!wildernessBounds.HasValue || !motion.HasPosition)
                        return false;
                    if (!WildernessLocalWorldProjection.TryProjectWorldToLocal(
                            context.WildernessHex,
                            motion.WorldPosition,
                            wildernessBounds.Value,
                            hexSize,
                            out localX,
                            out localY))
                        return false;
                    break;

                case LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WorldSite:
                    if (!siteBounds.HasValue || !siteBounds.Value.IsValid ||
                        context.Site == null || !motion.HasPosition)
                        return false;
                    if (!WorldSiteSpatialMapping.TryWorldSurfaceToLocal(
                            context.Site,
                            siteBounds.Value,
                            motion.WorldPosition,
                            hexSize,
                            out var local))
                        return false;
                    localX = local.X;
                    localY = local.Y;
                    break;

                default:
                    return false;
            }

            ApplyFormationOffset(ref localX, ref localY, slot);
            ClampToBounds(ref localX, ref localY, context, wildernessBounds, siteBounds);
            return true;
        }

        /// <summary>
        /// Residual 角色 Local placement 解析。核心规则：
        /// 1) presence.HasContinuousWorldPosition → 该 precise world position 是倒下瞬间从
        ///    EntityView local 经 surface mapping 得到的真实落点 —— 反向 WorldToLocal 回放，
        ///    <b>绝不 ApplyFormationOffset</b>（真实落点不需要 spread）；仅轻微 bounds safety clamp
        ///    （正常 roundtrip 应无改变）。
        /// 2) 无 precise（老存档 / 旧 Strategic Battle / Auto Battle 只有 BattleHex）→ legacy
        ///    fallback：ResidualHex → Hex center → WorldToLocal → stable formation offset。
        /// </summary>
        static bool TryResolveResidualLocalPlacement(
            SimulationWorld world,
            EntityId characterId,
            WorldAgentPresence presence,
            LoadedLocalMapBelongingQuery.LoadedLocalMapContext context,
            WildernessLocalWorldProjection.WildernessLocalMapBounds? wildernessBounds,
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds? siteBounds,
            float hexSize,
            out float localX,
            out float localY)
        {
            localX = 0f;
            localY = 0f;
            if (presence == null)
                return false;

            if (presence.HasContinuousWorldPosition)
            {
                var precise = presence.ContinuousWorldPosition;
                switch (context.Kind)
                {
                    case LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WildernessHex:
                        if (!wildernessBounds.HasValue)
                            return false;
                        if (!WildernessLocalWorldProjection.TryProjectWorldToLocal(
                                context.WildernessHex,
                                precise,
                                wildernessBounds.Value,
                                hexSize,
                                out localX,
                                out localY))
                            return false;
                        break;

                    case LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WorldSite:
                        if (!siteBounds.HasValue || !siteBounds.Value.IsValid || context.Site == null)
                            return false;
                        if (!WorldSiteSpatialMapping.TryWorldSurfaceToLocal(
                                context.Site,
                                siteBounds.Value,
                                precise,
                                hexSize,
                                out var local))
                            return false;
                        localX = local.X;
                        localY = local.Y;
                        break;

                    default:
                        return false;
                }

                // 真实落点：只做 safety clamp（roundtrip 正常时无改变），不加 formation offset。
                ClampToBounds(ref localX, ref localY, context, wildernessBounds, siteBounds);
                return true;
            }

            // ---- legacy hex-only fallback ----
            var hex = presence.ResidualHex;
            HexMath.ToWorldPosition(hex, hexSize, out var worldX, out var worldY);
            var worldPos = new WorldVec2(worldX, worldY);

            switch (context.Kind)
            {
                case LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WildernessHex:
                    if (!wildernessBounds.HasValue)
                        return false;
                    if (!WildernessLocalWorldProjection.TryProjectWorldToLocal(
                            context.WildernessHex,
                            worldPos,
                            wildernessBounds.Value,
                            hexSize,
                            out localX,
                            out localY))
                        return false;
                    break;

                case LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WorldSite:
                    if (!siteBounds.HasValue || !siteBounds.Value.IsValid || context.Site == null)
                        return false;
                    if (!WorldSiteSpatialMapping.TryWorldSurfaceToLocal(
                            context.Site,
                            siteBounds.Value,
                            worldPos,
                            hexSize,
                            out var local))
                        return false;
                    localX = local.X;
                    localY = local.Y;
                    break;

                default:
                    return false;
            }

            // 尸体 offset 按 EntityId 稳定派生，无随机、不随帧变化（仅 legacy hex-only fallback）。
            ApplyFormationOffset(ref localX, ref localY, (int)(characterId.Value % 17));
            ClampToBounds(ref localX, ref localY, context, wildernessBounds, siteBounds);
            return true;
        }

        static void ApplyFormationOffset(ref float x, ref float y, int slot)
        {
            // 六边形稳定 formation：slot 顺序固定 → 每次一致、不随帧变化。
            var ring = slot / 6;
            var dir = slot % 6;
            var radius = FormationSpacing * (1.0 + ring * 0.85);
            var angle = dir * (Math.PI / 3.0);
            x += (float)(Math.Cos(angle) * radius);
            y += (float)(Math.Sin(angle) * radius);
        }

        static void ClampToBounds(
            ref float x,
            ref float y,
            LoadedLocalMapBelongingQuery.LoadedLocalMapContext context,
            WildernessLocalWorldProjection.WildernessLocalMapBounds? wildernessBounds,
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds? siteBounds)
        {
            if (context.Kind == LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WildernessHex &&
                wildernessBounds.HasValue)
            {
                var b = wildernessBounds.Value;
                x = Clamp(x, b.MinX + BoundsInset, b.MaxX - BoundsInset);
                y = Clamp(y, b.MinY + BoundsInset, b.MaxY - BoundsInset);
                return;
            }

            if (context.Kind == LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WorldSite &&
                siteBounds.HasValue)
            {
                var b = siteBounds.Value;
                x = Clamp(x, b.MinX + BoundsInset, b.MaxX - BoundsInset);
                y = Clamp(y, b.MinY + BoundsInset, b.MaxY - BoundsInset);
            }
        }

        static float Clamp(float v, float min, float max)
        {
            if (v < min)
                return min;
            if (v > max)
                return max;
            return v;
        }
    }
}
