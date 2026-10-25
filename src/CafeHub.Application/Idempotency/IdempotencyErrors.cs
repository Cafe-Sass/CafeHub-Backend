using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Idempotency
{
    public static class IdempotencyErrors
    {
        public const string InvalidKeyCode = "Idempotency.KeyInvalid";
        public const string InvalidKeyMessage = "The Idempotency-Key must be 8 to 64 characters long and may contain only ASCII letters, digits, hyphen, or underscore.";

        public const string KeyReusedCode = "Idempotency.KeyReused";
        public const string KeyReusedMessage = "A request with this idempotency key was already processed.";

        public static Error InvalidKey()
        {
            return Error.Validation(InvalidKeyCode, InvalidKeyMessage);
        }

        public static Error KeyReused()
        {
            return Error.Conflict(KeyReusedCode, KeyReusedMessage);
        }
    }
}
