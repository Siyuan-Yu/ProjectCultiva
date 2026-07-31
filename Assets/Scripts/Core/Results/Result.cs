using System;

namespace XianXia.Core.Results
{
    /// <summary>
    /// Non-generic success/failure result for operations without a value.
    /// </summary>
    public readonly struct Result
    {
        readonly GameError _error;
        readonly bool _isSuccess;

        Result(bool isSuccess, GameError error)
        {
            _isSuccess = isSuccess;
            _error = error;
        }

        public bool IsSuccess => _isSuccess;

        public bool IsFailure => !_isSuccess;

        public GameError Error
        {
            get
            {
                if (_isSuccess)
                    throw new InvalidOperationException("Successful Result has no Error.");
                return _error;
            }
        }

        public static Result Success() => new Result(true, default);

        public static Result Failure(GameError error) => new Result(false, error);

        public static Result Failure(ErrorCode code, string message = null, string detail = null) =>
            Failure(new GameError(code, message, detail));

        public static Result<T> Ok<T>(T value) => Result<T>.Success(value);

        public static Result<T> Fail<T>(GameError error) => Result<T>.Failure(error);

        public static Result<T> Fail<T>(ErrorCode code, string message = null, string detail = null) =>
            Result<T>.Failure(code, message, detail);
    }
}
