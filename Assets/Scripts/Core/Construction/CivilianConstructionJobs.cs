using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Construction
{
    /// <summary>One player-requested, Site-local construction order. Material in transit is job escrow, never NPC inventory.</summary>
    public sealed class CivilianConstructionJob
    {
        public string JobId { get; set; } = string.Empty;
        public string BuildingId { get; set; } = string.Empty;
        public string DemolitionFlagId { get; set; } = string.Empty;
        public string SiteId { get; set; } = string.Empty;
        public string FactionId { get; set; } = string.Empty;
        public string SurfaceId { get; set; } = string.Empty;
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public int LaborProgress { get; set; }
        public ulong LastLaborTick { get; set; }
        public EntityId CarrierId { get; set; }
        public string PayloadItemId { get; set; } = string.Empty;
        public int PayloadCount { get; set; }
        public string LastError { get; set; } = string.Empty;
        public List<ConstructionMaterialCost> Required { get; } = new List<ConstructionMaterialCost>();
        public Dictionary<string, int> Delivered { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
        public bool MaterialsDelivered
        {
            get
            {
                for (var i = 0; i < Required.Count; i++)
                    if (!Delivered.TryGetValue(Required[i].ItemId, out var have) || have < Required[i].Count)
                        return false;
                return true;
            }
        }
    }

    public sealed class CivilianConstructionJobBoard
    {
        readonly Dictionary<string, CivilianConstructionJob> _jobs =
            new Dictionary<string, CivilianConstructionJob>(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, CivilianConstructionJob> Jobs => _jobs;
        public long NextSequence { get; private set; } = 1;
        public string NextId => "civilian:construction:" + NextSequence;
        public bool TryGet(string id, out CivilianConstructionJob job) => _jobs.TryGetValue(id ?? string.Empty, out job);
        public bool TryRegister(CivilianConstructionJob job)
        {
            if (job == null || string.IsNullOrWhiteSpace(job.JobId) || _jobs.ContainsKey(job.JobId)) return false;
            _jobs.Add(job.JobId, job); NextSequence++; return true;
        }
        public bool Restore(CivilianConstructionJob job)
        {
            if (job == null || string.IsNullOrWhiteSpace(job.JobId) || _jobs.ContainsKey(job.JobId)) return false;
            _jobs.Add(job.JobId, job); return true;
        }
        public void RestoreSequence(long sequence) { NextSequence = sequence; }
        public void Remove(string id) => _jobs.Remove(id ?? string.Empty);
        public void Clear() { _jobs.Clear(); NextSequence = 1; }
    }

    public static class CivilianConstructionJobService
    {
        public const int RequiredLabor = 12;

        public static void CancelInvalidJobs(SimulationWorld world)
        {
            var cancel = new List<string>();
            foreach (var pair in world.CivilianConstructionJobs.Jobs)
            {
                var job = pair.Value;
                if (world.Strategic.Sites.TryGet(job.SiteId, out var site) &&
                    site.OwnerFactionId == job.FactionId &&
                    (string.IsNullOrEmpty(job.DemolitionFlagId) ||
                     world.Strategic.FactionFlags.Flags.ContainsKey(job.DemolitionFlagId))) continue;
                if (TryRefund(world, job)) cancel.Add(pair.Key);
            }
            for (var i = 0; i < cancel.Count; i++) world.CivilianConstructionJobs.Remove(cancel[i]);
        }

        public static void CancelAllJobs(SimulationWorld world)
        {
            if (world == null) return;
            var canceled = new List<string>();
            foreach (var job in world.CivilianConstructionJobs.Jobs.Values)
                if (TryRefund(world, job)) canceled.Add(job.JobId);
            for (var i = 0; i < canceled.Count; i++) world.CivilianConstructionJobs.Remove(canceled[i]);
        }

        static bool TryRefund(SimulationWorld world, CivilianConstructionJob job)
        {
            if (!world.Strategic.Sites.TryGet(job.SiteId, out _))
            { job.LastError = "Source Site missing; escrow retained."; return false; }
            var refunds = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var row in job.Delivered) refunds[row.Key] = row.Value;
            if (job.PayloadCount > 0)
            {
                refunds.TryGetValue(job.PayloadItemId, out var existing);
                refunds[job.PayloadItemId] = checked(existing + job.PayloadCount);
            }
            foreach (var row in refunds)
                if (row.Value < 0 || WorldSitePublicStockService.GetCount(world, job.SiteId, row.Key) > int.MaxValue - row.Value)
                { job.LastError = "Refund overflow; escrow retained."; return false; }
            foreach (var row in refunds)
                if (row.Value > 0 && WorldSitePublicStockService.TryAdd(world, job.SiteId, row.Key, row.Value).IsFailure)
                { job.LastError = "Refund failed; escrow retained."; return false; }
            return true;
        }

        public static bool HasAvailableWorker(SimulationWorld world, string siteId, string factionId)
        {
            foreach (var entity in world.Entities.All)
                if (MortalCivilianQuery.IsManagedCivilian(world, entity) &&
                    entity.TryGet<FactionMembershipComponent>(out var own) && own.IsAffiliated &&
                    own.FactionId == factionId && MortalCivilianQuery.ResolveCurrentSiteId(world, entity.Id) == siteId &&
                    world.Civilians.TryGet(entity.Id, out var state) && state.Disposition == CivilianDisposition.Normal &&
                    world.Strategic.PlayerPartyContext?.IsMember(entity.Id) != true &&
                    MortalCivilianMovementAuthority.CanCivilianOwnLocalMovement(world, entity.Id, state) &&
                    entity.TryGet<LifecycleComponent>(out var life) && life.State == LifecycleState.Alive)
                    return true;
            return false;
        }

        public static bool HasJob(SimulationWorld world, Entity worker, bool haul) => FindJob(world, worker, haul) != null;

        static CivilianConstructionJob FindJob(SimulationWorld world, Entity worker, bool haul)
        {
            if (world == null || worker == null || !worker.TryGet<FactionMembershipComponent>(out var own) ||
                !own.IsAffiliated || !world.Civilians.TryGet(worker.Id, out var civilian) ||
                !MortalCivilianMovementAuthority.CanCivilianOwnLocalMovement(world, worker.Id, civilian)) return null;
            var site = MortalCivilianQuery.ResolveCurrentSiteId(world, worker.Id);
            var keys = new List<string>(world.CivilianConstructionJobs.Jobs.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (var i = 0; i < keys.Count; i++)
            {
                var job = world.CivilianConstructionJobs.Jobs[keys[i]];
                if (job.SiteId != site || job.FactionId != own.FactionId ||
                    job.MaterialsDelivered == haul) continue;
                if (haul && !job.CarrierId.IsNone && job.CarrierId != worker.Id) continue;
                return job;
            }
            return null;
        }

        public static void ReleaseCarrier(SimulationWorld world, EntityId worker)
        {
            foreach (var job in world.CivilianConstructionJobs.Jobs.Values)
            {
                if (job.CarrierId != worker) continue;
                if (job.PayloadCount > 0 && !string.IsNullOrEmpty(job.PayloadItemId) &&
                    WorldSitePublicStockService.TryAdd(world, job.SiteId, job.PayloadItemId, job.PayloadCount, worker).IsFailure)
                    continue;
                job.CarrierId = EntityId.None; job.PayloadItemId = string.Empty; job.PayloadCount = 0;
            }
        }

        public static void Work(SimulationWorld world, Entity worker, bool haul)
        {
            var job = FindJob(world, worker, haul);
            if (job == null) return;
            if (!world.Strategic.Sites.TryGet(job.SiteId, out var site) ||
                site.OwnerFactionId != job.FactionId || !site.HasContinuousCore)
            { ReleaseCarrier(world, worker.Id); return; }
            var workPoint = new WorldVec2(job.WorldX, job.WorldY);
            if (haul)
            {
                if (job.CarrierId.IsNone)
                {
                    var source = new WorldVec2(site.CoreWorldX, site.CoreWorldY);
                    Move(worker, job.SurfaceId, source);
                    if (!Arrived(world, worker, source)) return;
                    for (var i = 0; i < job.Required.Count; i++)
                    {
                        var cost = job.Required[i];
                        job.Delivered.TryGetValue(cost.ItemId, out var delivered);
                        var need = cost.Count - delivered;
                        if (need <= 0) continue;
                        if (WorldSitePublicStockService.TryRemove(world, job.SiteId, cost.ItemId, need, worker.Id).IsFailure)
                            return;
                        job.CarrierId = worker.Id; job.PayloadItemId = cost.ItemId; job.PayloadCount = need;
                        break;
                    }
                }
                if (job.CarrierId != worker.Id || job.PayloadCount <= 0) return;
                Move(worker, job.SurfaceId, workPoint);
                if (!Arrived(world, worker, workPoint)) return;
                job.Delivered.TryGetValue(job.PayloadItemId, out var prior);
                job.Delivered[job.PayloadItemId] = checked(prior + job.PayloadCount);
                job.CarrierId = EntityId.None; job.PayloadItemId = string.Empty; job.PayloadCount = 0;
                return;
            }
            if (!job.MaterialsDelivered) return;
            Move(worker, job.SurfaceId, workPoint);
            if (!Arrived(world, worker, workPoint) || job.LastLaborTick == world.Tick.Value) return;
            job.LastLaborTick = world.Tick.Value;
            job.LaborProgress++;
            if (job.LaborProgress < RequiredLabor) return;
            var completed = string.IsNullOrEmpty(job.DemolitionFlagId)
                ? ConstructionService.TryCompleteCivilianJob(world, job, out _)
                : ConstructionService.TryDismantleFactionFlag(world, job.BuildingId,
                    job.FactionId, job.DemolitionFlagId, out _);
            if (completed.IsSuccess) world.CivilianConstructionJobs.Remove(job.JobId);
            else { job.LaborProgress = RequiredLabor - 1; job.LastError = completed.Error.Message; }
        }

        static void Move(Entity worker, string surfaceId, WorldVec2 point)
        {
            if (!worker.TryGet<MovementIntentComponent>(out var intent))
            { intent = new MovementIntentComponent(); worker.AddComponent(intent); }
            if (!intent.Active || !intent.HasWorldTarget || intent.TargetSurfaceId != surfaceId ||
                Math.Abs(intent.TargetWorldX - point.X) > .1f || Math.Abs(intent.TargetWorldY - point.Y) > .1f)
                intent.BeginWorldPoint(surfaceId, point.X, point.Y);
        }

        static bool Arrived(SimulationWorld world, Entity worker, WorldVec2 point) =>
            worker.TryGet<MovementIntentComponent>(out var intent) && intent.HostArrived &&
            CharacterWorldPresenceQuery.TryResolve(world, worker.Id, out var presence) && presence.HasWorldPosition &&
            (presence.WorldPosition.X - point.X) * (presence.WorldPosition.X - point.X) +
            (presence.WorldPosition.Y - point.Y) * (presence.WorldPosition.Y - point.Y) <= 9f;
    }
}
