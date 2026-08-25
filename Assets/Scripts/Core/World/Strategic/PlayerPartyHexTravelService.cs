using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// PlayerParty Hex 世界旅行（非 FormalArmy；共用 Hex A*／步进时间规则）。
    /// </summary>
    public static class PlayerPartyHexTravelService
    {
        static readonly List<HexCoord> PathScratch = new List<HexCoord>(64);

        public static bool TryResolvePartyWorldHex(
            SimulationWorld world,
            PlayerPartyRuntime party,
            out HexCoord worldHex)
        {
            worldHex = default;
            if (world?.PlayerPartyTravel != null && world.PlayerPartyTravel.HasPosition)
            {
                worldHex = world.PlayerPartyTravel.CurrentHex;
                return true;
            }

            return CharacterWorldPresenceQuery.TryGetPartyWorldHex(world, party, out worldHex);
        }

        public static Result BeginTravel(
            SimulationWorld world,
            PlayerPartyRuntime party,
            HexCoord destination,
            HexTravelMode mode = HexTravelMode.Ground)
        {
            if (world == null || party == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid party travel args.");
            if (!party.HasActive)
                return Result.Failure(ErrorCode.InvalidOperation, "PlayerParty has no active character.");
            if (mode != HexTravelMode.Ground)
                return Result.Failure(ErrorCode.InvalidOperation, "Only Ground travel is supported in V1.");
            if (!world.HexWorld.HasGrid)
                return Result.Failure(ErrorCode.InvalidOperation, "Hex grid not loaded.");
            if (!world.HexWorld.TryGetTile(destination, out var destTile) ||
                destTile == null ||
                !destTile.IsPassable)
                return Result.Failure(ErrorCode.InvalidArgument, "Destination hex is not passable.");

            if (!TryResolvePartyWorldHex(world, party, out var start))
                return Result.Failure(ErrorCode.InvalidOperation, "PlayerParty has no world hex.");

            if (start == destination && !world.PlayerPartyTravel.IsMoving)
                return Result.Failure(ErrorCode.InvalidArgument, "Already at destination hex.");

            if (!HexPathfinder.TryFindPath(world.HexWorld, start, destination, PathScratch, mode) ||
                PathScratch.Count < 1)
                return Result.Failure(ErrorCode.InvalidOperation, "No hex path to destination.");

            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            ApplyTravelingMembersAtHex(world, start);
            world.PlayerPartyTravel.SetIdleAt(start);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            world.PlayerPartyTravel.SetHexPath(PathScratch, destination, mode);

            if (world.PartyWorld != null &&
                world.PartyWorld.Mode == PartyWorldPresenceMode.AtSite)
            {
                world.PartyWorld.ClearSiteFocus();
                world.PartyWorld.Mode = PartyWorldPresenceMode.AtHex;
            }

            return Result.Success();
        }

        public static Result CancelTravel(SimulationWorld world, PlayerPartyRuntime party = null)
        {
            if (world?.PlayerPartyTravel == null)
                return Result.Failure(ErrorCode.InvalidArgument, "No party travel state.");
            if (!world.PlayerPartyTravel.IsMoving)
                return Result.Failure(ErrorCode.InvalidOperation, "PlayerParty is not traveling.");

            var stop = world.PlayerPartyTravel.CurrentHex;
            world.PlayerPartyTravel.CancelAtCurrentHex();
            ApplyTravelingMembersAtHex(world, stop);
            return Result.Success();
        }

        public static void AdvanceAll(SimulationWorld world, int ticks)
        {
            if (world?.PlayerPartyTravel == null || ticks < 1)
                return;
            if (!world.PlayerPartyTravel.IsMoving)
                return;

            for (var i = 0; i < ticks; i++)
            {
                if (!AdvanceOneTick(world))
                    break;
            }
        }

        static bool AdvanceOneTick(SimulationWorld world)
        {
            var motion = world.PlayerPartyTravel;
            if (!motion.TryGetActiveStepHexes(out _, out var to))
            {
                motion.CompleteMove();
                return false;
            }

            if (!world.HexWorld.TryGetTile(to, out var tile) || tile == null)
            {
                motion.CompleteMove();
                return false;
            }

            var stepTicks = ComputeStepTicks(tile);
            if (stepTicks < 1)
                stepTicks = 1;

            if (motion.StepTotalTicks <= 0)
                motion.SetStep(stepTicks, stepTicks, 0f);

            var remaining = Math.Max(0, motion.StepRemainingTicks - 1);
            var total = motion.StepTotalTicks > 0 ? motion.StepTotalTicks : stepTicks;
            motion.SetStep(
                total,
                remaining,
                total <= 0 ? 1f : 1f - (float)remaining / total);

            if (remaining > 0)
                return true;

            motion.AdvanceToHex(to);
            ApplyTravelingMembersAtHex(world, to);
            motion.IncrementPathIndex();

            if (motion.CurrentPathIndex >= motion.HexPathCount - 1)
            {
                motion.CompleteMove();
                return false;
            }

            if (!motion.TryGetActiveStepHexes(out _, out var nextTo) ||
                !world.HexWorld.TryGetTile(nextTo, out var nextTile) ||
                nextTile == null)
            {
                motion.CompleteMove();
                return false;
            }

            var nextTicks = ComputeStepTicks(nextTile);
            motion.SetStep(nextTicks, nextTicks, 0f);
            return true;
        }

        public static Result EnterLocalViewAtCurrentHex(
            SimulationWorld world,
            PlayerPartyRuntime party)
        {
            if (world == null || party == null || !party.HasActive)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid party enter args.");
            if (world.PlayerPartyTravel != null && world.PlayerPartyTravel.IsMoving)
                return Result.Failure(ErrorCode.InvalidOperation, "Stop travel before entering local view.");

            if (!TryResolvePartyWorldHex(world, party, out var hex))
                return Result.Failure(ErrorCode.InvalidOperation, "PlayerParty has no world hex.");

            if (world.Strategic.Sites.TryGetAtHex(hex, out var site) &&
                site != null &&
                !string.IsNullOrWhiteSpace(site.LocalMapId))
                return EnterWorldSiteAsParty(world, party, site);

            if (!WildernessLocalMapFallback.TryResolve(world, hex, out var mapId) ||
                string.IsNullOrEmpty(mapId))
                return Result.Failure(ErrorCode.InvalidOperation, "No wilderness fallback LocalMap for hex.");

            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            ApplyTravelingMembersAtHex(world, hex);
            world.PlayerPartyTravel.SetIdleAt(hex);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            return WorldTravelService.EnterWildernessLocalMap(world, hex, mapId);
        }

        public static Result EnterWorldSiteAsParty(
            SimulationWorld world,
            PlayerPartyRuntime party,
            WorldSite site)
        {
            if (world == null || party == null || site == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid site enter args.");

            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            ApplyTravelingMembersAtSite(world, site.SiteId);
            world.PlayerPartyTravel.SetIdleAt(site.PresenceHex);
            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            return WorldTravelService.EnterWorldSiteScene(world, site.SiteId, string.Empty);
        }

        static void ApplyTravelingMembersAtHex(SimulationWorld world, HexCoord hex)
        {
            if (world?.WorldPresence == null || world.PlayerPartyTravel == null)
                return;
            var members = world.PlayerPartyTravel.TravelingMembers;
            for (var i = 0; i < members.Count; i++)
            {
                var id = members[i];
                if (id.IsNone)
                    continue;
                world.WorldPresence.SetAtHex(id, hex);
            }
        }

        static void ApplyTravelingMembersAtSite(SimulationWorld world, string siteId)
        {
            if (world?.WorldPresence == null || world.PlayerPartyTravel == null || string.IsNullOrEmpty(siteId))
                return;
            var members = world.PlayerPartyTravel.TravelingMembers;
            for (var i = 0; i < members.Count; i++)
            {
                var id = members[i];
                if (id.IsNone)
                    continue;
                world.WorldPresence.SetAtSite(id, siteId);
            }
        }

        public static void ApplyMembersAtHex(
            SimulationWorld world,
            PlayerPartyRuntime party,
            HexCoord hex)
        {
            if (world?.WorldPresence == null || party == null)
                return;
            world.PlayerPartyTravel?.CaptureTravelingMembers(party.Members);
            ApplyTravelingMembersAtHex(world, hex);
        }

        public static void ApplyMembersAtSite(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string siteId)
        {
            if (world?.WorldPresence == null || party == null || string.IsNullOrEmpty(siteId))
                return;
            world.PlayerPartyTravel?.CaptureTravelingMembers(party.Members);
            ApplyTravelingMembersAtSite(world, siteId);
        }

        static int ComputeStepTicks(HexCell tile)
        {
            var cost = tile?.ResolveMovementCost() ?? 1f;
            return Math.Max(4, (int)Math.Round(cost * 8f));
        }
    }
}
