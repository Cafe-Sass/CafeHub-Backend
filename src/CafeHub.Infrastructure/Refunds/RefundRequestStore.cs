using System.Runtime.ExceptionServices;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Idempotency;
using CafeHub.Application.Refunds;
using CafeHub.Application.Refunds.Contracts;
using CafeHub.Application.Refunds.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Refunds
{
    internal sealed class RefundRequestStore(TenantDbContext context) : IRefundRequestStore
    {
        public async Task<IReadOnlyList<GuestRefundablePayment>?> ListConfirmedPaymentsForBillAsync(
            string billReference,
            CancellationToken cancellationToken)
        {
            var billId = await context.Bills.AsNoTracking()
                .Where(bill => bill.PublicReference == billReference)
                .Select(bill => (Guid?)bill.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (billId is null)
            {
                return null;
            }

            var payments = await context.Payments.AsNoTracking()
                .Where(payment => payment.BillId == billId && payment.Status == PaymentStatus.Confirmed)
                .OrderBy(payment => payment.ConfirmedAt)
                .ThenBy(payment => payment.Id)
                .Select(payment => new
                {
                    payment.Id,
                    payment.Method,
                    payment.Amount,
                    payment.ConfirmedAt,
                })
                .ToListAsync(cancellationToken);

            return payments.Select(payment => new GuestRefundablePayment(
                    payment.Id,
                    payment.Method.ToString(),
                    payment.Amount,
                    payment.ConfirmedAt!.Value))
                .ToArray();
        }

        public async Task<GuestRefundRequestStatus?> FindGuestStatusAsync(
            string publicReference,
            CancellationToken cancellationToken)
        {
            var request = await context.RefundRequests.AsNoTracking()
                .Where(candidate => candidate.PublicReference == publicReference)
                .Select(candidate => new
                {
                    candidate.PublicReference,
                    candidate.Status,
                    candidate.ResolutionNote,
                })
                .SingleOrDefaultAsync(cancellationToken);

            return request is null
                ? null
                : GuestRefundRequestStatus.From(
                    request.PublicReference,
                    request.Status,
                    request.ResolutionNote);
        }

        public async Task<RefundRequestSubmissionContext?> FindForSubmissionAsync(
            string billReference,
            Guid paymentId,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            var bill = await context.Bills.AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.PublicReference == billReference, cancellationToken);
            if (bill is null)
            {
                return null;
            }

            await context.LockBillAsync(bill.Id, cancellationToken);

            var existing = await context.RefundRequests.AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.BranchId == bill.BranchId
                    && candidate.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return new RefundRequestSubmissionContext(
                    bill,
                    null,
                    0m,
                    0,
                    existing.BillId == bill.Id ? existing : null,
                    existing.BillId != bill.Id);
            }

            var payment = await context.Payments.AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == paymentId
                    && candidate.BillId == bill.Id
                    && candidate.Status == PaymentStatus.Confirmed, cancellationToken);
            if (payment is null)
            {
                return new RefundRequestSubmissionContext(bill, null, 0m, 0, null, false);
            }

            var approvedRefundedAmount = await context.RefundRequests.AsNoTracking()
                .Where(candidate => candidate.PaymentId == paymentId
                    && candidate.Status == RefundRequestStatus.Approved)
                .SumAsync(candidate => candidate.ClaimedAmount, cancellationToken);
            var openRequestCount = await context.RefundRequests.AsNoTracking()
                .CountAsync(candidate => candidate.BillId == bill.Id
                    && (candidate.Status == RefundRequestStatus.Open || candidate.Status == RefundRequestStatus.InReview),
                    cancellationToken);

            return new RefundRequestSubmissionContext(
                bill,
                payment,
                approvedRefundedAmount,
                openRequestCount,
                null,
                false);
        }

        public async Task<Result<RefundRequestInsertResult>> AddAsync(
            RefundRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            context.RefundRequests.Add(request);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return Result.Success(new RefundRequestInsertResult(request, false));
            }
            catch (DbUpdateException exception) when (IsIdempotencyKeyClash(exception))
            {
                Detach(request);
                var existing = await context.RefundRequests.AsNoTracking()
                    .SingleOrDefaultAsync(candidate => candidate.BranchId == request.BranchId
                        && candidate.IdempotencyKey == request.IdempotencyKey, cancellationToken);
                if (existing is null)
                {
                    ExceptionDispatchInfo.Capture(exception).Throw();
                }

                if (existing.BillId != request.BillId)
                {
                    return Result.Failure<RefundRequestInsertResult>(IdempotencyErrors.KeyReused());
                }

                return Result.Success(new RefundRequestInsertResult(existing, true));
            }
        }

        public async Task<RefundRequestListPage> ListAsync(
            RefundRequestListRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var requests = context.RefundRequests.AsNoTracking();
            if (request.BranchId is { } branchId)
            {
                requests = requests.Where(candidate => candidate.BranchId == branchId);
            }

            if (request.Status is { } status)
            {
                requests = requests.Where(candidate => candidate.Status == status);
            }

            if (request.AssignedUserId is { } assignedUserId)
            {
                requests = requests.Where(candidate => candidate.AssignedUserId == assignedUserId);
            }

            if (request.BillId is { } billId)
            {
                requests = requests.Where(candidate => candidate.BillId == billId);
            }

            int? totalCount = request.IncludeTotalCount
                ? await requests.CountAsync(cancellationToken)
                : null;

            var rows = await requests
                .OrderByDescending(candidate => candidate.CreatedAt)
                .ThenByDescending(candidate => candidate.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize + 1)
                .Select(candidate => new RefundRequestListItem(
                    candidate.Id,
                    candidate.BillId,
                    candidate.PaymentId,
                    candidate.BranchId,
                    candidate.PublicReference,
                    candidate.Status.ToString(),
                    candidate.Reason,
                    candidate.ClaimedAmount,
                    candidate.GuestDisplayName,
                    candidate.AssignedUserId,
                    candidate.CreatedAt,
                    candidate.UpdatedAt))
                .ToListAsync(cancellationToken);

            return new RefundRequestListPage(rows, totalCount);
        }

        public async Task<RefundRequest?> FindAsync(
            Guid? branchId,
            Guid refundRequestId,
            CancellationToken cancellationToken)
        {
            IQueryable<RefundRequest> requests = context.RefundRequests
                .Include(request => request.Events)
                .Include(request => request.Attachments)
                .Where(request => request.Id == refundRequestId);
            if (branchId is { } branch)
            {
                requests = requests.Where(request => request.BranchId == branch);
            }

            return await requests.SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<RefundRequestAttachment?> FindAttachmentAsync(
            Guid? branchId,
            Guid refundRequestId,
            Guid attachmentId,
            CancellationToken cancellationToken)
        {
            var requests = context.RefundRequests.AsNoTracking()
                .Where(request => request.Id == refundRequestId);
            if (branchId is { } branch)
            {
                requests = requests.Where(request => request.BranchId == branch);
            }

            return await requests
                .SelectMany(request => request.Attachments)
                .SingleOrDefaultAsync(attachment => attachment.Id == attachmentId, cancellationToken);
        }

        public async Task<bool> IsAssignableStaffAsync(
            Guid branchId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            var user = await context.TenantUsers.AsNoTracking()
                .Where(candidate => candidate.Id == userId && candidate.DeletedAt == null)
                .Select(candidate => new { candidate.UserKind, candidate.BranchId })
                .SingleOrDefaultAsync(cancellationToken);

            return user is not null
                && RefundRequestAssignment.IsAllowed(user.UserKind, user.BranchId, branchId);
        }

        public async Task<Result> SaveAsync(RefundRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return Result.Success();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure(RefundRequestErrors.ConcurrencyConflict());
            }
        }

        private static bool IsIdempotencyKeyClash(DbUpdateException exception) =>
            exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName == "uq_refund_requests_branch_idempotency_key";

        private void Detach(RefundRequest request)
        {
            foreach (var attachment in request.Attachments)
            {
                context.Entry(attachment).State = EntityState.Detached;
            }

            foreach (var entry in request.Events)
            {
                context.Entry(entry).State = EntityState.Detached;
            }

            context.Entry(request).State = EntityState.Detached;
        }
    }
}
