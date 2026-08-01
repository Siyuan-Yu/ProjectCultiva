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

        public IReadOnlyDictionary<DefinitionId, CharacterDefinition> Characters => _characters;
        public IReadOnlyDictionary<DefinitionId, CultivationDefinition> Cultivations => _cultivations;
        public IReadOnlyDictionary<DefinitionId, ItemDefinition> Items => _items;
        public IReadOnlyDictionary<DefinitionId, OpportunitySiteDefinition> OpportunitySites => _opportunitySites;

        public bool ContainsId(DefinitionId id) =>
            _characters.ContainsKey(id) ||
            _cultivations.ContainsKey(id) ||
            _items.ContainsKey(id) ||
            _opportunitySites.ContainsKey(id);

        public Result RegisterCharacter(CharacterDefinition definition)
        {
            if (definition == null)
                return Result.Failure(ErrorCode.InvalidArgument, "CharacterDefinition is null.");
            if (ContainsId(definition.Id))
                return Result.Failure(ErrorCode.DuplicateDefinitionId, "Duplicate DefinitionId.", definition.Id.ToString());

            _characters.Add(definition.Id, definition);
            return Result.Success();
        }

        public Result RegisterCultivation(CultivationDefinition definition)
        {
            if (definition == null)
                return Result.Failure(ErrorCode.InvalidArgument, "CultivationDefinition is null.");
            if (ContainsId(definition.Id))
                return Result.Failure(ErrorCode.DuplicateDefinitionId, "Duplicate DefinitionId.", definition.Id.ToString());

            _cultivations.Add(definition.Id, definition);
            return Result.Success();
        }

        public Result RegisterItem(ItemDefinition definition)
        {
            if (definition == null)
                return Result.Failure(ErrorCode.InvalidArgument, "ItemDefinition is null.");
            if (ContainsId(definition.Id))
                return Result.Failure(ErrorCode.DuplicateDefinitionId, "Duplicate DefinitionId.", definition.Id.ToString());

            _items.Add(definition.Id, definition);
            return Result.Success();
        }

        public Result RegisterOpportunitySite(OpportunitySiteDefinition definition)
        {
            if (definition == null)
                return Result.Failure(ErrorCode.InvalidArgument, "OpportunitySiteDefinition is null.");
            if (ContainsId(definition.Id))
                return Result.Failure(ErrorCode.DuplicateDefinitionId, "Duplicate DefinitionId.", definition.Id.ToString());

            _opportunitySites.Add(definition.Id, definition);
            return Result.Success();
        }

        public bool TryGetCharacter(DefinitionId id, out CharacterDefinition definition) =>
            _characters.TryGetValue(id, out definition);

        public bool TryGetCultivation(DefinitionId id, out CultivationDefinition definition) =>
            _cultivations.TryGetValue(id, out definition);

        public bool TryGetItem(DefinitionId id, out ItemDefinition definition) =>
            _items.TryGetValue(id, out definition);

        public bool TryGetOpportunitySite(DefinitionId id, out OpportunitySiteDefinition definition) =>
            _opportunitySites.TryGetValue(id, out definition);
    }
}
