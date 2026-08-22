using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Formal Army 成员正反索引一致性校验（EditMode 用）。</summary>
    public static class ArmyInvariants
    {
        public static Result AssertMembershipSync(SimulationWorld world)
        {
            if (world?.Strategic?.FormalArmies == null || world.Entities == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld incomplete.");

            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null)
                    continue;

                for (var i = 0; i < army.MemberCharacterIds.Count; i++)
                {
                    var memberId = new EntityId(army.MemberCharacterIds[i]);
                    if (!world.Entities.TryGet(memberId, out var entity))
                    {
                        return Result.Failure(
                            ErrorCode.ValidationFailed,
                            "Army member entity missing.",
                            army.ArmyId + ";" + memberId);
                    }

                    EnsureMembershipComponent(entity);
                    var mem = entity.Get<ArmyMembershipComponent>();
                    if (!string.Equals(mem.ArmyId, army.ArmyId, System.StringComparison.Ordinal))
                    {
                        return Result.Failure(
                            ErrorCode.ValidationFailed,
                            "ArmyMembership reverse index drift.",
                            army.ArmyId + ";" + memberId);
                    }
                }
            }

            foreach (var entity in world.Entities.All)
            {
                if (!entity.TryGet<ArmyMembershipComponent>(out var mem) || !mem.IsInArmy)
                    continue;

                if (!world.Strategic.FormalArmies.TryGet(mem.ArmyId, out var army) ||
                    army == null ||
                    !army.ContainsMember(entity.Id))
                {
                    return Result.Failure(
                        ErrorCode.ValidationFailed,
                        "Orphan ArmyMembership reverse index.",
                        entity.Id + ";" + mem.ArmyId);
                }
            }

            var seenMembers = new HashSet<ulong>();
            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null)
                    continue;
                for (var i = 0; i < army.MemberCharacterIds.Count; i++)
                {
                    var memberValue = army.MemberCharacterIds[i];
                    if (memberValue == 0)
                        continue;
                    if (!seenMembers.Add(memberValue))
                    {
                        return Result.Failure(
                            ErrorCode.ValidationFailed,
                            "Character appears in multiple Formal Armies.",
                            new EntityId(memberValue).ToString());
                    }
                }
            }

            return Result.Success();
        }

        internal static void EnsureMembershipComponent(Entity entity)
        {
            if (!entity.TryGet<ArmyMembershipComponent>(out _))
                entity.AddComponent(new ArmyMembershipComponent());
        }
    }
}
