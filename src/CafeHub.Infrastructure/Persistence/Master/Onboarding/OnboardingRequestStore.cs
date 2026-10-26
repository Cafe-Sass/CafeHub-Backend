using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Onboarding.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Onboarding;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Master.Onboarding
{
    internal sealed class OnboardingRequestStore(MasterDbContext context) : IOnboardingRequestStore
    {
        public Task<OnboardingRequest?> FindPublicByEmailAsync(string emailLower, CancellationToken cancellationToken)
        {
            return context.OnboardingRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(request => request.OwnerEmail == emailLower, cancellationToken);
        }

        public Task<OnboardingRequest?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return context.OnboardingRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(request => request.Id == id, cancellationToken);
        }

        public Task AddAsync(OnboardingRequest request, CancellationToken cancellationToken)
        {
            context.OnboardingRequests.Add(request);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(OnboardingRequest request, CancellationToken cancellationToken)
        {
            context.OnboardingRequests.Update(request);
            return Task.CompletedTask;
        }

        public async Task<OnboardingRequestListPage> ListForReviewAsync(OnboardingRequestListRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            int? totalCount = request.IncludeTotalCount
                ? await context.OnboardingRequests.AsNoTracking().CountAsync(item => item.Status == request.State, cancellationToken)
                : null;

            var query = context.OnboardingRequests
                .AsNoTracking()
                .Where(item => item.Status == request.State);

            var fetchedItems = await ApplySort(query, request.Sort)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize + 1)
                .ToListAsync(cancellationToken);

            return new OnboardingRequestListPage(fetchedItems, totalCount);
        }

        private static IOrderedQueryable<OnboardingRequest> ApplySort(IQueryable<OnboardingRequest> query, IReadOnlyList<SortTerm> sort)
        {
            if (sort is null || sort.Count == 0)
            {
                throw new InvalidOperationException("The onboarding review queue requires at least one resolved sort term.");
            }

            var ordered = OrderBy(query, sort[0]);
            for (var index = 1; index < sort.Count; index++)
            {
                ordered = ThenBy(ordered, sort[index]);
            }

            return ordered;
        }

        private static IOrderedQueryable<OnboardingRequest> OrderBy(IQueryable<OnboardingRequest> query, SortTerm term)
        {
            return (term.Field, term.Direction) switch
            {
                (OnboardingRequestListSort.CreatedAtField, SortDirection.Asc) => query.OrderBy(item => item.CreatedAt),
                (OnboardingRequestListSort.CreatedAtField, SortDirection.Desc) => query.OrderByDescending(item => item.CreatedAt),
                (OnboardingRequestListSort.StateField, SortDirection.Asc) => query.OrderBy(item => item.Status),
                (OnboardingRequestListSort.StateField, SortDirection.Desc) => query.OrderByDescending(item => item.Status),
                (SortWhitelist.IdentifierField, SortDirection.Asc) => query.OrderBy(item => item.Id),
                (SortWhitelist.IdentifierField, SortDirection.Desc) => query.OrderByDescending(item => item.Id),
                _ => throw UnsupportedSortTerm(term),
            };
        }

        private static IOrderedQueryable<OnboardingRequest> ThenBy(IOrderedQueryable<OnboardingRequest> query, SortTerm term)
        {
            return (term.Field, term.Direction) switch
            {
                (OnboardingRequestListSort.CreatedAtField, SortDirection.Asc) => query.ThenBy(item => item.CreatedAt),
                (OnboardingRequestListSort.CreatedAtField, SortDirection.Desc) => query.ThenByDescending(item => item.CreatedAt),
                (OnboardingRequestListSort.StateField, SortDirection.Asc) => query.ThenBy(item => item.Status),
                (OnboardingRequestListSort.StateField, SortDirection.Desc) => query.ThenByDescending(item => item.Status),
                (SortWhitelist.IdentifierField, SortDirection.Asc) => query.ThenBy(item => item.Id),
                (SortWhitelist.IdentifierField, SortDirection.Desc) => query.ThenByDescending(item => item.Id),
                _ => throw UnsupportedSortTerm(term),
            };
        }

        private static InvalidOperationException UnsupportedSortTerm(SortTerm term)
        {
            return new InvalidOperationException(
                $"The onboarding review queue sort term '{term.Field}' with direction '{term.Direction}' is not supported.");
        }
    }
}
