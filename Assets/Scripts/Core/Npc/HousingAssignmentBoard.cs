using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Core.Social;

namespace XianXia.Core.Npc
{
    /// <summary>Runtime housing-area ownership (session-only).</summary>
    public sealed class HousingAssignmentBoard
    {
        readonly Dictionary<string, EntityId> _ownerByArea =
            new Dictionary<string, EntityId>(System.StringComparer.Ordinal);

        public IReadOnlyDictionary<string, EntityId> OwnerByArea => _ownerByArea;

        public void Clear() => _ownerByArea.Clear();

        public bool TryGetOwner(string workAreaId, out EntityId owner)
        {
            owner = EntityId.None;
            if (string.IsNullOrEmpty(workAreaId) || !_ownerByArea.TryGetValue(workAreaId, out owner))
                return false;
            return !owner.IsNone;
        }

        public void SetOwner(string workAreaId, EntityId owner)
        {
            if (string.IsNullOrEmpty(workAreaId))
                return;
            if (owner.IsNone)
                _ownerByArea.Remove(workAreaId);
            else
                _ownerByArea[workAreaId] = owner;
        }

        public void ClearOwner(string workAreaId)
        {
            if (!string.IsNullOrEmpty(workAreaId))
                _ownerByArea.Remove(workAreaId);
        }
    }

