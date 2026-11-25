using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Printing.Contracts;
using CafeHub.Application.Printing.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Printing.Commands
{
    public sealed class ClaimPrintJobCommandHandler(
        IPrintJobOperationsStore jobStore,
        IPrintConnectorStore connectorStore,
        TimeProvider timeProvider)
        : ICommandHandler<ClaimPrintJobCommand, PrintJobResponse?>
    {
        public async Task<Result<PrintJobResponse?>> Handle(
            ClaimPrintJobCommand command,
            CancellationToken cancellationToken)
        {
            var job = await jobStore.ClaimNextAsync(
                command.BranchId,
                PrintJob.DefaultLease,
                cancellationToken);
            await connectorStore.TouchLastSeenAsync(
                command.ConnectorId,
                timeProvider.GetUtcNow(),
                cancellationToken);

            return Result.Success(job is null ? null : PrintJobResponse.From(job));
        }
    }

    public sealed class ReportPrintJobSucceededCommandHandler(IPrintJobOperationsStore store)
        : ICommandHandler<ReportPrintJobSucceededCommand, PrintJobResponse>
    {
        public Task<Result<PrintJobResponse>> Handle(
            ReportPrintJobSucceededCommand command,
            CancellationToken cancellationToken) =>
            store.ReportSucceededAsync(command.JobId, command.BranchId, cancellationToken);
    }

    public sealed class ReportPrintJobFailedCommandHandler(IPrintJobOperationsStore store)
        : ICommandHandler<ReportPrintJobFailedCommand, PrintJobResponse>
    {
        public Task<Result<PrintJobResponse>> Handle(
            ReportPrintJobFailedCommand command,
            CancellationToken cancellationToken) =>
            store.ReportFailedAsync(
                command.JobId,
                command.BranchId,
                command.FailureCode!,
                cancellationToken);
    }

    public sealed class CancelPrintJobCommandHandler(
        IPrintJobOperationsStore store,
        ICurrentTenantUser currentUser)
        : ICommandHandler<CancelPrintJobCommand, PrintJobResponse>
    {
        public Task<Result<PrintJobResponse>> Handle(
            CancelPrintJobCommand command,
            CancellationToken cancellationToken) =>
            PrinterCommandScope.RunAsync(
                currentUser,
                command.BranchId,
                actorId => store.CancelAsync(
                    command.JobId,
                    command.BranchId,
                    actorId,
                    cancellationToken));
    }

    public sealed class RequeuePrintJobCommandHandler(
        IPrintJobOperationsStore store,
        ICurrentTenantUser currentUser)
        : ICommandHandler<RequeuePrintJobCommand, PrintJobResponse>
    {
        public Task<Result<PrintJobResponse>> Handle(
            RequeuePrintJobCommand command,
            CancellationToken cancellationToken) =>
            PrinterCommandScope.RunAsync(
                currentUser,
                command.BranchId,
                actorId => store.RequeueAsync(
                    command.JobId,
                    command.BranchId,
                    actorId,
                    cancellationToken));
    }
}
