using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using XianXia.Core.Domain.Ids;

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

        public string ArmyId { get; internal set; } = string.Empty;
        public string FactionId { get; internal set; } = string.Empty;
        public EntityId LeaderCharacterId { get; internal set; }
        public string NodeId { get; internal set; } = string.Empty;
        public FormalArmyState State { get; internal set; } = FormalArmyState.AtNode;

        /// <summary>Phase D+：战略位置真源（Route / Travel ticks）。</summary>
        public string RouteId { get; internal set; } = string.Empty;
        public string DestNodeId { get; internal set; } = string.Empty;
        public int RemainingTravelTicks { get; internal set; }
        public int TravelTotalTicks { get; internal set; }
        public float RouteAnchorProgress { get; internal set; } = -1f;
        /// <summary>路中出发区段（与 WorldAgentPresence 同语义）。</summary>
        public float RouteSegmentOriginProgress { get; internal set; } = -1f;
        public float RouteSegmentEndProgress { get; internal set; } = -1f;

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

        internal void ClearTravel()
        {
            if (!IsRouteAnchored)
                RouteId = string.Empty;
            DestNodeId = string.Empty;
            RemainingTravelTicks = 0;
            TravelTotalTicks = 0;
            RouteSegmentOriginProgress = -1f;
            RouteSegmentEndProgress = -1f;
        }

        internal void ClearRouteSegment()
        {
            RouteSegmentOriginProgress = -1f;
            RouteSegmentEndProgress = -1f;
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
