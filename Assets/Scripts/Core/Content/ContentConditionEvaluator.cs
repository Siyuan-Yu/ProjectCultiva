using System;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
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
                    return world.Flags.Has(c.Id);
                case "missingflag":
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
                    return world.Settlements.TryGetPrimary(out var settlement) &&
                           settlement.GetStock(c.Id) >= c.Amount;
                case "questactive":
                    return world.Quests.TryGet(c.Id, out var qa) && qa.Status == QuestStatus.Active;
                case "questcompleted":
                    return world.Quests.TryGet(c.Id, out var qc) && qc.Status == QuestStatus.Completed;
                case "exploredlocation":
                    return world.Flags.Has(ExploredFlag(c.Id));
                case "hasmanual":
                    return world.Entities.TryGet(subject, out var eMan) &&
                           eMan.TryGet<CultivationComponent>(out var man) &&
                           man.HasLearnedManual &&
                           man.LearnedManualId.HasValue &&
                           string.Equals(man.LearnedManualId.Value.ToString(), c.Id, StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        public static string ExploredFlag(string locationId) => "explored:" + (locationId ?? string.Empty);

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
