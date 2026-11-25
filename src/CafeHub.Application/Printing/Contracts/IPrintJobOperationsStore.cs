using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Printing.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Printing.Contracts
{
    /// <summary>
    /// Persistence port for the connector queue and the staff job controls. Every read and write stays
    /// inside the supplied branch, an expired claim is returned to the queue before the next claim, and
    /// only a connector report can move a job to Succeeded.
    /// </summary>
    public interface IPrintJobOperationsStore
    {
        Task<PrintJob?> ClaimNextAsync(
            Guid branchId,
            TimeSpan lease,
            CancellationToken cancellationToken);

        Task<Result<PrintJobResponse>> ReportSucceededAsync(
            Guid jobId,
            Guid branchId,
            CancellationToken cancellationToken);

        Task<Result<PrintJobResponse>> ReportFailedAsync(
            Guid jobId,
            Guid branchId,
            string failureCode,
            CancellationToken cancellationToken);

        Task<Result<PrintJobResponse>> CancelAsync(
            Guid jobId,
            Guid branchId,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<Result<PrintJobResponse>> RequeueAsync(
            Guid jobId,
            Guid branchId,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<PrintJobResponse>> ListAsync(
            Guid branchId,
            PrintJobStatus? status,
            Guid? printerId,
            PrintDocumentType? documentType,
            CancellationToken cancellationToken);
    }
}
