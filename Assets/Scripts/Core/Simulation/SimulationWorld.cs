using System.Collections.Generic;
using XianXia.Core.Actions;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Orders;
using XianXia.Core.Random;

namespace XianXia.Core.Simulation
{
    /// <summary>Single-region M1 simulation state container.</summary>
    public sealed class SimulationWorld
    {
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
            Tick = WorldTick.Zero;
            EnabledPackageId = "base";
            EnabledPackageVersion = "0.0.1-m1";
        }

        public WorldTick Tick { get; set; }

        public EntityStore Entities { get; }

        public DomainEventQueue Events { get; }

        public IRandomSource Random { get; set; }

        public RegionId RegionId { get; set; }

        public DefaultOrderTranslator Translator { get; }

        public Dictionary<EntityId, OrderQueue> OrderQueues { get; }

        public Dictionary<ActionId, IAction> ActiveActions { get; }

        public string EnabledPackageId { get; set; }

        public string EnabledPackageVersion { get; set; }

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
