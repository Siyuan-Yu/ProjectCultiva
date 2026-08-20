using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;

namespace XianXia.Core.Entities
{
    public sealed class Entity : IEntity
    {
        static readonly HashSet<Type> Whitelist = new HashSet<Type>
        {
            typeof(IdentityComponent),
            typeof(AttributesComponent),
            typeof(LifecycleComponent),
            typeof(ActionStateComponent),
            typeof(XianXia.Core.Cultivation.CultivationComponent),
            typeof(XianXia.Core.Cultivation.SpiritRootComponent),
            typeof(XianXia.Core.Labor.DailyTaskComponent),
            typeof(XianXia.Core.Schedule.ScheduleComponent),
            typeof(XianXia.Core.Opportunity.KnownSitesComponent),
            typeof(XianXia.Core.Concealment.PersonalConcealmentRiskComponent),
            typeof(XianXia.Core.Social.PersonalityProfileComponent),
            typeof(XianXia.Core.Social.RelationshipComponent),
            typeof(XianXia.Core.Social.FactionMembershipComponent),
            typeof(XianXia.Core.Social.NpcAiRoleComponent),
            typeof(XianXia.Core.Social.CharacterBioComponent),
            typeof(XianXia.Core.Settlement.WorkAssignmentComponent),
            typeof(XianXia.Core.Exploration.EntityLocationComponent),
            typeof(XianXia.Core.Npc.JobComponent),
            typeof(XianXia.Core.Npc.ActivityTendencyComponent),
            typeof(XianXia.Core.Npc.MovementIntentComponent),
            // 战斗：未进白名单时 AddComponent 静默失败 → 互砍无伤害／无特效／斗技装不上
            typeof(XianXia.Core.Combat.CombatVitalsComponent),
            typeof(XianXia.Core.Combat.CombatArtsComponent),
            typeof(XianXia.Core.Combat.EncounterLinkComponent),
            typeof(XianXia.Core.Combat.SpiritVeilComponent),
            typeof(XianXia.Core.Combat.CorpseComponent)
        };

        readonly Dictionary<Type, IComponent> _components = new Dictionary<Type, IComponent>();

        public Entity(EntityId id, DefinitionId definitionId, EntityTag tags, string displayName)
        {
            Id = id;
            DefinitionId = definitionId;
            Tags = tags;
            DisplayName = displayName ?? string.Empty;
        }

        public EntityId Id { get; }

        public DefinitionId DefinitionId { get; }

        public string DisplayName { get; set; }

        public EntityTag Tags { get; set; }

        public Result AddComponent(IComponent component)
        {
            if (component == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Component is null.");

            var type = component.GetType();
            if (!Whitelist.Contains(type))
                return Result.Failure(ErrorCode.InvalidOperation, "Component type not in whitelist.", type.Name);

            if (_components.ContainsKey(type))
                return Result.Failure(ErrorCode.AlreadyExists, "Component already present.", type.Name);

            _components.Add(type, component);
            return Result.Success();
        }

        public bool TryGet<T>(out T component) where T : class, IComponent
        {
            if (_components.TryGetValue(typeof(T), out var raw))
            {
                component = (T)raw;
                return true;
            }

            component = null;
            return false;
        }

        public T Get<T>() where T : class, IComponent
        {
            if (!TryGet<T>(out var component))
                throw new InvalidOperationException("Missing component " + typeof(T).Name);
            return component;
        }

        public IEnumerable<IComponent> Components => _components.Values;
    }
}
