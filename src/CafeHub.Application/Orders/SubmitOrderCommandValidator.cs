using CafeHub.Application.Idempotency;
using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Orders
{
    public sealed class SubmitOrderCommandValidator : AbstractValidator<SubmitOrderCommand>
    {
        public SubmitOrderCommandValidator()
        {
            RuleFor(command => command.IdempotencyKey).MustHaveValidIdempotencyKey();

            RuleFor(command => command.Code).Must(BranchTable.IsValidQrCode)
                .WithMessage("The table QR code must be 22 URL-safe base64url characters.");
            RuleFor(command => command.ServiceType).IsInEnum();
            RuleFor(command => command.Source).IsInEnum();
            RuleFor(command => command.ActingStaffUserId).Must(id => id != Guid.Empty)
                .WithMessage("The acting staff id cannot be empty.");
            RuleFor(command => command.ActingStaffUserId).Null()
                .When(command => command.Source == OrderSource.Guest)
                .WithMessage("A guest submission cannot name an acting staff user.");
            RuleFor(command => command.ActingStaffUserId).NotNull()
                .When(command => command.Source == OrderSource.Captain)
                .WithMessage("A captain submission requires the acting staff user.");

            RuleFor(command => command.GuestDisplayName).Must(Order.IsValidGuestDisplayName)
                .WithMessage("The guest name must be 1 to 64 characters after trimming, with no control characters.");
            RuleFor(command => command.GuestDisplayName)
                .Must(name => name is null || !name.Any(char.IsDigit))
                .WithMessage("The guest name must not contain digits or a phone number.");
            RuleFor(command => command.GuestDeviceId).Must(id => id is null || Order.IsValidGuestDeviceId(id))
                .WithMessage("The guest device id must be 16 to 64 letters, digits, hyphens, or underscores.");

            RuleFor(command => command.ExpectedTotal).GreaterThanOrEqualTo(0m)
                .When(command => command.ExpectedTotal.HasValue)
                .WithMessage("The expected total cannot be negative.");

            RuleFor(command => command.Lines).NotNull()
                .Must(lines => lines is { Count: > 0 })
                .WithMessage("An order requires at least one item.");
            RuleFor(command => command.Lines).Must(lines => lines is not null && lines.Count <= Order.MaximumItemCount)
                .WithMessage($"An order accepts at most {Order.MaximumItemCount} items.");

            RuleForEach(command => command.Lines).ChildRules(line =>
            {
                line.RuleFor(item => item.ProductId).NotEmpty();
                line.RuleFor(item => item.SizeId).NotEmpty();
                line.RuleFor(item => item.Quantity).InclusiveBetween(1, OrderItem.MaximumQuantity);
                line.RuleFor(item => item.ExpectedUnitPrice).GreaterThanOrEqualTo(0m)
                    .Must(price => decimal.Round(price, 2) == price)
                    .WithMessage("The expected unit price must have at most two decimal places.");
                line.RuleFor(item => item.AddonIds).NotNull()
                    .Must(addonIds => addonIds is { Count: <= OrderItem.MaximumAddonCount })
                    .WithMessage($"An order line accepts at most {OrderItem.MaximumAddonCount} add-ons.");
                line.RuleFor(item => item.AddonIds).Must(addonIds => addonIds is null
                        || (addonIds.All(id => id != Guid.Empty)
                            && addonIds.Distinct().Count() == addonIds.Count))
                    .WithMessage("Add-on ids must be non-empty and unique within a line.");
                line.RuleFor(item => item.ExpectedAddonPrices).NotNull()
                    .Must((item, prices) => prices is not null && prices.Count == item.AddonIds.Count)
                    .WithMessage("Every add-on requires its expected price.");
                line.RuleFor(item => item.ExpectedAddonPrices).Must(prices => prices is null
                        || prices.All(price => price >= 0m && decimal.Round(price, 2) == price))
                    .WithMessage("Expected add-on prices must be non-negative with at most two decimal places.");
            });
        }
    }
}
