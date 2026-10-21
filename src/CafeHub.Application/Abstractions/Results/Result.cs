namespace CafeHub.Application.Abstractions.Results
{
    public class Result
    {
        private readonly Error? error;

        protected Result(bool isSuccess, Error? error)
        {
            if (isSuccess && error is not null)
            {
                throw new InvalidOperationException("A successful result must not carry an error.");
            }

            if (!isSuccess && error is null)
            {
                throw new ArgumentNullException(nameof(error), "A failed result must carry an error.");
            }

            IsSuccess = isSuccess;
            this.error = error;
        }

        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        public Error? Error => error;

        public static Result Success()
        {
            return new Result(true, null);
        }

        public static Result Failure(Error error)
        {
            return new Result(false, error);
        }

        public static Result<T> Success<T>(T value)
        {
            return new Result<T>(value, true, null);
        }

        public static Result<T> Failure<T>(Error error)
        {
            return new Result<T>(default, false, error);
        }
    }
}
