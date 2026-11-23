using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Tables;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Queries
{
    public sealed class GetGuestPaymentOptionsQueryHandler(
        ICafePaymentSettingsReader reader,
        ITenantContextAccessor tenantContextAccessor)
        : IQueryHandler<GetGuestPaymentOptionsQuery, GuestPaymentOptionsResponse>
    {
        public async Task<Result<GuestPaymentOptionsResponse>> Handle(
            GetGuestPaymentOptionsQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            if (!BranchTable.IsValidQrCode(query.Code) || !tenantContextAccessor.GetRequired().IsActive)
            {
                return Result.Failure<GuestPaymentOptionsResponse>(GuestQrErrors.NotFound());
            }

            var options = await reader.FindOptionsByQrCodeAsync(query.Code!, cancellationToken);
            if (options is null)
            {
                return Result.Failure<GuestPaymentOptionsResponse>(GuestQrErrors.NotFound());
            }

            return Result.Success(options);
        }
    }
}
