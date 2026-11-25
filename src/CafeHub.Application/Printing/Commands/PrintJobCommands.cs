using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Printing.DTOs;

namespace CafeHub.Application.Printing.Commands
{
    public sealed record ClaimPrintJobCommand(
        Guid ConnectorId,
        Guid BranchId) : ICommand<PrintJobResponse?>, ITenantTransactionalCommand;

    public sealed record ReportPrintJobSucceededCommand(
        Guid JobId,
        Guid BranchId) : ICommand<PrintJobResponse>, ITenantTransactionalCommand;

    public sealed record ReportPrintJobFailedCommand(
        Guid JobId,
        Guid BranchId,
        string? FailureCode) : ICommand<PrintJobResponse>, ITenantTransactionalCommand;

    public sealed record CancelPrintJobCommand(
        Guid JobId,
        Guid BranchId) : ICommand<PrintJobResponse>, ITenantTransactionalCommand;

    public sealed record RequeuePrintJobCommand(
        Guid JobId,
        Guid BranchId) : ICommand<PrintJobResponse>, ITenantTransactionalCommand;
}
