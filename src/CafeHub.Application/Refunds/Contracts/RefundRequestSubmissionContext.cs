using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Refunds.Contracts
{
    public sealed record RefundRequestSubmissionContext(
        Bill Bill,
        Payment? Payment,
        decimal ApprovedRefundedAmount,
        int OpenRequestCount,
        RefundRequest? ExistingRequest,
        bool IdempotencyKeyUsedByAnotherBill);
}
