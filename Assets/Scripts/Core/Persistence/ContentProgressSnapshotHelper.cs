using System;
using System.Collections.Generic;
using XianXia.Core.Content;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Persistence
{
    public static class ContentProgressSnapshotHelper
    {
        public static ContentProgressSnapshotDto Capture(SimulationWorld world)
        {
            var dto = new ContentProgressSnapshotDto { HasAuthority = true };
            world.Flags.CaptureState(out var flags, out var history);
            flags.Sort(StringComparer.Ordinal);
            dto.Flags.AddRange(flags); dto.FlagHistory.AddRange(history);
            var questState = world.Quests.CaptureRuntime();
            dto.NextQuestInstanceSequence = questState.NextInstanceSequence;
            var questRuntime = questState.Runtime;
            var questIds = new List<string>(questRuntime.Keys);
            questIds.Sort(StringComparer.Ordinal);
            foreach (var questId in questIds)
            {
                var runtime = questRuntime[questId];
                dto.Quests.Add(new QuestRuntimeSnapshotDto
                {
                    QuestInstanceId = runtime.QuestInstanceId, QuestId = runtime.QuestId,
                    IssuerEntityId = runtime.IssuerEntityId.Value,
                    SourceOpportunityInstanceId = runtime.SourceOpportunityInstanceId,
                    IssuerDisplayName = runtime.IssuerDisplayName,
                    AcceptedByEntityId = runtime.AcceptedByEntityId.Value,
                    Status = (int)runtime.Status,
                    ProgressCount = runtime.ProgressCount, ProgressMax = runtime.ProgressMax,
                    AcceptedAtDayIndex = runtime.AcceptedAtDayIndex,
                    DeadlineDayIndexExclusive = runtime.DeadlineDayIndexExclusive,
                    DeliveryCompleted = runtime.DeliveryCompleted,
                    FailureReason = runtime.FailureReason
                });
            }
            var firedKeys = world.ContentEvents.CaptureFiredKeys();
            firedKeys.Sort(StringComparer.Ordinal);
            dto.FiredEventKeys.AddRange(firedKeys);
            world.Chapters.CaptureRuntime(out var chapterId, out var start, out var beats);
            beats.Sort(StringComparer.Ordinal);
            dto.Chapter.ActiveChapterId = chapterId; dto.Chapter.ChapterStartDayIndex = start;
            dto.Chapter.AppliedBeatKeys.AddRange(beats);
            AddSorted(world.ContentCounters.CaptureState(), dto.Counters);
            AddSorted(world.ContentDaily.CaptureState(), dto.DailyMarks);
            world.LocationLabor.CaptureState(out var ticks, out var harvests);
            AddSorted(ticks, dto.LaborTicks); AddSorted(harvests, dto.LaborHarvests);
            return dto;
        }

        public static Result Restore(SimulationWorld world, ContentProgressSnapshotDto dto)
        {
            if (dto == null || !dto.HasAuthority)
                return Result.Failure(ErrorCode.SnapshotInvalid, "Snapshot contentProgress authority is missing.");
            if (!UniqueStrings(dto.Flags, false) || !ValidStrings(dto.FlagHistory) ||
                !UniqueStrings(dto.FiredEventKeys, false))
                return Result.Failure(ErrorCode.SnapshotInvalid, "Invalid ContentProgress string collection.");
            var quests = new Dictionary<string, QuestRuntime>(StringComparer.Ordinal);
            foreach (var q in dto.Quests ?? new List<QuestRuntimeSnapshotDto>())
            {
                if (q == null || string.IsNullOrWhiteSpace(q.QuestInstanceId) || string.IsNullOrWhiteSpace(q.QuestId) ||
                    quests.ContainsKey(q.QuestInstanceId) ||
                    !Enum.IsDefined(typeof(QuestStatus), q.Status) || q.ProgressCount < 0 || q.ProgressMax < 0 ||
                    (q.ProgressMax > 0 && q.ProgressCount > q.ProgressMax))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Invalid or duplicate Quest runtime.", q?.QuestId);
                quests.Add(q.QuestInstanceId, new QuestRuntime { QuestInstanceId = q.QuestInstanceId,
                    QuestId = q.QuestId, IssuerEntityId = new XianXia.Core.Domain.Ids.EntityId(q.IssuerEntityId),
                    SourceOpportunityInstanceId = q.SourceOpportunityInstanceId ?? string.Empty,
                    IssuerDisplayName = q.IssuerDisplayName ?? string.Empty,
                    AcceptedByEntityId = new XianXia.Core.Domain.Ids.EntityId(q.AcceptedByEntityId),
                    Status = (QuestStatus)q.Status,
                    ProgressCount = q.ProgressCount, ProgressMax = q.ProgressMax,
                    AcceptedAtDayIndex = q.AcceptedAtDayIndex, DeadlineDayIndexExclusive = q.DeadlineDayIndexExclusive,
                    DeliveryCompleted = q.DeliveryCompleted, FailureReason = q.FailureReason ?? string.Empty });
            }
            if (!TryMap(dto.Counters, out var counters) || !TryMap(dto.DailyMarks, out var daily) ||
                !TryMap(dto.LaborTicks, out var ticks) || !TryMap(dto.LaborHarvests, out var harvests))
                return Result.Failure(ErrorCode.SnapshotInvalid, "Invalid ContentProgress key/value collection.");
            var chapter = dto.Chapter ?? new ChapterRuntimeSnapshotDto();
            var appliedBeatKeys = chapter.AppliedBeatKeys ?? new List<string>();
            if (!UniqueStrings(appliedBeatKeys, false) ||
                (string.IsNullOrEmpty(chapter.ActiveChapterId) && appliedBeatKeys.Count > 0))
                return Result.Failure(ErrorCode.SnapshotInvalid, "Invalid Chapter applied beat key.");
            world.Flags.RestoreState(dto.Flags, dto.FlagHistory);
            if (dto.NextQuestInstanceSequence == 0)
                return Result.Failure(ErrorCode.SnapshotInvalid, "Quest instance sequence is invalid.");
            world.Quests.RestoreRuntime(quests, dto.NextQuestInstanceSequence);
            world.ContentEvents.RestoreFiredKeys(dto.FiredEventKeys);
            world.Chapters.RestoreRuntime(chapter.ActiveChapterId, chapter.ChapterStartDayIndex, appliedBeatKeys);
            world.ContentCounters.RestoreState(counters); world.ContentDaily.RestoreState(daily);
            world.LocationLabor.RestoreState(ticks, harvests);
            return Result.Success();
        }

        public static Result ValidateDefinitions(SimulationWorld world)
        {
            var issuerTemplate = new HashSet<string>(StringComparer.Ordinal);
            ulong maxCommissionSequence = 0;
            foreach (var pair in world.Quests.Runtime)
            {
                var runtime = pair.Value;
                if (!string.Equals(pair.Key, runtime.QuestInstanceId, StringComparison.Ordinal) ||
                    !world.Quests.Specs.TryGetValue(runtime.QuestId, out var spec))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Restored Quest definition is missing.", runtime.QuestId);
                if (!spec.IsCharacterCommission)
                {
                    if (!string.Equals(runtime.QuestInstanceId, runtime.QuestId, StringComparison.Ordinal) || !runtime.IssuerEntityId.IsNone)
                        return Result.Failure(ErrorCode.SnapshotInvalid, "Fixed Quest identity is invalid.", runtime.QuestInstanceId);
                    continue;
                }
                if (runtime.IssuerEntityId.IsNone || string.IsNullOrWhiteSpace(runtime.IssuerDisplayName) ||
                    !issuerTemplate.Add(runtime.IssuerEntityId.Value + ":" + runtime.QuestId))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Commission issuer/template identity is invalid.", runtime.QuestInstanceId);
                const string prefix = "quest-instance:";
                if (!runtime.QuestInstanceId.StartsWith(prefix, StringComparison.Ordinal) ||
                    !ulong.TryParse(runtime.QuestInstanceId.Substring(prefix.Length), out var sequence) || sequence == 0)
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Commission instance id is invalid.", runtime.QuestInstanceId);
                if (sequence > maxCommissionSequence) maxCommissionSequence = sequence;
                if (runtime.DeliveryCompleted && runtime.Status != QuestStatus.ReadyToClaim && runtime.Status != QuestStatus.Completed)
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Delivered commission has invalid status.", runtime.QuestInstanceId);
                if (runtime.DeadlineDayIndexExclusive > 0 &&
                    runtime.DeadlineDayIndexExclusive <= runtime.AcceptedAtDayIndex)
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Commission deadline is invalid.", runtime.QuestInstanceId);
                if ((runtime.Status == QuestStatus.Active || runtime.Status == QuestStatus.Inactive) &&
                    (!world.Entities.TryGet(runtime.IssuerEntityId, out _) ||
                     (!string.IsNullOrEmpty(runtime.SourceOpportunityInstanceId) &&
                      (!world.WorldOpportunities.ActiveInstances.TryGetValue(runtime.SourceOpportunityInstanceId, out var source) ||
                       source.SpawnedEntityId != runtime.IssuerEntityId))))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Active commission source is missing or mismatched.", runtime.QuestInstanceId);
            }
            if (world.Quests.NextInstanceSequence <= maxCommissionSequence)
                return Result.Failure(ErrorCode.SnapshotInvalid, "Quest instance sequence would reuse an id.");
            ChapterSpec chapter = null;
            if (world.Chapters.HasActive && !world.Chapters.Specs.TryGetValue(world.Chapters.ActiveChapterId, out chapter))
                return Result.Failure(ErrorCode.SnapshotInvalid, "Restored active Chapter definition is missing.", world.Chapters.ActiveChapterId);
            if (world.Chapters.HasActive)
                foreach (var key in world.Chapters.AppliedBeatKeys)
                {
                    var prefix = world.Chapters.ActiveChapterId + ":";
                    if (!key.StartsWith(prefix, StringComparison.Ordinal) ||
                        !int.TryParse(key.Substring(prefix.Length), out var dayIndex) || dayIndex < 0 ||
                        !chapter.DayBeats.Exists(beat => beat.DayIndex == dayIndex))
                        return Result.Failure(ErrorCode.SnapshotInvalid, "Applied beat does not belong to active Chapter.", key);
                }
            foreach (var key in world.ContentEvents.CaptureFiredKeys())
            {
                var id = EventIdFromFiredKey(key);
                if (string.IsNullOrEmpty(id) || !world.ContentEvents.Specs.ContainsKey(id))
                    return Result.Failure(ErrorCode.SnapshotInvalid, "Restored fired Event definition is missing.", key);
            }
            return Result.Success();
        }

        static string EventIdFromFiredKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (!key.StartsWith("#target:", StringComparison.Ordinal) && !key.StartsWith("#pair:", StringComparison.Ordinal)) return key;
            var pair = key.StartsWith("#pair:", StringComparison.Ordinal);
            var first = key.IndexOf(':'); var second = key.IndexOf(':', first + 1);
            if (second < 0 || !int.TryParse(key.Substring(first + 1, second - first - 1), out var len) || len <= 0)
                return string.Empty;
            var idStart = second + 1;
            var idEnd = idStart + len;
            if (idEnd >= key.Length || key[idEnd] != ':') return string.Empty;
            var scope = key.Substring(idEnd + 1);
            if (string.IsNullOrEmpty(scope)) return string.Empty;
            if (pair)
            {
                var separator = scope.IndexOf(':');
                if (separator <= 0 || separator == scope.Length - 1 ||
                    !ulong.TryParse(scope.Substring(0, separator), out _)) return string.Empty;
            }
            return key.Substring(idStart, len);
        }
        static void AddSorted(IReadOnlyDictionary<string, int> source, List<ContentIntEntrySnapshotDto> target)
        {
            var keys = new List<string>(source.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var key in keys) target.Add(new ContentIntEntrySnapshotDto { Key = key, Value = source[key] });
        }
        static bool TryMap(IEnumerable<ContentIntEntrySnapshotDto> source, out Dictionary<string, int> map)
        {
            map = new Dictionary<string, int>(StringComparer.Ordinal);
            if (source == null) return true;
            foreach (var item in source)
                if (item == null || string.IsNullOrWhiteSpace(item.Key) || item.Value < 0 || map.ContainsKey(item.Key)) return false;
                else map.Add(item.Key, item.Value);
            return true;
        }
        static bool ValidStrings(IEnumerable<string> values)
        { if (values == null) return true; foreach (var value in values) if (string.IsNullOrWhiteSpace(value)) return false; return true; }
        static bool UniqueStrings(IEnumerable<string> values, bool allowEmpty)
        { var set = new HashSet<string>(StringComparer.Ordinal); if (values == null) return true; foreach (var value in values) if ((!allowEmpty && string.IsNullOrWhiteSpace(value)) || !set.Add(value)) return false; return true; }
    }
}
