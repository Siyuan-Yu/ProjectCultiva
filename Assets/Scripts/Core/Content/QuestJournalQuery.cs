using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;

namespace XianXia.Core.Content
{
    public enum QuestJournalTab
    {
        Available = 0,
        Active = 1,
        Finished = 2
    }

    public enum QuestListKind
    {
        Available = 0,
        Locked = 1,
        Active = 2,
        ReadyToClaim = 3,
        Completed = 4,
        Failed = 5
    }

    public sealed class QuestListEntry
    {
        public string QuestId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public QuestListKind Kind { get; set; }
        public QuestStatus Status { get; set; }
        public int ProgressCount { get; set; }
        public int ProgressMax { get; set; }
        public string ProgressLabel { get; set; } = string.Empty;
        public bool CanAccept { get; set; }
        public bool CanClaim { get; set; }
        public bool CanAbandon { get; set; }
        public string LockReason { get; set; } = string.Empty;
        public string RewardsSummary { get; set; } = string.Empty;
        public string ObjectivesSummary { get; set; } = string.Empty;
    }

    /// <summary>任务日志列表投影（UI 只读查询）。</summary>
    public static class QuestJournalQuery
    {
        public static bool HasUnclaimedRewards(SimulationWorld world)
        {
            if (world?.Quests?.Runtime == null)
                return false;
            foreach (var kv in world.Quests.Runtime)
            {
                if (kv.Value.Status == QuestStatus.ReadyToClaim)
                    return true;
            }

            return false;
        }

        public static void Collect(
            SimulationWorld world,
            EntityId subject,
            List<QuestListEntry> into)
        {
            into.Clear();
            if (world?.Quests?.Specs == null)
                return;

            foreach (var kv in world.Quests.Specs)
            {
                var spec = kv.Value;
                if (spec == null || string.IsNullOrEmpty(spec.Id))
                    continue;
                if (!world.Quests.TryGet(spec.Id, out var runtime))
                    continue;

                var entry = BuildEntry(world, subject, spec, runtime);
                if (entry != null)
                    into.Add(entry);
            }

            into.Sort(CompareEntries);
        }

        static QuestListEntry BuildEntry(
            SimulationWorld world,
            EntityId subject,
            QuestSpec spec,
            QuestRuntime runtime)
        {
            var entry = new QuestListEntry
            {
                QuestId = spec.Id,
                Name = string.IsNullOrEmpty(spec.Name) ? spec.Id : spec.Name,
                Description = spec.Description ?? string.Empty,
                Status = runtime.Status,
                ProgressCount = runtime.ProgressCount,
                ProgressMax = runtime.ProgressMax,
                RewardsSummary = SummarizeOutcomes(spec.Rewards),
                ObjectivesSummary = SummarizeObjectivesLive(world, spec.CompleteConditions, runtime)
            };

            if (entry.ProgressMax > 0)
                entry.ProgressLabel = entry.ProgressCount + "/" + entry.ProgressMax;

            switch (runtime.Status)
            {
                case QuestStatus.Active:
                    entry.Kind = QuestListKind.Active;
                    entry.CanAbandon = spec.Abandonable;
                    break;
                case QuestStatus.ReadyToClaim:
                    entry.Kind = QuestListKind.ReadyToClaim;
                    entry.CanClaim = true;
                    entry.CanAbandon = spec.Abandonable;
                    break;
                case QuestStatus.Completed:
                    entry.Kind = QuestListKind.Completed;
                    break;
                case QuestStatus.Failed:
                    entry.Kind = QuestListKind.Failed;
                    break;
                default:
                {
                    var offerOk = ContentConditionEvaluator.AllPass(world, subject, spec.OfferConditions);
                    if (offerOk)
                    {
                        entry.Kind = QuestListKind.Available;
                        entry.CanAccept = true;
                    }
                    else
                    {
                        entry.Kind = QuestListKind.Locked;
                        entry.LockReason = DescribeLockReason(world, subject, spec.OfferConditions);
                    }

                    break;
                }
            }

            return entry;
        }

        static int CompareEntries(QuestListEntry a, QuestListEntry b)
        {
            var ka = KindOrder(a.Kind);
            var kb = KindOrder(b.Kind);
            if (ka != kb)
                return ka.CompareTo(kb);
            return string.CompareOrdinal(a.Name, b.Name);
        }

        static int KindOrder(QuestListKind k)
        {
            switch (k)
            {
                case QuestListKind.ReadyToClaim: return 0;
                case QuestListKind.Available: return 1;
                case QuestListKind.Active: return 2;
                case QuestListKind.Locked: return 3;
                case QuestListKind.Completed: return 4;
                default: return 5;
            }
        }

        public static string DescribeLockReason(
            SimulationWorld world,
            EntityId subject,
            IReadOnlyList<ContentCondition> conditions)
        {
            if (conditions == null || conditions.Count == 0)
                return "暂不可接";

            for (var i = 0; i < conditions.Count; i++)
            {
                var c = conditions[i];
                if (ContentConditionEvaluator.Pass(world, subject, c))
                    continue;
                return FormatCondition(c);
            }

            return "条件未满足";
        }

