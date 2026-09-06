using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Background Character 在玩家已 Loaded 的目标 LocalMap 内 Materialize（Initial Load + Runtime Arrival 共用核心）。
    /// </summary>
    public static class LoadedDestinationArrivalMaterializer
    {
        public enum LoadedLocalMapMaterializationTrigger
        {
            InitialLoad = 0,
            RuntimeArrival = 1,
        }

        public readonly struct LoadedLocalMapMaterializationRequest
        {
            public LoadedLocalMapMaterializationRequest(
                LoadedLocalMapMaterializationTrigger trigger,
                HexCoord? ingressFromHex,
                BackgroundTravelArrivalContext? arrivalContext,
                WildernessLocalWorldProjection.WildernessLocalMapBounds? explicitBounds,
                bool stopBackgroundTravel)
            {
                Trigger = trigger;
                IngressFromHex = ingressFromHex;
                ArrivalContext = arrivalContext;
                ExplicitBounds = explicitBounds;
                StopBackgroundTravel = stopBackgroundTravel;
            }

            public LoadedLocalMapMaterializationTrigger Trigger { get; }
            public HexCoord? IngressFromHex { get; }
            public BackgroundTravelArrivalContext? ArrivalContext { get; }
            public WildernessLocalWorldProjection.WildernessLocalMapBounds? ExplicitBounds { get; }
            public bool StopBackgroundTravel { get; }

            public static LoadedLocalMapMaterializationRequest ForInitialLoad(
                WildernessLocalWorldProjection.WildernessLocalMapBounds bounds) =>
                new LoadedLocalMapMaterializationRequest(
                    LoadedLocalMapMaterializationTrigger.InitialLoad,
                    null,
                    null,
                    bounds,
                    stopBackgroundTravel: false);

            public static LoadedLocalMapMaterializationRequest ForRuntimeArrival(
                bool stopBackgroundTravel) =>
                new LoadedLocalMapMaterializationRequest(
                    LoadedLocalMapMaterializationTrigger.RuntimeArrival,
                    null,
                    null,
                    null,
                    stopBackgroundTravel);

            public static LoadedLocalMapMaterializationRequest ForRuntimeHexEntry(
                HexCoord ingressFromHex,
                bool stopBackgroundTravel) =>
                new LoadedLocalMapMaterializationRequest(
                    LoadedLocalMapMaterializationTrigger.RuntimeArrival,
                    ingressFromHex,
                    null,
                    null,
                    stopBackgroundTravel);

            public static LoadedLocalMapMaterializationRequest ForRuntimeSiteArrival(
                in BackgroundTravelArrivalContext arrivalContext) =>
                new LoadedLocalMapMaterializationRequest(
                    LoadedLocalMapMaterializationTrigger.RuntimeArrival,
                    null,
                    arrivalContext,
                    null,
                    stopBackgroundTravel: false);
        }

        static readonly List<SurfaceExitConnection> ConnectionScratch = new List<SurfaceExitConnection>(16);
        static readonly List<EntityId> PendingPresentationIds = new List<EntityId>(8);
        static readonly List<EntityId> ReleaseScratch = new List<EntityId>(16);

        public static IReadOnlyList<EntityId> PendingPresentationFlush => PendingPresentationIds;

        public static void ClearPendingPresentationFlush() => PendingPresentationIds.Clear();

        /// <summary>
        /// Initial Load + Runtime Arrival 共用 Materialization 核心。
        /// </summary>
        public static bool TryMaterializeCharacterIntoLoadedLocalMap(
            SimulationWorld world,
            EntityId characterId,
            PlayerPartyRuntime party,
            in LoadedLocalMapMaterializationRequest request)
        {
            var previousLocation = string.Empty;
            if (world?.WorldPresence != null &&
                world.WorldPresence.TryGet(characterId, out var previousPresence) &&
                previousPresence != null)
                previousLocation = BackgroundLoadedLocalMapArrivalDebug.DescribePresence(previousPresence);

            CharacterWorldMovementAuthorityQuery.TryGetAuthority(
                world,
                characterId,
                party,
                out var authorityBefore);

            var materializeRequested = request.Trigger == LoadedLocalMapMaterializationTrigger.RuntimeArrival;
            var activeTrace = default(BackgroundLoadedLocalMapArrivalDebug.ActiveSideEffectTrace);
            if (materializeRequested)
            {
                activeTrace = BackgroundLoadedLocalMapArrivalDebug.CaptureActiveSideEffectTrace(
                    world,
                    characterId,
                    party,
                    nameof(TryMaterializeCharacterIntoLoadedLocalMap));
            }

            try
            {
            var belongs = LoadedLocalMapBelongingQuery.DoesWorldLocationBelongToLoadedLocalMap(
                world,
                characterId,
                out var loadedContext);

            LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap(world, out _);
            var loadedMap = loadedContext.ActiveMapLayoutId;
            if (string.IsNullOrEmpty(loadedMap) &&
                LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap(world, out var mapContext))
                loadedMap = mapContext.ActiveMapLayoutId;

            var newLocation = previousLocation;
            if (world?.WorldPresence != null &&
                world.WorldPresence.TryGet(characterId, out var currentPresence) &&
                currentPresence != null)
                newLocation = BackgroundLoadedLocalMapArrivalDebug.DescribePresence(currentPresence);

            if (!belongs)
            {
                LogRuntimeArrival(
                    characterId,
                    previousLocation,
                    newLocation,
                    loadedMap,
                    false,
                    materializeRequested,
                    false,
                    authorityBefore,
                    authorityBefore,
                    "NotOnLoadedLocalMap");
                return false;
            }

            if (!IsEligibleCharacter(world, characterId, party))
            {
                LogRuntimeArrival(
                    characterId,
                    previousLocation,
                    newLocation,
                    loadedMap,
                    true,
                    materializeRequested,
                    false,
                    authorityBefore,
                    authorityBefore,
                    "NotEligible");
                return false;
            }

            if (world.LocalMap.ContainsOccupant(characterId))
            {
                if (request.Trigger == LoadedLocalMapMaterializationTrigger.InitialLoad)
                {
                    LogRuntimeArrival(
                        characterId,
                        previousLocation,
                        newLocation,
                        loadedMap,
                        true,
                        materializeRequested,
                        false,
                        authorityBefore,
                        authorityBefore,
                        "AlreadyOccupantInitialLoad");
                    return false;
                }

                QueuePresentationFlush(characterId);
                CharacterWorldMovementAuthorityQuery.TryGetAuthority(
                    world,
                    characterId,
                    party,
                    out var authorityAfterOccupant);
                LogRuntimeArrival(
                    characterId,
                    previousLocation,
                    newLocation,
                    loadedMap,
                    true,
                    materializeRequested,
                    true,
                    authorityBefore,
                    authorityAfterOccupant,
                    "AlreadyOccupant");
                return true;
            }

            if (!TryResolveMaterializationBounds(world, request.ExplicitBounds, out var bounds))
            {
                LogRuntimeArrival(
                    characterId,
                    previousLocation,
                    newLocation,
                    loadedMap,
                    true,
                    materializeRequested,
                    false,
                    authorityBefore,
                    authorityBefore,
                    "MissingPlayableBounds");
                return false;
            }

            var materialized = false;
            switch (loadedContext.Kind)
            {
                case LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WildernessHex:
                    materialized = TryMaterializeWildernessCore(
                        world,
                        characterId,
                        loadedContext.WildernessHex,
                        bounds,
                        in request);
                    break;

                case LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WorldSite:
                    materialized = TryMaterializeWorldSiteCore(
                        world,
                        characterId,
                        loadedContext.Site,
                        bounds,
                        in request);
                    break;
            }

            if (!materialized)
            {
                LogRuntimeArrival(
                    characterId,
                    previousLocation,
                    newLocation,
                    loadedMap,
                    true,
                    materializeRequested,
                    false,
                    authorityBefore,
                    authorityBefore,
                    "MaterializeCoreFailed");
                return false;
            }

            QueuePresentationFlush(characterId);

            if (request.StopBackgroundTravel)
                BackgroundCharacterTravelService.CancelTravelIfAny(world, characterId);

            CharacterWorldMovementAuthorityQuery.TryGetAuthority(
                world,
                characterId,
                party,
                out var authorityAfter);
            LogRuntimeArrival(
                characterId,
                previousLocation,
                newLocation,
                loadedMap,
                true,
                materializeRequested,
                true,
                authorityBefore,
                authorityAfter,
                BackgroundLoadedLocalMapArrivalDebug.DescribeLoadedContext(in loadedContext));
            return true;
            }
            finally
            {
                if (materializeRequested)
                {
                    BackgroundLoadedLocalMapArrivalDebug.LogActiveSideEffect(
                        BackgroundLoadedLocalMapArrivalDebug.FinishActiveSideEffectTrace(in activeTrace, world));
                }
            }
        }

        /// <summary>
        /// Wilderness LocalMap 展开后：扫描应出现在当前 Loaded Hex 的 Background Character（Initial Load）。
        /// </summary>
        public static int MaterializeEligibleWildernessCharactersOnLocalMap(
            SimulationWorld world,
            PlayerPartyRuntime party,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds)
        {
            if (world?.LocalMap == null || world.WorldPresence == null)
                return 0;
            if (!LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap(world, out var loaded) ||
                loaded.Kind != LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WildernessHex)
                return 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            BackgroundBgTravelFullTrace.BeginTrace();
            LoadedLocalMapBelongingExplain.ExplainTryResolveLoadedLocalMap(
                world,
                out _,
                out var initialLoadReason);
            BackgroundBgTravelFullTrace.Log(
                "InitialLoad",
                "CASE C entry MaterializeEligibleWildernessCharactersOnLocalMap " + initialLoadReason);
#endif

            var request = LoadedLocalMapMaterializationRequest.ForInitialLoad(bounds);
            var materialized = 0;
            foreach (var kv in world.WorldPresence.All)
            {
                var presence = kv.Value;
                if (presence == null || presence.EntityId.IsNone)
                    continue;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LoadedLocalMapBelongingExplain.ExplainDoesBelong(
                    world,
                    presence.EntityId,
                    loaded.WildernessHex,
                    out var belongs,
                    out var belongReason);
                BackgroundBgTravelFullTrace.Log(
                    "InitialLoad",
                    "Scan CharacterId=" + presence.EntityId.Value +
                    " Belongs=" + belongs +
                    " " + belongReason);
#endif
                if (!TryMaterializeCharacterIntoLoadedLocalMap(
                        world,
                        presence.EntityId,
                        party,
                        in request))
                    continue;
                materialized++;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            BackgroundBgTravelFullTrace.Log(
                "InitialLoad",
                "MaterializedCount=" + materialized);
#endif
            return materialized;
        }

        public static bool TryResolveLoadedWildernessHex(SimulationWorld world, out HexCoord hex)
        {
            hex = default;
            if (!LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap(world, out var loaded) ||
                loaded.Kind != LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WildernessHex)
                return false;
            hex = loaded.WildernessHex;
            return true;
        }

        public static bool IsBackgroundCharacterVisibleOnLoadedWildernessLocalMap(
            SimulationWorld world,
            EntityId characterId)
        {
            if (world?.LocalMap == null || characterId.IsNone)
                return false;
            if (!LoadedLocalMapBelongingQuery.DoesWorldLocationBelongToLoadedLocalMap(
                    world,
                    characterId,
                    out var loaded) ||
                loaded.Kind != LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WildernessHex)
                return false;
            return world.LocalMap.ContainsOccupant(characterId);
        }

        public static bool TryMaterializeBackgroundCharacterOnLoadedWildernessHex(
            SimulationWorld world,
            EntityId characterId,
            HexCoord enteredHex,
            HexCoord ingressFromHex,
            bool useIngressEdgePresentation,
            PlayerPartyRuntime party = null,
            bool stopBackgroundTravel = true)
        {
            if (world == null || characterId.IsNone)
                return false;
            if (!TryResolveLoadedWildernessHex(world, out var loadedHex) || !enteredHex.Equals(loadedHex))
                return false;

            var request = LoadedLocalMapMaterializationRequest.ForRuntimeHexEntry(
                ingressFromHex,
                stopBackgroundTravel);
            return TryMaterializeCharacterIntoLoadedLocalMap(world, characterId, party, in request);
        }

        public static bool TryMaterializeBackgroundCharacterOnLoadedWildernessHex(
            SimulationWorld world,
            EntityId characterId,
            HexCoord enteredHex,
            PlayerPartyRuntime party = null,
            bool stopBackgroundTravel = true)
        {
            if (!world.WorldPresence.TryGet(characterId, out var presence) ||
                presence == null ||
                !presence.HasContinuousWorldPosition)
                return false;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var ingressFrom = HexMath.WorldToHex(
                presence.WorldPosX,
                presence.WorldPosY,
                hexSize);
            return TryMaterializeBackgroundCharacterOnLoadedWildernessHex(
                world,
                characterId,
                enteredHex,
                ingressFrom,
                useIngressEdgePresentation: false,
                party,
                stopBackgroundTravel);
        }

        public static bool TryMaterializeImmediateArrival(
            SimulationWorld world,
            EntityId characterId,
            in BackgroundTravelArrivalContext arrivalContext,
            PlayerPartyRuntime party = null)
        {
            if (world == null || characterId.IsNone)
                return false;

            var request = LoadedLocalMapMaterializationRequest.ForRuntimeSiteArrival(in arrivalContext);
            return TryMaterializeCharacterIntoLoadedLocalMap(world, characterId, party, in request);
        }

        public static void ReleaseEligibleOccupantsOnLocalMapUnload(
            SimulationWorld world,
            PlayerPartyRuntime party)
        {
            if (world?.LocalMap == null)
                return;

            ReleaseScratch.Clear();
            var occupants = world.LocalMap.OccupantIds;
            for (var i = 0; i < occupants.Count; i++)
            {
                var id = occupants[i];
                if (id.IsNone)
                    continue;
                if (party != null && party.IsMember(id))
                    continue;
                // Phase 5S-B2-3.1：FormalArmy Local presentation 是派生缓存
                // （World authority = FormalArmy.WorldMotion），LocalMap unload 可以安全 release；
                // 下一张正确地图由 LoadedStrategicPopulationMaterializer 重新 rematerialize。
                // 不清 FormalArmy.WorldMotion、不 disband、不修改成员 WorldPresence 世界位置。
                if (world.BackgroundCharacterTravel != null &&
                    world.BackgroundCharacterTravel.IsTraveling(id))
                    continue;
                ReleaseScratch.Add(id);
            }

            for (var i = 0; i < ReleaseScratch.Count; i++)
                ReleaseLocalPresentation(world, ReleaseScratch[i]);

            ReleaseScratch.Clear();
        }

        public static bool TryResolvePlayableBounds(
            SimulationWorld world,
            out WildernessLocalWorldProjection.WildernessLocalMapBounds bounds)
        {
            bounds = default;
            if (world?.LocalMap == null)
                return false;
            return world.LocalMap.TryGetPlayableBounds(out bounds);
        }

        static bool TryMaterializeWildernessCore(
            SimulationWorld world,
            EntityId characterId,
            HexCoord loadedHex,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            in LoadedLocalMapMaterializationRequest request)
        {
            if (!LoadedLocalMapBelongingQuery.DoesWorldLocationBelongToLoadedLocalMap(
                    world,
                    characterId,
                    out var loaded) ||
                loaded.Kind != LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WildernessHex ||
                !loaded.WildernessHex.Equals(loadedHex))
                return false;

            if (!TryMaterializeWildernessAtProjectedPosition(
                    world,
                    characterId,
                    ResolveCharacterWorldPosition(world, characterId),
                    bounds))
                return false;

            if (request.Trigger != LoadedLocalMapMaterializationTrigger.RuntimeArrival ||
                !request.IngressFromHex.HasValue)
                return true;

            var ingressFrom = request.IngressFromHex.Value;
            if (ingressFrom.Equals(loadedHex))
                return true;

            TryApplyWildernessIngressPresentation(
                world,
                characterId,
                loadedHex,
                ingressFrom,
                bounds);
            return true;
        }

        static bool TryMaterializeWorldSiteCore(
            SimulationWorld world,
            EntityId characterId,
            WorldSite site,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            in LoadedLocalMapMaterializationRequest request)
        {
            if (site == null)
                return false;

            if (request.ArrivalContext.HasValue)
            {
                var arrivalContext = request.ArrivalContext.Value;
                if (TryResolveSiteIngressPresentation(
                        world,
                        in arrivalContext,
                        site,
                        bounds,
                        out var connection,
                        out var entryLocalX,
                        out var entryLocalY) &&
                    ApplyLocalPresentation(
                        world,
                        characterId,
                        connection,
                        bounds,
                        entryLocalX,
                        entryLocalY))
                    return true;
            }

            return TryMaterializeWorldSiteFallbackPresentation(world, characterId, bounds);
        }

        static bool TryResolveSiteIngressPresentation(
            SimulationWorld world,
            in BackgroundTravelArrivalContext arrivalContext,
            WorldSite site,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            out SurfaceExitConnection connection,
            out float entryLocalX,
            out float entryLocalY)
        {
            connection = default;
            entryLocalX = bounds.CenterX;
            entryLocalY = bounds.CenterY;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var depth = SurfaceExitZoneCalculator.ResolveDepthFromSession(world, bounds);
            var spanFraction = SurfaceExitZoneCalculator.DefaultSlotSpanFraction;

            ConnectionScratch.Clear();
            WorldSiteFootprintExitConnectionResolver.CollectConnections(
                world,
                site,
                hexSize,
                bounds,
                depth,
                spanFraction,
                ConnectionScratch);

            for (var i = 0; i < ConnectionScratch.Count; i++)
            {
                var candidate = ConnectionScratch[i];
                if (!candidate.DestinationHex.Equals(arrivalContext.IngressOutsideHex))
                    continue;

                connection = candidate;
                WildernessLocalWorldProjection.GetLocalPositionNearEdge(
                    bounds,
                    candidate.SourceHex,
                    arrivalContext.IngressOutsideHex,
                    hexSize,
                    depth,
                    out entryLocalX,
                    out entryLocalY);
                return true;
            }

            return false;
        }

        static bool TryMaterializeWorldSiteFallbackPresentation(
            SimulationWorld world,
            EntityId characterId,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds)
        {
            var mapId = world.LocalMap?.ActiveMapLayoutId?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(mapId) &&
                LoadedLocalMapPlacementSnapshotRestore.TryResolveWorldSiteSpawnPosition(
                    characterId,
                    mapId,
                    bounds.CenterX,
                    bounds.CenterY,
                    out var savedX,
                    out var savedY,
                    out var source) &&
                source == LoadedLocalMapPlacementSnapshotRestore.SpawnPlacementSource.SnapshotLocalPlacement)
            {
                return ApplyWildernessPresentationOverride(world, characterId, savedX, savedY);
            }

            var startId = world.WorldRegion?.StartLocationId ?? string.Empty;
            if (!string.IsNullOrEmpty(startId) &&
                world.WorldRegion.TryGet(startId, out var startLoc) &&
                startLoc != null)
            {
                return ApplyWildernessPresentationOverride(
                    world,
                    characterId,
                    startLoc.PresentationX,
                    startLoc.PresentationZ);
            }

            return ApplyWildernessPresentationOverride(
                world,
                characterId,
                bounds.CenterX,
                bounds.CenterY);
        }

        static void TryApplyWildernessIngressPresentation(
            SimulationWorld world,
            EntityId characterId,
            HexCoord enteringHex,
            HexCoord ingressFromHex,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds)
        {
            if (!world.LocalMap.ContainsOccupant(characterId))
                return;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var depth = SurfaceExitZoneCalculator.ResolveDepthFromSession(world, bounds);
            var spanFraction = SurfaceExitZoneCalculator.DefaultSlotSpanFraction;

            if (SurfaceExitZoneCalculator.TryBuildConnectionBetweenHexes(
                    world,
                    enteringHex,
                    ingressFromHex,
                    ResolveDirectionBetween(enteringHex, ingressFromHex),
                    hexSize,
                    bounds,
                    depth,
                    spanFraction,
                    out var connection))
            {
                WildernessLocalWorldProjection.GetLocalPositionNearEdge(
                    bounds,
                    connection.SourceHex,
                    connection.DestinationHex,
                    hexSize,
                    depth,
                    out var entryLocalX,
                    out var entryLocalY);
                UpdatePresentationOverride(world, characterId, entryLocalX, entryLocalY);
                return;
            }

            WildernessLocalWorldProjection.GetLocalPositionNearEdge(
                bounds,
                enteringHex,
                ingressFromHex,
                hexSize,
                depth,
                out var localX,
                out var localY);
            UpdatePresentationOverride(world, characterId, localX, localY);
        }

        static bool TryResolveMaterializationBounds(
            SimulationWorld world,
            WildernessLocalWorldProjection.WildernessLocalMapBounds? explicitBounds,
            out WildernessLocalWorldProjection.WildernessLocalMapBounds bounds)
        {
            if (explicitBounds.HasValue)
            {
                bounds = explicitBounds.Value;
                return true;
            }

            return TryResolvePlayableBounds(world, out bounds);
        }

        static void QueuePresentationFlush(EntityId characterId)
        {
            if (characterId.IsNone)
                return;
            for (var i = 0; i < PendingPresentationIds.Count; i++)
            {
                if (PendingPresentationIds[i] == characterId)
                    return;
            }

            PendingPresentationIds.Add(characterId);
        }

        static void LogRuntimeArrival(
            EntityId characterId,
            string previousLocation,
            string newLocation,
            string loadedMap,
            bool belongs,
            bool requested,
            bool result,
            CharacterWorldMovementAuthority authorityBefore,
            CharacterWorldMovementAuthority authorityAfter,
            string detail)
        {
            if (!requested)
                return;

            BackgroundLoadedLocalMapArrivalDebug.Log(new BackgroundLoadedLocalMapArrivalDebug.Snapshot(
                characterId,
                previousLocation,
                newLocation,
                loadedMap,
                belongs,
                requested,
                result,
                authorityBefore,
                authorityAfter,
                detail));

            if (BackgroundBgTravelFullTrace.ActiveTraceId > 0)
            {
                BackgroundBgTravelFullTrace.LogLocationCommit(
                    previousLocation,
                    newLocation,
                    default,
                    default,
                    "TryMaterializeCharacterIntoLoadedLocalMap");
                BackgroundBgTravelFullTrace.LogMaterializeResult(
                    characterId,
                    requested,
                    presentationExistsBefore: belongs && detail == "AlreadyOccupant",
                    guardResult: detail,
                    result: result);
                BackgroundBgTravelFullTrace.LogAuthority(authorityBefore, authorityAfter, isTraveling: false);
            }
        }

        static bool IsEligibleCharacter(
            SimulationWorld world,
            EntityId characterId,
            PlayerPartyRuntime party)
        {
            if (party != null && party.IsMember(characterId))
                return false;
            if (ArmyService.TryGetArmyForCharacter(world, characterId, out _))
                return false;
            if (!world.Entities.TryGet(characterId, out var entity) ||
                !CombatLifeStateService.CanFight(entity))
                return false;
            if ((entity.Tags & EntityTag.Character) == 0)
                return false;
            // Npc 是内容标签，不是世界存在 authority。只有持有有效 WorldPresence 的
            // Background Character 才走此物化路径；纯 authored resident NPC 继续由静态路径管理。
            if (world.WorldPresence == null ||
                !world.WorldPresence.TryGet(characterId, out var presence) || presence == null)
                return false;
            return true;
        }

        static WorldVec2 ResolveCharacterWorldPosition(SimulationWorld world, EntityId characterId)
        {
            if (world.WorldPresence.TryGet(characterId, out var presence) &&
                presence != null &&
                presence.HasContinuousWorldPosition)
                return presence.ContinuousWorldPosition;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            if (world.WorldPresence.TryGet(characterId, out presence) &&
                presence != null &&
                presence.UsesHexPresence)
            {
                HexMath.ToWorldPosition(presence.ResidualHex, hexSize, out var x, out var y);
                return new WorldVec2(x, y);
            }

            return default;
        }

        static bool TryMaterializeWildernessAtProjectedPosition(
            SimulationWorld world,
            EntityId characterId,
            WorldVec2 worldPos,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds)
        {
            if (world.LocalMap.ContainsOccupant(characterId))
                return false;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            if (!WildernessLocalWorldProjection.TryProjectWorldToLocal(
                    worldPos,
                    bounds,
                    hexSize,
                    out var localX,
                    out var localY))
                return false;

            return ApplyWildernessPresentationOverride(world, characterId, localX, localY);
        }

        static bool ApplyWildernessPresentationOverride(
            SimulationWorld world,
            EntityId characterId,
            float localX,
            float localY)
        {
            world.LocalMap.AddOccupant(characterId);

            if (!world.Entities.TryGet(characterId, out var entity) || entity == null)
                return true;

            if (!entity.TryGet<EntityLocationComponent>(out var loc) || loc == null)
            {
                loc = new EntityLocationComponent();
                entity.AddComponent(loc);
            }

            loc.LocationId = string.Empty;
            loc.SetPresentationOverride(localX, localY);
            return true;
        }

        static void UpdatePresentationOverride(
            SimulationWorld world,
            EntityId characterId,
            float localX,
            float localY)
        {
            if (!world.Entities.TryGet(characterId, out var entity) || entity == null)
                return;
            if (!entity.TryGet<EntityLocationComponent>(out var loc) || loc == null)
                return;
            loc.SetPresentationOverride(localX, localY);
        }

        static int ResolveDirectionBetween(HexCoord fromHex, HexCoord toHex)
        {
            for (var i = 0; i < 6; i++)
            {
                if (HexMath.Neighbor(fromHex, i).Equals(toHex))
                    return i;
            }

            return 0;
        }

        static bool ApplyLocalPresentation(
            SimulationWorld world,
            EntityId characterId,
            SurfaceExitConnection connection,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float entryLocalX,
            float entryLocalY)
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

            loc.LocationId = string.Empty;
            loc.SetPresentationOverride(entryLocalX, entryLocalY);

            var depth = SurfaceExitZoneCalculator.ResolveDepthFromSession(world, bounds);
            if (WildernessLocalWorldProjection.IsInExitTriggerBand(entryLocalX, entryLocalY, bounds, depth) &&
                SurfaceExitZoneCalculator.PointBelongsToConnection(
                    entryLocalX, entryLocalY, connection, depth))
            {
                WildernessLocalWorldProjection.GetLocalPositionNearEdge(
                    bounds,
                    connection.SourceHex,
                    connection.DestinationHex,
                    world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f,
                    depth,
                    out entryLocalX,
                    out entryLocalY);
                loc.SetPresentationOverride(entryLocalX, entryLocalY);
            }

            return true;
        }

        static void ReleaseLocalPresentation(SimulationWorld world, EntityId characterId)
        {
            world.LocalMap.RemoveOccupant(characterId);
            if (!world.Entities.TryGet(characterId, out var entity) ||
                entity == null ||
                !entity.TryGet<EntityLocationComponent>(out var loc) ||
                loc == null)
                return;

            loc.HasPresentationOverride = false;
            loc.PresentationOverrideX = 0f;
            loc.PresentationOverrideZ = 0f;
        }
    }
}
