using System;

namespace XianXia.Core.Results
{
    /// <summary>
    /// Value-bearing result. Failed instances must not expose a usable Value.
    /// </summary>
    public readonly struct Result<T>
    {
        readonly T _value;
        readonly GameError _error;
        readonly bool _isSuccess;

        Result(bool isSuccess, T value, GameError error)
        {
            _isSuccess = isSuccess;
            _value = value;
            _error = error;
        }

        public bool IsSuccess => _isSuccess;

        public bool IsFailure => !_isSuccess;

        public T Value
        {
            get
            {
                if (!_isSuccess)
                    throw new InvalidOperationException("Cannot read Value from a failed Result<" + typeof(T).Name + ">.");
                return _value;
            }
        }

        public GameError Error
        {
            get
            {
                if (_isSuccess)
                    throw new InvalidOperationException("Successful Result<" + typeof(T).Name + "> has no Error.");
                return _error;
            }
        }

        public bool TryGetValue(out T value)
        {
            if (_isSuccess)
            {
                value = _value;
                return true;
            }

            value = default;
            return false;
        }

        public static Result<T> Success(T value) => new Result<T>(true, value, default);

        public static Result<T> Failure(GameError error) => new Result<T>(false, default, error);

        public static Result<T> Failure(ErrorCode code, string message = null, string detail = null) =>
            Failure(new GameError(code, message, detail));
    }
}
