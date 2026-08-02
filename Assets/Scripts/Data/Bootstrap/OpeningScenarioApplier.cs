using System;
using XianXia.Core.Concealment;
using XianXia.Core.Cultivation;
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
            OpeningScenarioDefinition scenario,
            GameStartLookup lookup,
            int dailyRequiredAmount)
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
            var factionId = string.IsNullOrWhiteSpace(scenario.OpeningFactionId)
                ? SocialAlphaConstants.OpeningFactionId
                : scenario.OpeningFactionId;

            foreach (var entry in scenario.Spawns)
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

                if (entry.AssignOpeningFaction)
                {
                    if (!TryParseFactionRole(entry.FactionRole, out var role))
                    {
                        return Result.Failure(
                            ErrorCode.InvalidArgument,
                            "Unknown or empty factionRole for assignOpeningFaction spawn.",
                            entry.DefinitionId + ":" + entry.FactionRole);
                    }

                    if (!entity.TryGet<FactionMembershipComponent>(out var mem))
                        entity.AddComponent(mem = new FactionMembershipComponent());
                    mem.Assign(factionId, role);
                }

                ApplyAiRole(world, entity, entry.AiRole);
                ApplyJob(world, entity, entry.JobId);
            }

            var relations = SeedOpeningRelations(world, scenario, lookup);
            if (relations.IsFailure)
                return relations;

            return Result.Success();
        }

        public static EntityId FindFirstRecruitable(
            OpeningScenarioDefinition scenario,
            GameStartLookup lookup)
        {
            if (scenario == null || lookup == null)
                return EntityId.None;

            foreach (var entry in scenario.Spawns)
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
            if (string.IsNullOrWhiteSpace(text))
            {
                role = FactionRoleKind.None;
                return false;
            }

            return Enum.TryParse(text.Trim(), ignoreCase: true, out role) && role != FactionRoleKind.None;
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
                if (site.OfferedManualId.HasValue &&
                    world.TryGetManual(site.OfferedManualId.Value, out var manual) &&
                    entity.TryGet<CultivationComponent>(out _))
                {
                    new CultivationService().LearnManual(world, entity.Id, manual);
                }

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
