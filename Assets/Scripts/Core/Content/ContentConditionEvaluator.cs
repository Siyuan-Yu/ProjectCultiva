using System;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Labor;
using XianXia.Core.Opportunity;
using XianXia.Core.Settlement;
using XianXia.Core.Simulation;

namespace XianXia.Core.Content
{
    public static class ContentConditionEvaluator
    {
        public static bool AllPass(
            SimulationWorld world,
            EntityId subject,
            System.Collections.Generic.IReadOnlyList<ContentCondition> conditions)
        {
            if (conditions == null || conditions.Count == 0)
                return true;
            for (var i = 0; i < conditions.Count; i++)
            {
                if (!Pass(world, subject, conditions[i]))
                    return false;
            }

            return true;
        }

        public static bool Pass(SimulationWorld world, EntityId subject, ContentCondition c)
        {
            if (world == null || c == null || string.IsNullOrEmpty(c.Kind))
                return false;

            switch (c.Kind.Trim().ToLowerInvariant())
            {
                case "atlocation":
                    return world.Entities.TryGet(subject, out var eLoc) &&
                           eLoc.TryGet<EntityLocationComponent>(out var loc) &&
                           string.Equals(loc.LocationId, c.Id, StringComparison.Ordinal);
                case "hasflag":
                case "storyflag":
                    return world.Flags.Has(c.Id);
                case "missingflag":
                case "missingstoryflag":
                    return !world.Flags.Has(c.Id);
                case "realmatleast":
                    return world.Entities.TryGet(subject, out var eRealm) &&
                           eRealm.TryGet<CultivationComponent>(out var cult) &&
                           RealmAtLeast(cult.Realm, c.Realm);
                case "knowssite":
                    return world.Entities.TryGet(subject, out var eSite) &&
                           eSite.TryGet<KnownSitesComponent>(out var known) &&
                           known.Knows(c.Id);
                case "stockatleast":
                    return world.Inventory.GetCount(c.Id) >= c.Amount;
                case "questactive":
                    return world.Quests.TryGet(c.Id, out var qa) && qa.Status == QuestStatus.Active;
                case "questcompleted":
                    // 目标达成（待领奖）即可解锁后续；领奖与否不挡剧情链。
                    return world.Quests.TryGet(c.Id, out var qc) &&
                           QuestStatusUtil.IsObjectivesDone(qc.Status);
                case "exploredlocation":
                    return world.Flags.Has(ExploredFlag(c.Id));
                case "hasmanual":
                    return world.Entities.TryGet(subject, out var eMan) &&
                           eMan.TryGet<CultivationComponent>(out var man) &&
                           man.HasLearnedManual &&
                           man.LearnedManualId.HasValue &&
                           string.Equals(man.LearnedManualId.Value.ToString(), c.Id, StringComparison.Ordinal);
                case "laboratlocation":
                    // amount = wall seconds at default Host tick pace (3s/tick).
                    return world.LocationLabor.MeetsSeconds(
                        string.IsNullOrEmpty(c.CharacterId) ? ResolveSubjectDefId(world, subject) : c.CharacterId,
                        c.Id,
                        c.Amount > 0 ? c.Amount : 3);
                case "uniquelaboratlocation":
                    // amount = required unique Character entities; each must labor ~3s at location.
                    return CountUniqueLaborersAtLocation(world, c.Id, UniqueLaborSeconds(c)) >=
                           (c.Amount > 0 ? c.Amount : 1);
                case "uniqueharvestatlocation":
                    // amount = required unique Characters who each harvested ≥1 at location.
                    return CountUniqueHarvestersAtLocation(world, c.Id) >= (c.Amount > 0 ? c.Amount : 1);
                case "characteratlocation":
                    return TryFindByDefinitionId(
                               world,
                               string.IsNullOrEmpty(c.CharacterId)
                                   ? ResolveSubjectDefId(world, subject)
                                   : c.CharacterId,
                               out var eChar) &&
                           eChar.TryGet<EntityLocationComponent>(out var charLoc) &&
                           string.Equals(charLoc.LocationId, c.Id, StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        public static string ExploredFlag(string locationId) => "explored:" + (locationId ?? string.Empty);

        /// <summary>Seconds each unique laborer must work for <c>uniqueLaborAtLocation</c>（约 1 拍）。</summary>
        public const int DefaultUniqueLaborSeconds = 3;

        public static int UniqueLaborSeconds(ContentCondition c) =>
            DefaultUniqueLaborSeconds;

        public static int CountUniqueLaborersAtLocation(
            SimulationWorld world,
            string locationId,
            int seconds)
        {
            if (world == null || string.IsNullOrEmpty(locationId))
                return 0;
            var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            var count = 0;
            foreach (var e in world.Entities.All)
            {
                if (e == null || (e.Tags & XianXia.Core.Entities.EntityTag.Character) == 0)
                    continue;
                var def = e.DefinitionId.ToString();
                if (!seen.Add(def))
                    continue;
                if (world.LocationLabor.MeetsSeconds(def, locationId, seconds))
                    count++;
            }

            return count;
        }

        public static int CountUniqueHarvestersAtLocation(SimulationWorld world, string locationId)
        {
            if (world == null || string.IsNullOrEmpty(locationId))
                return 0;
            var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            var count = 0;
            foreach (var e in world.Entities.All)
            {
                if (e == null || (e.Tags & XianXia.Core.Entities.EntityTag.Character) == 0)
                    continue;
                var def = e.DefinitionId.ToString();
                if (!seen.Add(def))
                    continue;
                if (world.LocationLabor.HasHarvested(def, locationId, 1))
                    count++;
            }

            return count;
        }

        static string ResolveSubjectDefId(SimulationWorld world, EntityId subject)
        {
            if (world.Entities.TryGet(subject, out var e))
                return e.DefinitionId.ToString();
            return string.Empty;
        }

        static bool TryFindByDefinitionId(SimulationWorld world, string definitionId, out XianXia.Core.Entities.Entity entity)
        {
            entity = null;
            if (world == null || string.IsNullOrEmpty(definitionId))
                return false;
            foreach (var e in world.Entities.All)
            {
                if (e == null)
                    continue;
                if (!string.Equals(e.DefinitionId.ToString(), definitionId, StringComparison.Ordinal))
                    continue;
                entity = e;
                return true;
            }

            return false;
        }

        static bool RealmAtLeast(RealmStage current, string required)
        {
            if (string.IsNullOrEmpty(required))
                return true;
            if (!TryParseRealm(required, out var need))
                return false;
            return (int)current >= (int)need;
        }

        static bool TryParseRealm(string text, out RealmStage realm)
        {
            if (string.Equals(text, "凡人", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "Mortal", StringComparison.OrdinalIgnoreCase))
            {
                realm = RealmStage.Mortal;
                return true;
            }

            if (string.Equals(text, "炼气", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "QiRefining", StringComparison.OrdinalIgnoreCase))
            {
                realm = RealmStage.QiRefining;
                return true;
            }

            return Enum.TryParse(text, true, out realm);
        }
    }
}
