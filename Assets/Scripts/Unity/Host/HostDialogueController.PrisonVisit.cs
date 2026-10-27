using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;
using XianXia.Core.Orders;

namespace XianXia.Unity.Host
{
    public sealed partial class HostDialogueController
    {
        EntityId _prisonVisitor;

        public bool TryStartPrisonVisit(PlayableHostSession session, EntityId actor)
        {
            if (session != null && XianXia.Core.Social.SupervisorAngerDriver.TryBeginPrisonTrespass(session.World, session.Loop, actor))
                return false;
            if (session == null || !session.IsInitialized ||
                HuangcunPrisonVisitService.ValidateVisit(session.World, actor).IsFailure) return false;
            Clear();
            _prisonVisitor = actor;
            BuildPrisonVisit(session);
            return true;
        }

        void BuildPrisonVisit(PlayableHostSession session, string feedback = null)
        {
            var world = session.World;
            var prisoner = HuangcunSurvivalService.Prisoner(world);
            if (prisoner == null) { Clear(); return; }
            prisoner.TryGet<LifecycleComponent>(out var life);
            prisoner.TryGet<CombatVitalsComponent>(out var hp);
            prisoner.TryGet<PersonalLifeComponent>(out var needs);
            prisoner.TryGet<AttributesComponent>(out var attrs);
            var maxHp = attrs?.GetFinal(AttributeId.MaxHp) ?? 1;
            var speech = life != null && !life.IsIncapacitated;
            Model.IsActive = Model.IsPrisonVisit = true;
            Model.PageKey = "prison-visit:" + prisoner.Id;
            Model.SpeakerName = prisoner.DisplayName;
            Model.Body = !speech ? "（他躺着，暂时无法应答。你仍可以查看伤势，给他送饭、敷药。）" :
                needs != null && needs.Satiety <= 35 ? "你来了……他们给的稀粥只能勉强撑着。若有余粮，能给我带一份饭吗？" :
                hp != null && hp.CurrentHp < maxHp || CombatInjuryService.HasRecoverable(prisoner) ? "有你送来的饭，我撑得住。伤还没好，能再替我找些灵草吗？药得慢慢敷，不会一下就好。" :
                prisoner.TryGet<CombatInjuryComponent>(out var injury) && injury.HasAny ?
                "我已经缓过来了，但身上的残伤还在，普通灵草治不好。出去的事，我们再找机会。" :
                "身上的伤已经养好了，多亏你照应。我还被关着，出去的事，我们再找机会。";
            if (speech && needs != null && needs.Satiety <= 65 && needs.Satiety > 35)
                Model.Body += "\n有余粮时也替我留一份，不必为了我把自己饿着。";
            Model.Body += "\n生命 " + (hp?.CurrentHp ?? 0) + "/" + maxHp + " · 饱食 " + (needs?.Satiety ?? 0) +
                " · 有效药量 " + (life?.HerbDoses ?? 0) + "/5 · 净恢复 " +
                (MedicalRecoveryService.ResolveRateMilli(world, prisoner) / 1000f).ToString("+0.0;-0.0;0.0") + " HP/秒。";
            Model.Body += "\n身体伤势：" + CombatInjuryService.Describe(prisoner);
            if (!string.IsNullOrEmpty(feedback)) Model.Body += "\n" + feedback;
            if (PrisonManagementService.TryManagedPrisoner(world, prisoner.Id, out _))
                Model.Body += "\n" + PrisonManagementService.Describe(world, prisoner.Id);
            Model.Choices.Clear();
            Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = "prison:body", Label = "查看你的身体状况" });
            AddCareChoices(session, "feed", "送饭", "粮食 1");
            AddCareChoices(session, "medicine", "送药", "灵草 1");
            if (!XianXia.Core.World.Strategic.HuangcunOpeningAccess.IsLocked(world) &&
                PrisonManagementService.TryManagedPrisoner(world, prisoner.Id, out _))
                Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = "prison:deliver:force_release", Label = "下令释放，让你离开牢房" });
            Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = "prison:leave", Label = "你先休息，我改日再来" });
        }

        void AddCareChoices(PlayableHostSession session, string task, string label, string cost)
        {
            var world = session.World;
            var prisoner = HuangcunSurvivalService.Prisoner(world);
            if (HuangcunPrisonVisitService.CanAccept(world, _prisonVisitor, task))
                Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = "prison:accept:" + task, Label = "接下请求：" + label });
            var pending = world.Quests.TryGetForIssuer(HuangcunPrisonVisitService.QuestId(task), prisoner.Id, out var quest) &&
                quest.Status == QuestStatus.Active;
            var check = HuangcunSurvivalService.Validate(world, _prisonVisitor, task);
            Model.Choices.Add(new HostDialogueChoiceLine {
                ChoiceId = "prison:deliver:" + task,
                Label = (pending ? "完成请求：" : "主动照料：") + label + "（" + cost + "）" +
                    (check.IsFailure ? "\n" + check.Error.Message : ""),
                Enabled = check.IsSuccess
            });
        }

        bool SelectPrisonChoice(string choice, PlayableHostSession session, PlayableHostBootstrap bootstrap)
        {
            if (choice == "prison:leave") { Clear(); return true; }
            if (XianXia.Core.Social.SupervisorAngerDriver.TryBeginPrisonTrespass(session.World, session.Loop, _prisonVisitor))
            { Clear(); session.ManualPaused = false; return false; }
            var access = HuangcunPrisonVisitService.ValidateVisit(session.World, _prisonVisitor);
            if (access.IsFailure) { ShowFallback("探视结束", access.Error.Message); return false; }
            if (choice == "prison:body")
            {
                var prisoner = HuangcunSurvivalService.Prisoner(session.World);
                Clear();
                bootstrap?.CharacterSheetPanel?.OpenFor(prisoner.Id);
                return true;
            }
            if (choice.StartsWith("prison:accept:"))
            {
                var task = choice.Substring("prison:accept:".Length);
                var result = HuangcunPrisonVisitService.Accept(session.World, _prisonVisitor, task);
                BuildPrisonVisit(session, result.IsSuccess ? "请求已记入任务日志。你先照顾好自己，有了物资再带来就好。" : result.Error.Message);
                bootstrap?.DispatchDrainedEvents();
                return result.IsSuccess;
            }
            if (choice.StartsWith("prison:deliver:"))
            {
                var task = choice.Substring("prison:deliver:".Length);
                var result = HuangcunSurvivalService.Validate(session.World, _prisonVisitor, task);
                if (result.IsSuccess)
                {
                    bootstrap?.MoveController?.CancelPresentationMovementPublic(_prisonVisitor);
                    session.Loop.StopSubject(_prisonVisitor);
                    result = session.Loop.EnqueueOrder(new Order(session.Loop.AllocateOrderId(), _prisonVisitor,
                        OrderType.HuangcunSurvival, OrderSource.Player,
                        waitTicks: HuangcunSurvivalService.Duration(task), targetRef: task));
                }
                if (result.IsFailure) { BuildPrisonVisit(session, result.Error.Message); return false; }
                bootstrap?.MoveController?.CancelPresentationMovementPublic(HuangcunSurvivalService.Prisoner(session.World).Id);
                session.World.HuangcunSurvival.Message = task == "force_release" ? "正在办理释放；同伴获释后伤势保留，可自由离开牢房。" :
                    "正在" + (task == "feed" ? "给同伴递饭" : "替同伴敷药") + "；完成后才扣物资，被发现、离开牢房或远离同伴会中断。";
                Clear();
                session.ManualPaused = false;
                return true;
            }
            return false;
        }
    }
}
