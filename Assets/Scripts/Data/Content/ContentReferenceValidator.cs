using System;
using System.Collections.Generic;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Cross-reference check for chapter production packages (quest／event／flag／npc／location…).
    /// </summary>
    public sealed class ContentReferenceValidator
    {
        public ValidationReport Validate(DefinitionRegistry registry)
        {
            var report = new ValidationReport();
            if (registry == null)
            {
                report.Add(ErrorCode.InvalidArgument, "DefinitionRegistry is null.");
                return report;
            }

            var locations = CollectLocationIds(registry);
            var producedFlags = new HashSet<string>(StringComparer.Ordinal);
            var consumedFlags = new HashSet<string>(StringComparer.Ordinal);

            ValidateScenarios(registry, report);
            ValidateWorldRegions(registry, locations, report);
            ValidateQuests(registry, locations, producedFlags, consumedFlags, report);
            ValidateContentEvents(registry, locations, producedFlags, consumedFlags, report);
            ValidateChapters(registry, locations, producedFlags, consumedFlags, report);
            ValidateFlagConsumers(producedFlags, consumedFlags, report);

            return report;
        }

        static HashSet<string> CollectLocationIds(DefinitionRegistry registry)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in registry.WorldRegions)
            {
                var locs = kv.Value.Locations;
                if (locs == null)
                    continue;
                for (var i = 0; i < locs.Count; i++)
                {
                    if (!string.IsNullOrEmpty(locs[i].Id))
                        set.Add(locs[i].Id);
                }
            }

            return set;
        }

        void ValidateScenarios(DefinitionRegistry registry, ValidationReport report)
        {
            foreach (var kv in registry.OpeningScenarios)
            {
                var s = kv.Value;
                var ctx = s.Id.ToString();
                RequireDef(registry, s.OpeningSettlementId, "settlement", ctx + ".openingSettlementId", report);
                RequireDef(registry, s.OpeningWorldRegionId, "worldRegion", ctx + ".openingWorldRegionId", report);
                RequireDef(registry, s.OpeningChapterId, "chapter", ctx + ".openingChapterId", report);

                if (s.Spawns == null)
                    continue;
                for (var i = 0; i < s.Spawns.Count; i++)
                {
                    var spawn = s.Spawns[i];
                    RequireDef(registry, spawn.DefinitionId, "character", ctx + ".spawn[" + i + "]", report);
                    if (!string.IsNullOrWhiteSpace(spawn.JobId))
                        RequireDef(registry, spawn.JobId, "job", ctx + ".spawn[" + i + "].jobId", report);
                }

                if (s.OpeningRelations == null)
                    continue;
                for (var i = 0; i < s.OpeningRelations.Count; i++)
                {
                    var e = s.OpeningRelations[i];
                    RequireDef(registry, e.FromDefinitionId, "character", ctx + ".relation.from", report);
                    RequireDef(registry, e.ToDefinitionId, "character", ctx + ".relation.to", report);
                }
            }
        }

        void ValidateWorldRegions(
            DefinitionRegistry registry,
            HashSet<string> locations,
            ValidationReport report)
        {
            foreach (var kv in registry.WorldRegions)
            {
                var region = kv.Value;
                var ctx = region.Id.ToString();
                if (!string.IsNullOrEmpty(region.StartLocationId) && !locations.Contains(region.StartLocationId))
                    report.Add(ErrorCode.NotFound, "startLocationId missing in locations.", ctx + ":" + region.StartLocationId);

                if (region.Locations == null)
                    continue;
                for (var i = 0; i < region.Locations.Count; i++)
                {
                    var loc = region.Locations[i];
                    var lctx = ctx + "." + loc.Id;
                    if (loc.AdjacentIds != null)
                    {
                        for (var a = 0; a < loc.AdjacentIds.Count; a++)
                        {
                            if (!locations.Contains(loc.AdjacentIds[a]))
                                report.Add(ErrorCode.NotFound, "adjacent location missing.", lctx + "->" + loc.AdjacentIds[a]);
                        }
                    }

                    RequireDef(registry, loc.OpportunitySiteId, "opportunitySite", lctx + ".opportunitySiteId", report);
                    RequireDef(registry, loc.ResidentNpcDefinitionId, "character", lctx + ".residentNpc", report);
                    RequireDef(registry, loc.ResourceOnExploreId, "resource", lctx + ".resourceOnExploreId", report);
                    ScanConditions(loc.EnterConditions, registry, locations, null, null, lctx + ".enter", report);
                    if (loc.QuestOfferIds != null)
                    {
                        for (var q = 0; q < loc.QuestOfferIds.Count; q++)
                            RequireDef(registry, loc.QuestOfferIds[q], "quest", lctx + ".questOffer", report);
                    }
                }
            }
        }

        void ValidateQuests(
            DefinitionRegistry registry,
            HashSet<string> locations,
            HashSet<string> producedFlags,
            HashSet<string> consumedFlags,
            ValidationReport report)
        {
            foreach (var kv in registry.Quests)
            {
                var q = kv.Value;
                var ctx = q.Id.ToString();
                ScanConditions(q.OfferConditions, registry, locations, producedFlags, consumedFlags, ctx + ".offer", report);
                ScanConditions(q.CompleteConditions, registry, locations, producedFlags, consumedFlags, ctx + ".complete", report);
                ScanConditions(q.FailConditions, registry, locations, producedFlags, consumedFlags, ctx + ".fail", report);
                ScanOutcomes(q.Rewards, registry, locations, producedFlags, consumedFlags, ctx + ".reward", report);
                ScanOutcomes(q.FailResults, registry, locations, producedFlags, consumedFlags, ctx + ".failResult", report);
            }
        }

        void ValidateContentEvents(
            DefinitionRegistry registry,
            HashSet<string> locations,
            HashSet<string> producedFlags,
            HashSet<string> consumedFlags,
            ValidationReport report)
        {
            foreach (var kv in registry.ContentEvents)
            {
                var e = kv.Value;
                var ctx = e.Id.ToString();
                if (!string.IsNullOrEmpty(e.LocationId) && !locations.Contains(e.LocationId))
                    report.Add(ErrorCode.NotFound, "contentEvent.locationId missing.", ctx + ":" + e.LocationId);
                RequireDef(registry, e.QuestId, "quest", ctx + ".questId", report);
                ScanConditions(e.Conditions, registry, locations, producedFlags, consumedFlags, ctx + ".cond", report);
                if (e.Choices == null)
                    continue;
                for (var i = 0; i < e.Choices.Count; i++)
                {
                    var c = e.Choices[i];
                    var cctx = ctx + ".choice." + c.Id;
                    ScanConditions(c.Conditions, registry, locations, producedFlags, consumedFlags, cctx, report);
                    ScanOutcomes(c.Outcomes, registry, locations, producedFlags, consumedFlags, cctx, report);
                }
            }
        }

        void ValidateChapters(
            DefinitionRegistry registry,
            HashSet<string> locations,
            HashSet<string> producedFlags,
            HashSet<string> consumedFlags,
            ValidationReport report)
        {
            foreach (var kv in registry.Chapters)
            {
                var ch = kv.Value;
                var ctx = ch.Id.ToString();
                // openingScenarioId is documentary; warn only if set and missing
                if (!string.IsNullOrEmpty(ch.OpeningScenarioId))
                    RequireDef(registry, ch.OpeningScenarioId, "openingScenario", ctx + ".openingScenarioId", report);

                for (var i = 0; i < ch.QuestChainIds.Count; i++)
                    RequireDef(registry, ch.QuestChainIds[i], "quest", ctx + ".questChain", report);
                for (var i = 0; i < ch.EventChainIds.Count; i++)
                    RequireDef(registry, ch.EventChainIds[i], "contentEvent", ctx + ".eventChain", report);

                for (var b = 0; b < ch.DayBeats.Count; b++)
                {
                    var beat = ch.DayBeats[b];
                    var bctx = ctx + ".dayBeat[" + beat.DayIndex + "]";
                    ScanConditions(beat.Conditions, registry, locations, producedFlags, consumedFlags, bctx, report);
                    for (var i = 0; i < beat.QuestOfferIds.Count; i++)
                        RequireDef(registry, beat.QuestOfferIds[i], "quest", bctx + ".questOffer", report);
                    for (var i = 0; i < beat.ContentEventIds.Count; i++)
                        RequireDef(registry, beat.ContentEventIds[i], "contentEvent", bctx + ".event", report);
                    for (var i = 0; i < beat.SetFlags.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(beat.SetFlags[i]))
                            producedFlags.Add(beat.SetFlags[i]);
                    }
                }
            }
        }

        void ScanConditions(
            IList<ContentCondition> conditions,
            DefinitionRegistry registry,
            HashSet<string> locations,
            HashSet<string> producedFlags,
            HashSet<string> consumedFlags,
            string ctx,
            ValidationReport report)
        {
            if (conditions == null)
                return;
            for (var i = 0; i < conditions.Count; i++)
            {
                var c = conditions[i];
                if (c == null || string.IsNullOrEmpty(c.Kind))
                    continue;
                var kind = c.Kind.Trim().ToLowerInvariant();
                switch (kind)
                {
                    case "atlocation":
                    case "exploredlocation":
                        if (!string.IsNullOrEmpty(c.Id) && !locations.Contains(c.Id))
                            report.Add(ErrorCode.NotFound, "condition location missing.", ctx + ":" + c.Id);
                        break;
                    case "laboratlocation":
                    case "uniquelaboratlocation":
                    case "uniqueharvestatlocation":
                        if (!string.IsNullOrEmpty(c.Id) && !locations.Contains(c.Id))
                            report.Add(ErrorCode.NotFound, "condition location missing.", ctx + ":" + c.Id);
                        if (kind == "laboratlocation" && !string.IsNullOrEmpty(c.CharacterId))
                            RequireDef(registry, c.CharacterId, "character", ctx + "." + c.Kind, report);
                        break;
                    case "characteratlocation":
                        if (!string.IsNullOrEmpty(c.Id) && !locations.Contains(c.Id))
                            report.Add(ErrorCode.NotFound, "condition location missing.", ctx + ":" + c.Id);
                        if (!string.IsNullOrEmpty(c.CharacterId))
                            RequireDef(registry, c.CharacterId, "character", ctx + "." + c.Kind, report);
                        break;
                    case "knowssite":
                        RequireDef(registry, c.Id, "opportunitySite", ctx + ".knowsSite", report);
                        break;
                    case "stockatleast":
                        RequireDef(registry, c.Id, "resource", ctx + ".stock", report);
                        break;
                    case "questactive":
                    case "questcompleted":
                        RequireDef(registry, c.Id, "quest", ctx + ".quest", report);
                        break;
                    case "hasmanual":
                        RequireDef(registry, c.Id, "cultivation", ctx + ".manual", report);
                        break;
                    case "hasflag":
                    case "storyflag":
                    case "missingflag":
                    case "missingstoryflag":
                        if (!string.IsNullOrEmpty(c.Id) && consumedFlags != null)
                            consumedFlags.Add(c.Id);
                        break;
                }
            }
        }

        void ScanOutcomes(
            IList<ContentOutcome> outcomes,
            DefinitionRegistry registry,
            HashSet<string> locations,
            HashSet<string> producedFlags,
            HashSet<string> consumedFlags,
            string ctx,
            ValidationReport report)
        {
            if (outcomes == null)
                return;
            for (var i = 0; i < outcomes.Count; i++)
            {
                var o = outcomes[i];
                if (o == null || string.IsNullOrEmpty(o.Kind))
                    continue;
                var kind = o.Kind.Trim().ToLowerInvariant();
                switch (kind)
                {
                    case "setflag":
                    case "setstoryflag":
                        if (!string.IsNullOrEmpty(o.Id) && producedFlags != null)
                            producedFlags.Add(o.Id);
                        break;
                    case "clearflag":
                    case "clearstoryflag":
                        if (!string.IsNullOrEmpty(o.Id) && consumedFlags != null)
                            consumedFlags.Add(o.Id);
                        break;
                    case "addstock":
                        RequireDef(registry, o.Id, "resource", ctx + ".addStock", report);
                        break;
                    case "startquest":
                        RequireDef(registry, o.Id, "quest", ctx + ".startQuest", report);
                        break;
                    case "discoversite":
                        RequireDef(registry, o.Id, "opportunitySite", ctx + ".discoverSite", report);
                        break;
                    case "relationdelta":
                        RequireDef(registry, o.FromDefinitionId, "character", ctx + ".relation.from", report);
                        if (o.ToDefinitionIds.Count > 0)
                        {
                            for (var ti = 0; ti < o.ToDefinitionIds.Count; ti++)
                            {
                                var targetId = o.ToDefinitionIds[ti];
                                if (string.Equals(targetId, "@party", StringComparison.OrdinalIgnoreCase))
                                    continue;
                                RequireDef(registry, targetId, "character", ctx + ".relation.to", report);
                            }
                        }
                        else if (!string.IsNullOrEmpty(o.ToDefinitionId))
                        {
                            if (!string.Equals(o.ToDefinitionId, "@party", StringComparison.OrdinalIgnoreCase))
                                RequireDef(registry, o.ToDefinitionId, "character", ctx + ".relation.to", report);
                        }
                        else
                        {
                            report.Add(
                                ErrorCode.MissingRequiredField,
                                "relationDelta requires toDefinitionId or toDefinitionIds.",
                                ctx);
                        }

                        break;
                }
            }
        }

        static void ValidateFlagConsumers(
            HashSet<string> producedFlags,
            HashSet<string> consumedFlags,
            ValidationReport report)
        {
            foreach (var flag in consumedFlags)
            {
                if (IsRuntimeProducedFlag(flag))
                    continue;
                if (!producedFlags.Contains(flag))
                {
                    report.Add(
                        ErrorCode.NotFound,
                        "Flag consumed but never produced by content outcomes／dayBeats.",
                        flag);
                }
            }
        }

        static bool IsRuntimeProducedFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag))
                return true;
            // Core exploration writes explored:<locationId>
            if (flag.StartsWith("explored:", StringComparison.Ordinal))
                return true;
            // SupervisorPressureHandler writes this at day end.
            if (string.Equals(flag, "story:supervisor_pressure", StringComparison.Ordinal))
                return true;
            return false;
        }

        static void RequireDef(
            DefinitionRegistry registry,
            string idText,
            string expectedKind,
            string context,
            ValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(idText))
                return;
            if (!DefinitionId.TryParse(idText, out var id))
            {
                report.Add(ErrorCode.InvalidDefinitionId, "Invalid DefinitionId.", context + ":" + idText);
                return;
            }

            var ok = false;
            switch (expectedKind)
            {
                case "character":
                    ok = registry.Characters.ContainsKey(id);
                    break;
                case "quest":
                    ok = registry.Quests.ContainsKey(id);
                    break;
                case "contentEvent":
                    ok = registry.ContentEvents.ContainsKey(id);
                    break;
                case "chapter":
                    ok = registry.Chapters.ContainsKey(id);
                    break;
                case "openingScenario":
                    ok = registry.OpeningScenarios.ContainsKey(id);
                    break;
                case "worldRegion":
                    ok = registry.WorldRegions.ContainsKey(id);
                    break;
                case "settlement":
                    ok = registry.Settlements.ContainsKey(id);
                    break;
                case "job":
                    ok = registry.Jobs.ContainsKey(id);
                    break;
                case "workArea":
                    ok = registry.WorkAreas.ContainsKey(id);
                    break;
                case "resource":
                    ok = registry.Resources.ContainsKey(id);
                    break;
                case "opportunitySite":
                    ok = registry.OpportunitySites.ContainsKey(id);
                    break;
                case "cultivation":
                    ok = registry.Cultivations.ContainsKey(id);
                    break;
                case "facility":
                    ok = registry.Facilities.ContainsKey(id);
                    break;
                case "item":
                    ok = registry.Items.ContainsKey(id);
                    break;
            }

            if (!ok)
                report.Add(ErrorCode.NotFound, expectedKind + " reference missing.", context + ":" + idText);
        }
    }
}
