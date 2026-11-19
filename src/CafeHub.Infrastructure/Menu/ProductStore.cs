using System.Globalization;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Menu;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Menu.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Menu
{
    internal sealed class ProductStore(
        TenantDbContext context, ITenantAuditWriter auditWriter, TimeProvider timeProvider, IProductCatalogSchema catalogSchema)
        : IProductStore
    {
        private const string TargetType = "products";
        private const string ActiveSizeDefaultIndex = "uq_product_sizes_active_default";
        private const string ActiveSizeNameIndex = "uq_product_sizes_active_name";
        private const string ActiveAddonNameIndex = "uq_product_addons_active_name";

        public Task<Result<ProductResponse>> CreateAsync(
            ProductWriteModel product, Guid actorId, CancellationToken cancellationToken) =>
            catalogSchema.RunAsync(() => CreateCoreAsync(product, actorId, cancellationToken), cancellationToken);

        public Task<Result<ProductResponse>> UpdateAsync(
            Guid id, ProductWriteModel product, uint version, Guid actorId, CancellationToken cancellationToken) =>
            catalogSchema.RunAsync(() => UpdateCoreAsync(id, product, version, actorId, cancellationToken), cancellationToken);

        public Task<Result<ProductResponse>> ChangeStatusAsync(
            Guid id, ProductStatus status, uint version, Guid actorId, CancellationToken cancellationToken,
            bool includeHiddenCategories = false) =>
            catalogSchema.RunAsync(() => ChangeStatusCoreAsync(id, status, version, actorId, cancellationToken,
                includeHiddenCategories), cancellationToken);

        public Task<Result<OffsetPage<ProductListItemResponse>>> ListAsync(
            Guid? categoryId,
            ProductKind? kind,
            bool? caffeine,
            string? tag,
            ProductStatus? status,
            string? search,
            int page,
            int pageSize,
            IReadOnlyList<SortTerm> sort,
            bool includeTotalCount,
            CancellationToken cancellationToken,
            bool includeHiddenCategories = false) =>
            catalogSchema.RunAsync(() => ListCoreAsync(categoryId, kind, caffeine, tag, status, search, page, pageSize,
                sort, includeTotalCount, cancellationToken, includeHiddenCategories), cancellationToken, includeTotalCount);

        public Task<Result<ProductResponse>> GetAsync(
            Guid id, CancellationToken cancellationToken, bool includeHiddenCategories = false) =>
            catalogSchema.RunAsync(() => GetCoreAsync(id, cancellationToken, includeHiddenCategories), cancellationToken);

        private async Task<Result<ProductResponse>> CreateCoreAsync(
            ProductWriteModel product, Guid actorId, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(product);

            if (!await LockVisibleCategoryAsync(product.CategoryId, cancellationToken))
            {
                return Result.Failure<ProductResponse>(ProductErrors.InvalidCategory());
            }

            if (product.StationId is not null)
            {
                return Result.Failure<ProductResponse>(ProductErrors.InvalidStation());
            }

            if (product.Tags.Any(tag => tag.Id is not null))
            {
                return Result.Failure<ProductResponse>(ProductErrors.InvalidTags());
            }

            var now = timeProvider.GetUtcNow();
            var created = Product.Create(
                Guid.CreateVersion7(),
                product.CategoryId,
                null,
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
                product.Tags.Select(tag => tag.Tag),
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

        private async Task<Result<ProductResponse>> UpdateCoreAsync(
            Guid id,
            ProductWriteModel product,
            uint version,
            Guid actorId,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(product);

            if (!await LockVisibleCategoryAsync(product.CategoryId, cancellationToken))
            {
                return Result.Failure<ProductResponse>(ProductErrors.InvalidCategory());
            }

            if (product.StationId is not null)
            {
                return Result.Failure<ProductResponse>(ProductErrors.InvalidStation());
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
            var previousDefault = existing.Sizes.SingleOrDefault(size =>
                size.Status == ProductComponentStatus.Active && size.IsDefault);
            var before = TenantAuditChangeSets.ProductFields(existing);
            var activeSizeNames = existing.Sizes.Where(size => size.Status == ProductComponentStatus.Active)
                .ToDictionary(size => size.Id, size => size.Name);
            var activeAddonNames = existing.Addons.Where(addon => addon.Status == ProductComponentStatus.Active)
                .ToDictionary(addon => addon.Id, addon => addon.Name);
            var activeTagLabels = existing.Tags.Where(tag => tag.Status == ProductComponentStatus.Active)
                .ToDictionary(tag => tag.Id, tag => tag.Tag);

            var now = timeProvider.GetUtcNow();
            var changed = existing.Update(
                product.CategoryId,
                existing.StationId,
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
                changed |= existing.ReplaceTagsById(product.Tags, now, actorId);
            }
            catch (ArgumentException)
            {
                return Result.Failure<ProductResponse>(ProductErrors.InvalidTags());
            }

            if (!changed)
            {
                return Result.Success(ToResponse(existing, version));
            }

            var releasedSizeNames = existing.Sizes.Where(size => activeSizeNames.TryGetValue(size.Id, out var oldName)
                    && (size.Status != ProductComponentStatus.Active || size.Name != oldName)).ToArray();
            if (releasedSizeNames.Length > 0)
            {
                var ids = releasedSizeNames.Select(size => size.Id).ToArray();
                var released = await context.ProductSizes.Where(size => size.ProductId == id
                    && ids.Contains(size.Id) && size.Status == ProductComponentStatus.Active)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(size => size.Status, ProductComponentStatus.Inactive),
                        cancellationToken);
                if (released != ids.Length)
                {
                    return Result.Failure<ProductResponse>(ProductErrors.ConcurrencyConflict());
                }

                foreach (var size in releasedSizeNames.Where(size => size.Status == ProductComponentStatus.Active))
                {
                    context.Entry(size).Property(item => item.Status).IsModified = true;
                }
            }

            var releasedAddonNames = existing.Addons.Where(addon => activeAddonNames.TryGetValue(addon.Id, out var oldName)
                    && (addon.Status != ProductComponentStatus.Active || addon.Name != oldName)).ToArray();
            if (releasedAddonNames.Length > 0)
            {
                var ids = releasedAddonNames.Select(addon => addon.Id).ToArray();
                var released = await context.ProductAddons.Where(addon => addon.ProductId == id
                    && ids.Contains(addon.Id) && addon.Status == ProductComponentStatus.Active)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(addon => addon.Status, ProductComponentStatus.Inactive),
                        cancellationToken);
                if (released != ids.Length)
                {
                    return Result.Failure<ProductResponse>(ProductErrors.ConcurrencyConflict());
                }

                foreach (var addon in releasedAddonNames.Where(addon => addon.Status == ProductComponentStatus.Active))
                {
                    context.Entry(addon).Property(item => item.Status).IsModified = true;
                }
            }

            if (previousDefault is not null && !releasedSizeNames.Contains(previousDefault)
                && (previousDefault.Status != ProductComponentStatus.Active || !previousDefault.IsDefault))
            {
                var released = await context.ProductSizes
                    .Where(size => size.ProductId == id && size.Id == previousDefault.Id
                        && size.Status == ProductComponentStatus.Active && size.IsDefault)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(size => size.Status, previousDefault.Status)
                        .SetProperty(size => size.IsDefault, previousDefault.IsDefault), cancellationToken);
                if (released != 1)
                {
                    return Result.Failure<ProductResponse>(ProductErrors.ConcurrencyConflict());
                }
            }

            var changedTags = existing.Tags.Where(tag => activeTagLabels.TryGetValue(tag.Id, out var oldLabel)
                && (tag.Status != ProductComponentStatus.Active || tag.Tag != oldLabel)).ToArray();
            if (changedTags.Length > 0)
            {
                var ids = changedTags.Select(tag => tag.Id).ToArray();
                var released = await context.ProductTags.Where(tag => tag.ProductId == id
                    && ids.Contains(tag.Id) && tag.Status == ProductComponentStatus.Active)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(tag => tag.Status, ProductComponentStatus.Inactive),
                        cancellationToken);
                if (released != ids.Length)
                {
                    return Result.Failure<ProductResponse>(ProductErrors.ConcurrencyConflict());
                }

                foreach (var tag in changedTags.Where(tag => tag.Status == ProductComponentStatus.Active))
                {
                    context.Entry(tag).Property(item => item.Status).IsModified = true;
                }
            }

            context.Entry(existing).Property(row => row.UpdatedAt).IsModified = true;

            context.Entry(existing).Property<uint>("xmin").OriginalValue = version;
            return await SaveAsync(
                existing,
                actorId,
                "products.updated.succeeded",
                TenantAuditChangeSets.ProductUpdate(before, existing),
                cancellationToken);
        }

        private async Task<Result<ProductResponse>> ChangeStatusCoreAsync(
            Guid id, ProductStatus status, uint version, Guid actorId, CancellationToken cancellationToken,
            bool includeHiddenCategories)
        {
            var candidates = CatalogProducts(includeHiddenCategories).Where(row => row.Id == id);
            if (!includeHiddenCategories && !await candidates.AnyAsync(cancellationToken))
            {
                return Result.Failure<ProductResponse>(ProductErrors.NotFound());
            }

            await context.LockProductAsync(id, cancellationToken);
            // Recheck visibility after the lock in case the product or its category changed while waiting.
            var product = await candidates.AsTracking().SingleOrDefaultAsync(cancellationToken);
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

        private async Task<Result<OffsetPage<ProductListItemResponse>>> ListCoreAsync(
            Guid? categoryId,
            ProductKind? kind,
            bool? caffeine,
            string? tag,
            ProductStatus? status,
            string? search,
            int page,
            int pageSize,
            IReadOnlyList<SortTerm> sort,
            bool includeTotalCount,
            CancellationToken cancellationToken,
            bool includeHiddenCategories = false)
        {
            ArgumentNullException.ThrowIfNull(sort);

            var query = CatalogProducts(includeHiddenCategories);
            if (status is { } statusFilter)
            {
                query = query.Where(product => product.Status == statusFilter);
            }
            else
            {
                query = query.Where(product => product.Status == ProductStatus.Active);
            }

            if (categoryId is { } categoryIdValue)
            {
                query = query.Where(product => context.MenuCategories.Any(category =>
                    category.Id == product.CategoryId
                    && (category.Id == categoryIdValue || category.ParentId == categoryIdValue)));
            }

            if (kind is { } kindFilter)
            {
                query = query.Where(product => product.Kind == kindFilter);
            }

            if (caffeine is { } caffeineFilter)
            {
                query = query.Where(product => product.Caffeine == caffeineFilter);
            }

            if (!string.IsNullOrWhiteSpace(tag))
            {
                var normalizedTag = tag.Trim().ToLowerInvariant();
                query = query.Where(product => product.Tags.Any(tagRow => tagRow.Status == ProductComponentStatus.Active && tagRow.Tag == normalizedTag));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{EscapeLike(search.Trim())}%";
                query = query.Where(product => EF.Functions.ILike(product.Name, pattern, @"\"));
            }


            int? totalCount = includeTotalCount ? await query.CountAsync(cancellationToken) : null;

            var fetchedItems = await ApplySort(query, sort)
                .Skip(checked((page - 1) * pageSize))
                .Take(pageSize + 1)
                .Select(product => new
                {
                    Product = product,
                    DefaultSizePrice = product.Sizes
                        .Where(size => size.IsDefault && size.Status == ProductComponentStatus.Active)
                        .Select(size => (decimal?)size.Price)
                        .FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);

            return Result.Success(OffsetPage<ProductListItemResponse>.Create(
                fetchedItems.Select(row => ProductListItemResponse.From(row.Product, row.DefaultSizePrice)).ToArray(),
                page,
                pageSize,
                totalCount));
        }

        private async Task<Result<ProductResponse>> GetCoreAsync(
            Guid id, CancellationToken cancellationToken, bool includeHiddenCategories = false)
        {
            var row = await CatalogProducts(includeHiddenCategories)
                .Include(product => product.Sizes)
                .Include(product => product.Addons)
                .Include(product => product.Tags)
                .Where(product => product.Id == id)
                .Select(product => new
                {
                    Product = product,
                    Version = EF.Property<uint>(product, "xmin"),
                })
                .SingleOrDefaultAsync(cancellationToken);
            return row is null
                ? Result.Failure<ProductResponse>(ProductErrors.NotFound())
                : Result.Success(ProductResponse.From(row.Product, row.Version.ToString(CultureInfo.InvariantCulture)));
        }

        private static IOrderedQueryable<Product> ApplySort(
            IQueryable<Product> query,
            IReadOnlyList<SortTerm> sort)
        {
            if (sort.Count == 0)
            {
                throw new InvalidOperationException("The products list requires at least one resolved sort term.");
            }

            var ordered = OrderBy(query, sort[0]);
            for (var index = 1; index < sort.Count; index++)
            {
                ordered = ThenBy(ordered, sort[index]);
            }

            return ordered;
        }

        private static IOrderedQueryable<Product> OrderBy(IQueryable<Product> query, SortTerm term)
        {
            return (term.Field, term.Direction) switch
            {
                (ProductListSort.SortOrderField, SortDirection.Asc) => query.OrderBy(product => product.SortOrder),
                (ProductListSort.SortOrderField, SortDirection.Desc) => query.OrderByDescending(product => product.SortOrder),
                (ProductListSort.NameField, SortDirection.Asc) => query.OrderBy(product => product.Name),
                (ProductListSort.NameField, SortDirection.Desc) => query.OrderByDescending(product => product.Name),
                (ProductListSort.CreatedAtField, SortDirection.Asc) => query.OrderBy(product => product.CreatedAt),
                (ProductListSort.CreatedAtField, SortDirection.Desc) => query.OrderByDescending(product => product.CreatedAt),
                (SortWhitelist.IdentifierField, SortDirection.Asc) => query.OrderBy(product => product.Id),
                (SortWhitelist.IdentifierField, SortDirection.Desc) => query.OrderByDescending(product => product.Id),
                _ => throw new InvalidOperationException(
                    $"The products list sort term '{term.Field}' with direction '{term.Direction}' is not supported."),
            };
        }

        private static IOrderedQueryable<Product> ThenBy(IOrderedQueryable<Product> query, SortTerm term)
        {
            return (term.Field, term.Direction) switch
            {
                (ProductListSort.SortOrderField, SortDirection.Asc) => query.ThenBy(product => product.SortOrder),
                (ProductListSort.SortOrderField, SortDirection.Desc) => query.ThenByDescending(product => product.SortOrder),
                (ProductListSort.NameField, SortDirection.Asc) => query.ThenBy(product => product.Name),
                (ProductListSort.NameField, SortDirection.Desc) => query.ThenByDescending(product => product.Name),
                (ProductListSort.CreatedAtField, SortDirection.Asc) => query.ThenBy(product => product.CreatedAt),
                (ProductListSort.CreatedAtField, SortDirection.Desc) => query.ThenByDescending(product => product.CreatedAt),
                (SortWhitelist.IdentifierField, SortDirection.Asc) => query.ThenBy(product => product.Id),
                (SortWhitelist.IdentifierField, SortDirection.Desc) => query.ThenByDescending(product => product.Id),
                _ => throw new InvalidOperationException(
                    $"The products list sort term '{term.Field}' with direction '{term.Direction}' is not supported."),
            };
        }

        private static string EscapeLike(string value)
        {
            return value
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_");
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

        private IQueryable<Product> CatalogProducts(bool includeHiddenCategories)
        {
            var query = context.Products.AsNoTracking();
            return includeHiddenCategories
                ? query
                : query.Where(product => context.MenuCategories.EffectivelyVisible()
                    .Any(category => category.Id == product.CategoryId));
        }

        private async Task<bool> LockVisibleCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
        {
            var key = await ReadCategoryAsync();
            if (key is null)
            {
                return false;
            }

            if (key.Value.ParentId is { } rootId)
            {
                await context.LockMenuCategoryAsync(rootId, cancellationToken);
            }

            await context.LockMenuCategoryAsync(categoryId, cancellationToken);
            var category = await ReadCategoryAsync();
            if (category is null || category.Value.Status != MenuCategoryStatus.Active)
            {
                return false;
            }

            if (category.Value.ParentId is not { } parentId)
            {
                return true;
            }

            var parentStatus = await context.MenuCategories.AsNoTracking()
                .Where(row => row.Id == parentId)
                .Select(row => (MenuCategoryStatus?)row.Status)
                .SingleOrDefaultAsync(cancellationToken);
            return parentStatus == MenuCategoryStatus.Active;

            async Task<(Guid? ParentId, MenuCategoryStatus Status)?> ReadCategoryAsync()
            {
                var row = await context.MenuCategories.AsNoTracking()
                    .Where(item => item.Id == categoryId)
                    .Select(item => new { item.ParentId, item.Status })
                    .SingleOrDefaultAsync(cancellationToken);
                return row is null ? null : (row.ParentId, row.Status);
            }
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
    }
}
