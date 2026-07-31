using System.Collections.Generic;

namespace XianXia.Core.Results
{
    /// <summary>
    /// Collects multiple validation errors without stopping at the first failure.
    /// </summary>
    public sealed class ValidationReport
    {
        readonly List<GameError> _errors = new List<GameError>();

        public bool IsValid => _errors.Count == 0;

        public IReadOnlyList<GameError> Errors => _errors;

        public void Add(GameError error)
        {
            _errors.Add(error);
        }

        public void Add(ErrorCode code, string message = null, string detail = null)
        {
            _errors.Add(new GameError(code, message, detail));
        }

        public void AddRange(IEnumerable<GameError> errors)
        {
            if (errors == null) return;
            _errors.AddRange(errors);
        }

        public Result ToResult()
        {
            if (IsValid)
                return Result.Success();

            var first = _errors[0];
            var detail = _errors.Count == 1
                ? first.Detail
                : "error_count=" + _errors.Count;
            return Result.Failure(ErrorCode.ValidationFailed, first.Message, detail);
        }

        public Result<T> ToResult<T>(T valueWhenValid)
        {
            if (IsValid)
                return Result<T>.Success(valueWhenValid);

            var first = _errors[0];
            var detail = _errors.Count == 1
                ? first.Detail
                : "error_count=" + _errors.Count;
            return Result<T>.Failure(ErrorCode.ValidationFailed, first.Message, detail);
        }
    }
}
