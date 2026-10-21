namespace CafeHub.Application.Abstractions.Results
{
    public sealed class Result<T> : Result
    {
        private readonly T? value;

        internal Result(T? value, bool isSuccess, Error? error)
            : base(isSuccess, error)
        {
            this.value = value;
        }

        public T Value
        {
            get
            {
                if (IsFailure)
                {
                    throw new InvalidOperationException("The value of a failed result cannot be read.");
                }

                return value!;
            }
        }
    }
}
