using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Tables.DTOs;

namespace CafeHub.Application.Tables.Queries
{
    public sealed record GetTableQrQuery(Guid BranchId, Guid TableId) : IQuery<TableQrData>, ISensitiveRequest;
}
