using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Tables.Contracts;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Tables.Queries
{
    public sealed class ResolveGuestQrQueryHandler(ITableStore store, ITenantContextAccessor tenantContextAccessor)
        : IQueryHandler<ResolveGuestQrQuery, GuestQrContext>
    {
        public async Task<Result<GuestQrContext>> Handle(ResolveGuestQrQuery query, CancellationToken cancellationToken)
        {
            if (!BranchTable.IsValidQrCode(query.Code) || !tenantContextAccessor.GetRequired().IsActive)
            {
                return Result.Failure<GuestQrContext>(GuestQrErrors.NotFound());
            }

            var table = await store.ResolveGuestQrAsync(query.Code!, cancellationToken);
            return table is null
                ? Result.Failure<GuestQrContext>(GuestQrErrors.NotFound())
                : Result.Success(table);
        }
    }
}
