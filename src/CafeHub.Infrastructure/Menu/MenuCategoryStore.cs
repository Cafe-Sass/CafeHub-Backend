using System.Data;
using System.Globalization;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Menu;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Menu
{
    internal sealed class MenuCategoryStore(TenantDbContext context, ITenantAuditWriter auditWriter, TimeProvider timeProvider)
        : IMenuCategoryStore
    {
        private const string TargetType = "menu_categories";

        public async Task<Result<MenuCategoryResponse>> CreateAsync(
            Guid? parentId, string name, int sortOrder, Guid actorId, CancellationToken cancellationToken)
        {
            MenuCategory? parent = null;
            if (parentId is { } id)
            {
                await context.LockMenuCategoryAsync(id, cancellationToken);
                parent = await context.MenuCategories.AsNoTracking()
                    .SingleOrDefaultAsync(category => category.Id == id, cancellationToken);
                if (parent is null || !parent.IsActiveRoot())
                {
                    return Result.Failure<MenuCategoryResponse>(MenuCategoryErrors.InvalidParent());
                }
            }

            var category = MenuCategory.Create(Guid.CreateVersion7(), parent, name, sortOrder, timeProvider.GetUtcNow(), actorId);
            context.MenuCategories.Add(category);
            var changes = TenantAuditChanges.Create(
                [
                    new(nameof(MenuCategory.ParentId), null, category.ParentId?.ToString()),
                    new(nameof(MenuCategory.Name), null, category.Name),
                    new(nameof(MenuCategory.SortOrder), null, category.SortOrder.ToString(CultureInfo.InvariantCulture)),
                    new(nameof(MenuCategory.Status), null, category.Status.ToString()),
                ]);
            return await SaveAsync(category, actorId, "menu_categories.created.succeeded", changes, cancellationToken);
        }

        public async Task<Result<MenuCategoryResponse>> UpdateAsync(
            Guid id, string name, int sortOrder, uint version, Guid actorId, CancellationToken cancellationToken)
        {
            var category = await LockAndLoadAsync(id, cancellationToken);
            if (category is null)
            {
                return Result.Failure<MenuCategoryResponse>(MenuCategoryErrors.NotFound());
            }

            if (GetVersion(category) != version)
            {
                return Result.Failure<MenuCategoryResponse>(MenuCategoryErrors.ConcurrencyConflict());
            }

            var oldName = category.Name;
            var oldOrder = category.SortOrder;
            if (!category.Update(name, sortOrder, timeProvider.GetUtcNow(), actorId))
            {
                return Result.Success(ToResponse(category, version));
            }

            context.Entry(category).Property<uint>("xmin").OriginalValue = version;
            var changes = TenantAuditChanges.Update(
                [
                    new(nameof(MenuCategory.Name), oldName, category.Name),
                    new(nameof(MenuCategory.SortOrder),
                        oldOrder.ToString(CultureInfo.InvariantCulture), category.SortOrder.ToString(CultureInfo.InvariantCulture)),
                ]);
            return await SaveAsync(category, actorId, "menu_categories.updated.succeeded", changes, cancellationToken);
        }

        public async Task<Result<MenuCategoryResponse>> ChangeStatusAsync(
            Guid id, MenuCategoryStatus status, uint version, Guid actorId, CancellationToken cancellationToken)
        {
            var category = await LockAndLoadAsync(id, cancellationToken);
            if (category is null)
            {
                return Result.Failure<MenuCategoryResponse>(MenuCategoryErrors.NotFound());
            }

            if (GetVersion(category) != version)
            {
                return Result.Failure<MenuCategoryResponse>(MenuCategoryErrors.ConcurrencyConflict());
            }

            if (category.Status == status)
            {
                return Result.Success(ToResponse(category, version));
            }

            MenuCategory? parent = null;
            if (status == MenuCategoryStatus.Active && category.ParentId is { } parentId)
            {
                parent = await context.MenuCategories.AsNoTracking()
                    .SingleOrDefaultAsync(root => root.Id == parentId, cancellationToken);
                if (parent is null || !parent.IsActiveRoot())
                {
                    return Result.Failure<MenuCategoryResponse>(MenuCategoryErrors.ParentInactive());
                }
            }

            var oldStatus = category.Status;
            category.ChangeStatus(status, parent, timeProvider.GetUtcNow(), actorId);
            context.Entry(category).Property<uint>("xmin").OriginalValue = version;
            var changes = TenantAuditChanges.Update([new(nameof(MenuCategory.Status), oldStatus.ToString(), status.ToString())]);
            var action = status == MenuCategoryStatus.Active
                ? "menu_categories.reactivated.succeeded"
                : "menu_categories.deactivated.succeeded";
            return await SaveAsync(category, actorId, action, changes, cancellationToken);
        }

        public async Task<MenuCategoryResponse?> GetVisibleAsync(Guid id, CancellationToken cancellationToken)
        {
            var row = await context.MenuCategories.AsNoTracking()
                .EffectivelyVisible()
                .Where(category => category.Id == id)
                .Select(category => new { Category = category, Version = EF.Property<uint>(category, "xmin") })
                .SingleOrDefaultAsync(cancellationToken);
            return row is null ? null : ToResponse(row.Category, row.Version);
        }

        public async Task<OffsetPage<MenuCategoryTreeNodeResponse>> GetTreeAsync(
            bool includeInactive, int page, int pageSize, bool includeTotalCount, CancellationToken cancellationToken)
        {
            var categories = context.MenuCategories.AsNoTracking();
            if (!includeInactive)
            {
                categories = categories.Where(category => category.Status == MenuCategoryStatus.Active);
            }

            var roots = categories.Where(category => category.ParentId == null);
            // The page and its children use one statement. An optional separate count shares its snapshot.
            await using var transaction = includeTotalCount
                ? await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
                : null;
            var totalCount = includeTotalCount ? await roots.CountAsync(cancellationToken) : (int?)null;
            var rootWindow = roots
                .OrderBy(category => category.SortOrder)
                .ThenBy(category => category.Id)
                .Skip(checked((page - 1) * pageSize));
            var lookahead = rootWindow.Take(checked(pageSize + 1)).Skip(pageSize);
            var rows = await rootWindow
                .Take(pageSize)
                .AsSingleQuery()
                .Select(root => new
                {
                    Category = root,
                    Version = EF.Property<uint>(root, "xmin"),
                    HasNextPage = lookahead.Any(),
                    Children = categories.Where(child => child.ParentId == root.Id)
                        .OrderBy(child => child.SortOrder)
                        .ThenBy(child => child.Id)
                        .Select(child => new { Category = child, Version = EF.Property<uint>(child, "xmin") })
                        .ToList(),
                })
                .ToListAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return new OffsetPage<MenuCategoryTreeNodeResponse>
            {
                Items = rows.Select(row => new MenuCategoryTreeNodeResponse(
                    row.Category.Id, row.Category.ParentId, row.Category.Name, row.Category.SortOrder,
                    row.Category.Status.ToString(), row.Version.ToString(CultureInfo.InvariantCulture),
                    row.Children.Select(child => ToResponse(child.Category, child.Version)).ToArray())).ToArray(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                HasNextPage = rows.FirstOrDefault()?.HasNextPage ?? false,
            };
        }

        private async Task<MenuCategory?> LockAndLoadAsync(Guid id, CancellationToken cancellationToken)
        {
            var key = await context.MenuCategories.AsNoTracking().Where(category => category.Id == id)
                .Select(category => new { category.ParentId })
                .SingleOrDefaultAsync(cancellationToken);
            if (key is null)
            {
                return null;
            }

            if (key.ParentId is { } parentId)
            {
                await context.LockMenuCategoryAsync(parentId, cancellationToken);
            }

            await context.LockMenuCategoryAsync(id, cancellationToken);
            return await context.MenuCategories.SingleOrDefaultAsync(category => category.Id == id, cancellationToken);
        }

        private async Task<Result<MenuCategoryResponse>> SaveAsync(
            MenuCategory category, Guid actorId, string action, TenantAuditChanges changes, CancellationToken cancellationToken)
        {
            var audit = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = action,
                TargetType = TargetType,
                TargetId = category.Id,
                ActorUserId = actorId,
                Changes = changes,
            }, cancellationToken);
            if (audit.IsFailure)
            {
                return Result.Failure<MenuCategoryResponse>(audit.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<MenuCategoryResponse>(MenuCategoryErrors.ConcurrencyConflict());
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "uq_menu_categories_active_root_name" or "uq_menu_categories_active_child_name",
            })
            {
                return Result.Failure<MenuCategoryResponse>(MenuCategoryErrors.NameConflict());
            }

            return Result.Success(ToResponse(category, GetVersion(category)));
        }

        private uint GetVersion(MenuCategory category) => context.Entry(category).Property<uint>("xmin").CurrentValue;

        private static MenuCategoryResponse ToResponse(MenuCategory category, uint version) =>
            new(category.Id, category.ParentId, category.Name, category.SortOrder, category.Status.ToString(),
                version.ToString(CultureInfo.InvariantCulture));
    }
}
