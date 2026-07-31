using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;

namespace XianXia.Data.Content
{
    public sealed class DefinitionRegistry
    {
        readonly Dictionary<DefinitionId, CharacterDefinition> _characters =
            new Dictionary<DefinitionId, CharacterDefinition>();

        public IReadOnlyDictionary<DefinitionId, CharacterDefinition> Characters => _characters;

        public Result RegisterCharacter(CharacterDefinition definition)
        {
            if (definition == null)
                return Result.Failure(ErrorCode.InvalidArgument, "CharacterDefinition is null.");
            if (_characters.ContainsKey(definition.Id))
                return Result.Failure(ErrorCode.DuplicateDefinitionId, "Duplicate DefinitionId.", definition.Id.ToString());

            _characters.Add(definition.Id, definition);
            return Result.Success();
        }

        public bool TryGetCharacter(DefinitionId id, out CharacterDefinition definition) =>
            _characters.TryGetValue(id, out definition);
    }
}
