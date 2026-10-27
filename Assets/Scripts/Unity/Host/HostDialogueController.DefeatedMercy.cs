using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Inventory;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    public sealed partial class HostDialogueController
    {
        SimulationWorld _mercyWorld;
        EntityId _mercyActor, _mercyTarget;
        int _mercyEpisode;
        string _mercyEncounterId;
        string _mercyPage = "main";

        public bool TryStartDefeatedMercy(PlayableHostSession session, EntityId actor, EntityId target)
        {
            if (session?.World == null) return false;
            var episode = DefeatedMercyService.Episode(session.World, target);
            if (DefeatedMercyService.Validate(session.World, actor, target, episode).IsFailure) return false;
            Clear();
            _mercyWorld = session.World; _mercyActor = actor; _mercyTarget = target; _mercyEpisode = episode;
            _mercyEncounterId = session.World.Strategic.CharacterEncounter.EncounterId;
            _mercyPage = "main";
            BuildDefeatedMercy();
            return true;
        }
        void BuildDefeatedMercy(string feedback = null)
        {
            if (_mercyWorld == null || !_mercyWorld.Entities.TryGet(_mercyTarget, out var target)) { Clear(); return; }
            Model.IsActive = Model.IsDefeatedMercy = true;
            Model.PageKey = "combat-mercy:" + _mercyTarget.Value + ":" + _mercyEpisode + ":" + _mercyPage;
            Model.SpeakerName = target.DisplayName;
            Model.Body = "饶我一命……若有能交出的财物，我都愿意给你。\n" +
                CombatInjuryService.Describe(target) + "\n暂且停手，对方仍需养伤。";
            if (!string.IsNullOrEmpty(feedback)) Model.Body += "\n" + feedback;
            Model.Choices.Clear();
            var offers = DefeatedMercyService.ItemOffers(_mercyWorld, _mercyTarget);
            var wallet = _mercyWorld.Commerce.GetCharacterWallet(_mercyWorld, _mercyTarget);
            var claimed = DefeatedMercyService.HasClaimedAssets(_mercyWorld, _mercyTarget, _mercyEpisode);
            if (_mercyPage == "main")
            {
                var hasMoney = wallet.Low > 0 || wallet.Mid > 0 || wallet.High > 0;
                Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = "spare", Label = "放你一马" });
                Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = "open-items", Label = claimed ? "索取物品（本次已索取财物）" : offers.Count > 0 ? "留下物品" : "索取物品（无可交付物品）", Enabled = !claimed && offers.Count > 0 });
                Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = "open-money", Label = claimed ? "索取灵石（本次已索取财物）" : hasMoney ? "留下灵石" : "索取灵石（对方身无分文）", Enabled = !claimed && hasMoney });
                Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = "refuse", Label = "不接受求饶，继续攻击" });
                Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = "cancel", Label = "暂不处置" });
                return;
            }
            if (_mercyPage == "items")
            {
                foreach (var offer in offers)
                {
                    var item = DefeatedMercyService.ItemId(_mercyWorld, _mercyTarget, offer);
                    Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = "item:" + offer,
                        Label = _mercyWorld.InventoryCatalog.GetName(item) + " ×1，收下后暂且停手", Enabled = !claimed && _mercyWorld.Inventory.CanAddAll(item, 1) });
                }
            }
            else for (var i = 0; i < 3; i++)
            {
                var grade = (SpiritStoneGrade)i; var amount = wallet.Balance(grade);
                if (amount <= 0) continue;
                var name = i == 0 ? "下品" : i == 1 ? "中品" : "上品";
                Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = "money:" + i,
                    Label = name + "灵石 ×" + amount + "，收下后暂且停手", Enabled = !claimed && _mercyWorld.Commerce.PlayerWallet.CanAdd(grade, amount) });
            }
            Model.Choices.Add(new HostDialogueChoiceLine { ChoiceId = "back", Label = "返回，不索取" });
        }
        bool SelectDefeatedMercy(string choice, PlayableHostSession session, PlayableHostBootstrap bootstrap)
        {
            if (choice == "cancel" || choice == "spare") { Clear(); return true; }
            if (!ReferenceEquals(_mercyWorld, session.World) ||
                session.World.Strategic.CharacterEncounter?.EncounterId != _mercyEncounterId)
            { Clear(); return false; }
            if (choice == "open-items" || choice == "open-money" || choice == "back")
            {
                _mercyPage = choice == "open-items" ? "items" : choice == "open-money" ? "money" : "main";
                BuildDefeatedMercy(); return true;
            }
            var result = DefeatedMercyService.Resolve(_mercyWorld, _mercyActor, _mercyTarget, _mercyEpisode, choice);
            if (result.IsFailure) { BuildDefeatedMercy(result.Error.Message); return true; }
            HostLifeStatePresentationSync.RefreshDownedOrDead(session.World, bootstrap.ViewSpawner,
                bootstrap.MoveController, _mercyTarget, captureOrdinaryPlacement: false);
            session.World.Events.Publish(XianXia.Core.Events.EventType.ActionCompleted, session.World.Tick,
                actor: _mercyActor, target: _mercyTarget, payload: "combatMercy:" + choice);
            bootstrap.DispatchDrainedEvents();
            Clear(); return true;
        }
    }
}
