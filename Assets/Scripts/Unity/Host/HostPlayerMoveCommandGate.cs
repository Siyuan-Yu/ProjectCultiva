using XianXia.Core.Domain.Ids;
using XianXia.Core.World;

namespace XianXia.Unity.Host
{
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
    }
}
