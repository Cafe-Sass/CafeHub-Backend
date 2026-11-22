using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Menu;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Menu
{
    internal sealed class BranchProductAvailabilityStore(
        TenantDbContext context,
        ITenantAuditWriter audit,
        TimeProvider timeProvider,
        IProductCatalogSchema catalogSchema) : IBranchProductAvailabilityStore
    {
        private const string TargetType = "products";

        public Task<Result<BranchProductAvailabilityResponse>> SetAsync(
            Guid branchId, Guid productId, bool available, string reason, Guid actorId,
            CancellationToken cancellationToken) =>
            catalogSchema.RunAsync(
                () => SetCoreAsync(branchId, productId, available, reason, actorId, cancellationToken),
                cancellationToken);

        public Task<Result<OffsetPage<BranchProductAvailabilityItem>>> ListAsync(
            Guid branchId, bool? available, Guid? categoryId, int page, int pageSize, bool includeTotalCount,
            CancellationToken cancellationToken) =>
            catalogSchema.RunAsync(
                () => ListCoreAsync(branchId, available, categoryId, page, pageSize, includeTotalCount, cancellationToken),
                cancellationToken,
                includeTotalCount);

        private async Task<Result<BranchProductAvailabilityResponse>> SetCoreAsync(
            Guid branchId, Guid productId, bool available, string reason, Guid actorId,
            CancellationToken cancellationToken)
        {
            await context.LockBranchesAsync(cancellationToken, branchId);
            var branchStatus = await context.Branches.AsNoTracking()
                .Where(branch => branch.Id == branchId)
                .Select(branch => (BranchLifecycleStatus?)branch.LifecycleStatus)
                .SingleOrDefaultAsync(cancellationToken);
            if (branchStatus is null)
            {
                return Result.Failure<BranchProductAvailabilityResponse>(ProductErrors.NotFound());
            }

            if (branchStatus != BranchLifecycleStatus.Active)
            {
                return Result.Failure<BranchProductAvailabilityResponse>(BranchProductAvailabilityErrors.BranchNotActive());
            }

            await context.LockProductForReferenceAsync(productId, cancellationToken);
            var productStatus = await context.Products.AsNoTracking()
                .Where(product => product.Id == productId)
                .Select(product => (ProductStatus?)product.Status)
                .SingleOrDefaultAsync(cancellationToken);
            if (productStatus is null)
            {
                return Result.Failure<BranchProductAvailabilityResponse>(ProductErrors.NotFound());
            }

            if (productStatus != ProductStatus.Active)
            {
                return Result.Failure<BranchProductAvailabilityResponse>(BranchProductAvailabilityErrors.ProductNotActive());
            }

            var row = await context.BranchProductAvailability
                .SingleOrDefaultAsync(item => item.BranchId == branchId && item.ProductId == productId, cancellationToken);
            var now = timeProvider.GetUtcNow();

            if (row is null)
            {
                // A missing row already means available, so only an unavailable state needs a row.
                if (available)
                {
                    return Result.Success(new BranchProductAvailabilityResponse(
                        branchId, productId, true, reason, now));
                }

                row = BranchProductAvailability.Create(branchId, productId, false, reason, now, actorId);
                context.BranchProductAvailability.Add(row);
                var created = await audit.WriteAsync(new TenantAuditEntry
                {
                    Action = "products.availability.changed",
                    TargetType = TargetType,
                    TargetId = productId,
                    ActorUserId = actorId,
                    Changes = TenantAuditChangeSets.BranchProductAvailabilityCreate(row),
                }, cancellationToken);
                return await SaveAsync(row, created, cancellationToken);
            }

            var previousAvailable = row.IsAvailable;
            var previousReason = row.ReasonCode;
            if (!row.Set(available, reason, now, actorId))
            {
                return Result.Success(ToResponse(row));
            }

            var updated = await audit.WriteAsync(new TenantAuditEntry
            {
                Action = "products.availability.changed",
                TargetType = TargetType,
                TargetId = productId,
                ActorUserId = actorId,
                Changes = TenantAuditChangeSets.BranchProductAvailabilityUpdate(
                    previousAvailable, previousReason, row),
            }, cancellationToken);
            return await SaveAsync(row, updated, cancellationToken);
        }

        private async Task<Result<OffsetPage<BranchProductAvailabilityItem>>> ListCoreAsync(
            Guid branchId, bool? available, Guid? categoryId, int page, int pageSize, bool includeTotalCount,
            CancellationToken cancellationToken)
        {
            var branchStatus = await context.Branches.AsNoTracking()
                .Where(branch => branch.Id == branchId)
                .Select(branch => (BranchLifecycleStatus?)branch.LifecycleStatus)
                .SingleOrDefaultAsync(cancellationToken);
            if (branchStatus != BranchLifecycleStatus.Active)
            {
                return Result.Failure<OffsetPage<BranchProductAvailabilityItem>>(ProductErrors.NotFound());
            }

            var query = context.Products.AsNoTracking()
                .Where(product => product.Status == ProductStatus.Active)
                .Where(product => context.MenuCategories.EffectivelyVisible()
                    .Any(category => category.Id == product.CategoryId));

            if (categoryId is { } categoryFilter)
            {
                query = query.Where(product => context.MenuCategories.Any(category =>
                    category.Id == product.CategoryId
                    && (category.Id == categoryFilter || category.ParentId == categoryFilter)));
            }

            if (available is { } availabilityFilter)
            {
                query = availabilityFilter
                    ? query.Where(product => !context.BranchProductAvailability.Any(row =>
                        row.BranchId == branchId && row.ProductId == product.Id && !row.IsAvailable))
                    : query.Where(product => context.BranchProductAvailability.Any(row =>
                        row.BranchId == branchId && row.ProductId == product.Id && !row.IsAvailable));
            }

            int? totalCount = includeTotalCount ? await query.CountAsync(cancellationToken) : null;

            var fetchedItems = await query
                .OrderBy(product => product.Name)
                .ThenBy(product => product.Id)
                .Skip(checked((page - 1) * pageSize))
                .Take(pageSize + 1)
                .Select(product => new
                {
                    Product = product,
                    Row = context.BranchProductAvailability
                        .Where(row => row.BranchId == branchId && row.ProductId == product.Id)
                        .Select(row => new { row.IsAvailable, row.ReasonCode })
                        .FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);

            return Result.Success(OffsetPage<BranchProductAvailabilityItem>.Create(
                fetchedItems.Select(row => new BranchProductAvailabilityItem(
                    row.Product.Id,
                    row.Product.Name,
                    row.Product.CategoryId,
                    row.Row is null || row.Row.IsAvailable,
                    row.Row is not null && !row.Row.IsAvailable ? row.Row.ReasonCode : null)).ToArray(),
                page,
                pageSize,
                totalCount));
        }

        private async Task<Result<BranchProductAvailabilityResponse>> SaveAsync(
            BranchProductAvailability row, Result auditResult, CancellationToken cancellationToken)
        {
            if (auditResult.IsFailure)
            {
                return Result.Failure<BranchProductAvailabilityResponse>(auditResult.Error!);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success(ToResponse(row));
        }

        private static BranchProductAvailabilityResponse ToResponse(BranchProductAvailability row) =>
            new(row.BranchId, row.ProductId, row.IsAvailable, row.ReasonCode, row.UpdatedAt);
    }
}
