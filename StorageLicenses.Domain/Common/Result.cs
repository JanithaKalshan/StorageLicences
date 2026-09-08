namespace StorageLicenses.Domain.Common
{
    public class Result
    {
        protected Result(bool isSuccess, Error error)
            : this(isSuccess, isSuccess ? Array.Empty<Error>() : new[] { error })
        {
        }

        protected Result(bool isSuccess, IReadOnlyList<Error> errors)
        {
            if (isSuccess && errors.Any(e => e != Error.None) || !isSuccess && (errors.Count == 0 || errors.All(e => e == Error.None)))
                throw new ArgumentException("Invalid error state", nameof(errors));

            IsSuccess = isSuccess;
            Errors = errors;
        }

        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// All failure errors associated with this result. Empty when <see cref="IsSuccess"/> is true.
        /// </summary>
        public IReadOnlyList<Error> Errors { get; }

        /// <summary>
        /// The first error, or <see cref="Error.None"/> when successful. Kept for backwards compatibility.
        /// </summary>
        public Error Error => Errors.Count > 0 ? Errors[0] : Error.None;

        public static Result Success() => new(true, Error.None);
        public static Result Failure(Error error) => new(false, error);
        public static Result Failure(IEnumerable<Error> errors) => new(false, errors.ToArray());

        public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
        public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
        public static Result<TValue> Failure<TValue>(IEnumerable<Error> errors) => new(default, false, errors.ToArray());
    }

    public class Result<TValue> : Result
    {
        private readonly TValue? _value;

        protected internal Result(TValue? value, bool isSuccess, Error error)
            : base(isSuccess, error) => _value = value;

        protected internal Result(TValue? value, bool isSuccess, IReadOnlyList<Error> errors)
            : base(isSuccess, errors) => _value = value;

        public TValue Value => IsSuccess
            ? _value!
            : throw new InvalidOperationException("The value of a failure result can not be accessed.");
    }
}
