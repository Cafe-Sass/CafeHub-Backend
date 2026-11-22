using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Orders;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Orders
{
    internal sealed class OrderDecisionStore(TenantDbContext context) : IOrderDecisionStore
    {
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
        }
    }
}
