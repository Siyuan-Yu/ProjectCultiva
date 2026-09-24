using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Unity.Host
{
    public enum PartyWorkerCommandResolutionStatus
    {
        Invalid = 0,
        SelectedPartyWorkers = 1,
        ImplicitActiveWorker = 2,
        InvalidNonEmptySelection = 3,
        NoCommandableActive = 4
    }

    /// <summary>
    /// Phase 1: world move / route preview share one gate — Active is the sole command
    /// authority, but only when view selection authorizes it (empty or includes Active).
    /// </summary>
    public static class HostPlayerMoveCommandGate
    {
        public static bool IsActiveCommandContext(
            HostSelectionController selection,
            PlayerPartyRuntime party)
        {
            if (party == null || party.ActiveCharacterId.IsNone)
                return false;

            if (selection == null || selection.State.Count == 0)
                return true;

            return selection.State.Contains(party.ActiveCharacterId);
        }

        public static EntityId ResolveActiveForWorldMove(
            HostSelectionController selection,
            PlayerPartyRuntime party)
        {
            if (!IsActiveCommandContext(selection, party))
                return EntityId.None;
            return party.ActiveCharacterId;
        }

        public static EntityId ResolveActiveForWorldMove(
            HostSelectionController selection,
            PlayableHostSession session) =>
            ResolveActiveForWorldMove(selection, session?.PlayerParty);

        /// <summary>
        /// Shared party-work actor resolution. A valid nonempty selection yields its Party members;
        /// an empty selection means the current Active; an invalid nonempty context never falls back.
        /// </summary>
        public static PartyWorkerCommandResolutionStatus CollectPartyWorkersOrActive(
            SimulationWorld world,
            HostSelectionController selection,
            PlayerPartyRuntime party,
            List<EntityId> into)
        {
            if (into == null)
                return PartyWorkerCommandResolutionStatus.Invalid;
            into.Clear();
            if (party == null || !party.HasActive)
                return PartyWorkerCommandResolutionStatus.NoCommandableActive;

            if (selection != null && selection.State.Count > 0)
            {
                if (!IsActiveCommandContext(selection, party))
                    return PartyWorkerCommandResolutionStatus.InvalidNonEmptySelection;
                for (var i = 0; i < selection.State.Count; i++)
                {
                    var id = selection.State.SelectedIds[i];
                    if (party.IsMember(id))
                        into.Add(id);
                }
                return into.Count > 0
                    ? PartyWorkerCommandResolutionStatus.SelectedPartyWorkers
                    : PartyWorkerCommandResolutionStatus.InvalidNonEmptySelection;
            }

            var active = party.ActiveCharacterId;
            if (!party.IsMember(active) ||
                !PlayerPartyRuntime.CanActAsActive(world, active, out _))
                return PartyWorkerCommandResolutionStatus.NoCommandableActive;
            into.Add(active);
            return PartyWorkerCommandResolutionStatus.ImplicitActiveWorker;
        }
    }
}
