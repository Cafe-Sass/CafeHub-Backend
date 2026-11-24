using System.Runtime.ExceptionServices;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Billing
{
    internal sealed class PaymentStore(TenantDbContext context, ITenantAuditWriter auditWriter) : IPaymentStore
    {
        private const string TargetType = "payments";

        public async Task<BillSettlement?> FindSettlementAsync(
            Guid? branchId,
            Guid billId,
            CancellationToken cancellationToken)
        {
            var bills = context.Bills.Where(bill => bill.Id == billId);
            if (branchId is { } branch)
            {
                bills = bills.Where(bill => bill.BranchId == branch);
            }

            var bill = await bills.SingleOrDefaultAsync(cancellationToken);
            if (bill is null)
            {
                return null;
            }

            await context.LockBillAsync(bill.Id, cancellationToken);

            var orders = await context.Orders.AsNoTracking()
                .Where(order => order.BillId == billId)
                .OrderBy(order => order.OrderNumber)
                .ToListAsync(cancellationToken);
            var payments = await context.Payments.AsNoTracking()
                .Where(payment => payment.BillId == billId)
                .OrderBy(payment => payment.CreatedAt)
                .ThenBy(payment => payment.Id)
                .ToListAsync(cancellationToken);
            return new BillSettlement(bill, orders, payments);
        }

        public async Task<BillSettlement?> FindForProofAsync(
            string billReference,
            CancellationToken cancellationToken)
        {
            var bill = await context.Bills
                .Where(candidate => candidate.PublicReference == billReference)
                .SingleOrDefaultAsync(cancellationToken);
            if (bill is null)
            {
                return null;
            }

            await context.LockBillAsync(bill.Id, cancellationToken);

            var orders = await context.Orders.AsNoTracking()
                .Where(order => order.BillId == bill.Id)
                .OrderBy(order => order.OrderNumber)
                .ToListAsync(cancellationToken);
            var payments = await context.Payments.AsNoTracking()
                .Where(payment => payment.BillId == bill.Id)
                .OrderBy(payment => payment.CreatedAt)
                .ThenBy(payment => payment.Id)
                .ToListAsync(cancellationToken);
            return new BillSettlement(bill, orders, payments);
        }

        public async Task<PaymentReview?> FindReviewAsync(
            Guid? branchId,
            Guid paymentId,
            CancellationToken cancellationToken)
        {
            var billId = await context.Payments.AsNoTracking()
                .Where(payment => payment.Id == paymentId)
                .Select(payment => (Guid?)payment.BillId)
                .SingleOrDefaultAsync(cancellationToken);
            if (billId is null)
            {
                return null;
            }

            var bills = context.Bills.Where(bill => bill.Id == billId);
            if (branchId is { } branch)
            {
                bills = bills.Where(bill => bill.BranchId == branch);
            }

            var bill = await bills.SingleOrDefaultAsync(cancellationToken);
            if (bill is null)
            {
                return null;
            }

            await context.LockBillAsync(bill.Id, cancellationToken);

            var orders = await context.Orders.AsNoTracking()
                .Where(order => order.BillId == bill.Id)
                .OrderBy(order => order.OrderNumber)
                .ToListAsync(cancellationToken);
            var payments = await context.Payments
                .Where(payment => payment.BillId == bill.Id)
                .OrderBy(payment => payment.CreatedAt)
                .ThenBy(payment => payment.Id)
                .ToListAsync(cancellationToken);
            var reviewed = payments.SingleOrDefault(payment => payment.Id == paymentId);
            if (reviewed is null)
            {
                return null;
            }

            return new PaymentReview(new BillSettlement(bill, orders, payments), reviewed);
        }

        public async Task<Result<Payment>> AddProofAsync(Payment payment, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(payment);

            context.Payments.Add(payment);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return Result.Success(payment);
            }
            catch (DbUpdateException exception) when (IsIdempotencyKeyClash(exception))
            {
                context.Entry(payment).State = EntityState.Detached;
                var stored = await context.Payments.AsNoTracking()
                    .Where(candidate => candidate.BillId == payment.BillId
                        && candidate.IdempotencyKey == payment.IdempotencyKey
                        && candidate.Method == PaymentMethod.Online
                        && candidate.ClaimedAmount != null)
                    .SingleOrDefaultAsync(cancellationToken);
                if (stored is null)
                {
                    ExceptionDispatchInfo.Capture(exception).Throw();
                }

                return Result.Success(stored);
            }
        }

        public async Task<Result<Payment>> SaveReviewAsync(
            Payment payment,
            string? rejectionReason,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(payment);

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = payment.Status == PaymentStatus.Confirmed
                    ? "payments.online.confirmed.succeeded"
                    : "payments.online.rejected.succeeded",
                TargetType = TargetType,
                TargetId = payment.Id,
                ActorUserId = payment.UpdatedBy,
                Changes = TenantAuditChangeSets.PaymentReview(payment, rejectionReason),
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<Payment>(auditResult.Error!);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success(payment);
        }

        public async Task<Result<Payment>> AddAsync(Payment payment, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(payment);

            context.Payments.Add(payment);
            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "payments.cash.confirmed.succeeded",
                TargetType = TargetType,
                TargetId = payment.Id,
                ActorUserId = payment.ConfirmedByUserId,
                Changes = TenantAuditChangeSets.PaymentConfirmCash(payment),
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<Payment>(auditResult.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return Result.Success(payment);
            }
            catch (DbUpdateException exception) when (IsIdempotencyKeyClash(exception))
            {
                context.Entry(payment).State = EntityState.Detached;
                DetachPendingPaymentAudit(payment.Id);
                var stored = await context.Payments.AsNoTracking()
                    .Where(candidate => candidate.BillId == payment.BillId
                        && candidate.IdempotencyKey == payment.IdempotencyKey)
                    .SingleOrDefaultAsync(cancellationToken);
                if (stored is null)
                {
                    ExceptionDispatchInfo.Capture(exception).Throw();
                }

                return Result.Success(stored);
            }
        }

        private static bool IsIdempotencyKeyClash(DbUpdateException exception) =>
            exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName == "uq_payments_bill_idempotency_key";

        private void DetachPendingPaymentAudit(Guid paymentId)
        {
            foreach (var entry in context.ChangeTracker.Entries<TenantAuditRecord>()
                         .Where(entry => entry.State == EntityState.Added
                             && entry.Entity.SubjectType == TargetType
                             && entry.Entity.SubjectId == paymentId)
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
    }
}
