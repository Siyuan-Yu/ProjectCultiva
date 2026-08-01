using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;

namespace XianXia.Data.Content
{
    public sealed class DefinitionRegistry
    {
        readonly Dictionary<DefinitionId, CharacterDefinition> _characters =
            new Dictionary<DefinitionId, CharacterDefinition>();
        readonly Dictionary<DefinitionId, CultivationDefinition> _cultivations =
            new Dictionary<DefinitionId, CultivationDefinition>();
        readonly Dictionary<DefinitionId, ItemDefinition> _items =
            new Dictionary<DefinitionId, ItemDefinition>();
        readonly Dictionary<DefinitionId, OpportunitySiteDefinition> _opportunitySites =
            new Dictionary<DefinitionId, OpportunitySiteDefinition>();
        readonly Dictionary<DefinitionId, OpeningScenarioDefinition> _openingScenarios =
            new Dictionary<DefinitionId, OpeningScenarioDefinition>();
        readonly Dictionary<DefinitionId, ResourceDefinition> _resources =
            new Dictionary<DefinitionId, ResourceDefinition>();
        readonly Dictionary<DefinitionId, FacilityDefinition> _facilities =
            new Dictionary<DefinitionId, FacilityDefinition>();
        readonly Dictionary<DefinitionId, SettlementDefinition> _settlements =
            new Dictionary<DefinitionId, SettlementDefinition>();
        readonly Dictionary<DefinitionId, WorldRegionDefinition> _worldRegions =
            new Dictionary<DefinitionId, WorldRegionDefinition>();
        readonly Dictionary<DefinitionId, QuestDefinition> _quests =
            new Dictionary<DefinitionId, QuestDefinition>();
        readonly Dictionary<DefinitionId, ContentEventDefinition> _contentEvents =
            new Dictionary<DefinitionId, ContentEventDefinition>();
        readonly Dictionary<DefinitionId, ChapterDefinition> _chapters =
            new Dictionary<DefinitionId, ChapterDefinition>();

        public IReadOnlyDictionary<DefinitionId, CharacterDefinition> Characters => _characters;
        public IReadOnlyDictionary<DefinitionId, CultivationDefinition> Cultivations => _cultivations;
        public IReadOnlyDictionary<DefinitionId, ItemDefinition> Items => _items;
        public IReadOnlyDictionary<DefinitionId, OpportunitySiteDefinition> OpportunitySites => _opportunitySites;
        public IReadOnlyDictionary<DefinitionId, OpeningScenarioDefinition> OpeningScenarios => _openingScenarios;
        public IReadOnlyDictionary<DefinitionId, ResourceDefinition> Resources => _resources;
        public IReadOnlyDictionary<DefinitionId, FacilityDefinition> Facilities => _facilities;
        public IReadOnlyDictionary<DefinitionId, SettlementDefinition> Settlements => _settlements;
        public IReadOnlyDictionary<DefinitionId, WorldRegionDefinition> WorldRegions => _worldRegions;
        public IReadOnlyDictionary<DefinitionId, QuestDefinition> Quests => _quests;
        public IReadOnlyDictionary<DefinitionId, ContentEventDefinition> ContentEvents => _contentEvents;
        public IReadOnlyDictionary<DefinitionId, ChapterDefinition> Chapters => _chapters;

        public bool ContainsId(DefinitionId id) =>
            _characters.ContainsKey(id) ||
            _cultivations.ContainsKey(id) ||
            _items.ContainsKey(id) ||
            _opportunitySites.ContainsKey(id) ||
            _openingScenarios.ContainsKey(id) ||
            _resources.ContainsKey(id) ||
            _facilities.ContainsKey(id) ||
            _settlements.ContainsKey(id) ||
            _worldRegions.ContainsKey(id) ||
            _quests.ContainsKey(id) ||
            _contentEvents.ContainsKey(id) ||
            _chapters.ContainsKey(id);

        public Result RegisterCharacter(CharacterDefinition definition)
        {
            if (definition == null)
                return Result.Failure(ErrorCode.InvalidArgument, "CharacterDefinition is null.");
            return Register(_characters, definition, definition.Id);
        }

        public Result RegisterCultivation(CultivationDefinition definition)
        {
            if (definition == null)
                return Result.Failure(ErrorCode.InvalidArgument, "CultivationDefinition is null.");
            return Register(_cultivations, definition, definition.Id);
        }

