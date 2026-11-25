using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Printing.Commands
{
    public sealed class RegisterPrinterCommandValidator : AbstractValidator<RegisterPrinterCommand>
    {
        public RegisterPrinterCommandValidator()
        {
            RuleFor(command => command.BranchId).NotEmpty()
                .WithMessage("The branch id is required.");
            RuleFor(command => command.StationId)
                .Must(stationId => stationId is null || stationId != Guid.Empty)
                .WithMessage("The station id cannot be empty.");
            RuleFor(command => command.Name).Must(Printer.IsValidName)
                .WithMessage($"The printer name must be 1 to {Printer.MaximumNameLength} characters without control characters.");
            RuleFor(command => command.Profile).Must(IsSupportedProfile)
                .WithMessage("The printer profile must be Escpos80mm or Escpos58mm.");
            RuleFor(command => command.ConnectionKind).Must(Printer.IsValidConnectionKind)
                .WithMessage($"The connection kind must be 1 to {Printer.MaximumConnectionKindLength} characters without control characters.");
        }

        internal static bool IsSupportedProfile(PrinterProfile profile) =>
            Enum.IsDefined(profile) && PrinterProfiles.TryGet(profile, out _);
    }

    public sealed class UpdatePrinterCommandValidator : AbstractValidator<UpdatePrinterCommand>
    {
        public UpdatePrinterCommandValidator()
        {
            RuleFor(command => command.PrinterId).NotEmpty()
                .WithMessage("The printer id is required.");
            RuleFor(command => command.BranchId).NotEmpty()
                .WithMessage("The branch id is required.");
            RuleFor(command => command.StationId)
                .Must(stationId => stationId is null || stationId != Guid.Empty)
                .WithMessage("The station id cannot be empty.");
            RuleFor(command => command.Name).Must(Printer.IsValidName)
                .WithMessage($"The printer name must be 1 to {Printer.MaximumNameLength} characters without control characters.");
            RuleFor(command => command.Profile).Must(RegisterPrinterCommandValidator.IsSupportedProfile)
                .WithMessage("The printer profile must be Escpos80mm or Escpos58mm.");
            RuleFor(command => command.ConnectionKind).Must(Printer.IsValidConnectionKind)
                .WithMessage($"The connection kind must be 1 to {Printer.MaximumConnectionKindLength} characters without control characters.");
            RuleFor(command => command.ConcurrencyToken).NotNull()
                .WithMessage("The concurrency token is required.");
        }
    }

    public sealed class SetPrinterStatusCommandValidator : AbstractValidator<SetPrinterStatusCommand>
    {
        public SetPrinterStatusCommandValidator()
        {
            RuleFor(command => command.PrinterId).NotEmpty()
                .WithMessage("The printer id is required.");
            RuleFor(command => command.BranchId).NotEmpty()
                .WithMessage("The branch id is required.");
            RuleFor(command => command.Status).Must(status => Enum.IsDefined(status))
                .WithMessage("The printer status must be Active or Inactive.");
            RuleFor(command => command.ConcurrencyToken).NotNull()
                .WithMessage("The concurrency token is required.");
        }
    }

    public sealed class SetPrintRouteCommandValidator : AbstractValidator<SetPrintRouteCommand>
    {
        public SetPrintRouteCommandValidator()
        {
            RuleFor(command => command.BranchId).NotEmpty()
                .WithMessage("The branch id is required.");
            RuleFor(command => command.StationId)
                .Must(stationId => stationId is null || stationId != Guid.Empty)
                .WithMessage("The station id cannot be empty.");
            RuleFor(command => command.DocumentType).Must(documentType => Enum.IsDefined(documentType))
                .WithMessage("The document type must be OrderTicket or Receipt.");
            RuleFor(command => command.PrinterId).NotEmpty()
                .WithMessage("The printer id is required.");
        }
    }
}
