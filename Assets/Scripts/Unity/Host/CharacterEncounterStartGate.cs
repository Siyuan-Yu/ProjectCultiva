using System;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// One-shot Host staging between a materialized independent field and tactical execution.
    /// This is presentation state only; the persisted CharacterEncounter domain phase remains Active.
    /// </summary>
    public sealed class CharacterEncounterStartGate
    {
        SimulationWorld _world;
        Action _onStart;

        public string EncounterId { get; private set; } = string.Empty;
        public EntityId Attacker { get; private set; } = EntityId.None;
        public EntityId Target { get; private set; } = EntityId.None;
        public bool IsRestore { get; private set; }
        public bool RecordCharacterAttack { get; private set; }
        public bool IsStaged => _world != null && !string.IsNullOrEmpty(EncounterId);

        public void Stage(
            SimulationWorld world,
            string encounterId,
            EntityId attacker,
            EntityId target,
            Action onStart,
            bool isRestore,
            bool recordCharacterAttack)
        {
            Clear();
            _world = world;
            EncounterId = encounterId ?? string.Empty;
            Attacker = attacker;
            Target = target;
            _onStart = onStart;
            IsRestore = isRestore;
            RecordCharacterAttack = recordCharacterAttack;
        }

        public Result ValidateStart(
            SimulationWorld world,
            CharacterEncounterState state,
            string independentFieldId,
            bool pauseOwned,
            bool inputLocked)
        {
            if (!IsStaged)
                return Failure("开始意图不存在。");
            if (!ReferenceEquals(_world, world))
                return Failure("战斗世界已经替换。");
            if (state == null)
                return Failure("CharacterEncounter 已丢失。");
            if (!string.Equals(state.EncounterId, EncounterId, StringComparison.Ordinal))
                return Failure("战斗身份已经变化。");
            if (state.Phase != CharacterEncounterPhase.Active)
                return Failure("战斗领域状态不是 Active。");
            if (!string.Equals(independentFieldId, EncounterId, StringComparison.Ordinal))
                return Failure("独立战场身份不匹配。");
            if (!pauseOwned)
                return Failure("CharacterEncounterUI 暂停所有权缺失。");
            if (!inputLocked)
                return Failure("CharacterEncounter 输入锁缺失。");
            if (Attacker.IsNone != Target.IsNone)
                return Failure("初始攻击目标不完整。");
            if (!Attacker.IsNone && !state.Opposing(Attacker.Value, Target.Value))
                return Failure("初始攻击双方不属于当前战斗敌对侧。");
            return Result.Success();
        }

        public Action TakeStartAction()
        {
            var action = _onStart;
            _onStart = null;
            return action;
        }

        public void Clear()
        {
            _world = null;
            _onStart = null;
            EncounterId = string.Empty;
            Attacker = EntityId.None;
            Target = EntityId.None;
            IsRestore = false;
            RecordCharacterAttack = false;
        }

        static Result Failure(string message) =>
            Result.Failure(ErrorCode.InvalidOperation, message);
    }
}