        public Result RegisterItem(ItemDefinition definition)
        {
            if (definition == null)
                return Result.Failure(ErrorCode.InvalidArgument, "ItemDefinition is null.");
            return Register(_items, definition, definition.Id);
        }

        public Result RegisterOpportunitySite(OpportunitySiteDefinition definition)
        {
            if (definition == null)
                return Result.Failure(ErrorCode.InvalidArgument, "OpportunitySiteDefinition is null.");
            return Register(_opportunitySites, definition, definition.Id);
        }

        public Result RegisterOpeningScenario(OpeningScenarioDefinition definition)
        {
            if (definition == null)
                return Result.Failure(ErrorCode.InvalidArgument, "OpeningScenarioDefinition is null.");
            return Register(_openingScenarios, definition, definition.Id);
        }

        public Result RegisterResource(ResourceDefinition definition)
        {
            if (definition == null)
                return Result.Failure(ErrorCode.InvalidArgument, "ResourceDefinition is null.");
            return Register(_resources, definition, definition.Id);
        }

        public Result RegisterFacility(FacilityDefinition definition)
        {
            if (definition == null)
                return Result.Failure(ErrorCode.InvalidArgument, "FacilityDefinition is null.");
            return Register(_facilities, definition, definition.Id);
        }

        public Result RegisterSettlement(SettlementDefinition definition)
        {
            if (definition == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SettlementDefinition is null.");
            return Register(_settlements, definition, definition.Id);
        }

        public Result RegisterWorldRegion(WorldRegionDefinition definition)
        {
            if (definition == null)
                return Result.Failure(ErrorCode.InvalidArgument, "WorldRegionDefinition is null.");
            return Register(_worldRegions, definition, definition.Id);
        }

        public Result RegisterQuest(QuestDefinition definition)
        {
            if (definition == null)
                return Result.Failure(ErrorCode.InvalidArgument, "QuestDefinition is null.");
            return Register(_quests, definition, definition.Id);
        }

        public Result RegisterContentEvent(ContentEventDefinition definition)
        {
            if (definition == null)
                return Result.Failure(ErrorCode.InvalidArgument, "ContentEventDefinition is null.");
            return Register(_contentEvents, definition, definition.Id);
        }

        public Result RegisterChapter(ChapterDefinition definition)
        {
            if (definition == null)
                return Result.Failure(ErrorCode.InvalidArgument, "ChapterDefinition is null.");
            return Register(_chapters, definition, definition.Id);
        }

        public bool TryGetCharacter(DefinitionId id, out CharacterDefinition definition) =>
            _characters.TryGetValue(id, out definition);

        public bool TryGetCultivation(DefinitionId id, out CultivationDefinition definition) =>
            _cultivations.TryGetValue(id, out definition);

        public bool TryGetItem(DefinitionId id, out ItemDefinition definition) =>
            _items.TryGetValue(id, out definition);

        public bool TryGetOpportunitySite(DefinitionId id, out OpportunitySiteDefinition definition) =>
            _opportunitySites.TryGetValue(id, out definition);

        public bool TryGetOpeningScenario(DefinitionId id, out OpeningScenarioDefinition definition) =>
            _openingScenarios.TryGetValue(id, out definition);

        public bool TryGetResource(DefinitionId id, out ResourceDefinition definition) =>
            _resources.TryGetValue(id, out definition);

        public bool TryGetFacility(DefinitionId id, out FacilityDefinition definition) =>
            _facilities.TryGetValue(id, out definition);

        public bool TryGetSettlement(DefinitionId id, out SettlementDefinition definition) =>
            _settlements.TryGetValue(id, out definition);

        public bool TryGetWorldRegion(DefinitionId id, out WorldRegionDefinition definition) =>
            _worldRegions.TryGetValue(id, out definition);

        public bool TryGetQuest(DefinitionId id, out QuestDefinition definition) =>
            _quests.TryGetValue(id, out definition);

        public bool TryGetContentEvent(DefinitionId id, out ContentEventDefinition definition) =>
            _contentEvents.TryGetValue(id, out definition);

        public bool TryGetChapter(DefinitionId id, out ChapterDefinition definition) =>
            _chapters.TryGetValue(id, out definition);

        Result Register<T>(Dictionary<DefinitionId, T> map, T definition, DefinitionId id)
        {
            if (ContainsId(id))
                return Result.Failure(ErrorCode.DuplicateDefinitionId, "Duplicate DefinitionId.", id.ToString());
            map.Add(id, definition);
            return Result.Success();
        }
    }
}
