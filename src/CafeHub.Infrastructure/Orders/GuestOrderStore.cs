using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Wifi;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Orders
{
    internal sealed class GuestOrderStore(TenantDbContext context) : IGuestOrderStore
    {
        public async Task<GuestOrderEntry?> FindByPublicReferenceAsync(
            string publicReference,
            CancellationToken cancellationToken)
        {
            var order = await context.Orders.AsNoTracking()
                .Include(candidate => candidate.Items)
                .SingleOrDefaultAsync(
                    candidate => candidate.PublicReference == publicReference,
                    cancellationToken);
            if (order is null)
            {
                return null;
            }

            int? tableNumber = null;
            if (order.TableId is { } tableId)
            {
                tableNumber = await context.BranchTables.AsNoTracking()
                    .Where(table => table.Id == tableId)
                    .Select(table => (int?)table.Number)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            string? billReference = null;
            decimal? billTotal = null;
            decimal? billPaid = null;
            if (order.BillId is { } billId)
            {
                billReference = await context.Bills.AsNoTracking()
                    .Where(bill => bill.Id == billId)
                    .Select(bill => bill.PublicReference)
                    .FirstOrDefaultAsync(cancellationToken);
                billTotal = await context.Orders.AsNoTracking()
                    .Where(candidate => candidate.BillId == billId && candidate.Status == OrderStatus.Accepted)
                    .SumAsync(candidate => (decimal?)candidate.TotalAmount, cancellationToken) ?? 0m;
                billPaid = await context.Payments.AsNoTracking()
                    .Where(payment => payment.BillId == billId && payment.Status == PaymentStatus.Confirmed)
                    .SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0m;
            }

            var orderPaid = await context.Payments.AsNoTracking()
                .Where(payment => payment.OrderId == order.Id && payment.Status == PaymentStatus.Confirmed)
                .SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0m;
            var released = WifiCodeRelease.IsReleased(
                order.TotalAmount,
                orderPaid,
                billTotal,
                billPaid);

            var itemIds = order.Items.Select(item => item.Id).ToArray();
            var codes = new Dictionary<Guid, string>();
            if (itemIds.Length > 0)
            {
                codes = await context.WifiVouchers.AsNoTracking()
                    .Where(voucher => voucher.AssignedOrderItemId != null
                        && itemIds.Contains(voucher.AssignedOrderItemId.Value))
                    .ToDictionaryAsync(
                        voucher => voucher.AssignedOrderItemId!.Value,
                        voucher => voucher.Code,
                        cancellationToken);
            }

            return new GuestOrderEntry(
                order.Id,
                order.OrderNumber,
                tableNumber,
                order.ServiceType.ToString(),
                order.Status.ToString(),
                order.RejectionReason,
                order.SubmittedAt,
                order.ResponseDeadline,
                order.TotalAmount,
                order.Items
                    .OrderBy(item => item.LineNumber)
                    .Select(item =>
                    {
                        var isWifi = item.WifiPackageId is not null;
                        return new GuestOrderEntryItem(
                            item.ProductName,
                            item.SizeName,
                            item.Quantity,
                            item.Addons.OrderBy(addon => addon.Id)
                                .Select(addon => addon.AddonName)
                                .ToList())
                        {
                            WifiDurationMinutes = item.WifiDurationMinutes,
                            WifiSpeedMbps = item.WifiSpeedMbps,
                            WifiCode = isWifi && released ? codes.GetValueOrDefault(item.Id) : null,
                            WifiCodePending = isWifi && !released,
                        };
                    })
                    .ToList())
            {
                BillReference = billReference,
            };
        }
    }
}
