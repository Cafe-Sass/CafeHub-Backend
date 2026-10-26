using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Provisioning;
using CafeHub.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Master.Onboarding
{
    internal sealed class OnboardingStatusReader(MasterDbContext context) : IOnboardingStatusReader
    {
        public Task<OnboardingStatusSnapshot?> FindByRequestIdAsync(Guid requestId, CancellationToken cancellationToken)
        {
            return context.OnboardingRequests
                .AsNoTracking()
                .Where(request => request.Id == requestId)
                .Select(request => new OnboardingStatusSnapshot(
                    request.Status,
                    context.Tenants
                        .Where(tenant => tenant.OnboardingRequestId == requestId)
                        .Select(tenant => (TenantLifecycleStatus?)tenant.LifecycleStatus)
                        .FirstOrDefault(),
                    context.Tenants
                        .Where(tenant => tenant.OnboardingRequestId == requestId)
                        .Join(
                            context.ProvisioningJobs,
                            tenant => tenant.Id,
                            job => job.TenantId,
                            (tenant, job) => (ProvisioningJobStatus?)job.Status)
                        .FirstOrDefault()))
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
