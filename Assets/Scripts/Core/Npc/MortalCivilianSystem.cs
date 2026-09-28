using System;
using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Cultivation;
using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    public enum MortalProfession { Unassigned = 0, Farmer = 1, HerbFarmer = 2, Logger = 3, Medic = 4 }
    public enum MortalActivity
    {
        Idle = 0, Eat = 1, SleepAtResidence = 2, SleepOnGround = 3,
        FarmerWork = 4, HerbFarmerWork = 5, Logging = 6, MedicCare = 7,
        Rescue = 8, Haul = 9, Construction = 10, Flee = 11,
        Escorted = 12, Detained = 13, FetchFood = 14
    }
    public enum CivilianDisposition
    {
        Normal = 0, SurrenderWaiting = 1, Fleeing = 2, Displaced = 3,
        CapturedEscorted = 4, Detained = 5
    }
    public enum ResidenceUsage { Normal = 0, PrisonerOnly = 1 }
    public enum FactionMortalSchedulePhase { Work = 0, OffDuty = 1 }
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
        public const int BedSleepRestoreEveryTicks = 2;
        public const int GroundSleepRestoreEveryTicks = 4;
        public const int SatietyDecayEveryTicks = 4;
        public const int EnergyDecayEveryTicks = 5;
        public const int DetainedCareLoyaltyLossPerDay = 5;
        public const int SurrenderLoyaltyThreshold = 15;
        public const float FleeMoveSpeedMultiplier = 0.2f;
        public const float FleeBoundaryBufferCells = 3f;
        public const int FleeCandidateCount = 8;
        public const ulong FleeRetryDelayTicks = 8UL;
        // One V1 faction profile; hours are centralized tunables, not a permanent design rule.
        public const int WorkStartHour = 8;
        public const int WorkEndHour = 18;
    }

    public static class FactionMortalSchedule
    {
        public static FactionMortalSchedulePhase Current(WorldTick tick)
        {
            var hour = DayClock.FromWorldTick(tick).HourOfDay;
            return hour >= MortalCivilianTuning.WorkStartHour && hour < MortalCivilianTuning.WorkEndHour
                ? FactionMortalSchedulePhase.Work : FactionMortalSchedulePhase.OffDuty;
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
        // Short-lived transport reservation. Snapshot stores both real character positions; jobs re-evaluate on load.
        public EntityId RescueTargetId { get; set; }
        public bool RescueCarrying { get; set; }
        public string RescueDestinationId { get; set; } = string.Empty;
        // Session-only food trip. Stock remains SitePublicStock authority and is removed only on arrival.
        public string FoodFetchStorageRoomId { get; set; } = string.Empty;
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
            var siteId = ResolveCurrentSiteId(world, entity.Id);
            if (string.IsNullOrEmpty(siteId) || !world.Strategic.Sites.TryGet(siteId, out var site) ||
                site == null || !site.IsCoreActive ||
                !string.Equals(site.OwnerFactionId, membership.FactionId, StringComparison.Ordinal)) return false;

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
                    !world.ContinuousOutdoorMaterialization.TryGetPlace(siteId, candidate.LocationId, out _)) continue;
                area = candidate;
                return true;
            }
            return false;
        }
    }

    public enum CivilianMovementOwner
    {
        None = 0,
        Battle = 1,
        SeparateSpace = 2,
        PlayerParty = 3,
        ActiveSquadTravel = 4,
        CivilianLocal = 5,
        StaticSquad = 6
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

    /// <summary>Priority evaluator: forced states, hunger, sleep, rescue, profession, common work, idle.</summary>
    public static class MortalActivityEvaluator
    {
        public static MortalActivity Evaluate(SimulationWorld world, Entity entity, MortalCivilianState state)
        {
            if (state.Disposition == CivilianDisposition.CapturedEscorted) return MortalActivity.Escorted;
            if (state.Disposition == CivilianDisposition.Detained) return MortalActivity.Detained;
            if (state.Disposition == CivilianDisposition.Fleeing) return MortalActivity.Flee;
            if (state.Activity == MortalActivity.Eat && state.MealStartedTick > 0 &&
                world.Tick.Value < state.MealStartedTick + 2) return MortalActivity.Eat;
            // Once real sleep has started it continues to WakeThreshold. Traveling home is not
            // sleep, so Energy dropping below the emergency threshold still switches to ground rest.
            if (state.SleepPhase == CivilianSleepPhase.SleepingOnGround &&
                state.Energy < MortalCivilianTuning.WakeThreshold)
                return MortalActivity.SleepOnGround;
            if (state.SleepPhase == CivilianSleepPhase.SleepingAtResidence &&
                state.Energy < MortalCivilianTuning.WakeThreshold &&
                !string.IsNullOrEmpty(state.ResidenceWorkAreaId) &&
                world.Civilians.GetResidenceUsage(state.ResidenceWorkAreaId) == ResidenceUsage.Normal)
                return MortalActivity.SleepAtResidence;
            if (state.Satiety <= MortalCivilianTuning.HungerThreshold &&
                MortalCivilianService.IsFoodPlanInProgress(state))
                return MortalActivity.FetchFood;
            if (state.Energy < MortalCivilianTuning.GroundSleepThreshold) return MortalActivity.SleepOnGround;
            if (state.Satiety <= MortalCivilianTuning.HungerThreshold &&
                MortalCivilianService.ShouldRetryFood(world, entity, state))
                return MortalActivity.FetchFood;
            if (state.Energy <= MortalCivilianTuning.SleepThreshold)
                return !string.IsNullOrEmpty(state.ResidenceWorkAreaId) &&
                       world.Civilians.GetResidenceUsage(state.ResidenceWorkAreaId) == ResidenceUsage.Normal
                    ? MortalActivity.SleepAtResidence : MortalActivity.SleepOnGround;
            // A failed food plan remains a need-level wait and does not fall through into work.
            if (state.Satiety <= MortalCivilianTuning.HungerThreshold) return MortalActivity.Idle;
            if (state.Disposition == CivilianDisposition.SurrenderWaiting || state.Disposition == CivilianDisposition.Displaced)
                return MortalActivity.Idle;
            if (!state.RescueTargetId.IsNone || MortalCivilianService.HasRescueTarget(world, entity))
                return MortalActivity.Rescue;
            if (!entity.TryGet<FactionMembershipComponent>(out var membership) || !membership.IsAffiliated)
                return MortalActivity.Idle;
            if (FactionMortalSchedule.Current(world.Tick) == FactionMortalSchedulePhase.OffDuty)
                return MortalActivity.Idle;
            switch (state.Profession)
            {
                case MortalProfession.Farmer:
                    if (MortalCivilianQuery.TryFindProfessionArea(world, entity, "grain", out _)) return MortalActivity.FarmerWork;
                    break;
                case MortalProfession.HerbFarmer:
                    if (MortalCivilianQuery.TryFindProfessionArea(world, entity, "herb", out _)) return MortalActivity.HerbFarmerWork;
                    break;
                case MortalProfession.Logger:
                    if (MortalCivilianQuery.TryFindProfessionArea(world, entity, "wood", out _)) return MortalActivity.Logging;
                    break;
                case MortalProfession.Medic:
                    if (HasInjuredAlly(world, entity)) return MortalActivity.MedicCare;
                    break;
            }
            return CivilianConstructionJobService.HasJob(world, entity, true) ? MortalActivity.Haul :
                CivilianConstructionJobService.HasJob(world, entity, false) ? MortalActivity.Construction :
                MortalActivity.Idle;
        }

        static bool HasInjuredAlly(SimulationWorld world, Entity entity)
        {
            if (!entity.TryGet<FactionMembershipComponent>(out var own) || !own.IsAffiliated) return false;
            var siteId = MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id);
            if (string.IsNullOrEmpty(siteId)) return false;
            foreach (var other in world.Entities.All)
            {
                if (other.Id == entity.Id ||
                    !other.TryGet<FactionMembershipComponent>(out var faction) || !faction.IsAffiliated ||
                    !string.Equals(faction.FactionId, own.FactionId, StringComparison.Ordinal) ||
                    !string.Equals(MortalCivilianQuery.ResolveCurrentSiteId(world, other.Id), siteId, StringComparison.Ordinal)) continue;
                if (other.TryGet<LifecycleComponent>(out var life) && life.IsIncapacitated) return true;
                if (other.TryGet<CombatVitalsComponent>(out var vitals) &&
                    other.TryGet<AttributesComponent>(out var attrs) &&
                    vitals.CurrentHp < Math.Max(1, attrs.GetFinal(AttributeId.MaxHp))) return true;
            }
            return false;
        }

        public static bool HasTag(WorkAreaDefinition area, string tag)
        {
            if (area?.Tags == null) return false;
            for (var i = 0; i < area.Tags.Count; i++)
                if (string.Equals(area.Tags[i], tag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    public static class MortalCivilianService
    {
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
                if (entity.TryGet<ActivityTendencyComponent>(out var tendency))
                    state.ResidenceWorkAreaId = tendency.HomeWorkAreaId ?? string.Empty;
                state.CurrentSiteId = MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id);
            }
            return state;
        }

        public static Result SetProfession(SimulationWorld world, EntityId id, MortalProfession profession)
        {
            if (!CanSetProfession(world, id, out var reason))
                return Result.Failure(ErrorCode.InvalidOperation, reason);
            world.Entities.TryGet(id, out var entity);
            var state = Ensure(world, entity);
            state.Profession = profession;
            return Result.Success();
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

        public static void Tick(SimulationWorld world)
        {
            if (world == null) return;
            CivilianConstructionJobService.CancelInvalidJobs(world);
            foreach (var entity in world.Entities.All)
            {
                var state = Ensure(world, entity);
                if (state == null) continue;
                if (!entity.TryGet<LifecycleComponent>(out var life) || life.State == LifecycleState.Dead || life.State == LifecycleState.Removed)
                { DropRescue(world, entity, state); CivilianConstructionJobService.ReleaseCarrier(world, entity.Id); state.Activity = MortalActivity.Idle; continue; }
                AdvanceDayCare(world, entity.Id, state, world.Tick.Value / WorldTick.TicksPerDay);
                state.CurrentSiteId = MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id);
                AdvanceNeeds(world, entity, state);
                if (life.State != LifecycleState.Alive)
                {
                    DropRescue(world, entity, state);
                    CivilianConstructionJobService.ReleaseCarrier(world, entity.Id);
                    state.Activity = MortalActivity.Idle;
                    state.LastUpdateTick = world.Tick.Value;
                    continue;
                }
                var movementOwner = MortalCivilianMovementAuthority.Resolve(world, entity.Id, state);
                // Party control owns movement and work. Civilian needs remain persistent while enlisted.
                if (movementOwner == CivilianMovementOwner.PlayerParty)
                {
                    ClearFoodTrip(entity, state);
                    ClearCivilianMovement(entity);
                    DropRescue(world, entity, state);
                    CivilianConstructionJobService.ReleaseCarrier(world, entity.Id);
                    if (state.Satiety <= MortalCivilianTuning.HungerThreshold &&
                        TryConsumeFoodAbstract(world, entity.Id, state.CurrentSiteId))
                    { state.Satiety += MortalCivilianTuning.MealRestore; state.AteToday = true; state.Clamp(); }
                    state.Activity = MortalActivity.Idle;
                    state.LastUpdateTick = world.Tick.Value;
                    continue;
                }
                if (movementOwner == CivilianMovementOwner.Battle)
                { ClearCivilianMovement(entity); DropRescue(world, entity, state); CivilianConstructionJobService.ReleaseCarrier(world, entity.Id); state.Activity = MortalActivity.Idle; continue; }
                if (movementOwner == CivilianMovementOwner.SeparateSpace ||
                    movementOwner == CivilianMovementOwner.ActiveSquadTravel)
                { ClearCivilianMovement(entity); DropRescue(world, entity, state); CivilianConstructionJobService.ReleaseCarrier(world, entity.Id); state.Activity = MortalActivity.Idle; continue; }
                if (state.Disposition == CivilianDisposition.Fleeing) AdvanceFlee(world, entity, state);
                if (state.Disposition == CivilianDisposition.CapturedEscorted) SyncEscort(world, entity, state);
                state.Activity = MortalActivityEvaluator.Evaluate(world, entity, state);
                if (state.Satiety > MortalCivilianTuning.HungerThreshold &&
                    state.Activity != MortalActivity.Eat)
                    ClearFoodTrip(entity, state);
                if (state.Activity != MortalActivity.Rescue)
                    DropRescue(world, entity, state);
                if (state.Activity != MortalActivity.Haul)
                    CivilianConstructionJobService.ReleaseCarrier(world, entity.Id);
                ApplyActivity(world, entity, state);
                state.LastUpdateTick = world.Tick.Value;
            }
        }

        static void AdvanceDayCare(SimulationWorld world, EntityId characterId, MortalCivilianState state, ulong day)
        {
            if (state.CareDayIndex == day) return;
            if (state.Disposition == CivilianDisposition.Detained && state.AteToday && state.SleptToday)
                CharacterFactionLoyaltyService.AdjustLoyalty(world, characterId, -MortalCivilianTuning.DetainedCareLoyaltyLossPerDay);
            state.CareDayIndex = day; state.AteToday = false; state.SleptToday = false;
        }

        static void AdvanceNeeds(SimulationWorld world, Entity entity, MortalCivilianState state)
        {
            var now = world.Tick.Value;
            if (now % (ulong)MortalCivilianTuning.SatietyDecayEveryTicks == 0) state.Satiety--;
            var sleeping = state.SleepPhase == CivilianSleepPhase.SleepingAtResidence ||
                           state.SleepPhase == CivilianSleepPhase.SleepingOnGround;
            if (sleeping)
            {
                var interval = state.SleepPhase == CivilianSleepPhase.SleepingOnGround
                    ? MortalCivilianTuning.GroundSleepRestoreEveryTicks
                    : MortalCivilianTuning.BedSleepRestoreEveryTicks;
                if (now % (ulong)interval == 0) { state.Energy++; state.SleptToday = true; }
            }
            else if (now % (ulong)MortalCivilianTuning.EnergyDecayEveryTicks == 0) state.Energy--;
            state.Clamp();
        }

        static void ApplyActivity(SimulationWorld world, Entity entity, MortalCivilianState state)
        {
            if (state.Activity != MortalActivity.SleepAtResidence && state.Activity != MortalActivity.SleepOnGround &&
                state.Activity != MortalActivity.Detained)
                ClearSleep(state);
            switch (state.Activity)
            {
                case MortalActivity.Idle:
                    if (FactionMortalSchedule.Current(world.Tick) == FactionMortalSchedulePhase.OffDuty &&
                        entity.TryGet<FactionMembershipComponent>(out var idleFaction) && idleFaction.IsAffiliated &&
                        !string.IsNullOrEmpty(state.ResidenceWorkAreaId) &&
                        world.Civilians.GetResidenceUsage(state.ResidenceWorkAreaId) == ResidenceUsage.Normal)
                        MoveToArea(world, entity, state.ResidenceWorkAreaId);
                    else if (entity.TryGet<MovementIntentComponent>(out var idleIntent)) idleIntent.Clear();
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
                    ApplyResidenceSleep(world, entity, state, state.ResidenceWorkAreaId, ResidenceUsage.Normal);
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
                    TreatOneAlly(world, entity); break;
                case MortalActivity.Rescue:
                    RescueOneAlly(world, entity); break;
                case MortalActivity.Haul:
                    CivilianConstructionJobService.Work(world, entity, true); break;
                case MortalActivity.Construction:
                    CivilianConstructionJobService.Work(world, entity, false); break;
                case MortalActivity.Escorted:
                case MortalActivity.Detained:
                    if (state.Activity == MortalActivity.Escorted)
                    {
                        if (entity.TryGet<MovementIntentComponent>(out var forcedIntent)) forcedIntent.Clear();
                        ClearSleep(state);
                        break;
                    }
                    if (state.Satiety <= MortalCivilianTuning.HungerThreshold && TryConsumeFoodAbstract(world, entity.Id, state.CurrentSiteId))
                    { state.Satiety += MortalCivilianTuning.MealRestore; state.AteToday = true; state.Clamp(); }
                    if (state.Energy <= MortalCivilianTuning.SleepThreshold ||
                        state.SleepPhase == CivilianSleepPhase.SleepingAtResidence &&
                        state.Energy < MortalCivilianTuning.WakeThreshold)
                        ApplyResidenceSleep(world, entity, state,
                            state.DetainedResidenceWorkAreaId, ResidenceUsage.PrisonerOnly);
                    else ClearSleep(state);
                    break;
            }
        }

        static void ApplyResidenceSleep(
            SimulationWorld world, Entity entity, MortalCivilianState state,
            string residenceAreaId, ResidenceUsage requiredUsage)
        {
            if (string.IsNullOrEmpty(residenceAreaId) ||
                !world.TryGetWorkArea(residenceAreaId, out var residence) ||
                !MortalActivityEvaluator.HasTag(residence, "home") ||
                world.Civilians.GetResidenceUsage(residenceAreaId) != requiredUsage)
            {
                StartGroundSleep(world, entity, state);
                return;
            }

            if (state.SleepPhase == CivilianSleepPhase.SleepingAtResidence)
            {
                if (entity.TryGet<MovementIntentComponent>(out var sleepingIntent) && sleepingIntent.Active)
                    sleepingIntent.Clear();
                return;
            }

            MoveToArea(world, entity, residenceAreaId);
            state.SleepPhase = CivilianSleepPhase.TravelingToResidence;
            state.SleepStartedTick = 0;
            if (!entity.TryGet<MovementIntentComponent>(out var intent))
            {
                StartGroundSleep(world, entity, state);
                return;
            }
            if (intent.HostPathState == MovementHostPathState.RetryablePathUnavailable ||
                intent.HostPathState == MovementHostPathState.PermanentFailure)
            {
                StartGroundSleep(world, entity, state);
                return;
            }
            if (!intent.HostArrived || !string.Equals(intent.TargetWorkAreaId, residenceAreaId, StringComparison.Ordinal))
                return;
            intent.Clear();
            state.SleepPhase = CivilianSleepPhase.SleepingAtResidence;
            state.SleepStartedTick = world.Tick.Value;
        }

        static void StartGroundSleep(SimulationWorld world, Entity entity, MortalCivilianState state)
        {
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

        static bool TryConsumeFoodAbstract(SimulationWorld world, EntityId eater, string siteId)
        {
            if (!world.Entities.TryGet(eater, out var entity) || !world.Civilians.TryGet(eater, out var civilian)) return false;
            var inParty = world.Strategic.PlayerPartyContext?.IsMember(eater) == true &&
                          PlayerPartyRuntime.CanPlayerControlCharacter(world, eater);
            if (!string.IsNullOrEmpty(siteId) && world.Strategic.SitePublicStocks.TryGet(siteId, out var stock) &&
                world.Strategic.Sites.TryGet(siteId, out var site))
            {
                var allowed = false;
                if (civilian.Disposition == CivilianDisposition.Detained)
                    allowed = !string.IsNullOrEmpty(civilian.DetainedResidenceWorkAreaId) &&
                              string.Equals(site.OwnerFactionId, world.Strategic.PlayerFactionId, StringComparison.Ordinal);
                else if (entity.TryGet<FactionMembershipComponent>(out var membership) && membership.IsAffiliated)
                    allowed = string.Equals(membership.FactionId, site.OwnerFactionId, StringComparison.Ordinal);
                else
                    allowed = !string.IsNullOrEmpty(civilian.ResidenceWorkAreaId) &&
                              world.TryGetWorkArea(civilian.ResidenceWorkAreaId, out var residence) &&
                              MortalActivityEvaluator.HasTag(residence, "home") &&
                              world.Civilians.GetResidenceUsage(residence.Id) == ResidenceUsage.Normal &&
                              string.Equals(civilian.CurrentSiteId, siteId, StringComparison.Ordinal);
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
            var siteId = MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id);
            state.CurrentSiteId = siteId;
            if (string.IsNullOrEmpty(siteId) || !world.Strategic.Sites.TryGet(siteId, out var site) ||
                site == null || !site.IsCoreActive)
            { FailFoodPlan(world, entity, state, FoodFetchPhase.NoFoodSource, "饥饿·没有合法食物来源"); return; }
            if (!CanUseSiteFood(world, entity, state, site))
            { FailFoodPlan(world, entity, state, FoodFetchPhase.AccessDenied, "饥饿·无权使用公粮"); return; }
            if (!world.SiteStorageRooms.HasActiveStorageForSite(world, siteId) ||
                !world.SiteStorageRooms.TryGetBySite(siteId, out var room))
            { FailFoodPlan(world, entity, state, FoodFetchPhase.NoStorage, "饥饿·无可用储藏室"); return; }
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
            if (state.Disposition == CivilianDisposition.SurrenderWaiting ||
                state.Disposition == CivilianDisposition.Displaced) return true;
            if (entity.TryGet<FactionMembershipComponent>(out var membership) && membership.IsAffiliated)
                return string.Equals(membership.FactionId, site.OwnerFactionId, StringComparison.Ordinal);
            return !string.IsNullOrEmpty(state.ResidenceWorkAreaId) &&
                   world.TryGetWorkArea(state.ResidenceWorkAreaId, out var residence) &&
                   MortalActivityEvaluator.HasTag(residence, "home") &&
                   world.Civilians.GetResidenceUsage(residence.Id) == ResidenceUsage.Normal;
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
                string.IsNullOrEmpty(state.PendingFoodResourceId) && !state.HasFoodFetchAccessPoint &&
                state.MealStartedTick == 0 && state.FoodFetchPhase == FoodFetchPhase.None &&
                state.FoodFetchRetryAfterTick == 0 && string.IsNullOrEmpty(state.FoodFetchFailureSignature)) return;
            StopFoodMovement(entity);
            state.FoodFetchStorageRoomId = string.Empty;
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
            if (!intent.Active || !string.Equals(intent.TargetWorkAreaId, area.Id, StringComparison.Ordinal))
                intent.Begin(area.LocationId, area.Id, 0);
        }

        public static bool HasRescueTarget(SimulationWorld world, Entity rescuer) =>
            FindRescueTarget(world, rescuer) != null;

        static Entity FindRescueTarget(SimulationWorld world, Entity rescuer)
        {
            if (!rescuer.TryGet<FactionMembershipComponent>(out var own) || !own.IsAffiliated) return null;
            var siteId = MortalCivilianQuery.ResolveCurrentSiteId(world, rescuer.Id);
            if (string.IsNullOrEmpty(siteId)) return null;
            Entity chosen = null;
            foreach (var other in world.Entities.All)
            {
                if (other.Id == rescuer.Id ||
                    !other.TryGet<FactionMembershipComponent>(out var faction) || !faction.IsAffiliated ||
                    !string.Equals(faction.FactionId, own.FactionId, StringComparison.Ordinal) ||
                    !string.Equals(MortalCivilianQuery.ResolveCurrentSiteId(world, other.Id), siteId, StringComparison.Ordinal) ||
                    !other.TryGet<LifecycleComponent>(out var life) || !life.IsIncapacitated ||
                    !CharacterWorldPresenceQuery.TryResolve(world, other.Id, out var presence) || !presence.HasWorldPosition ||
                    !TryRescueDestination(world, rescuer, out _, out var destination, out var surfaceId) ||
                    !string.Equals(presence.SurfaceId, surfaceId, StringComparison.Ordinal) ||
                    DistanceSquared(presence.WorldPosition, destination) < 1f ||
                    IsRescueReserved(world, other.Id)) continue;
                if (chosen == null || other.Id.Value < chosen.Id.Value) chosen = other;
            }
            return chosen;
        }

        static bool IsRescueReserved(SimulationWorld world, EntityId target)
        {
            foreach (var pair in world.Civilians.All)
                if (pair.Value.RescueTargetId == target) return true;
            return false;
        }

        static float DistanceSquared(WorldVec2 a, WorldVec2 b)
        { var x = a.X - b.X; var y = a.Y - b.Y; return x * x + y * y; }

        static bool TryRescueDestination(SimulationWorld world, Entity rescuer,
            out string destinationId, out WorldVec2 destination, out string surfaceId)
        {
            destinationId = surfaceId = string.Empty; destination = default;
            var siteId = MortalCivilianQuery.ResolveCurrentSiteId(world, rescuer.Id);
            if (string.IsNullOrEmpty(siteId) || !world.Strategic.Sites.TryGet(siteId, out var site) ||
                !site.HasContinuousCore || !rescuer.TryGet<FactionMembershipComponent>(out var own) ||
                !string.Equals(site.OwnerFactionId, own.FactionId, StringComparison.Ordinal)) return false;
            surfaceId = site.CoreSurfaceId;
            destinationId = siteId;
            destination = new WorldVec2(site.CoreWorldX, site.CoreWorldY);
            var keys = new List<string>(world.OutdoorConstructedAssets.Assets.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var key in keys)
            {
                var asset = world.OutdoorConstructedAssets.Assets[key];
                if (asset.Kind != OutdoorConstructedAssetSemantics.RecoverySpotKind ||
                    asset.SurfaceId != surfaceId) continue;
                var center = new WorldVec2(asset.WorldX + asset.WorldWidth * .5f,
                    asset.WorldY + asset.WorldHeight * .5f);
                if (!WorldSitePhysicalRegionQuery.TryResolve(world, center, out var recoverySite) ||
                    recoverySite.SiteId != siteId) continue;
                destinationId = asset.StableAssetId; destination = center; break;
            }
            return true;
        }

        static void RescueOneAlly(SimulationWorld world, Entity rescuer)
        {
            var state = world.Civilians.GetOrCreate(rescuer.Id);
            var target = state.RescueTargetId.IsNone ? FindRescueTarget(world, rescuer) :
                world.Entities.TryGet(state.RescueTargetId, out var saved) ? saved : null;
            if (target == null || !target.TryGet<LifecycleComponent>(out var life) || !life.IsIncapacitated ||
                !TryRescueDestination(world, rescuer, out var destinationId, out var destination, out var surfaceId) ||
                !CharacterWorldPresenceQuery.TryResolve(world, rescuer.Id, out var rescuerPresence) ||
                !rescuerPresence.HasWorldPosition || rescuerPresence.SurfaceId != surfaceId ||
                !CharacterWorldPresenceQuery.TryResolve(world, target.Id, out var targetPresence) ||
                !targetPresence.HasWorldPosition || targetPresence.SurfaceId != surfaceId)
            { DropRescue(world, rescuer, state); return; }
            state.RescueTargetId = target.Id;
            state.RescueDestinationId = destinationId;
            if (!state.RescueCarrying)
            {
                MoveToWorldPoint(rescuer, targetPresence.SurfaceId, targetPresence.WorldPosition);
                if (rescuer.TryGet<MovementIntentComponent>(out var approach) && approach.HostArrived &&
                    DistanceSquared(rescuerPresence.WorldPosition, targetPresence.WorldPosition) <= 9f)
                { state.RescueCarrying = true; approach.Clear(); }
                return;
            }
            world.WorldPresence.SetAtWorldPosition(target.Id, rescuerPresence.WorldPosition, surfaceId);
            MoveToWorldPoint(rescuer, surfaceId, destination);
            if (rescuer.TryGet<MovementIntentComponent>(out var delivery) && delivery.HostArrived &&
                DistanceSquared(rescuerPresence.WorldPosition, destination) <= 9f)
            {
                world.WorldPresence.SetAtWorldPosition(target.Id, destination, surfaceId);
                delivery.Clear(); state.RescueTargetId = EntityId.None;
                state.RescueCarrying = false; state.RescueDestinationId = string.Empty;
            }
        }

        static void DropRescue(SimulationWorld world, Entity rescuer, MortalCivilianState state)
        {
            if (state.RescueCarrying && !state.RescueTargetId.IsNone &&
                CharacterWorldPresenceQuery.TryResolve(world, rescuer.Id, out var presence) && presence.HasWorldPosition)
                world.WorldPresence.SetAtWorldPosition(state.RescueTargetId, presence.WorldPosition, presence.SurfaceId);
            state.RescueTargetId = EntityId.None; state.RescueCarrying = false;
            state.RescueDestinationId = string.Empty;
        }

        public static void InterruptTransportsForEncounter(SimulationWorld world, CharacterEncounterState encounter)
        {
            if (world == null || encounter == null) return;
            foreach (var pair in world.Civilians.All)
            {
                if (encounter.Find(pair.Key.Value) == null || !world.Entities.TryGet(pair.Key, out var worker))
                    continue;
                DropRescue(world, worker, pair.Value);
                CivilianConstructionJobService.ReleaseCarrier(world, worker.Id);
            }
        }

        public static void CancelAllRescues(SimulationWorld world)
        {
            if (world == null) return;
            foreach (var pair in world.Civilians.All)
                if (world.Entities.TryGet(pair.Key, out var rescuer))
                    DropRescue(world, rescuer, pair.Value);
        }

        static void MoveToWorldPoint(Entity entity, string surfaceId, WorldVec2 point, float speedMultiplier = 1f)
        {
            if (!entity.TryGet<MovementIntentComponent>(out var intent))
            { intent = new MovementIntentComponent(); entity.AddComponent(intent); }
            if (!intent.Active || !intent.HasWorldTarget || intent.TargetSurfaceId != surfaceId ||
                Math.Abs(intent.TargetWorldX - point.X) > .2f || Math.Abs(intent.TargetWorldY - point.Y) > .2f ||
                Math.Abs(intent.SpeedMultiplier - speedMultiplier) > .001f)
                intent.BeginWorldPoint(surfaceId, point.X, point.Y, speedMultiplier);
        }

        static void TreatOneAlly(SimulationWorld world, Entity healer)
        {
            if (world.Tick.Value % 12UL != 0 || !healer.TryGet<FactionMembershipComponent>(out var own) || !own.IsAffiliated) return;
            var siteId = MortalCivilianQuery.ResolveCurrentSiteId(world, healer.Id);
            if (string.IsNullOrEmpty(siteId)) return;
            foreach (var other in world.Entities.All)
            {
                if (other.Id == healer.Id || !other.TryGet<FactionMembershipComponent>(out var faction) ||
                    !string.Equals(faction.FactionId, own.FactionId, StringComparison.Ordinal) ||
                    !string.Equals(MortalCivilianQuery.ResolveCurrentSiteId(world, other.Id), siteId, StringComparison.Ordinal)) continue;
                if (other.TryGet<LifecycleComponent>(out var life) && life.IsIncapacitated)
                {
                    if (!IsRescueReserved(world, other.Id) &&
                        TryRescueDestination(world, healer, out _, out var safePoint, out var safeSurface) &&
                        CharacterWorldPresenceQuery.TryResolve(world, other.Id, out var injuredPresence) &&
                        injuredPresence.HasWorldPosition && injuredPresence.SurfaceId == safeSurface &&
                        DistanceSquared(injuredPresence.WorldPosition, safePoint) <= 1f)
                        CombatLifeStateService.TryRecoverFromIncapacitated(world, healer.Id, other, 1);
                    return;
                }
                if (!other.TryGet<CombatVitalsComponent>(out var vitals) || !other.TryGet<AttributesComponent>(out var attrs)) continue;
                var max = Math.Max(1, attrs.GetFinal(AttributeId.MaxHp));
                if (vitals.CurrentHp < max) { vitals.CurrentHp = Math.Min(max, vitals.CurrentHp + 1); return; }
            }
        }

        public static void OnSiteTransferred(SimulationWorld world, string siteId, string oldFactionId, string newFactionId)
        {
            if (world == null || string.IsNullOrEmpty(siteId) || string.Equals(oldFactionId, newFactionId, StringComparison.Ordinal)) return;
            foreach (var entity in world.Entities.All)
            {
                var state = Ensure(world, entity);
                if (state == null || !entity.TryGet<FactionMembershipComponent>(out var member) || !member.IsAffiliated ||
                    !string.Equals(member.FactionId, oldFactionId, StringComparison.Ordinal) ||
                    !string.Equals(MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id), siteId, StringComparison.Ordinal)) continue;
                state.Disposition = CharacterFactionLoyaltyService.TryGetLoyalty(world, entity.Id, out var loyalty) &&
                    loyalty < MortalCivilianTuning.SurrenderLoyaltyThreshold
                    ? CivilianDisposition.SurrenderWaiting : CivilianDisposition.Fleeing;
                if (state.Disposition == CivilianDisposition.Fleeing) BeginFlee(world, entity, state, siteId);
            }
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
                if (entity.TryGet<MovementIntentComponent>(out var completedIntent)) completedIntent.Clear();
                if (entity.TryGet<ActivityTendencyComponent>(out var tendency)) tendency.HomeWorkAreaId = string.Empty;
                return;
            }
            if (!state.HasFleeTarget) SetFleeCandidate(world, sourceSite, state);
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
            if (!TryInteractive(world, target, out var entity, out var state)) return Invalid("Target is not an interactive mortal civilian.");
            if (state.Disposition != CivilianDisposition.SurrenderWaiting && state.Disposition != CivilianDisposition.Detained)
                return Invalid("Recruitment requires surrender or detention.");
            if (!CharacterFactionLoyaltyService.TryGetLoyalty(world, target, out var loyalty) ||
                loyalty >= MortalCivilianTuning.SurrenderLoyaltyThreshold)
                return Invalid("此人仍不愿归降。");
            if (string.IsNullOrEmpty(world.Strategic.PlayerFactionId)) return Invalid("Player faction is missing.");
            if (!entity.TryGet<FactionMembershipComponent>(out var membership)) { membership = new FactionMembershipComponent(); entity.AddComponent(membership); }
            membership.Assign(world.Strategic.PlayerFactionId, FactionRoleKind.Member);
            CharacterFactionLoyaltyService.SetLoyalty(world, target, CharacterFactionLoyaltyService.DefaultLoyalty);
            if (entity.TryGet<LifecycleComponent>(out var life) && life.State == LifecycleState.Captured) life.State = LifecycleState.Alive;
            state.Profession = MortalProfession.Unassigned;
            state.Disposition = CivilianDisposition.Normal; state.CarrierId = EntityId.None;
            state.DetainedResidenceWorkAreaId = string.Empty; state.PendingCaptureCarrierId = EntityId.None;
            return Result.Success();
        }

        public static Result Release(SimulationWorld world, EntityId target)
        {
            if (!TryInteractive(world, target, out var entity, out var state)) return Invalid("Target is not an interactive mortal civilian.");
            if (entity.TryGet<LifecycleComponent>(out var life) && life.State == LifecycleState.Captured) life.State = LifecycleState.Alive;
            state.DetainedResidenceWorkAreaId = string.Empty; state.CarrierId = EntityId.None;
            state.Disposition = CivilianDisposition.Fleeing;
            var site = MortalCivilianQuery.ResolveCurrentSiteId(world, target);
            BeginFlee(world, entity, state, site);
            return Result.Success();
        }

        public static Result RequestCapture(SimulationWorld world, EntityId carrier, EntityId target)
        {
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
            }
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
            state.CarrierId = ResolveCarrier(world, state.CarrierId);
            if (state.CarrierId.IsNone) return;
            if (CharacterWorldPresenceQuery.TryResolve(world, state.CarrierId, out var carrier) && carrier.HasWorldPosition)
                world.WorldPresence.SetAtWorldPosition(entity.Id, carrier.WorldPosition, carrier.SurfaceId);
        }

        public static Result SetResidenceUsage(SimulationWorld world, string areaId, ResidenceUsage usage)
        {
            if (world == null || !HousingAssignmentService.CanManageHousing(world) ||
                !world.TryGetWorkArea(areaId, out var area) || !HousingAssignmentService.IsHousingArea(area))
                return Invalid("Residence usage requires an authorized housing area.");
            if (usage == ResidenceUsage.Normal)
                foreach (var pair in world.Civilians.All)
                    if (pair.Value.Disposition == CivilianDisposition.Detained &&
                        string.Equals(pair.Value.DetainedResidenceWorkAreaId, areaId, StringComparison.Ordinal))
                        return Invalid("Release or recruit detained occupants before restoring normal housing usage.");
            world.Civilians.SetResidenceUsage(areaId, usage); return Result.Success();
        }

        public static Result Detain(SimulationWorld world, EntityId target, string residenceId)
        {
            if (!TryInteractive(world, target, out var entity, out var state) || state.Disposition != CivilianDisposition.CapturedEscorted)
                return Invalid("Only an escorted captive can be detained.");
            var party = world.Strategic.PlayerPartyContext;
            if (state.CarrierId.IsNone || party?.IsMember(state.CarrierId) != true ||
                !world.Entities.TryGet(state.CarrierId, out var carrier) || !CombatLifeStateService.CanFight(carrier))
                return Invalid("Detention requires a living player-party carrier.");
            if (world.Civilians.GetResidenceUsage(residenceId) != ResidenceUsage.PrisonerOnly ||
                !world.TryGetWorkArea(residenceId, out var area) || !HousingAssignmentService.IsHousingArea(area))
                return Invalid("A PrisonerOnly residence is required.");
            var currentSiteId = MortalCivilianQuery.ResolveCurrentSiteId(world, target);
            var carrierSiteId = MortalCivilianQuery.ResolveCurrentSiteId(world, state.CarrierId);
            if (string.IsNullOrEmpty(currentSiteId) || !world.Strategic.Sites.TryGet(currentSiteId, out var currentSite) ||
                !string.Equals(currentSiteId, carrierSiteId, StringComparison.Ordinal) ||
                !string.Equals(currentSite.OwnerFactionId, world.Strategic.PlayerFactionId, StringComparison.Ordinal))
                return Invalid("Captive must be at a player-controlled Site.");
            var occupants = new HashSet<EntityId>();
            foreach (var resident in world.Entities.All)
                if (resident.TryGet<ActivityTendencyComponent>(out var tendency) &&
                    string.Equals(tendency.HomeWorkAreaId, residenceId, StringComparison.Ordinal))
                    occupants.Add(resident.Id);
            foreach (var pair in world.Civilians.All)
                if (pair.Value.Disposition == CivilianDisposition.Detained &&
                    string.Equals(pair.Value.DetainedResidenceWorkAreaId, residenceId, StringComparison.Ordinal))
                    occupants.Add(pair.Value.EntityId);
            if (occupants.Count >= Math.Max(1, area.Capacity)) return Invalid("Prisoner residence is full.");
            state.Disposition = CivilianDisposition.Detained; state.DetainedResidenceWorkAreaId = residenceId;
            state.CarrierId = EntityId.None; state.CurrentSiteId = MortalCivilianQuery.ResolveCurrentSiteId(world, target);
            if (entity.TryGet<MovementIntentComponent>(out var intent)) intent.Clear();
            return Result.Success();
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
            return Result.Success();
        }

        static bool TryInteractive(SimulationWorld world, EntityId target, out Entity entity, out MortalCivilianState state)
        {
            entity = null; state = null;
            return world != null && world.Entities.TryGet(target, out entity) &&
                   MortalCivilianQuery.IsManagedCivilian(world, entity) && (state = Ensure(world, entity)) != null;
        }
        static Result Invalid(string message) => Result.Failure(ErrorCode.InvalidOperation, message);
    }
}
