using System;

namespace XianXia.Unity.Host
{
    public sealed class HostCharacterArtState
    {
        // A readable two-step cycle at the normal management camera; never changes movement speed.
        public const float WalkWorldUnitsPerCycle = 3.2f;
        const float MaxWalkCyclesPerSecond = 2.8f;
        public HostStepAArt.Action Action { get; private set; }
        public float Clock { get; private set; }
        public float WalkPhase01 { get; private set; }
        public bool IsLocomoting { get; private set; }
        float _idleClock;
        float _workClock;
        float _attackElapsed = .36f;
        public bool AttackPending => _attackElapsed < .36f;

        public void Strike() { _attackElapsed = 0f; }

        public void Advance(float deltaTime, bool paused, bool canAct, bool sleeping,
            bool moved, bool working, float walkedWorldDistance = 0f, float actionDeltaTime = -1f,
            bool workContact = false)
        {
            if (!canAct || sleeping)
            {
                IsLocomoting = false;
                _attackElapsed = .36f;
                Action = HostStepAArt.Action.Idle;
                Clock = 0f;
                return;
            }
            if (paused) return;
            var realDt = Math.Max(0f, deltaTime);
            var actionDt = actionDeltaTime < 0f ? realDt : Math.Max(0f, actionDeltaTime);
            _idleClock += realDt;
            IsLocomoting = moved && walkedWorldDistance > 0f;
            // Legs keep walking under an actual upper-body strike. An attack must not freeze
            // the gait phase of a pursuing actor, or cause a fresh phase each time it fires.
            if (IsLocomoting)
            {
                var distance = Math.Min(Math.Max(0f, walkedWorldDistance),
                    WalkWorldUnitsPerCycle * MaxWalkCyclesPerSecond * realDt);
                WalkPhase01 = (WalkPhase01 + distance / WalkWorldUnitsPerCycle) % 1f;
            }
            var next = _attackElapsed < .36f ? HostStepAArt.Action.Attack :
                workContact && working ? HostStepAArt.Action.Work :
                moved ? HostStepAArt.Action.Walk : working ? HostStepAArt.Action.Work :
                HostStepAArt.Action.Idle;
            if (next == HostStepAArt.Action.Walk)
            {
                Clock = WalkPhase01 * .48f;
            }
            else
            {
                if (next == HostStepAArt.Action.Work)
                    _workClock += Math.Min(actionDt, realDt * 2f);
                Clock = next == HostStepAArt.Action.Attack ? _attackElapsed :
                    next == HostStepAArt.Action.Work ? _workClock : _idleClock;
            }
            Action = next;
            _attackElapsed += Math.Min(actionDt, realDt * 3f);
        }

        public int Frame => Action == HostStepAArt.Action.Idle ? 0 :
            Action == HostStepAArt.Action.Attack ? Math.Min(3, (int)(Clock / .09f)) :
            Action == HostStepAArt.Action.Walk ? Math.Min(3, (int)(WalkPhase01 * 4f)) :
            (int)(Clock / .16f) % 4;
    }
}
