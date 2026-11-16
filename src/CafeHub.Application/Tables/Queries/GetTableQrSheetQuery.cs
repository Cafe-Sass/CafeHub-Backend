using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Tables.DTOs;

namespace CafeHub.Application.Tables.Queries
{
    public sealed record GetTableQrSheetQuery(Guid BranchId) : IQuery<IReadOnlyList<TableQrData>>, ISensitiveRequest;
}
