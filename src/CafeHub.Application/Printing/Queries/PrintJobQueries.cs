using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Printing.DTOs;

namespace CafeHub.Application.Printing.Queries
{
    public sealed record ListPrintJobsQuery(
        Guid? BranchId,
        string? Status,
        Guid? PrinterId,
        string? DocumentType) : IQuery<IReadOnlyList<PrintJobResponse>>;

    public sealed record ListPrintConnectorsQuery(Guid? BranchId)
        : IQuery<IReadOnlyList<PrintConnectorResponse>>;
}
