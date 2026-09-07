using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.Social
{
    public sealed class SocialBondService
    {
        public Result TryAddBond(SimulationWorld world, SocialBondKind kind, EntityId from, EntityId to) =>
            Add(world, kind, from, to, publish: true);

        public Result RestoreBond(SimulationWorld world, SocialBondKind kind, EntityId from, EntityId to) =>
            Add(world, kind, from, to, publish: false);

        public Result TryRemoveBond(SimulationWorld world, SocialBondKind kind, EntityId from, EntityId to)
        {
            var valid = Validate(world, kind, from, to);
            if (valid.IsFailure)
                return valid;
            if (kind == SocialBondKind.ParentChild || kind == SocialBondKind.Sibling)
                return Result.Failure(ErrorCode.InvalidOperation, "此类关系事实不能由普通 Gameplay 解除。");
            if (!world.SocialBonds.TryRemove(kind, from, to))
                return Result.Failure(ErrorCode.NotFound, "关系事实不存在。");
            world.Events.Publish(EventType.SocialBondChanged, world.Tick, from, to,
                "operation=remove;kind=" + kind);
            return Result.Success();
        }

        Result Add(
            SimulationWorld world,
            SocialBondKind kind,
            EntityId from,
            EntityId to,
            bool publish)
        {
            var valid = Validate(world, kind, from, to);
            if (valid.IsFailure)
                return valid;
            var bond = new SocialBond(kind, from, to);
            if (!world.SocialBonds.TryAdd(bond))
                return Result.Failure(ErrorCode.AlreadyExists, "同类型关系事实已存在。");
            if (publish)
                world.Events.Publish(EventType.SocialBondChanged, world.Tick, bond.From, bond.To,
                    "operation=add;kind=" + kind);
            return Result.Success();
        }

        static Result Validate(SimulationWorld world, SocialBondKind kind, EntityId from, EntityId to)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld 不能为空。");
            if (!System.Enum.IsDefined(typeof(SocialBondKind), kind))
                return Result.Failure(ErrorCode.InvalidArgument, "未知 SocialBondKind。");
            if (from.IsNone || to.IsNone || from == to)
                return Result.Failure(ErrorCode.InvalidArgument, "关系事实端点无效。");
            if (!world.Entities.TryGet(from, out var fromEntity) || !IsCharacter(fromEntity) ||
                !world.Entities.TryGet(to, out var toEntity) || !IsCharacter(toEntity))
                return Result.Failure(ErrorCode.EntityNotFound, "关系事实端点必须是 Character。");
            return Result.Success();
        }

        internal static bool IsCharacter(Entity entity) =>
            entity != null && (entity.Tags & (EntityTag.Character | EntityTag.Npc)) != 0;
    }
}
