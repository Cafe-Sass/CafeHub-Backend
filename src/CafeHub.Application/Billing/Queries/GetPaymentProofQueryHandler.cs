using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Application.Media;
using CafeHub.Application.Media.Contracts;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Queries
{
    public sealed class GetPaymentProofQueryHandler(
        IPaymentQueryStore store,
        IMediaStorage storage,
        ICurrentTenantUser currentUser,
        ITenantContextAccessor tenantContextAccessor)
        : IQueryHandler<GetPaymentProofQuery, PaymentProofFile>
    {
        public async Task<Result<PaymentProofFile>> Handle(
            GetPaymentProofQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<PaymentProofFile>(scope.Error!);
            }

            var payment = await store.FindProofAsync(scope.Value, query.PaymentId, cancellationToken);
            if (payment is null
                || payment.Method is not PaymentMethod.Online
                || payment.MediaReference is null
                || !MediaKey.TryParse(payment.MediaReference, out var key)
                || key is null
                || key.Scope.TenantId != tenantContextAccessor.GetRequired().TenantId)
            {
                return Result.Failure<PaymentProofFile>(BillingErrors.PaymentNotFound());
            }

            var content = await storage.OpenAsync(key, cancellationToken);
            if (content is null)
            {
                return Result.Failure<PaymentProofFile>(BillingErrors.PaymentNotFound());
            }

            var detected = MediaFileValidator.Detect(content);
            if (detected is null)
            {
                await content.DisposeAsync();
                return Result.Failure<PaymentProofFile>(BillingErrors.PaymentNotFound());
            }

            return Result.Success(new PaymentProofFile(
                content,
                MediaContentTypes.ToContentType(detected.Value)));
        }
    }
}
