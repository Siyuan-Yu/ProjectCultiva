using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>战略军事境界门槛唯一配置点；当前为兼容既有内容而关闭执行。</summary>
    public static class StrategicMilitaryRules
    {
        public const RealmStage MinimumRealm = RealmStage.QiRefining;
        public const bool EnforceMinimumRealm = false;

        public static Result ValidatePlayerPartyCanInitiateStrategicMilitaryAction(
            SimulationWorld world, PlayerPartyRuntime party)
        {
            if (!EnforceMinimumRealm)
                return Result.Success();
            if (world == null || party == null)
                return Result.Failure(ErrorCode.InvalidArgument, "PlayerParty required.");
            for (var i = 0; i < party.Members.Count; i++)
                if (ValidateCharacterCanJoinFormalArmy(world, party.Members[i]).IsSuccess)
                    return Result.Success();
            return Result.Failure(ErrorCode.InvalidOperation, "至少一名队伍成员需要达到炼气境才能发起战略军事行动。");
        }

        public static Result ValidateFormalArmyCanParticipate(SimulationWorld world, FormalArmy army)
        {
            if (!EnforceMinimumRealm)
                return Result.Success();
            if (world == null || army == null)
                return Result.Failure(ErrorCode.InvalidArgument, "FormalArmy required.");
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var check = ValidateCharacterCanJoinFormalArmy(world, new EntityId(army.MemberCharacterIds[i]));
                if (check.IsFailure)
                    return check;
            }
            return Result.Success();
        }

        public static Result ValidateCharacterCanJoinFormalArmy(SimulationWorld world, EntityId characterId)
        {
            if (!EnforceMinimumRealm)
                return Result.Success();
            if (world == null || characterId.IsNone || !world.Entities.TryGet(characterId, out var entity) ||
                entity == null || !entity.TryGet<CultivationComponent>(out var cultivation) ||
                cultivation.Realm < MinimumRealm)
                return Result.Failure(ErrorCode.InvalidOperation, "战略军事成员需要达到炼气境。");
            return Result.Success();
        }
    }
}
