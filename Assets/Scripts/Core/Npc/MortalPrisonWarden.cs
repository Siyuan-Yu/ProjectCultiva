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
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    public static partial class MortalCivilianService
    {
        public static void SettleWardenPayload(SimulationWorld world, Entity worker, MortalCivilianState state)
        {
            if (string.IsNullOrEmpty(state.WardenCarriedResourceId))
            { state.WardenTargetId = EntityId.None; return; }
            var dead = worker.TryGet<LifecycleComponent>(out var life) && (life.IsDead || life.IsRemoved);
            var returned = !dead && worker.TryGet<FactionMembershipComponent>(out var faction) && faction.IsAffiliated &&
                world.Strategic.Sites.TryGet(state.WardenSourceSiteId, out var site) && site.IsCoreActive &&
                site.OwnerFactionId == faction.FactionId &&
                world.SiteStorageRooms.TryGet(state.WardenSourceRoomId, out var room) && room.SiteId == site.SiteId &&
                WorldSitePublicStockService.TryAdd(world, site.SiteId, state.WardenCarriedResourceId, 1, worker.Id).IsSuccess;
            if (!returned && !GroundLootService.TryDropFromCharacter(world, worker, state.WardenCarriedResourceId, 1)) return;
            state.WardenCarriedResourceId = state.WardenSourceSiteId = state.WardenSourceRoomId = string.Empty;
            state.WardenTargetId = EntityId.None;
            state.WardenStatus = "未交付物资已归还公库或落地";
        }

        static bool WardenCanReachPrisoner(SimulationWorld w, Entity worker, EntityId target,
            out OutdoorConstructedAssetState room)
        {
            room = null;
            return worker.TryGet<FactionMembershipComponent>(out var own) && own.FactionId == w.Strategic.PlayerFactionId &&
                PrisonManagementService.TryManagedPrisoner(w, target, out room) &&
                CharacterWorldPresenceQuery.TryResolve(w, worker.Id, out var at) && at.HasWorldPosition &&
                at.SurfaceId == room.SurfaceId && MortalCivilianQuery.IsWithinLocalContinuity(w, at.SurfaceId,
                    at.WorldPosition, new WorldVec2(room.WorldX, room.WorldY));
        }

        static bool ClaimedByOtherWarden(SimulationWorld w, EntityId worker, EntityId target)
        {
            foreach (var pair in w.Civilians.All)
                if (pair.Key != worker && pair.Value.Profession == MortalProfession.Warden &&
                    pair.Value.Activity == MortalActivity.PrisonCare && pair.Value.WardenTargetId == target &&
                    w.Entities.TryGet(pair.Key, out var other) && CombatLifeStateService.CanFight(other)) return true;
            return false;
        }

        static string RequiredPrisonSupply(SimulationWorld w, EntityId target)
        {
            if (PrisonManagementService.NeedsEmergencyMedicine(w, target) && PrisonManagementService.ShouldSendMedicine(w, target)) return HerbResourceId;
            if (PrisonManagementService.Satiety(w, target) <= MortalCivilianTuning.HungerThreshold) return GrainResourceId;
            return PrisonManagementService.ShouldSendMedicine(w, target)
                ? HerbResourceId : string.Empty;
        }

        static void FailWarden(SimulationWorld w, Entity worker, MortalCivilianState state, string reason)
        {
            ClearCivilianMovement(worker);
            if (!state.WardenTargetId.IsNone) state.WardenTargetRetryAfter[state.WardenTargetId.Value] = w.Tick.Value + 8;
            state.WardenRetryAfterTick = w.Tick.Value + (string.IsNullOrEmpty(state.WardenCarriedResourceId) ? 1UL : 8UL);
            state.WardenStatus = reason;
            if (string.IsNullOrEmpty(state.WardenCarriedResourceId)) state.WardenTargetId = EntityId.None;
        }

        static void WorkAsWarden(SimulationWorld w, Entity worker, MortalCivilianState state)
        {
            if (w.Tick.Value < state.WardenRetryAfterTick) return;
            if (worker.TryGet<MovementIntentComponent>(out var failed) && failed.Active &&
                failed.DestinationKey.StartsWith("prison-warden:", StringComparison.Ordinal) &&
                (failed.HostPathState == MovementHostPathState.RetryablePathUnavailable || failed.HostPathState == MovementHostPathState.PermanentFailure))
            { FailWarden(w, worker, state, "监管路径不通，携带物保留，稍后重试"); return; }
            var carrying = !string.IsNullOrEmpty(state.WardenCarriedResourceId);
            if (!state.WardenTargetId.IsNone && (!WardenCanReachPrisoner(w, worker, state.WardenTargetId, out _) ||
                string.IsNullOrEmpty(RequiredPrisonSupply(w, state.WardenTargetId))))
            {
                SettleWardenPayload(w, worker, state);
                if (!string.IsNullOrEmpty(state.WardenCarriedResourceId)) return;
                state.WardenTargetId = EntityId.None; carrying = false;
            }
            if (state.WardenTargetId.IsNone)
            {
                var expired = new List<ulong>();
                foreach (var retry in state.WardenTargetRetryAfter)
                    if (retry.Value <= w.Tick.Value) expired.Add(retry.Key);
                foreach (var id in expired) state.WardenTargetRetryAfter.Remove(id);
                EntityId selected = EntityId.None;
                foreach (var prisoner in w.Entities.All)
                    if (WardenCanReachPrisoner(w, worker, prisoner.Id, out _) &&
                        !state.WardenTargetRetryAfter.ContainsKey(prisoner.Id.Value) &&
                        !ClaimedByOtherWarden(w, worker.Id, prisoner.Id) && !string.IsNullOrEmpty(RequiredPrisonSupply(w, prisoner.Id)) &&
                        (selected.IsNone || PreferPrisonPatient(w, prisoner.Id, selected)))
                        selected = prisoner.Id;
                state.WardenTargetId = selected;
                if (selected.IsNone)
                {
                    if (state.WardenTargetRetryAfter.Count == 0) state.WardenStatus = "本地暂无需要补给的囚犯";
                    ClearCivilianMovement(worker); return;
                }
            }
            var target = state.WardenTargetId;
            if (!WardenCanReachPrisoner(w, worker, target, out var prison)) return;
            if (!carrying)
            {
                var required = RequiredPrisonSupply(w, target);
                if (!FetchWardenSupply(w, worker, state, required))
                    FailWarden(w, worker, state, required == HerbResourceId ? "监管受阻：无可达合法储藏室或公库缺药" : "监管受阻：无可达合法储藏室或公库缺粮");
                return;
            }
            if (!CharacterWorldPresenceQuery.TryResolve(w, target, out var patient) || !patient.HasWorldPosition ||
                !w.SurfaceSpatial.TryGet(prison.SurfaceId, out var metric)) return;
            var points = new List<WorldVec2>();
            for (var dy = -2; dy <= 2; dy++) for (var dx = -2; dx <= 2; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var point = new WorldVec2(patient.WorldPosition.X + dx*metric.CellSize, patient.WorldPosition.Y + dy*metric.CellSize);
                if (PrisonConfinementService.IsInside(w, prison, prison.SurfaceId, point)) points.Add(point);
            }
            if (!TryAccessiblePoint(w, worker, prison.SurfaceId, points, out var access))
            { FailWarden(w, worker, state, "监管受阻：无法进入囚犯身边，携带物保留"); return; }
            state.WardenStatus = "携带" + w.InventoryCatalog.GetName(state.WardenCarriedResourceId) + "前往囚犯身边";
            if (!ReachWardenPoint(w, worker, prison.SurfaceId, access, metric.CellSize * .25f)) return;
            if (PrisonManagementService.Deliver(w, target, state.WardenCarriedResourceId))
            {
                state.WardenCarriedResourceId = state.WardenSourceSiteId = state.WardenSourceRoomId = string.Empty;
                state.WardenTargetId = EntityId.None;
                state.WardenStatus = "已完成一次真实送饭／送药";
            }
            else SettleWardenPayload(w, worker, state);
        }

        static bool PreferPrisonPatient(SimulationWorld w, EntityId candidate, EntityId previous)
        {
            var urgent = PrisonManagementService.NeedsEmergencyMedicine(w, candidate);
            var previousUrgent = PrisonManagementService.NeedsEmergencyMedicine(w, previous);
            if (urgent != previousUrgent) return urgent;
            var hunger = PrisonManagementService.Satiety(w, candidate).CompareTo(PrisonManagementService.Satiety(w, previous));
            return hunger < 0 || hunger == 0 && candidate.Value < previous.Value;
        }

        static bool ReachWardenPoint(SimulationWorld w, Entity worker, string surface, WorldVec2 point, float radius)
        {
            if (CharacterWorldPresenceQuery.TryResolve(w, worker.Id, out var at) && at.HasWorldPosition && at.SurfaceId == surface &&
                WorldVec2.Distance(at.WorldPosition, point) <= radius)
            { ClearCivilianMovement(worker); return true; }
            if (!w.ContinuousOutdoorMaterialization.IsMaterialized(worker.Id))
            {
                if (!NpcColdMovement.Advance(w, worker, surface, point, radius, out var arrived))
                {
                    if (w.Civilians.TryGet(worker.Id, out var state)) FailWarden(w, worker, state, "离屏监管受阻：无合法移动权限或可走路径");
                    return false;
                }
                return arrived;
            }
            MoveToWorldPoint(worker, surface, point, destinationKey: "prison-warden:" + worker.Id.Value, arrivalRadiusWorld: radius);
            return false;
        }

        static bool FetchWardenSupply(SimulationWorld w, Entity worker, MortalCivilianState state, string required)
        {
            if (!CharacterWorldPresenceQuery.TryResolve(w, worker.Id, out var at) || !at.HasWorldPosition ||
                !worker.TryGet<FactionMembershipComponent>(out var faction) || faction.FactionId != w.Strategic.PlayerFactionId) return false;
            WorldSiteStorageRoomState storage = null;
            var selected = default(WorldVec2); var best = float.MaxValue; var resource = required;
            foreach (var pair in w.SiteStorageRooms.All)
            {
                var room = pair.Value;
                if (room.SurfaceId != at.SurfaceId || !w.Strategic.Sites.TryGet(room.SiteId, out var site) || !site.IsCoreActive ||
                    site.OwnerFactionId != faction.FactionId || !MortalCivilianQuery.IsWithinLocalContinuity(w, at.SurfaceId,
                        at.WorldPosition, new WorldVec2(room.WorldX, room.WorldY))) continue;
                var item = required;
                if (required == HerbResourceId ? WorldSitePublicStockService.GetCount(w, room.SiteId, item) < 1 : !TryFindSiteFood(w, room.SiteId, out item)) continue;
                var points = new List<WorldVec2>();
                for (var i = 0; i < WorldSiteStorageRoomState.AccessCandidateCount; i++)
                    if (room.TryGetAccessCandidate(i, out var access)) points.Add(access);
                if (!TryAccessiblePoint(w, worker, room.SurfaceId, points, out var point)) continue;
                var distance = DistanceSquared(at.WorldPosition, point);
                if (distance >= best) continue;
                best = distance; storage = room; selected = point; resource = item;
            }
            if (storage == null) return false;
            state.WardenStatus = "前往储藏室领取" + w.InventoryCatalog.GetName(resource);
            var radius = w.SurfaceSpatial.TryGet(at.SurfaceId, out var metric) ? metric.CellSize * .25f : .01f;
            if (!ReachWardenPoint(w, worker, storage.SurfaceId, selected, radius)) return true;
            if (WorldSitePublicStockService.TryRemove(w, storage.SiteId, resource, 1, worker.Id).IsFailure) return false;
            state.WardenCarriedResourceId = resource;
            state.WardenSourceSiteId = storage.SiteId; state.WardenSourceRoomId = storage.StorageRoomId;
            state.WardenStatus = "已取出一份物资，准备送往牢房";
            return true;
        }
    }
}
