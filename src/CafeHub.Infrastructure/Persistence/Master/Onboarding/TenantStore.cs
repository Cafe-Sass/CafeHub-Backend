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

        public async Task<global::CafeHub.Domain.Tenants.Tenant?> FindByIdAsync(
            Guid tenantId,
            CancellationToken cancellationToken)
        {
            return await context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(tenant => tenant.Id == tenantId, cancellationToken);
        }

        public Task AddAsync(
            global::CafeHub.Domain.Tenants.Tenant tenant,
            CancellationToken cancellationToken)
        {
            context.Tenants.Add(tenant);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            global::CafeHub.Domain.Tenants.Tenant tenant,
            CancellationToken cancellationToken)
        {
            context.Tenants.Update(tenant);
            return Task.CompletedTask;
        }
    }
}
