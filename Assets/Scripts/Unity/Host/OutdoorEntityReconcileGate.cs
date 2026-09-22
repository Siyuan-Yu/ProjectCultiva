using System;
using XianXia.Core.Combat;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Unity.Host
{
    public enum OutdoorEntityReconcileDecision
    {
        None = 0,
        RefreshViewsOnly = 1,
        ReconcileAndRefresh = 2
    }

    /// <summary>
    /// Coalesces Host population refreshes around real presentation-scope changes.
    /// This boundary is pure C#: Unity lifecycle code only supplies the current Domain fingerprint
    /// and Continuous runtime reconcile generation.
    /// </summary>
    public sealed class OutdoorEntityReconcileGate
    {
        bool _initialized;
        bool _dirty;
        ulong _fingerprint;
        int _entityReconcileGeneration;
        int _dirtyAtEntityReconcileGeneration;

        public bool HasBaseline => _initialized;
        public bool IsDirty => _dirty;

        public void MarkDirty(int entityReconcileGeneration)
        {
            _dirty = true;
            _dirtyAtEntityReconcileGeneration = entityReconcileGeneration;
        }

        public OutdoorEntityReconcileDecision Decide(
            ulong fingerprint,
            int entityReconcileGeneration)
        {
            if (!_initialized)
                return OutdoorEntityReconcileDecision.ReconcileAndRefresh;

            if (_dirty)
            {
                // A runtime reconcile after the dirty signal already consumed that Domain change;
                // only Host views remain. If it happened before the signal, the matching generation
                // proves that the dirty change still needs a runtime reconcile.
                return entityReconcileGeneration != _dirtyAtEntityReconcileGeneration
                    ? OutdoorEntityReconcileDecision.RefreshViewsOnly
                    : OutdoorEntityReconcileDecision.ReconcileAndRefresh;
            }

            // Chunk streaming, activation and encounter handoff reconcile through the runtime
            // owner. A generation advance proves runtime membership is current, but Host views
            // still need to spawn/prune against that membership.
            if (entityReconcileGeneration != _entityReconcileGeneration)
                return OutdoorEntityReconcileDecision.RefreshViewsOnly;

            return fingerprint != _fingerprint
                ? OutdoorEntityReconcileDecision.ReconcileAndRefresh
                : OutdoorEntityReconcileDecision.None;
        }

        public void Commit(ulong fingerprint, int entityReconcileGeneration)
        {
            _fingerprint = fingerprint;
            _entityReconcileGeneration = entityReconcileGeneration;
            _dirty = false;
            _dirtyAtEntityReconcileGeneration = entityReconcileGeneration;
            _initialized = true;
        }

        public void Reset()
        {
            _initialized = false;
            _dirty = false;
            _fingerprint = 0;
            _entityReconcileGeneration = 0;
            _dirtyAtEntityReconcileGeneration = 0;
        }

        /// <summary>
        /// Hashes only membership/scope authority. Exact moving positions and WorldTick are
        /// intentionally excluded, so an ordinary stationary or in-route tick remains clean.
        /// </summary>
        public static ulong CaptureFingerprint(SimulationWorld world, PlayerPartyRuntime party)
        {
            if (world == null)
                return 0;

            var hash = Offset;
            Add(ref hash, world.LocalMap?.IsActive == true);
            Add(ref hash, world.LocalMap?.ActiveMapLayoutId);
            Add(ref hash, world.Strategic?.CharacterEncounter?.EncounterId);
            Add(ref hash, (int)(world.Strategic?.CharacterEncounter?.Phase ?? 0));

            if (party != null)
            {
                Add(ref hash, party.ActiveCharacterId.Value);
                Add(ref hash, party.Members.Count);
                for (var i = 0; i < party.Members.Count; i++)
                    Add(ref hash, party.Members[i].Value);
            }

            var presenceHash = 0UL;
            var presenceCount = 0;
            foreach (var pair in world.WorldPresence.All)
            {
                var entry = Offset;
                Add(ref entry, pair.Key);
                Add(ref entry, (int)(pair.Value?.Mode ?? 0));
                Add(ref entry, pair.Value?.SiteId);
                Add(ref entry, pair.Value?.PersonalSurfaceId);
                presenceHash ^= Mix(entry);
                presenceCount++;
            }
            Add(ref hash, presenceCount);
            Add(ref hash, presenceHash);

            var lifeHash = 0UL;
            var lifeCount = 0;
            foreach (var entity in world.Entities.All)
            {
                var entry = Offset;
                Add(ref entry, entity.Id.Value);
                Add(ref entry, CombatLifeStateService.ShouldHideFromSpawn(entity));
                Add(ref entry, CombatLifeStateService.CanFight(entity));
                lifeHash ^= Mix(entry);
                lifeCount++;
            }
            Add(ref hash, lifeCount);
            Add(ref hash, lifeHash);

            var squadHash = 0UL;
            var squadCount = 0;
            foreach (var pair in world.Strategic.Squads.Squads)
            {
                var squad = pair.Value;
                var entry = Offset;
                Add(ref entry, pair.Key);
                Add(ref entry, squad?.MemberCharacterIds.Count ?? 0);
                if (squad != null)
                    for (var i = 0; i < squad.MemberCharacterIds.Count; i++)
                        Add(ref entry, squad.MemberCharacterIds[i]);
                if (world.Strategic.SquadWorldMotions.TryGet(pair.Key, out var motion))
                {
                    Add(ref entry, motion.IsMoving);
                    Add(ref entry, motion.SiteId);
                    Add(ref entry, motion.SurfaceId);
                }
                squadHash ^= Mix(entry);
                squadCount++;
            }
            Add(ref hash, squadCount);
            Add(ref hash, squadHash);

            var backgroundHash = 0UL;
            var backgroundCount = 0;
            foreach (var pair in world.BackgroundCharacterTravel.All)
            {
                var entry = Offset;
                Add(ref entry, pair.Key);
                Add(ref entry, pair.Value?.IsMoving == true);
                Add(ref entry, pair.Value?.DestinationSiteId);
                Add(ref entry, pair.Value?.SurfaceId);
                backgroundHash ^= Mix(entry);
                backgroundCount++;
            }
            Add(ref hash, backgroundCount);
            Add(ref hash, backgroundHash);
            return hash;
        }

        public static ulong CombineFingerprint(ulong domainFingerprint, ulong loadedTravelScopeFingerprint)
        {
            var hash = domainFingerprint;
            Add(ref hash, loadedTravelScopeFingerprint);
            return hash;
        }

        const ulong Offset = 14695981039346656037UL;
        const ulong Prime = 1099511628211UL;

        static void Add(ref ulong hash, bool value) => Add(ref hash, value ? 1UL : 0UL);
        static void Add(ref ulong hash, int value) => Add(ref hash, unchecked((ulong)value));

        static void Add(ref ulong hash, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                Add(ref hash, 0UL);
                return;
            }
            for (var i = 0; i < value.Length; i++)
                Add(ref hash, value[i]);
        }

        static void Add(ref ulong hash, ulong value)
        {
            hash ^= value;
            hash *= Prime;
        }

        static ulong Mix(ulong value)
        {
            value ^= value >> 30;
            value *= 0xbf58476d1ce4e5b9UL;
            value ^= value >> 27;
            value *= 0x94d049bb133111ebUL;
            return value ^ (value >> 31);
        }
    }
}
