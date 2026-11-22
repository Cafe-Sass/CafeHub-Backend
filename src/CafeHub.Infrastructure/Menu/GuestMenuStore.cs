using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Menu.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Menu
{
    internal sealed class GuestMenuStore(TenantDbContext context) : IGuestMenuStore
    {
        public async Task<OffsetPage<GuestMenuItemResponse>> ListAsync(
            Guid branchId,
            string? search,
            Guid? categoryId,
            ProductKind? kind,
            bool? caffeine,
            string? tag,
            int page,
            int pageSize,
            IReadOnlyList<SortTerm> sort,
            bool includeTotalCount,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(sort);

            var query = context.Products.AsNoTracking()
                .Where(product => product.Status == ProductStatus.Active)
                .Where(product => context.MenuCategories.EffectivelyVisible()
                    .Any(category => category.Id == product.CategoryId))
                .Where(product => !context.BranchProductAvailability.Any(row =>
                    row.BranchId == branchId && row.ProductId == product.Id && !row.IsAvailable));

            if (categoryId is { } categoryFilter)
            {
                query = query.Where(product => context.MenuCategories.Any(category =>
                    category.Id == product.CategoryId
                    && (category.Id == categoryFilter || category.ParentId == categoryFilter)));
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
                query = query.Where(product => product.Tags.Any(tagRow =>
                    tagRow.Status == ProductComponentStatus.Active && tagRow.Tag == normalizedTag));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{EscapeLike(search.Trim())}%";
                query = query.Where(product => EF.Functions.ILike(product.Name, pattern, @"\"));
            }

            int? totalCount = includeTotalCount ? await query.CountAsync(cancellationToken) : null;

            var rows = await ApplySort(query, sort)
                .Skip(checked((page - 1) * pageSize))
                .Take(pageSize + 1)
                .Select(product => new
                {
                    Product = product,
                    Category = context.MenuCategories
                        .Where(category => category.Id == product.CategoryId)
                        .Select(category => new
                        {
                            category.Name,
                            category.ParentId,
                            ParentName = category.Parent!.Name,
                        })
                        .FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                return OffsetPage<GuestMenuItemResponse>.Create([], page, pageSize, totalCount);
            }

            var productIds = rows.Take(pageSize).Select(row => row.Product.Id).ToArray();
            var sizes = await context.ProductSizes.AsNoTracking()
                .Where(size => productIds.Contains(size.ProductId) && size.Status == ProductComponentStatus.Active)
                .OrderBy(size => size.Name)
                .Select(size => new { size.ProductId, size.Id, size.Name, size.Price, size.IsDefault })
                .ToListAsync(cancellationToken);
            var addons = await context.ProductAddons.AsNoTracking()
                .Where(addon => productIds.Contains(addon.ProductId) && addon.Status == ProductComponentStatus.Active)
                .OrderBy(addon => addon.Name)
                .Select(addon => new { addon.ProductId, addon.Id, addon.Name, addon.Price })
                .ToListAsync(cancellationToken);
            var tags = await context.ProductTags.AsNoTracking()
                .Where(tagRow => productIds.Contains(tagRow.ProductId)
                    && tagRow.Status == ProductComponentStatus.Active)
                .OrderBy(tagRow => tagRow.Tag)
                .Select(tagRow => new { tagRow.ProductId, tagRow.Tag })
                .ToListAsync(cancellationToken);

            return OffsetPage<GuestMenuItemResponse>.Create(
                rows.Take(pageSize).Select(row => new GuestMenuItemResponse(
                    row.Product.Id,
                    row.Product.Name,
                    row.Product.Description,
                    row.Product.Kind.ToString(),
                    row.Product.Caffeine,
                    row.Product.PreparationMinutes,
                    row.Product.CategoryId,
                    row.Category?.Name ?? string.Empty,
                    row.Category?.ParentId,
                    row.Category?.ParentName,
                    sizes.Where(size => size.ProductId == row.Product.Id)
                        .Select(size => new GuestMenuSizeResponse(size.Id, size.Name, size.Price, size.IsDefault))
                        .ToArray(),
                    addons.Where(addon => addon.ProductId == row.Product.Id)
                        .Select(addon => new GuestMenuAddonResponse(addon.Id, addon.Name, addon.Price))
                        .ToArray(),
                    tags.Where(tagRow => tagRow.ProductId == row.Product.Id)
                        .Select(tagRow => tagRow.Tag)
                        .ToArray()))
                .ToArray(),
                page,
                pageSize,
                totalCount);
        }

        private static IOrderedQueryable<Product> ApplySort(IQueryable<Product> query, IReadOnlyList<SortTerm> sort)
        {
            if (sort.Count == 0)
            {
                throw new InvalidOperationException("The guest menu requires at least one resolved sort term.");
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
                    $"The guest menu sort term '{term.Field}' with direction '{term.Direction}' is not supported."),
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
                    $"The guest menu sort term '{term.Field}' with direction '{term.Direction}' is not supported."),
            };
        }

        private static string EscapeLike(string value)
        {
            return value
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_");
        }
    }
}
