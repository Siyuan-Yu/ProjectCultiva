using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// PlayerParty 世界旅行运动状态（独立于 FormalArmy；成员 WorldPresence 同步到 CurrentHex）。
    /// </summary>
    public sealed class PlayerPartyWorldMotion
    {
        readonly List<HexCoord> _hexPath = new List<HexCoord>(32);
        readonly List<EntityId> _travelingMembers = new List<EntityId>(6);
        ReadOnlyCollection<HexCoord> _hexPathView;

        public bool IsMoving { get; private set; }
        public HexTravelMode TravelMode { get; private set; } = HexTravelMode.Ground;
        public HexCoord CurrentHex { get; private set; }
        public HexCoord DestinationHex { get; private set; }
        public int CurrentPathIndex { get; private set; }
        public float StepProgress { get; private set; }
        public int StepRemainingTicks { get; private set; }
        public int StepTotalTicks { get; private set; }
        public bool HasPosition { get; private set; }

        public int HexPathCount => _hexPath.Count;

        public IReadOnlyList<HexCoord> HexPath =>
            _hexPathView ?? (_hexPathView = _hexPath.AsReadOnly());

        public IReadOnlyList<EntityId> TravelingMembers => _travelingMembers;

        public void Clear()
        {
            _hexPath.Clear();
            _travelingMembers.Clear();
            CurrentPathIndex = 0;
            StepProgress = 0f;
            StepRemainingTicks = 0;
            StepTotalTicks = 0;
            IsMoving = false;
            DestinationHex = CurrentHex;
            TravelMode = HexTravelMode.Ground;
        }

        public void SetIdleAt(HexCoord hex)
        {
            CurrentHex = hex;
            DestinationHex = hex;
            HasPosition = true;
            _hexPath.Clear();
            CurrentPathIndex = 0;
            StepProgress = 0f;
            StepRemainingTicks = 0;
            StepTotalTicks = 0;
            IsMoving = false;
        }

        public void CaptureTravelingMembers(IReadOnlyList<EntityId> members)
        {
            _travelingMembers.Clear();
            if (members == null)
                return;
            for (var i = 0; i < members.Count; i++)
            {
                if (!members[i].IsNone)
                    _travelingMembers.Add(members[i]);
            }
        }

        internal void SetHexPath(IReadOnlyList<HexCoord> path, HexCoord destination, HexTravelMode mode)
        {
            TravelMode = mode;
            _hexPath.Clear();
            if (path != null)
            {
                for (var i = 0; i < path.Count; i++)
                    _hexPath.Add(path[i]);
            }

            DestinationHex = destination;
            CurrentPathIndex = 0;
            StepProgress = 0f;
            if (_hexPath.Count < 2 || CurrentHex == destination)
            {
                CompleteMove();
                return;
            }

            IsMoving = true;
            StepTotalTicks = Math.Max(4, 8);
            StepRemainingTicks = StepTotalTicks;
            StepProgress = 0f;
        }

        internal void CompleteMove()
        {
            _hexPath.Clear();
            CurrentPathIndex = 0;
            StepProgress = 0f;
            StepRemainingTicks = 0;
            StepTotalTicks = 0;
            DestinationHex = CurrentHex;
            IsMoving = false;
        }

        internal void CancelAtCurrentHex() => CompleteMove();

        internal void AdvanceToHex(HexCoord hex)
        {
            CurrentHex = hex;
            HasPosition = true;
        }

        internal void SetStep(int total, int remaining, float progress)
        {
            StepTotalTicks = total;
            StepRemainingTicks = remaining;
            StepProgress = progress;
        }

        internal void IncrementPathIndex() => CurrentPathIndex++;

        public bool TryGetActiveStepHexes(out HexCoord from, out HexCoord to)
        {
            from = CurrentHex;
            to = CurrentHex;
            if (_hexPath.Count < 2 || CurrentPathIndex < 0 || CurrentPathIndex >= _hexPath.Count - 1)
                return false;
            from = _hexPath[CurrentPathIndex];
            to = _hexPath[CurrentPathIndex + 1];
            return true;
        }
    }
}
