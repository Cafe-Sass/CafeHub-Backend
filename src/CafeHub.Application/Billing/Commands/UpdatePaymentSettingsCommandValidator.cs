using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Billing.Commands
{
    public sealed class UpdatePaymentSettingsCommandValidator : AbstractValidator<UpdatePaymentSettingsCommand>
    {
        public UpdatePaymentSettingsCommandValidator()
        {
            RuleFor(command => command)
                .Custom((command, context) =>
                {
                    if (command.OnlineEnabled
                        && string.IsNullOrWhiteSpace(command.PaymentLink)
                        && string.IsNullOrWhiteSpace(command.PaymentAddress))
                    {
                        context.AddFailure(new FluentValidation.Results.ValidationFailure(
                            nameof(UpdatePaymentSettingsCommand.OnlineEnabled),
                            "Enabling online payment requires a payment link or a payment address."));
                    }
                });

            RuleFor(command => command.MethodLabel)
                .Must(label => string.IsNullOrWhiteSpace(label)
                    || (label.Trim().Length is >= 1 and <= CafePaymentSettings.MaximumMethodLabelLength
                        && !label.Trim().Any(char.IsControl)))
                .WithMessage(
                    $"The method label must be 1 to {CafePaymentSettings.MaximumMethodLabelLength} characters without control characters.");

            RuleFor(command => command.PaymentLink)
                .Must(link => string.IsNullOrWhiteSpace(link) || CafePaymentSettings.IsValidPaymentLink(link))
                .WithMessage(
                    $"The payment link must be an absolute https URL of up to {CafePaymentSettings.MaximumPaymentLinkLength} characters.");

            RuleFor(command => command.PaymentAddress)
                .Must(address => string.IsNullOrWhiteSpace(address)
                    || (address.Trim().Length is >= 1 and <= CafePaymentSettings.MaximumPaymentAddressLength
                        && !address.Trim().Any(char.IsControl)))
                .WithMessage(
                    $"The payment address must be 1 to {CafePaymentSettings.MaximumPaymentAddressLength} characters without control characters.");

            RuleFor(command => command.DisplayName)
                .Must(name => string.IsNullOrWhiteSpace(name)
                    || (name.Trim().Length is >= 1 and <= CafePaymentSettings.MaximumDisplayNameLength
                        && !name.Trim().Any(char.IsControl)))
                .WithMessage(
                    $"The display name must be 1 to {CafePaymentSettings.MaximumDisplayNameLength} characters without control characters.");
        }
    }
}
