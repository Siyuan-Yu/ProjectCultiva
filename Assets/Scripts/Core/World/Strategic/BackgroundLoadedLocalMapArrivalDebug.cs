using System.Diagnostics;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Runtime arrival 一次性诊断（非每帧）。
    /// </summary>
    public static class BackgroundLoadedLocalMapArrivalDebug
    {
        public readonly struct Snapshot
        {
            public Snapshot(
                EntityId characterId,
                string previousLocation,
                string newLocation,
                string loadedMap,
                bool belongsToLoadedMap,
                bool materializeRequested,
                bool materializeResult,
                CharacterWorldMovementAuthority authorityBefore,
                CharacterWorldMovementAuthority authorityAfter,
                string detail)
            {
                CharacterId = characterId;
                PreviousLocation = previousLocation ?? string.Empty;
                NewLocation = newLocation ?? string.Empty;
                LoadedMap = loadedMap ?? string.Empty;
                BelongsToLoadedMap = belongsToLoadedMap;
                MaterializeRequested = materializeRequested;
                MaterializeResult = materializeResult;
                AuthorityBefore = authorityBefore;
                AuthorityAfter = authorityAfter;
                Detail = detail ?? string.Empty;
            }

            public EntityId CharacterId { get; }
            public string PreviousLocation { get; }
            public string NewLocation { get; }
            public string LoadedMap { get; }
            public bool BelongsToLoadedMap { get; }
            public bool MaterializeRequested { get; }
            public bool MaterializeResult { get; }
            public CharacterWorldMovementAuthority AuthorityBefore { get; }
            public CharacterWorldMovementAuthority AuthorityAfter { get; }
            public string Detail { get; }
        }

        public readonly struct ActiveSideEffectTrace
        {
            public ActiveSideEffectTrace(
                EntityId arrivingCharacterId,
                EntityId activeCharacterId,
                string activeWorldBefore,
                string activeLocalBefore,
                string arrivalWorldPosition,
                string materializationMethod,
                string activeWorldAfter,
                string activeLocalAfter)
            {
                ArrivingCharacterId = arrivingCharacterId;
                ActiveCharacterId = activeCharacterId;
                ActiveWorldBefore = activeWorldBefore ?? string.Empty;
                ActiveLocalBefore = activeLocalBefore ?? string.Empty;
                ArrivalWorldPosition = arrivalWorldPosition ?? string.Empty;
                MaterializationMethod = materializationMethod ?? string.Empty;
                ActiveWorldAfter = activeWorldAfter ?? string.Empty;
                ActiveLocalAfter = activeLocalAfter ?? string.Empty;
            }

            public EntityId ArrivingCharacterId { get; }
            public EntityId ActiveCharacterId { get; }
            public string ActiveWorldBefore { get; }
            public string ActiveLocalBefore { get; }
            public string ArrivalWorldPosition { get; }
            public string MaterializationMethod { get; }
            public string ActiveWorldAfter { get; }
            public string ActiveLocalAfter { get; }

            public bool ActivePositionChanged =>
                !string.Equals(ActiveWorldBefore, ActiveWorldAfter, System.StringComparison.Ordinal) ||
                !string.Equals(ActiveLocalBefore, ActiveLocalAfter, System.StringComparison.Ordinal);
        }

        public static ActiveSideEffectTrace CaptureActiveSideEffectTrace(
            SimulationWorld world,
            EntityId arrivingCharacterId,
            PlayerPartyRuntime party,
            string materializationMethod)
        {
            var activeId = party != null && party.HasActive ? party.ActiveCharacterId : EntityId.None;
            return new ActiveSideEffectTrace(
                arrivingCharacterId,
                activeId,
                DescribeCharacterWorldPosition(world, activeId),
                DescribeCharacterLocalPosition(world, activeId),
                DescribeCharacterWorldPosition(world, arrivingCharacterId),
                materializationMethod,
                DescribeCharacterWorldPosition(world, activeId),
                DescribeCharacterLocalPosition(world, activeId));
        }

        public static ActiveSideEffectTrace FinishActiveSideEffectTrace(
            in ActiveSideEffectTrace before,
            SimulationWorld world)
        {
            return new ActiveSideEffectTrace(
                before.ArrivingCharacterId,
                before.ActiveCharacterId,
                before.ActiveWorldBefore,
                before.ActiveLocalBefore,
                before.ArrivalWorldPosition,
                before.MaterializationMethod,
                DescribeCharacterWorldPosition(world, before.ActiveCharacterId),
                DescribeCharacterLocalPosition(world, before.ActiveCharacterId));
        }

        public static void Log(in Snapshot snapshot)
        {
            var line =
                "BackgroundArrival:" +
                " Character=" + snapshot.CharacterId.Value +
                " PreviousLocation=" + snapshot.PreviousLocation +
                " NewLocation=" + snapshot.NewLocation +
                " LoadedMap=" + snapshot.LoadedMap +
                " BelongsToLoadedMap=" + snapshot.BelongsToLoadedMap +
                " MaterializeRequested=" + snapshot.MaterializeRequested +
                " MaterializeResult=" + snapshot.MaterializeResult +
                " AuthorityBefore=" + snapshot.AuthorityBefore +
                " AuthorityAfter=" + snapshot.AuthorityAfter +
                (string.IsNullOrEmpty(snapshot.Detail) ? string.Empty : " Detail=" + snapshot.Detail);
            BackgroundTravelTraceSink.Emit(line);
        }

        public static void LogActiveSideEffect(in ActiveSideEffectTrace trace)
        {
            var line =
                "BackgroundArrivalSideEffect:" +
                " ArrivingCharacterId=" + trace.ArrivingCharacterId.Value +
                " ActiveCharacterId=" + trace.ActiveCharacterId.Value +
                " ActiveWorldBefore=" + trace.ActiveWorldBefore +
                " ActiveLocalBefore=" + trace.ActiveLocalBefore +
                " ArrivalCharacterWorldPosition=" + trace.ArrivalWorldPosition +
                " MaterializeTargetCharacterId=" + trace.ArrivingCharacterId.Value +
                " MaterializationMethod=" + trace.MaterializationMethod +
                " ActiveWorldAfter=" + trace.ActiveWorldAfter +
                " ActiveLocalAfter=" + trace.ActiveLocalAfter +
                " ActivePositionChanged=" + trace.ActivePositionChanged;
            BackgroundTravelTraceSink.Emit(line);

            if (BackgroundBgTravelFullTrace.ActiveTraceId > 0)
                BackgroundBgTravelFullTrace.LogActiveSideEffect(in trace);
        }

        public static string DescribePresence(WorldAgentPresence presence)
        {
            if (presence == null)
                return "None";
            switch (presence.Mode)
            {
                case PartyWorldPresenceMode.AtSite:
                    return "AtSite(" + (presence.SiteId ?? string.Empty) + ")";
                case PartyWorldPresenceMode.AtWorldPosition:
                    return "AtWorldPosition(" +
                           presence.WorldPosX.ToString("0.###") + "," +
                           presence.WorldPosY.ToString("0.###") + ")";
                case PartyWorldPresenceMode.AtHex:
                    return "AtHex(" + presence.ResidualHex + ")";
                default:
                    return presence.Mode.ToString();
            }
        }

        public static string DescribeLoadedContext(in LoadedLocalMapBelongingQuery.LoadedLocalMapContext context)
        {
            switch (context.Kind)
            {
                case LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WorldSite:
                    return "WorldSite(" + (context.Site?.SiteId ?? string.Empty) + ")";
                case LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WildernessHex:
                    return "WildernessHex(" + context.WildernessHex + ")";
                default:
                    return "None";
            }
        }

        static string DescribeCharacterWorldPosition(SimulationWorld world, EntityId characterId)
        {
            if (world == null || characterId.IsNone)
                return "None";
            if (!world.WorldPresence.TryGet(characterId, out var presence) || presence == null)
                return "None";
            return DescribePresence(presence);
        }

        static string DescribeCharacterLocalPosition(SimulationWorld world, EntityId characterId)
        {
            if (world == null || characterId.IsNone)
                return "None";
            if (!world.Entities.TryGet(characterId, out var entity) ||
                entity == null ||
                !entity.TryGet<EntityLocationComponent>(out var loc) ||
                loc == null)
                return "None";
            if (!loc.HasPresentationOverride)
                return "NoOverride";
            return loc.PresentationOverrideX.ToString("0.###") + "," +
                   loc.PresentationOverrideZ.ToString("0.###");
        }
    }
}
