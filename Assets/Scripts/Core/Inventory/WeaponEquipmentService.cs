using System;
using XianXia.Core.Actions;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Npc;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Inventory
{
    public enum WeaponKind { Hoe, Axe, Pick, Sword, Saber, Hammer, Spear }

    public sealed class WeaponSpec
    {
        internal WeaponSpec(string itemId, string name, WeaponKind kind, int attackBonus)
        { ItemId = itemId; Name = name; Kind = kind; AttackBonus = attackBonus; DefinitionId = XianXia.Core.Domain.Ids.DefinitionId.Parse(itemId).Value; }
        public string ItemId { get; }
        public string Name { get; }
        public WeaponKind Kind { get; }
        public int AttackBonus { get; }
        internal DefinitionId DefinitionId { get; }
    }

    /// <summary>
    /// One held item per character. Its Equipment/Attack/Fixed modifier is the single
    /// persisted authority: Source.DefinitionId identifies the real item removed from the bag.
    /// Existing attribute Snapshot capture/restore therefore preserves ownership and damage.
    /// </summary>
    public static class WeaponEquipmentService
    {
        public const string Hoe = "base:item_iron_hoe", Axe = "base:item_iron_axe", Pick = "base:item_iron_pick";
        public const string Sword = "base:item_iron_sword", Saber = "base:item_iron_saber";
        public const string Hammer = "base:item_iron_hammer", Spear = "base:item_iron_spear";
        static readonly WeaponSpec[] Specs = {
            new WeaponSpec(Hoe, "铁锄", WeaponKind.Hoe, 2),
            new WeaponSpec(Axe, "伐木斧", WeaponKind.Axe, 4),
            new WeaponSpec(Pick, "铁镐", WeaponKind.Pick, 3),
            new WeaponSpec(Sword, "铁剑", WeaponKind.Sword, 6),
            new WeaponSpec(Saber, "朴刀", WeaponKind.Saber, 7),
            new WeaponSpec(Hammer, "铁锤", WeaponKind.Hammer, 9),
            new WeaponSpec(Spear, "铁矛", WeaponKind.Spear, 6)
        };

        public static WeaponSpec Find(string itemId)
        {
            for (var i = 0; i < Specs.Length; i++)
                if (string.Equals(Specs[i].ItemId, itemId, StringComparison.Ordinal)) return Specs[i];
            return null;
        }

        static WeaponSpec FromModifier(AttributeModifier modifier)
        {
            if (modifier.Source.Kind != SourceKind.Equipment || !modifier.Source.DefinitionId.HasValue) return null;
            var definition = modifier.Source.DefinitionId.Value;
            for (var i = 0; i < Specs.Length; i++)
                if (Specs[i].DefinitionId == definition) return Specs[i];
            return null;
        }

        public static WeaponSpec GetEquipped(SimulationWorld world, EntityId id)
        {
            if (world == null || !world.Entities.TryGet(id, out var entity) ||
                !entity.TryGet<AttributesComponent>(out var attrs)) return null;
            for (var i = 0; i < attrs.Modifiers.Count; i++)
            {
                var weapon = FromModifier(attrs.Modifiers[i]);
                if (weapon != null) return weapon;
            }
            return null;
        }

        public static bool IsEquippedKind(SimulationWorld world, EntityId id, WeaponKind kind) =>
            GetEquipped(world, id)?.Kind == kind;

        static Result Fail(string message) => Result.Failure(ErrorCode.InvalidOperation, message);

        static Result ValidateCharacter(SimulationWorld world, EntityId id, bool opening, out AttributesComponent attrs)
        {
            attrs = null;
            if (world == null || !world.Entities.TryGet(id, out var entity) ||
                !entity.TryGet<AttributesComponent>(out attrs) || !CombatLifeStateService.CanFight(entity))
                return Fail("当前人物无法更换武器。");
            if (!opening && world.Strategic.PlayerPartyContext?.IsMember(id) != true)
                return Fail("只能为同行队员更换武器。");
            if (entity.TryGet<LifecycleComponent>(out var life) && life.State == LifecycleState.Captured ||
                PrisonConfinementService.IsConfinedOutdoor(world, id) ||
                AutonomousActionContinuationService.BlocksOrdinaryWork(world, id) ||
                MortalTransportConstraintService.ValidateUnburdenedAction(world, id).IsFailure)
                return Fail("战斗、拘押或搬运期间不能更换武器。");
            if (entity.TryGet<CombatInjuryComponent>(out var injury) && injury.MissingArms >= 2)
                return Fail("双臂缺失，无法持用武器。");
            if (world.Civilians.TryGet(id, out var civilian) &&
                (civilian.RescueCarrying || !civilian.CarrierId.IsNone || civilian.SleepPhase != CivilianSleepPhase.None))
                return Fail("睡眠或搬运期间不能更换武器。");
            var count = 0;
            for (var i = 0; i < attrs.Modifiers.Count; i++)
                if (FromModifier(attrs.Modifiers[i]) != null) count++;
            return count > 1 ? Fail("装备记录重复，未改变物品或属性。") : Result.Success();
        }

        public static Result TryEquip(SimulationWorld world, EntityId id, string itemId) => Equip(world, id, itemId, false);

        /// <summary>NewGame-only bootstrap: consumes its authored starting hoe before PlayerParty is bound.</summary>
        public static Result EquipOpeningHoe(SimulationWorld world, EntityId id) => Equip(world, id, Hoe, true);

        static Result Equip(SimulationWorld world, EntityId id, string itemId, bool opening)
        {
            var valid = ValidateCharacter(world, id, opening, out var attrs);
            if (valid.IsFailure) return valid;
            var weapon = Find(itemId);
            if (weapon == null || !world.InventoryCatalog.TryGet(itemId, out _)) return Fail("此物不能装备为武器。");
            var previous = GetEquipped(world, id);
            if (previous == weapon) return Result.Success();
            var inventory = world.Inventory.CaptureState();
            if (!world.Inventory.TryRemoveAll(itemId, 1)) return Fail("背包中没有这件武器。");
            if (previous != null && !world.Inventory.TryAddAll(previous.ItemId, 1))
            { world.Inventory.RestoreState(inventory); return Fail("背包放不下换回的武器，请先腾出空间。"); }
            var modifiers = attrs.CaptureModifiers();
            var nextId = attrs.PeekNextModifierId;
            RemoveWeaponModifiers(attrs);
            var added = attrs.AddModifier(AttributeId.Attack, ModifierOperation.Fixed, weapon.AttackBonus,
                new SourceRef(SourceKind.Equipment, weapon.DefinitionId, id));
            if (added.IsFailure)
            { world.Inventory.RestoreState(inventory); attrs.RestoreModifiers(modifiers, nextId); return Result.Failure(added.Error.Code, added.Error.Message); }
            world.Events.Publish(EventType.PartyInventoryChanged, world.Tick, actor: id, payload: "equip:" + itemId);
            return Result.Success();
        }

        public static Result TryUnequip(SimulationWorld world, EntityId id)
        {
            var valid = ValidateCharacter(world, id, false, out var attrs);
            if (valid.IsFailure) return valid;
            var weapon = GetEquipped(world, id);
            if (weapon == null) return Result.Success();
            if (!world.Inventory.TryAddAll(weapon.ItemId, 1)) return Fail("背包放不下武器，请先腾出空间。");
            RemoveWeaponModifiers(attrs);
            world.Events.Publish(EventType.PartyInventoryChanged, world.Tick, actor: id, payload: "unequip:" + weapon.ItemId);
            return Result.Success();
        }

        static void RemoveWeaponModifiers(AttributesComponent attrs)
        {
            for (var i = attrs.Modifiers.Count - 1; i >= 0; i--)
                if (FromModifier(attrs.Modifiers[i]) != null) attrs.RemoveBySource(attrs.Modifiers[i].Source);
        }

        /// <summary>Called once after load/content binding. Refresh numeric definitions without granting or returning an item.</summary>
        public static Result Rehydrate(SimulationWorld world)
        {
            foreach (var entity in world.Entities.All)
            {
                if (!entity.TryGet<AttributesComponent>(out var attrs)) continue;
                AttributeModifier saved = null;
                WeaponSpec weapon = null;
                for (var i = 0; i < attrs.Modifiers.Count; i++)
                {
                    var candidate = FromModifier(attrs.Modifiers[i]);
                    if (candidate == null) continue;
                    if (saved != null) return Result.Failure(ErrorCode.SnapshotInvalid, "同一人物保存了多件手持武器。", entity.Id.ToString());
                    saved = attrs.Modifiers[i]; weapon = candidate;
                }
                if (saved == null) continue;
                if (!world.InventoryCatalog.TryGet(weapon.ItemId, out _))
                    return Result.Failure(ErrorCode.ContentLoadFailed, "已装备武器的内容定义缺失。", weapon.ItemId);
                var source = new SourceRef(SourceKind.Equipment, weapon.DefinitionId, entity.Id);
                if (saved.Target == AttributeId.Attack && saved.Operation == ModifierOperation.Fixed &&
                    saved.Value == weapon.AttackBonus && saved.Source == source) continue;
                attrs.RemoveBySource(saved.Source);
                attrs.AddModifier(AttributeId.Attack, ModifierOperation.Fixed, weapon.AttackBonus, source);
            }
            return Result.Success();
        }

        /// <summary>Actual tree hits call this; a held axe does more work than an unsuitable held weapon.</summary>
        public static int TreeWorkDamage(SimulationWorld world, EntityId id, int baseDamage) =>
            IsEquippedKind(world, id, WeaponKind.Axe) ? (int)Math.Min(int.MaxValue, Math.Max(1L, baseDamage) * 2) : Math.Max(1, baseDamage);
    }
}
