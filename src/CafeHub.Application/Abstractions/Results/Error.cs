namespace CafeHub.Application.Abstractions.Results
{
    public record Error
    {
        protected Error(string code, string message, ErrorType type)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("An error code must not be null or whitespace.", nameof(code));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("An error message must not be null or whitespace.", nameof(message));
            }

            Code = code;
            Message = message;
            Type = type;
        }

        public string Code { get; }

        public string Message { get; }

        public ErrorType Type { get; }

        public static Error Failure(string code, string message)
        {
            return new Error(code, message, ErrorType.Failure);
        }

        public static Error Validation(string code, string message)
        {
            return new Error(code, message, ErrorType.Validation);
        }

        public static Error NotFound(string code, string message)
        {
            return new Error(code, message, ErrorType.NotFound);
        }

        public static Error Conflict(string code, string message)
        {
            return new Error(code, message, ErrorType.Conflict);
        }

        public static Error Unauthorized(string code, string message)
        {
            return new Error(code, message, ErrorType.Unauthorized);
        }

        public static Error Forbidden(string code, string message)
        {
            return new Error(code, message, ErrorType.Forbidden);
        }
    }
}
