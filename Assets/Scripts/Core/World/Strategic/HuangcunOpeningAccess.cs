using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.Schedule;
using XianXia.Core.Results;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Opening basin and its inner village wall, rebound from authored Surface walls.</summary>
    public sealed class HuangcunOpeningArea
    {
        public string SurfaceId { get; private set; } = string.Empty;
        public float MinX { get; private set; }
        public float MinY { get; private set; }
        public float MaxX { get; private set; }
        public float MaxY { get; private set; }
        public float CellSize { get; private set; }
        public float VillageMinX { get; private set; }
        public float VillageMinY { get; private set; }
        public float VillageMaxX { get; private set; }
        public float VillageMaxY { get; private set; }
        readonly System.Collections.Generic.List<WorldVec2> _boundary = new System.Collections.Generic.List<WorldVec2>();
        public WorldVec2 InnerGate { get; private set; }
        public WorldVec2 OuterGate { get; private set; }
        public bool IsConfigured => !string.IsNullOrEmpty(SurfaceId) && MaxX > MinX && MaxY > MinY;

        // Session-only development override; deliberately absent from Snapshot and story flags.
        public bool DebugBypassMovementRestriction { get; set; }

        public void Clear() { SurfaceId = string.Empty; _boundary.Clear(); DebugBypassMovementRestriction = false; }

        public void Configure(string surfaceId, float minX, float minY, float maxX, float maxY, float cellSize)
        {
            SurfaceId = surfaceId ?? string.Empty;
            VillageMinX = minX; VillageMinY = minY;
            VillageMaxX = maxX; VillageMaxY = maxY;
            var extraX = (maxX - minX) * 1.5f;
            var extraY = (maxY - minY) * 1.5f;
            MinX = minX - extraX; MinY = minY - extraY;
            MaxX = maxX + extraX; MaxY = maxY + extraY; CellSize = cellSize;
            _boundary.Clear();
            InnerGate = new WorldVec2(MaxX - CellSize * 30f, (MinY + MaxY) * .5f);
            OuterGate = new WorldVec2(MaxX + CellSize * 125f, InnerGate.Y);
        }

        // The authored closed basin can be irregular and is independent of the village's level.
        public void ConfigureBasin(System.Collections.Generic.IReadOnlyList<WorldVec2> boundary,
            WorldVec2 innerGate, WorldVec2 outerGate)
        {
            _boundary.Clear(); _boundary.AddRange(boundary);
            MinX = MinY = float.PositiveInfinity; MaxX = MaxY = float.NegativeInfinity;
            foreach (var p in _boundary)
            { MinX = Math.Min(MinX, p.X); MaxX = Math.Max(MaxX, p.X); MinY = Math.Min(MinY, p.Y); MaxY = Math.Max(MaxY, p.Y); }
            InnerGate = innerGate; OuterGate = outerGate;
        }

        public bool ContainsVillage(string surfaceId, float x, float y) =>
            IsConfigured && string.Equals(SurfaceId, surfaceId, StringComparison.Ordinal) &&
            x >= VillageMinX && x <= VillageMaxX && y >= VillageMinY && y <= VillageMaxY;

        public bool Contains(string surfaceId, float x, float y)
        {
            if (!IsConfigured || !string.Equals(SurfaceId, surfaceId, StringComparison.Ordinal)) return false;
            if (_boundary.Count >= 3)
            {
                var inside = false;
                for (var i = 0; i < _boundary.Count; i++)
                {
                    var a = _boundary[i]; var b = _boundary[(i + 1) % _boundary.Count];
                    if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
                        inside = !inside;
                }
                return inside;
            }
            // Half a cell keeps the entire one-cell footprint inside, not just its centre.
            var inset = CellSize * .5f;
            return IsConfigured && string.Equals(SurfaceId, surfaceId, StringComparison.Ordinal) &&
                x >= MinX + inset && y >= MinY + inset && x <= MaxX - inset && y <= MaxY - inset;
        }
    }

    /// <summary>ADR-0057. The basin is initially isolated; the village wall has a separate patrol gate.</summary>
    public static class HuangcunOpeningAccess
    {
        public const string ConfinedFlag = "story:huangcun_confined_opening";
        public const string UnlockedFlag = "story:huangcun_world_unlocked";
        public const string BlockedMessage = "荒村周边的天险尚不能通过；取得荒村后才能前往外界。";
        public const string PatrolBlockedMessage = "巡卫正在附近巡视，暂时不能擅自出村。";
        public const string ManagementLockedMessage = "取得荒村后，才能经营领地与建造。";
        public const string FacilityRestrictedMessage = "占领荒村前，练功室、斗技坊与功法阁只供主管和巡卫使用。";
        static readonly string[] RestrictedFacilityRoomIds =
        {
            "base:site_huangcun:practice_house", "base:site_huangcun:manual_hall", "base:site_huangcun:combat_hall"
        };

        public static bool IsRestrictedFacilityLocation(string locationId) =>
            locationId == "base:loc_huangcun_practice" || locationId == "base:loc_huangcun_manual_hall" ||
            locationId == "base:loc_huangcun_combat_hall";

        public static bool IsOpeningStaff(SimulationWorld world, EntityId id)
        {
            if (world == null || !world.Entities.TryGet(id, out var entity)) return false;
            var definition = entity.DefinitionId.ToString();
            return definition == "base:character_ch01_ref_supervisor" ||
                definition == "base:character_ch01_ref_guard_a" || definition == "base:character_ch01_ref_guard_b";
        }

        public static Result ValidateFacilityUse(SimulationWorld world, EntityId id, string locationId) =>
            !IsLocked(world) || !IsRestrictedFacilityLocation(locationId) || IsOpeningStaff(world, id)
                ? Result.Success() : Result.Failure(ErrorCode.ActionCannotStart, FacilityRestrictedMessage);

        public static bool AllowsFacilityPosition(SimulationWorld world, EntityId id, string surfaceId, float x, float y)
        {
            if (!IsLocked(world) || IsOpeningStaff(world, id)) return true;
            for (var i = 0; i < RestrictedFacilityRoomIds.Length; i++)
                if (world.OutdoorConstructedAssets.Assets.TryGetValue(RestrictedFacilityRoomIds[i], out var room) &&
                    room.SurfaceId == surfaceId && room.Kind == "room" &&
                    x >= room.WorldX && x < room.WorldX+room.WorldWidth &&
                    y >= room.WorldY && y < room.WorldY+room.WorldHeight)
                    return false;
            return true;
        }

        public static Result ValidateCurrentFacilityUse(SimulationWorld world, EntityId id)
        {
            if (!IsLocked(world) || world.LocalMap.IsInInterior ||
                CharacterEncounterService.OwnsParticipantSpatialState(world, id)) return Result.Success();
            return CharacterWorldPresenceQuery.TryResolve(world, id, out var at) && at.HasWorldPosition &&
                !AllowsFacilityPosition(world, id, at.SurfaceId, at.WorldPosition.X, at.WorldPosition.Y)
                    ? Result.Failure(ErrorCode.ActionCannotStart, FacilityRestrictedMessage) : Result.Success();
        }

        public static bool IsLocked(SimulationWorld world) =>
            world != null && world.Flags.Has(ConfinedFlag) && !world.Flags.Has(UnlockedFlag) &&
            !OwnsVillage(world);

        static bool OwnsVillage(SimulationWorld world) =>
            world.Strategic.Sites.TryGet(Ch01ScenarioProgressionHooks.HuangcunSiteId, out var site) &&
            string.Equals(site.OwnerFactionId, world.Strategic.PlayerFactionId, StringComparison.Ordinal);

        public static void RefreshUnlock(SimulationWorld world)
        {
            if (world == null || !world.Flags.Has(ConfinedFlag) || world.Flags.Has(UnlockedFlag) ||
                !OwnsVillage(world)) return;
            world.Flags.Set(UnlockedFlag);
            world.Flags.Set(Ch01ScenarioProgressionHooks.FlagPlayerFactionPoliticallyActive);
        }

        public static bool IsMovementLocked(SimulationWorld world) =>
            IsLocked(world) && !world.OpeningArea.DebugBypassMovementRestriction;

        public static bool AllowsPosition(SimulationWorld world, string surfaceId, float x, float y) =>
            !IsMovementLocked(world) || world.OpeningArea.Contains(surfaceId, x, y);

        public static string PlayerMoveBlockedMessage(SimulationWorld world, string surfaceId, float x, float y) =>
            !AllowsPosition(world, surfaceId, x, y) ? BlockedMessage :
            !AllowsFacilityPosition(world, world?.Strategic.PlayerPartyContext?.ActiveCharacterId ?? EntityId.None,
                surfaceId, x, y) ? FacilityRestrictedMessage : PatrolBlockedMessage;

        /// <summary>Only an outward crossing can be stopped. Once outside, patrols never strand the player.</summary>
        public static bool AllowsPlayerMove(SimulationWorld world, string surfaceId,
            float fromX, float fromY, float toX, float toY)
        {
            if (!AllowsPosition(world, surfaceId, toX, toY)) return false;
            if (!AllowsFacilityPosition(world, world?.Strategic.PlayerPartyContext?.ActiveCharacterId ?? EntityId.None,
                    surfaceId, toX, toY)) return false;
            if (!IsMovementLocked(world) || world.OpeningArea.DebugBypassMovementRestriction ||
                !world.OpeningArea.ContainsVillage(surfaceId, fromX, fromY) ||
                world.OpeningArea.ContainsVillage(surfaceId, toX, toY)) return true;
            var area = world.OpeningArea;
            var fraction = 1f;
            var stepX = toX - fromX;
            var stepY = toY - fromY;
            if (stepX > 0f) fraction = Math.Min(fraction, (area.VillageMaxX - fromX) / stepX);
            else if (stepX < 0f) fraction = Math.Min(fraction, (area.VillageMinX - fromX) / stepX);
            if (stepY > 0f) fraction = Math.Min(fraction, (area.VillageMaxY - fromY) / stepY);
            else if (stepY < 0f) fraction = Math.Min(fraction, (area.VillageMinY - fromY) / stepY);
            fraction = Math.Max(0f, Math.Min(1f, fraction));
            var exitX = fromX + stepX * fraction;
            var exitY = fromY + stepY * fraction;
            var watchRadius = world.OpeningArea.CellSize * XianXia.Core.Social.SupervisorAngerDriver.ObservationRangeCells;
            var radiusSquared = watchRadius * watchRadius;
            foreach (var entity in world.Entities.All)
            {
                if (!XianXia.Core.Social.SupervisorAngerDriver.IsOnWatch(world, entity) ||
                    !CharacterWorldPresenceQuery.TryResolve(world, entity.Id, out var at) ||
                    !at.HasWorldPosition || !string.Equals(at.SurfaceId, surfaceId, StringComparison.Ordinal))
                    continue;
                var dx = at.WorldPosition.X - exitX;
                var dy = at.WorldPosition.Y - exitY;
                if (dx * dx + dy * dy <= radiusSquared) return false;
            }
            return true;
        }

        /// <summary>The old formation is a local, exact-position Surface handoff after liberation.</summary>
        public static Result UseSpiritGate(SimulationWorld world, PlayerPartyRuntime party)
        {
            if (world == null || party == null || !party.HasActive ||
                world.OpeningArea == null || !world.OpeningArea.IsConfigured ||
                world.LocalMap.IsInInterior ||
                CharacterEncounterService.BlocksOrdinaryContinuousSurface(world) || IsLocked(world))
                return Result.Failure(ErrorCode.InvalidOperation, "取得荒村后，才能启用城外传送灵阵。");
            var motion = world.PlayerPartyTravel;
            var area = world.OpeningArea;
            if (motion == null || !motion.HasPosition || motion.IsMoving ||
                !string.Equals(motion.SurfaceId, area.SurfaceId, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "需要在荒村城外的灵阵前停下。");
            var innerGate = area.InnerGate;
            var outerGate = area.OuterGate;
            var radius = area.CellSize * 10f;
            var innerDx = motion.WorldPosition.X - innerGate.X;
            var innerDy = motion.WorldPosition.Y - innerGate.Y;
            var outerDx = motion.WorldPosition.X - outerGate.X;
            var outerDy = motion.WorldPosition.Y - outerGate.Y;
            var atInner = innerDx * innerDx + innerDy * innerDy <= radius * radius;
            var atOuter = outerDx * outerDx + outerDy * outerDy <= radius * radius;
            if (!atInner && !atOuter)
                return Result.Failure(ErrorCode.InvalidOperation, "请靠近荒村山崖两侧的传送灵阵。");
            var destination = atInner ? outerGate : innerGate;
            if (!world.SurfaceGround.TryGet(area.SurfaceId, out var navigation) ||
                !navigation.IsWalkable(destination.X, destination.Y))
                return Result.Failure(ErrorCode.InvalidOperation, "灵阵另一端的落脚点暂不可用。");
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
            motion.SetAtSurfacePosition(area.SurfaceId, destination);
            PlayerPartyTransitionMembership.ReconcilePlayerPartyMemberWorldPresenceFromMotion(world, party);
            return Result.Success();
        }

        public static bool IsSimulated(SimulationWorld world, Entity entity)
        {
            if (entity == null) return false;
            if (!IsLocked(world)) return true;
            if (world.Strategic.PlayerPartyContext?.IsMember(entity.Id) == true ||
                CharacterEncounterService.OwnsParticipantSpatialState(world, entity.Id) ||
                SeparateSpaceCombatPolicy.IsEntityInActiveSpace(world, entity.Id)) return true;
            if (!CharacterWorldPresenceQuery.TryResolve(world, entity.Id, out var at)) return false;
            return at.HasWorldPosition
                ? world.OpeningArea.Contains(at.SurfaceId, at.WorldPosition.X, at.WorldPosition.Y)
                : string.Equals(at.SiteId, Ch01ScenarioProgressionHooks.HuangcunSiteId, StringComparison.Ordinal);
        }

        public static bool IsSimulated(SimulationWorld world, EntityId id) =>
            world != null && world.Entities.TryGet(id, out var entity) && IsSimulated(world, entity);

        /// <summary>Exclude suspended time from existing elapsed-time consumers, without advancing gameplay.</summary>
        public static void KeepDormantTimeCurrent(SimulationWorld world, Entity entity)
        {
            var now = world.Tick.Value;
            if (entity.TryGet<PersonalLifeComponent>(out var personal)) personal.LastProcessedTick = now;
            if (world.Civilians.TryGet(entity.Id, out var civilian))
            {
                civilian.LastUpdateTick = now;
                civilian.BackgroundNeedsLastProcessedTick = now;
                civilian.CareDayIndex = now / Domain.Time.WorldTick.TicksPerDay;
            }
            if (world.BackgroundCharacterTravel.TryGet(entity.Id, out var motion) && motion != null)
                motion.LastProcessedWorldTick = now;
            // Existing decay timestamps are absolute WorldTicks; shift them while suspended.
            if (entity.TryGet<LifecycleComponent>(out var life) && life.BleedOutAfterTick > 0)
                life.BleedOutAfterTick++;
            if (entity.TryGet<Combat.CorpseComponent>(out var corpse) && corpse.RemoveAfterTick > 0)
                corpse.RemoveAfterTick++;
        }

        public static void HoldExternalMarkets(SimulationWorld world)
        {
            foreach (var house in world.Commerce.AuctionHouses.Values)
                foreach (var listing in house.Listings.Values)
                    if (listing.Status == Inventory.AuctionListingStatus.Open)
                    {
                        listing.CreatedTick++;
                        listing.EndTick++;
                        listing.NextMarketReviewTick++;
                    }
        }
    }
}
