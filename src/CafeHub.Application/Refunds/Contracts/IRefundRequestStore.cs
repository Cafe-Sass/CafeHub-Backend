using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Refunds.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Refunds.Contracts
{
    public interface IRefundRequestStore
    {
        Task<IReadOnlyList<GuestRefundablePayment>?> ListConfirmedPaymentsForBillAsync(
            string billReference,
            CancellationToken cancellationToken);

        Task<GuestRefundRequestStatus?> FindGuestStatusAsync(
            string publicReference,
            CancellationToken cancellationToken);

        Task<RefundRequestSubmissionContext?> FindForSubmissionAsync(
            string billReference,
            Guid paymentId,
            string idempotencyKey,
            CancellationToken cancellationToken);

        Task<Result<RefundRequestInsertResult>> AddAsync(
            RefundRequest request,
            CancellationToken cancellationToken);

        Task<RefundRequestListPage> ListAsync(
            RefundRequestListRequest request,
            CancellationToken cancellationToken);

        Task<RefundRequest?> FindAsync(
            Guid? branchId,
            Guid refundRequestId,
            CancellationToken cancellationToken);

        Task<RefundRequestAttachment?> FindAttachmentAsync(
            Guid? branchId,
            Guid refundRequestId,
            Guid attachmentId,
            CancellationToken cancellationToken);

        Task<bool> IsAssignableStaffAsync(
            Guid branchId,
            Guid userId,
            CancellationToken cancellationToken);

        Task<Result> SaveAsync(
            RefundRequest request,
            CancellationToken cancellationToken);
    }
}
