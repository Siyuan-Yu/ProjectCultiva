using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Npc;

namespace XianXia.Unity.Host
{
    public sealed partial class HostDialogueController
    {
        EntityId _managedPrisonVisitor, _managedPrisoner;

        bool TryStartPrisonManagement(PlayableHostSession session, EntityId actor, EntityId target)
        {
            if (PrisonManagementService.ValidateVisit(session.World, actor, target).IsFailure) return false;
            Clear(); _managedPrisonVisitor = actor; _managedPrisoner = target;
            BuildPrisonManagement(session);
            return true;
        }

        void BuildPrisonManagement(PlayableHostSession session, string feedback = null)
        {
            var world = session.World;
            if (!world.Entities.TryGet(_managedPrisoner, out var prisoner)) { Clear(); return; }
            prisoner.TryGet<LifecycleComponent>(out var life);
            prisoner.TryGet<CombatVitalsComponent>(out var hp);
            var resistance = PrisonManagementService.Resistance(world, prisoner.Id);
            Model.IsActive = Model.IsPrisonManagement = true;
            Model.PageKey = "prison-management:" + prisoner.Id;
            Model.SpeakerName = prisoner.DisplayName;
            Model.Body = life?.IsIncapacitated == true ? "（他暂时无法应答，仍可以照料、查看身体状况。）" :
                resistance > 65 ? "我还不信任你。先让我吃饱，把伤养好，其他的以后再说。" :
                resistance > 40 ? "这些天的饭和药，我记着。但要我归附，还需要些时间。" :
                resistance > 20 ? "我愿意听你说，不过现在还不能答应加入。" : "这段时间的照料我看到了。你愿意接纳我，让我离开牢房吗？";
            Model.Body += "\n" + PrisonManagementService.Describe(world, prisoner.Id) +
                "\n生命 " + (hp?.CurrentHp ?? 0) + " · 有效药量 " + (life?.HerbDoses ?? 0) + "/5" +
                "\n身体伤势：" + CombatInjuryService.Describe(prisoner);
            if (!string.IsNullOrEmpty(feedback)) Model.Body += "\n" + feedback;
            Model.Choices.Clear();
            Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = "body", Label = "查看身体状况" });
            Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = "feed", Label = "亲自送饭（私人粮食1）" });
            Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = "medicine", Label = "亲自送药（私人灵草1，濒危可补足药量）" });
            Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = "recruit", Label = "劝说归附，加入我的势力" });
            Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = "release", Label = "下令释放，不要求归附" });
            Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = "leave", Label = "你先休息，改日再谈" });
        }

        bool SelectPrisonManagementChoice(string choice, PlayableHostSession session, PlayableHostBootstrap bootstrap)
        {
            if (choice == "leave") { Clear(); return true; }
            var access = PrisonManagementService.ValidateVisit(session.World, _managedPrisonVisitor, _managedPrisoner);
            if (access.IsFailure) { ShowFallback("牢房管理", access.Error.Message); return false; }
            if (choice == "body") { var target = _managedPrisoner; Clear(); bootstrap?.CharacterSheetPanel?.OpenFor(target); return true; }
            if (choice == "feed" || choice == "medicine")
            {
                var care = PrisonManagementService.GivePrivateCare(session.World, _managedPrisonVisitor, _managedPrisoner, choice == "medicine");
                bootstrap?.DispatchDrainedEvents();
                BuildPrisonManagement(session, care.IsSuccess ? "已完成照料；伤势按原医疗规则逐渐恢复。" : care.Error.Message);
                return care.IsSuccess;
            }
            if (choice == "recruit" || choice == "release")
            {
                var name = Model.SpeakerName;
                var result = choice == "recruit" ? MortalCivilianService.Recruit(session.World, _managedPrisonVisitor, _managedPrisoner) :
                    MortalCivilianService.Release(session.World, _managedPrisoner);
                if (result.IsFailure) { BuildPrisonManagement(session, result.Error.Message); return false; }
                bootstrap?.MoveController?.CancelPresentationMovementPublic(_managedPrisoner);
                bootstrap?.DispatchDrainedEvents();
                ShowFallback(name, choice == "recruit" ? "我愿意加入。\n已解除拘押并加入玩家势力，位置与伤势保留，未加入当前队伍；可另行安排工作或休养。" :
                    "已解除拘押；位置与伤势保留，没有要求其加入势力。");
                return true;
            }
            return false;
        }
    }
}
