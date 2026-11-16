using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Pagination;
using CafeHub.Application.Tables.Contracts;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Tables.Queries
{
    /// <summary>
    /// Lists a branch's tables, ordered by number, with optional status and number-range filters.
    /// Marked as a tenant transactional query so the read runs against the tenant context only.
    /// </summary>
    public sealed record ListTablesQuery : IQuery<OffsetPage<TableResponse>>
    {
        public required Guid BranchId { get; init; }

        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }

        public BranchTableStatus? Status { get; init; }

        public int? FromNumber { get; init; }

        public int? ToNumber { get; init; }
    }
}
