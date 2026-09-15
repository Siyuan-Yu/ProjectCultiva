using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>One WorldSite's public administrative resources. Political ownership is read from WorldSite.</summary>
    public sealed class WorldSitePublicStockState
    {
        readonly Dictionary<string, int> _resources =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public WorldSitePublicStockState(string siteId)
        {
            if (string.IsNullOrWhiteSpace(siteId)) throw new ArgumentException("SiteId is required.", nameof(siteId));
            SiteId = siteId;
        }

        public string SiteId { get; }
        public IReadOnlyDictionary<string, int> Resources => _resources;
        public int GetCount(string resourceId) =>
            !string.IsNullOrEmpty(resourceId) && _resources.TryGetValue(resourceId, out var amount) ? amount : 0;

        internal Result Set(string resourceId, int amount)
        {
            if (string.IsNullOrWhiteSpace(resourceId) || amount < 0)
                return Result.Failure(ErrorCode.InvalidArgument, "Public stock requires resourceId and non-negative amount.");
            _resources[resourceId] = amount;
            return Result.Success();
        }
    }

    public sealed class WorldSitePublicStockBoard
    {
        readonly Dictionary<string, WorldSitePublicStockState> _bySite =
            new Dictionary<string, WorldSitePublicStockState>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, WorldSitePublicStockState> All => _bySite;
        public bool HasSnapshotAuthority { get; internal set; }
        public bool DefaultsInitialized { get; internal set; }
        public void MarkDefaultsInitialized() => DefaultsInitialized = true;
        public void Clear() { _bySite.Clear(); HasSnapshotAuthority = false; DefaultsInitialized = false; }
        public bool TryGet(string siteId, out WorldSitePublicStockState state)
        {
            state = null;
            return !string.IsNullOrEmpty(siteId) && _bySite.TryGetValue(siteId, out state);
        }
        public WorldSitePublicStockState GetOrCreate(string siteId)
        {
            if (string.IsNullOrWhiteSpace(siteId)) throw new ArgumentException("SiteId is required.", nameof(siteId));
            if (!_bySite.TryGetValue(siteId, out var state))
            {
                state = new WorldSitePublicStockState(siteId);
                _bySite.Add(siteId, state);
            }
            return state;
        }
    }

    public static class WorldSitePublicStockService
    {
        public static int GetCount(SimulationWorld world, string siteId, string resourceId) =>
            world?.Strategic?.SitePublicStocks != null &&
            world.Strategic.SitePublicStocks.TryGet(siteId, out var state)
                ? state.GetCount(resourceId)
                : 0;

        public static Result<int> TryAdd(SimulationWorld world, string siteId, string resourceId, int amount,
            EntityId? actor = null)
        {
            if (!Validate(world, siteId, resourceId, amount, out var state, out var failure))
                return Result.Fail<int>(failure.Error);
            int next;
            try { next = checked(state.GetCount(resourceId) + amount); }
            catch (OverflowException) { return Result.Fail<int>(ErrorCode.InvalidOperation, "Public stock overflow."); }
            var set = state.Set(resourceId, next);
            if (set.IsFailure) return Result.Fail<int>(set.Error);
            Publish(world, siteId, resourceId, next, amount, actor);
            return Result.Ok(next);
        }

        public static Result<int> TryRemove(SimulationWorld world, string siteId, string resourceId, int amount,
            EntityId? actor = null)
        {
            if (!Validate(world, siteId, resourceId, amount, out var state, out var failure))
                return Result.Fail<int>(failure.Error);
            var current = state.GetCount(resourceId);
            if (current < amount)
                return Result.Fail<int>(ErrorCode.InvalidOperation, "Insufficient WorldSite public stock.");
            var next = current - amount;
            var set = state.Set(resourceId, next);
            if (set.IsFailure) return Result.Fail<int>(set.Error);
            Publish(world, siteId, resourceId, next, -amount, actor);
            return Result.Ok(next);
        }

        public static Result SetInitial(SimulationWorld world, string siteId, string resourceId, int amount)
        {
            if (world?.Strategic?.Sites == null || string.IsNullOrWhiteSpace(siteId) ||
                string.IsNullOrWhiteSpace(resourceId) || amount < 0 ||
                !world.Strategic.Sites.TryGet(siteId, out _))
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid initial WorldSite public stock.", siteId ?? string.Empty);
            return world.Strategic.SitePublicStocks.GetOrCreate(siteId).Set(resourceId, amount);
        }

        static bool Validate(SimulationWorld world, string siteId, string resourceId, int amount,
            out WorldSitePublicStockState state, out Result failure)
        {
            state = null; failure = Result.Success();
            if (world?.Strategic?.Sites == null || string.IsNullOrWhiteSpace(siteId) ||
                string.IsNullOrWhiteSpace(resourceId) || amount < 0)
            { failure = Result.Failure(ErrorCode.InvalidArgument, "Invalid WorldSite public stock mutation."); return false; }
            if (!world.Strategic.Sites.TryGet(siteId, out _))
            { failure = Result.Failure(ErrorCode.NotFound, "WorldSite public stock target missing.", siteId); return false; }
            state = world.Strategic.SitePublicStocks.GetOrCreate(siteId);
            return true;
        }

        static void Publish(SimulationWorld world, string siteId, string resourceId, int amount, int delta,
            EntityId? actor)
        {
            world.Events.Publish(EventType.WorldSitePublicStockChanged, world.Tick, actor,
                payload: siteId + "|" + resourceId + "|" + amount + "|" + delta);
        }
    }
}
