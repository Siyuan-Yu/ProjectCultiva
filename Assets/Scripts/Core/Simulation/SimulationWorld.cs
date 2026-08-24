using System.Collections.Generic;
using XianXia.Core.Actions;
using XianXia.Core.Combat;
using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Opportunity;
using XianXia.Core.Orders;
using XianXia.Core.Random;
using XianXia.Core.Schedule;
using XianXia.Core.Exploration;
using XianXia.Core.Inventory;
using XianXia.Core.Labor;
using XianXia.Core.Npc;
using XianXia.Core.Settlement;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

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
        readonly Dictionary<string, CombatArtSpec> _combatArts =
            new Dictionary<string, CombatArtSpec>(System.StringComparer.Ordinal);
        readonly Dictionary<string, WorkAreaDefinition> _workAreas =
            new Dictionary<string, WorkAreaDefinition>(System.StringComparer.Ordinal);
        readonly Dictionary<string, JobDefinition> _jobs =
            new Dictionary<string, JobDefinition>(System.StringComparer.Ordinal);

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
            WorldRegion = new WorldRegionBoard();
            HexWorld = new HexWorld();
            WorldPresence = new WorldPresenceBoard();
            PartyWorld = new PartyWorldPresence();
            Flags = new WorldFlagBoard();
            Quests = new QuestBoard();
            ContentEvents = new ContentEventBoard();
            Chapters = new ChapterBoard();
            SupervisorAnger = new XianXia.Core.Social.SupervisorAngerBoard();
            LocationLabor = new LocationLaborProgressBoard();
            WorkAreaOccupancy = new WorkAreaOccupancyBoard();
            ControlCores = new ControlCoreBoard();
            HousingAssignments = new HousingAssignmentBoard();
            SettlementAuthority = new SettlementAuthorityBoard();
            InventoryCatalog = new InventoryCatalog();
            Inventory = new PartyInventory(InventoryCatalog, PartyInventory.DefaultSlotCapacity);
            ContentCounters = new ContentCounterBoard();
            ContentDaily = new ContentDailyBoard();
            LocalMap = new LocalMapSession();
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

        /// <summary>村内地点表（历史名 worldRegion；非正式大世界）。</summary>
        public WorldRegionBoard WorldRegion { get; }

        /// <summary>Hex 战略世界真源（155+）。</summary>
        public HexWorld HexWorld { get; }

        /// <summary>兼容旧属性名。</summary>
        public HexWorld HexGrid => HexWorld;

        /// <summary>各角色宏观位置。</summary>
        public WorldPresenceBoard WorldPresence { get; }

        /// <summary>当前镜头／焦点 Node 摘要。</summary>
        public PartyWorldPresence PartyWorld { get; }

        /// <summary>宏观战略层：外交／军队／遭遇／接战（[138]）。</summary>
        public StrategicBoard Strategic { get; } = new StrategicBoard();

        /// <summary>Content Ready: session flags for quests／events (not in Snapshot v1).</summary>
        public WorldFlagBoard Flags { get; }

        /// <summary>Content Ready: quest specs＋runtime (session-only; not in Snapshot v1).</summary>
        public QuestBoard Quests { get; }

        /// <summary>Content Ready: content events (session-only; not in Snapshot v1).</summary>
        public ContentEventBoard ContentEvents { get; }

        /// <summary>Chapter Production: active chapter＋beats (session-only; not in Snapshot v1).</summary>
        public ChapterBoard Chapters { get; }

        /// <summary>Demo [49] supervisor anger (display-only; not in Snapshot v1).</summary>
        public XianXia.Core.Social.SupervisorAngerBoard SupervisorAnger { get; }

        /// <summary>Player labor ticks at locations (session-only; not in Snapshot v1).</summary>
        public LocationLaborProgressBoard LocationLabor { get; }

        /// <summary>Soft work-area slot occupancy (session-only; not in Snapshot v1).</summary>
        public WorkAreaOccupancyBoard WorkAreaOccupancy { get; }

        /// <summary>Control cores (主管府等); session-only; not in Snapshot v1.</summary>
        public ControlCoreBoard ControlCores { get; }

        /// <summary>Housing area ownership; session-only; not in Snapshot v1.</summary>
        public HousingAssignmentBoard HousingAssignments { get; }

        /// <summary>Privileges from captured control cores; session-only.</summary>
        public SettlementAuthorityBoard SettlementAuthority { get; }

        /// <summary>Quest／event counters（对弈胜场等）; session-only; not in Snapshot v1.</summary>
        public ContentCounterBoard ContentCounters { get; }

        /// <summary>Per-game-day marks（今日已对弈）; session-only; not in Snapshot v1.</summary>
        public ContentDailyBoard ContentDaily { get; }

        /// <summary>当前 LocalMap 进出状态；session-only; not in Snapshot v1.</summary>
        public LocalMapSession LocalMap { get; }

        /// <summary>Item display／stack rules for the shared party bag.</summary>
        public InventoryCatalog InventoryCatalog { get; }

        /// <summary>Shared party backpack (session-only; not in Snapshot v1).</summary>
        public PartyInventory Inventory { get; }

        /// <summary>Realm breakthrough ladder (content/default; session-only).</summary>
        public RealmLadderBoard RealmLadder { get; set; } = RealmLadderBoard.CreateDefault();

        /// <summary>Alias for story／content flags (same board as <see cref="Flags"/>).</summary>
        public WorldFlagBoard StoryFlags => Flags;

        public IReadOnlyDictionary<string, ScheduleDefinition> Schedules => _schedules;

        public IReadOnlyDictionary<string, OpportunitySite> OpportunitySites => _opportunitySites;

        public IReadOnlyDictionary<string, CultivationManualSpec> Manuals => _manuals;

        public IReadOnlyDictionary<string, CombatArtSpec> CombatArts => _combatArts;

        public IReadOnlyDictionary<string, WorkAreaDefinition> WorkAreas => _workAreas;

        public IReadOnlyDictionary<string, JobDefinition> Jobs => _jobs;

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

        public void RegisterCombatArt(CombatArtSpec art)
        {
            if (art == null)
                throw new System.ArgumentNullException(nameof(art));
            _combatArts[art.Id.ToString()] = art;
        }

        public bool TryGetCombatArt(DefinitionId id, out CombatArtSpec art)
        {
            art = null;
            if (string.IsNullOrEmpty(id.Namespace))
                return false;
            return _combatArts.TryGetValue(id.ToString(), out art);
        }

        public void RegisterWorkArea(WorkAreaDefinition definition)
        {
            if (definition == null)
                throw new System.ArgumentNullException(nameof(definition));
            if (string.IsNullOrEmpty(definition.Id))
                throw new System.ArgumentException("WorkAreaDefinition.Id required.");
            _workAreas[definition.Id] = definition;
            ControlCores.RegisterOrRefresh(definition);
            if (definition.IsControlCore &&
                ControlCores.TryGet(definition.Id, out var core) &&
                core != null)
            {
                var siteId = ResolveSiteIdForLocation(definition.LocationId);
                CaptureObjectiveService.RegisterControlCore(this, core, siteId);
            }
        }

        static string ResolveSiteIdForLocation(SimulationWorld world, string locationId)
        {
            if (world?.Strategic?.Sites == null || string.IsNullOrEmpty(locationId))
                return string.Empty;

            var partySiteId = world.PartyWorld?.SiteId;
            if (!string.IsNullOrEmpty(partySiteId) &&
                world.Strategic.Sites.TryGet(partySiteId, out var partySite) &&
                partySite != null &&
                string.Equals(partySite.LocalMapId, locationId, System.StringComparison.Ordinal))
                return partySiteId;

            foreach (var kv in world.Strategic.Sites.Sites)
            {
                var site = kv.Value;
                if (site != null &&
                    string.Equals(site.LocalMapId, locationId, System.StringComparison.Ordinal))
                    return site.SiteId;
            }

            return string.Empty;
        }

        string ResolveSiteIdForLocation(string locationId) => ResolveSiteIdForLocation(this, locationId);

        public bool TryGetWorkArea(string id, out WorkAreaDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(id))
                return false;
            return _workAreas.TryGetValue(id, out definition);
        }

        public void RegisterJob(JobDefinition definition)
        {
            if (definition == null)
                throw new System.ArgumentNullException(nameof(definition));
            if (string.IsNullOrEmpty(definition.Id))
                throw new System.ArgumentException("JobDefinition.Id required.");
            _jobs[definition.Id] = definition;
        }

        public bool TryGetJob(string id, out JobDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(id))
                return false;
            return _jobs.TryGetValue(id, out definition);
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
