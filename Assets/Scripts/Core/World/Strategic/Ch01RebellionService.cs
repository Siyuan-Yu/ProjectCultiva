using System;
using XianXia.Core.Cultivation;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 第一章最小政治动作：玩家主动脱离荒村压迫宗门附庸，并进入战争。
    /// 不承担通用外交职责，也不直接触碰 Board 的内部集合。
    /// </summary>
    public static class Ch01RebellionService
    {
        public const string FlagRebellionStarted = "ch01:rebellion_started";

        public static Result CanBegin(SimulationWorld world, PlayerPartyRuntime party)
        {
            if (world?.Strategic == null || party == null || !party.HasActive)
                return Result.Failure(ErrorCode.InvalidOperation, "第一章起事需要已建立的 PlayerParty 与战略状态。");
            if (!world.Strategic.Ch01FormationScenarioCompat)
                return Result.Failure(ErrorCode.InvalidOperation, "当前不是可起事的第一章场景。");

            var playerFactionId = world.Strategic.PlayerFactionId ?? string.Empty;
            if (!string.Equals(playerFactionId, StrategicFactionCatalog.PlayerFactionId, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "当前玩家势力不满足第一章起事条件。");
            if (!world.Strategic.Vassalages.TryGetOverlord(playerFactionId, out var overlordFactionId) ||
                !string.Equals(overlordFactionId, StrategicFactionCatalog.HuangcunLaborId, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "玩家势力当前并非压迫宗门附庸。");
            if (WarGateService.IsAtWar(world, playerFactionId, overlordFactionId))
                return Result.Failure(ErrorCode.InvalidOperation, "玩家势力已与压迫宗门处于战争。");
            if (!string.Equals(world.PartyWorld?.SiteId, Ch01ScenarioProgressionHooks.HuangcunSiteId, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "必须身处青石荒村才能起事。");
            if (!HasQiRefiningPartyMember(world, party))
                return Result.Failure(ErrorCode.InvalidOperation, "至少一名队伍成员达到炼气境后才能起事。");

            return Result.Success();
        }

        public static Result TryBegin(SimulationWorld world, PlayerPartyRuntime party)
        {
            var eligibility = CanBegin(world, party);
            if (eligibility.IsFailure)
                return eligibility;

            var playerFactionId = world.Strategic.PlayerFactionId;
            var overlordFactionId = StrategicFactionCatalog.HuangcunLaborId;
            var warPreflight = WarGateService.CanDeclareWar(world, playerFactionId, overlordFactionId);
            if (warPreflight.IsFailure)
                return warPreflight;

            if (!world.Strategic.Vassalages.TryReleaseVassalage(playerFactionId, overlordFactionId))
                return Result.Failure(ErrorCode.InvalidOperation, "解除附庸关系失败。");

            var declareWar = WarGateService.DeclareWar(world, playerFactionId, overlordFactionId);
            if (declareWar.IsFailure)
            {
                // 声明失败时回滚本次刚刚解除的同一条关系，避免留下半完成政治状态。
                world.Strategic.Vassalages.TryBindVassalage(playerFactionId, overlordFactionId);
                return declareWar;
            }

            world.Flags.Set(FlagRebellionStarted);
            return Result.Success();
        }

        static bool HasQiRefiningPartyMember(SimulationWorld world, PlayerPartyRuntime party)
        {
            for (var i = 0; i < party.Members.Count; i++)
            {
                var id = party.Members[i];
                if (id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                    continue;
                if (entity.TryGet<CultivationComponent>(out var cultivation) &&
                    cultivation.Realm >= RealmStage.QiRefining)
                    return true;
            }

            return false;
        }
    }
}
