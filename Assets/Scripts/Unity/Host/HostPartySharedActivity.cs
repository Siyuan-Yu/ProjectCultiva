using System;
using XianXia.Core.Input;

namespace XianXia.Unity.Host
{
    /// <summary>Phase 1: shareable Active activities that Followers may mirror (Party-derived).</summary>
    public enum HostPartySharedActivityKind
    {
        None = 0,
        FollowIdle = 1,
        Farming = 2,
        Woodcutting = 3,
        Gathering = 4,
        Movement = 5,
        Combat = 6
    }

    /// <summary>Snapshot of Active shareable activity for Follower sync.</summary>
    public readonly struct HostPartySharedActivity : IEquatable<HostPartySharedActivity>
    {
        public HostPartySharedActivityKind Kind { get; }
        public string LocationId { get; }
        public int DestructibleInstanceId { get; }
        public PlayerCommandKind LoopKind { get; }

        HostPartySharedActivity(
            HostPartySharedActivityKind kind,
            string locationId,
            int destructibleInstanceId,
            PlayerCommandKind loopKind)
        {
            Kind = kind;
            LocationId = locationId ?? string.Empty;
            DestructibleInstanceId = destructibleInstanceId;
            LoopKind = loopKind;
        }

        public static HostPartySharedActivity FollowIdle =>
            new HostPartySharedActivity(HostPartySharedActivityKind.FollowIdle, null, 0, PlayerCommandKind.Stop);

        public static HostPartySharedActivity Movement =>
            new HostPartySharedActivity(HostPartySharedActivityKind.Movement, null, 0, PlayerCommandKind.Stop);

        public static HostPartySharedActivity Combat =>
            new HostPartySharedActivity(HostPartySharedActivityKind.Combat, null, 0, PlayerCommandKind.Stop);

        public static HostPartySharedActivity Farming(string locationId) =>
            new HostPartySharedActivity(HostPartySharedActivityKind.Farming, locationId, 0, PlayerCommandKind.Stop);

        public static HostPartySharedActivity Woodcutting(int destructibleInstanceId) =>
            new HostPartySharedActivity(
                HostPartySharedActivityKind.Woodcutting,
                null,
                destructibleInstanceId,
                PlayerCommandKind.Stop);

        public static HostPartySharedActivity Gathering(string locationId, PlayerCommandKind loopKind) =>
            new HostPartySharedActivity(
                HostPartySharedActivityKind.Gathering,
                locationId,
                0,
                loopKind);

        public bool IsShareable =>
            Kind == HostPartySharedActivityKind.Farming ||
            Kind == HostPartySharedActivityKind.Woodcutting ||
            Kind == HostPartySharedActivityKind.Gathering;

        public bool Equals(HostPartySharedActivity other) =>
            Kind == other.Kind &&
            string.Equals(LocationId, other.LocationId, StringComparison.Ordinal) &&
            DestructibleInstanceId == other.DestructibleInstanceId &&
            LoopKind == other.LoopKind;

        public override bool Equals(object obj) => obj is HostPartySharedActivity other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ (LocationId != null ? StringComparer.Ordinal.GetHashCode(LocationId) : 0);
                hash = (hash * 397) ^ DestructibleInstanceId;
                hash = (hash * 397) ^ (int)LoopKind;
                return hash;
            }
        }

        public static bool operator ==(HostPartySharedActivity a, HostPartySharedActivity b) => a.Equals(b);
        public static bool operator !=(HostPartySharedActivity a, HostPartySharedActivity b) => !a.Equals(b);
    }
}