    /// <summary>Housing = Rest／Eat home areas (not control cores).</summary>
    public static class HousingAssignmentService
    {
        public static bool IsHousingArea(WorkAreaDefinition area)
        {
            if (area == null || area.IsControlCore)
                return false;
            if (area.ResidentTags != null && area.ResidentTags.Count > 0)
                return true;
            if (area.Tags != null)
            {
                for (var i = 0; i < area.Tags.Count; i++)
                {
                    if (string.Equals(area.Tags[i], "home", System.StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return Allows(area, ScheduleActivity.Rest) || Allows(area, ScheduleActivity.Eat);
        }

        static bool Allows(WorkAreaDefinition area, ScheduleActivity activity)
        {
            if (area.AllowedActivities == null || area.AllowedActivities.Count == 0)
                return false;
            var needle = activity.ToString();
            for (var i = 0; i < area.AllowedActivities.Count; i++)
            {
                if (string.Equals(area.AllowedActivities[i], needle, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>Seed owners from entities' <see cref="ActivityTendencyComponent.HomeWorkAreaId"/>.</summary>
        public static void SeedFromHomeBindings(SimulationWorld world)
        {
            if (world == null)
                return;
            world.HousingAssignments.Clear();
            foreach (var entity in world.Entities.All)
            {
                if (!entity.TryGet<ActivityTendencyComponent>(out var tendency) ||
                    string.IsNullOrWhiteSpace(tendency.HomeWorkAreaId))
                    continue;
                if (!world.TryGetWorkArea(tendency.HomeWorkAreaId, out var area) ||
                    !IsHousingArea(area))
                    continue;
                // First binder wins as displayed owner; later still keep HomeWorkAreaId.
                if (!world.HousingAssignments.TryGetOwner(tendency.HomeWorkAreaId, out _))
                    world.HousingAssignments.SetOwner(tendency.HomeWorkAreaId, entity.Id);
            }
        }

        public static bool CanManageHousing(SimulationWorld world) =>
            world != null &&
            (world.SettlementAuthority.CanManageHousing ||
             world.ControlCores.AnyPlayerControlled() ||
             world.Flags.Has("settlement_player_controlled"));

        public static bool CanManageSchedules(SimulationWorld world) =>
            world != null &&
            (world.SettlementAuthority.CanManageSchedules ||
             world.ControlCores.AnyPlayerControlled() ||
             world.Flags.Has("settlement_player_controlled"));

        public static string ResolvePlayerFactionId(SimulationWorld world, IReadOnlyList<EntityId> partyIds)
        {
            if (world == null || partyIds == null)
                return string.Empty;
            for (var i = 0; i < partyIds.Count; i++)
            {
                if (!world.Entities.TryGet(partyIds[i], out var e))
                    continue;
                if (!e.TryGet<FactionMembershipComponent>(out var mem) || !mem.IsAffiliated)
                    continue;
                return mem.FactionId;
            }

            return string.Empty;
        }

        public static bool IsPlayerCampMember(
            SimulationWorld world,
            IReadOnlyList<EntityId> partyIds,
            EntityId candidate)
        {
            if (world == null || candidate.IsNone || !world.Entities.TryGet(candidate, out var entity))
                return false;

            if (partyIds != null)
            {
                for (var i = 0; i < partyIds.Count; i++)
                {
                    if (partyIds[i] == candidate)
                        return true;
                }
            }

            var playerFaction = ResolvePlayerFactionId(world, partyIds);
            if (string.IsNullOrEmpty(playerFaction))
                return false;
            return entity.TryGet<FactionMembershipComponent>(out var mem) &&
                   mem.IsAffiliated &&
                   string.Equals(mem.FactionId, playerFaction, System.StringComparison.Ordinal);
        }

        public static void CollectResidents(
            SimulationWorld world,
            string workAreaId,
            List<EntityId> into)
        {
            into.Clear();
            if (world == null || string.IsNullOrEmpty(workAreaId))
                return;
            foreach (var entity in world.Entities.All)
            {
                if (!entity.TryGet<ActivityTendencyComponent>(out var tendency))
                    continue;
                if (!string.Equals(tendency.HomeWorkAreaId, workAreaId, System.StringComparison.Ordinal))
                    continue;
                into.Add(entity.Id);
            }
        }

        public static void CollectPlayerCampCandidates(
            SimulationWorld world,
            IReadOnlyList<EntityId> partyIds,
            List<EntityId> into)
        {
            into.Clear();
            if (world == null)
                return;
            foreach (var entity in world.Entities.All)
            {
                if (!IsPlayerCampMember(world, partyIds, entity.Id))
                    continue;
                into.Add(entity.Id);
            }
        }

        /// <summary>Assign housing owner; updates HomeWorkAreaId. Requires manage permission + player camp.</summary>
        public static Result TryAssignOwner(
            SimulationWorld world,
            string workAreaId,
            EntityId newOwner,
            IReadOnlyList<EntityId> partyIds)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "world null");
            if (string.IsNullOrEmpty(workAreaId) || !world.TryGetWorkArea(workAreaId, out var area))
                return Result.Failure(ErrorCode.NotFound, "Housing work area missing.");
            if (!IsHousingArea(area))
                return Result.Failure(ErrorCode.InvalidOperation, "Not a housing area.");
            if (!CanManageHousing(world))
                return Result.Failure(ErrorCode.InvalidOperation, "Need settlement management permission.");
            if (!IsPlayerCampMember(world, partyIds, newOwner))
                return Result.Failure(ErrorCode.InvalidOperation, "Owner must be in player camp.");
            if (!world.Entities.TryGet(newOwner, out var ownerEntity))
                return Result.Failure(ErrorCode.EntityNotFound, "Owner entity missing.");

            if (!ownerEntity.TryGet<ActivityTendencyComponent>(out var tendency))
            {
                tendency = new ActivityTendencyComponent();
                ownerEntity.AddComponent(tendency);
            }

            // Vacate previous home pointer if pointing elsewhere; keep others sharing this area.
            var previousHome = tendency.HomeWorkAreaId;
            tendency.HomeWorkAreaId = workAreaId;
            world.HousingAssignments.SetOwner(workAreaId, newOwner);

            world.Events.Publish(
                EventType.WorkAssignmentChanged,
                world.Tick,
                actor: newOwner,
                payload: "housing=" + workAreaId + ";prevHome=" + (previousHome ?? string.Empty));

            return Result.Success();
        }

        public static string EntityDisplayName(Entity entity)
        {
            if (entity == null)
                return "?";
            if (!string.IsNullOrWhiteSpace(entity.DisplayName))
                return entity.DisplayName;
            return entity.Id.ToString();
        }
    }
}
