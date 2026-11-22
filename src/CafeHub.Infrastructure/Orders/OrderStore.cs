using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Orders;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Menu;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Orders
{
    internal sealed class OrderStore(TenantDbContext context) : IOrderStore
    {
        private const string PublicReferenceIndex = "uq_orders_public_reference";
        private const string OrderNumberIndex = "uq_orders_branch_business_date_number";

        public async Task<OrderCatalog> LoadCatalogAsync(
            Guid branchId,
            IReadOnlyCollection<Guid> productIds,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(productIds);

            if (productIds.Count == 0)
            {
                return new OrderCatalog([]);
            }

            var requested = productIds.ToArray();
            var rows = await context.Products
                .Where(product => requested.Contains(product.Id))
                .Select(product => new
                {
                    product.Id,
                    product.Name,
                    product.StationId,
                    product.Status,
                    IsVisible = context.MenuCategories.EffectivelyVisible()
                        .Any(category => category.Id == product.CategoryId),
                    IsAvailableInBranch = !context.BranchProductAvailability.Any(row =>
                        row.BranchId == branchId && row.ProductId == product.Id && !row.IsAvailable),
                    Sizes = context.ProductSizes
                        .Where(size => size.ProductId == product.Id
                            && size.Status == ProductComponentStatus.Active)
                        .OrderBy(size => size.Name)
                        .Select(size => new { size.Id, size.Name, size.Price })
                        .ToList(),
                    Addons = context.ProductAddons
                        .Where(addon => addon.ProductId == product.Id
                            && addon.Status == ProductComponentStatus.Active)
                        .OrderBy(addon => addon.Name)
                        .Select(addon => new { addon.Id, addon.Name, addon.Price })
                        .ToList(),
                })
                .ToListAsync(cancellationToken);

            return new OrderCatalog(rows
                .Select(row => new OrderCatalogProduct(
                    row.Id,
                    row.Name,
                    row.StationId,
                    row.Status,
                    row.IsVisible,
                    row.IsAvailableInBranch,
                    row.Sizes
                        .Select(size => new OrderCatalogSize(size.Id, size.Name, size.Price))
                        .ToArray(),
                    row.Addons
                        .Select(addon => new OrderCatalogAddon(addon.Id, addon.Name, addon.Price))
                        .ToArray()))
                .ToArray());
        }

        public async Task<Result<SubmitOrderResult>> AddAsync(Order order, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(order);

            context.Orders.Add(order);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
            } violation)
            {
                context.Entry(order).State = EntityState.Detached;
                if (violation.ConstraintName == PublicReferenceIndex)
                {
                    return Result.Failure<SubmitOrderResult>(OrdersErrors.ReferenceConflict());
                }

                if (violation.ConstraintName == OrderNumberIndex)
                {
                    return Result.Failure<SubmitOrderResult>(OrdersErrors.OrderNumberConflict());
                }

                throw;
            }

            return Result.Success(new SubmitOrderResult(
                order.OrderNumber,
                order.PublicReference,
                order.Status.ToString(),
                order.TotalAmount,
                order.ResponseDeadline));
        }
    }
}
