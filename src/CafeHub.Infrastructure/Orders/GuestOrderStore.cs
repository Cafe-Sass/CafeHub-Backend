using CafeHub.Application.Orders.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Orders
{
    internal sealed class GuestOrderStore(TenantDbContext context) : IGuestOrderStore
    {
        public async Task<GuestOrderEntry?> FindByPublicReferenceAsync(
            string publicReference,
            CancellationToken cancellationToken) =>
            await context.Orders.AsNoTracking()
                .Where(order => order.PublicReference == publicReference)
                .Select(order => new GuestOrderEntry(
                    order.Id,
                    order.OrderNumber,
                    context.BranchTables
                        .Where(table => table.Id == order.TableId)
                        .Select(table => (int?)table.Number)
                        .FirstOrDefault(),
                    order.ServiceType.ToString(),
                    order.Status.ToString(),
                    order.RejectionReason,
                    order.SubmittedAt,
                    order.ResponseDeadline,
                    order.TotalAmount,
                    order.Items
                        .OrderBy(item => item.LineNumber)
                        .Select(item => new GuestOrderEntryItem(
                            item.ProductName,
                            item.SizeName,
                            item.Quantity,
                            item.Addons.OrderBy(addon => addon.Id).Select(addon => addon.AddonName).ToList()))
                        .ToList())
                {
                    BillReference = order.BillId == null
                        ? null
                        : context.Bills
                            .Where(bill => bill.Id == order.BillId)
                            .Select(bill => bill.PublicReference)
                            .FirstOrDefault(),
                })
                .SingleOrDefaultAsync(cancellationToken);
    }
}
