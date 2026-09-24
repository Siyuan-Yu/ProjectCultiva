using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Content
{
    public static class ContentEntityReferenceResolver
    {
        public static Result Resolve(SimulationWorld world, ContentInteractionContext context,
            string reference, out EntityId entityId)
        {
            entityId = EntityId.None;
            if (world == null || string.IsNullOrWhiteSpace(reference))
                return Result.Failure(ErrorCode.InvalidArgument, "Entity reference required.");
            var token = reference.Trim();
            if (string.Equals(token, "@actor", StringComparison.OrdinalIgnoreCase))
                entityId = context?.ActorId ?? EntityId.None;
            else if (string.Equals(token, "@target", StringComparison.OrdinalIgnoreCase))
                entityId = context?.TargetEntityId ?? EntityId.None;
            else if (string.Equals(token, "@issuer", StringComparison.OrdinalIgnoreCase))
                entityId = context?.IssuerEntityId ?? EntityId.None;
            else
            {
                Entity match = null;
                foreach (var entity in world.Entities.All)
                    if (string.Equals(entity.DefinitionId.ToString(), token, StringComparison.Ordinal))
                    {
                        if (match != null)
                            return Result.Failure(ErrorCode.InvalidOperation,
                                "Character definition resolves to multiple runtime entities.", token);
                        match = entity;
                    }
                if (match != null) entityId = match.Id;
            }
            return entityId.IsNone || !world.Entities.TryGet(entityId, out _)
                ? Result.Failure(ErrorCode.EntityNotFound, "Entity reference cannot be resolved.", token)
                : Result.Success();
        }
    }
}
