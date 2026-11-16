using System.Globalization;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Menu;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Menu
{
    internal sealed class ProductStore(TenantDbContext context, ITenantAuditWriter auditWriter, TimeProvider timeProvider)
        : IProductStore
    {
        private const string TargetType = "products";
        private const string ActiveSizeDefaultIndex = "uq_product_sizes_active_default";
        private const string ActiveSizeNameIndex = "uq_product_sizes_active_name";
        private const string ActiveAddonNameIndex = "uq_product_addons_active_name";

        public async Task<Result<ProductResponse>> CreateAsync(
            ProductWriteModel product, Guid actorId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(product);

            await context.LockMenuCategoryAsync(product.CategoryId, cancellationToken);
            if (!await IsCategoryVisibleAsync(product.CategoryId, cancellationToken))
            {
                return Result.Failure<ProductResponse>(ProductErrors.InvalidCategory());
            }

            if (product.StationId is { } stationId)
            {
                await context.LockStationAsync(stationId, cancellationToken);
                if (!await IsStationActiveAsync(stationId, cancellationToken))
                {
                    return Result.Failure<ProductResponse>(ProductErrors.InvalidStation());
                }
            }

            var now = timeProvider.GetUtcNow();
            var created = Product.Create(
                Guid.CreateVersion7(),
                product.CategoryId,
                product.StationId,
                product.Name,
                product.Description,
                product.Ingredients,
                product.Kind,
                product.Caffeine,
                product.PreparationMinutes,
                product.SortOrder,
                product.Sizes
                    .Select(size => ProductSize.Create(
                        Guid.CreateVersion7(), size.Name, size.Price, size.IsDefault, now, actorId))
                    .ToArray(),
                product.Addons
                    .Select(addon => ProductAddon.Create(
                        Guid.CreateVersion7(), addon.Name, addon.Price, now, actorId))
                    .ToArray(),
                product.Tags,
                now,
                actorId);

            context.Products.Add(created);
            return await SaveAsync(
                created,
                actorId,
                "products.created.succeeded",
                TenantAuditChangeSets.ProductCreate(created),
                cancellationToken);
        }

        public async Task<Result<ProductResponse>> UpdateAsync(
            Guid id,
            ProductWriteModel product,
            uint version,
            Guid actorId,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(product);

            await context.LockMenuCategoryAsync(product.CategoryId, cancellationToken);
            if (!await IsCategoryVisibleAsync(product.CategoryId, cancellationToken))
            {
                return Result.Failure<ProductResponse>(ProductErrors.InvalidCategory());
            }

            if (product.StationId is { } stationId)
            {
                await context.LockStationAsync(stationId, cancellationToken);
                if (!await IsStationActiveAsync(stationId, cancellationToken))
                {
                    return Result.Failure<ProductResponse>(ProductErrors.InvalidStation());
                }
            }

            var existing = await LockAndLoadAsync(id, cancellationToken);
            if (existing is null)
            {
                return Result.Failure<ProductResponse>(ProductErrors.NotFound());
            }

            if (GetVersion(existing) != version)
            {
                return Result.Failure<ProductResponse>(ProductErrors.ConcurrencyConflict());
            }

            await LoadComponentsAsync(existing, cancellationToken);
            var oldName = existing.Name;
            var oldKind = existing.Kind;
            var oldCaffeine = existing.Caffeine;
            var oldPreparationMinutes = existing.PreparationMinutes;
            var oldCategoryId = existing.CategoryId;
            var oldStationId = existing.StationId;
            var oldSizePrices = existing.Sizes.ToDictionary(size => size.Id, size => size.Price);
            var oldAddonPrices = existing.Addons.ToDictionary(addon => addon.Id, addon => addon.Price);

            var now = timeProvider.GetUtcNow();
            var changed = existing.Update(
                product.CategoryId,
                product.StationId,
                product.Name,
                product.Description,
                product.Ingredients,
                product.Kind,
                product.Caffeine,
                product.PreparationMinutes,
                product.SortOrder,
                now,
                actorId);

            try
            {
                changed |= existing.ReplaceSizes(product.Sizes, now, actorId);
            }
            catch (ArgumentException)
            {
                return Result.Failure<ProductResponse>(ProductErrors.InvalidSize());
            }

            try
            {
                changed |= existing.ReplaceAddons(product.Addons, now, actorId);
            }
            catch (ArgumentException)
            {
                return Result.Failure<ProductResponse>(ProductErrors.InvalidAddon());
            }

            try
            {
                changed |= existing.ReplaceTags(product.Tags, now, actorId);
            }
            catch (ArgumentException)
            {
                return Result.Failure<ProductResponse>(ProductErrors.InvalidTags());
            }

            if (!changed)
            {
                return Result.Success(ToResponse(existing, version));
            }

            var priceChanges = PriceChanges(product, oldSizePrices, oldAddonPrices);
            context.Entry(existing).Property<uint>("xmin").OriginalValue = version;
            return await SaveAsync(
                existing,
                actorId,
                "products.updated.succeeded",
                TenantAuditChangeSets.ProductUpdate(
                    existing,
                    oldName,
                    oldKind,
                    oldCaffeine,
                    oldPreparationMinutes,
                    oldCategoryId,
                    oldStationId,
                    priceChanges),
                cancellationToken);
        }

        public async Task<Result<ProductResponse>> ChangeStatusAsync(
            Guid id, ProductStatus status, uint version, Guid actorId, CancellationToken cancellationToken)
        {
            var product = await LockAndLoadAsync(id, cancellationToken);
            if (product is null)
            {
                return Result.Failure<ProductResponse>(ProductErrors.NotFound());
            }

            if (GetVersion(product) != version)
            {
                return Result.Failure<ProductResponse>(ProductErrors.ConcurrencyConflict());
            }

            await LoadComponentsAsync(product, cancellationToken);
            var oldStatus = product.Status;
            if (!product.ChangeStatus(status, timeProvider.GetUtcNow(), actorId))
            {
                return Result.Success(ToResponse(product, version));
            }

            context.Entry(product).Property<uint>("xmin").OriginalValue = version;
            var action = status == ProductStatus.Active
                ? "products.reactivated.succeeded"
                : "products.deactivated.succeeded";
            return await SaveAsync(
                product,
                actorId,
                action,
                TenantAuditChangeSets.ProductLifecycleStatus(product, oldStatus),
                cancellationToken);
        }

        private async Task<Product?> LockAndLoadAsync(Guid id, CancellationToken cancellationToken)
        {
            await context.LockProductAsync(id, cancellationToken);
            return await context.Products.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        }

        private async Task LoadComponentsAsync(Product product, CancellationToken cancellationToken)
        {
            await context.Entry(product).Collection(row => row.Sizes).LoadAsync(cancellationToken);
            await context.Entry(product).Collection(row => row.Addons).LoadAsync(cancellationToken);
            await context.Entry(product).Collection(row => row.Tags).LoadAsync(cancellationToken);
        }

        private async Task<bool> IsCategoryVisibleAsync(Guid categoryId, CancellationToken cancellationToken)
        {
            var category = await context.MenuCategories.AsNoTracking()
                .Where(row => row.Id == categoryId)
                .Select(row => new { row.ParentId, row.Status })
                .SingleOrDefaultAsync(cancellationToken);
            if (category is null || category.Status != MenuCategoryStatus.Active)
            {
                return false;
            }

            if (category.ParentId is not { } parentId)
            {
                return true;
            }

            var parentStatus = await context.MenuCategories.AsNoTracking()
                .Where(row => row.Id == parentId)
                .Select(row => (MenuCategoryStatus?)row.Status)
                .SingleOrDefaultAsync(cancellationToken);
            return parentStatus == MenuCategoryStatus.Active;
        }

        private async Task<bool> IsStationActiveAsync(Guid stationId, CancellationToken cancellationToken)
        {
            var status = await context.Stations.AsNoTracking()
                .Where(row => row.Id == stationId)
                .Select(row => (StationStatus?)row.Status)
                .SingleOrDefaultAsync(cancellationToken);
            return status == StationStatus.Active;
        }

        internal static List<TenantAuditChanges.FieldChange> PriceChanges(
            ProductWriteModel product,
            IReadOnlyDictionary<Guid, decimal> oldSizePrices,
            IReadOnlyDictionary<Guid, decimal> oldAddonPrices)
        {
            var changes = new List<TenantAuditChanges.FieldChange>();
            foreach (var size in product.Sizes)
            {
                if (size.Id is { } sizeId)
                {
                    if (oldSizePrices[sizeId] != size.Price)
                    {
                        changes.Add(new TenantAuditChanges.FieldChange(
                            $"price.{size.Name.Trim()}", FormatPrice(oldSizePrices[sizeId]), FormatPrice(size.Price)));
                    }
                }
                else
                {
                    changes.Add(new TenantAuditChanges.FieldChange(
                        $"price.{size.Name.Trim()}", null, FormatPrice(size.Price)));
                }
            }

            foreach (var addon in product.Addons)
            {
                if (addon.Id is { } addonId)
                {
                    if (oldAddonPrices[addonId] != addon.Price)
                    {
                        changes.Add(new TenantAuditChanges.FieldChange(
                            $"price.{addon.Name.Trim()}", FormatPrice(oldAddonPrices[addonId]), FormatPrice(addon.Price)));
                    }
                }
                else
                {
                    changes.Add(new TenantAuditChanges.FieldChange(
                        $"price.{addon.Name.Trim()}", null, FormatPrice(addon.Price)));
                }
            }

            return changes;
        }

        private async Task<Result<ProductResponse>> SaveAsync(
            Product product,
            Guid actorId,
            string action,
            TenantAuditChanges changes,
            CancellationToken cancellationToken)
        {
            var audit = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = action,
                TargetType = TargetType,
                TargetId = product.Id,
                ActorUserId = actorId,
                Changes = changes,
            }, cancellationToken);
            if (audit.IsFailure)
            {
                return Result.Failure<ProductResponse>(audit.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<ProductResponse>(ProductErrors.ConcurrencyConflict());
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: ActiveSizeDefaultIndex or ActiveSizeNameIndex or ActiveAddonNameIndex,
            } postgres)
            {
                return Result.Failure<ProductResponse>(
                    postgres.ConstraintName == ActiveSizeDefaultIndex
                        ? ProductErrors.DefaultSizeConflict()
                        : ProductErrors.NameConflict());
            }

            return Result.Success(ToResponse(product, GetVersion(product)));
        }

        private uint GetVersion(Product product) => context.Entry(product).Property<uint>("xmin").CurrentValue;

        private static ProductResponse ToResponse(Product product, uint version) =>
            ProductResponse.From(product, version.ToString(CultureInfo.InvariantCulture));

        private static string FormatPrice(decimal price) => price.ToString(CultureInfo.InvariantCulture);
    }
}
