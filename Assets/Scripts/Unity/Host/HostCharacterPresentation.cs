using System;
using System.Collections.Generic;
using XianXia.Core.Actions;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Npc;
using XianXia.Core.Schedule;
using XianXia.Core.Social;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Host 人物展示的只读汇总。Runtime Component 是当前状态真源；人物定义只补充静态作者信息。
    /// </summary>
    public sealed class HostCharacterPresentation
    {
        public Entity Entity { get; internal set; }
        public EntityId EntityId { get; internal set; }
        public CharacterDefinition Definition { get; internal set; }
        public string DisplayName { get; internal set; }
        public string DefinitionId { get; internal set; }
        public string FactionName { get; internal set; }
        public string FactionRole { get; internal set; }
        public string Location { get; internal set; }
        public string Schedule { get; internal set; }
        public string Activity { get; internal set; }
        public IReadOnlyList<string> PersonalityTags { get; internal set; }
        public IReadOnlyList<string> BackgroundTags { get; internal set; }
        public IReadOnlyList<string> TalentTags { get; internal set; }
    }

    public static class HostCharacterPresentationResolver
    {
        static readonly IReadOnlyList<string> EmptyTags = Array.Empty<string>();

        public static bool TryBuild(PlayableHostSession session, EntityId entityId, out HostCharacterPresentation info)
        {
            info = null;
            if (session?.World == null || entityId.IsNone ||
                !session.World.Entities.TryGet(entityId, out var entity))
                return false;

            CharacterDefinition definition = null;
            var definitionId = string.Empty;
            if (entity.TryGet<IdentityComponent>(out var identity))
            {
                definitionId = identity.DefinitionId.ToString();
                session.Registry?.TryGetCharacter(identity.DefinitionId, out definition);
            }

            var factionName = "无";
            var factionRole = "无";
            if (entity.TryGet<FactionMembershipComponent>(out var faction) && faction.IsAffiliated)
            {
                factionName = StrategicFactionCatalog.DisplayName(faction.FactionId);
                factionRole = FactionRoleDisplay(faction.Role);
            }

            var location = ResolveLocation(session, entityId, entity);
            var schedule = "无";
            if (entity.TryGet<ScheduleComponent>(out var scheduleComponent) &&
                !string.IsNullOrEmpty(scheduleComponent.DefinitionId))
                schedule = scheduleComponent.DefinitionId;

            info = new HostCharacterPresentation
            {
                Entity = entity,
                EntityId = entityId,
                Definition = definition,
                DisplayName = string.IsNullOrEmpty(entity.DisplayName) ? entityId.ToString() : entity.DisplayName,
                DefinitionId = definitionId,
                FactionName = factionName,
                FactionRole = factionRole,
                Location = location,
                Schedule = schedule,
                Activity = ResolveActivity(session, entity),
                PersonalityTags = definition?.PersonalityTags ?? EmptyTags,
                BackgroundTags = definition?.BackgroundTags ?? EmptyTags,
                TalentTags = definition?.TalentTags ?? EmptyTags
            };
            return true;
        }

        public static string FactionRoleDisplay(FactionRoleKind role)
        {
            switch (role)
            {
                case FactionRoleKind.LaborDisciple: return "劳役弟子";
                case FactionRoleKind.Member: return "成员";
                case FactionRoleKind.Supervisor: return "主管";
                default: return "无";
            }
        }

        static string ResolveLocation(PlayableHostSession session, EntityId id, Entity entity)
        {
            if (CharacterWorldPresenceQuery.TryDescribe(session.World, id, out _, out var siteId, out var hex, out var loaded))
            {
                if (!string.IsNullOrEmpty(siteId))
                    return StrategicSiteAccessService.DescribeSite(session.World, siteId) + (loaded ? " · 当前地图已加载" : string.Empty);
                return "荒野" + hex;
            }
            if (entity.TryGet<EntityLocationComponent>(out var location) && location.HasLocation &&
                session.World.WorldRegion.TryGet(location.LocationId, out var place))
                return string.IsNullOrEmpty(place.Name) ? place.Id : place.Name;
            return "未知";
        }

        static string ResolveActivity(PlayableHostSession session, Entity entity)
        {
            if (!entity.TryGet<ActionStateComponent>(out var state) || !state.HasActiveAction ||
                !session.World.ActiveActions.TryGetValue(state.ActiveActionId, out var action))
                return "待命";
            if (action is MoveAction) return "移动中";
            if (action is WorkAction work) return ActivityDisplay(work.Activity) + "中";
            if (action is LaborAction) return "工作中";
            if (action is CultivateAction) return "修炼中";
            if (action is RestAction) return "休息中";
            if (action is ObserveAction) return "观察中";
            return "行动中";
        }

        static string ActivityDisplay(ScheduleActivity activity)
        {
            switch (activity)
            {
                case ScheduleActivity.Labor: return "工作";
                case ScheduleActivity.Rest: return "休息";
                case ScheduleActivity.Eat: return "吃饭";
                case ScheduleActivity.Cultivate: return "修炼";
                case ScheduleActivity.Explore: return "探索";
                case ScheduleActivity.Patrol: return "巡视";
                case ScheduleActivity.Inspect: return "检查";
                case ScheduleActivity.Idle: return "待命";
                default: return "待命";
            }
        }
    }

    /// <summary>人物静态标签的中文呈现；未知标签保留原值，方便内容继续迭代。</summary>
    public static class HostCharacterTagPresentation
    {
        static readonly HashSet<string> WarnedUnknown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "personality_persistent", "坚毅" }, { "personality_cautious", "谨慎" },
            { "background_orphan", "孤儿出身" }, { "talent_mixed_root", "杂灵根" }
        };

        public static string Display(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return string.Empty;
            if (Labels.TryGetValue(tag.Trim(), out var label)) return label;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (WarnedUnknown.Add(tag.Trim()))
                UnityEngine.Debug.LogWarning("[CharacterUI] Unknown presentation tag: " + tag);
#endif
            return tag.Trim();
        }
    }
}
