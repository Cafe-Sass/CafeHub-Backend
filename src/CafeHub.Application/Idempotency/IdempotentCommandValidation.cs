using FluentValidation;

namespace CafeHub.Application.Idempotency
{
    public static class IdempotentCommandValidation
    {
        public static IRuleBuilderOptions<T, string?> MustHaveValidIdempotencyKey<T>(this IRuleBuilder<T, string?> ruleBuilder)
        {
            return ruleBuilder
                .Must(value => IdempotencyKey.IsValid(value))
                .WithMessage(IdempotencyErrors.InvalidKeyMessage);
        }
    }
}
