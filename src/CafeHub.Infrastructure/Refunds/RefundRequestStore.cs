using System.Runtime.ExceptionServices;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Idempotency;
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
