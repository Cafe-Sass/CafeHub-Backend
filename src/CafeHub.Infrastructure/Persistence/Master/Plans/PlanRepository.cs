using CafeHub.Application.Pagination;
using CafeHub.Application.Plans.Contracts;
using CafeHub.Application.Plans.Queries;
using CafeHub.Domain.Plans;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Master.Plans
{
    internal sealed class PlanRepository(MasterDbContext context) : IPlanRepository
    {
        public Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken)
        {
            return context.Plans
                .AsNoTracking()
                .AnyAsync(plan => plan.Code == key, cancellationToken);
        }

        public void Add(Plan plan)
        {
            context.Plans.Add(plan);
        }

        public Task<Plan?> FindByIdWithVersionsAsync(Guid planId, CancellationToken cancellationToken)
        {
            return context.Plans
                .Include(plan => plan.Versions)
                .ThenInclude(version => version.Entitlements)
                .AsSplitQuery()
                .SingleOrDefaultAsync(plan => plan.Id == planId, cancellationToken);
        }

        public async Task<PlanListPage> ListAsync(PlanListRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            int? totalCount = request.IncludeTotalCount
                ? await context.Plans.AsNoTracking().CountAsync(cancellationToken)
                : null;

            var query = context.Plans
                .AsNoTracking()
                .Include(plan => plan.Versions)
                .ThenInclude(version => version.Entitlements)
                .AsSplitQuery();

            var fetchedItems = await ApplySort(query, request.Sort)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize + 1)
                .ToListAsync(cancellationToken);

            return new PlanListPage(fetchedItems, totalCount);
        }

        private static IOrderedQueryable<Plan> ApplySort(IQueryable<Plan> query, IReadOnlyList<SortTerm> sort)
        {
            if (sort is null || sort.Count == 0)
            {
                throw new InvalidOperationException("The plans list requires at least one resolved sort term.");
            }

            var ordered = OrderBy(query, sort[0]);
            for (var index = 1; index < sort.Count; index++)
            {
                ordered = ThenBy(ordered, sort[index]);
            }

            return ordered;
        }

        private static IOrderedQueryable<Plan> OrderBy(IQueryable<Plan> query, SortTerm term)
        {
            return (term.Field, term.Direction) switch
            {
                (PlanListSort.KeyField, SortDirection.Asc) => query.OrderBy(plan => plan.Code),
                (PlanListSort.KeyField, SortDirection.Desc) => query.OrderByDescending(plan => plan.Code),
                (PlanListSort.CreatedAtField, SortDirection.Asc) => query.OrderBy(plan => plan.CreatedAt),
                (PlanListSort.CreatedAtField, SortDirection.Desc) => query.OrderByDescending(plan => plan.CreatedAt),
                (SortWhitelist.IdentifierField, SortDirection.Asc) => query.OrderBy(plan => plan.Id),
                (SortWhitelist.IdentifierField, SortDirection.Desc) => query.OrderByDescending(plan => plan.Id),
                _ => throw UnsupportedSortTerm(term),
            };
        }

        private static IOrderedQueryable<Plan> ThenBy(IOrderedQueryable<Plan> query, SortTerm term)
        {
            return (term.Field, term.Direction) switch
            {
                (PlanListSort.KeyField, SortDirection.Asc) => query.ThenBy(plan => plan.Code),
                (PlanListSort.KeyField, SortDirection.Desc) => query.ThenByDescending(plan => plan.Code),
                (PlanListSort.CreatedAtField, SortDirection.Asc) => query.ThenBy(plan => plan.CreatedAt),
                (PlanListSort.CreatedAtField, SortDirection.Desc) => query.ThenByDescending(plan => plan.CreatedAt),
                (SortWhitelist.IdentifierField, SortDirection.Asc) => query.ThenBy(plan => plan.Id),
                (SortWhitelist.IdentifierField, SortDirection.Desc) => query.ThenByDescending(plan => plan.Id),
                _ => throw UnsupportedSortTerm(term),
            };
        }

        private static InvalidOperationException UnsupportedSortTerm(SortTerm term)
        {
            return new InvalidOperationException(
                $"The plans list sort term '{term.Field}' with direction '{term.Direction}' is not supported.");
        }
    }
}
