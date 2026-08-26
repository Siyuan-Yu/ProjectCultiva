using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Background Character WorldLocation 进入当前 Loaded LocalMap 时触发 Runtime Materialization（非轮询）。
    /// </summary>
    public static class BackgroundCharacterWildernessLocalMapMaterialization
    {
        public static void NotifyEnteredWorldHex(
            SimulationWorld world,
            EntityId characterId,
            HexCoord enteredHex,
            HexCoord ingressFromHex,
            PlayerPartyRuntime party = null)
        {
            var traceActive = BackgroundBgTravelFullTrace.ActiveTraceId > 0;
            if (world == null || characterId.IsNone || enteredHex.Equals(ingressFromHex))
            {
                if (traceActive)
                {
                    BackgroundBgTravelFullTrace.LogMaterializeGuard(
                        "NotifyEnteredWorldHex",
                        false,
                        "EarlyExit worldNull=" + (world == null) +
                        " characterNone=" + characterId.IsNone +
                        " ingressEqualsEntered=" + enteredHex.Equals(ingressFromHex));
                }

                return;
            }

            world.Events?.Publish(
                EventType.BackgroundCharacterEnteredWorldHex,
                world.Tick,
                target: characterId,
                payload: enteredHex.Q + "," + enteredHex.R + ";" + ingressFromHex.Q + "," + ingressFromHex.R);

            if (traceActive)
            {
                BackgroundBgTravelFullTrace.LogNotification(
                    "BackgroundCharacterEnteredWorldHex",
                    handlerInvoked: false,
                    "PublishOnly;NoMaterializationSubscriber");
            }

            if (!LoadedLocalMapBelongingQuery.TryResolveLoadedLocalMap(world, out var loaded))
            {
                if (traceActive)
                {
                    LoadedLocalMapBelongingExplain.ExplainTryResolveLoadedLocalMap(
                        world,
                        out _,
                        out var reason);
                    BackgroundBgTravelFullTrace.LogRuntimeMap(world, enteredHex);
                    BackgroundBgTravelFullTrace.LogMaterializeGuard(
                        "NotifyLoadedLocalMapResolve",
                        false,
                        reason);
                }

                return;
            }

            if (loaded.Kind != LoadedLocalMapBelongingQuery.LoadedLocalMapKind.WildernessHex)
            {
                if (traceActive)
                {
                    BackgroundBgTravelFullTrace.LogRuntimeMap(world, enteredHex);
                    BackgroundBgTravelFullTrace.LogMaterializeGuard(
                        "NotifyLoadedLocalMapKind",
                        false,
                        "LoadedKind=" + loaded.Kind + " Expected=WildernessHex");
                }

                return;
            }

            if (!enteredHex.Equals(loaded.WildernessHex))
            {
                if (traceActive)
                {
                    LoadedLocalMapBelongingExplain.ExplainDoesBelong(
                        world,
                        characterId,
                        enteredHex,
                        out var belongs,
                        out var reason);
                    BackgroundBgTravelFullTrace.LogRuntimeMap(world, enteredHex);
                    BackgroundBgTravelFullTrace.LogMaterializeGuard(
                        "NotifyEnteredEqualsLoadedHex",
                        false,
                        "EnteredHex=" + enteredHex +
                        " LoadedWildernessHex=" + loaded.WildernessHex +
                        " BelongsToLoadedMap=" + belongs +
                        " " + reason);
                }

                return;
            }

            if (traceActive)
            {
                var presentationExistsBefore = world.LocalMap != null && world.LocalMap.ContainsOccupant(characterId);
                LoadedLocalMapBelongingExplain.ExplainDoesBelong(
                    world,
                    characterId,
                    enteredHex,
                    out _,
                    out var reason);
                BackgroundBgTravelFullTrace.LogRuntimeMap(world, enteredHex);
                BackgroundBgTravelFullTrace.LogMaterializeGuard(
                    "NotifyPreMaterialize",
                    true,
                    reason);
            }

            var isActiveTravelEntry = world.BackgroundCharacterTravel != null &&
                                      world.BackgroundCharacterTravel.IsTraveling(characterId);
            var request = LoadedDestinationArrivalMaterializer.LoadedLocalMapMaterializationRequest.ForRuntimeHexEntry(
                ingressFromHex,
                stopBackgroundTravel: isActiveTravelEntry);
            var presentationBeforeMaterialize = world.LocalMap != null && world.LocalMap.ContainsOccupant(characterId);
            var materialized = LoadedDestinationArrivalMaterializer.TryMaterializeCharacterIntoLoadedLocalMap(
                world,
                characterId,
                party,
                in request);

            if (traceActive)
            {
                BackgroundBgTravelFullTrace.LogMaterializeResult(
                    characterId,
                    requested: true,
                    presentationExistsBefore: presentationBeforeMaterialize,
                    guardResult: "NotifyEnteredWorldHex",
                    result: materialized);
            }
        }
    }
}
