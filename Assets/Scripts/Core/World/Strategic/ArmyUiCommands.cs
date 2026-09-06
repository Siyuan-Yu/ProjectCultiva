using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Phase B：Host 组军 UI 薄层；只调用 ArmyService，不实现 Domain 规则。</summary>
    public static class ArmyUiCommands
    {
        public static Result<FormalArmy> TryCreateArmy(
            SimulationWorld world,
            HexCoord formationHex,
            string factionId,
            IReadOnlyList<EntityId> selectedMembers,
            EntityId? explicitLeaderId = null,
            PlayerPartyRuntime party = null)
        {
            if (world == null)
                return Result.Fail<FormalArmy>(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (selectedMembers == null || selectedMembers.Count < 1)
                return Result.Fail<FormalArmy>(ErrorCode.InvalidArgument, "Select at least one member.");
            var activeId = ArmyAuthorityRules.ResolveActiveControlledCharacterId(party);
            return ArmyService.CreateArmy(
                world, factionId, formationHex, selectedMembers, explicitLeaderId, party, activeId);
        }

        /// <summary>Legacy test／LevelTester Site adapter；正式 Host 使用 Hex overload。</summary>
        public static Result<FormalArmy> TryCreateArmy(
            SimulationWorld world,
            string siteId,
            string factionId,
            IReadOnlyList<EntityId> selectedMembers,
            EntityId? explicitLeaderId = null,
            PlayerPartyRuntime party = null)
        {
            if (world == null)
                return Result.Fail<FormalArmy>(ErrorCode.InvalidArgument, "SimulationWorld is null.");
            if (selectedMembers == null || selectedMembers.Count < 1)
                return Result.Fail<FormalArmy>(ErrorCode.InvalidArgument, "Select at least one member.");
            var activeId = ArmyAuthorityRules.ResolveActiveControlledCharacterId(party);
            return ArmyService.CreateArmy(
                world, factionId, siteId, selectedMembers, explicitLeaderId, party, activeId);
        }

        public static Result TryGarrison(SimulationWorld world, string armyId) =>
            ArmyService.GarrisonArmy(world, armyId);

        public static Result TryMobilize(SimulationWorld world, string armyId) =>
            ArmyService.MobilizeArmy(world, armyId);

        public static Result TryDisband(SimulationWorld world, string armyId) =>
            ArmyService.DisbandArmy(world, armyId);

        public static Result TryChangeLeader(SimulationWorld world, string armyId, EntityId leaderId) =>
            ArmyService.ChangeLeader(world, armyId, leaderId);

        public static Result TryAddMember(
            SimulationWorld world,
            string armyId,
            EntityId memberId,
            PlayerPartyRuntime party = null)
        {
            var activeId = ArmyAuthorityRules.ResolveActiveControlledCharacterId(party);
            return ArmyService.AddMember(world, armyId, memberId, party, activeId);
        }

        public static Result TryRemoveMember(SimulationWorld world, string armyId, EntityId memberId) =>
            ArmyService.RemoveMember(world, armyId, memberId);

        public static string DescribeError(GameError error)
        {
            if (error.Code == ErrorCode.None)
                return string.Empty;
            var msg = error.Message ?? error.Code.ToString();
            if (msg.Contains("All members must be at the same world hex"))
                return "无法组建军队：所选角色不在同一 Hex";
            if (msg.Contains("Member must be at the same world hex as army"))
                return "不能加入：角色与军队不在同一 Hex";
            if (msg.Contains("Cross-faction"))
                return "不能组军：角色不属于同一势力";
            if (msg.Contains("Garrison requires friendly WorldSite"))
                return "只能在己方 WorldSite 驻扎";
            if (msg.Contains("Army formation requires faction-controlled territory"))
                return "不能组军：当前地点不在该势力控制范围内";
            if (msg.Contains("faction-controlled territory"))
                return "不能管理军队：当前地点不在所属势力控制范围内";
            if (msg.Contains("friendly node") || msg.Contains("friendly node owner") ||
                msg.Contains("OwnerFactionId") || msg.Contains("faction presence") ||
                msg.Contains("player-controlled WorldSite"))
                return "不能组军：当前地点不是己方据点";
            if (msg.Contains("Active controlled character") || msg.Contains("Active character cannot"))
                return "不能加入：主控角色不可编入军团";
            if (msg.Contains("PlayerParty member must leave") || msg.Contains("leave the party before joining"))
                return "不能加入：须先退出 PlayerParty（Stop Follow）";
            if (msg.Contains("already in an army"))
                return "不能加入：角色已经属于其他军团";
            if (msg.Contains("Cannot remove last member"))
                return "不能移除最后一名成员，请解散军团";
            return msg;
        }
    }
}