        static string FormatCondition(ContentCondition c)
        {
            if (c == null || string.IsNullOrEmpty(c.Kind))
                return "条件未满足";
            switch (c.Kind.Trim().ToLowerInvariant())
            {
                case "questcompleted":
                    return "需先完成：" + ShortId(c.Id);
                case "questactive":
                    return "需进行中：" + ShortId(c.Id);
                case "hasflag":
                case "storyflag":
                    return "需标记：" + ShortId(c.Id);
                case "missingflag":
                case "missingstoryflag":
                    return "需无标记：" + ShortId(c.Id);
                case "atlocation":
                    return "需在地点：" + ShortId(c.Id);
                case "exploredlocation":
                    return "需已探索：" + ShortId(c.Id);
                case "realmatleast":
                    return "需境界：" + (c.Realm ?? "?");
                case "stockatleast":
                    return "需库存 " + ShortId(c.Id) + " ≥ " + c.Amount;
                case "laboratlocation":
                    return ShortId(c.CharacterId) + " 在 " + ShortId(c.Id) + " 劳动 ≥" + c.Amount + "秒";
                case "uniquelaboratlocation":
                    return ShortId(c.Id) + " 不同角色劳动各约3秒 ×" + (c.Amount > 0 ? c.Amount : 1);
                case "characteratlocation":
                    return ShortId(c.CharacterId) + " 到达 " + ShortId(c.Id);
                default:
                    return c.Kind + (string.IsNullOrEmpty(c.Id) ? "" : " " + ShortId(c.Id));
            }
        }

        static string SummarizeObjectivesLive(
            SimulationWorld world,
            IReadOnlyList<ContentCondition> list,
            QuestRuntime runtime)
        {
            if (list == null || list.Count == 0)
                return "（无完成条件）";
            for (var i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c == null || string.IsNullOrEmpty(c.Kind))
                    continue;
                if (!string.Equals(c.Kind, "uniqueLaborAtLocation", System.StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(c.Kind, "uniqueHarvestAtLocation", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                var max = c.Amount > 0 ? c.Amount : 1;
                var cur = runtime != null ? runtime.ProgressCount : 0;
                if (world != null && (runtime == null || runtime.Status == QuestStatus.Active))
                {
                    cur = string.Equals(c.Kind, "uniqueHarvestAtLocation", System.StringComparison.OrdinalIgnoreCase)
                        ? ContentConditionEvaluator.CountUniqueHarvestersAtLocation(world, c.Id)
                        : ContentConditionEvaluator.CountUniqueLaborersAtLocation(
                            world, c.Id, ContentConditionEvaluator.UniqueLaborSeconds(c));
                    if (cur > max)
                        cur = max;
                }

                return string.Equals(c.Kind, "uniqueHarvestAtLocation", System.StringComparison.OrdinalIgnoreCase)
                    ? "农田采集 " + cur + "/" + max + "（每人采到1个，同一人不重复计）"
                    : "农田劳作 " + cur + "/" + max + "（每人约3秒，同一人不重复计）";
            }

            return SummarizeObjectives(list);
        }

        static string SummarizeObjectives(IReadOnlyList<ContentCondition> list)
        {
            if (list == null || list.Count == 0)
                return "（无完成条件）";
            var parts = new List<string>(list.Count);
            for (var i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c == null || string.IsNullOrEmpty(c.Kind))
                {
                    parts.Add("?");
                    continue;
                }

                switch (c.Kind.Trim().ToLowerInvariant())
                {
                    case "exploredlocation":
                        parts.Add("探索 " + ShortId(c.Id));
                        break;
                    case "atlocation":
                        parts.Add("抵达 " + ShortId(c.Id));
                        break;
                    case "questcompleted":
                        parts.Add("完成 " + ShortId(c.Id));
                        break;
                    case "hasflag":
                    case "storyflag":
                        parts.Add("标记 " + ShortId(c.Id));
                        break;
                    case "stockatleast":
                        parts.Add(ShortId(c.Id) + "≥" + c.Amount);
                        break;
                    case "realmatleast":
                        parts.Add("境界 " + (c.Realm ?? "?"));
                        break;
                    case "laboratlocation":
                        parts.Add(ShortId(c.CharacterId) + "@" + ShortId(c.Id) + "≥" + c.Amount + "秒");
                        break;
                    case "uniquelaboratlocation":
                        parts.Add(ShortId(c.Id) + " 劳作人数≥" + c.Amount);
                        break;
                    case "uniqueharvestatlocation":
                        parts.Add(ShortId(c.Id) + " 采集人数≥" + c.Amount);
                        break;
                    case "characteratlocation":
                        parts.Add(ShortId(c.CharacterId) + "→" + ShortId(c.Id));
                        break;
                    default:
                        parts.Add(c.Kind + (string.IsNullOrEmpty(c.Id) ? "" : " " + ShortId(c.Id)));
                        break;
                }
            }

            return string.Join("；", parts);
        }

        static string SummarizeOutcomes(IReadOnlyList<ContentOutcome> list)
        {
            if (list == null || list.Count == 0)
                return "（无奖励）";
            var parts = new List<string>(list.Count);
            for (var i = 0; i < list.Count; i++)
            {
                var o = list[i];
                if (o == null) continue;
                var kind = string.IsNullOrEmpty(o.Kind) ? "?" : o.Kind;
                if (!string.IsNullOrEmpty(o.Id))
                    parts.Add(kind + " " + ShortId(o.Id) + (o.Amount != 0 ? " ×" + o.Amount : ""));
                else
                    parts.Add(kind + (o.Amount != 0 ? " ×" + o.Amount : ""));
            }

            return parts.Count == 0 ? "（无奖励）" : string.Join("；", parts);
        }

        static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "-";
            var i = id.LastIndexOf('_');
            if (i >= 0 && i + 1 < id.Length)
                return id.Substring(i + 1);
            i = id.IndexOf(':');
            return i >= 0 && i + 1 < id.Length ? id.Substring(i + 1) : id;
        }
    }
}
