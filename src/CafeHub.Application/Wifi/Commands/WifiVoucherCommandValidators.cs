using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Wifi.Commands
{
    public sealed class ImportWifiVouchersCommandValidator : AbstractValidator<ImportWifiVouchersCommand>
    {
        public const int MaximumBatchSize = 1000;

        public ImportWifiVouchersCommandValidator()
        {
            RuleFor(command => command.PackageId)
                .NotEmpty()
                .WithMessage("The WiFi package id is required.");

            RuleFor(command => command.Codes)
                .NotNull()
                .Must(codes => codes!.Count is >= 1 and <= MaximumBatchSize)
                .WithMessage($"The import must carry between 1 and {MaximumBatchSize} codes.");
        }
    }

    public sealed class VoidWifiVoucherCommandValidator : AbstractValidator<VoidWifiVoucherCommand>
    {
        public VoidWifiVoucherCommandValidator()
        {
            RuleFor(command => command.PackageId)
                .NotEmpty()
                .WithMessage("The WiFi package id is required.");

            RuleFor(command => command.Code)
                .Must(WifiVoucher.IsValidCode)
                .WithMessage(
                    $"The voucher code must be {WifiVoucher.MinimumCodeLength} to {WifiVoucher.MaximumCodeLength} letters, digits, or hyphens.");
        }
    }
}
