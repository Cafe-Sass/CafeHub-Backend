using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Master.Onboarding
{
    internal sealed class SlugAvailability(MasterDbContext context) : ISlugAvailability
    {
        public async Task<bool> IsAvailableAsync(string slug, CancellationToken cancellationToken)
        {
            var normalizedSlug = slug.ToLowerInvariant();

            var requestTaken = await context.OnboardingRequests
                .AsNoTracking()
                .Where(request => request.TenantSlug == normalizedSlug)
                .Where(request =>
                    request.Status != OnboardingRequestStatus.Approved
                    && request.Status != OnboardingRequestStatus.Rejected
                    && request.Status != OnboardingRequestStatus.Cancelled)
                .AnyAsync(cancellationToken);

            if (requestTaken)
            {
                return false;
            }

            var tenantTaken = await context.Tenants
                .AsNoTracking()
                .Where(tenant => tenant.Slug == normalizedSlug)
                .AnyAsync(cancellationToken);

            return !tenantTaken;
        }
    }
}
