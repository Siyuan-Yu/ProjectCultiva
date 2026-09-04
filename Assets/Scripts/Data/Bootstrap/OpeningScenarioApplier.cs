using System;
using System.Collections.Generic;
using System.Diagnostics;
using XianXia.Core.Concealment;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Labor;
using XianXia.Core.Opportunity;
using XianXia.Core.Results;
using XianXia.Core.Npc;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>
    /// Applies openingScenario post-spawn wiring (faction／schedule／daily／relations).
    /// Content → Core services only; no gameplay formulas.
    /// </summary>
    public static class OpeningScenarioApplier
    {
        public static Result Apply(
            SimulationWorld world,
            DefinitionRegistry registry,
            OpeningScenarioDefinition scenario,
            GameStartLookup lookup,
            int dailyRequiredAmount,
            System.Collections.Generic.IList<OpeningSpawnEntry> spawnEntries = null)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (scenario == null)
                return Result.Failure(ErrorCode.InvalidArgument, "OpeningScenarioDefinition is null.");
            if (lookup == null)
                return Result.Failure(ErrorCode.InvalidArgument, "GameStartLookup is null.");

            var scheduleId = string.IsNullOrWhiteSpace(scenario.ScheduleId)
                ? PlayableDayBootstrap.DefaultScheduleId
                : scenario.ScheduleId;
            var entries = spawnEntries ?? scenario.Spawns;
            foreach (var entry in entries)
            {
                if (!lookup.TryGetEntity(entry.DefinitionId, out var entityId))
                {
                    return Result.Failure(
                        ErrorCode.NotFound,
                        "Scenario spawn entity missing after GameStart.",
                        entry.DefinitionId);
                }

                if (!world.Entities.TryGet(entityId, out var entity))
                {
                    return Result.Failure(
                        ErrorCode.EntityNotFound,
                        "Spawned entity missing after bootstrap.",
                        entityId.ToString());
                }

                var boundSchedule = !string.IsNullOrWhiteSpace(entry.ScheduleId)
                    ? entry.ScheduleId
                    : scheduleId;
                if (entry.BindSchedule && !entity.TryGet<ScheduleComponent>(out _))
                    entity.AddComponent(new ScheduleComponent(boundSchedule));

                if (entry.BindDailyTask)
                    EnsurePlayableExtras(entity, dailyRequiredAmount);

                var membership = ApplyFactionMembership(entity, registry, scenario, entry);
                if (membership.IsFailure)
                    return membership;

                ApplyAiRole(world, entity, entry.AiRole);
                // Profession jobs removed: WorkArea resolve is global. Keep optional legacy jobId.
                if (!string.IsNullOrWhiteSpace(entry.JobId))
                    ApplyJob(world, entity, entry.JobId);
                else if (!entity.TryGet<JobComponent>(out _))
                    entity.AddComponent(new JobComponent());
            }

            var relations = SeedOpeningRelations(world, scenario, lookup);
            if (relations.IsFailure)
                return relations;

            return Result.Success();
        }

        public static EntityId FindFirstRecruitable(
            OpeningScenarioDefinition scenario,
            GameStartLookup lookup,
            System.Collections.Generic.IList<OpeningSpawnEntry> spawnEntries = null)
        {
            if (lookup == null)
                return EntityId.None;

            var entries = spawnEntries ?? scenario?.Spawns;
            if (entries == null)
                return EntityId.None;

            foreach (var entry in entries)
            {
                if (!entry.Recruitable)
                    continue;
                if (lookup.TryGetEntity(entry.DefinitionId, out var id))
                    return id;
            }

            return EntityId.None;
        }

        static Result SeedOpeningRelations(
            SimulationWorld world,
            OpeningScenarioDefinition scenario,
            GameStartLookup lookup)
        {
            if (scenario.OpeningRelations == null || scenario.OpeningRelations.Count == 0)
                return Result.Success();

            var service = new RelationshipService();
            foreach (var edge in scenario.OpeningRelations)
            {
                if (edge == null)
                    continue;
                if (!lookup.TryGetEntity(edge.FromDefinitionId, out var from) ||
                    !lookup.TryGetEntity(edge.ToDefinitionId, out var to))
                {
                    return Result.Failure(
                        ErrorCode.NotFound,
                        "Opening relation endpoint missing.",
                        (edge.FromDefinitionId ?? "?") + "→" + (edge.ToDefinitionId ?? "?"));
                }

                var reason = string.IsNullOrWhiteSpace(edge.ReasonTag)
                    ? SocialAlphaConstants.ReasonOpeningCompanion
                    : edge.ReasonTag;

                var ab = service.Record(world, from, to, edge.Delta, reason);
                if (ab.IsFailure)
                    return ab;

                if (edge.Mutual)
                {
                    var ba = service.Record(world, to, from, edge.Delta, reason);
                    if (ba.IsFailure)
                        return ba;
                }
            }

            return Result.Success();
        }

        static void EnsurePlayableExtras(Entity entity, int dailyRequired)
        {
            if (!entity.TryGet<DailyTaskComponent>(out var daily))
                entity.AddComponent(new DailyTaskComponent { RequiredAmount = dailyRequired });
            else
                daily.RequiredAmount = dailyRequired;

            if (!entity.TryGet<KnownSitesComponent>(out _))
                entity.AddComponent(new KnownSitesComponent());

            if (!entity.TryGet<PersonalConcealmentRiskComponent>(out _))
                entity.AddComponent(new PersonalConcealmentRiskComponent());
        }

        static bool TryParseFactionRole(string text, out FactionRoleKind role)
        {
            return OpeningFactionAssignmentResolver.TryParseFactionRole(text, out role);
        }

        /// <summary>
        /// 统一走 OpeningFactionAssignmentResolver：Override / Unaffiliated / CharacterDefault / Legacy。
        /// CharacterDefault 需要 entry.DefinitionId 对应的 CharacterDefinition 才能取人物默认。
        /// </summary>
        static Result ApplyFactionMembership(
            Entity entity,
            DefinitionRegistry registry,
            OpeningScenarioDefinition scenario,
            OpeningSpawnEntry entry)
        {
            var character = ResolveCharacter(registry, entry?.DefinitionId);

            ResolvedFactionAssignment resolved;
            try
            {
                resolved = OpeningFactionAssignmentResolver.Resolve(
                    entry,
                    character,
                    scenario?.OpeningFactionId);
            }
            catch (InvalidOperationException ex)
            {
                return Result.Failure(ErrorCode.InvalidArgument, ex.Message);
            }

            if (resolved.IsAffiliated)
            {
                AssignMembership(entity, resolved.FactionId, resolved.Role);
                TraceOpeningFaction(entity, entry, resolved);
                return Result.Success();
            }

            // 明确 Unaffiliated：清掉已有 membership；CharacterDefault 且无 default → 保持无势力。
            if (resolved.Source == FactionAssignmentSource.ExplicitUnaffiliated &&
                entity.TryGet<FactionMembershipComponent>(out var existing))
            {
                existing.ClearMembership();
            }

            TraceOpeningFaction(entity, entry, resolved);

            return Result.Success();
        }

        /// <summary>仅在开局 Spawn 时记录一次归属来源；绝不参与运行时势力判定。</summary>
        static void TraceOpeningFaction(
            Entity entity,
            OpeningSpawnEntry entry,
            ResolvedFactionAssignment resolved)
        {
#if DEBUG || UNITY_EDITOR || DEVELOPMENT_BUILD
            var source = resolved.Source == FactionAssignmentSource.ScenarioOverride
                ? "ScenarioOverride"
                : resolved.Source == FactionAssignmentSource.ExplicitUnaffiliated
                    ? "ExplicitUnaffiliated"
                    : resolved.Source.ToString();
            var displayName = entity == null || string.IsNullOrEmpty(entity.DisplayName)
                ? entry?.DefinitionId ?? "?"
                : entity.DisplayName;
            Trace.TraceInformation(
                "[OpeningFaction] Character=" + displayName +
                " Definition=" + (entry?.DefinitionId ?? "?") +
                " Source=" + source +
                " Faction=" + (resolved.IsAffiliated ? resolved.FactionId : "无") +
                " Role=" + (resolved.IsAffiliated ? resolved.Role.ToString() : "无") +
                " EntityId=" + (entity == null ? "?" : entity.Id.ToString()));
#endif
        }

        static CharacterDefinition ResolveCharacter(DefinitionRegistry registry, string definitionIdText)
        {
            if (registry == null || string.IsNullOrWhiteSpace(definitionIdText))
                return null;
            if (!DefinitionId.TryParse(definitionIdText.Trim(), out var id))
                return null;
            registry.TryGetCharacter(id, out var character);
            return character;
        }

        static void AssignMembership(Entity entity, string factionId, FactionRoleKind role)
        {
            if (!entity.TryGet<FactionMembershipComponent>(out var membership))
                entity.AddComponent(membership = new FactionMembershipComponent());
            membership.Assign(factionId, role);
        }

        static void ApplyJob(SimulationWorld world, Entity entity, string jobIdText)
        {
            if (string.IsNullOrWhiteSpace(jobIdText) || entity == null)
                return;
            if (world != null && !world.TryGetJob(jobIdText.Trim(), out _))
                return;

            if (!entity.TryGet<JobComponent>(out var job))
            {
                job = new JobComponent();
                var added = entity.AddComponent(job);
                if (added.IsFailure)
                    return;
            }

            job.Assign(jobIdText.Trim());
        }

        static void ApplyAiRole(SimulationWorld world, Entity entity, string aiRoleText)
        {
            if (string.IsNullOrWhiteSpace(aiRoleText))
                return;
            if (!Enum.TryParse(aiRoleText.Trim(), true, out NpcAiRoleKind role) || role == NpcAiRoleKind.None)
                return;

            if (!entity.TryGet<NpcAiRoleComponent>(out var ai))
            {
                ai = new NpcAiRoleComponent();
                var added = entity.AddComponent(ai);
                if (added.IsFailure)
                    return;
            }

            ai.Set(role);

            // 修士 NPC 可预知可修炼机缘点，但不得开局静默学会 offeredManual（青云诀等）。
            if (role != NpcAiRoleKind.Cultivator || world == null)
                return;

            if (!entity.TryGet<KnownSitesComponent>(out var known))
            {
                known = new KnownSitesComponent();
                entity.AddComponent(known);
            }

            foreach (var siteKv in world.OpportunitySites)
            {
                var site = siteKv.Value;
                if (site == null || !site.AllowsCultivation)
                    continue;
                known.Discover(site.Id);
                break;
            }
        }
    }

    /// <summary>DefinitionId text → spawned EntityId map from GameStart.</summary>
    public sealed class GameStartLookup
    {
        readonly System.Collections.Generic.IReadOnlyDictionary<string, EntityId> _byDefinition;

        public GameStartLookup(System.Collections.Generic.IReadOnlyDictionary<string, EntityId> byDefinition)
        {
            _byDefinition = byDefinition ??
                new System.Collections.Generic.Dictionary<string, EntityId>(StringComparer.Ordinal);
        }

        public bool TryGetEntity(string definitionIdText, out EntityId entityId)
        {
            entityId = EntityId.None;
            if (string.IsNullOrWhiteSpace(definitionIdText) || _byDefinition == null)
                return false;
            return _byDefinition.TryGetValue(definitionIdText, out entityId);
        }
    }
}
