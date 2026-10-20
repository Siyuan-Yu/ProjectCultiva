using System;
using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Content;
using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Surface;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    public static partial class MortalCivilianService
    {
        static bool CanPracticeMedicine(SimulationWorld world, Entity doctor, MortalCivilianState state) =>
            state.Profession == MortalProfession.Medic && state.Disposition == CivilianDisposition.Normal &&
            doctor.TryGet<LifecycleComponent>(out var life) && life.State == LifecycleState.Alive &&
            MortalCivilianMovementAuthority.Resolve(world, doctor.Id, state) == CivilianMovementOwner.CivilianLocal;

        static void InterruptMedic(MortalCivilianState state)
        {
            state.MedicTargetId = EntityId.None;
            state.MedicTreatmentSeconds = 0;
            state.MedicLastTreatmentTick = 0;
            // A withdrawn herb is physical carried stock; interruption never recreates or destroys it.
        }

        static void ReconcileMedicAssignment(SimulationWorld world, Entity doctor, MortalCivilianState state)
        {
            if (state.MedicCarryingHerb && (state.Profession != MortalProfession.Medic ||
                doctor.TryGet<LifecycleComponent>(out var life) && (life.IsDead || life.IsRemoved)))
                SettleCarriedMedicine(world, doctor, state);
            if (!CanPracticeMedicine(world, doctor, state)) InterruptMedic(state);
            if (state.MedicLastTreatmentTick + 1 != world.Tick.Value)
                state.MedicTreatmentSeconds = 0;
        }

        public static void SettleCarriedMedicine(SimulationWorld world, Entity doctor, MortalCivilianState state)
        {
            if (!state.MedicCarryingHerb) return;
            var dead = doctor.TryGet<LifecycleComponent>(out var life) && (life.IsDead || life.IsRemoved);
            var returned = false;
            if (!dead)
            {
                if (!state.MedicSourcePartyMemberId.IsNone)
                    returned = world.Inventory.TryAdd(HerbResourceId, 1) == 1;
                if (!returned && doctor.TryGet<FactionMembershipComponent>(out var own) && own.IsAffiliated)
                {
                    var sites = new[] { state.MedicSourceSiteId, MortalCivilianQuery.ResolveCurrentSiteId(world, doctor.Id) };
                    foreach (var siteId in sites)
                        if (!string.IsNullOrEmpty(siteId) && world.Strategic.Sites.TryGet(siteId, out var site) &&
                            site.IsCoreActive && site.OwnerFactionId == own.FactionId && world.SiteStorageRooms.TryGetBySite(siteId, out _) &&
                            WorldSitePublicStockService.TryAdd(world, siteId, HerbResourceId, 1, doctor.Id).IsSuccess)
                        { returned = true; break; }
                }
            }
            if (!returned) returned = GroundLootService.TryDropFromCharacter(world, doctor, HerbResourceId, 1);
            if (!returned) return; // Keep the one authoritative unit until a lawful destination exists.
            state.MedicCarryingHerb = false;
            state.MedicSourceSiteId = string.Empty;
            state.MedicSourceRoomId = string.Empty;
            state.MedicSourcePartyMemberId = EntityId.None;
            InterruptMedic(state);
            state.MedicStatus = dead ? "携带灵药已落地" : "转职后未用灵药已归还或落地";
        }

        static bool IsPatient(SimulationWorld world, Entity doctor, Entity patient,
            out OutdoorConstructedAssetState bed)
        {
            bed = null;
            if (patient == null || patient.Id == doctor.Id ||
                !patient.TryGet<LifecycleComponent>(out var life) || !life.IsDying ||
                MedicalRecoveryService.ResolveRateMilli(world, patient) > 0 ||
                IsRescueReserved(world, patient.Id) ||
                world.Civilians.TryGet(patient.Id, out var captive) && !captive.CarrierId.IsNone ||
                CharacterEncounterService.OwnsParticipantSpatialState(world, patient.Id) ||
                !MedicalRecoveryService.TryGetPatientBed(world, patient, out bed) ||
                !CharacterWorldPresenceQuery.TryResolve(world, doctor.Id, out var from) || !from.HasWorldPosition ||
                from.SurfaceId != bed.SurfaceId ||
                !MortalCivilianQuery.IsWithinLocalContinuity(world, bed.SurfaceId, from.WorldPosition,
                    new WorldVec2(bed.WorldX, bed.WorldY)) ||
                !doctor.TryGet<FactionMembershipComponent>(out var own) || !own.IsAffiliated)
                return false;
            if (patient.TryGet<FactionMembershipComponent>(out var faction) && faction.IsAffiliated &&
                faction.FactionId == own.FactionId) return true;
            // An occupied prison bed in our administered site is eligible regardless of prisoner faction.
            var siteId = bed.BoundWorldSiteId;
            if (string.IsNullOrEmpty(siteId) && world.OutdoorConstructedAssets.Assets.TryGetValue(bed.ParentRoomId, out var room))
                siteId = room.BoundWorldSiteId;
            return RoomBedFacilityService.BedHasUsage(world, bed, RoomUsage.Prison) &&
                world.Strategic.Sites.TryGet(siteId, out var site) && site.OwnerFactionId == own.FactionId;
        }

        static bool ClaimedByOtherMedic(SimulationWorld world, Entity doctor, EntityId patient)
        {
            foreach (var pair in world.Civilians.All)
                if (pair.Key != doctor.Id && pair.Value.MedicTargetId == patient &&
                    pair.Value.Activity == MortalActivity.MedicCare &&
                    world.Entities.TryGet(pair.Key, out var other) && CanPracticeMedicine(world, other, pair.Value))
                    return true;
            return false;
        }

        public static bool HasMedicTarget(SimulationWorld world, Entity doctor, MortalCivilianState state)
        {
            if (!CanPracticeMedicine(world, doctor, state) || world.Tick.Value < state.MedicRetryAfterTick)
                return false;
            if (!state.MedicTargetId.IsNone && world.Entities.TryGet(state.MedicTargetId, out var retained) &&
                IsPatient(world, doctor, retained, out _) && !ClaimedByOtherMedic(world, doctor, retained.Id))
                return true;
            InterruptMedic(state);
            Entity best = null;
            foreach (var patient in world.Entities.All)
            {
                if (!IsPatient(world, doctor, patient, out _) || ClaimedByOtherMedic(world, doctor, patient.Id)) continue;
                patient.TryGet<LifecycleComponent>(out var life);
                if (best == null) best = patient;
                else
                {
                    best.TryGet<LifecycleComponent>(out var previous);
                    if (life.DyingReserveMilli < previous.DyingReserveMilli ||
                        life.DyingReserveMilli == previous.DyingReserveMilli && patient.Id.Value < best.Id.Value)
                        best = patient;
                }
            }
            state.MedicTargetId = best?.Id ?? EntityId.None;
            return best != null;
        }

        static void FailMedic(SimulationWorld world, Entity doctor, MortalCivilianState state, string message)
        {
            InterruptMedic(state);
            ClearCivilianMovement(doctor);
            state.MedicRetryAfterTick = world.Tick.Value + 8;
            state.MedicStatus = message;
        }

        static bool TryAccessiblePoint(SimulationWorld world, Entity doctor, string surfaceId,
            IReadOnlyList<WorldVec2> points, out WorldVec2 point)
        {
            point = default;
            if (!CharacterWorldPresenceQuery.TryResolve(world, doctor.Id, out var from) || !from.HasWorldPosition ||
                from.SurfaceId != surfaceId || !world.SurfaceGround.TryGet(surfaceId, out var ground)) return false;
            var radius = world.SurfaceSpatial.TryGet(surfaceId, out var metric) ? metric.CellSize * .5f : .01f;
            foreach (var candidate in points)
                if (DistanceSquared(from.WorldPosition, candidate) <= radius * radius)
                { point = candidate; return true; }
            if (doctor.TryGet<MovementIntentComponent>(out var active) && active.Active && active.HasWorldTarget &&
                active.TargetSurfaceId == surfaceId && active.HostPathState != MovementHostPathState.PermanentFailure &&
                active.HostPathState != MovementHostPathState.RetryablePathUnavailable)
                foreach (var candidate in points)
                    if (DistanceSquared(candidate, new WorldVec2(active.TargetWorldX, active.TargetWorldY)) < .00000001f)
                    { point = candidate; return true; }
            var distance = float.MaxValue;
            var found = false;
            var route = new List<WorldVec2>();
            foreach (var candidate in points)
            {
                var d = DistanceSquared(from.WorldPosition, candidate);
                if (d >= distance) continue;
                route.Clear();
                if (ground.TryFindRoute(from.WorldPosition, candidate, route) != SurfaceGroundRouteStatus.Found) continue;
                point = candidate; distance = d; found = true;
            }
            return found;
        }

        static bool ReachMedicalPoint(SimulationWorld world, Entity doctor, string surfaceId, WorldVec2 point)
        {
            var radius = world.SurfaceSpatial.TryGet(surfaceId, out var metric) ? metric.CellSize * .5f : .01f;
            if (CharacterWorldPresenceQuery.TryResolve(world, doctor.Id, out var presence) && presence.HasWorldPosition &&
                presence.SurfaceId == surfaceId && DistanceSquared(presence.WorldPosition, point) <= radius * radius)
            { ClearCivilianMovement(doctor); return true; }
            MoveToWorldPoint(doctor, surfaceId, point, arrivalRadiusWorld: radius);
            return false;
        }

        static bool FetchMedicalHerb(SimulationWorld world, Entity doctor, MortalCivilianState state)
        {
            if (!doctor.TryGet<FactionMembershipComponent>(out var own) || !own.IsAffiliated ||
                !CharacterWorldPresenceQuery.TryResolve(world, doctor.Id, out var from) || !from.HasWorldPosition) return false;
            // Keep a selected source/access stable while traveling; quantities are checked again on arrival.
            WorldSiteStorageRoomState storage = null;
            var point = default(WorldVec2);
            var best = float.MaxValue;
            foreach (var pair in world.SiteStorageRooms.All)
            {
                var candidate = pair.Value;
                if (candidate.SurfaceId != from.SurfaceId ||
                    !world.Strategic.Sites.TryGet(candidate.SiteId, out var site) || !site.IsCoreActive ||
                    site.OwnerFactionId != own.FactionId ||
                    WorldSitePublicStockService.GetCount(world, candidate.SiteId, HerbResourceId) < 1 ||
                    !MortalCivilianQuery.IsWithinLocalContinuity(world, from.SurfaceId, from.WorldPosition,
                        new WorldVec2(candidate.WorldX, candidate.WorldY))) continue;
                var points = new List<WorldVec2>();
                for (var i = 0; i < WorldSiteStorageRoomState.AccessCandidateCount; i++)
                    if (candidate.TryGetAccessCandidate(i, out var access)) points.Add(access);
                if (!TryAccessiblePoint(world, doctor, candidate.SurfaceId, points, out var accessPoint)) continue;
                var distance = DistanceSquared(from.WorldPosition, accessPoint);
                if (distance >= best) continue;
                storage = candidate; point = accessPoint; best = distance;
            }
            if (storage != null)
            {
                state.MedicSourceSiteId = storage.SiteId;
                state.MedicSourceRoomId = storage.StorageRoomId;
                state.MedicSourcePartyMemberId = EntityId.None;
                state.MedicStatus = "前往储藏室取灵药";
                if (!ReachMedicalPoint(world, doctor, storage.SurfaceId, point)) return true;
                if (WorldSitePublicStockService.TryRemove(world, storage.SiteId, HerbResourceId, 1, doctor.Id).IsSuccess)
                { state.MedicCarryingHerb = true; state.MedicStatus = "已领取灵药，前往床边"; return true; }
                return false;
            }
            // Party inventory may supply medicine only through a real, nearby player-party member.
            if (own.FactionId != world.Strategic.PlayerFactionId || world.Inventory.GetCount(HerbResourceId) < 1) return false;
            Entity supplier = null;
            foreach (var member in world.Entities.All)
            {
                if (world.Strategic.PlayerPartyContext?.IsMember(member.Id) != true ||
                    !member.TryGet<LifecycleComponent>(out var life) || life.State != LifecycleState.Alive ||
                    CharacterEncounterService.OwnsParticipantSpatialState(world, member.Id) ||
                    !CharacterWorldPresenceQuery.TryResolve(world, member.Id, out var presence) || !presence.HasWorldPosition ||
                    presence.SurfaceId != from.SurfaceId ||
                    !MortalCivilianQuery.IsWithinLocalContinuity(world, from.SurfaceId, from.WorldPosition, presence.WorldPosition) ||
                    !TryAccessiblePoint(world, doctor, from.SurfaceId, new[] { presence.WorldPosition }, out var access)) continue;
                var distance = DistanceSquared(from.WorldPosition, access);
                if (distance >= best) continue;
                best = distance; supplier = member; point = access;
            }
            if (supplier == null) return false;
            state.MedicSourceSiteId = string.Empty; state.MedicSourceRoomId = string.Empty;
            state.MedicSourcePartyMemberId = supplier.Id;
            state.MedicStatus = "前往队伍领取灵药";
            if (!ReachMedicalPoint(world, doctor, from.SurfaceId, point)) return true;
            if (!world.Inventory.TryRemoveAll(HerbResourceId, 1)) return false;
            state.MedicCarryingHerb = true;
            state.MedicStatus = "已从队伍领取灵药，前往床边";
            return true;
        }

        static void TreatAtBed(SimulationWorld world, Entity doctor, MortalCivilianState state)
        {
            if (doctor.TryGet<MovementIntentComponent>(out var failedMove) && failedMove.Active &&
                (failedMove.HostPathState == MovementHostPathState.PermanentFailure ||
                 failedMove.HostPathState == MovementHostPathState.RetryablePathUnavailable))
            { FailMedic(world, doctor, state, "治疗路径受阻，保留携药稍后重试"); return; }
            if (!world.Entities.TryGet(state.MedicTargetId, out var patient) || !IsPatient(world, doctor, patient, out var bed))
            { FailMedic(world, doctor, state, "治疗中断：伤员不在有效床位或已恢复"); return; }
            if (!state.MedicCarryingHerb)
            {
                state.MedicTreatmentSeconds = 0;
                if (!FetchMedicalHerb(world, doctor, state))
                    FailMedic(world, doctor, state, "治疗等待：无合法可达的灵药来源");
                return;
            }
            var points = new List<WorldVec2>();
            RoomBedGeometry.CollectAccessPoints(world.OutdoorConstructedAssets, bed, points);
            if (!TryAccessiblePoint(world, doctor, bed.SurfaceId, points, out var access))
            { FailMedic(world, doctor, state, "治疗受阻：床边不可达"); return; }
            if (!ReachMedicalPoint(world, doctor, bed.SurfaceId, access) ||
                !RoomBedFacilityService.IsAtAccessPoint(world, bed, doctor.Id))
            { state.MedicTreatmentSeconds = 0; state.MedicStatus = "携药前往伤员床边"; return; }
            if (state.MedicLastTreatmentTick == world.Tick.Value) return;
            state.MedicLastTreatmentTick = world.Tick.Value;
            state.MedicTreatmentSeconds++;
            state.MedicStatus = "床边施药 " + state.MedicTreatmentSeconds + "/" + MedicalRecoveryService.TreatmentSeconds;
            if (state.MedicTreatmentSeconds < MedicalRecoveryService.TreatmentSeconds) return;
            patient.TryGet<LifecycleComponent>(out var patientLife);
            patientLife.HerbDoses++;
            state.MedicCarryingHerb = false;
            state.MedicTreatmentSeconds = 0;
            state.MedicStatus = "施药完成，累计 " + patientLife.HerbDoses + " 份";
            ClearCivilianMovement(doctor);
        }
    }
}
