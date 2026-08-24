using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Formal Army 领域真源（Phase A）。成员正向真源为 <see cref="MemberCharacterIds"/>；
    /// Character 侧 <see cref="ArmyMembershipComponent"/> 仅为反向索引。
    /// 战略位置真源：<see cref="CurrentHex"/>（Pure Hex）。
    /// </summary>
    public sealed class FormalArmy
    {
        readonly List<ulong> _memberCharacterIds = new List<ulong>(8);
        ReadOnlyCollection<ulong> _memberCharacterIdsView;

        FormalArmyState _state = FormalArmyState.Idle;

        HexCoord _currentHex;
        HexCoord _destinationHex;
        readonly List<HexCoord> _hexPath = new List<HexCoord>(32);
        int _currentPathIndex;
        float _stepProgress;
        int _stepRemainingTicks;
        int _stepTotalTicks;
        bool _usesHexStrategicPosition;

        public string ArmyId { get; internal set; } = string.Empty;
        public string FactionId { get; internal set; } = string.Empty;
        public EntityId LeaderCharacterId { get; internal set; }

        public bool UsesHexStrategicPosition
        {
            get => _usesHexStrategicPosition;
            internal set => _usesHexStrategicPosition = value;
        }

        public ArmyHexPosition HexPosition => new ArmyHexPosition(this);

        public HexCoord CurrentHex
        {
            get => _currentHex;
            internal set => _currentHex = value;
        }

        public HexCoord DestinationHex
        {
            get => _destinationHex;
            internal set => _destinationHex = value;
        }

        public int CurrentPathIndex
        {
            get => _currentPathIndex;
            internal set => _currentPathIndex = value;
        }

        public float StepProgress
        {
            get => _stepProgress;
            internal set => _stepProgress = value;
        }

        /// <summary>相邻格间移动进度 0～1（<see cref="ArmyHexPosition"/> 语义）。</summary>
        public float MoveProgress => StepProgress;

        public bool TryGetNextHex(out HexCoord nextHex)
        {
            if (State == FormalArmyState.Moving && TryGetActiveStepHexes(out _, out nextHex))
                return true;
            nextHex = default;
            return false;
        }

        public int StepRemainingTicks
        {
            get => _stepRemainingTicks;
            internal set => _stepRemainingTicks = value;
        }

        public int StepTotalTicks
        {
            get => _stepTotalTicks;
            internal set => _stepTotalTicks = value;
        }

        public int HexPathCount => _hexPath.Count;

        public IReadOnlyList<HexCoord> HexPath =>
            _hexPathView ?? (_hexPathView = _hexPath.AsReadOnly());

        ReadOnlyCollection<HexCoord> _hexPathView;

        public FormalArmyState State
        {
            get => _state;
            internal set => FormalArmyStrategicMutationDiagnostics.WriteState(this, ref _state, value);
        }

        /// <summary>若当前 Hex 落在 WorldSite 足迹内，返回该 SiteId。</summary>
        public bool TryGetFormationSiteId(SimulationWorld world, out string siteId)
        {
            siteId = string.Empty;
            if (world?.Strategic?.Sites == null)
                return false;
            if (!world.Strategic.Sites.TryGetAtHex(CurrentHex, out var site) || site == null)
                return false;
            siteId = site.SiteId ?? string.Empty;
            return !string.IsNullOrEmpty(siteId);
        }

        internal void SetHexPath(IReadOnlyList<HexCoord> path, HexCoord destination)
        {
            _hexPath.Clear();
            if (path != null)
            {
                for (var i = 0; i < path.Count; i++)
                    _hexPath.Add(path[i]);
            }

            _destinationHex = destination;
            _currentPathIndex = 0;
            _stepProgress = 0f;
            if (_hexPath.Count < 2 || _currentHex == destination)
            {
                CompleteHexMove();
                return;
            }

            State = FormalArmyState.Moving;
            _currentPathIndex = 0;
            StepTotalTicks = Math.Max(4, 8);
            StepRemainingTicks = StepTotalTicks;
            StepProgress = 0f;
        }

        internal void ClearHexPath()
        {
            _hexPath.Clear();
            _currentPathIndex = 0;
            _stepProgress = 0f;
            _stepRemainingTicks = 0;
            _stepTotalTicks = 0;
            _destinationHex = _currentHex;
        }

        internal void CompleteHexMove()
        {
            ClearHexPath();
            if (State == FormalArmyState.Moving)
                State = FormalArmyState.Idle;
        }

        public bool TryGetActiveStepHexes(out HexCoord from, out HexCoord to)
        {
            from = _currentHex;
            to = _currentHex;
            if (_hexPath.Count < 2 || _currentPathIndex < 0 || _currentPathIndex >= _hexPath.Count - 1)
                return false;
            from = _hexPath[_currentPathIndex];
            to = _hexPath[_currentPathIndex + 1];
            return true;
        }

        public IReadOnlyList<ulong> MemberCharacterIds =>
            _memberCharacterIdsView ?? (_memberCharacterIdsView = _memberCharacterIds.AsReadOnly());

        internal void ReplaceMembers(IReadOnlyList<ulong> memberIds)
        {
            _memberCharacterIds.Clear();
            if (memberIds == null)
                return;
            for (var i = 0; i < memberIds.Count; i++)
                _memberCharacterIds.Add(memberIds[i]);
        }

        internal void AddMember(EntityId memberId)
        {
            if (memberId.IsNone || _memberCharacterIds.Contains(memberId.Value))
                return;
            _memberCharacterIds.Add(memberId.Value);
        }

        internal void RemoveMember(EntityId memberId)
        {
            if (memberId.IsNone)
                return;
            _memberCharacterIds.Remove(memberId.Value);
        }

        public bool ContainsMember(EntityId characterId)
        {
            if (characterId.IsNone)
                return false;
            return _memberCharacterIds.Contains(characterId.Value);
        }
    }
}
