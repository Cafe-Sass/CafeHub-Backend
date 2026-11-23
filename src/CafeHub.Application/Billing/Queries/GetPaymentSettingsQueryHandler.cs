using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.DTOs;

namespace CafeHub.Application.Billing.Queries
{
    public sealed class GetPaymentSettingsQueryHandler(ICafePaymentSettingsReader reader)
        : IQueryHandler<GetPaymentSettingsQuery, PaymentSettingsResponse>
    {
        public async Task<Result<PaymentSettingsResponse>> Handle(
            GetPaymentSettingsQuery query,
            CancellationToken cancellationToken)
        {
            var settings = await reader.GetAsync(cancellationToken);
            if (settings is null)
            {
                return Result.Failure<PaymentSettingsResponse>(PaymentSettingsErrors.NotFound());
            }

            return Result.Success(settings);
        }
    }
}
