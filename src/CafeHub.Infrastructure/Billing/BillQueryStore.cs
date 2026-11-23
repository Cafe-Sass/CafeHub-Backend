using CafeHub.Application.Billing.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Billing
{
    internal sealed class BillQueryStore(TenantDbContext context) : IBillQueryStore
    {
        public async Task<BillListPage> ListAsync(
            BillListRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var bills = context.Bills.AsNoTracking();
            if (request.BranchId is { } branchId)
            {
                bills = bills.Where(bill => bill.BranchId == branchId);
            }

            if (request.TableId is { } tableId)
            {
                bills = bills.Where(bill => bill.TableId == tableId);
            }

            if (request.Status is { } status)
            {
                bills = bills.Where(bill => bill.Status == status);
            }

            int? totalCount = request.IncludeTotalCount
                ? await bills.CountAsync(cancellationToken)
                : null;

            var rows = await bills
                .OrderByDescending(bill => bill.OpenedAt)
                .ThenByDescending(bill => bill.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize + 1)
                .Select(bill => new BillListEntry(
                    bill.Id,
                    bill.BranchId,
                    bill.TableId,
                    context.BranchTables
                        .Where(table => table.Id == bill.TableId)
                        .Select(table => table.Number)
                        .FirstOrDefault(),
                    bill.Status,
                    bill.PublicReference,
                    bill.OpenedAt,
                    bill.ClosedAt,
                    context.Orders
                        .Where(order => order.BillId == bill.Id && order.Status == OrderStatus.Accepted)
                        .Sum(order => (decimal?)order.TotalAmount) ?? 0m,
                    context.Payments
                        .Where(payment => payment.BillId == bill.Id && payment.Status == PaymentStatus.Confirmed)
                        .Sum(payment => (decimal?)payment.Amount) ?? 0m))
                .ToListAsync(cancellationToken);

            return new BillListPage(rows, totalCount);
        }

        public async Task<BillSettlement?> FindDetailAsync(
            Guid? branchId,
            Guid billId,
            CancellationToken cancellationToken)
        {
            var bills = context.Bills.AsNoTracking().Where(bill => bill.Id == billId);
            if (branchId is { } branch)
            {
                bills = bills.Where(bill => bill.BranchId == branch);
            }

            var bill = await bills.SingleOrDefaultAsync(cancellationToken);
            if (bill is null)
            {
                return null;
            }

            var orders = await context.Orders.AsNoTracking()
                .Where(order => order.BillId == billId && order.Status == OrderStatus.Accepted)
                .OrderBy(order => order.OrderNumber)
                .ToListAsync(cancellationToken);
            var payments = await context.Payments.AsNoTracking()
                .Where(payment => payment.BillId == billId)
                .OrderBy(payment => payment.CreatedAt)
                .ThenBy(payment => payment.Id)
                .ToListAsync(cancellationToken);
            return new BillSettlement(bill, orders, payments);
        }
    }
}
