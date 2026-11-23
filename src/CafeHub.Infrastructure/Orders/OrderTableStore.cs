using CafeHub.Application.Orders.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Orders
{
    internal sealed class OrderTableStore(TenantDbContext context) : IOrderTableStore
    {
        public async Task<OrderTable?> FindActiveTableAsync(Guid tableId, CancellationToken cancellationToken)
        {
            var row = await context.BranchTables.AsNoTracking()
                .Where(table => table.Id == tableId && table.Status == BranchTableStatus.Active)
                .Select(table => new { table.BranchId, table.QrCode })
                .SingleOrDefaultAsync(cancellationToken);

            return row is null ? null : new OrderTable(row.BranchId, row.QrCode);
        }
    }
}
