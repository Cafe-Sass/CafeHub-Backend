using System.Text.RegularExpressions;
using CafeHub.Domain.Plans;
using FluentValidation;
using FluentValidation.Results;

namespace CafeHub.Application.Plans.Commands
{
    public sealed class CreatePlanVersionCommandValidator : AbstractValidator<CreatePlanVersionCommand>
    {
        public const decimal MaximumPrice = 9_999_999_999.99m;
        public const int MaximumEntitlementKeyLength = 128;
        public const int MaximumEntitlementValueLength = 512;

        private const string CurrencyPattern = "^[a-z]{3}$";
        private const string EntitlementKeyPattern = "^[a-z][a-z0-9_.]*$";
        private const string DuplicateEntitlementKeyMessage = "An entitlement key must appear at most once per plan version.";

        private static readonly Regex CurrencyRegex = new(CurrencyPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex EntitlementKeyRegex = new(EntitlementKeyPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public CreatePlanVersionCommandValidator()
        {
            RuleFor(command => command.PlanId)
                .NotEmpty();

            RuleFor(command => command.Price)
                .InclusiveBetween(0m, MaximumPrice)
                .PrecisionScale(12, 2, ignoreTrailingZeros: false);

            RuleFor(command => command.Currency)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(currency => currency is not null && CurrencyRegex.IsMatch(currency))
                .WithMessage("The currency code must be three lowercase letters.");

            RuleFor(command => command.Period)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .IsEnumName(typeof(BillingPeriod))
                .WithMessage("The billing period must be Monthly or Annual.");

            RuleFor(command => command.Entitlements)
                .NotNull();

            RuleFor(command => command.Entitlements)
                .Custom(ValidateUniqueEntitlementKeys);

            RuleForEach(command => command.Entitlements)
                .ChildRules(entitlement =>
                {
                    entitlement.RuleFor(item => item.Key)
                        .Cascade(CascadeMode.Stop)
                        .NotEmpty()
                        .MaximumLength(MaximumEntitlementKeyLength)
                        .Must(key => key is not null && EntitlementKeyRegex.IsMatch(key))
                        .WithMessage(
                            "The entitlement key is required and must start with a lowercase letter and contain only lowercase letters, digits, underscores, and dots, up to 128 characters.");

                    entitlement.RuleFor(item => item.Value)
                        .Cascade(CascadeMode.Stop)
                        .NotEmpty()
                        .MaximumLength(MaximumEntitlementValueLength);
                });
        }

        private static void ValidateUniqueEntitlementKeys(
            IReadOnlyList<PlanEntitlementRequest>? entitlements,
            ValidationContext<CreatePlanVersionCommand> context)
        {
            if (entitlements is null)
            {
                return;
            }

            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < entitlements.Count; index++)
            {
                var entitlement = entitlements[index];
                if (entitlement is null || entitlement.Key is null)
                {
                    continue;
                }

                if (!seenKeys.Add(entitlement.Key))
                {
                    context.AddFailure(new ValidationFailure($"Entitlements[{index}].Key", DuplicateEntitlementKeyMessage));
                }
            }
        }
    }
}
