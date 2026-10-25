using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Domain.Onboarding;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Master.Onboarding
{
    internal sealed class SlugAvailability(MasterDbContext context) : ISlugAvailability
    {
        public Task<bool> IsAvailableAsync(string slug, CancellationToken cancellationToken)
        {
            var taken = context.OnboardingRequests
                .AsNoTracking()
                .Where(request => request.TenantSlug == slug)
                .Where(request =>
                    request.Status != OnboardingRequestStatus.Approved
                    && request.Status != OnboardingRequestStatus.Rejected
                    && request.Status != OnboardingRequestStatus.Cancelled)
                .AnyAsync(cancellationToken);

            return taken.ContinueWith(task => !task.Result, cancellationToken);
        }
    }
}
