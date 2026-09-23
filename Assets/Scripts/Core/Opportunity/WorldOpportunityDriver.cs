using System;
using System.Collections.Generic;
using XianXia.Core.Bootstrap;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Exploration;
using XianXia.Core.Random;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Opportunity
{
    public sealed class WorldOpportunityDriver
    {
        const int PositionAttempts = 64;
        const int TemplateAttempts = 64;
        const float MinimumOpportunitySpacing = 1.5f;
        ulong _lastExpiryDay = ulong.MaxValue;

        public void Tick(SimulationWorld world)
        {
            if (world?.WorldOpportunities == null) return;
            var day = DayClock.FromWorldTick(world.Tick).DayIndex;
            if (_lastExpiryDay != day)
            {
                _lastExpiryDay = day;
                Expire(world, day);
            }
            CleanupRemoved(world, day);

            var party = world.Strategic?.PlayerPartyContext;
            if (!PlayerPartyWorldLocationQuery.TryResolve(world, party, out var location)) return;
            var surfaceId = location.SurfaceId;
            if (world.WorldOpportunities.WasRefreshedToday(surfaceId, day)) return;
            world.WorldOpportunities.MarkRefreshed(surfaceId, day);
            Refill(world, party, surfaceId, location.WorldPosition, day);
        }

        void Refill(SimulationWorld world, PlayerPartyRuntime party, string surfaceId, WorldVec2 playerPosition, ulong day)
        {
            var board = world.WorldOpportunities;
            if (!board.TryGetDirectorForSurface(surfaceId, out var director)) return;
            var active = board.CountActiveOnSurface(surfaceId);
            if (active >= director.TargetActiveMin) return;
            var target = director.TargetActiveMin;
            if (director.TargetActiveMax > director.TargetActiveMin)
                target = world.Random.NextInt(director.TargetActiveMin, director.TargetActiveMax + 1);
            var actor = party != null && party.HasActive ? party.ActiveCharacterId : EntityId.None;
            var attempts = 0;
            while (active < target && attempts++ < TemplateAttempts)
            {
                var eligible = EligibleSpecs(world, surfaceId, actor);
                if (eligible.Count == 0) break;
                var spec = PickSpec(world, eligible);
                if (spec == null || !TrySpawn(world, spec, playerPosition, day)) continue;
                active++;
            }
        }

        static List<WorldOpportunitySpec> EligibleSpecs(SimulationWorld world, string surfaceId, EntityId actor)
        {
            var result = new List<WorldOpportunitySpec>();
            foreach (var spec in world.WorldOpportunities.Specs.Values)
            {
                if (spec == null || spec.Weight <= 0 || spec.MaxActive <= 0 ||
                    !string.Equals(spec.SurfaceId, surfaceId, StringComparison.Ordinal) ||
                    world.WorldOpportunities.CountActive(spec.Id) >= spec.MaxActive ||
                    !ContentConditionEvaluator.AllPass(world, actor, spec.Conditions)) continue;
                result.Add(spec);
            }
            return result;
        }

        static WorldOpportunitySpec PickSpec(SimulationWorld world, List<WorldOpportunitySpec> specs)
        {
            var index = WeightedRandomPicker.PickIndex(specs.Count, i => specs[i]?.Weight ?? 0, world.Random);
            return index >= 0 ? specs[index] : null;
        }

        static WorldOpportunityNpcCandidate PickNpc(SimulationWorld world, WorldOpportunitySpec spec)
        {
            var index = WeightedRandomPicker.PickIndex(
                spec.NpcCandidates.Count,
                i => spec.NpcCandidates[i]?.Weight ?? 0,
                world.Random);
            return index >= 0 ? spec.NpcCandidates[index] : null;
        }

        static bool TrySpawn(SimulationWorld world, WorldOpportunitySpec spec, WorldVec2 playerPosition, ulong day)
        {
            if (!TryFindPosition(world, spec, playerPosition, out var position)) return false;
            var npc = PickNpc(world, spec);
            if (npc?.Spawn == null) return false;
            var spawned = GameStartBootstrap.SpawnIntoWorld(world, npc.Spawn);
            if (spawned.IsFailure) return false;
            var entity = spawned.Value;
            if (!entity.TryGet<EntityLocationComponent>(out var location))
            {
                location = new EntityLocationComponent();
                if (entity.AddComponent(location).IsFailure)
                {
                    world.Entities.MarkRemoved(entity.Id);
                    return false;
                }
            }
            location.ClearPresence();
            world.WorldPresence.SetAtWorldPosition(entity.Id, position, spec.SurfaceId);
            var instance = new WorldOpportunityInstance
            {
                InstanceId = world.WorldOpportunities.AllocateInstanceId(),
                OpportunityDefinitionId = spec.Id,
                SurfaceId = spec.SurfaceId,
                SpawnedEntityId = entity.Id,
                CreatedDayIndex = day,
                ExpireDayIndexExclusive = day + (ulong)Math.Max(1, spec.DurationDays),
                DiscoveryMode = spec.DiscoveryMode
            };
            if (!world.WorldOpportunities.AddInstance(instance))
            {
                RemoveEntityFromWorld(world, entity.Id);
                return false;
            }
            if (string.Equals(spec.DiscoveryMode, WorldOpportunityDiscoveryMode.PublicNotice, StringComparison.Ordinal))
            {
                world.WorldActivities.CreateActive(
                    WorldActivitySourceKind.WorldOpportunity,
                    instance.InstanceId,
                    string.IsNullOrWhiteSpace(spec.PublicNoticeTitle) ? spec.Name : spec.PublicNoticeTitle,
                    spec.PublicNoticeText,
                    day);
                world.Events.Publish(EventType.WorldOpportunityNotice, world.Tick, target: entity.Id, payload: spec.PublicNoticeText);
            }
            return true;
        }

        static bool TryFindPosition(SimulationWorld world, WorldOpportunitySpec spec, WorldVec2 player, out WorldVec2 position)
        {
            position = default;
            if (!world.SurfaceGround.TryGet(spec.SurfaceId, out var ground) || ground == null) return false;
            var min = Math.Max(0f, spec.MinPlayerDistanceWorld);
            var max = Math.Max(min, spec.MaxPlayerDistanceWorld);
            for (var attempt = 0; attempt < PositionAttempts; attempt++)
            {
                var distance = min + (float)world.Random.NextDouble() * (max - min);
                var angle = world.Random.NextDouble() * Math.PI * 2.0;
                var candidate = new WorldVec2(
                    player.X + (float)Math.Cos(angle) * distance,
                    player.Y + (float)Math.Sin(angle) * distance);
                var actualDistance = WorldVec2.Distance(player, candidate);
                if (actualDistance < min || actualDistance > max || !ground.Contains(candidate.X, candidate.Y) ||
                    !ground.IsWalkable(candidate.X, candidate.Y)) continue;
                if (!spec.AllowInsideWorldSite && !string.IsNullOrEmpty(WorldSitePhysicalRegionQuery.ResolveSiteIdOrEmpty(world, candidate))) continue;
                var separated = true;
                foreach (var other in world.WorldOpportunities.ActiveInstances.Values)
                {
                    if (!string.Equals(other.SurfaceId, spec.SurfaceId, StringComparison.Ordinal) ||
                        !world.WorldPresence.TryGet(other.SpawnedEntityId, out var presence) || presence == null ||
                        !presence.HasContinuousWorldPosition) continue;
                    if (WorldVec2.Distance(candidate, presence.ContinuousWorldPosition) < MinimumOpportunitySpacing)
                    {
                        separated = false;
                        break;
                    }
                }
                if (!separated) continue;
                position = candidate;
                return true;
            }
            return false;
        }

        static void Expire(SimulationWorld world, ulong day)
        {
            var expired = new List<string>();
            foreach (var instance in world.WorldOpportunities.ActiveInstances.Values)
                if (instance.ExpireDayIndexExclusive <= day) expired.Add(instance.InstanceId);
            for (var i = 0; i < expired.Count; i++)
            {
                if (!world.WorldOpportunities.ActiveInstances.TryGetValue(expired[i], out var instance)) continue;
                if (world.WorldOpportunities.TryGetSpec(instance.OpportunityDefinitionId, out var spec))
                    ContentOutcomeApplier.ApplyAll(world, instance.SpawnedEntityId, spec.ExpireOutcomes);
                RemoveEntityFromWorld(world, instance.SpawnedEntityId);
                world.WorldActivities.ResolveSource(
                    WorldActivitySourceKind.WorldOpportunity, instance.InstanceId, day);
                world.WorldOpportunities.RemoveInstance(instance.InstanceId);
            }
        }

        static void CleanupRemoved(SimulationWorld world, ulong day)
        {
            var stale = new List<string>();
            foreach (var instance in world.WorldOpportunities.ActiveInstances.Values)
                if (!world.Entities.TryGet(instance.SpawnedEntityId, out var entity) ||
                    (entity.TryGet<LifecycleComponent>(out var life) &&
                     (life.State == LifecycleState.Dead || life.State == LifecycleState.Removed)))
                    stale.Add(instance.InstanceId);
            for (var i = 0; i < stale.Count; i++)
            {
                world.WorldActivities.ResolveSource(
                    WorldActivitySourceKind.WorldOpportunity, stale[i], day);
                world.WorldOpportunities.RemoveInstance(stale[i]);
            }
        }

        static void RemoveEntityFromWorld(SimulationWorld world, EntityId entityId)
        {
            world.BackgroundCharacterTravel.Remove(entityId);
            world.WorldPresence.Remove(entityId);
            if (world.Entities.TryGet(entityId, out var entity) && entity.TryGet<EntityLocationComponent>(out var location))
                location.ClearPresence();
            if (world.Entities.TryGet(entityId, out var existing) &&
                existing.TryGet<LifecycleComponent>(out var life) && life.State != LifecycleState.Removed)
                world.Entities.MarkRemoved(entityId);
        }
    }
}
