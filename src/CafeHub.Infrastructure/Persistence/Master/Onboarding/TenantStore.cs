using CafeHub.Application.Onboarding.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Master.Onboarding
{
    internal sealed class TenantStore(MasterDbContext context) : ITenantStore
    {
        public Task<global::CafeHub.Domain.Tenants.Tenant?> FindByOnboardingRequestIdAsync(
            Guid onboardingRequestId,
            CancellationToken cancellationToken)
        {
            return context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(tenant => tenant.OnboardingRequestId == onboardingRequestId, cancellationToken);
        }

        public Task AddAsync(
            global::CafeHub.Domain.Tenants.Tenant tenant,
            CancellationToken cancellationToken)
        {
            context.Tenants.Add(tenant);
            return Task.CompletedTask;
        }
    }
}
