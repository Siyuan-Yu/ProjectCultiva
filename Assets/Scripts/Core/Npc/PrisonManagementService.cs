using System;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Construction;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Npc
{
    public static class PrisonManagementService
    {
        static Result Fail(string message) => Result.Failure(ErrorCode.InvalidOperation, message);
        static string Key(SimulationWorld w, EntityId id, string part) =>
            "prison-management:" + w.Strategic.PlayerFactionId + ":" + id.Value + ":" + part;
        static int Day(SimulationWorld w) => (int)(w.Tick.Value / WorldTick.TicksPerDay);
        static int Get(SimulationWorld w, EntityId id, string part) => w.ContentCounters.Get(Key(w, id, part));
        static void Set(SimulationWorld w, EntityId id, string part, int value) => w.ContentCounters.Set(Key(w, id, part), value);
        public static bool IsOriginalCompanion(SimulationWorld w, EntityId id) => w.HuangcunSurvival.Enabled && w.HuangcunSurvival.PrisonerId == id.Value;

        public static bool TryManagedPrisoner(SimulationWorld w, EntityId id, out OutdoorConstructedAssetState room)
        {
            room = null;
            return w != null && PrisonConfinementService.TryGetRoom(w, id, out room) &&
                PrisonConfinementService.IsInside(w, room, id) &&
                PrisonWallBreachService.TryOwner(w, room, out var site) &&
                site.OwnerFactionId == w.Strategic.PlayerFactionId;
        }

        public static int Satiety(SimulationWorld w, EntityId id)
        {
            if (IsOriginalCompanion(w, id) && w.Entities.TryGet(id, out var original) &&
                original.TryGet<PersonalLifeComponent>(out var needs)) return needs.Satiety;
            return w.Civilians.TryGet(id, out var civilian) ? civilian.Satiety : 0;
        }

        public static bool NeedsMedicine(SimulationWorld w, EntityId id) =>
            w.Entities.TryGet(id, out var entity) && entity.TryGet<LifecycleComponent>(out var life) &&
            !life.IsDead && !life.IsRemoved && life.HerbDoses < 5 &&
            (life.IsIncapacitated || CombatInjuryService.HasRecoverable(entity) || entity.TryGet<CombatVitalsComponent>(out var hp) &&
                entity.TryGet<AttributesComponent>(out var attrs) && hp.CurrentHp < attrs.GetFinal(AttributeId.MaxHp));

        public static bool MedicineDeliveredToday(SimulationWorld w, EntityId id) =>
            w.ContentDaily.IsMarkedToday(Key(w, id, "medicine"), w.Tick);
        public static bool NeedsEmergencyMedicine(SimulationWorld w, EntityId id) =>
            w.Entities.TryGet(id, out var entity) && entity.TryGet<LifecycleComponent>(out var life) &&
            life.IsDying && MedicalRecoveryService.ResolveRateMilli(w, entity) <= 0;
        public static bool ShouldSendMedicine(SimulationWorld w, EntityId id) =>
            NeedsMedicine(w, id) && (!MedicineDeliveredToday(w, id) || NeedsEmergencyMedicine(w, id));

        public static void BeginDetention(SimulationWorld w, EntityId id)
        {
            if (IsOriginalCompanion(w, id)) return;
            CharacterFactionLoyaltyService.TryGetLoyalty(w, id, out var loyalty);
            Set(w, id, "initialized", 1); Set(w, id, "resistance", Math.Max(50, loyalty));
            Set(w, id, "day", Day(w)); Set(w, id, "first-day", Day(w)); ResetFacts(w, id);
        }

        static void ResetFacts(SimulationWorld w, EntityId id)
        {
            foreach (var part in new[] { "fed", "treated", "neglected", "abused", "needs-medicine" }) Set(w, id, part, 0);
        }

        static void AdvanceDay(SimulationWorld w, EntityId id)
        {
            if (IsOriginalCompanion(w, id)) return;
            if (Get(w, id, "initialized") == 0) { BeginDetention(w, id); return; }
            var today = Day(w); var previous = Get(w, id, "day");
            if (today == previous) return;
            var resistance = Get(w, id, "resistance");
            if (today == previous + 1 && previous > Get(w, id, "first-day"))
            {
                var change = Get(w, id, "abused") > 0 ? 10 :
                    Get(w, id, "fed") == 0 || Get(w, id, "neglected") > 0 ||
                    Get(w, id, "needs-medicine") > 0 && Get(w, id, "treated") == 0 ? 5 : -5;
                CharacterFactionLoyaltyService.TryGetLoyalty(w, id, out var loyalty);
                resistance = Math.Max(loyalty >= 80 ? 30 : 0, Math.Min(100, resistance + change));
                Set(w, id, "resistance", resistance);
            }
            Set(w, id, "day", today); ResetFacts(w, id);
        }

        public static void Tick(SimulationWorld w)
        {
            foreach (var entity in w.Entities.All)
            {
                if (IsOriginalCompanion(w, entity.Id) || !TryManagedPrisoner(w, entity.Id, out _)) continue;
                AdvanceDay(w, entity.Id);
                if (Satiety(w, entity.Id) < 15) Set(w, entity.Id, "neglected", 1);
                if (NeedsMedicine(w, entity.Id) && !MedicineDeliveredToday(w, entity.Id)) Set(w, entity.Id, "needs-medicine", 1);
            }
        }

        public static void RecordCare(SimulationWorld w, EntityId id, bool medicine)
        {
            if (!TryManagedPrisoner(w, id, out _)) return;
            AdvanceDay(w, id);
            if (!IsOriginalCompanion(w, id)) Set(w, id, medicine ? "treated" : "fed", 1);
            if (medicine) w.ContentDaily.MarkToday(Key(w, id, "medicine"), w.Tick);
        }

        public static void RecordAttack(SimulationWorld w, EntityId id)
        {
            if (IsOriginalCompanion(w, id) || !TryManagedPrisoner(w, id, out _)) return;
            AdvanceDay(w, id); Set(w, id, "abused", 1);
        }

        public static int Resistance(SimulationWorld w, EntityId id)
        {
            if (IsOriginalCompanion(w, id)) return 0;
            if (TryManagedPrisoner(w, id, out _)) AdvanceDay(w, id);
            return Get(w, id, "resistance");
        }

        public static string Attitude(int resistance) => resistance > 65 ? "强烈抗拒" :
            resistance > 40 ? "心存戒备" : resistance > 20 ? "愿意交谈" : "愿意考虑归附";

        public static Result ValidateVisit(SimulationWorld w, EntityId actor, EntityId target)
        {
            if (!TryManagedPrisoner(w, target, out var room)) return Fail("这名囚犯不在玩家管理的牢房内。");
            if (w.LocalMap.IsInInterior || CharacterEncounterService.BlocksOrdinaryContinuousSurface(w) ||
                w.Strategic.PlayerPartyContext?.IsMember(actor) != true ||
                !PlayerPartyRuntime.CanPlayerControlCharacter(w, actor) ||
                !w.Entities.TryGet(actor, out var entity) || !CombatLifeStateService.CanFight(entity))
                return Fail("当前不能进行牢房管理对话。");
            if (!PrisonConfinementService.IsInside(w, room, actor) ||
                !CharacterWorldPresenceQuery.TryResolve(w, actor, out var from) ||
                !CharacterWorldPresenceQuery.TryResolve(w, target, out var to) ||
                !w.SurfaceSpatial.TryGet(room.SurfaceId, out var metric) ||
                WorldVec2.Distance(from.WorldPosition, to.WorldPosition) > metric.CellSize * 3)
                return Fail("先进入牢房并靠近这名囚犯。");
            return Result.Success();
        }

        public static Result ValidateRecruit(SimulationWorld w, EntityId actor, EntityId target)
        {
            if (IsOriginalCompanion(w, target)) return Fail("同伴乙无需招募，请直接下令释放。");
            var access = ValidateVisit(w, actor, target); if (access.IsFailure) return access;
            if (!w.Entities.TryGet(target, out var entity) || !PrisonConfinementService.CanWalk(w, entity))
                return Fail("对方尚未清醒，不能应答归附邀请。");
            if (w.Civilians.TryGet(target, out var sleeper) && sleeper.SleepPhase == CivilianSleepPhase.SleepingAtResidence)
                return Fail("对方正在睡眠，等他正常起床后再谈归附。");
            if (CharacterFactionLoyaltyService.TryGetLoyalty(w, target, out var loyalty) && loyalty >= 80)
                return Fail("对方对原势力立场坚定，仅靠关押供养不能使其归附。");
            if (Resistance(w, target) > 20) return Fail("对方仍有抵抗，需要持续照料，并非反复劝说就能归附。");
            var attitude = w.Relationships.GetAttitude(target, actor);
            if (w.Relationships.HasPrivateHostility(target, actor) || attitude.Trust < -20 || attitude.Grudge >= 50)
                return Fail("对方因与你的旧怨或不信任，仍不愿归附。");
            return Result.Success();
        }

        public static bool Deliver(SimulationWorld w, EntityId target, string resource)
        {
            if (!TryManagedPrisoner(w, target, out _) || !w.Entities.TryGet(target, out var entity)) return false;
            var medicine = resource == MortalCivilianService.HerbResourceId;
            if (medicine)
            {
                if (!ShouldSendMedicine(w, target)) return false;
                entity.TryGet<LifecycleComponent>(out var life); life.HerbDoses++;
            }
            else
            {
                if (!w.InventoryCatalog.HasTag(resource, "food") || Satiety(w, target) > 65) return false;
                if (IsOriginalCompanion(w, target))
                { var needs = PersonalLifeService.Ensure(w, entity); needs.Satiety += 55; needs.Clamp(); }
                else if (w.Civilians.TryGet(target, out var civilian))
                { civilian.Satiety += 55; civilian.AteToday = true; civilian.Clamp(); }
                else return false;
            }
            RecordCare(w, target, medicine);
            return true;
        }

        public static Result GivePrivateCare(SimulationWorld w, EntityId actor, EntityId target, bool medicine)
        {
            var access = ValidateVisit(w, actor, target); if (access.IsFailure) return access;
            var resource = medicine ? MortalCivilianService.HerbResourceId : MortalCivilianService.GrainResourceId;
            if (w.Inventory.GetCount(resource) < 1) return Fail(medicine ? "私人背包缺少灵草。" : "私人背包缺少粮食。");
            if (!Deliver(w, target, resource)) return Fail("暂时无需补送，或今天已送过药。");
            w.Inventory.TryRemoveAll(resource, 1);
            w.Events.Publish(EventType.PartyInventoryChanged, w.Tick, actor: actor, payload: "prison-care");
            return Result.Success();
        }

        public static string Describe(SimulationWorld w, EntityId id)
        {
            if (!TryManagedPrisoner(w, id, out var room)) return "当前不在玩家监管范围内。";
            var keeper = "无人任职监管";
            foreach (var pair in w.Civilians.All)
                if (pair.Value.Profession == MortalProfession.Warden && pair.Value.Disposition == CivilianDisposition.Normal &&
                    w.Entities.TryGet(pair.Key, out var worker) && worker.TryGet<FactionMembershipComponent>(out var faction) &&
                    faction.FactionId == w.Strategic.PlayerFactionId &&
                    CharacterWorldPresenceQuery.TryResolve(w, pair.Key, out var at) && at.HasWorldPosition && at.SurfaceId == room.SurfaceId &&
                    MortalCivilianQuery.IsWithinLocalContinuity(w, at.SurfaceId, at.WorldPosition,
                        new WorldVec2(room.WorldX, room.WorldY)))
                { keeper = worker.DisplayName + "：" + (string.IsNullOrEmpty(pair.Value.WardenStatus) ? "按工作时段照管本地囚犯" : pair.Value.WardenStatus); break; }
            return (IsOriginalCompanion(w, id) ? "自己人，无需招募。" : "抵抗 " + Resistance(w, id) + "/100 · " + Attitude(Resistance(w, id))) +
                (!IsOriginalCompanion(w, id) && CharacterFactionLoyaltyService.TryGetLoyalty(w, id, out var loyalty) && loyalty >= 80 ?
                    " · 对原势力立场坚定，不能仅靠供养招募" : "") +
                "\n监管：" + keeper + "\n饱食 " + Satiety(w, id) + " · 今日送药：" + (MedicineDeliveredToday(w, id) ? "已完成" : "未送／无需") +
                (IsOriginalCompanion(w, id) ? "" : " · 今日送饭：" + (Get(w, id, "fed") > 0 ? "已完成" : "尚未完成"));
        }
    }
}
