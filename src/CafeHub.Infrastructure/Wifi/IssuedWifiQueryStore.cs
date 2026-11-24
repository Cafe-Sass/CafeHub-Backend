using CafeHub.Application.Wifi;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Wifi
{
    internal sealed class IssuedWifiQueryStore(TenantDbContext context) : IIssuedWifiQueryStore
    {
        public async Task<IssuedWifiListPage> ListAsync(
            IssuedWifiListRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var baseRows = from item in context.OrderItems.AsNoTracking()
                           join order in context.Orders.AsNoTracking() on item.OrderId equals order.Id
                           join package in context.WifiPackages.AsNoTracking()
                               on item.WifiPackageId equals package.Id
                           where item.WifiPackageId != null
                           select new { item, order, package };

            if (request.BranchId is { } branchId)
            {
                baseRows = baseRows.Where(row => row.order.BranchId == branchId);
            }

            if (request.PackageId is { } packageId)
            {
                baseRows = baseRows.Where(row => row.package.Id == packageId);
            }

            if (request.OrderNumber is { } orderNumber)
            {
                baseRows = baseRows.Where(row => row.order.OrderNumber == orderNumber);
            }

            if (request.Date is { } date)
            {
                baseRows = baseRows.Where(row => row.order.BusinessDate == date);
            }

            var projected = baseRows.Select(row => new
            {
                Entry = new IssuedWifiEntry(
                    row.package.Id,
                    row.package.Name,
                    row.package.DurationMinutes,
                    row.package.SpeedMbps,
                    row.order.OrderNumber,
                    row.order.PublicReference,
                    context.WifiVouchers
                        .Where(voucher => voucher.AssignedOrderItemId == row.item.Id)
                        .Select(voucher => voucher.AssignedAt)
                        .FirstOrDefault(),
                    context.WifiVouchers
                        .Where(voucher => voucher.AssignedOrderItemId == row.item.Id)
                        .Select(voucher => voucher.Code)
                        .FirstOrDefault(),
                    row.order.TotalAmount,
                    context.Payments
                        .Where(payment => payment.OrderId == row.order.Id
                            && payment.Status == PaymentStatus.Confirmed)
                        .Sum(payment => (decimal?)payment.Amount) ?? 0m,
                    row.order.BillId == null
                        ? 0m
                        : context.Orders
                            .Where(order => order.BillId == row.order.BillId
                                && order.Status == OrderStatus.Accepted)
                            .Sum(order => (decimal?)order.TotalAmount) ?? 0m,
                    row.order.BillId == null
                        ? 0m
                        : context.Payments
                            .Where(payment => payment.BillId == row.order.BillId
                                && payment.Status == PaymentStatus.Confirmed)
                            .Sum(payment => (decimal?)payment.Amount) ?? 0m),
                Code = context.WifiVouchers
                    .Where(voucher => voucher.AssignedOrderItemId == row.item.Id)
                    .Select(voucher => voucher.Code)
                    .FirstOrDefault(),
                Released = (context.Payments
                    .Where(payment => payment.OrderId == row.order.Id
                        && payment.Status == PaymentStatus.Confirmed)
                    .Sum(payment => (decimal?)payment.Amount) ?? 0m) >= row.order.TotalAmount
                    || (row.order.BillId != null
                        && (context.Payments
                                .Where(payment => payment.BillId == row.order.BillId
                                    && payment.Status == PaymentStatus.Confirmed)
                                .Sum(payment => (decimal?)payment.Amount) ?? 0m)
                            >= (context.Orders
                                .Where(order => order.BillId == row.order.BillId
                                    && order.Status == OrderStatus.Accepted)
                                .Sum(order => (decimal?)order.TotalAmount) ?? 0m)),
                row.order.SubmittedAt,
                row.item.Id,
            });

            projected = request.Status switch
            {
                IssuedWifiStatus.AwaitingVoucher => projected.Where(row => row.Code == null),
                IssuedWifiStatus.AwaitingPayment => projected.Where(row => row.Code != null && !row.Released),
                IssuedWifiStatus.Released => projected.Where(row => row.Code != null && row.Released),
                _ => projected,
            };

            int? totalCount = request.IncludeTotalCount
                ? await projected.CountAsync(cancellationToken)
                : null;

            var rows = await projected
                .OrderByDescending(row => row.SubmittedAt)
                .ThenByDescending(row => row.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize + 1)
                .Select(row => row.Entry)
                .ToListAsync(cancellationToken);

            return new IssuedWifiListPage(rows, totalCount);
        }
    }
}
