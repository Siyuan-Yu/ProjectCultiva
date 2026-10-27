using System;
using XianXia.Core.Combat;
using XianXia.Core.Construction;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    /// <summary>Person-to-person prison visits and persisted care requests.</summary>
    public static class HuangcunPrisonVisitService
    {
        public static string QuestId(string task) => "base:quest_huangcun_prison_" + task;
        public static string ProgressKey(string task) => "survival:prison:care:" + task;
        static string DailyKey(string task) => "survival:prison:completed:" + task;
        public static bool Applies(SimulationWorld world) => world?.HuangcunSurvival.Enabled == true &&
            !world.HuangcunSurvival.Released;

        public static Result ValidateVisit(SimulationWorld world, EntityId actor, bool requireSpeech = false)
        {
            if (!Applies(world)) return Fail("同伴已不在关押状态。");
            if (!world.Entities.TryGet(actor, out var acting) || !CombatLifeStateService.CanFight(acting) ||
                world.Strategic.PlayerFactionId != world.HuangcunSurvival.OwnerFactionId ||
                world.Strategic.PlayerPartyContext?.IsMember(actor) != true ||
                world.LocalMap.IsInInterior || world.Strategic.CharacterEncounter != null)
                return Fail("当前人物不能探视牢房。");
            if (!world.OutdoorConstructedAssets.Assets.TryGetValue(world.HuangcunSurvival.PrisonRoomId, out var room) ||
                room.RoomUsage != RoomUsage.Prison) return Fail("原牢房已不存在或不再用于关押。");
            var prisoner = HuangcunSurvivalService.Prisoner(world);
            if (prisoner == null || !prisoner.TryGet<LifecycleComponent>(out var life) || life.IsDead || life.IsRemoved)
                return Fail("被囚同伴已无法接受探视。");
            if (!CharacterWorldPresenceQuery.TryResolve(world, prisoner.Id, out var captiveAt) ||
                !captiveAt.HasWorldPosition || captiveAt.SurfaceId != room.SurfaceId ||
                !PrisonConfinementService.IsInside(world, room, captiveAt.SurfaceId, captiveAt.WorldPosition))
                return Fail("同伴目前不在这座牢房内。");
            if (!CharacterWorldPresenceQuery.TryResolve(world, actor, out var at) || !at.HasWorldPosition ||
                !PrisonConfinementService.IsInside(world, room, at.SurfaceId, at.WorldPosition) ||
                WorldVec2.Distance(at.WorldPosition, captiveAt.WorldPosition) > world.OpeningArea.CellSize * 3)
                return Fail("先走进牢房，靠近这名囚犯（三格以内）再交谈或递送。");
            if (HuangcunOpeningAccess.IsLocked(world) &&
                (world.HuangcunSurvival.PunishmentTargetId == actor.Value ||
                 SupervisorAngerDriver.TryFindPrisonObserver(world, actor, out _)))
                return Fail("进入牢房禁区已被发现，看守正在追赶，当前无法安心交谈或照料。");
            if (requireSpeech && life.IsIncapacitated) return Fail("同伴尚未清醒，无法交谈；仍可查看身体、送饭送药。");
            return Result.Success();
        }

        public static bool Needs(SimulationWorld world, string task)
        {
            var prisoner = HuangcunSurvivalService.Prisoner(world);
            if (prisoner == null || !prisoner.TryGet<LifecycleComponent>(out var life) || life.IsDead || life.IsRemoved) return false;
            if (task == "feed") return prisoner.TryGet<PersonalLifeComponent>(out var needs) && needs.Satiety <= 65;
            return task == "medicine" && life.HerbDoses < 5 && prisoner.TryGet<CombatVitalsComponent>(out var hp) &&
                prisoner.TryGet<AttributesComponent>(out var attrs) &&
                (hp.CurrentHp < attrs.GetFinal(XianXia.Core.Attributes.AttributeId.MaxHp) || CombatInjuryService.HasRecoverable(prisoner));
        }

        static ContentInteractionContext Context(SimulationWorld world, EntityId actor)
        {
            var prisoner = HuangcunSurvivalService.Prisoner(world);
            return ContentInteractionContext.ForNpc(actor, prisoner.Id, prisoner.DefinitionId.ToString());
        }

        public static bool CanAccept(SimulationWorld world, EntityId actor, string task)
        {
            if (ValidateVisit(world, actor, true).IsFailure || !Needs(world, task) ||
                world.ContentDaily.IsMarkedToday(DailyKey(task), world.Tick) ||
                !world.Quests.TryGetSpec(QuestId(task), out _)) return false;
            return !world.Quests.TryGetForIssuer(QuestId(task), HuangcunSurvivalService.Prisoner(world).Id, out var runtime) ||
                runtime.Status == QuestStatus.Inactive || runtime.Status == QuestStatus.Completed;
        }

        public static Result Accept(SimulationWorld world, EntityId actor, string task)
        {
            if (!CanAccept(world, actor, task)) return Fail("同伴目前没有这项新请求；已有请求可以继续完成。");
            // Reuse the same issuer-bound request next day. No unlimited daily instance history.
            world.Quests.TryGetForIssuer(QuestId(task), HuangcunSurvivalService.Prisoner(world).Id, out var previous);
            var oldStatus = previous?.Status ?? QuestStatus.Inactive;
            if (previous != null && previous.Status == QuestStatus.Completed) previous.Status = QuestStatus.Inactive;
            var result = new QuestService().TryAcceptFromTarget(world, QuestId(task), Context(world, actor));
            if (result.IsFailure) { if (previous != null) previous.Status = oldStatus; return result; }
            world.ContentCounters.Set(ProgressKey(task), 0);
            return Result.Success();
        }

        public static void RecordDelivered(SimulationWorld world, EntityId actor, string task)
        {
            world.ContentDaily.MarkToday(DailyKey(task), world.Tick);
            if (!world.Quests.TryGetForIssuer(QuestId(task), HuangcunSurvivalService.Prisoner(world).Id, out var runtime) ||
                runtime.Status != QuestStatus.Active) return;
            world.ContentCounters.Set(ProgressKey(task), 1);
            var service = new QuestService();
            service.Evaluate(world, actor);
            if (runtime.Status == QuestStatus.ReadyToClaim) service.TryClaimRewards(world, runtime.QuestInstanceId, actor);
        }

        public static void CloseUnavailableRequests(SimulationWorld world)
        {
            var prisoner = HuangcunSurvivalService.Prisoner(world);
            var alive = prisoner != null && prisoner.TryGet<LifecycleComponent>(out var life) && !life.IsDead && !life.IsRemoved;
            if (Applies(world) && alive) return;
            if (prisoner == null) return;
            foreach (var task in new[] { "feed", "medicine" })
                if (world.Quests.TryGetForIssuer(QuestId(task), prisoner.Id, out var request) && request.Status == QuestStatus.Active)
                    new QuestService().TryAbandon(world, request.QuestInstanceId, request.AcceptedByEntityId);
        }

        static Result Fail(string message) => Result.Failure(ErrorCode.ActionCannotStart, message);
    }
}
