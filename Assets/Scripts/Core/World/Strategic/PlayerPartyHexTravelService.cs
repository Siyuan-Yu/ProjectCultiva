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
    /// PlayerParty Hex／连续世界旅行（非 FormalArmy）。
    /// Phase 2C：AutoTravel 沿路径几何连续推进 WorldPosition；CurrentHex 派生。
    /// </summary>
    public static class PlayerPartyHexTravelService
    {
        static readonly List<HexCoord> PathScratch = new List<HexCoord>(64);

        /// <summary>相邻格心距折算为恒定速度的参考 tick 数（距离预算，非“每段固定 N tick”）。</summary>
        public const float GroundBaseStepTicks = 8f;

        public static bool TryResolvePartyWorldHex(
            SimulationWorld world,
            PlayerPartyRuntime party,
            out HexCoord worldHex)
        {
            worldHex = default;
            if (!PlayerPartyWorldLocationQuery.TryResolve(world, party, out var resolved))
                return false;
            worldHex = resolved.DerivedHex;
            return true;
        }

        public static bool TryResolvePartyWorldPosition(
            SimulationWorld world,
            PlayerPartyRuntime party,
            out WorldVec2 worldPos)
        {
            worldPos = default;
            if (!PlayerPartyWorldLocationQuery.TryResolve(world, party, out var resolved))
                return false;
            worldPos = resolved.WorldPosition;
            return true;
        }

        public static Result BeginTravel(
            SimulationWorld world,
            PlayerPartyRuntime party,
            HexCoord destination,
            HexTravelMode mode = HexTravelMode.Ground) =>
            BeginTravel(world, party, destination, string.Empty, mode);

        public static Result BeginTravel(
            SimulationWorld world,
            PlayerPartyRuntime party,
            HexCoord destination,
            string destinationSiteId,
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

            if (!TryResolvePartyWorldHex(world, party, out var startHex))
                return Result.Failure(ErrorCode.InvalidOperation, "PlayerParty has no world hex.");

            // TargetHex V1：目的地语义为该格 canonical center（不存点击像素）。
            var goalHex = destination;
            if (!string.IsNullOrEmpty(destinationSiteId) &&
                world.Strategic.Sites.TryGet(destinationSiteId, out var targetSite) &&
                targetSite != null)
            {
                // Site 目标：路径落到 footprint 上最近可达格；进入后再聚合 PresenceHex。
                goalHex = ResolveDeterministicSiteApproachHex(world, startHex, targetSite);
            }

            if (startHex == goalHex &&
                !world.PlayerPartyTravel.IsMoving &&
                string.IsNullOrEmpty(destinationSiteId))
                return Result.Failure(ErrorCode.InvalidArgument, "Already at destination hex.");

            if (!HexPathfinder.TryFindPath(world.HexWorld, startHex, goalHex, PathScratch, mode) ||
                PathScratch.Count < 1)
                return Result.Failure(ErrorCode.InvalidOperation, "No hex path to destination.");

            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            EnsureMotionHasContinuousStart(world, startHex);
            ApplyTravelingMembersPresence(world);

            // 正式离开 Site / 开始开世界旅行：清空 PartyWorld presentation cache，
            // 禁止残留 SiteId/LocalMapId 在 TravelComplete 后反写 Domain。
            ClearPartyWorldPresentationCacheForOpenWorld(world);
            PlayerPartyWorldLocationDebug.LogSnapshot(world, party, "BeginTravel.LeaveSiteOrOpenWorld");

            world.PlayerPartyTravel.BeginAutoTravel(
                PathScratch,
                goalHex,
                destinationSiteId,
                mode,
                world.HexWorld.HexSize);
            ApplyTravelingMembersPresence(world);
            PlayerPartyWorldLocationDebug.LogSnapshot(world, party, "BeginTravel.AfterBeginAutoTravel");
            return Result.Success();
        }

        /// <summary>
        /// PartyWorld 仅作已展开 LocalMap presentation 缓存；开世界旅行期间不得保留 Site 权威。
        /// </summary>
        public static void ClearPartyWorldPresentationCacheForOpenWorld(SimulationWorld world)
        {
            if (world?.PartyWorld == null)
                return;
            world.PartyWorld.ClearSiteFocus();
            world.PartyWorld.LocalMapId = string.Empty;
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtHex;
            world.PartyWorld.EncounterId = string.Empty;
        }

        public static Result CancelTravel(SimulationWorld world, PlayerPartyRuntime party = null)
        {
            if (world?.PlayerPartyTravel == null)
                return Result.Failure(ErrorCode.InvalidArgument, "No party travel state.");
            if (!world.PlayerPartyTravel.IsMoving)
                return Result.Failure(ErrorCode.InvalidOperation, "PlayerParty is not traveling.");

            world.PlayerPartyTravel.CancelAutoTravelPreservePosition();
            ApplyTravelingMembersPresence(world);
            return Result.Success();
        }

        public static float WorldUnitsPerTick(float hexSize)
        {
            var size = hexSize > 0.0001f ? hexSize : 1f;
            var adjacentCenterDist = size * (float)Math.Sqrt(3.0);
            return adjacentCenterDist / GroundBaseStepTicks;
        }

        public static void AdvanceAll(SimulationWorld world, int ticks)
        {
            if (world?.PlayerPartyTravel == null || ticks < 1)
                return;
            if (!world.PlayerPartyTravel.IsMoving)
                return;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            AdvanceDistanceBudget(world, WorldUnitsPerTick(hexSize) * ticks);
        }

        /// <summary>沿路径几何消耗距离预算（可跨段，段边界不暂停）。</summary>
        public static void AdvanceDistanceBudget(SimulationWorld world, float distanceBudget)
        {
            if (world?.PlayerPartyTravel == null || distanceBudget <= 0f)
                return;
            if (!world.PlayerPartyTravel.IsMoving)
                return;

            var motion = world.PlayerPartyTravel;
            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var remainingBudget = distanceBudget;
            var guard = 0;
            while (remainingBudget > 0.0001f && motion.IsMoving && guard++ < 64)
            {
                if (!motion.TryGetActiveSegmentWorld(hexSize, out var fromPos, out var toPos))
                {
                    FinishArrival(world);
                    return;
                }

                var segmentLen = WorldVec2.Distance(fromPos, toPos);
                if (segmentLen < 0.0001f)
                {
                    motion.IncrementPathIndex();
                    if (motion.SegmentIndex >= motion.HexPathCount - 1)
                    {
                        FinishArrival(world);
                        return;
                    }

                    continue;
                }

                var remainingOnSegment = WorldVec2.Distance(motion.WorldPosition, toPos);
                if (remainingOnSegment <= remainingBudget + 0.0001f)
                {
                    motion.SetWorldPositionInternal(toPos, HexMath.WorldToHex(toPos.X, toPos.Y, hexSize));
                    motion.SetSegment(motion.SegmentIndex, 1f);
                    remainingBudget -= remainingOnSegment;
                    motion.IncrementPathIndex();
                    ApplyTravelingMembersPresence(world);
                    if (motion.SegmentIndex >= motion.HexPathCount - 1)
                    {
                        FinishArrival(world);
                        return;
                    }

                    continue;
                }

                var dirX = (toPos.X - motion.WorldPosition.X) / remainingOnSegment;
                var dirY = (toPos.Y - motion.WorldPosition.Y) / remainingOnSegment;
                var next = new WorldVec2(
                    motion.WorldPosition.X + dirX * remainingBudget,
                    motion.WorldPosition.Y + dirY * remainingBudget);
                var derived = HexMath.WorldToHex(next.X, next.Y, hexSize);
                motion.SetWorldPositionInternal(next, derived);
                var progress = 1f - WorldVec2.Distance(next, toPos) / segmentLen;
                motion.SetSegment(motion.SegmentIndex, progress);
                ApplyTravelingMembersPresence(world);
                remainingBudget = 0f;
            }
        }

        static void FinishArrival(SimulationWorld world)
        {
            var motion = world.PlayerPartyTravel;
            if (motion == null)
                return;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var kindBefore = motion.LocationKind;
            var siteBefore = motion.SiteId ?? string.Empty;
            var posBefore = motion.WorldPosition;
            var hexBefore = motion.CurrentHex;
            var destSiteId = motion.DestinationSiteId ?? string.Empty;
            var destHex = motion.DestinationHex;

            // 普通 TargetHex：停在目标 canonical center，保持 AtWorldPosition。
            motion.SnapToHexCenter(destHex, hexSize);

            // 仅当旅行目标明确是 WorldSite 时才聚合；禁止用出发 Site / PartyWorld 复活。
            if (!string.IsNullOrEmpty(destSiteId) &&
                world.Strategic.Sites.TryGet(destSiteId, out var site) &&
                site != null)
            {
                motion.SetAtWorldSite(site.SiteId, site.PresenceHex, hexSize);
                ApplyTravelingMembersAtSite(world, site.SiteId);
                // Site 到达：Domain 已是 AtWorldSite；PartyWorld 仍等玩家关地图再 Expand。
                ClearPartyWorldPresentationCacheForOpenWorld(world);
            }
            else
            {
                // 普通 Hex：确保 LocationKind 仍为 AtWorldPosition，且无 SiteId。
                motion.SnapToHexCenter(destHex, hexSize);
                ClearPartyWorldPresentationCacheForOpenWorld(world);
                ApplyTravelingMembersAtHex(world, destHex);
            }

            motion.CompleteMove();
            // Idle 后再次确保 Presence 与 LocationKind 一致（禁止 Complete 后再被 Site 覆盖）。
            ApplyTravelingMembersPresence(world);
            PlayerPartyWorldLocationDebug.LogBeforeAfter(
                world,
                null,
                "TravelComplete",
                kindBefore,
                siteBefore,
                posBefore,
                hexBefore);
        }

        public static Result EnterLocalViewAtCurrentHex(
            SimulationWorld world,
            PlayerPartyRuntime party)
        {
            if (world == null || party == null || !party.HasActive)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid party enter args.");
            if (world.PlayerPartyTravel != null && world.PlayerPartyTravel.IsMoving)
                return Result.Failure(ErrorCode.InvalidOperation, "Stop travel before entering local view.");

            if (!PlayerPartyWorldLocationQuery.TryResolve(world, party, out var resolved))
                return Result.Failure(ErrorCode.InvalidOperation, "PlayerParty has no world location.");

            if (resolved.LocationKind == PlayerPartyLocationKind.AtWorldSite)
            {
                if (!world.Strategic.Sites.TryGet(resolved.SiteId, out var focusSite) || focusSite == null)
                    return Result.Failure(ErrorCode.NotFound, "WorldSite missing.", resolved.SiteId);

                // Peek：已在该 Site LocalMap → 不重进、不重置 LocalPosition。
                if (string.Equals(world.PartyWorld?.SiteId, focusSite.SiteId, System.StringComparison.Ordinal) &&
                    string.Equals(
                        world.PartyWorld?.LocalMapId?.Trim() ?? string.Empty,
                        focusSite.LocalMapId?.Trim() ?? string.Empty,
                        System.StringComparison.Ordinal))
                {
                    world.PlayerPartyTravel.SetAtWorldSite(
                        focusSite.SiteId,
                        focusSite.PresenceHex,
                        world.HexWorld != null && world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f);
                    ApplyTravelingMembersAtSite(world, focusSite.SiteId);
                    PlayerPartyWorldLocationDebug.LogTransition(world, party, "EnterLocalView.SitePeekRestore");
                    return Result.Success();
                }

                return EnterWorldSiteAsParty(world, party, focusSite);
            }

            // AtWorldPosition：唯一依据 DerivedHex → Terrain Fallback。
            var hex = resolved.DerivedHex;
            if (!WildernessLocalMapFallback.TryResolve(world, hex, out var mapId) ||
                string.IsNullOrEmpty(mapId))
                return Result.Failure(ErrorCode.InvalidOperation, "No wilderness fallback LocalMap for hex.");

            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            EnsureMotionHasContinuousStart(world, hex);
            ApplyTravelingMembersPresence(world);
            PlayerPartyWorldLocationDebug.LogTransition(world, party, "EnterLocalView.Wilderness");
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
            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            world.PlayerPartyTravel.SetAtWorldSite(site.SiteId, site.PresenceHex, hexSize);
            ApplyTravelingMembersAtSite(world, site.SiteId);
            PlayerPartyWorldLocationDebug.LogTransition(world, party, "EnterWorldSiteAsParty");
            return WorldTravelService.EnterWorldSiteScene(world, site.SiteId, string.Empty);
        }

        /// <summary>关闭 WorldMap／中断 AutoTravel：保留连续位置并解析近景。</summary>
        public static Result CloseWorldMapTakeover(
            SimulationWorld world,
            PlayerPartyRuntime party)
        {
            if (world == null || party == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid takeover args.");

            if (world.PlayerPartyTravel != null && world.PlayerPartyTravel.IsMoving)
                CancelTravel(world, party);

            PlayerPartyWorldLocationDebug.LogTransition(world, party, "CloseWorldMapTakeover");
            return EnterLocalViewAtCurrentHex(world, party);
        }

        /// <summary>
        /// 若权威位置与当前 PartyWorld LocalMap 已一致，关闭地图无需 Expand／Materialize。
        /// </summary>
        public static bool PartyLocalMapMatchesAuthoritativeLocation(
            SimulationWorld world,
            PlayerPartyRuntime party)
        {
            if (!PlayerPartyWorldLocationQuery.TryResolve(world, party, out var resolved))
                return false;
            var focusMap = world.PartyWorld?.LocalMapId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(focusMap) || string.IsNullOrEmpty(resolved.ResolvedLocalMapId))
                return false;
            if (!string.Equals(focusMap, resolved.ResolvedLocalMapId.Trim(), System.StringComparison.Ordinal))
                return false;

            if (resolved.LocationKind == PlayerPartyLocationKind.AtWorldSite)
            {
                return string.Equals(
                    world.PartyWorld?.SiteId,
                    resolved.SiteId,
                    System.StringComparison.Ordinal);
            }

            return string.IsNullOrEmpty(world.PartyWorld?.SiteId);
        }

        static void EnsureMotionHasContinuousStart(SimulationWorld world, HexCoord startHex)
        {
            var motion = world.PlayerPartyTravel;
            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite)
            {
                // BeginAutoTravel 将把 Site 转为 Presence 中心上的 AtWorldPosition。
                if (world.Strategic.Sites.TryResolveSitePresenceHex(motion.SiteId, out var presence))
                    motion.SetAtWorldSite(motion.SiteId, presence, hexSize);
                else
                    motion.SetAtWorldSite(motion.SiteId, startHex, hexSize);
                return;
            }

            if (!motion.HasPosition)
                motion.SnapToHexCenter(startHex, hexSize);
        }

        static HexCoord ResolveDeterministicSiteApproachHex(
            SimulationWorld world,
            HexCoord from,
            WorldSite site)
        {
            HexCoord best = site.PresenceHex;
            var bestDist = int.MaxValue;
            foreach (var hex in site.EnumerateFootprintHexes())
            {
                if (!world.HexWorld.TryGetTile(hex, out var tile) || tile == null || !tile.IsPassable)
                    continue;
                var d = HexMath.Distance(from, hex);
                if (d < bestDist ||
                    (d == bestDist && (hex.Q < best.Q || (hex.Q == best.Q && hex.R < best.R))))
                {
                    bestDist = d;
                    best = hex;
                }
            }

            return best;
        }

        static void ApplyTravelingMembersPresence(SimulationWorld world)
        {
            var motion = world.PlayerPartyTravel;
            if (motion == null)
                return;
            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(motion.SiteId))
            {
                ApplyTravelingMembersAtSite(world, motion.SiteId);
                return;
            }

            ApplyTravelingMembersAtHex(world, motion.CurrentHex);
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
            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;
            world.PlayerPartyTravel?.SnapToHexCenter(hex, hexSize);
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
            if (world.Strategic.Sites.TryResolveSitePresenceHex(siteId, out var presence))
            {
                var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                    ? world.HexWorld.HexSize
                    : 1f;
                world.PlayerPartyTravel?.SetAtWorldSite(siteId, presence, hexSize);
            }

            ApplyTravelingMembersAtSite(world, siteId);
        }
    }
}
