using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Inventory;
using XianXia.Core.Npc;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Combat
{
    /// <summary>Repeatable disposition, at most one asset claim per defeat. No invented inventory or healing.</summary>
    public static class DefeatedMercyService
    {
        static string EpisodeKey(EntityId target) => "combat:defeat:" + target.Value;
        static string ResultKey(EntityId target, int episode) => "combat:mercy:" + target.Value + ":" + episode;
        public static int Episode(SimulationWorld world, EntityId target) => world.ContentCounters.Get(EpisodeKey(target));
        public static bool HasClaimedAssets(SimulationWorld world, EntityId target, int episode)
        {
            var result = world.ContentCounters.Get(ResultKey(target, episode));
            return result == 2 || result == 3;
        }
        public static void RecordDefeat(SimulationWorld world, EntityId target) =>
            world.ContentCounters.Set(EpisodeKey(target), Math.Min(int.MaxValue - 1, Episode(world, target)) + 1);
        public static bool IsEncounterOpponent(SimulationWorld world, EntityId actor, EntityId target)
        {
            var state = world?.Strategic?.CharacterEncounter;
            var a = state?.Find(actor.Value); var b = state?.Find(target.Value);
            return a != null && b != null && a.Enemy != b.Enemy &&
                (state.Phase == CharacterEncounterPhase.Active || state.Phase == CharacterEncounterPhase.ReadyToEnd);
        }
        public static Result Validate(SimulationWorld world, EntityId actor, EntityId target, int episode)
        {
            ReconcileOldAutomaticWithdrawals(world);
            if (!IsEncounterOpponent(world, actor, target) ||
                world.Strategic.PlayerPartyContext?.IsPlayerControllableMember(actor) != true ||
                world.Strategic.PlayerPartyContext.ActiveCharacterId != actor ||
                !world.Entities.TryGet(actor, out var acting) || !CombatLifeStateService.CanFight(acting) ||
                MortalTransportConstraintService.ValidateAttack(world, actor).IsFailure ||
                !world.Entities.TryGet(target, out var victim) ||
                !victim.TryGet<LifecycleComponent>(out var life) || life.State == LifecycleState.Captured ||
                !(life.IsUnconscious || life.Convalescing))
                return Fail("需由当前主控靠近本场被击败的敌人，才能进行战败处置。");
            var state = world.Strategic.CharacterEncounter;
            var a = state.Find(actor.Value); var b = state.Find(target.Value);
            var dx = a.TacticalX - b.TacticalX; var dy = a.TacticalY - b.TacticalY;
            var range = Math.Max(.1f, world.OpeningArea.CellSize) * 3f;
            if (dx * dx + dy * dy > range * range) return Fail("需要先靠近败者。");
            if (b.Withdrawn || episode != Episode(world, target))
                return Fail("目标战败轮次或参战状态已变化。");
            return Result.Success();
        }
        public static List<string> ItemOffers(SimulationWorld world, EntityId target)
        {
            var offers = new List<string>();
            if (!world.Civilians.TryGet(target, out var carried)) return offers;
            if (carried.MedicCarryingHerb) offers.Add("medicine");
            if (!string.IsNullOrEmpty(carried.WardenCarriedResourceId)) offers.Add("warden");
            return offers;
        }
        public static string ItemId(SimulationWorld world, EntityId target, string offer)
        {
            if (!world.Civilians.TryGet(target, out var carried)) return null;
            if (offer == "medicine" && carried.MedicCarryingHerb) return MortalCivilianService.HerbResourceId;
            if (offer == "warden" && !string.IsNullOrEmpty(carried.WardenCarriedResourceId)) return carried.WardenCarriedResourceId;
            return null;
        }
        public static Result Resolve(SimulationWorld world, EntityId actor, EntityId target, int episode, string choice)
        {
            if (string.IsNullOrEmpty(choice)) return Fail("无效战败处置。");
            var valid = Validate(world, actor, target, episode); if (valid.IsFailure) return valid;
            if (choice == "spare") return Result.Success();
            if (choice == "refuse")
            {
                var hit = new MeleeCombatService().ApplyDispositionRefusal(world, actor, target, out _, out var defeated);
                if (hit.IsFailure) return hit;
                if (!defeated) return Fail("本场非致命约束不允许补击转弥留。");
                PrisonManagementService.RecordAttack(world, target);
                if (!HasClaimedAssets(world, target, episode)) world.ContentCounters.Set(ResultKey(target, episode), 4);
                return Result.Success();
            }
            if ((choice.StartsWith("money:", StringComparison.Ordinal) || choice.StartsWith("item:", StringComparison.Ordinal)) &&
                HasClaimedAssets(world, target, episode))
                return Fail("本次战败已索取过财物，仍可选择其他处置。");
            if (choice.StartsWith("money:", StringComparison.Ordinal))
            {
                if (!int.TryParse(choice.Substring(6), out var n) || n < 0 || n > 2) return Fail("无效灵石品阶。");
                var grade = (SpiritStoneGrade)n; var wallet = world.Commerce.GetCharacterWallet(world, target);
                var amount = wallet.Balance(grade);
                if (amount <= 0 || !SpiritStoneWallet.TryTransfer(wallet, world.Commerce.PlayerWallet, grade, amount))
                    return Fail("对方没有该品阶灵石，或接收钱包已达上限；未索取。");
            }
            else if (choice.StartsWith("item:", StringComparison.Ordinal))
            {
                var offer = choice.Substring(5); var item = ItemId(world, target, offer);
                if (item == null || !world.Inventory.TryAddAll(item, 1)) return Fail("对方已无该携带物或背包空间不足；未索取。");
                world.Civilians.TryGet(target, out var carried);
                if (offer == "medicine")
                {
                    carried.MedicCarryingHerb = false; carried.MedicSourceSiteId = carried.MedicSourceRoomId = "";
                    carried.MedicSourcePartyMemberId = EntityId.None; carried.MedicTargetId = EntityId.None;
                    carried.MedicTreatmentSeconds = 0;
                }
                else
                {
                    carried.WardenCarriedResourceId = carried.WardenSourceSiteId = carried.WardenSourceRoomId = "";
                    carried.WardenTargetId = EntityId.None;
                }
            }
            else return Fail("无效战败处置。");
            world.ContentCounters.Set(ResultKey(target, episode), choice.StartsWith("item:", StringComparison.Ordinal) ? 2 : 3);
            return Result.Success();
        }
        public static void ReconcileOldAutomaticWithdrawals(SimulationWorld world)
        {
            var state = world?.Strategic?.CharacterEncounter;
            if (state == null || state.Phase == CharacterEncounterPhase.Committed) return;
            foreach (var p in state.Participants)
                if (p.WithdrawalKind == "Spared" || p.Withdrawn && (p.WithdrawalKind == "Flee" || p.WithdrawalKind == "Surrender"))
                {
                    var target = new EntityId(p.CharacterId);
                    var key = ResultKey(target, Episode(world, target));
                    if (p.WithdrawalKind == "Spared" && world.ContentCounters.Get(key) == 1)
                        world.ContentCounters.Set(key, 0);
                    p.Withdrawn = false; p.WithdrawalKind = ""; p.TargetId = 0;
                }
        }
        static Result Fail(string message) => Result.Failure(ErrorCode.InvalidOperation, message);
    }
}
