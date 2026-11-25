using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Printing.Contracts
{
    /// <summary>
    /// Adds print jobs to the current unit of work. Implementations never save and never let a print
    /// problem fail the business event: a missing route or an unexpected error is reported and skipped.
    /// </summary>
    public interface IPrintJobQueue
    {
        Task EnqueueOrderTicketAsync(Order order, CancellationToken cancellationToken);

        Task EnqueueReceiptAsync(Receipt receipt, CancellationToken cancellationToken);
    }
}
