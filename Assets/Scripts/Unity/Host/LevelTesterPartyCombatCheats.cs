using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    public readonly struct PartyCombatCheatResult
    {
        public PartyCombatCheatResult(int succeeded, int skipped)
        { Succeeded = succeeded; Skipped = skipped; }
        public int Succeeded { get; }
        public int Skipped { get; }
    }

    /// <summary>仅供 LevelTester Host 使用；调用者必须显式传入 PlayerParty.Members。</summary>
    public static class LevelTesterPartyCombatCheats
    {
        public static PartyCombatCheatResult AddAttack(
            SimulationWorld world, IReadOnlyList<EntityId> partyMembers) =>
            AddAttribute(world, partyMembers, AttributeId.Attack, 10, false);

        public static PartyCombatCheatResult AddMaxHpAndRefill(
            SimulationWorld world, IReadOnlyList<EntityId> partyMembers) =>
            AddAttribute(world, partyMembers, AttributeId.MaxHp, 50, true);

        public static PartyCombatCheatResult Refill(
            SimulationWorld world, IReadOnlyList<EntityId> partyMembers)
        {
            var succeeded = 0;
            var skipped = 0;
            if (world == null || partyMembers == null) return new PartyCombatCheatResult(0, 0);
            for (var i = 0; i < partyMembers.Count; i++)
            {
                if (!world.Entities.TryGet(partyMembers[i], out var entity) ||
                    CombatRecoveryService.RestoreToMaximum(entity).IsFailure)
                    skipped++;
                else
                    succeeded++;
            }
            return new PartyCombatCheatResult(succeeded, skipped);
        }

        static PartyCombatCheatResult AddAttribute(
            SimulationWorld world, IReadOnlyList<EntityId> partyMembers,
            AttributeId attribute, int delta, bool refill)
        {
            var succeeded = 0;
            var skipped = 0;
            if (world == null || partyMembers == null) return new PartyCombatCheatResult(0, 0);
            for (var i = 0; i < partyMembers.Count; i++)
            {
                if (!world.Entities.TryGet(partyMembers[i], out var entity) ||
                    !entity.TryGet<LifecycleComponent>(out var life) || life.IsDead || life.IsRemoved || life.IsIncapacitated ||
                    !entity.TryGet<AttributesComponent>(out var attrs))
                { skipped++; continue; }
                var next = (long)attrs.GetBase(attribute) + delta;
                attrs.SetBase(attribute, next > int.MaxValue ? int.MaxValue : next < int.MinValue ? int.MinValue : (int)next);
                if (refill)
                {
                    CombatDamageRules.EnsureVitals(entity);
                    if (!entity.TryGet<CombatVitalsComponent>(out var vitals))
                    { skipped++; continue; }
                    vitals.CurrentHp = System.Math.Max(1, attrs.GetFinal(AttributeId.MaxHp));
                    vitals.CurrentSpiritPower = System.Math.Max(0, attrs.GetFinal(AttributeId.SpiritPower));
                    vitals.PoolsInitialized = true;
                }
                succeeded++;
            }
            return new PartyCombatCheatResult(succeeded, skipped);
        }
    }

    public readonly struct CharacterCombatCheatResult
    {
        public CharacterCombatCheatResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public string Message { get; }
    }

    /// <summary>仅供 LevelTester 单角色战斗状态验收；目标可以是玩家队员或 NPC。</summary>
    public static class LevelTesterCharacterCombatCheats
    {
        public static CharacterCombatCheatResult TryForceSelectedCharacterIncapacitated(
            SimulationWorld world,
            IReadOnlyList<EntityId> selectedIds)
        {
            if (selectedIds == null || selectedIds.Count == 0)
                return Failed("请先单选一个角色。");
            if (selectedIds.Count != 1)
                return Failed("当前选择包含多个角色。");
            if (world == null)
                return Failed("当前世界尚未初始化。");

            var id = selectedIds[0];
            if (id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                return Failed("选中 Entity 不存在。");
            if ((entity.Tags & EntityTag.Character) == 0)
                return Failed("选中 Entity 不是 Character。");
            if (!entity.TryGet<LifecycleComponent>(out var life) || life == null)
                return Failed("选中角色没有合法 LifecycleComponent。");

            var name = entity.TryGet<IdentityComponent>(out var identity) &&
                       !string.IsNullOrWhiteSpace(identity.DisplayName)
                ? identity.DisplayName
                : "角色";
            if (life.State != LifecycleState.Alive)
                return Failed(name + "当前不是可进入弥留的 Alive 状态。");
            if (!CombatLifeStateService.TryEnterIncapacitated(world, entity))
                return Failed(name + "进入弥留失败。");

            return new CharacterCombatCheatResult(
                true,
                "成功：" + name + "（" + id.Value + "）已进入弥留。");
        }

        static CharacterCombatCheatResult Failed(string reason) =>
            new CharacterCombatCheatResult(false, "失败：" + reason);
    }

    public static class LevelTesterNpcSquadMotionCheats
    {
        public static CharacterCombatCheatResult TryMoveSelectedNpcSquadNearPlayer(
            SimulationWorld world, IReadOnlyList<EntityId> selectedIds)
        {
            if (world == null) return Failed("当前世界尚未初始化。");
            if (selectedIds == null || selectedIds.Count != 1) return Failed("请单选一个 NPC 角色。");
            var id = selectedIds[0];
            if (!world.Entities.TryGet(id, out var entity) || entity == null || (entity.Tags & EntityTag.Npc) == 0)
                return Failed("选中 Entity 不是 NPC Character。");
            if (!world.Strategic.Squads.TryGetForCharacter(id, out var squad) ||
                !SquadWorldMotionService.TryGetActiveNpcSquadAuthority(
                    world, squad.SquadId, out _, out var motion))
                return Failed("所选 NPC 不属于可移动的现代小队。");
            if (world.Strategic.PlayerPartyContext != null &&
                world.Strategic.PlayerPartyContext.IsMember(id)) return Failed("不能对玩家小队使用此验收命令。");
            var living = 0;
            for (var i = 0; i < squad.MemberCharacterIds.Count; i++)
                if (CharacterLifeStateQuery.IsLivingForMacroOrder(world, new EntityId(squad.MemberCharacterIds[i]))) living++;
            if (living < 2) return Failed("所选 NPC 小队需要至少两名存活成员。");
            var player = world.PlayerPartyTravel;
            if (player == null || !player.HasPosition || !world.SurfaceGround.TryGet(motion.SurfaceId, out var navigation))
                return Failed("主控或 NPC 小队没有可用的连续世界位置。");
            var offsets = new[] { new WorldVec2(12f, 0f), new WorldVec2(-12f, 0f), new WorldVec2(0f, 12f), new WorldVec2(0f, -12f) };
            for (var i = 0; i < offsets.Length; i++)
            {
                var target = new WorldVec2(player.WorldPosition.X + offsets[i].X * navigation.CellSize,
                    player.WorldPosition.Y + offsets[i].Y * navigation.CellSize);
                if (!navigation.Contains(target.X, target.Y) || !navigation.IsWalkable(target.X, target.Y)) continue;
                var result = SquadWorldMotionService.MoveToWorldPosition(world, squad.SquadId, target);
                if (result.IsSuccess) return new CharacterCombatCheatResult(true, "成功：NPC 小队已开始前往主控附近测试点。");
            }
            return Failed("主控附近没有可用的安全测试点或路径。");
        }

        static CharacterCombatCheatResult Failed(string reason) =>
            new CharacterCombatCheatResult(false, "失败：" + reason);
    }
}
