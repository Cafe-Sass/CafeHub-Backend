using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Printing.DTOs;

namespace CafeHub.Application.Printing.Queries
{
    public sealed record ListPrintersQuery(Guid? BranchId) : IQuery<IReadOnlyList<PrinterResponse>>;

    public sealed record ListPrintRoutesQuery(Guid? BranchId) : IQuery<IReadOnlyList<PrintRouteResponse>>;
}
