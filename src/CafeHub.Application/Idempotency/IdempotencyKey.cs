using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Idempotency
{
    public sealed class IdempotencyKey : IEquatable<IdempotencyKey>
    {
        public const int MinLength = 8;
        public const int MaxLength = 64;

        private IdempotencyKey(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public static Result<IdempotencyKey> TryCreate(string? value)
        {
            if (!IsValid(value))
            {
                return Result.Failure<IdempotencyKey>(IdempotencyErrors.InvalidKey());
            }

            return Result.Success<IdempotencyKey>(new IdempotencyKey(value!));
        }

        public static bool IsValid(string? value)
        {
            if (value is null || value.Length < MinLength || value.Length > MaxLength)
            {
                return false;
            }

            return value.All(IsAllowedCharacter);
        }

        private static bool IsAllowedCharacter(char character)
        {
            return char.IsAsciiLetterOrDigit(character) || character is '-' or '_';
        }

        public bool Equals(IdempotencyKey? other)
        {
            return other is not null && Value == other.Value;
        }

        public override bool Equals(object? obj)
        {
            return obj is IdempotencyKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }
    }
}
