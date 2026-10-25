using CafeHub.Application.Onboarding.Contracts;
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
    }
}
