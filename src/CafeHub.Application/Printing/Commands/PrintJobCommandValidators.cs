using CafeHub.Domain.Tenants;
using FluentValidation;

namespace CafeHub.Application.Printing.Commands
{
    public sealed class ClaimPrintJobCommandValidator : AbstractValidator<ClaimPrintJobCommand>
    {
        public ClaimPrintJobCommandValidator()
        {
            RuleFor(command => command.ConnectorId).NotEmpty()
                .WithMessage("The connector id is required.");
            RuleFor(command => command.BranchId).NotEmpty()
                .WithMessage("The branch id is required.");
        }
    }

    public sealed class ReportPrintJobSucceededCommandValidator
        : AbstractValidator<ReportPrintJobSucceededCommand>
    {
        public ReportPrintJobSucceededCommandValidator()
        {
            RuleFor(command => command.JobId).NotEmpty()
                .WithMessage("The print job id is required.");
            RuleFor(command => command.BranchId).NotEmpty()
                .WithMessage("The branch id is required.");
        }
    }

    public sealed class ReportPrintJobFailedCommandValidator
        : AbstractValidator<ReportPrintJobFailedCommand>
    {
        public ReportPrintJobFailedCommandValidator()
        {
            RuleFor(command => command.JobId).NotEmpty()
                .WithMessage("The print job id is required.");
            RuleFor(command => command.BranchId).NotEmpty()
                .WithMessage("The branch id is required.");
            RuleFor(command => command.FailureCode).Must(PrintFailureCodes.IsSupported)
                .WithMessage("The failure code must be PrinterOffline, PaperOut, PayloadRejected, or Unknown.");
        }
    }

    public sealed class CancelPrintJobCommandValidator : AbstractValidator<CancelPrintJobCommand>
    {
        public CancelPrintJobCommandValidator()
        {
            RuleFor(command => command.JobId).NotEmpty()
                .WithMessage("The print job id is required.");
            RuleFor(command => command.BranchId).NotEmpty()
                .WithMessage("The branch id is required.");
        }
    }

    public sealed class RequeuePrintJobCommandValidator : AbstractValidator<RequeuePrintJobCommand>
    {
        public RequeuePrintJobCommandValidator()
        {
            RuleFor(command => command.JobId).NotEmpty()
                .WithMessage("The print job id is required.");
            RuleFor(command => command.BranchId).NotEmpty()
                .WithMessage("The branch id is required.");
        }
    }
}
