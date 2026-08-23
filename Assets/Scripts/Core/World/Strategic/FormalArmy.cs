using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Formal Army 领域真源（Phase A）。成员正向真源为 <see cref="MemberCharacterIds"/>；
    /// Character 侧 <see cref="ArmyMembershipComponent"/> 仅为反向索引。
    /// </summary>
    public sealed class FormalArmy
    {
        readonly List<ulong> _memberCharacterIds = new List<ulong>(8);
        ReadOnlyCollection<ulong> _memberCharacterIdsView;

        string _nodeId = string.Empty;
        FormalArmyState _state = FormalArmyState.AtNode;
        string _routeId = string.Empty;
        string _destNodeId = string.Empty;
        int _remainingTravelTicks;
        int _travelTotalTicks;
        float _routeAnchorProgress = -1f;
        float _routeSegmentOriginProgress = -1f;
        float _routeSegmentEndProgress = -1f;

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

        public string NodeId
        {
            get => _nodeId;
            internal set => FormalArmyStrategicMutationDiagnostics.WriteString(this, nameof(NodeId), ref _nodeId, value);
        }

        public FormalArmyState State
        {
            get => _state;
            internal set => FormalArmyStrategicMutationDiagnostics.WriteState(this, ref _state, value);
        }

        public string RouteId
        {
            get => _routeId;
            internal set => FormalArmyStrategicMutationDiagnostics.WriteString(this, nameof(RouteId), ref _routeId, value);
        }

        public string DestNodeId
        {
            get => _destNodeId;
            internal set => FormalArmyStrategicMutationDiagnostics.WriteString(this, nameof(DestNodeId), ref _destNodeId, value);
        }

        public int RemainingTravelTicks
        {
            get => _remainingTravelTicks;
            internal set => FormalArmyStrategicMutationDiagnostics.WriteInt(this, nameof(RemainingTravelTicks), ref _remainingTravelTicks, value);
        }

        public int TravelTotalTicks
        {
            get => _travelTotalTicks;
            internal set => FormalArmyStrategicMutationDiagnostics.WriteInt(this, nameof(TravelTotalTicks), ref _travelTotalTicks, value);
        }

        public float RouteAnchorProgress
        {
            get => _routeAnchorProgress;
            internal set => FormalArmyStrategicMutationDiagnostics.WriteFloat(this, nameof(RouteAnchorProgress), ref _routeAnchorProgress, value);
        }

        public float RouteSegmentOriginProgress
        {
            get => _routeSegmentOriginProgress;
            internal set => FormalArmyStrategicMutationDiagnostics.WriteFloat(this, nameof(RouteSegmentOriginProgress), ref _routeSegmentOriginProgress, value);
        }

        public float RouteSegmentEndProgress
        {
            get => _routeSegmentEndProgress;
            internal set => FormalArmyStrategicMutationDiagnostics.WriteFloat(this, nameof(RouteSegmentEndProgress), ref _routeSegmentEndProgress, value);
        }

        public bool IsTraveling =>
            State == FormalArmyState.OnRoute &&
            !string.IsNullOrEmpty(RouteId) &&
            RemainingTravelTicks > 0;

        public bool IsRouteAnchored =>
            !string.IsNullOrEmpty(RouteId) &&
            RouteAnchorProgress >= 0f &&
            !IsTraveling;

        public float TravelProgress
        {
            get
            {
                if (!IsTraveling || TravelTotalTicks <= 0)
                    return 0f;
                var done = TravelTotalTicks - RemainingTravelTicks;
                if (done <= 0)
                    return 0f;
                if (done >= TravelTotalTicks)
                    return 1f;
                return (float)done / TravelTotalTicks;
            }
        }

        public float GetRouteDisplayProgress()
        {
            if (IsTraveling &&
                RouteSegmentOriginProgress >= 0f &&
                RouteSegmentEndProgress >= 0f)
            {
                return RouteSegmentOriginProgress +
                       (RouteSegmentEndProgress - RouteSegmentOriginProgress) * TravelProgress;
            }

            if (IsTraveling)
                return TravelProgress;
            if (IsRouteAnchored)
                return RouteAnchorProgress;
            return 0f;
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

        internal void ClearTravel()
        {
            FormalArmyStrategicMutationDiagnostics.ExecuteMutation(this, nameof(ClearTravel), () =>
            {
                if (!IsRouteAnchored)
                    _routeId = string.Empty;
                _destNodeId = string.Empty;
                _remainingTravelTicks = 0;
                _travelTotalTicks = 0;
                _routeSegmentOriginProgress = -1f;
                _routeSegmentEndProgress = -1f;
            });
        }

        internal void ClearRouteSegment()
        {
            FormalArmyStrategicMutationDiagnostics.ExecuteMutation(this, nameof(ClearRouteSegment), () =>
            {
                _routeSegmentOriginProgress = -1f;
                _routeSegmentEndProgress = -1f;
            });
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
