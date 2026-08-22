using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Development acceptance UI 薄层：只调用正式 Domain／Service，不写 Board。</summary>
    public static class StrategicAcceptanceCommands
    {
        public static Result TryDeclareWar(SimulationWorld world, string factionA, string factionB) =>
            WarGateService.DeclareWar(world, factionA, factionB);

        public static bool IsAtWar(SimulationWorld world, string factionA, string factionB) =>
            WarGateService.IsAtWar(world, factionA, factionB);

        public static Result TryFormAlliance(SimulationWorld world, string factionA, string factionB)
        {
            if (world?.Strategic?.Alliances == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld incomplete.");
            if (string.IsNullOrEmpty(factionA) || string.IsNullOrEmpty(factionB))
                return Result.Failure(ErrorCode.InvalidArgument, "Both factions required.");
            if (world.Strategic.Alliances.FormAlliance(factionA, factionB, out _))
                return Result.Success();
            return Result.Failure(ErrorCode.InvalidOperation, "Alliance formation rejected by domain.");
        }

        public static Result TryBindVassalage(SimulationWorld world, string overlordFactionId, string vassalFactionId)
        {
            if (world?.Strategic?.Vassalages == null)
                return Result.Failure(ErrorCode.InvalidArgument, "SimulationWorld incomplete.");
            if (string.IsNullOrEmpty(overlordFactionId) || string.IsNullOrEmpty(vassalFactionId))
                return Result.Failure(ErrorCode.InvalidArgument, "Overlord and vassal required.");
            if (world.Strategic.Vassalages.TryBindVassalage(vassalFactionId, overlordFactionId))
                return Result.Success();
            return Result.Failure(ErrorCode.InvalidOperation, "Vassalage binding rejected by domain.");
        }

        public static Result TryCollectTribute(
            SimulationWorld world,
            string payerFactionId,
            string receiverFactionId,
            out int amount) =>
            TributeService.TryCollectTribute(world, payerFactionId, receiverFactionId, out amount);

        public static Result TryAddArmyMember(SimulationWorld world, string armyId, EntityId memberId) =>
            ArmyService.AddMember(world, armyId, memberId);

        public static Result TryRemoveArmyMember(SimulationWorld world, string armyId, EntityId memberId) =>
            ArmyService.RemoveMember(world, armyId, memberId);

        public static Result TryChangeArmyLeader(SimulationWorld world, string armyId, EntityId leaderId) =>
            ArmyService.ChangeLeader(world, armyId, leaderId);
    }
}
