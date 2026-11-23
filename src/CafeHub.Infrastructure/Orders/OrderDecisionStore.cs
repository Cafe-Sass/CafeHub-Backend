using System.Runtime.ExceptionServices;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Orders;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Orders
{
    internal sealed class OrderDecisionStore(TenantDbContext context) : IOrderDecisionStore
    {
        private const string BillTargetType = "bills";

        public async Task<Order?> FindDecisionTargetAsync(
            Guid? branchId,
            Guid orderId,
            CancellationToken cancellationToken)
        {
            var orders = context.Orders.Include(order => order.Items).Where(order => order.Id == orderId);
            if (branchId is { } branch)
            {
                orders = orders.Where(order => order.BranchId == branch);
            }

            return await orders.SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<Result<OrderStatus>> SaveDecisionAsync(Order order, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(order);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return Result.Success(order.Status);
            }
            catch (DbUpdateConcurrencyException)
            {
                var current = await context.Orders.AsNoTracking()
                    .Where(stored => stored.Id == order.Id)
                    .Select(stored => (OrderStatus?)stored.Status)
                    .FirstOrDefaultAsync(cancellationToken);
                return Result.Failure<OrderStatus>(
                    OrdersErrors.StatusConflict(current ?? order.Status));
            }
            catch (DbUpdateException exception) when (IsOpenBillClash(exception))
            {
                var losers = context.ChangeTracker.Entries<Bill>()
                    .Where(entry => entry.State == EntityState.Added)
                    .Select(entry => entry.Entity)
                    .ToList();
                foreach (var loser in losers)
                {
                    DetachPendingBillAudit(loser.Id);
                    context.Entry(loser).State = EntityState.Detached;
                }

                var winner = await context.Bills
                    .Where(candidate => candidate.TableId == order.TableId && candidate.Status == BillStatus.Open)
                    .SingleOrDefaultAsync(cancellationToken);
                if (winner is null)
                {
                    ExceptionDispatchInfo.Capture(exception).Throw();
                }

                order.AttachToBill(winner.Id);
                await context.SaveChangesAsync(cancellationToken);
                return Result.Success(order.Status);
            }
        }

        private static bool IsOpenBillClash(DbUpdateException exception) =>
            exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName == "uq_bills_table_open";

        private void DetachPendingBillAudit(Guid billId)
        {
            foreach (var entry in context.ChangeTracker.Entries<TenantAuditRecord>()
                         .Where(entry => entry.State == EntityState.Added
                             && entry.Entity.SubjectType == BillTargetType
                             && entry.Entity.SubjectId == billId)
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
    }
}
