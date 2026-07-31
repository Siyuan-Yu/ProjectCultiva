using System;

namespace XianXia.Core.Results
{
    /// <summary>
    /// Immutable business failure payload. Ordinary simulation failures use this, not exceptions.
    /// </summary>
    public readonly struct GameError : IEquatable<GameError>
    {
        public GameError(ErrorCode code, string message = null, string detail = null)
        {
            Code = code;
            Message = message ?? code.ToString();
            Detail = detail;
        }

        public ErrorCode Code { get; }

        public string Message { get; }

        public string Detail { get; }

        public bool Equals(GameError other) =>
            Code == other.Code &&
            string.Equals(Message, other.Message, StringComparison.Ordinal) &&
            string.Equals(Detail, other.Detail, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is GameError other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Code;
                hash = hash * 31 + (Message != null ? StringComparer.Ordinal.GetHashCode(Message) : 0);
                hash = hash * 31 + (Detail != null ? StringComparer.Ordinal.GetHashCode(Detail) : 0);
                return hash;
            }
        }

        public override string ToString() =>
            Detail == null ? Code + ": " + Message : Code + ": " + Message + " (" + Detail + ")";

        public static bool operator ==(GameError left, GameError right) => left.Equals(right);

        public static bool operator !=(GameError left, GameError right) => !left.Equals(right);
    }
}
