namespace CafeHub.Application.Abstractions.Results
{
    public sealed record ValidationError : Error
    {
        private const string FailedCode = "Validation.Failed";
        private const string FailedMessage = "One or more validation errors occurred.";

        private ValidationError(IReadOnlyList<FieldError> fieldErrors)
            : base(FailedCode, FailedMessage, ErrorType.Validation)
        {
            Errors = fieldErrors;
        }

        public IReadOnlyList<FieldError> Errors { get; }

        public static ValidationError Create(IEnumerable<FieldError> fieldErrors)
        {
            return new ValidationError(fieldErrors.ToArray());
        }
    }
}
