using System;
using System.Collections.Generic;
using XianXia.Core.Actions;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Orders;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Surface;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    public enum MortalProfession { Unassigned = 0, Farmer = 1, HerbFarmer = 2, Logger = 3, Medic = 4, Warden = 5 }
    public enum MortalActivity
    {
        Idle = 0, Eat = 1, SleepAtResidence = 2, SleepOnGround = 3,
        FarmerWork = 4, HerbFarmerWork = 5, Logging = 6, MedicCare = 7,
        Rescue = 8, Haul = 9, Construction = 10, Flee = 11,
        Escorted = 12, Detained = 13, FetchFood = 14, DedicatedDuty = 15, Chess = 16, PrisonCare = 17
    }
    public enum CivilianDisposition
    {
        Normal = 0, SurrenderWaiting = 1, Fleeing = 2, Displaced = 3,
        CapturedEscorted = 4, Detained = 5
    }
    public enum ResidenceUsage { Normal = 0, PrisonerOnly = 1 }
    public enum FactionMortalSchedulePhase
    {
        Work = 0, Rest = 1, Free = 2, DedicatedDuty = 3, Missing = 4
    }
    public enum FoodFetchPhase
    {
        None = 0, NeedFood = 1, NoFoodSource = 2, NoStorage = 3,
        AccessDenied = 4, WaitingForPath = 5, Traveling = 6,
        PathUnavailable = 7, Arrived = 8, Eating = 9, NoFoodAvailable = 10
    }
    public enum CivilianSleepPhase
    {
        None = 0, TravelingToResidence = 1, SleepingAtResidence = 2, SleepingOnGround = 3
    }

    public static class MortalCivilianTuning
    {
        public const int DefaultNeed = 100;
        public const int HungerThreshold = 40;
        public const int GroundSleepThreshold = 10;
        public const int SleepThreshold = 35;
        public const int WakeThreshold = 85;
        public const int MealRestore = 55;
        public const int BedSleepRestoreEveryTicks = 1;
        public const int GroundSleepRestoreEveryTicks = 4;
        public const int SatietyDecayEveryTicks = 4;
        public const int EnergyDecayEveryTicks = 5;
        public const int DetainedCareLoyaltyLossPerDay = 5;
        public const int SurrenderLoyaltyThreshold = 15;
        public const float FleeMoveSpeedMultiplier = 0.2f;
        public const float FleeBoundaryBufferCells = 3f;
        public const int FleeCandidateCount = 8;
        public const ulong FleeRetryDelayTicks = 8UL;
        public const int LocalContinuityRadiusChunks = ContinuousSurfaceStreamingPolicy.ActiveDiameterChunks;
        public const ulong RescueRetryDelayTicks = 8UL;
    }

    public static class FactionMortalSchedule
    {
        public static FactionMortalSchedulePhase Current(
            SimulationWorld world, Entity entity, out string scheduleId,
            out ScheduleBlock block, out string reason)
        {
            scheduleId = string.Empty;
            block = null;
            reason = string.Empty;
            if (world == null || entity == null ||
                !entity.TryGet<ScheduleComponent>(out var schedule) ||
                string.IsNullOrWhiteSpace(schedule.DefinitionId))
            { reason = "未绑定日程"; return FactionMortalSchedulePhase.Missing; }
            scheduleId = schedule.DefinitionId;
            if (!world.TryGetSchedule(scheduleId, out var definition))
            { reason = "日程配置缺失：" + scheduleId; return FactionMortalSchedulePhase.Missing; }
            if (!definition.TryResolve(world.Tick, out block))
            { reason = "当前时间未被日程覆盖：" + scheduleId; return FactionMortalSchedulePhase.Missing; }
            switch (block.Activity)
            {
                case ScheduleActivity.Rest: return FactionMortalSchedulePhase.Rest;
                case ScheduleActivity.Labor: return FactionMortalSchedulePhase.Work;
                case ScheduleActivity.Idle: return FactionMortalSchedulePhase.Free;
                default:
                    reason = "专职日程交由既有职责 action 执行：" + block.Activity;
                    return FactionMortalSchedulePhase.DedicatedDuty;
            }
        }
    }

    public sealed class MortalCivilianState
    {
        public EntityId EntityId { get; set; }
        public int Satiety { get; set; } = MortalCivilianTuning.DefaultNeed;
        public int Energy { get; set; } = MortalCivilianTuning.DefaultNeed;
        public MortalProfession Profession { get; set; }
        public MortalActivity Activity { get; set; }
        public CivilianDisposition Disposition { get; set; }
        public string ResidenceWorkAreaId { get; set; } = string.Empty;
        public string CurrentSiteId { get; set; } = string.Empty;
        public ulong LastUpdateTick { get; set; }
        public ulong SleepStartedTick { get; set; }
        public CivilianSleepPhase SleepPhase { get; set; }
        public ulong FleeStartedTick { get; set; }
        public bool HasFleeTarget { get; set; }
        public float FleeTargetX { get; set; }
        public float FleeTargetY { get; set; }
        public string FleeSourceSiteId { get; set; } = string.Empty;
        public string RefugeTargetSiteId { get; set; } = string.Empty;
        public ulong BackgroundNeedsLastProcessedTick { get; set; }
        public int FleeCandidateIndex { get; set; }
        public int FleeAttemptCount { get; set; }
        public string FleeStatus { get; set; } = string.Empty;
        public EntityId CarrierId { get; set; }
        public string DetainedResidenceWorkAreaId { get; set; } = string.Empty;
        public ulong CareDayIndex { get; set; }
        public bool AteToday { get; set; }
        public bool SleptToday { get; set; }
        public ulong LastWorkOutputTick { get; set; }
        public EntityId PendingCaptureCarrierId { get; set; }
        // Rescue transport is persisted together with the real bed reservation and world positions.
        public EntityId RescueTargetId { get; set; }
        public bool RescueCarrying { get; set; }
        public string RescueDestinationId { get; set; } = string.Empty;
        public string RescueRejectedDestinationId { get; set; } = string.Empty;
        public EntityId RescueRejectedTargetId { get; set; }
        public ulong RescueRetryAfterTick { get; set; }
        public string RescueStatus { get; set; } = string.Empty;
        public EntityId MedicTargetId { get; set; }
        public bool MedicCarryingHerb { get; set; }
        public string MedicSourceSiteId { get; set; } = string.Empty;
        public string MedicSourceRoomId { get; set; } = string.Empty;
        public EntityId MedicSourcePartyMemberId { get; set; }
        public int MedicTreatmentSeconds { get; set; }
        public ulong MedicLastTreatmentTick { get; set; }
        public ulong MedicRetryAfterTick { get; set; }
        public string MedicStatus { get; set; } = string.Empty;
        public EntityId WardenTargetId { get; set; }
        public string WardenCarriedResourceId { get; set; } = string.Empty;
        public string WardenSourceSiteId { get; set; } = string.Empty;
        public string WardenSourceRoomId { get; set; } = string.Empty;
        public string WardenStatus { get; set; } = string.Empty;
        public ulong WardenRetryAfterTick { get; set; }
        public readonly Dictionary<ulong, ulong> WardenTargetRetryAfter = new Dictionary<ulong, ulong>();
        // Session-only food trip. Stock remains SitePublicStock authority and is removed only on arrival.
        public string FoodFetchStorageRoomId { get; set; } = string.Empty;
        public string FoodFetchSourceSiteId { get; set; } = string.Empty;
        public string PendingFoodResourceId { get; set; } = string.Empty;
        public string FoodFetchStatus { get; set; } = string.Empty;
        public FoodFetchPhase FoodFetchPhase { get; set; }
        public ulong FoodFetchRetryAfterTick { get; set; }
        public string FoodFetchFailureSignature { get; set; } = string.Empty;
        public bool HasFoodFetchAccessPoint { get; set; }
        public float FoodFetchAccessWorldX { get; set; }
        public float FoodFetchAccessWorldY { get; set; }
        public int FoodFetchAccessCandidateIndex { get; set; } = -1;
        public int FoodFetchAccessAttemptCount { get; set; }
        public ulong MealStartedTick { get; set; }
        // Runtime diagnostic only. The ScheduleDefinition remains the authority.
        public string ScheduleStatus { get; set; } = string.Empty;

        public void Clamp()
        {
            Satiety = Clamp100(Satiety);
            Energy = Clamp100(Energy);
        }

        static int Clamp100(int value) => value < 0 ? 0 : value > 100 ? 100 : value;
    }

    /// <summary>Single runtime authority for mortal needs, occupation and captivity.</summary>
    public sealed class MortalCivilianBoard
    {
        readonly Dictionary<EntityId, MortalCivilianState> _states =
            new Dictionary<EntityId, MortalCivilianState>();
        readonly Dictionary<string, ResidenceUsage> _residenceUsage =
            new Dictionary<string, ResidenceUsage>(StringComparer.Ordinal);

        public IReadOnlyDictionary<EntityId, MortalCivilianState> All => _states;
        public IReadOnlyDictionary<string, ResidenceUsage> ResidenceUsages => _residenceUsage;

        public MortalCivilianState GetOrCreate(EntityId id)
        {
            if (id.IsNone) throw new ArgumentException("EntityId required.");
            if (_states.TryGetValue(id, out var state)) return state;
            state = new MortalCivilianState { EntityId = id };
            _states.Add(id, state);
            return state;
        }

        public bool TryGet(EntityId id, out MortalCivilianState state)
        {
            state = null;
            return !id.IsNone && _states.TryGetValue(id, out state);
        }

        public void Remove(EntityId id) => _states.Remove(id);
        public void Clear() { _states.Clear(); _residenceUsage.Clear(); }
        public ResidenceUsage GetResidenceUsage(string id) =>
            !string.IsNullOrEmpty(id) && _residenceUsage.TryGetValue(id, out var usage)
                ? usage : ResidenceUsage.Normal;
        public void SetResidenceUsage(string id, ResidenceUsage usage)
        {
            if (!string.IsNullOrWhiteSpace(id)) _residenceUsage[id] = usage;
        }
    }

    public static class MortalCivilianQuery
    {
        public static bool IsMortal(Entity entity) =>
            entity != null && entity.TryGet<CultivationComponent>(out var cultivation) &&
            cultivation.Realm == RealmStage.Mortal;

        public static bool IsManagedCivilian(SimulationWorld world, Entity entity) =>
            world != null && entity != null && (entity.Tags & EntityTag.Npc) != 0 &&
            IsMortal(entity) && !world.QuestCompanions.TryGet(entity.Id, out _);

        public static bool IsPlayerFactionManageable(SimulationWorld world, Entity entity)
        {
            if (!IsManagedCivilian(world, entity) ||
                !entity.TryGet<FactionMembershipComponent>(out var membership) || !membership.IsAffiliated)
                return false;
            return string.Equals(membership.FactionId, world.Strategic.PlayerFactionId, StringComparison.Ordinal);
        }

        public static string ResolveCurrentSiteId(SimulationWorld world, EntityId id)
        {
            if (world == null || id.IsNone) return string.Empty;
            if (CharacterWorldPresenceQuery.TryResolve(world, id, out var presence))
            {
                if (!string.IsNullOrEmpty(presence.SiteId)) return presence.SiteId;
                if (presence.HasWorldPosition && WorldSitePhysicalRegionQuery.TryResolve(world, presence.WorldPosition, out var site))
                    return site.SiteId;
            }
            return string.Empty;
        }

        public static bool TryFindProfessionArea(SimulationWorld world, Entity entity, string tag,
            out WorkAreaDefinition area)
        {
            area = null;
            if (world == null || entity == null ||
                !entity.TryGet<FactionMembershipComponent>(out var membership) || !membership.IsAffiliated)
                return false;
            var ids = new List<string>();
            if (entity.TryGet<ActivityTendencyComponent>(out var tendency))
                for (var i = 0; i < tendency.PreferredWorkAreaIds.Count; i++)
                    if (!ids.Contains(tendency.PreferredWorkAreaIds[i])) ids.Add(tendency.PreferredWorkAreaIds[i]);
            var all = new List<string>(world.WorkAreas.Keys); all.Sort(StringComparer.Ordinal);
            for (var i = 0; i < all.Count; i++) if (!ids.Contains(all[i])) ids.Add(all[i]);
            for (var i = 0; i < ids.Count; i++)
            {
                if (!world.TryGetWorkArea(ids[i], out var candidate) ||
                    !MortalActivityEvaluator.HasTag(candidate, tag) ||
                    !TryResolveAuthorizedLocalAreaSite(world, entity.Id, membership.FactionId,
                        candidate.LocationId, out _)) continue;
                area = candidate;
                return true;
            }
            return false;
        }

        public static bool IsWithinLocalContinuity(
            SimulationWorld world, string surfaceId, WorldVec2 a, WorldVec2 b)
        {
            if (world == null || string.IsNullOrWhiteSpace(surfaceId) ||
                !world.SurfaceSpatial.TryGet(surfaceId, out var metric) || metric == null) return false;
            var radius = Math.Max(metric.ChunkWidth, metric.ChunkHeight) *
                         MortalCivilianTuning.LocalContinuityRadiusChunks;
            var dx = a.X - b.X; var dy = a.Y - b.Y;
            return dx * dx + dy * dy <= radius * radius;
        }

        static bool TryResolveAuthorizedLocalAreaSite(
            SimulationWorld world, EntityId characterId, string factionId, string locationId, out string siteId)
        {
            siteId = string.Empty;
            if (!CharacterWorldPresenceQuery.TryResolve(world, characterId, out var presence) ||
                !presence.HasWorldPosition) return false;
            var ids = new List<string>();
            var current = ResolveCurrentSiteId(world, characterId);
            if (!string.IsNullOrEmpty(current)) ids.Add(current);
            foreach (var candidate in world.Strategic.Sites.Sites)
                if (!ids.Contains(candidate.Key)) ids.Add(candidate.Key);
            for (var i = 0; i < ids.Count; i++)
            {
                if (!world.Strategic.Sites.TryGet(ids[i], out var site) || site == null || !site.IsCoreActive ||
                    !string.Equals(site.OwnerFactionId, factionId, StringComparison.Ordinal) ||
                    !string.Equals(site.CoreSurfaceId, presence.SurfaceId, StringComparison.Ordinal) ||
                    !world.ContinuousOutdoorMaterialization.TryGetPlace(ids[i], locationId, out var place)) continue;
                var mapper = world.SurfaceSpatial.TryGet(site.CoreSurfaceId, out var metric) && metric != null
                    ? new OutdoorSurfaceCoordinateMapper(metric.ChunkWidth, metric.ChunkHeight, metric.CellSize,
                        presentationUnitsPerWorldUnit: 1f / metric.CellSize,
                        originWorldX: metric.OriginWorldX, originWorldY: metric.OriginWorldY)
                    : null;
                if (mapper == null) continue;
                mapper.PresentationToWorld(place.PresentationX, place.PresentationZ, out var x, out var y);
                if (!IsWithinLocalContinuity(world, presence.SurfaceId, presence.WorldPosition, new WorldVec2(x, y))) continue;
                siteId = ids[i];
                return true;
            }
            return false;
        }

        public static bool TryResolveWorkAreaWorldCenter(
            SimulationWorld world, string siteId, WorkAreaDefinition area,
            out string surfaceId, out WorldVec2 center, out float cellSize)
        {
            surfaceId = string.Empty; center = default; cellSize = 0f;
            if (world == null || area == null || string.IsNullOrEmpty(siteId) ||
                !world.Strategic.Sites.TryGet(siteId, out var site) || site == null || !site.HasContinuousCore ||
                !world.ContinuousOutdoorMaterialization.TryGetPlace(siteId, area.LocationId, out var place) ||
                !world.SurfaceSpatial.TryGet(site.CoreSurfaceId, out var metric) || metric == null)
                return false;
            var mapper = new OutdoorSurfaceCoordinateMapper(
                metric.ChunkWidth, metric.ChunkHeight, metric.CellSize,
                presentationUnitsPerWorldUnit: 1f / metric.CellSize,
                originWorldX: metric.OriginWorldX, originWorldY: metric.OriginWorldY);
            mapper.PresentationToWorld(place.PresentationX + area.OffsetX,
                place.PresentationZ + area.OffsetZ, out var x, out var y);
            surfaceId = site.CoreSurfaceId;
            center = new WorldVec2(x, y);
            cellSize = metric.CellSize;
            return true;
        }

        public static bool IsAtWorkArea(
            SimulationWorld world, EntityId characterId, string siteId,
            WorkAreaDefinition area, float rangeCells = 4f)
        {
            return TryResolveWorkAreaWorldCenter(world, siteId, area,
                       out var surfaceId, out var center, out var cellSize) &&
                   CharacterWorldPresenceQuery.TryResolve(world, characterId, out var presence) &&
                   presence.HasWorldPosition &&
                   string.Equals(presence.SurfaceId, surfaceId, StringComparison.Ordinal) &&
                   DistanceSquared(presence.WorldPosition, center) <=
                       cellSize * Math.Max(1f, rangeCells) * cellSize * Math.Max(1f, rangeCells);
        }

        static float DistanceSquared(WorldVec2 a, WorldVec2 b)
        { var x = a.X - b.X; var y = a.Y - b.Y; return x * x + y * y; }
    }

    public enum CivilianMovementOwner
    {
        None = 0,
        Battle = 1,
        SeparateSpace = 2,
        PlayerParty = 3,
        ActiveSquadTravel = 4,
        CivilianLocal = 5,
        StaticSquad = 6,
        BackgroundTravel = 7
    }

    /// <summary>
    /// One policy shared by Core intent production and Host spatial consumers. A stationary
    /// resident squad is placement context; it does not suppress managed-civilian local life.
    /// </summary>
    public static class MortalCivilianMovementAuthority
    {
        public static CivilianMovementOwner Resolve(
            SimulationWorld world, EntityId characterId, MortalCivilianState civilianState)
        {
            if (world == null || characterId.IsNone) return CivilianMovementOwner.None;
            if (CharacterEncounterService.OwnsParticipantSpatialState(world, characterId) ||
                ActualBattleParticipantQuery.TryFind(world.Strategic.Participants, characterId, out _))
                return CivilianMovementOwner.Battle;
            if (SeparateSpaceTransitionService.IsOwnedByActiveSeparateSpace(world, characterId))
                return CivilianMovementOwner.SeparateSpace;
            if (world.Strategic.PlayerPartyContext?.IsMember(characterId) == true)
                return CivilianMovementOwner.PlayerParty;
            if (world.BackgroundCharacterTravel.IsTraveling(characterId))
                return CivilianMovementOwner.BackgroundTravel;

            var hasSquad = world.Strategic.Squads.TryGetForCharacter(characterId, out var squad) && squad != null;
            if (hasSquad && IsActivelyTravelingNpcSquad(world, squad, out _))
                return CivilianMovementOwner.ActiveSquadTravel;
            if (hasSquad && squad.MemberCharacterIds.Count > 1 &&
                squad.CommandKind == SquadCommandKind.FollowLeader)
            {
                var leader = squad.CommandTargetCharacterId.IsNone
                    ? squad.LeaderCharacterId : squad.CommandTargetCharacterId;
                if (characterId != leader) return CivilianMovementOwner.ActiveSquadTravel;
            }

            if (civilianState != null &&
                civilianState.Disposition != CivilianDisposition.CapturedEscorted &&
                world.Entities.TryGet(characterId, out var entity) &&
                MortalCivilianQuery.IsManagedCivilian(world, entity))
                return CivilianMovementOwner.CivilianLocal;

            if (hasSquad && world.Strategic.SquadWorldMotions.TryGet(squad.SquadId, out var motion) &&
                SquadWorldMotionService.IsActiveNpcSquadAuthority(world, squad, motion))
                return CivilianMovementOwner.StaticSquad;
            return CivilianMovementOwner.None;
        }

        public static bool CanCivilianOwnLocalMovement(
            SimulationWorld world, EntityId characterId, MortalCivilianState civilianState) =>
            Resolve(world, characterId, civilianState) == CivilianMovementOwner.CivilianLocal;

        public static bool IsActivelyTravelingNpcSquad(
            SimulationWorld world, SquadState squad, out SquadWorldMotionState motion)
        {
            motion = null;
            return squad != null &&
                   world?.Strategic?.SquadWorldMotions != null &&
                   world.Strategic.SquadWorldMotions.TryGet(squad.SquadId, out motion) &&
                   SquadWorldMotionService.IsActiveNpcSquadAuthority(world, squad, motion) &&
                   motion.IsMoving && motion.Route.Count > 0 && motion.WaypointIndex < motion.Route.Count;
        }
    }

    /// <summary>Shared authority query for actions that require both hands while escorting.</summary>
    public static class MortalTransportConstraintService
    {
        public const string EscortActionDenied = "正在押送囚犯，无法执行该动作。";

        public static bool TryGetEscortedCaptive(
            SimulationWorld world, EntityId carrierId, out EntityId captiveId)
        {
            captiveId = EntityId.None;
            if (world == null || carrierId.IsNone) return false;
            foreach (var pair in world.Civilians.All)
            {
                var state = pair.Value;
                if (state == null || state.Disposition != CivilianDisposition.CapturedEscorted ||
                    state.CarrierId != carrierId) continue;
                if (!captiveId.IsNone && captiveId != pair.Key)
                {
                    // Corrupt/legacy duplicate loads must fail closed: the carrier remains
                    // burdened until validation rejects the duplicate relation.
                    return true;
                }
                captiveId = pair.Key;
            }
            return !captiveId.IsNone;
        }

        public static Result ValidateAttack(SimulationWorld world, EntityId attackerId) =>
            TryGetEscortedCaptive(world, attackerId, out _)
                ? Result.Failure(ErrorCode.InvalidOperation, EscortActionDenied)
                : Result.Success();

        public static Result ValidateUnburdenedAction(SimulationWorld world, EntityId actorId) =>
            TryGetEscortedCaptive(world, actorId, out _)
                ? Result.Failure(ErrorCode.ActionCannotStart, EscortActionDenied)
                : Result.Success();
    }

    /// <summary>Priority evaluator: forced states, hunger, sleep, rescue, profession, common work, idle.</summary>
    public static class MortalActivityEvaluator
    {
        public static MortalActivity Evaluate(SimulationWorld world, Entity entity, MortalCivilianState state)
        {
            if (state.Disposition == CivilianDisposition.CapturedEscorted) return MortalActivity.Escorted;
            if (state.Disposition == CivilianDisposition.Detained) return MortalActivity.Detained;
            if (state.Disposition == CivilianDisposition.Fleeing) return MortalActivity.Flee;
            var schedulePhase = FactionMortalSchedule.Current(
                world, entity, out var scheduleId, out var scheduleBlock, out var scheduleReason);
            state.ScheduleStatus = schedulePhase == FactionMortalSchedulePhase.Missing ||
                                   schedulePhase == FactionMortalSchedulePhase.DedicatedDuty
                ? scheduleReason
                : scheduleId + " · " + scheduleBlock.Activity;
            if (state.Energy < MortalCivilianTuning.GroundSleepThreshold) return MortalActivity.SleepOnGround;
            if (MortalTransportConstraintService.TryGetEscortedCaptive(world, entity.Id, out _))
            {
                state.ScheduleStatus = MortalTransportConstraintService.EscortActionDenied;
                return MortalActivity.Idle;
            }
            if (!state.RescueTargetId.IsNone || MortalCivilianService.HasRescueTarget(world, entity))
                return MortalActivity.Rescue;
            if (state.Activity == MortalActivity.Eat && state.MealStartedTick > 0 &&
                world.Tick.Value < state.MealStartedTick + 2) return MortalActivity.Eat;
            if (state.SleepPhase == CivilianSleepPhase.SleepingOnGround &&
                state.Energy < MortalCivilianTuning.WakeThreshold)
                return MortalActivity.SleepOnGround;
            if (state.Satiety <= MortalCivilianTuning.HungerThreshold &&
                MortalCivilianService.IsFoodPlanInProgress(state))
                return MortalActivity.FetchFood;
            if (state.Satiety <= MortalCivilianTuning.HungerThreshold &&
                MortalCivilianService.ShouldRetryFood(world, entity, state))
                return MortalActivity.FetchFood;
            if (state.Profession == MortalProfession.Medic &&
                MortalCivilianService.HasMedicTarget(world, entity, state))
                return MortalActivity.MedicCare;
            var validResidenceSleep =
                RoomBedFacilityService.IsValidOccupiedBed(
                    world, entity.Id, RoomUsage.Residential, out _);
            if (validResidenceSleep &&
                (schedulePhase == FactionMortalSchedulePhase.Rest ||
                 state.Energy < MortalCivilianTuning.WakeThreshold))
                return MortalActivity.SleepAtResidence;
            if (state.Disposition == CivilianDisposition.SurrenderWaiting || state.Disposition == CivilianDisposition.Displaced)
                return MortalActivity.Idle;
            if (schedulePhase == FactionMortalSchedulePhase.Rest)
                return RoomBedFacilityService.HasAvailableBed(world, entity.Id, RoomUsage.Residential)
                    ? MortalActivity.SleepAtResidence : MortalActivity.SleepOnGround;
            if (state.Energy <= MortalCivilianTuning.SleepThreshold)
                return RoomBedFacilityService.HasAvailableBed(world, entity.Id, RoomUsage.Residential)
                    ? MortalActivity.SleepAtResidence : MortalActivity.SleepOnGround;
            // During planned rest a failed food trip yields to sleep until its retry cooldown.
            if (state.Satiety <= MortalCivilianTuning.HungerThreshold) return MortalActivity.Idle;
            if (schedulePhase == FactionMortalSchedulePhase.Free)
            { state.ScheduleStatus = scheduleId + " · 自由活动";
              return ChessRecreationService.Owns(world, entity.Id) ? MortalActivity.Chess : MortalActivity.Idle; }
            if (schedulePhase == FactionMortalSchedulePhase.DedicatedDuty)
                return MortalActivity.DedicatedDuty;
            if (schedulePhase == FactionMortalSchedulePhase.Missing) return MortalActivity.Idle;
            if (!entity.TryGet<FactionMembershipComponent>(out var membership) || !membership.IsAffiliated)
            { state.ScheduleStatus = "工作受阻：无正式势力归属"; return MortalActivity.Idle; }
            switch (state.Profession)
            {
                case MortalProfession.Warden: return MortalActivity.PrisonCare;
                case MortalProfession.Farmer:
                    if (MortalCivilianQuery.TryFindProfessionArea(world, entity, "grain", out _)) return MortalActivity.FarmerWork;
                    state.ScheduleStatus = "工作受阻：无合法粮田工区";
                    break;
                case MortalProfession.HerbFarmer:
                    if (MortalCivilianQuery.TryFindProfessionArea(world, entity, "herb", out _)) return MortalActivity.HerbFarmerWork;
                    state.ScheduleStatus = "工作受阻：无合法药田工区";
                    break;
                case MortalProfession.Logger:
                    if (MortalCivilianQuery.TryFindProfessionArea(world, entity, "wood", out _)) return MortalActivity.Logging;
                    state.ScheduleStatus = "工作受阻：无合法伐木工区";
                    break;
                case MortalProfession.Medic:
                    state.ScheduleStatus = "医者待命：无可治疗床上伤员或等待补药";
                    break;
                default:
                    state.ScheduleStatus = "工作受阻：未分配职业";
                    break;
            }
            return CivilianConstructionJobService.HasJob(world, entity, true) ? MortalActivity.Haul :
                CivilianConstructionJobService.HasJob(world, entity, false) ? MortalActivity.Construction :
                MortalActivity.Idle;
        }

        public static bool HasTag(WorkAreaDefinition area, string tag)
        {
            if (area?.Tags == null) return false;
            for (var i = 0; i < area.Tags.Count; i++)
                if (string.Equals(area.Tags[i], tag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    public static partial class MortalCivilianService
    {
        public static bool CanReceiveAutomaticRescue(SimulationWorld world, EntityId target)
        {
            if (world == null || target.IsNone || !world.Entities.TryGet(target, out var injured) ||
                CharacterEncounterService.OwnsParticipantSpatialState(world, target) ||
                injured.TryGet<LifecycleComponent>(out var life) && life.State == LifecycleState.Captured)
                return false;
            return !world.Civilians.TryGet(target, out var state) ||
                state.Disposition != CivilianDisposition.CapturedEscorted && state.Disposition != CivilianDisposition.Detained;
        }

        static void ReconcileAutomaticRescues(SimulationWorld world)
        {
            foreach (var pair in world.Civilians.All)
                if (!pair.Value.RescueTargetId.IsNone && !CanReceiveAutomaticRescue(world, pair.Value.RescueTargetId) &&
                    world.Entities.TryGet(pair.Key, out var rescuer))
                {
                    if (rescuer.TryGet<MovementIntentComponent>(out var intent) && intent.Active &&
                        (intent.DestinationKey == "rescue-target:" + pair.Value.RescueTargetId.Value ||
                         !string.IsNullOrEmpty(pair.Value.RescueDestinationId) && intent.DestinationKey == pair.Value.RescueDestinationId))
                        intent.Clear();
                    DropRescue(world, rescuer, pair.Value);
                    pair.Value.RescueStatus = "救护已取消：伤员受拘押或已进入战场。";
                }
        }

        static bool CanRescueCommunity(SimulationWorld world, Entity rescuer, Entity injured)
        {
            if (world == null || rescuer == null || injured == null ||
                !CanReceiveAutomaticRescue(world, injured.Id) ||
                CharacterEncounterService.OwnsParticipantSpatialState(world, rescuer.Id)) return false;
            if (!rescuer.TryGet<FactionMembershipComponent>(out var own) || !own.IsAffiliated ||
                !injured.TryGet<FactionMembershipComponent>(out var patient) || !patient.IsAffiliated) return false;
            if (own.FactionId == patient.FactionId) return true;
            return HuangcunSurvivalService.Applies(world, injured) && !HuangcunSurvivalService.IsPrisoner(world, injured.Id) &&
                world.Strategic.Sites.TryGet(Ch01ScenarioProgressionHooks.HuangcunSiteId, out var village) &&
                village.OwnerFactionId == own.FactionId && CharacterWorldPresenceQuery.TryResolve(world, injured.Id, out var at) &&
                at.HasWorldPosition && world.OpeningArea.ContainsVillage(at.SurfaceId, at.WorldPosition.X, at.WorldPosition.Y);
        }
        public const string GrainResourceId = "base:resource_grain";
        public const string HerbResourceId = "base:resource_spirit_herb";
        public const string WoodResourceId = "base:resource_rough_wood";
        public const ulong FoodFetchRetryDelayTicks = 8UL;

        public static MortalCivilianState Ensure(SimulationWorld world, Entity entity)
        {
            if (!MortalCivilianQuery.IsManagedCivilian(world, entity)) return null;
            var created = !world.Civilians.TryGet(entity.Id, out var state);
            state = world.Civilians.GetOrCreate(entity.Id);
            if (created)
            {
                state.LastUpdateTick = world.Tick.Value;
                state.ResidenceWorkAreaId = string.Empty;
                state.CurrentSiteId = MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id);
            }
            return state;
        }

        public static Result SetProfession(SimulationWorld world, EntityId id, MortalProfession profession)
        {
            if (!Enum.IsDefined(typeof(MortalProfession), profession)) return Result.Failure(ErrorCode.InvalidArgument, "未知凡人职业。");
            if (!CanSetProfession(world, id, out var reason))
                return Result.Failure(ErrorCode.InvalidOperation, reason);
            world.Entities.TryGet(id, out var entity);
            var state = Ensure(world, entity);
            var changed = state.Profession != profession;
            state.Profession = profession;
            if (profession != MortalProfession.Medic) SettleCarriedMedicine(world, entity, state);
            if (profession != MortalProfession.Warden) SettleWardenPayload(world, entity, state);
            if (changed && entity.TryGet<MovementIntentComponent>(out var previousWork)) previousWork.Clear();
            return Result.Success();
        }

        /// <summary>Wake-up handoff: keep persistent assignments, rebuild transient work from the current schedule.</summary>
        public static void ResumeAfterMedicalRecovery(SimulationWorld world, Entity entity)
        {
            var state = Ensure(world, entity);
            if (state == null || !entity.TryGet<LifecycleComponent>(out var life) || life.State != LifecycleState.Alive)
                return;
            ClearCivilianMovement(entity);
            ClearFoodTrip(entity, state);
            ClearSleep(state);
            InterruptMedic(state);
            world.WorkAreaOccupancy.Release(entity.Id);
            world.GetOrCreateOrderQueue(entity.Id).RemoveWhere(order => order.Source == OrderSource.Schedule);
            if (entity.TryGet<ActionStateComponent>(out var actionState) && actionState.HasActiveAction &&
                (!world.ActiveActions.TryGetValue(actionState.ActiveActionId, out var active) ||
                 active.Status == ActionStatus.Cancelled || active.Status == ActionStatus.Completed ||
                 active.Status == ActionStatus.Failed))
            {
                actionState.ActiveActionId = ActionId.None;
                actionState.ActiveClock = null;
            }
            state.Activity = MortalActivity.Idle;
            state.CurrentSiteId = MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id);
            state.ScheduleStatus = string.Empty;
            // Party and battle orders retain authority; only autonomous residents resume work here.
            if (!MortalCivilianMovementAuthority.CanCivilianOwnLocalMovement(world, entity.Id, state)) return;
            state.Activity = MortalActivityEvaluator.Evaluate(world, entity, state);
            ApplyActivity(world, entity, state);
        }

        public static bool CanSetProfession(SimulationWorld world, EntityId id, out string reason)
        {
            reason = string.Empty;
            if (world == null || !world.Entities.TryGet(id, out var entity) ||
                !MortalCivilianQuery.IsPlayerFactionManageable(world, entity) ||
                !PlayerPartyRuntime.CanPlayerControlCharacter(world, id))
            { reason = "仅可管理玩家势力的永久凡人人员。"; return false; }
            var state = Ensure(world, entity);
            if (state == null || state.Disposition != CivilianDisposition.Normal)
            { reason = "被俘、囚禁、逃亡或待招降人物不可安排职业。"; return false; }
            return true;
        }

        public static void Tick(SimulationWorld world, Action<string, long> phaseSample = null)
        {
            if (world == null) return;
            var diagnosticStart = phaseSample == null ? 0L : System.Diagnostics.Stopwatch.GetTimestamp();
            long siteTicks = 0, needsTicks = 0, evaluateTicks = 0, applyTicks = 0;
            CivilianConstructionJobService.CancelInvalidJobs(world);
            ReconcileAutomaticRescues(world);
            // Custody also supports cultivators; it must not enroll them as mortal workers.
            foreach (var pair in world.Civilians.All)
                if (pair.Value.Disposition == CivilianDisposition.CapturedEscorted &&
                    world.Entities.TryGet(pair.Key, out var escorted) &&
                    !MortalCivilianQuery.IsManagedCivilian(world, escorted))
                    SyncEscort(world, escorted, pair.Value);
            foreach (var pair in world.Civilians.All)
                if (!pair.Value.RescueTargetId.IsNone && !world.Entities.TryGet(pair.Key, out _))
                {
                    pair.Value.RescueTargetId = EntityId.None;
                    pair.Value.RescueCarrying = false;
                    pair.Value.RescueDestinationId = string.Empty;
                }
            foreach (var entity in world.Entities.All)
            {
                var state = Ensure(world, entity);
                if (state == null) continue;
                if (!HuangcunOpeningAccess.IsSimulated(world, entity)) continue;
                ReconcileMedicAssignment(world, entity, state);
                if (state.Profession != MortalProfession.Warden ||
                    entity.TryGet<LifecycleComponent>(out var workerLife) && (workerLife.IsDead || workerLife.IsRemoved))
                    SettleWardenPayload(world, entity, state);
                if (!entity.TryGet<LifecycleComponent>(out var life) || life.State == LifecycleState.Dead || life.State == LifecycleState.Removed)
                {
                    EndCivilianSleep(world, entity.Id, state); DropRescue(world, entity, state);
                    state.CarrierId = EntityId.None;
                    state.Disposition = CivilianDisposition.Normal;
                    state.DetainedResidenceWorkAreaId = string.Empty;
                    CivilianConstructionJobService.ReleaseCarrier(world, entity.Id);
                    state.Activity = MortalActivity.Idle; continue;
                }
                AdvanceDayCare(world, entity.Id, state, world.Tick.Value / WorldTick.TicksPerDay);
                var diagnosticPart = phaseSample == null ? 0L : System.Diagnostics.Stopwatch.GetTimestamp();
                state.CurrentSiteId = MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id);
                if (phaseSample != null) siteTicks += System.Diagnostics.Stopwatch.GetTimestamp() - diagnosticPart;
                if (life.State == LifecycleState.Alive &&
                    state.Disposition == CivilianDisposition.Displaced &&
                    HandleDisplaced(world, entity, state))
                {
                    state.LastUpdateTick = world.Tick.Value;
                    continue;
                }
                diagnosticPart = phaseSample == null ? 0L : System.Diagnostics.Stopwatch.GetTimestamp();
                AdvanceNeeds(world, entity, state);
                if (phaseSample != null) needsTicks += System.Diagnostics.Stopwatch.GetTimestamp() - diagnosticPart;
                if (life.IsIncapacitated && state.Disposition != CivilianDisposition.CapturedEscorted ||
                    life.State != LifecycleState.Alive && life.State != LifecycleState.Captured)
                {
                    EndCivilianSleep(world, entity.Id, state);
                    DropRescue(world, entity, state);
                    CivilianConstructionJobService.ReleaseCarrier(world, entity.Id);
                    state.Activity = MortalActivity.Idle;
                    state.LastUpdateTick = world.Tick.Value;
                    continue;
                }
                if (life.State == LifecycleState.Captured &&
                    state.Disposition == CivilianDisposition.CapturedEscorted)
                {
                    EndCivilianSleep(world, entity.Id, state);
                    DropRescue(world, entity, state);
                    CivilianConstructionJobService.ReleaseCarrier(world, entity.Id);
                    SyncEscort(world, entity, state);
                    state.Activity = MortalActivity.Escorted;
                    state.LastUpdateTick = world.Tick.Value;
                    continue;
                }
                if (SupervisorAngerDriver.OwnsPursuit(world, entity.Id))
                {
                    state.Activity = MortalActivity.DedicatedDuty;
                    state.LastUpdateTick = world.Tick.Value;
                    continue;
                }
                var movementOwner = MortalCivilianMovementAuthority.Resolve(world, entity.Id, state);
                // Party control owns movement and work. Civilian needs remain persistent while enlisted.
                if (movementOwner == CivilianMovementOwner.PlayerParty)
                {
                    if (!PartyRestService.BlocksMovement(entity)) EndCivilianSleep(world, entity.Id, state);
                    ClearFoodTrip(entity, state);
                    ClearCivilianMovement(entity);
                    DropRescue(world, entity, state);
                    CivilianConstructionJobService.ReleaseCarrier(world, entity.Id);
                    if (state.Satiety <= MortalCivilianTuning.HungerThreshold &&
                        TryConsumeFoodAbstract(world, entity.Id, state.CurrentSiteId))
                    { state.Satiety += MortalCivilianTuning.MealRestore; state.AteToday = true; state.Clamp(); }
                    var tired = NpcAutonomyState.Ensure(entity);
                    if (state.Energy == 0) PartyRestService.EnterExhaustion(world, entity);
                    if (state.Energy >= PartyRestService.ExhaustionRecoveryThreshold) tired.Exhausted = false;
                    if (!tired.Exhausted && tired.DetachedByExhaustion &&
                        world.Strategic.PlayerPartyContext?.ActiveCharacterId == entity.Id)
                    {
                        tired.DetachedByExhaustion = false;
                        PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, world.Strategic.PlayerPartyContext);
                    }
                    if (tired.Exhausted && !PartyRestService.IsResting(world, entity))
                    {
                        world.GetOrCreateOrderQueue(entity.Id).Clear();
                        if (entity.TryGet<ActionStateComponent>(out var current) && current.HasActiveAction &&
                            world.ActiveActions.TryGetValue(current.ActiveActionId, out var running)) running.Cancel();
                    }
                    if (tired.Exhausted && !PartyRestService.IsResting(world, entity))
                    {
                        // Core does not own a SimulationLoop here: forced rest restoration is driven by the same needs Tick.
                        state.SleepPhase = CivilianSleepPhase.SleepingOnGround;
                    }
                    state.Activity = tired.Exhausted || PartyRestService.IsResting(world, entity)
                        ? MortalActivity.SleepOnGround : MortalActivity.Idle;
                    state.LastUpdateTick = world.Tick.Value;
                    continue;
                }
                if (movementOwner == CivilianMovementOwner.Battle)
                { EndCivilianSleep(world, entity.Id, state); ClearCivilianMovement(entity); DropRescue(world, entity, state); CivilianConstructionJobService.ReleaseCarrier(world, entity.Id); state.Activity = MortalActivity.Idle; continue; }
                if (movementOwner == CivilianMovementOwner.SeparateSpace ||
                    movementOwner == CivilianMovementOwner.ActiveSquadTravel ||
                    movementOwner == CivilianMovementOwner.BackgroundTravel)
                { EndCivilianSleep(world, entity.Id, state); ClearCivilianMovement(entity); DropRescue(world, entity, state); CivilianConstructionJobService.ReleaseCarrier(world, entity.Id); state.Activity = MortalActivity.Idle; continue; }
                if (PrisonWallBreachService.DriveExitIntent(world, entity))
                { state.Activity = MortalActivity.Flee; state.LastUpdateTick = world.Tick.Value; continue; }
                if (state.Disposition == CivilianDisposition.Fleeing) AdvanceFlee(world, entity, state);
                if (state.Disposition == CivilianDisposition.CapturedEscorted) SyncEscort(world, entity, state);
                diagnosticPart = phaseSample == null ? 0L : System.Diagnostics.Stopwatch.GetTimestamp();
                state.Activity = MortalActivityEvaluator.Evaluate(world, entity, state);
                if (phaseSample != null) evaluateTicks += System.Diagnostics.Stopwatch.GetTimestamp() - diagnosticPart;
                if (state.Satiety > MortalCivilianTuning.HungerThreshold &&
                    state.Activity != MortalActivity.Eat)
                    ClearFoodTrip(entity, state);
                if (state.Activity != MortalActivity.Rescue)
                    DropRescue(world, entity, state);
                if (state.Activity != MortalActivity.MedicCare)
                    InterruptMedic(state);
                if (state.Activity != MortalActivity.Haul)
                    CivilianConstructionJobService.ReleaseCarrier(world, entity.Id);
                diagnosticPart = phaseSample == null ? 0L : System.Diagnostics.Stopwatch.GetTimestamp();
                ApplyActivity(world, entity, state);
                if (phaseSample != null) applyTicks += System.Diagnostics.Stopwatch.GetTimestamp() - diagnosticPart;
                state.LastUpdateTick = world.Tick.Value;
            }
            if (phaseSample != null)
            {
                var totalTicks = System.Diagnostics.Stopwatch.GetTimestamp() - diagnosticStart;
                phaseSample("civilian-site", siteTicks);
                phaseSample("civilian-needs", needsTicks);
                phaseSample("civilian-evaluate", evaluateTicks);
                phaseSample("civilian-apply", applyTicks);
                phaseSample("civilian-other", Math.Max(0L, totalTicks - siteTicks - needsTicks - evaluateTicks - applyTicks));
            }
        }

        static void AdvanceDayCare(SimulationWorld world, EntityId characterId, MortalCivilianState state, ulong day)
        {
            if (state.CareDayIndex == day) return;
            // Captivity resistance is separate from loyalty to the original faction (405).
            state.CareDayIndex = day; state.AteToday = false; state.SleptToday = false;
        }

        static void AdvanceNeeds(SimulationWorld world, Entity entity, MortalCivilianState state)
        {
            var now = world.Tick.Value;
            if (now % (ulong)MortalCivilianTuning.SatietyDecayEveryTicks == 0) state.Satiety--;
            var sleepUsage = state.Disposition == CivilianDisposition.Detained
                ? RoomUsage.Prison : RoomUsage.Residential;
            var validResidenceSleep = state.SleepPhase == CivilianSleepPhase.SleepingAtResidence &&
                                      RoomBedFacilityService.IsValidOccupiedBed(
                                          world, entity.Id, sleepUsage, out _);
            if (state.SleepPhase == CivilianSleepPhase.SleepingAtResidence && !validResidenceSleep)
                EndCivilianSleep(world, entity.Id, state);
            var actionBedSleep = IsRunningBedRestAction(world, entity.Id);
            var medicalBedRest = entity.TryGet<LifecycleComponent>(out var life) && life.IsIncapacitated &&
                MedicalRecoveryService.TryGetPatientBed(world, entity, out _);
            var sleeping = PartyRestService.IsResting(world, entity) || medicalBedRest || validResidenceSleep || actionBedSleep ||
                           state.SleepPhase == CivilianSleepPhase.SleepingOnGround;
            if (sleeping)
            {
                var interval = !medicalBedRest && state.SleepPhase == CivilianSleepPhase.SleepingOnGround
                    ? MortalCivilianTuning.GroundSleepRestoreEveryTicks
                    : MortalCivilianTuning.BedSleepRestoreEveryTicks;
                if (now % (ulong)interval == 0) { state.Energy++; state.SleptToday = true; }
            }
            else if (now % (ulong)MortalCivilianTuning.EnergyDecayEveryTicks == 0) state.Energy--;
            state.Clamp();
        }

        static bool IsRunningBedRestAction(SimulationWorld world, EntityId id)
        {
            if (world == null || !world.Entities.TryGet(id, out var entity) ||
                !entity.TryGet<ActionStateComponent>(out var actionState) ||
                !actionState.HasActiveAction ||
                !world.ActiveActions.TryGetValue(actionState.ActiveActionId, out var action) ||
                !(action is BedRestAction bedRest) || action.Status != ActionStatus.Running ||
                !world.OutdoorConstructedAssets.Assets.TryGetValue(bedRest.BedId, out var bed))
                return false;
            return RoomBedFacilityService.BedHasUsage(world, bed, RoomUsage.Residential) &&
                   bed.OccupantCharacterId == id &&
                   RoomBedFacilityService.IsAtAccessPoint(world, bed, id);
        }

        static bool HandleDisplaced(SimulationWorld world, Entity entity, MortalCivilianState state)
        {
            var traveling = world.BackgroundCharacterTravel.IsTraveling(entity.Id);
            if (traveling && !IsValidRefugeSite(world, entity, state,
                    world.BackgroundCharacterTravel.TryGet(entity.Id, out var activeTravel)
                        ? activeTravel.DestinationSiteId : string.Empty))
            {
                BackgroundCharacterTravelService.CancelTravelIfAny(world, entity.Id);
                state.RefugeTargetSiteId = string.Empty;
                traveling = false;
            }

            if (traveling || !world.ContinuousOutdoorMaterialization.IsMaterialized(entity.Id))
            {
                ClearDisplacedLocalActivity(world, entity, state);
                if (BackgroundSimulationScheduler.ResolveBucketForWorldTick(world.Tick.Value) ==
                    BackgroundSimulationScheduler.ResolveTravelBucket(entity.Id))
                {
                    AdvanceBackgroundNeeds(world.Tick.Value, state);
                    if (!traveling) TryBeginRefugeTravel(world, entity, state);
                }
                state.Activity = MortalActivity.Idle;
                return true;
            }

            // Visible Displaced characters retain real needs and emergency ground sleep, but do
            // not start long-distance refugee movement while the loaded neighborhood owns them.
            state.BackgroundNeedsLastProcessedTick = world.Tick.Value;
            return false;
        }

        static void AdvanceBackgroundNeeds(ulong now, MortalCivilianState state)
        {
            var last = state.BackgroundNeedsLastProcessedTick;
            if (last == 0 || last > now) last = now;
            if (now > last)
            {
                state.Satiety -= CountIntervalCrossings(last, now, MortalCivilianTuning.SatietyDecayEveryTicks);
                state.Energy -= CountIntervalCrossings(last, now, MortalCivilianTuning.EnergyDecayEveryTicks);
                state.Clamp();
            }
            state.BackgroundNeedsLastProcessedTick = now;
        }

        static int CountIntervalCrossings(ulong fromTick, ulong toTick, int interval)
        {
            if (interval <= 0 || toTick <= fromTick) return 0;
            var count = toTick / (ulong)interval - fromTick / (ulong)interval;
            return count > int.MaxValue ? int.MaxValue : (int)count;
        }

        static void ClearDisplacedLocalActivity(
            SimulationWorld world, Entity entity, MortalCivilianState state)
        {
            ClearFoodTrip(entity, state);
            EndCivilianSleep(world, entity.Id, state);
            ClearCivilianMovement(entity);
            DropRescue(world, entity, state);
            CivilianConstructionJobService.ReleaseCarrier(world, entity.Id);
        }

        static void TryBeginRefugeTravel(
            SimulationWorld world, Entity entity, MortalCivilianState state)
        {
            if (!entity.TryGet<FactionMembershipComponent>(out var membership) || !membership.IsAffiliated ||
                !CharacterWorldPresenceQuery.TryResolve(world, entity.Id, out var presence) ||
                !presence.HasWorldPosition || string.IsNullOrEmpty(presence.SurfaceId))
            { state.RefugeTargetSiteId = string.Empty; return; }

            if (!string.IsNullOrEmpty(state.CurrentSiteId) &&
                !string.Equals(state.CurrentSiteId, state.FleeSourceSiteId, StringComparison.Ordinal) &&
                world.Strategic.Sites.TryGet(state.CurrentSiteId, out var currentSite) && currentSite != null &&
                currentSite.IsCoreActive &&
                string.Equals(currentSite.OwnerFactionId, membership.FactionId, StringComparison.Ordinal))
            { state.RefugeTargetSiteId = state.CurrentSiteId; return; }

            ReleaseDisplacedSquadMovement(world, entity.Id);
            var candidates = new List<WorldSite>();
            foreach (var pair in world.Strategic.Sites.Sites)
            {
                var site = pair.Value;
                if (site == null || !site.IsCoreActive ||
                    !string.Equals(site.OwnerFactionId, membership.FactionId, StringComparison.Ordinal) ||
                    string.Equals(site.SiteId, state.FleeSourceSiteId, StringComparison.Ordinal) ||
                    !string.Equals(site.CoreSurfaceId, presence.SurfaceId, StringComparison.Ordinal)) continue;
                candidates.Add(site);
            }
            candidates.Sort((a, b) =>
            {
                var adx = (double)a.CoreWorldX - presence.WorldPosition.X;
                var ady = (double)a.CoreWorldY - presence.WorldPosition.Y;
                var bdx = (double)b.CoreWorldX - presence.WorldPosition.X;
                var bdy = (double)b.CoreWorldY - presence.WorldPosition.Y;
                var distance = (adx * adx + ady * ady).CompareTo(bdx * bdx + bdy * bdy);
                return distance != 0 ? distance : string.CompareOrdinal(a.SiteId, b.SiteId);
            });

            if (!string.IsNullOrEmpty(state.RefugeTargetSiteId))
                for (var i = 0; i < candidates.Count; i++)
                    if (string.Equals(candidates[i].SiteId, state.RefugeTargetSiteId, StringComparison.Ordinal))
                    { var preferred = candidates[i]; candidates.RemoveAt(i); candidates.Insert(0, preferred); break; }

            state.RefugeTargetSiteId = string.Empty;
            for (var i = 0; i < candidates.Count; i++)
            {
                var result = BackgroundCharacterTravelService.BeginTravelToWorldSite(
                    world, entity.Id, candidates[i].SiteId);
                if (result.IsFailure) continue;
                state.RefugeTargetSiteId = candidates[i].SiteId;
                return;
            }
        }

        static bool IsValidRefugeSite(
            SimulationWorld world, Entity entity, MortalCivilianState state, string siteId)
        {
            return !string.IsNullOrEmpty(siteId) &&
                   !string.Equals(siteId, state.FleeSourceSiteId, StringComparison.Ordinal) &&
                   entity.TryGet<FactionMembershipComponent>(out var membership) && membership.IsAffiliated &&
                   CharacterWorldPresenceQuery.TryResolve(world, entity.Id, out var presence) &&
                   presence.HasWorldPosition && !string.IsNullOrEmpty(presence.SurfaceId) &&
                   world.Strategic.Sites.TryGet(siteId, out var site) && site != null && site.IsCoreActive &&
                   string.Equals(site.CoreSurfaceId, presence.SurfaceId, StringComparison.Ordinal) &&
                   string.Equals(site.OwnerFactionId, membership.FactionId, StringComparison.Ordinal);
        }

        static void ReleaseDisplacedSquadMovement(SimulationWorld world, EntityId id)
        {
            if (!world.Strategic.Squads.TryGetForCharacter(id, out var source) || source == null) return;
            var sourceId = source.SquadId;
            if (source.SquadId != SquadMembershipService.SingletonSquadId(id) ||
                source.MemberCharacterIds.Count != 1)
                SquadMembershipService.LeaveToSingleton(world, id);
            if (!world.Strategic.Squads.TryGet(sourceId, out _))
                world.Strategic.SquadWorldMotions.Remove(sourceId);
            if (world.Strategic.Squads.TryGetForCharacter(id, out var singleton) && singleton != null)
                world.Strategic.SquadWorldMotions.Remove(singleton.SquadId);
        }

        static void ApplyActivity(SimulationWorld world, Entity entity, MortalCivilianState state)
        {
            if (state.Activity != MortalActivity.SleepAtResidence && state.Activity != MortalActivity.SleepOnGround &&
                state.Activity != MortalActivity.Detained && state.SleepPhase != CivilianSleepPhase.None)
                EndCivilianSleep(world, entity.Id, state);
            switch (state.Activity)
            {
                case MortalActivity.Chess:
                    break; // Chess owns its two exact seat intents; never clear them as idle.
                case MortalActivity.Idle:
                    if (entity.TryGet<MovementIntentComponent>(out var idleIntent)) idleIntent.Clear();
                    break;
                case MortalActivity.Eat:
                    if (entity.TryGet<MovementIntentComponent>(out var eatIntent)) eatIntent.Clear();
                    if (state.MealStartedTick > 0 && world.Tick.Value >= state.MealStartedTick + 2)
                        ClearFoodTrip(entity, state);
                    break;
                case MortalActivity.FetchFood:
                    FetchFood(world, entity, state);
                    break;
                case MortalActivity.SleepAtResidence:
                    ApplyResidenceSleep(world, entity, state, RoomUsage.Residential);
                    break;
                case MortalActivity.SleepOnGround:
                    StartGroundSleep(world, entity, state);
                    break;
                case MortalActivity.FarmerWork:
                    Work(world, entity, "grain"); break;
                case MortalActivity.HerbFarmerWork:
                    Work(world, entity, "herb"); break;
                case MortalActivity.Logging:
                    Work(world, entity, "wood"); break;
                case MortalActivity.MedicCare:
                    TreatAtBed(world, entity, state); break;
                case MortalActivity.Rescue:
                    RescueOneAlly(world, entity); break;
                case MortalActivity.Haul:
                    CivilianConstructionJobService.Work(world, entity, true); break;
                case MortalActivity.Construction:
                    CivilianConstructionJobService.Work(world, entity, false); break;
                case MortalActivity.DedicatedDuty:
                    // Do not manufacture a fake civilian action or clear an intent owned by the
                    // existing squad/duty system. ScheduleStatus exposes the unsupported bridge.
                    break;
                case MortalActivity.PrisonCare:
                    WorkAsWarden(world, entity, state);
                    break;
                case MortalActivity.Escorted:
                case MortalActivity.Detained:
                    if (state.Activity == MortalActivity.Escorted)
                    {
                        if (entity.TryGet<MovementIntentComponent>(out var forcedIntent)) forcedIntent.Clear();
                        EndCivilianSleep(world, entity.Id, state);
                        break;
                    }
                    // Detained people cannot fetch abstract public food: a real keeper must deliver it.
                    if (state.Energy <= MortalCivilianTuning.SleepThreshold ||
                        state.SleepPhase == CivilianSleepPhase.SleepingAtResidence &&
                        state.Energy < MortalCivilianTuning.WakeThreshold)
                        ApplyResidenceSleep(world, entity, state,
                            RoomUsage.Prison);
                    else EndCivilianSleep(world, entity.Id, state);
                    break;
            }
        }

        static void ApplyResidenceSleep(
            SimulationWorld world, Entity entity, MortalCivilianState state,
            RoomUsage requiredUsage)
        {
            if (!RoomBedFacilityService.TryReserve(world, entity.Id, requiredUsage, out var bed))
            {
                state.ScheduleStatus = "休息受阻：没有可用的合法床位";
                StartGroundSleep(world, entity, state);
                return;
            }

            if (state.SleepPhase == CivilianSleepPhase.SleepingAtResidence)
            {
                if (RoomBedFacilityService.IsValidOccupiedBed(world, entity.Id, requiredUsage, out var occupied) &&
                    string.Equals(occupied.StableAssetId, bed.StableAssetId, StringComparison.Ordinal))
                {
                    if (entity.TryGet<MovementIntentComponent>(out var sleepingIntent) && sleepingIntent.Active)
                        sleepingIntent.Clear();
                    return;
                }
                EndCivilianSleep(world, entity.Id, state);
                if (!RoomBedFacilityService.TryReserve(world, entity.Id, requiredUsage, out bed))
                { state.ScheduleStatus = "休息受阻：原床位已失效"; StartGroundSleep(world, entity, state); return; }
            }

            if (!RoomBedFacilityService.TryResolveAccessPoint(world, bed, out var target))
            {
                state.ScheduleStatus = "休息受阻：床边没有合法使用格";
                StartGroundSleep(world, entity, state);
                return;
            }
            if (entity.TryGet<MovementIntentComponent>(out var retainedBedIntent) &&
                retainedBedIntent.Active && retainedBedIntent.HasWorldTarget &&
                string.Equals(retainedBedIntent.DestinationKey,
                    "sleep-bed:" + bed.StableAssetId, StringComparison.Ordinal))
            {
                var retained = new WorldVec2(
                    retainedBedIntent.TargetWorldX, retainedBedIntent.TargetWorldY);
                if (RoomBedGeometry.IsAccessPoint(
                        world.OutdoorConstructedAssets, bed, retained))
                    target = retained;
            }
            var arrivalRadius = world.SurfaceSpatial.TryGet(bed.SurfaceId, out var metric) && metric != null
                ? metric.CellSize * .75f : 0f;
            if (!entity.TryGet<MovementIntentComponent>(out var intent))
            { intent = new MovementIntentComponent(); entity.AddComponent(intent); }
            if (!intent.Active || !intent.HasWorldTarget || intent.TargetSurfaceId != bed.SurfaceId ||
                Math.Abs(intent.TargetWorldX - target.X) > .1f || Math.Abs(intent.TargetWorldY - target.Y) > .1f)
                intent.BeginWorldPoint(bed.SurfaceId, target.X, target.Y,
                    destinationKey: "sleep-bed:" + bed.StableAssetId, arrivalRadiusWorld: arrivalRadius);
            state.SleepPhase = CivilianSleepPhase.TravelingToResidence;
            state.SleepStartedTick = 0;
            if (state.Profession == MortalProfession.Warden && !world.ContinuousOutdoorMaterialization.IsMaterialized(entity.Id))
            {
                if (!NpcColdMovement.Advance(world, entity, bed.SurfaceId, target, arrivalRadius, out var coldArrived))
                { state.ScheduleStatus = "监管离屏返床受阻，就地休息"; StartGroundSleep(world, entity, state); return; }
                if (!coldArrived) return;
                intent.MarkPathRequested(); intent.HostArrived = true;
            }
            if (intent.HostPathState == MovementHostPathState.RetryablePathUnavailable ||
                intent.HostPathState == MovementHostPathState.PermanentFailure)
            {
                state.ScheduleStatus = "休息受阻：床位不可达" +
                    (string.IsNullOrEmpty(intent.HostPathFailureReason) ? string.Empty : " · " + intent.HostPathFailureReason);
                StartGroundSleep(world, entity, state);
                return;
            }
            if (!intent.HostArrived || !RoomBedFacilityService.IsAtAccessPoint(world, bed, entity.Id))
                return;
            intent.Clear();
            if (!RoomBedFacilityService.TryOccupy(world, bed.StableAssetId, entity.Id))
            { state.ScheduleStatus = "休息受阻：床位在到达时已被占用"; StartGroundSleep(world, entity, state); return; }
            state.SleepPhase = CivilianSleepPhase.SleepingAtResidence;
            state.SleepStartedTick = world.Tick.Value;
            state.ScheduleStatus = "已在床位睡眠：" + bed.StableAssetId;
        }

        static void StartGroundSleep(SimulationWorld world, Entity entity, MortalCivilianState state)
        {
            RoomBedFacilityService.Release(world, entity.Id);
            if (entity.TryGet<MovementIntentComponent>(out var intent)) intent.Clear();
            if (state.SleepPhase != CivilianSleepPhase.SleepingOnGround)
                state.SleepStartedTick = world.Tick.Value;
            state.SleepPhase = CivilianSleepPhase.SleepingOnGround;
        }

        static void ClearSleep(MortalCivilianState state)
        {
            state.SleepPhase = CivilianSleepPhase.None;
            state.SleepStartedTick = 0;
        }

        static void EndCivilianSleep(SimulationWorld world, EntityId id, MortalCivilianState state)
        {
            if (state == null || state.SleepPhase == CivilianSleepPhase.None) return;
            if (world.Entities.TryGet(id, out var waking) &&
                (CombatLifeStateService.CanFight(waking) || PrisonConfinementService.CanWalk(world, waking)) &&
                RoomBedFacilityService.TryGetOccupiedBed(world, id, RoomUsage.Prison, out var bed) &&
                RoomBedFacilityService.IsAtBed(world, bed, id) &&
                RoomBedFacilityService.TryResolveAccessPoint(world, bed, out var standing) &&
                PrisonConfinementService.AllowsPosition(world, id, bed.SurfaceId, standing))
                world.WorldPresence.SetAtWorldPosition(id, standing, bed.SurfaceId);
            RoomBedFacilityService.Release(world, id);
            ClearSleep(state);
        }

        static bool TryConsumeFoodAbstract(SimulationWorld world, EntityId eater, string siteId)
        {
            if (!world.Entities.TryGet(eater, out var entity) || !world.Civilians.TryGet(eater, out var civilian)) return false;
            var inParty = world.Strategic.PlayerPartyContext?.IsMember(eater) == true &&
                          PlayerPartyRuntime.CanPlayerControlCharacter(world, eater);
            if (!inParty && !string.IsNullOrEmpty(siteId) && world.Strategic.SitePublicStocks.TryGet(siteId, out var stock) &&
                world.Strategic.Sites.TryGet(siteId, out var site))
            {
                var allowed = false;
                if (civilian.Disposition == CivilianDisposition.Detained)
                    allowed = !string.IsNullOrEmpty(civilian.DetainedResidenceWorkAreaId) &&
                              string.Equals(site.OwnerFactionId, world.Strategic.PlayerFactionId, StringComparison.Ordinal);
                else if (entity.TryGet<FactionMembershipComponent>(out var membership) && membership.IsAffiliated)
                    allowed = CanAffiliatedUseSiteFood(world, membership.FactionId, site);
                else
                    allowed = HasLocalResidentialFoodRight(world, eater, civilian, siteId);
                if (allowed)
                {
                    var ids = new List<string>(stock.Resources.Keys); ids.Sort(StringComparer.Ordinal);
                    for (var i = 0; i < ids.Count; i++)
                        if (stock.GetCount(ids[i]) > 0 && world.InventoryCatalog.HasTag(ids[i], "food") &&
                            WorldSitePublicStockService.TryRemove(world, siteId, ids[i], 1, eater).IsSuccess) return true;
                }
            }
            if (inParty && civilian.Disposition != CivilianDisposition.Detained)
            {
                var ids = new List<string>();
                foreach (var slot in world.Inventory.Slots)
                    if (!slot.IsEmpty && !ids.Contains(slot.ItemId) && world.InventoryCatalog.HasTag(slot.ItemId, "food"))
                        ids.Add(slot.ItemId);
                ids.Sort(StringComparer.Ordinal);
                for (var i = 0; i < ids.Count; i++)
                    if (world.Inventory.TryRemoveAll(ids[i], 1)) return true;
            }
            return false;
        }

        static void FetchFood(SimulationWorld world, Entity entity, MortalCivilianState state)
        {
            state.FoodFetchPhase = FoodFetchPhase.NeedFood;
            state.FoodFetchStatus = "需要进食";
            state.CurrentSiteId = MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id);
            if (!TryResolveFoodSource(world, entity, state, out var siteId, out var room))
            {
                var current = MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id);
                if (!string.IsNullOrEmpty(current) &&
                    world.Strategic.Sites.TryGet(current, out var currentSite) &&
                    currentSite.IsCoreActive && CanUseSiteFood(world, entity, state, currentSite) &&
                    world.SiteStorageRooms.TryGetBySite(current, out _) && CountSiteFood(world, current) == 0)
                    FailFoodPlan(world, entity, state, FoodFetchPhase.NoFoodAvailable, "饥饿·公库无粮");
                else
                    FailFoodPlan(world, entity, state, FoodFetchPhase.NoFoodSource,
                        "饥饿·附近没有合法且可达的食物来源");
                return;
            }
            if (!CharacterWorldPresenceQuery.TryResolve(world, entity.Id, out var presence) ||
                !presence.HasWorldPosition || !string.Equals(presence.SurfaceId, room.SurfaceId, StringComparison.Ordinal))
            { FailFoodPlan(world, entity, state, FoodFetchPhase.PathUnavailable, "取食受阻：人物与储藏室不在同一地表"); return; }
            var roomChanged = !string.Equals(
                state.FoodFetchStorageRoomId, room.StorageRoomId, StringComparison.Ordinal);
            if (roomChanged)
            {
                state.HasFoodFetchAccessPoint = false;
                state.FoodFetchAccessCandidateIndex = (int)(entity.Id.Value %
                    (ulong)WorldSiteStorageRoomState.AccessCandidateCount);
                state.FoodFetchAccessAttemptCount = 0;
            }
            state.FoodFetchStorageRoomId = room.StorageRoomId;
            state.FoodFetchSourceSiteId = siteId;
            if (!state.HasFoodFetchAccessPoint)
            {
                if (state.FoodFetchAccessCandidateIndex < 0)
                    state.FoodFetchAccessCandidateIndex = (int)(entity.Id.Value %
                        (ulong)WorldSiteStorageRoomState.AccessCandidateCount);
                if (!room.TryGetAccessCandidate(state.FoodFetchAccessCandidateIndex, out var resolvedAccess))
                { FailFoodPlan(world, entity, state, FoodFetchPhase.PathUnavailable, "取食受阻：储藏室访问点无效"); return; }
                state.HasFoodFetchAccessPoint = true;
                state.FoodFetchAccessWorldX = resolvedAccess.X;
                state.FoodFetchAccessWorldY = resolvedAccess.Y;
            }
            if (!TryFindSiteFood(world, siteId, out var foodId))
            { state.PendingFoodResourceId = string.Empty; FailFoodPlan(world, entity, state, FoodFetchPhase.NoFoodAvailable, "饥饿·公库无粮"); return; }
            state.PendingFoodResourceId = foodId;
            var destination = new WorldVec2(state.FoodFetchAccessWorldX, state.FoodFetchAccessWorldY);
            MoveToWorldPoint(entity, room.SurfaceId, destination);
            if (!entity.TryGet<MovementIntentComponent>(out var intent))
            { FailFoodPlan(world, entity, state, FoodFetchPhase.PathUnavailable, "取食受阻：移动意图未建立"); return; }
            if (intent.HostPathState == MovementHostPathState.RetryablePathUnavailable ||
                intent.HostPathState == MovementHostPathState.PermanentFailure)
            {
                state.HasFoodFetchAccessPoint = false;
                state.FoodFetchAccessAttemptCount++;
                state.FoodFetchAccessCandidateIndex =
                    (state.FoodFetchAccessCandidateIndex + 1) % WorldSiteStorageRoomState.AccessCandidateCount;
                FailFoodPlan(world, entity, state, FoodFetchPhase.PathUnavailable,
                    "取食受阻" + (string.IsNullOrEmpty(intent.HostPathFailureReason)
                        ? string.Empty : "：" + intent.HostPathFailureReason));
                return;
            }
            if (state.Profession == MortalProfession.Warden && !world.ContinuousOutdoorMaterialization.IsMaterialized(entity.Id))
            {
                if (!NpcColdMovement.Advance(world, entity, room.SurfaceId, destination,
                        world.SurfaceSpatial.TryGet(room.SurfaceId, out var foodMetric) ? foodMetric.CellSize * .25f : .01f,
                        out var coldArrived))
                { FailFoodPlan(world, entity, state, FoodFetchPhase.PathUnavailable, "监管离屏取食路径受阻"); return; }
                if (!coldArrived) { state.FoodFetchStatus = "监管离屏前往储藏室取食"; return; }
                intent.MarkPathRequested(); intent.HostArrived = true;
            }
            if (!intent.HostPathRequested)
            {
                state.FoodFetchPhase = FoodFetchPhase.WaitingForPath;
                state.FoodFetchStatus = "等待取食路径";
                return;
            }
            if (!intent.HostArrived)
            {
                state.FoodFetchPhase = FoodFetchPhase.Traveling;
                state.FoodFetchStatus = "前往储藏室";
                return;
            }

            state.FoodFetchPhase = FoodFetchPhase.Arrived;
            state.FoodFetchStatus = "到达储藏室";

            // Re-select and remove atomically at arrival. Concurrent hungry civilians race here safely.
            if (!TryFindSiteFood(world, siteId, out foodId) ||
                WorldSitePublicStockService.TryRemove(world, siteId, foodId, 1, entity.Id).IsFailure)
            { state.PendingFoodResourceId = string.Empty; FailFoodPlan(world, entity, state, FoodFetchPhase.NoFoodAvailable, "饥饿·到达时公库无粮"); return; }
            intent.Clear();
            state.PendingFoodResourceId = foodId;
            state.Activity = MortalActivity.Eat;
            state.MealStartedTick = world.Tick.Value;
            state.FoodFetchPhase = FoodFetchPhase.Eating;
            state.FoodFetchStatus = "进食";
            state.Satiety += MortalCivilianTuning.MealRestore;
            state.AteToday = true;
            state.Clamp();
        }

        static bool TryResolveFoodSource(
            SimulationWorld world, Entity entity, MortalCivilianState state,
            out string siteId, out WorldSiteStorageRoomState room)
        {
            siteId = string.Empty; room = null;
            if (!CharacterWorldPresenceQuery.TryResolve(world, entity.Id, out var presence) ||
                !presence.HasWorldPosition) return false;
            if (!string.IsNullOrEmpty(state.FoodFetchSourceSiteId) &&
                !string.IsNullOrEmpty(state.FoodFetchStorageRoomId) &&
                world.SiteStorageRooms.TryGet(state.FoodFetchStorageRoomId, out var retained) &&
                string.Equals(retained.SiteId, state.FoodFetchSourceSiteId, StringComparison.Ordinal) &&
                string.Equals(retained.SurfaceId, presence.SurfaceId, StringComparison.Ordinal) &&
                world.Strategic.Sites.TryGet(retained.SiteId, out var retainedSite) && retainedSite.IsCoreActive &&
                CanUseSiteFood(world, entity, state, retainedSite) && TryFindSiteFood(world, retained.SiteId, out _))
            { siteId = retained.SiteId; room = retained; return true; }

            var currentSiteId = MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id);
            var bestDistance = double.MaxValue;
            var bestIsCurrent = false;
            foreach (var pair in world.SiteStorageRooms.All)
            {
                var candidate = pair.Value;
                if (candidate == null || !string.Equals(candidate.SurfaceId, presence.SurfaceId, StringComparison.Ordinal) ||
                    !world.Strategic.Sites.TryGet(candidate.SiteId, out var site) || site == null || !site.IsCoreActive ||
                    !CanUseSiteFood(world, entity, state, site) || !TryFindSiteFood(world, candidate.SiteId, out _) ||
                    !MortalCivilianQuery.IsWithinLocalContinuity(world, presence.SurfaceId, presence.WorldPosition,
                        new WorldVec2(candidate.WorldX, candidate.WorldY))) continue;
                var routeFound = false;
                for (var accessIndex = 0; accessIndex < WorldSiteStorageRoomState.AccessCandidateCount; accessIndex++)
                {
                    if (!candidate.TryGetAccessCandidate(accessIndex, out var access) ||
                        !world.SurfaceGround.TryGet(presence.SurfaceId, out var navigation)) continue;
                    var route = new List<WorldVec2>();
                    if (navigation.TryFindRoute(presence.WorldPosition, access, route) == SurfaceGroundRouteStatus.Found)
                    { routeFound = true; break; }
                }
                if (!routeFound) continue;
                var distance = (double)DistanceSquared(
                    presence.WorldPosition, new WorldVec2(candidate.WorldX, candidate.WorldY));
                var isCurrent = string.Equals(candidate.SiteId, currentSiteId, StringComparison.Ordinal);
                if (room != null && (bestIsCurrent && !isCurrent || bestIsCurrent == isCurrent && distance >= bestDistance))
                    continue;
                bestDistance = distance; bestIsCurrent = isCurrent; siteId = candidate.SiteId; room = candidate;
            }
            return room != null;
        }

        public static bool IsFoodPlanInProgress(MortalCivilianState state) => state != null &&
            (state.FoodFetchPhase == FoodFetchPhase.WaitingForPath ||
             state.FoodFetchPhase == FoodFetchPhase.Traveling ||
             state.FoodFetchPhase == FoodFetchPhase.Arrived);

        public static bool ShouldRetryFood(SimulationWorld world, Entity entity, MortalCivilianState state)
        {
            if (world == null || entity == null || state == null) return false;
            if (state.FoodFetchPhase == FoodFetchPhase.None || state.FoodFetchPhase == FoodFetchPhase.NeedFood)
                return true;
            if (IsFoodPlanInProgress(state) || state.FoodFetchPhase == FoodFetchPhase.Eating)
                return false;
            return world.Tick.Value >= state.FoodFetchRetryAfterTick ||
                   !string.Equals(state.FoodFetchFailureSignature,
                       BuildFoodConditionSignature(world, entity, state), StringComparison.Ordinal);
        }

        static void FailFoodPlan(
            SimulationWorld world, Entity entity, MortalCivilianState state,
            FoodFetchPhase phase, string status)
        {
            StopFoodMovement(entity);
            state.FoodFetchPhase = phase;
            state.FoodFetchStatus = status ?? string.Empty;
            state.FoodFetchRetryAfterTick = world.Tick.Value + FoodFetchRetryDelayTicks;
            state.FoodFetchFailureSignature = BuildFoodConditionSignature(world, entity, state);
            if (state.Energy < MortalCivilianTuning.GroundSleepThreshold)
            {
                state.Activity = MortalActivity.SleepOnGround;
                StartGroundSleep(world, entity, state);
            }
        }

        static string BuildFoodConditionSignature(
            SimulationWorld world, Entity entity, MortalCivilianState state)
        {
            var siteId = MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id);
            var roomId = world.SiteStorageRooms.TryGetBySite(siteId, out var room)
                ? room.StorageRoomId : string.Empty;
            var usable = world.Strategic.Sites.TryGet(siteId, out var site) && site != null && site.IsCoreActive &&
                         CanUseSiteFood(world, entity, state, site);
            return siteId + "|" + roomId + "|" + usable + "|" + CountSiteFood(world, siteId);
        }

        public static int CountSiteFood(SimulationWorld world, string siteId)
        {
            if (world == null || !world.Strategic.SitePublicStocks.TryGet(siteId ?? string.Empty, out var stock))
                return 0;
            var total = 0;
            foreach (var row in stock.Resources)
                if (row.Value > 0 && world.InventoryCatalog.HasTag(row.Key, "food"))
                    total = total > int.MaxValue - row.Value ? int.MaxValue : total + row.Value;
            return total;
        }

        static bool CanUseSiteFood(SimulationWorld world, Entity entity, MortalCivilianState state, WorldSite site)
        {
            if (entity.TryGet<FactionMembershipComponent>(out var membership) && membership.IsAffiliated)
                return CanAffiliatedUseSiteFood(world, membership.FactionId, site);
            return HasLocalResidentialFoodRight(world, entity.Id, state, site.SiteId);
        }

        static bool CanAffiliatedUseSiteFood(SimulationWorld world, string factionId, WorldSite site) =>
            WorldSiteStorageAccessPolicy.CanFactionUse(world, factionId, site) &&
            (string.Equals(factionId, site.OwnerFactionId, StringComparison.Ordinal) ||
             world.SiteStorageRooms.HasActiveStorageForSite(world, site.SiteId));

        static bool HasLocalResidentialFoodRight(
            SimulationWorld world, EntityId characterId, MortalCivilianState state, string siteId)
        {
            if (world == null || state == null || string.IsNullOrEmpty(siteId) ||
                !RoomBedFacilityService.TryGetAssignedHome(
                    world, characterId, out var bed, out var room, out _))
                return false;
            var homeSiteId = !string.IsNullOrWhiteSpace(room.BoundWorldSiteId)
                ? room.BoundWorldSiteId
                : RoomBedFacilityService.ResolveBedSiteId(world, bed);
            return room.RoomUsage == RoomUsage.Residential &&
                   string.Equals(homeSiteId, siteId, StringComparison.Ordinal);
        }

        public static bool CanUseCurrentSiteFood(SimulationWorld world, EntityId id)
        {
            return world != null && !id.IsNone && world.Entities.TryGet(id, out var entity) &&
                   world.Civilians.TryGet(id, out var state) &&
                   !string.IsNullOrEmpty(state.CurrentSiteId) &&
                   world.Strategic.Sites.TryGet(state.CurrentSiteId, out var site) && site != null &&
                   CanUseSiteFood(world, entity, state, site);
        }

        static bool TryFindSiteFood(SimulationWorld world, string siteId, out string foodId)
        {
            foodId = string.Empty;
            if (!world.Strategic.SitePublicStocks.TryGet(siteId, out var stock)) return false;
            var ids = new List<string>(stock.Resources.Keys); ids.Sort(StringComparer.Ordinal);
            for (var i = 0; i < ids.Count; i++)
                if (stock.GetCount(ids[i]) > 0 && world.InventoryCatalog.HasTag(ids[i], "food"))
                { foodId = ids[i]; return true; }
            return false;
        }

        static void StopFoodMovement(Entity entity)
        {
            if (entity.TryGet<MovementIntentComponent>(out var intent) && intent.HasWorldTarget)
                intent.Clear();
        }

        static void ClearCivilianMovement(Entity entity)
        {
            if (entity.TryGet<MovementIntentComponent>(out var intent)) intent.Clear();
        }

        static void ClearFoodTrip(Entity entity, MortalCivilianState state)
        {
            if (string.IsNullOrEmpty(state.FoodFetchStorageRoomId) &&
                string.IsNullOrEmpty(state.FoodFetchSourceSiteId) &&
                string.IsNullOrEmpty(state.PendingFoodResourceId) && !state.HasFoodFetchAccessPoint &&
                state.MealStartedTick == 0 && state.FoodFetchPhase == FoodFetchPhase.None &&
                state.FoodFetchRetryAfterTick == 0 && string.IsNullOrEmpty(state.FoodFetchFailureSignature)) return;
            StopFoodMovement(entity);
            state.FoodFetchStorageRoomId = string.Empty;
            state.FoodFetchSourceSiteId = string.Empty;
            state.PendingFoodResourceId = string.Empty;
            state.FoodFetchStatus = string.Empty;
            state.FoodFetchPhase = FoodFetchPhase.None;
            state.FoodFetchRetryAfterTick = 0;
            state.FoodFetchFailureSignature = string.Empty;
            state.HasFoodFetchAccessPoint = false;
            state.FoodFetchAccessWorldX = 0f;
            state.FoodFetchAccessWorldY = 0f;
            state.FoodFetchAccessCandidateIndex = -1;
            state.FoodFetchAccessAttemptCount = 0;
            state.MealStartedTick = 0;
        }

        public static void ResetTransientNeedsActivity(Entity entity, MortalCivilianState state)
        {
            if (entity == null || state == null) return;
            ClearFoodTrip(entity, state);
            ClearSleep(state);
            state.RescueStatus = string.Empty;
            state.Activity = MortalActivity.Idle;
        }

        static void Work(SimulationWorld world, Entity entity, string areaTag)
        {
            if (!MortalCivilianQuery.TryFindProfessionArea(world, entity, areaTag, out var area)) return;
            MoveToArea(world, entity, area.Id);
        }

        static void MoveToArea(SimulationWorld world, Entity entity, string areaId)
        {
            if (string.IsNullOrEmpty(areaId) || !world.TryGetWorkArea(areaId, out var area)) return;
            if (!entity.TryGet<MovementIntentComponent>(out var intent))
            { intent = new MovementIntentComponent(); entity.AddComponent(intent); }
            var arrivedAtOldPosition = intent.HostArrived && !MortalCivilianQuery.IsAtWorkArea(
                world, entity.Id, MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id), area);
            if (!intent.Active || arrivedAtOldPosition || !string.Equals(intent.TargetWorkAreaId, area.Id, StringComparison.Ordinal))
                intent.Begin(area.LocationId, area.Id, 0);
        }

        public static bool HasRescueTarget(SimulationWorld world, Entity rescuer)
        {
            var target = FindRescueTarget(world, rescuer);
            if (target != null) return true;
            if (world != null && rescuer != null && world.Civilians.TryGet(rescuer.Id, out var state))
            {
                const string noBed = "救援受阻：本地同势力伤员没有可用且可达的医疗／本人／空闲住宅床";
                if (HasLocalSameFactionInjured(world, rescuer)) state.RescueStatus = noBed;
                else if (string.Equals(state.RescueStatus, noBed, StringComparison.Ordinal))
                    state.RescueStatus = string.Empty;
            }
            return false;
        }

        static bool HasLocalSameFactionInjured(SimulationWorld world, Entity rescuer)
        {
            if (world == null || rescuer == null ||
                !rescuer.TryGet<FactionMembershipComponent>(out var own) || !own.IsAffiliated ||
                !CharacterWorldPresenceQuery.TryResolve(world, rescuer.Id, out var rescuerPresence) ||
                !rescuerPresence.HasWorldPosition) return false;
            foreach (var entity in world.Entities.All)
                if (entity.Id != rescuer.Id &&
                    entity.TryGet<FactionMembershipComponent>(out var faction) && faction.IsAffiliated &&
                    CanRescueCommunity(world, rescuer, entity) &&
                    entity.TryGet<LifecycleComponent>(out var life) && life.IsIncapacitated &&
                    !world.RecoverySpotDeliveries.ContainsInjured(entity.Id) &&
                    !IsRescueReserved(world, entity.Id) &&
                    CharacterWorldPresenceQuery.TryResolve(world, entity.Id, out var injuredPresence) &&
                    injuredPresence.HasWorldPosition &&
                    string.Equals(injuredPresence.SurfaceId, rescuerPresence.SurfaceId, StringComparison.Ordinal) &&
                    MortalCivilianQuery.IsWithinLocalContinuity(world, rescuerPresence.SurfaceId,
                        rescuerPresence.WorldPosition, injuredPresence.WorldPosition))
                    return true;
            return false;
        }

        static Entity FindRescueTarget(SimulationWorld world, Entity rescuer)
        {
            ValidateRecoverySpotPlacements(world);
            if (!IsEligibleAutomaticRescuer(world, rescuer, out var own, out _, out var rescuerPresence))
                return null;
            var injured = new List<Entity>();
            foreach (var other in world.Entities.All)
            {
                if (other.Id == rescuer.Id || HuangcunSurvivalService.IsPrisoner(world,other.Id) ||
                    !other.TryGet<FactionMembershipComponent>(out var faction) || !faction.IsAffiliated ||
                    !CanRescueCommunity(world, rescuer, other) ||
                    !other.TryGet<LifecycleComponent>(out var life) || !life.IsIncapacitated ||
                    !CharacterWorldPresenceQuery.TryResolve(world, other.Id, out var presence) || !presence.HasWorldPosition ||
                    !string.Equals(presence.SurfaceId, rescuerPresence.SurfaceId, StringComparison.Ordinal) ||
                    !MortalCivilianQuery.IsWithinLocalContinuity(world, rescuerPresence.SurfaceId,
                        rescuerPresence.WorldPosition, presence.WorldPosition) ||
                    MedicalRecoveryService.TryGetPatientBed(world, other, out _) ||
                    world.RecoverySpotDeliveries.ContainsInjured(other.Id) ||
                    IsRescueReserved(world, other.Id)) continue;
                injured.Add(other);
            }
            injured.Sort((a, b) =>
            {
                a.TryGet<LifecycleComponent>(out var al);
                b.TryGet<LifecycleComponent>(out var bl);
                if (al.IsDying != bl.IsDying) return al.IsDying ? -1 : 1;
                var deadline = al.BleedOutAfterTick.CompareTo(bl.BleedOutAfterTick);
                return deadline != 0 ? deadline : a.Id.Value.CompareTo(b.Id.Value);
            });
            for (var i = 0; i < injured.Count; i++)
            {
                var target = injured[i];
                if (!TryResolveBestRescuer(world, target, own.FactionId, out var best)) continue;
                // The first executable injury is the most urgent. Other workers must wait for its
                // selected rescuer instead of consuming the remaining bed for a later injury.
                return best == rescuer.Id ? target : null;
            }
            return null;
        }

        static bool IsEligibleAutomaticRescuer(
            SimulationWorld world,
            Entity rescuer,
            out FactionMembershipComponent faction,
            out string siteId,
            out CharacterWorldPresenceQuery.ResolvedPresence presence)
        {
            faction = null; siteId = string.Empty; presence = default;
            if (world == null || rescuer == null ||
                !MortalCivilianQuery.IsManagedCivilian(world, rescuer) ||
                !rescuer.TryGet<FactionMembershipComponent>(out faction) || !faction.IsAffiliated ||
                !rescuer.TryGet<LifecycleComponent>(out var life) || life.State != LifecycleState.Alive ||
                !world.Civilians.TryGet(rescuer.Id, out var state) ||
                state.Disposition != CivilianDisposition.Normal || !state.RescueTargetId.IsNone ||
                state.Energy < MortalCivilianTuning.GroundSleepThreshold ||
                world.Strategic.PlayerPartyContext?.IsMember(rescuer.Id) == true ||
                MortalTransportConstraintService.TryGetEscortedCaptive(world, rescuer.Id, out _) ||
                !MortalCivilianMovementAuthority.CanCivilianOwnLocalMovement(world, rescuer.Id, state) ||
                !CharacterWorldPresenceQuery.TryResolve(world, rescuer.Id, out presence) ||
                !presence.HasWorldPosition)
                return false;
            if (rescuer.TryGet<ActionStateComponent>(out var actions) && actions.HasActiveAction &&
                actions.ActiveOrderSource == OrderSource.Player)
                return false;
            foreach (var job in world.CivilianConstructionJobs.Jobs.Values)
                if (job.CarrierId == rescuer.Id && job.PayloadCount > 0)
                    return false;
            if (state.RescueRetryAfterTick > world.Tick.Value) return false;
            if (state.RescueRetryAfterTick != 0)
            { state.RescueRetryAfterTick = 0; state.RescueRejectedTargetId = EntityId.None; }
            siteId = MortalCivilianQuery.ResolveCurrentSiteId(world, rescuer.Id);
            return true;
        }

        static bool TryResolveBestRescuer(
            SimulationWorld world, Entity injured, string factionId, out EntityId best)
        {
            best = EntityId.None;
            if (!CharacterWorldPresenceQuery.TryResolve(world, injured.Id, out var injuredPresence) ||
                !injuredPresence.HasWorldPosition) return false;
            var bestCost = double.MaxValue;
            foreach (var pair in world.Civilians.All)
            {
                if (!world.Entities.TryGet(pair.Key, out var candidate) ||
                    !IsEligibleAutomaticRescuer(world, candidate, out var own, out _,
                        out var rescuerPresence) ||
                    !string.Equals(own.FactionId, factionId, StringComparison.Ordinal) ||
                    !string.Equals(rescuerPresence.SurfaceId, injuredPresence.SurfaceId, StringComparison.Ordinal) ||
                    pair.Value.RescueRejectedTargetId == injured.Id ||
                    !MortalCivilianQuery.IsWithinLocalContinuity(world, injuredPresence.SurfaceId,
                        rescuerPresence.WorldPosition, injuredPresence.WorldPosition) ||
                    !CanRoute(world, injuredPresence.SurfaceId, rescuerPresence.WorldPosition,
                        injuredPresence.WorldPosition) ||
                    !TryRescueDestination(world, candidate, injured.Id, string.Empty,
                        pair.Value.RescueRejectedDestinationId, out _, out var destination,
                        out var surfaceId, out _)) continue;
                var cost = DistanceSquared(rescuerPresence.WorldPosition, injuredPresence.WorldPosition) +
                           DistanceSquared(injuredPresence.WorldPosition, destination);
                if (cost < bestCost - .000001d ||
                    Math.Abs(cost - bestCost) <= .000001d && (best.IsNone || pair.Key.Value < best.Value))
                { best = pair.Key; bestCost = cost; }
            }
            return !best.IsNone;
        }

        static bool IsRescueReserved(SimulationWorld world, EntityId target)
        {
            foreach (var pair in world.Civilians.All)
                if (pair.Value.RescueTargetId == target) return true;
            return false;
        }

        static float DistanceSquared(WorldVec2 a, WorldVec2 b)
        { var x = a.X - b.X; var y = a.Y - b.Y; return x * x + y * y; }

        static bool CanRoute(SimulationWorld world, string surfaceId, WorldVec2 start, WorldVec2 goal)
        {
            if (world == null || !world.SurfaceGround.TryGet(surfaceId, out var navigation)) return false;
            var route = new List<WorldVec2>();
            return navigation.TryFindRoute(start, goal, route) == SurfaceGroundRouteStatus.Found;
        }

        public static bool IsRecoverySpotReserved(SimulationWorld world, string spotId, EntityId rescuerId)
        {
            foreach (var pair in world.Civilians.All)
                if (pair.Key != rescuerId && !pair.Value.RescueTargetId.IsNone &&
                    string.Equals(pair.Value.RescueDestinationId, spotId, StringComparison.Ordinal)) return true;
            return false;
        }

        public static bool TryRescueDestination(SimulationWorld world, Entity rescuer, EntityId injured,
            string requestedSpotId, string excludedSpotId,
            out string destinationId, out WorldVec2 destination, out string surfaceId,
            out float deliveryRange)
        {
            destinationId = surfaceId = string.Empty; destination = default; deliveryRange = 0f;
            if (world == null || rescuer == null || injured.IsNone ||
                HuangcunSurvivalService.IsPrisoner(world,injured) ||
                !world.Entities.TryGet(injured, out var injuredEntity) ||
                !injuredEntity.TryGet<LifecycleComponent>(out var injuredLife) || !injuredLife.IsIncapacitated)
                return false;
            if (!rescuer.TryGet<FactionMembershipComponent>(out var own) || !own.IsAffiliated ||
                !injuredEntity.TryGet<FactionMembershipComponent>(out var injuredFaction) || !injuredFaction.IsAffiliated ||
                !CanRescueCommunity(world, rescuer, injuredEntity) ||
                !CharacterWorldPresenceQuery.TryResolve(world, rescuer.Id, out var rescuerPresence) ||
                !rescuerPresence.HasWorldPosition ||
                !CharacterWorldPresenceQuery.TryResolve(world, injured, out var injuredPresence) ||
                !injuredPresence.HasWorldPosition ||
                !string.Equals(rescuerPresence.SurfaceId, injuredPresence.SurfaceId, StringComparison.Ordinal) ||
                !MortalCivilianQuery.IsWithinLocalContinuity(world, injuredPresence.SurfaceId,
                    rescuerPresence.WorldPosition, injuredPresence.WorldPosition)) return false;
            surfaceId = injuredPresence.SurfaceId;
            var keys = new List<string>(world.OutdoorConstructedAssets.Assets.Keys);
            keys.Sort(StringComparer.Ordinal);
            var bestPriority = int.MaxValue;
            var bestDistance = double.MaxValue;
            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i]; var asset = world.OutdoorConstructedAssets.Assets[key];
                var bedSiteId = RoomBedFacilityService.ResolveBedSiteId(world, asset);
                if (!RoomBedGeometry.IsBed(asset) || asset.SurfaceId != surfaceId ||
                    IsRejectedDestination(excludedSpotId, key) ||
                    string.IsNullOrEmpty(bedSiteId) ||
                    !world.Strategic.Sites.TryGet(bedSiteId, out var bedSite) || bedSite == null ||
                    !bedSite.IsCoreActive ||
                    !string.Equals(bedSite.OwnerFactionId, own.FactionId, StringComparison.Ordinal) ||
                    (!world.RecoverySpotDeliveries.TryGetInjured(key, out var placed) ? false : placed != injured) ||
                    IsRecoverySpotReserved(world, key, rescuer.Id)) continue;
                var priority = RoomBedFacilityService.BedHasUsage(world, asset, RoomUsage.Medical) ? 0 :
                    RoomBedFacilityService.BedHasUsage(world, asset, RoomUsage.Residential) &&
                    asset.AssignedCharacterId == injured ? 1 :
                    RoomBedFacilityService.BedHasUsage(world, asset, RoomUsage.Residential) &&
                    asset.AssignedCharacterId.IsNone ? 2 : int.MaxValue;
                if (priority == int.MaxValue) continue;
                if ((!asset.ReservedCharacterId.IsNone && asset.ReservedCharacterId != injured) ||
                    (!asset.OccupantCharacterId.IsNone && asset.OccupantCharacterId != injured)) continue;
                var center = new WorldVec2(asset.WorldX + asset.WorldWidth * .5f,
                    asset.WorldY + asset.WorldHeight * .5f);
                if (!RoomBedFacilityService.CanUseBed(world, asset, injured, out _) ||
                    !RoomBedFacilityService.TryResolveAccessPoint(world, asset, out var access) ||
                    !MortalCivilianQuery.IsWithinLocalContinuity(world, surfaceId,
                        injuredPresence.WorldPosition, center)) continue;
                var requested = string.Equals(key, requestedSpotId, StringComparison.Ordinal);
                if (requested && rescuer.TryGet<MovementIntentComponent>(out var retainedIntent) &&
                    retainedIntent.Active && retainedIntent.HasWorldTarget &&
                    string.Equals(retainedIntent.DestinationKey, key, StringComparison.Ordinal))
                {
                    var retained = new WorldVec2(
                        retainedIntent.TargetWorldX, retainedIntent.TargetWorldY);
                    if (RoomBedGeometry.IsAccessPoint(
                            world.OutdoorConstructedAssets, asset, retained))
                        access = retained;
                }
                var distance = DistanceSquared(injuredPresence.WorldPosition, access);
                if (!requested && (priority > bestPriority ||
                    priority == bestPriority && distance > bestDistance + .000001d)) continue;
                if (!requested && priority == bestPriority && Math.Abs(distance - bestDistance) <= .000001d &&
                    !string.IsNullOrEmpty(destinationId) &&
                    StringComparer.Ordinal.Compare(key, destinationId) >= 0) continue;
                // Run the comparatively expensive route query only for a candidate that can
                // actually improve the current priority/distance choice.
                if (!CanRoute(world, surfaceId, injuredPresence.WorldPosition, access)) continue;
                bestPriority = priority; bestDistance = distance;
                destinationId = asset.StableAssetId; destination = access;
                deliveryRange = world.SurfaceSpatial.TryGet(surfaceId, out var metric) && metric != null
                    ? metric.CellSize * .75f : Math.Min(asset.WorldWidth, asset.WorldHeight) * .5f;
                if (requested) return true;
            }
            return !string.IsNullOrEmpty(destinationId);
        }

        static bool IsRejectedDestination(string rejected, string destinationId)
        {
            if (string.IsNullOrEmpty(rejected) || string.IsNullOrEmpty(destinationId)) return false;
            var start = 0;
            while (start <= rejected.Length)
            {
                var end = rejected.IndexOf('\n', start);
                if (end < 0) end = rejected.Length;
                if (string.Equals(rejected.Substring(start, end - start), destinationId,
                        StringComparison.Ordinal)) return true;
                if (end == rejected.Length) break;
                start = end + 1;
            }
            return false;
        }

        static void RejectDestination(MortalCivilianState state, string destinationId)
        {
            if (state == null || string.IsNullOrEmpty(destinationId) ||
                IsRejectedDestination(state.RescueRejectedDestinationId, destinationId)) return;
            state.RescueRejectedDestinationId = string.IsNullOrEmpty(state.RescueRejectedDestinationId)
                ? destinationId : state.RescueRejectedDestinationId + "\n" + destinationId;
        }

        static bool TryClaimRescueDestination(
            SimulationWorld world, Entity rescuer, EntityId injured, string requestedId,
            MortalCivilianState state, out string destinationId, out WorldVec2 destination,
            out string surfaceId, out float deliveryRange)
        {
            if (!TryRescueDestination(world, rescuer, injured, requestedId,
                    state.RescueRejectedDestinationId, out destinationId, out destination,
                    out surfaceId, out deliveryRange) ||
                !world.OutdoorConstructedAssets.Assets.TryGetValue(destinationId, out var bed) ||
                !bed.ReservedCharacterId.IsNone && bed.ReservedCharacterId != injured ||
                !bed.OccupantCharacterId.IsNone && bed.OccupantCharacterId != injured)
                return false;
            bed.ReservedCharacterId = injured;
            return true;
        }

        static void RescueOneAlly(SimulationWorld world, Entity rescuer)
        {
            var state = world.Civilians.GetOrCreate(rescuer.Id);
            if (state.RescueRetryAfterTick > world.Tick.Value)
            { state.RescueStatus = "救援路径暂未准备，等待重试"; return; }
            if (state.RescueRetryAfterTick != 0)
            { state.RescueRetryAfterTick = 0; state.RescueRejectedTargetId = EntityId.None; }
            var target = state.RescueTargetId.IsNone ? FindRescueTarget(world, rescuer) :
                world.Entities.TryGet(state.RescueTargetId, out var saved) ? saved : null;
            if (target == null || !target.TryGet<LifecycleComponent>(out var life) || !life.IsIncapacitated ||
                !TryClaimRescueDestination(world, rescuer, target?.Id ?? EntityId.None,
                    state.RescueDestinationId, state,
                    out var destinationId, out var destination, out var surfaceId, out var deliveryRange) ||
                !CharacterWorldPresenceQuery.TryResolve(world, rescuer.Id, out var rescuerPresence) ||
                !rescuerPresence.HasWorldPosition || rescuerPresence.SurfaceId != surfaceId ||
                !CharacterWorldPresenceQuery.TryResolve(world, target.Id, out var targetPresence) ||
                !targetPresence.HasWorldPosition || targetPresence.SurfaceId != surfaceId)
            {
                var exhaustedDestinations = target != null &&
                    !string.IsNullOrEmpty(state.RescueRejectedDestinationId);
                state.RescueStatus = exhaustedDestinations
                    ? "救援受阻：当前床位方案均不可达，稍后改由其它方案重试"
                    : "救援受阻：没有合法伤员、床位或本地路径";
                if (exhaustedDestinations)
                {
                    state.RescueRejectedTargetId = target.Id;
                    state.RescueRetryAfterTick = world.Tick.Value + MortalCivilianTuning.RescueRetryDelayTicks;
                }
                DropRescue(world, rescuer, state, preserveRetry: exhaustedDestinations);
                return;
            }
            state.RescueTargetId = target.Id;
            state.RescueDestinationId = destinationId;
            if (!state.RescueCarrying)
            {
                state.RescueStatus = "前往伤员";
                var pickupRange = world.SurfaceSpatial.TryGet(surfaceId, out var pickupMetric) && pickupMetric != null
                    ? pickupMetric.CellSize * 1.5f : .1f;
                MoveToWorldPoint(rescuer, targetPresence.SurfaceId, targetPresence.WorldPosition,
                    destinationKey: "rescue-target:" + target.Id.Value,
                    arrivalRadiusWorld: pickupRange);
                if (rescuer.TryGet<MovementIntentComponent>(out var failedApproach) &&
                    failedApproach.HostPathState == MovementHostPathState.RetryablePathUnavailable)
                {
                    state.RescueStatus = "救援路径暂未准备，保留任务稍后重试";
                    state.RescueRetryAfterTick = world.Tick.Value + MortalCivilianTuning.RescueRetryDelayTicks;
                    failedApproach.Clear();
                    return;
                }
                if (rescuer.TryGet<MovementIntentComponent>(out failedApproach) &&
                    failedApproach.HostPathState == MovementHostPathState.PermanentFailure)
                {
                    state.RescueStatus = "救援受阻：伤员不可达，稍后改由其它方案重试";
                    state.RescueRejectedTargetId = target.Id;
                    state.RescueRetryAfterTick = world.Tick.Value + MortalCivilianTuning.RescueRetryDelayTicks;
                    DropRescue(world, rescuer, state, preserveRetry: true);
                    return;
                }
                if (rescuer.TryGet<MovementIntentComponent>(out var approach) && approach.HostArrived &&
                    DistanceSquared(rescuerPresence.WorldPosition, targetPresence.WorldPosition) <= pickupRange * pickupRange)
                {
                    state.RescueCarrying = true;
                    state.RescueStatus = "正在搬往床位";
                    approach.Clear();
                }
                else return;
            }
            // Pickup has no work duration: issue the bed-bound intent in this same update.
            world.WorldPresence.SetAtWorldPosition(target.Id, rescuerPresence.WorldPosition, surfaceId);
            MoveToWorldPoint(rescuer, surfaceId, destination, destinationKey: destinationId,
                arrivalRadiusWorld: deliveryRange);
            if (rescuer.TryGet<MovementIntentComponent>(out var failedDelivery) &&
                failedDelivery.HostPathState == MovementHostPathState.RetryablePathUnavailable)
            {
                state.RescueStatus = "安置路径暂未准备，保留床位稍后重试";
                state.RescueRetryAfterTick = world.Tick.Value + MortalCivilianTuning.RescueRetryDelayTicks;
                failedDelivery.Clear();
                return;
            }
            if (rescuer.TryGet<MovementIntentComponent>(out failedDelivery) &&
                failedDelivery.HostPathState == MovementHostPathState.PermanentFailure)
            {
                RoomBedFacilityService.Release(world, target.Id);
                RejectDestination(state, destinationId);
                state.RescueDestinationId = string.Empty;
                state.RescueStatus = "安置受阻：床位不可达，正在改选";
                failedDelivery.Clear();
                return;
            }
            if (rescuer.TryGet<MovementIntentComponent>(out var delivery) && delivery.HostArrived &&
                DistanceSquared(rescuerPresence.WorldPosition, destination) <= deliveryRange * deliveryRange)
            {
                if (!world.RecoverySpotDeliveries.TryPlace(destinationId, target.Id))
                { state.RescueStatus = "安置失败：床位记录冲突"; DropRescue(world, rescuer, state); return; }
                if (!world.OutdoorConstructedAssets.Assets.TryGetValue(destinationId, out var bed) ||
                    !RoomBedGeometry.IsBed(bed))
                {
                    world.RecoverySpotDeliveries.RemoveSpot(destinationId);
                    state.RescueStatus = "安置失败：目标床位已失效";
                    DropRescue(world, rescuer, state);
                    return;
                }
                var usage = RoomBedFacilityService.BedHasUsage(world, bed, RoomUsage.Medical)
                    ? RoomUsage.Medical : RoomUsage.Residential;
                var placed = RoomBedFacilityService.TryPlaceCarriedOccupant(
                    world, destinationId, rescuer.Id, target.Id, usage);
                if (placed.IsFailure)
                {
                    world.RecoverySpotDeliveries.RemoveSpot(destinationId);
                    RoomBedFacilityService.Release(world, target.Id);
                    RejectDestination(state, destinationId);
                    state.RescueDestinationId = string.Empty;
                    state.RescueStatus = "安置失败：" + placed.Error.Message + "，正在改选";
                    delivery.Clear();
                    return;
                }
                delivery.Clear(); state.RescueTargetId = EntityId.None;
                state.RescueCarrying = false; state.RescueDestinationId = string.Empty;
                state.RescueRejectedDestinationId = string.Empty;
                state.RescueRejectedTargetId = EntityId.None; state.RescueRetryAfterTick = 0;
                state.RescueStatus = "已完成床位安置";
            }
        }

        static void DropRescue(
            SimulationWorld world, Entity rescuer, MortalCivilianState state, bool preserveRetry = false)
        {
            var rescued = state.RescueTargetId;
            var ownsTarget = CanReceiveAutomaticRescue(world, rescued);
            var destinationId = state.RescueDestinationId;
            if (ownsTarget && state.RescueCarrying && !state.RescueTargetId.IsNone &&
                CharacterWorldPresenceQuery.TryResolve(world, rescuer.Id, out var presence) && presence.HasWorldPosition)
                world.WorldPresence.SetAtWorldPosition(state.RescueTargetId, presence.WorldPosition, presence.SurfaceId);
            state.RescueTargetId = EntityId.None; state.RescueCarrying = false;
            state.RescueDestinationId = string.Empty;
            state.RescueRejectedDestinationId = string.Empty;
            if (!preserveRetry)
            { state.RescueRejectedTargetId = EntityId.None; state.RescueRetryAfterTick = 0; }
            if (ownsTarget) RoomBedFacilityService.Release(world, rescued);
            else if (!string.IsNullOrEmpty(destinationId) &&
                world.OutdoorConstructedAssets.Assets.TryGetValue(destinationId, out var oldBed) &&
                oldBed.ReservedCharacterId == rescued && oldBed.OccupantCharacterId.IsNone &&
                (RoomBedFacilityService.BedHasUsage(world, oldBed, RoomUsage.Medical) ||
                 RoomBedFacilityService.BedHasUsage(world, oldBed, RoomUsage.Residential)))
                oldBed.ReservedCharacterId = EntityId.None;
        }

        public static Result ValidateRecoverySpotPlacements(SimulationWorld world, bool rejectInvalid = false)
        {
            if (world == null) return Invalid("World is required.");
            var remove = new List<string>();
            foreach (var pair in world.RecoverySpotDeliveries.Placements)
            {
                if (!world.OutdoorConstructedAssets.Assets.TryGetValue(pair.Key, out var spot) ||
                    !(RoomBedGeometry.IsBed(spot) &&
                      (RoomBedFacilityService.BedHasUsage(world, spot, RoomUsage.Medical) ||
                       RoomBedFacilityService.BedHasUsage(world, spot, RoomUsage.Residential) ||
                       RoomBedFacilityService.BedHasUsage(world, spot, RoomUsage.Prison))) ||
                    !world.Entities.TryGet(pair.Value, out var injured) ||
                    !injured.TryGet<LifecycleComponent>(out var life) || !life.IsIncapacitated ||
                    spot.OccupantCharacterId != pair.Value ||
                    !RoomBedFacilityService.IsAtBed(world, spot, pair.Value) ||
                    !CharacterWorldPresenceQuery.TryResolve(world, pair.Value, out var presence) ||
                    !presence.HasWorldPosition || presence.SurfaceId != spot.SurfaceId)
                { remove.Add(pair.Key); continue; }
                var center = new WorldVec2(spot.WorldX + spot.WorldWidth * .5f, spot.WorldY + spot.WorldHeight * .5f);
                var range = world.SurfaceSpatial.TryGet(spot.SurfaceId, out var metric) && metric != null
                    ? metric.CellSize * .75f : Math.Min(spot.WorldWidth, spot.WorldHeight) * .5f;
                if (DistanceSquared(presence.WorldPosition, center) > range * range &&
                    !RoomBedFacilityService.IsAtAccessPoint(world, spot, pair.Value)) remove.Add(pair.Key);
            }
            if (rejectInvalid && remove.Count > 0)
                return Result.Failure(ErrorCode.SnapshotInvalid,
                    "Recovery spot placement no longer matches its physical delivery point.", remove[0]);
            for (var i = 0; i < remove.Count; i++)
            {
                if (world.RecoverySpotDeliveries.TryGetInjured(remove[i], out var injured))
                    RoomBedFacilityService.Release(world, injured);
                world.RecoverySpotDeliveries.RemoveSpot(remove[i]);
            }
            return Result.Success();
        }

        public static void InterruptTransportsForEncounter(SimulationWorld world, CharacterEncounterState encounter)
        {
            if (world == null || encounter == null) return;
            foreach (var pair in world.Civilians.All)
            {
                if (encounter.Find(pair.Key.Value) != null && world.Entities.TryGet(pair.Key, out var worker))
                {
                    DropRescue(world, worker, pair.Value);
                    CivilianConstructionJobService.ReleaseCarrier(world, worker.Id);
                }
            }
        }

        public static int ReconcileEscortsForEncounter(SimulationWorld world, CharacterEncounterState encounter)
        {
            if (world == null || encounter == null) return 0;
            var repaired = 0;
            foreach (var pair in world.Civilians.All)
            {
                var state = pair.Value;
                if (state.Disposition != CivilianDisposition.CapturedEscorted || state.CarrierId.IsNone ||
                    !world.Entities.TryGet(pair.Key, out var captive)) continue;
                var carrier = encounter.Find(state.CarrierId.Value);
                if (carrier == null) continue;
                var participant = encounter.Find(captive.Id.Value);
                if (participant != null)
                {
                    participant.TacticalX = carrier.TacticalX;
                    participant.TacticalY = carrier.TacticalY;
                    participant.TargetId = 0;
                }
                var presence = world.WorldPresence.GetOrCreate(captive.Id);
                if (presence.Mode != PartyWorldPresenceMode.InEncounter ||
                    presence.PersonalSurfaceId != encounter.SourceSurfaceId ||
                    !presence.HasContinuousWorldPosition || presence.WorldPosX != carrier.TacticalX ||
                    presence.WorldPosY != carrier.TacticalY) repaired++;
                presence.Mode = PartyWorldPresenceMode.InEncounter;
                presence.SiteId = string.Empty;
                presence.PersonalSurfaceId = encounter.SourceSurfaceId;
                presence.WorldPosX = carrier.TacticalX;
                presence.WorldPosY = carrier.TacticalY;
                presence.HasContinuousWorldPosition = true;
                if (!world.Entities.TryGet(state.CarrierId, out var carrierEntity) ||
                    !CombatLifeStateService.CanFight(carrierEntity))
                    ReleaseEscortedAtCarrier(world, captive, state);
            }
            return repaired;
        }

        public static void ReconcileEscortsAfterEncounter(SimulationWorld world, CharacterEncounterState encounter)
        {
            if (world == null || encounter == null) return;
            foreach (var pair in world.Civilians.All)
            {
                var state = pair.Value;
                if (state.Disposition != CivilianDisposition.CapturedEscorted || state.CarrierId.IsNone ||
                    encounter.Find(state.CarrierId.Value) == null ||
                    !world.Entities.TryGet(pair.Key, out var captive)) continue;
                if (world.Entities.TryGet(state.CarrierId, out var carrierEntity) &&
                    CombatLifeStateService.CanFight(carrierEntity) &&
                    CharacterWorldPresenceQuery.TryResolve(world, state.CarrierId, out var carrier) &&
                    carrier.HasWorldPosition)
                {
                    world.WorldPresence.SetAtWorldPosition(captive.Id, carrier.WorldPosition, carrier.SurfaceId);
                    continue;
                }
                ReleaseEscortedAtCarrier(world, captive, state);
            }
        }

        public static void CancelAllRescues(SimulationWorld world)
        {
            if (world == null) return;
            foreach (var pair in world.Civilians.All)
                if (world.Entities.TryGet(pair.Key, out var rescuer))
                    DropRescue(world, rescuer, pair.Value);
        }

        static void MoveToWorldPoint(Entity entity, string surfaceId, WorldVec2 point, float speedMultiplier = 1f,
            string destinationKey = "", float arrivalRadiusWorld = 0f)
        {
            if (!entity.TryGet<MovementIntentComponent>(out var intent))
            { intent = new MovementIntentComponent(); entity.AddComponent(intent); }
            if (!intent.Active || !intent.HasWorldTarget || intent.TargetSurfaceId != surfaceId ||
                !string.Equals(intent.DestinationKey, destinationKey ?? string.Empty, StringComparison.Ordinal) ||
                Math.Abs(intent.TargetWorldX - point.X) > .001f || Math.Abs(intent.TargetWorldY - point.Y) > .001f ||
                Math.Abs(intent.ArrivalRadiusWorld - arrivalRadiusWorld) > .0001f ||
                Math.Abs(intent.SpeedMultiplier - speedMultiplier) > .001f)
                intent.BeginWorldPoint(surfaceId, point.X, point.Y, speedMultiplier,
                    destinationKey, arrivalRadiusWorld);
        }

        public static void OnSiteTransferred(SimulationWorld world, string siteId, string oldFactionId, string newFactionId)
        {
            if (world == null || string.IsNullOrEmpty(siteId) || string.Equals(oldFactionId, newFactionId, StringComparison.Ordinal)) return;
            foreach (var entity in world.Entities.All)
            {
                var state = Ensure(world, entity);
                if (state == null || state.Disposition == CivilianDisposition.Detained ||
                    state.Disposition == CivilianDisposition.CapturedEscorted ||
                    !entity.TryGet<FactionMembershipComponent>(out var member) || !member.IsAffiliated ||
                    !string.Equals(member.FactionId, oldFactionId, StringComparison.Ordinal) ||
                    !string.Equals(MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id), siteId, StringComparison.Ordinal)) continue;
                state.Disposition = CharacterFactionLoyaltyService.TryGetLoyalty(world, entity.Id, out var loyalty) &&
                    loyalty < MortalCivilianTuning.SurrenderLoyaltyThreshold
                    ? CivilianDisposition.SurrenderWaiting : CivilianDisposition.Fleeing;
                if (state.Disposition == CivilianDisposition.Fleeing) BeginFlee(world, entity, state, siteId);
            }
        }

        /// <summary>Threat reaction without conquest/capture: unarmed managed mortals flee or wait to surrender.</summary>
        public static bool ReactToHostileContact(SimulationWorld world, EntityId civilianId)
        {
            if (world == null || !world.Entities.TryGet(civilianId, out var entity) ||
                !MortalCivilianQuery.IsManagedCivilian(world, entity) ||
                !world.Civilians.TryGet(civilianId, out var state) ||
                state.Disposition != CivilianDisposition.Normal ||
                !CombatLifeStateService.CanFight(entity) ||
                entity.TryGet<PersonalityProfileComponent>(out var profile) && profile.HasTag("hostile"))
                return false;
            if (CharacterFactionLoyaltyService.TryGetLoyalty(world, civilianId, out var loyalty) &&
                loyalty < MortalCivilianTuning.SurrenderLoyaltyThreshold ||
                SeparateSpaceCombatPolicy.IsEntityInActiveSpace(world, civilianId))
            {
                state.Disposition = CivilianDisposition.SurrenderWaiting;
                state.ScheduleStatus = "遭遇敌对者，等待投降处理";
                return true;
            }
            var siteId = MortalCivilianQuery.ResolveCurrentSiteId(world, civilianId);
            if (string.IsNullOrEmpty(siteId)) return false;
            state.Disposition = CivilianDisposition.Fleeing;
            BeginFlee(world, entity, state, siteId);
            state.ScheduleStatus = "遭遇敌对者，逃离";
            return true;
        }

        static void BeginFlee(SimulationWorld world, Entity entity, MortalCivilianState state, string siteId)
        {
            if (!world.Strategic.Sites.TryGet(siteId, out var site) || site == null || !site.HasContinuousCore) return;
            state.FleeSourceSiteId = siteId;
            state.FleeCandidateIndex = (int)(entity.Id.Value % (ulong)MortalCivilianTuning.FleeCandidateCount);
            state.FleeAttemptCount = 0;
            SetFleeCandidate(world, site, state);
            state.FleeStartedTick = world.Tick.Value + entity.Id.Value % 13UL;
            state.FleeStatus = "等待逃离";
        }

        static void AdvanceFlee(SimulationWorld world, Entity entity, MortalCivilianState state)
        {
            if (world.Tick.Value < state.FleeStartedTick) return;
            if (string.IsNullOrEmpty(state.FleeSourceSiteId) ||
                !world.Strategic.Sites.TryGet(state.FleeSourceSiteId, out var sourceSite) ||
                sourceSite == null || !sourceSite.HasContinuousCore)
            { state.FleeStatus = "逃离受阻：来源据点无效"; return; }
            if (!CharacterWorldPresenceQuery.TryResolve(world, entity.Id, out var current) || !current.HasWorldPosition) return;
            if (!string.Equals(current.SurfaceId, sourceSite.CoreSurfaceId, StringComparison.Ordinal))
            { state.FleeStatus = "逃离受阻：人物已离开来源地表"; return; }
            if (HasEscapedSourceSite(world, sourceSite, current.WorldPosition))
            {
                state.Disposition = CivilianDisposition.Displaced; state.HasFleeTarget = false;
                state.FleeStatus = "已离开失守据点";
                state.CurrentSiteId = string.Empty; state.ResidenceWorkAreaId = string.Empty;
                state.RefugeTargetSiteId = string.Empty;
                state.BackgroundNeedsLastProcessedTick = world.Tick.Value;
                ClearDisplacedLocalActivity(world, entity, state);
                if (entity.TryGet<ActivityTendencyComponent>(out var tendency)) tendency.HomeWorkAreaId = string.Empty;
                return;
            }
            if (!state.HasFleeTarget) SetFleeCandidate(world, sourceSite, state);
            // The Host consumes MovementIntent only for materialized characters. Cold residents
            // must advance their own real WorldPresence, or an offscreen escape stalls forever.
            if (!world.ContinuousOutdoorMaterialization.IsMaterialized(entity.Id))
            {
                if (entity.TryGet<MovementIntentComponent>(out var staleIntent) && staleIntent.Active)
                    staleIntent.Clear();
                AdvanceColdFlee(world, entity, state, sourceSite, current.WorldPosition);
                return;
            }
            MoveToWorldPoint(entity, sourceSite.CoreSurfaceId,
                new WorldVec2(state.FleeTargetX, state.FleeTargetY),
                MortalCivilianTuning.FleeMoveSpeedMultiplier);
            if (!entity.TryGet<MovementIntentComponent>(out var intent))
            { state.FleeStatus = "逃离受阻：移动意图未建立"; return; }
            if (intent.HostPathState == MovementHostPathState.RetryablePathUnavailable ||
                intent.HostPathState == MovementHostPathState.PermanentFailure || intent.HostArrived)
            {
                state.FleeStatus = intent.HostArrived ? "逃离出口未越过边界，改选出口" :
                    "逃离路径失败：" + intent.HostPathFailureReason;
                intent.Clear();
                state.FleeAttemptCount++;
                state.FleeCandidateIndex =
                    (state.FleeCandidateIndex + 1) % MortalCivilianTuning.FleeCandidateCount;
                if (state.FleeAttemptCount >= MortalCivilianTuning.FleeCandidateCount)
                {
                    state.FleeAttemptCount = 0;
                    state.HasFleeTarget = false;
                    state.FleeStartedTick = world.Tick.Value + MortalCivilianTuning.FleeRetryDelayTicks;
                    state.FleeStatus = "逃离受阻：全部出口暂不可达，等待重试";
                    return;
                }
                SetFleeCandidate(world, sourceSite, state);
                return;
            }
            state.FleeStatus = intent.HostPathRequested ? "逃离中" : "等待逃离路径";
        }

        static void AdvanceColdFlee(SimulationWorld world, Entity entity,
            MortalCivilianState state, WorldSite sourceSite, WorldVec2 start)
        {
            if (world.Tick.Value % 8UL != entity.Id.Value % 8UL) return;
            var goal = new WorldVec2(state.FleeTargetX, state.FleeTargetY);
            var route = new List<WorldVec2>(32);
            if (!world.SurfaceGround.TryGet(sourceSite.CoreSurfaceId, out var navigation) ||
                navigation == null ||
                navigation.TryFindRoute(start, goal, route) != SurfaceGroundRouteStatus.Found)
            {
                state.FleeAttemptCount++;
                state.FleeCandidateIndex =
                    (state.FleeCandidateIndex + 1) % MortalCivilianTuning.FleeCandidateCount;
                if (state.FleeAttemptCount >= MortalCivilianTuning.FleeCandidateCount)
                {
                    state.FleeAttemptCount = 0;
                    state.HasFleeTarget = false;
                    state.FleeStartedTick = world.Tick.Value + MortalCivilianTuning.FleeRetryDelayTicks;
                    state.FleeStatus = "离屏逃离受阻：等待重试";
                }
                else SetFleeCandidate(world, sourceSite, state);
                return;
            }
            var position = start;
            var remaining = MortalCivilianTuning.FleeMoveSpeedMultiplier * 8f;
            for (var i = 1; i < route.Count && remaining > 0f; i++)
            {
                var waypoint = route[i];
                var distance = WorldVec2.Distance(position, waypoint);
                if (distance <= remaining)
                { position = waypoint; remaining -= distance; }
                else
                {
                    var ratio = remaining / distance;
                    position = new WorldVec2(position.X + (waypoint.X - position.X) * ratio,
                        position.Y + (waypoint.Y - position.Y) * ratio);
                    remaining = 0f;
                }
            }
            world.WorldPresence.SetAtWorldPosition(entity.Id, position, sourceSite.CoreSurfaceId);
            state.FleeStatus = "离屏逃离中";
        }

        static void SetFleeCandidate(SimulationWorld world, WorldSite site, MortalCivilianState state)
        {
            var halfW = site.CoreRangeWidth * .5f;
            var halfH = site.CoreRangeHeight * .5f;
            var buffer = ResolveFleeBoundaryBuffer(world, site);
            var outX = halfW + buffer;
            var outY = halfH + buffer;
            var offsetX = halfW * .45f;
            var offsetY = halfH * .45f;
            switch ((state.FleeCandidateIndex % MortalCivilianTuning.FleeCandidateCount +
                     MortalCivilianTuning.FleeCandidateCount) % MortalCivilianTuning.FleeCandidateCount)
            {
                case 0: state.FleeTargetX = site.CoreWorldX - offsetX; state.FleeTargetY = site.CoreWorldY + outY; break;
                case 1: state.FleeTargetX = site.CoreWorldX + offsetX; state.FleeTargetY = site.CoreWorldY + outY; break;
                case 2: state.FleeTargetX = site.CoreWorldX + outX; state.FleeTargetY = site.CoreWorldY + offsetY; break;
                case 3: state.FleeTargetX = site.CoreWorldX + outX; state.FleeTargetY = site.CoreWorldY - offsetY; break;
                case 4: state.FleeTargetX = site.CoreWorldX + offsetX; state.FleeTargetY = site.CoreWorldY - outY; break;
                case 5: state.FleeTargetX = site.CoreWorldX - offsetX; state.FleeTargetY = site.CoreWorldY - outY; break;
                case 6: state.FleeTargetX = site.CoreWorldX - outX; state.FleeTargetY = site.CoreWorldY - offsetY; break;
                default: state.FleeTargetX = site.CoreWorldX - outX; state.FleeTargetY = site.CoreWorldY + offsetY; break;
            }
            state.HasFleeTarget = true;
        }

        static bool HasEscapedSourceSite(SimulationWorld world, WorldSite site, WorldVec2 position)
        {
            var buffer = ResolveFleeBoundaryBuffer(world, site);
            var limitX = site.CoreRangeWidth * .5f + buffer;
            var limitY = site.CoreRangeHeight * .5f + buffer;
            return Math.Abs(position.X - site.CoreWorldX) >= limitX - .01f ||
                   Math.Abs(position.Y - site.CoreWorldY) >= limitY - .01f;
        }

        static float ResolveFleeBoundaryBuffer(SimulationWorld world, WorldSite site)
        {
            if (world?.SurfaceSpatial != null && site != null &&
                world.SurfaceSpatial.TryGet(site.CoreSurfaceId, out var metric) && metric != null &&
                metric.CellSize > 0f && !float.IsNaN(metric.CellSize) && !float.IsInfinity(metric.CellSize))
                return metric.CellSize * MortalCivilianTuning.FleeBoundaryBufferCells;
            return .1f;
        }

        public static Result Recruit(SimulationWorld world, EntityId actor, EntityId target)
        {
            if (PrisonManagementService.IsOriginalCompanion(world, target)) return Invalid("同伴乙无需招募，请直接释放。");
            if (!TryInteractive(world, target, out var entity, out var state)) return Invalid("Target is not an interactive mortal civilian.");
            if (state.Disposition != CivilianDisposition.SurrenderWaiting && state.Disposition != CivilianDisposition.Detained)
                return Invalid("Recruitment requires surrender or detention.");
            if (state.Disposition == CivilianDisposition.Detained)
            {
                var consent = PrisonManagementService.ValidateRecruit(world, actor, target);
                if (consent.IsFailure) return consent;
            }
            else if (!CharacterFactionLoyaltyService.TryGetLoyalty(world, target, out var loyalty) ||
                     loyalty >= MortalCivilianTuning.SurrenderLoyaltyThreshold) return Invalid("此人仍不愿归降。");
            if (string.IsNullOrEmpty(world.Strategic.PlayerFactionId)) return Invalid("Player faction is missing.");
            if (!entity.TryGet<FactionMembershipComponent>(out var membership)) { membership = new FactionMembershipComponent(); entity.AddComponent(membership); }
            membership.Assign(world.Strategic.PlayerFactionId, FactionRoleKind.Member);
            CharacterFactionLoyaltyService.SetLoyalty(world, target, CharacterFactionLoyaltyService.DefaultLoyalty);
            if (entity.TryGet<LifecycleComponent>(out var life) && life.State == LifecycleState.Captured)
                life.State = life.IsIncapacitated ? LifecycleState.Incapacitated : LifecycleState.Alive;
            state.Profession = MortalProfession.Unassigned;
            state.Disposition = CivilianDisposition.Normal; state.CarrierId = EntityId.None;
            state.DetainedResidenceWorkAreaId = string.Empty; state.PendingCaptureCarrierId = EntityId.None;
            ClearSleep(state); ClearCivilianMovement(entity);
            PrisonConfinementService.ReleasePlacementInPlace(world, target);
            return Result.Success();
        }

        public static Result Release(SimulationWorld world, EntityId target)
        {
            if (!TryInteractive(world, target, out var entity, out var state)) return Invalid("Target is not an interactive mortal civilian.");
            if (entity.TryGet<LifecycleComponent>(out var life) && life.State == LifecycleState.Captured)
                life.State = life.IsIncapacitated ? LifecycleState.Incapacitated : LifecycleState.Alive;
            state.DetainedResidenceWorkAreaId = string.Empty; state.CarrierId = EntityId.None;
            state.Disposition = CivilianDisposition.Fleeing;
            PrisonConfinementService.ReleasePlacementInPlace(world, target);
            var site = MortalCivilianQuery.ResolveCurrentSiteId(world, target);
            BeginFlee(world, entity, state, site);
            return Result.Success();
        }

        public static Result ValidateTransportPlacements(SimulationWorld world)
        {
            if (world == null) return Invalid("World is required.");
            ReconcileAutomaticRescues(world);
            var rescueTargets = new HashSet<EntityId>();
            var escortCarriers = new HashSet<EntityId>();
            foreach (var pair in world.Civilians.All)
            {
                var state = pair.Value;
                if (!state.RescueTargetId.IsNone)
                {
                    if (!rescueTargets.Add(state.RescueTargetId) ||
                        !world.Entities.TryGet(pair.Key, out _) ||
                        !world.Entities.TryGet(state.RescueTargetId, out var injured) ||
                        !injured.TryGet<LifecycleComponent>(out var life) || !life.IsIncapacitated ||
                        string.IsNullOrWhiteSpace(state.RescueDestinationId) ||
                        !world.OutdoorConstructedAssets.Assets.TryGetValue(state.RescueDestinationId, out var bed) ||
                        !RoomBedGeometry.IsBed(bed) || bed.ReservedCharacterId != state.RescueTargetId ||
                        !(RoomBedFacilityService.BedHasUsage(world, bed, RoomUsage.Medical) ||
                          RoomBedFacilityService.BedHasUsage(world, bed, RoomUsage.Residential)))
                        return Result.Failure(ErrorCode.SnapshotInvalid,
                            "Saved mortal rescue transport is inconsistent.", "Rescuer=" + pair.Key.Value);
                }
                if (state.Disposition == CivilianDisposition.CapturedEscorted)
                {
                    if (state.CarrierId.IsNone || !escortCarriers.Add(state.CarrierId) ||
                        !world.Entities.TryGet(state.CarrierId, out _) ||
                        !world.Entities.TryGet(pair.Key, out var captive) ||
                        !captive.TryGet<LifecycleComponent>(out var captiveLife) ||
                        captiveLife.State != LifecycleState.Captured)
                        return Result.Failure(ErrorCode.SnapshotInvalid,
                            "Saved escorted captive is inconsistent.", "Captive=" + pair.Key.Value);
                }
                if (state.Disposition != CivilianDisposition.Detained) continue;
                if (!world.OutdoorConstructedAssets.Assets.TryGetValue(
                        state.DetainedResidenceWorkAreaId, out var room) ||
                    !RoomBedGeometry.IsRoom(room) || room.RoomUsage != RoomUsage.Prison)
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Saved detainee does not reference a formal Prison room.", "Detainee=" + pair.Key.Value);
                var occupied = 0;
                foreach (var bed in RoomBedFacilityService.BedsInRoom(world, room.StableAssetId))
                    if (bed.OccupantCharacterId == pair.Key || bed.ReservedCharacterId == pair.Key)
                    {
                        occupied++;
                        if (!RoomBedFacilityService.BedHasUsage(world, bed, RoomUsage.Prison) ||
                            !(bed.OccupantCharacterId == pair.Key && RoomBedFacilityService.IsAtBed(world, bed, pair.Key) ||
                              bed.OccupantCharacterId.IsNone && bed.ReservedCharacterId == pair.Key &&
                              PrisonConfinementService.IsInside(world, room, pair.Key) &&
                              state.SleepPhase != CivilianSleepPhase.SleepingAtResidence))
                            return Result.Failure(ErrorCode.SnapshotInvalid,
                                "Saved detainee bed placement is invalid.", bed.StableAssetId);
                    }
                if (occupied != 1)
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Saved detainee must hold exactly one real prison bed.", "Detainee=" + pair.Key.Value);
            }
            // Host arrival callbacks are presentation-transient. An escorted captive with a
            // prison-bed reservation but no occupancy is therefore an interrupted command after
            // restore: keep the escort relationship and release only that stale prison hold.
            foreach (var asset in world.OutdoorConstructedAssets.Assets.Values)
                if (RoomBedGeometry.IsBed(asset) &&
                    RoomBedFacilityService.BedHasUsage(world, asset, RoomUsage.Prison) &&
                    !asset.ReservedCharacterId.IsNone && asset.OccupantCharacterId.IsNone &&
                    !(world.Civilians.TryGet(asset.ReservedCharacterId, out var detained) &&
                      detained.Disposition == CivilianDisposition.Detained && detained.DetainedResidenceWorkAreaId == asset.ParentRoomId))
                    asset.ReservedCharacterId = EntityId.None;
            return Result.Success();
        }

        public static Result ReleaseEscorted(SimulationWorld world, EntityId actor, EntityId target)
        {
            if (!TryInteractive(world, target, out var captiveEntity, out var state) ||
                state.Disposition != CivilianDisposition.CapturedEscorted || state.CarrierId != actor)
                return Invalid("只能由实际押送者主动释放所押送的囚犯。");
            if (world.Strategic.PlayerPartyContext?.IsMember(actor) != true)
                return Invalid("只有玩家队伍中的押送者可以释放囚犯。");
            var encounter = world.Strategic.CharacterEncounter;
            if (encounter != null && encounter.Find(actor.Value) != null &&
                (encounter.Phase == CharacterEncounterPhase.Active ||
                 encounter.Phase == CharacterEncounterPhase.ReadyToEnd))
            {
                if (world.Strategic.PlayerPartyContext.ActiveCharacterId != actor ||
                    !world.Entities.TryGet(actor, out var acting) || !CombatLifeStateService.CanFight(acting))
                    return Invalid("需由当前清醒的抱持者放下俘虏。");
                ReconcileEscortsForEncounter(world, encounter);
                if (state.CarrierId != actor) return Invalid("抱持关系已变化。");
                ReleaseEscortedAtCarrier(world, captiveEntity, state);
                CharacterEncounterService.RefreshCombatReadiness(world);
                return Result.Success();
            }
            if (!CharacterWorldPresenceQuery.TryResolve(world, actor, out var carrier) ||
                !carrier.HasWorldPosition ||
                !CharacterWorldPresenceQuery.TryResolve(world, target, out var captive) ||
                !captive.HasWorldPosition || carrier.SurfaceId != captive.SurfaceId)
                return Invalid("押送者与囚犯不在同一合法世界空间。");
            world.WorldPresence.SetAtWorldPosition(target, carrier.WorldPosition, carrier.SurfaceId);
            return Release(world, target);
        }

        public static Result CaptureDefeatedInBattle(SimulationWorld world, EntityId carrier, EntityId target)
        {
            var valid = DefeatedMercyService.Validate(world, carrier, target, DefeatedMercyService.Episode(world, target));
            if (valid.IsFailure) return valid;
            if (world.Civilians.TryGet(carrier, out var carried) &&
                (!carried.RescueTargetId.IsNone || carried.RescueCarrying))
                return Invalid("已在搬运另一人，不能再抱持俘虏。");
            foreach (var job in world.CivilianConstructionJobs.Jobs.Values)
                if (job.CarrierId == carrier && job.PayloadCount > 0)
                    return Invalid("已在搬运建材，不能同时抱持俘虏。");
            if (!world.Entities.TryGet(target, out var victim) ||
                !CombatLifeStateService.TryEnterCaptured(world, victim, world.Strategic.PlayerFactionId))
                return Invalid("目标已不能被俘虏。");
            var state = world.Civilians.GetOrCreate(target);
            state.CarrierId = carrier;
            state.Disposition = CivilianDisposition.CapturedEscorted;
            state.PendingCaptureCarrierId = EntityId.None;
            state.DetainedResidenceWorkAreaId = string.Empty;
            ClearSleep(state); ClearCivilianMovement(victim);
            ReconcileAutomaticRescues(world);
            CancelEscortIncompatibleActions(world, carrier);
            var encounter = world.Strategic.CharacterEncounter;
            encounter.Find(target.Value).TargetId = 0;
            encounter.Find(carrier.Value).TargetId = ulong.MaxValue;
            ReconcileEscortsForEncounter(world, encounter);
            return Result.Success();
        }

        public static Result RequestCapture(SimulationWorld world, EntityId carrier, EntityId target)
        {
            var burden = MortalTransportConstraintService.ValidateUnburdenedAction(world, carrier);
            if (burden.IsFailure) return burden;
            if (world == null || !world.Entities.TryGet(carrier, out var carrierEntity) ||
                !CombatLifeStateService.CanFight(carrierEntity))
                return Invalid("Capture requires a living carrier.");
            if (world.Civilians.TryGet(carrier, out var carrierState) &&
                (!carrierState.RescueTargetId.IsNone || carrierState.RescueCarrying))
                return Invalid("Carrier is already transporting another person.");
            foreach (var job in world.CivilianConstructionJobs.Jobs.Values)
                if (job.CarrierId == carrier && job.PayloadCount > 0)
                    return Invalid("Carrier is already transporting construction materials.");
            if (!TryInteractive(world, target, out _, out var state) || state.Disposition != CivilianDisposition.Fleeing)
                return Invalid("Only a fleeing mortal can be captured.");
            state.PendingCaptureCarrierId = carrier;
            return Result.Success();
        }

        public static void CancelCaptureRequest(SimulationWorld world, EntityId carrier, EntityId target)
        {
            if (world == null || target.IsNone || !world.Civilians.TryGet(target, out var state)) return;
            if (carrier.IsNone || state.PendingCaptureCarrierId == carrier)
                state.PendingCaptureCarrierId = EntityId.None;
        }

        public static void ResolvePendingCaptureAfterEncounter(SimulationWorld world, CharacterEncounterState encounter)
        {
            if (world == null || encounter == null) return;
            foreach (var pair in world.Civilians.All)
            {
                var state = pair.Value;
                if (state.PendingCaptureCarrierId.IsNone || encounter.Find(state.EntityId.Value) == null ||
                    encounter.Find(state.PendingCaptureCarrierId.Value) == null) continue;
                var requestedCarrier = state.PendingCaptureCarrierId;
                state.PendingCaptureCarrierId = EntityId.None;
                if (!encounter.PlayerWon) continue;
                if (!world.Entities.TryGet(state.EntityId, out var target) || !CombatLifeStateService.CanBeAttacked(target)) continue;
                if (!CombatLifeStateService.TryEnterCaptured(world, target, world.Strategic.PlayerFactionId)) continue;
                state.Disposition = CivilianDisposition.CapturedEscorted;
                state.CarrierId = ResolveCarrier(world, requestedCarrier);
                if (state.CarrierId.IsNone) state.CarrierId = FirstFriendly(encounter);
                CancelEscortIncompatibleActions(world, state.CarrierId);
            }
        }

        static void CancelEscortIncompatibleActions(SimulationWorld world, EntityId carrier)
        {
            if (world == null || carrier.IsNone) return;
            world.GetOrCreateOrderQueue(carrier).RemoveWhere(o =>
                o.Type == OrderType.Cultivate || o.Type == OrderType.Labor ||
                o.Type == OrderType.Work || o.Type == OrderType.Rest ||
                o.Type == OrderType.Recover || o.Type == OrderType.RestAtBed);
            world.WorkAreaOccupancy.Release(carrier);
            RoomBedFacilityService.Release(world, carrier);
            if (!world.Entities.TryGet(carrier, out var entity) ||
                !entity.TryGet<ActionStateComponent>(out var actionState) ||
                !actionState.HasActiveAction ||
                !world.ActiveActions.TryGetValue(actionState.ActiveActionId, out var action) ||
                action is MoveAction || action is WaitAction || action is ObserveAction)
                return;
            action.Cancel();
            world.ActiveActions.Remove(action.Id);
            actionState.ActiveActionId = ActionId.None;
            actionState.ActiveClock = null;
            actionState.ActiveOrderSource = OrderSource.Player;
        }

        static EntityId FirstFriendly(CharacterEncounterState encounter)
        {
            for (var i = 0; i < encounter.Participants.Count; i++)
                if (!encounter.Participants[i].Enemy) return new EntityId(encounter.Participants[i].CharacterId);
            return EntityId.None;
        }
        static EntityId FirstEnemy(CharacterEncounterState encounter)
        {
            for (var i = 0; i < encounter.Participants.Count; i++)
                if (encounter.Participants[i].Enemy) return new EntityId(encounter.Participants[i].CharacterId);
            return EntityId.None;
        }
        static EntityId ResolveCarrier(SimulationWorld world, EntityId preferred)
        {
            if (!preferred.IsNone && world.Entities.TryGet(preferred, out var e) && CombatLifeStateService.CanFight(e)) return preferred;
            var party = world.Strategic.PlayerPartyContext;
            if (party != null)
                for (var i = 0; i < party.Members.Count; i++)
                    if (world.Entities.TryGet(party.Members[i], out var member) && CombatLifeStateService.CanFight(member)) return member.Id;
            return EntityId.None;
        }

        static void SyncEscort(SimulationWorld world, Entity entity, MortalCivilianState state)
        {
            if (state.CarrierId.IsNone) return;
            var encounter = world.Strategic.CharacterEncounter;
            if (encounter != null && encounter.Find(state.CarrierId.Value) != null &&
                (encounter.Phase == CharacterEncounterPhase.Active ||
                 encounter.Phase == CharacterEncounterPhase.ReadyToEnd))
            {
                ReconcileEscortsForEncounter(world, encounter);
                return;
            }
            if (!world.Entities.TryGet(state.CarrierId, out var carrierEntity) ||
                !CombatLifeStateService.CanFight(carrierEntity))
            {
                ReleaseEscortedAtCarrier(world, entity, state);
                return;
            }
            if (CharacterWorldPresenceQuery.TryResolve(world, state.CarrierId, out var carrier) && carrier.HasWorldPosition)
                world.WorldPresence.SetAtWorldPosition(entity.Id, carrier.WorldPosition, carrier.SurfaceId);
            else DropEscort(world, entity, state);
        }

        static void DropEscort(SimulationWorld world, Entity entity, MortalCivilianState state)
        {
            var encounter = world.Strategic.CharacterEncounter;
            var participant = encounter?.Find(entity.Id.Value);
            var holder = encounter?.Find(state.CarrierId.Value);
            if (participant != null && holder != null &&
                CharacterEncounterService.OwnsParticipantSpatialState(world, entity.Id))
            {
                participant.TacticalX = holder.TacticalX; participant.TacticalY = holder.TacticalY;
                var presence = world.WorldPresence.GetOrCreate(entity.Id);
                presence.Mode = PartyWorldPresenceMode.InEncounter;
                presence.SiteId = string.Empty; presence.PersonalSurfaceId = encounter.SourceSurfaceId;
                presence.WorldPosX = holder.TacticalX; presence.WorldPosY = holder.TacticalY;
                presence.HasContinuousWorldPosition = true;
            }
            else if (!state.CarrierId.IsNone &&
                CharacterWorldPresenceQuery.TryResolve(world, state.CarrierId, out var carrier) &&
                carrier.HasWorldPosition)
                world.WorldPresence.SetAtWorldPosition(entity.Id, carrier.WorldPosition, carrier.SurfaceId);
            state.CarrierId = EntityId.None;
        }

        static void ReleaseEscortedAtCarrier(SimulationWorld world, Entity entity, MortalCivilianState state)
        {
            DropEscort(world, entity, state);
            if (entity.TryGet<LifecycleComponent>(out var life) && life.State == LifecycleState.Captured)
                life.State = life.IsIncapacitated ? LifecycleState.Incapacitated : LifecycleState.Alive;
            state.DetainedResidenceWorkAreaId = string.Empty;
            state.Disposition = CivilianDisposition.Fleeing;
            BeginFlee(world, entity, state, MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id));
        }

        public static Result SetResidenceUsage(SimulationWorld world, string areaId, ResidenceUsage usage)
        {
            if (world == null || !HousingAssignmentService.CanManageHousing(world) ||
                !world.TryGetWorkArea(areaId, out var area) || !HousingAssignmentService.IsHousingArea(area))
                return Invalid("Residence usage requires an authorized housing area.");
            if (HousingAssignmentService.IsDedicatedPrison(area) && usage != ResidenceUsage.PrisonerOnly)
                return Invalid("Dedicated prison usage is fixed to PrisonerOnly.");
            if (usage == ResidenceUsage.Normal)
                foreach (var pair in world.Civilians.All)
                    if (pair.Value.Disposition == CivilianDisposition.Detained &&
                        string.Equals(pair.Value.DetainedResidenceWorkAreaId, areaId, StringComparison.Ordinal))
                        return Invalid("Release or recruit detained occupants before restoring normal housing usage.");
            world.Civilians.SetResidenceUsage(areaId, usage); return Result.Success();
        }

        public static void SeedContentResidenceUsages(SimulationWorld world)
        {
            if (world == null) return;
            foreach (var pair in world.WorkAreas)
                if (HousingAssignmentService.IsDedicatedPrison(pair.Value))
                    world.Civilians.SetResidenceUsage(pair.Key, ResidenceUsage.PrisonerOnly);
        }

        public static Result ValidateContentResidenceUsages(SimulationWorld world)
        {
            if (world == null) return Invalid("World is required.");
            foreach (var pair in world.WorkAreas)
                if (HousingAssignmentService.IsDedicatedPrison(pair.Value) &&
                    world.Civilians.GetResidenceUsage(pair.Key) != ResidenceUsage.PrisonerOnly)
                    return Result.Failure(ErrorCode.SnapshotInvalid,
                        "Dedicated prison must restore as PrisonerOnly.", pair.Key);
            return Result.Success();
        }

        public static Result Detain(SimulationWorld world, EntityId target, string residenceId)
        {
            if (!TryInteractive(world, target, out _, out var state) || state.CarrierId.IsNone)
                return Invalid("Only an escorted captive can be detained.");
            if (!TryFindPrisonBed(world, residenceId, target, out var prisonBed))
                return Invalid("Prison room has no free real bed.");
            var reserved = ReservePrisonBedForDetain(
                world, state.CarrierId, target, residenceId, prisonBed.StableAssetId, out _);
            if (reserved.IsFailure) return reserved;
            var result = DetainAtBed(
                world, state.CarrierId, target, residenceId, prisonBed.StableAssetId);
            if (result.IsFailure)
                CancelPrisonBedReservation(world, prisonBed.StableAssetId, target);
            return result;
        }

        public static Result CanDetain(SimulationWorld world, EntityId target, string residenceId)
        {
            if (!TryInteractive(world, target, out _, out var state) || state.CarrierId.IsNone)
                return Invalid("Only an escorted captive can be detained.");
            if (!TryFindPrisonBed(world, residenceId, target, out var prisonBed))
                return Invalid("Prison room has no free real bed.");
            return ValidateDetainAtBed(world, state.CarrierId, target, residenceId,
                prisonBed.StableAssetId, requireReservation: false, requireArrival: true,
                out _, out _, out _);
        }

        public static Result CanBeginDetainAtBed(
            SimulationWorld world, EntityId carrierId, EntityId target, string roomId, string bedId) =>
            ValidateDetainAtBed(world, carrierId, target, roomId, bedId,
                requireReservation: false, requireArrival: false, out _, out _, out _);

        public static Result ReservePrisonBedForDetain(
            SimulationWorld world, EntityId carrierId, EntityId target,
            string roomId, string bedId, out WorldVec2 accessPoint)
        {
            var validation = ValidateDetainAtBed(world, carrierId, target, roomId, bedId,
                requireReservation: false, requireArrival: false,
                out _, out var bed, out accessPoint);
            if (validation.IsFailure) return validation;
            bed.ReservedCharacterId = target;
            return Result.Success();
        }

        public static Result CanCompleteDetainAtBed(
            SimulationWorld world, EntityId carrierId, EntityId target, string roomId, string bedId) =>
            ValidateDetainAtBed(world, carrierId, target, roomId, bedId,
                requireReservation: true, requireArrival: true, out _, out _, out _);

        public static Result DetainAtBed(
            SimulationWorld world, EntityId carrierId, EntityId target, string roomId, string bedId)
        {
            var validation = ValidateDetainAtBed(world, carrierId, target, roomId, bedId,
                requireReservation: true, requireArrival: true,
                out var entity, out var bed, out _);
            if (validation.IsFailure) return validation;
            var placed = RoomBedFacilityService.TryPlaceCarriedOccupant(
                world, bedId, carrierId, target, RoomUsage.Prison);
            if (placed.IsFailure) return placed;
            var state = world.Civilians.GetOrCreate(target);
            state.Disposition = CivilianDisposition.Detained;
            state.DetainedResidenceWorkAreaId = roomId;
            PrisonManagementService.BeginDetention(world, target);
            state.CarrierId = EntityId.None;
            state.CurrentSiteId = MortalCivilianQuery.ResolveCurrentSiteId(world, target);
            if (entity.TryGet<MovementIntentComponent>(out var intent)) intent.Clear();
            return Result.Success();
        }

        public static bool CancelPrisonBedReservation(
            SimulationWorld world, string bedId, EntityId captiveId)
        {
            if (world == null || captiveId.IsNone ||
                !world.OutdoorConstructedAssets.Assets.TryGetValue(bedId ?? string.Empty, out var bed) ||
                !RoomBedGeometry.IsBed(bed) || bed.ReservedCharacterId != captiveId ||
                bed.OccupantCharacterId == captiveId)
                return false;
            bed.ReservedCharacterId = EntityId.None;
            return true;
        }

        public static bool TrySelectPrisonBedForDetain(
            SimulationWorld world, EntityId carrierId, EntityId target, string roomId,
            out string bedId, out WorldVec2 accessPoint)
        {
            bedId = string.Empty; accessPoint = default;
            var keys = new List<string>();
            foreach (var candidate in RoomBedFacilityService.BedsInRoom(world, roomId))
                if (candidate != null) keys.Add(candidate.StableAssetId);
            keys.Sort(StringComparer.Ordinal);
            for (var i = 0; i < keys.Count; i++)
            {
                var validation = ValidateDetainAtBed(world, carrierId, target, roomId, keys[i],
                    requireReservation: false, requireArrival: false,
                    out _, out _, out var access);
                if (validation.IsFailure) continue;
                bedId = keys[i]; accessPoint = access; return true;
            }
            return false;
        }

        public static bool TryResolvePrisonDropOff(SimulationWorld world, string residenceId,
            out OutdoorConstructedAssetState dropOff, out WorldVec2 center, out float interactionRange)
        {
            dropOff = null; center = default; interactionRange = 0f;
            if (world == null || string.IsNullOrWhiteSpace(residenceId)) return false;
            if (world.OutdoorConstructedAssets.Assets.TryGetValue(residenceId, out var room) &&
                RoomBedGeometry.IsRoom(room) && room.RoomUsage == RoomUsage.Prison)
            {
                dropOff = room;
                OutdoorConstructedAssetState selected = null;
                foreach (var bed in RoomBedFacilityService.BedsInRoom(world, residenceId))
                    if (RoomBedFacilityService.BedHasUsage(world, bed, RoomUsage.Prison) &&
                        bed.OccupantCharacterId.IsNone && bed.ReservedCharacterId.IsNone &&
                        (selected == null || StringComparer.Ordinal.Compare(bed.StableAssetId, selected.StableAssetId) < 0))
                        selected = bed;
                if (selected == null || !RoomBedFacilityService.TryResolveAccessPoint(world, selected, out center))
                    return false;
                var cell = room.WorldWidth / room.CellsW;
                interactionRange = world.SurfaceSpatial.TryGet(room.SurfaceId, out var roomMetric) && roomMetric != null
                    ? roomMetric.CellSize * 1.5f : cell * 1.5f;
                return true;
            }
            return false;
        }

        static Result ValidateDetainAtBed(
            SimulationWorld world, EntityId carrierId, EntityId target,
            string roomId, string bedId, bool requireReservation, bool requireArrival,
            out Entity entity, out OutdoorConstructedAssetState prisonBed, out WorldVec2 accessPoint)
        {
            entity = null; prisonBed = null; accessPoint = default;
            if (!TryInteractive(world, target, out entity, out var state) ||
                state.Disposition != CivilianDisposition.CapturedEscorted ||
                state.CarrierId != carrierId)
                return Invalid("Only an escorted captive can be detained.");
            var party = world.Strategic.PlayerPartyContext;
            if (carrierId.IsNone || party?.IsMember(carrierId) != true ||
                !world.Entities.TryGet(carrierId, out var carrier) || !CombatLifeStateService.CanFight(carrier))
                return Invalid("Detention requires a living player-party carrier.");
            var encounter = world.Strategic.CharacterEncounter;
            if (SeparateSpaceTransitionService.IsOwnedByActiveSeparateSpace(world, carrierId) ||
                encounter != null && encounter.Find(carrierId.Value) != null &&
                (encounter.Phase == CharacterEncounterPhase.Active ||
                 encounter.Phase == CharacterEncounterPhase.ReadyToEnd))
                return Invalid("独立战斗空间中不能完成囚犯交接。");
            if (!world.OutdoorConstructedAssets.Assets.TryGetValue(roomId ?? string.Empty, out var prisonRoom) ||
                !RoomBedGeometry.IsRoom(prisonRoom) || prisonRoom.RoomUsage != RoomUsage.Prison)
                return Invalid("A Prison room is required.");
            if (!world.OutdoorConstructedAssets.Assets.TryGetValue(bedId ?? string.Empty, out prisonBed) ||
                !RoomBedGeometry.IsBed(prisonBed) ||
                !string.Equals(prisonBed.ParentRoomId, roomId, StringComparison.Ordinal) ||
                !RoomBedFacilityService.BedHasUsage(world, prisonBed, RoomUsage.Prison))
                return Invalid("指定床位不是该囚室中的牢床。");
            if (!prisonBed.AssignedCharacterId.IsNone && prisonBed.AssignedCharacterId != target)
                return Invalid("指定牢床已有长期分配，不能用于关押。");
            if (!prisonBed.ReservedCharacterId.IsNone && prisonBed.ReservedCharacterId != target)
                return Invalid("指定牢床已被其他人物预约。");
            if (!prisonBed.OccupantCharacterId.IsNone && prisonBed.OccupantCharacterId != target)
                return Invalid("指定牢床正在关押其他人物。");
            if (requireReservation && prisonBed.ReservedCharacterId != target)
                return Invalid("指定牢床的交接预约已失效。");
            var bedSiteId = RoomBedFacilityService.ResolveBedSiteId(world, prisonBed);
            if (string.IsNullOrEmpty(bedSiteId) ||
                !world.Strategic.Sites.TryGet(bedSiteId, out var bedSite) || bedSite == null || !bedSite.IsCoreActive ||
                !string.Equals(bedSite.OwnerFactionId, world.Strategic.PlayerFactionId, StringComparison.Ordinal))
                return Invalid("指定牢床必须属于玩家可合法使用的地点。");
            if (string.IsNullOrWhiteSpace(prisonBed.SurfaceId) ||
                !RoomBedFacilityService.TryResolveAccessPoint(world, prisonBed, out accessPoint) ||
                !CharacterWorldPresenceQuery.TryResolve(world, carrierId, out var carrierPresence) ||
                !carrierPresence.HasWorldPosition ||
                !CharacterWorldPresenceQuery.TryResolve(world, target, out var captivePresence) ||
                !captivePresence.HasWorldPosition ||
                !string.Equals(carrierPresence.SurfaceId, prisonBed.SurfaceId, StringComparison.Ordinal) ||
                !string.Equals(captivePresence.SurfaceId, prisonBed.SurfaceId, StringComparison.Ordinal))
                return Invalid("押送者、囚犯与牢床不在同一合法世界空间。");
            if (!requireArrival) return Result.Success();
            var interactionRange = world.SurfaceSpatial.TryGet(prisonBed.SurfaceId, out var metric) && metric != null
                ? metric.CellSize * 1.5f : Math.Max(prisonBed.WorldWidth, prisonBed.WorldHeight);
            if (!RoomBedFacilityService.IsAtAccessPoint(world, prisonBed, carrierId) ||
                DistanceSquared(captivePresence.WorldPosition, carrierPresence.WorldPosition) >
                interactionRange * interactionRange)
                return Invalid("Carrier and captive must physically reach the selected prison bed.");
            return Result.Success();
        }

        static bool TryFindPrisonBed(SimulationWorld world, string roomId, EntityId target,
            out OutdoorConstructedAssetState bed)
        {
            bed = null;
            foreach (var candidate in RoomBedFacilityService.BedsInRoom(world, roomId))
            {
                if (!RoomBedFacilityService.BedHasUsage(world, candidate, RoomUsage.Prison) ||
                    (!candidate.AssignedCharacterId.IsNone && candidate.AssignedCharacterId != target) ||
                    !RoomBedFacilityService.CanUseBed(world, candidate, target, out _)) continue;
                if (bed == null || StringComparer.Ordinal.Compare(candidate.StableAssetId, bed.StableAssetId) < 0)
                    bed = candidate;
            }
            return bed != null;
        }

        public static Result Execute(SimulationWorld world, EntityId actor, EntityId target)
        {
            if (!TryInteractive(world, target, out var entity, out var state) || state.Disposition != CivilianDisposition.Detained)
                return Invalid("Only a detained captive can be executed.");
            if (!entity.TryGet<LifecycleComponent>(out var life)) return Invalid("Target lifecycle missing.");
            life.State = LifecycleState.Incapacitated;
            CombatDamageRules.EnsureVitals(entity);
            if (entity.TryGet<CombatVitalsComponent>(out var vitals)) vitals.CurrentHp = 0;
            if (!CombatLifeStateService.TryConfirmDeath(
                    world, actor, entity, DeathConfirmationReason.PrisonerExecution, out var confirmed) || !confirmed)
                return Invalid("Formal death authority rejected execution.");
            state.DetainedResidenceWorkAreaId = string.Empty; state.CarrierId = EntityId.None;
            RoomBedFacilityService.Release(world, target);
            return Result.Success();
        }

        static bool TryInteractive(SimulationWorld world, EntityId target, out Entity entity, out MortalCivilianState state)
        {
            entity = null; state = null;
            if (world != null && world.Entities.TryGet(target, out entity) &&
                world.Civilians.TryGet(target, out state) &&
                (state.Disposition == CivilianDisposition.CapturedEscorted || state.Disposition == CivilianDisposition.Detained))
                return true;
            return world != null && world.Entities.TryGet(target, out entity) &&
                   MortalCivilianQuery.IsManagedCivilian(world, entity) && (state = Ensure(world, entity)) != null;
        }
        static Result Invalid(string message) => Result.Failure(ErrorCode.InvalidOperation, message);
    }
}
