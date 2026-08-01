using System.Collections.Generic;
using XianXia.Core.Actions;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Opportunity;
using XianXia.Core.Orders;
using XianXia.Core.Random;
using XianXia.Core.Schedule;
using XianXia.Core.Settlement;
using XianXia.Core.Social;
using XianXia.Core.World;

namespace XianXia.Core.Simulation
{
    /// <summary>Single-region M1 simulation state container.</summary>
    public sealed class SimulationWorld
    {
        readonly Dictionary<string, ScheduleDefinition> _schedules =
            new Dictionary<string, ScheduleDefinition>(System.StringComparer.Ordinal);
        readonly Dictionary<string, OpportunitySite> _opportunitySites =
            new Dictionary<string, OpportunitySite>(System.StringComparer.Ordinal);
        readonly Dictionary<string, CultivationManualSpec> _manuals =
            new Dictionary<string, CultivationManualSpec>(System.StringComparer.Ordinal);

        public SimulationWorld(
            EntityStore entities = null,
            DomainEventQueue events = null,
            IRandomSource random = null,
            RegionId? regionId = null,
            DefaultOrderTranslator translator = null)
        {
            Entities = entities ?? new EntityStore();
            Events = events ?? new DomainEventQueue();
            Random = random ?? new DeterministicRandom(1);
            RegionId = regionId ?? new RegionId(1);
            Translator = translator ?? new DefaultOrderTranslator();
            OrderQueues = new Dictionary<EntityId, OrderQueue>();
            ActiveActions = new Dictionary<ActionId, IAction>();
            Relationships = new RelationshipLedger();
            Settlements = new SettlementBoard();
            Tick = WorldTick.Zero;
            EnabledPackageId = "base";
            EnabledPackageVersion = "0.0.1-m1";
            ObservationDiscoverChancePercent = 100;
        }

        public WorldTick Tick { get; set; }

        /// <summary>0–100 chance Observe discovers an unknown site (tests may force 100).</summary>
        public int ObservationDiscoverChancePercent { get; set; }

        public EntityStore Entities { get; }

        public DomainEventQueue Events { get; }

        public IRandomSource Random { get; set; }

        public RegionId RegionId { get; set; }

        /// <summary>Optional VS0.1 layout placeholders (Region/LocalMap/Settlement). Not gameplay.</summary>
        public WorldInitData WorldLayout { get; set; }

        public DefaultOrderTranslator Translator { get; }

        public Dictionary<EntityId, OrderQueue> OrderQueues { get; }

        public Dictionary<ActionId, IAction> ActiveActions { get; }

        /// <summary>VS0.5 RelationshipLedger unique source of truth (not in Snapshot yet).</summary>
        public RelationshipLedger Relationships { get; }

        /// <summary>VS0.8 settlement board (session-only; not in Snapshot v1).</summary>
        public SettlementBoard Settlements { get; }

        public IReadOnlyDictionary<string, ScheduleDefinition> Schedules => _schedules;

        public IReadOnlyDictionary<string, OpportunitySite> OpportunitySites => _opportunitySites;

        public IReadOnlyDictionary<string, CultivationManualSpec> Manuals => _manuals;

        public string EnabledPackageId { get; set; }

        public string EnabledPackageVersion { get; set; }

        public void RegisterSchedule(ScheduleDefinition definition)
        {
            if (definition == null)
                throw new System.ArgumentNullException(nameof(definition));
            _schedules[definition.Id] = definition;
        }

        public bool TryGetSchedule(string definitionId, out ScheduleDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(definitionId))
                return false;
            return _schedules.TryGetValue(definitionId, out definition);
        }

        public void RegisterOpportunitySite(OpportunitySite site)
        {
            if (site == null)
                throw new System.ArgumentNullException(nameof(site));
            _opportunitySites[site.Id.ToString()] = site;
        }

        public bool TryGetOpportunitySite(DefinitionId id, out OpportunitySite site)
        {
            site = null;
            if (string.IsNullOrEmpty(id.Namespace))
                return false;
            return _opportunitySites.TryGetValue(id.ToString(), out site);
        }

        public void RegisterManual(CultivationManualSpec manual)
        {
            if (manual == null)
                throw new System.ArgumentNullException(nameof(manual));
            _manuals[manual.Id.ToString()] = manual;
        }

        public bool TryGetManual(DefinitionId id, out CultivationManualSpec manual)
        {
            manual = null;
            if (string.IsNullOrEmpty(id.Namespace))
                return false;
            return _manuals.TryGetValue(id.ToString(), out manual);
        }

        public OrderQueue GetOrCreateOrderQueue(EntityId id)
        {
            if (!OrderQueues.TryGetValue(id, out var q))
            {
                q = new OrderQueue();
                OrderQueues[id] = q;
            }
            return q;
        }
    }
}
