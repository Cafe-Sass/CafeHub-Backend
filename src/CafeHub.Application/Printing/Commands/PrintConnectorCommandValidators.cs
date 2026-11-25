using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Printing.Commands
{
    public sealed class CreatePrintConnectorCommandValidator : AbstractValidator<CreatePrintConnectorCommand>
    {
        public CreatePrintConnectorCommandValidator()
        {
            RuleFor(command => command.BranchId).NotEmpty()
                .WithMessage("The branch id is required.");
            RuleFor(command => command.Name).Must(PrintConnector.IsValidName)
                .WithMessage(
                    $"The connector name must be 1 to {PrintConnector.MaximumNameLength} characters without control characters.");
        }
    }

    public sealed class RevokePrintConnectorCommandValidator : AbstractValidator<RevokePrintConnectorCommand>
    {
        public RevokePrintConnectorCommandValidator()
        {
            RuleFor(command => command.ConnectorId).NotEmpty()
                .WithMessage("The connector id is required.");
            RuleFor(command => command.BranchId).NotEmpty()
                .WithMessage("The branch id is required.");
        }
    }
}
