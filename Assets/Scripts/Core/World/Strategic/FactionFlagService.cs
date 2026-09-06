using System;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>阵营旗的政治生命周期。所有变更后只通过 Territory Resolver 重建派生控制。</summary>
    public static class FactionFlagService
    {
        /// <summary>V1 阵营旗建筑防御；伤害与近战建筑公式统一。</summary>
        public const int StructureDefense = 4;

        public static int ComputeAssaultDamage(Entity attacker)
            => StrategicBuildingDamage.Compute(attacker, StructureDefense);

        public static Result ValidatePlacement(
            SimulationWorld world, string factionId, HexCoord anchor, out int neutralHexGain)
        {
            neutralHexGain = 0;
            if (world?.Strategic == null || string.IsNullOrEmpty(factionId))
                return Result.Failure(ErrorCode.InvalidArgument, "阵营旗参数无效。");
            if (world.HexWorld == null || !world.HexWorld.TryGetCell(anchor, out var anchorCell) ||
                anchorCell == null || !anchorCell.IsPassable)
                return Result.Failure(ErrorCode.InvalidOperation, "阵营旗只能建立在合法可通行的荒野格。");
            if (world.Strategic.Sites.TryGetAtHex(anchor, out var site) && site != null)
                return Result.Failure(ErrorCode.InvalidOperation, "WorldSite 范围内不能建立阵营旗。");
            if (world.Strategic.FactionFlags.TryGetAt(anchor, out _))
                return Result.Failure(ErrorCode.InvalidOperation,
                    "此 Hex 已有阵营控制建筑，需要先移除当前控制建筑。");

            var anchorController = TerritoryControlService.GetController(world, anchor);
            if (!string.IsNullOrEmpty(anchorController) &&
                !string.Equals(anchorController, factionId, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "敌方有效领土内不能建立阵营旗。");

            var nominal = StrategicTerritoryCoverageResolver.ExpandOneRing(new[] { anchor });
            for (var i = 0; i < nominal.Count; i++)
            {
                var hex = nominal[i];
                if (!world.HexWorld.Contains(hex))
                    continue;
                if (string.IsNullOrEmpty(TerritoryControlService.GetController(world, hex)))
                    neutralHexGain++;
            }
            if (neutralHexGain <= 0)
                return Result.Failure(ErrorCode.InvalidOperation, "候选范围没有可新增的无主 Hex。");
            return Result.Success();
        }

        public static long NextEstablishedOrder(SimulationWorld world)
        {
            long max = 0;
            if (world?.Strategic == null)
                return 1;
            foreach (var pair in world.Strategic.Sites.Sites)
                if (pair.Value != null && pair.Value.ControlEstablishedOrder > max)
                    max = pair.Value.ControlEstablishedOrder;
            foreach (var pair in world.Strategic.FactionFlags.Flags)
                if (pair.Value != null && pair.Value.EstablishedOrder > max)
                    max = pair.Value.EstablishedOrder;
            return max == long.MaxValue ? long.MaxValue : max + 1;
        }

        public static Result TryPlace(
            SimulationWorld world, string flagId, string factionId, HexCoord anchor,
            long establishedOrder, float localX, float localZ, bool hasLocalPosition)
        {
            if (world?.Strategic == null || string.IsNullOrEmpty(flagId) || string.IsNullOrEmpty(factionId))
                return Result.Failure(ErrorCode.InvalidArgument, "阵营旗参数无效。");
            var placement = ValidatePlacement(world, factionId, anchor, out _);
            if (placement.IsFailure)
                return placement;
            var flag = new FactionFlagState
            {
                FlagId=flagId, FactionId=factionId, AnchorHex=anchor, EstablishedOrder=establishedOrder,
                CurrentHp=100, MaxHp=100, HasLocalPosition=hasLocalPosition, LocalX=localX, LocalZ=localZ
            };
            if (!world.Strategic.FactionFlags.Register(flag))
                return Result.Failure(ErrorCode.InvalidOperation, "阵营旗 ID 或锚点重复。");
            StrategicTerritoryCoverageResolver.Rebuild(world);
            return Result.Success();
        }

        public static Result TryApplyAssault(
            SimulationWorld world, PlayerPartyRuntime party, string attackerFactionId, string flagId, int damage)
        {
            if (world?.Strategic == null || party == null || !party.HasActive || damage <= 0)
                return Result.Failure(ErrorCode.InvalidArgument, "阵营旗突击参数无效。");
            var military = StrategicMilitaryRules.ValidatePlayerPartyCanInitiateStrategicMilitaryAction(world, party);
            if (military.IsFailure)
                return military;
            if (!world.Strategic.FactionFlags.Flags.TryGetValue(flagId ?? string.Empty, out var flag) || flag == null)
                return Result.Failure(ErrorCode.NotFound, "阵营旗不存在。");
            if (string.Equals(attackerFactionId, flag.FactionId, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "不能攻击己方阵营旗。");
            if (!WarGateService.CanAttack(world, attackerFactionId, flag.FactionId))
                return Result.Failure(ErrorCode.InvalidOperation, "攻击阵营旗需要有效战争状态。");

            flag.CurrentHp = Math.Max(0, flag.CurrentHp - damage);
            if (flag.CurrentHp <= 0)
                return TryDestroy(world, flag.FlagId);
            return Result.Success();
        }

        /// <summary>角色按正式近战属性对阵营旗造成一击；政治／军事门槛仍由本服务校验。</summary>
        public static Result ApplyStrikeFromAttacker(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string attackerFactionId,
            string flagId,
            EntityId attackerId,
            out int damageApplied)
        {
            damageApplied = 0;
            if (world == null || attackerId.IsNone || !world.Entities.TryGet(attackerId, out var attacker))
                return Result.Failure(ErrorCode.EntityNotFound, "攻击者不存在。");
            if (party == null || !party.IsMember(attackerId))
                return Result.Failure(ErrorCode.InvalidOperation, "攻击者不属于 PlayerParty。");
            damageApplied = ComputeAssaultDamage(attacker);
            return TryApplyAssault(world, party, attackerFactionId, flagId, damageApplied);
        }

        public static Result TryDestroy(SimulationWorld world, string flagId)
        {
            if (world?.Strategic == null || !world.Strategic.FactionFlags.Remove(flagId))
                return Result.Failure(ErrorCode.NotFound, "阵营旗不存在。");
            // 不写攻击方、不硬编码无主：剩余 Control Assets 按 EstablishedOrder 自动重新求解。
            StrategicTerritoryCoverageResolver.Rebuild(world);
            return Result.Success();
        }
    }
}
